namespace Configurator.Presentation.Tests.Intake;

using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Configurator.Core.Definitions;
using Configurator.Presentation.Intake;
using Configurator.Presentation.Tests.Store;

public sealed class DefinitionRemoteFetcherTests
{
    private static readonly IPAddress PublicAddress = IPAddress.Parse("93.184.216.34");
    private static readonly TimeSpan TestDeadline = TimeSpan.FromSeconds(2);

    [Test]
    [Arguments("")]
    [Arguments("   ")]
    [Arguments("http://public.example/definition.json")]
    [Arguments("file:///tmp/definition.json")]
    [Arguments("https://127.0.0.1/definition.json")]
    [Arguments("https://127.0.0.1./definition.json")]
    [Arguments("https://[::1]/definition.json")]
    [Arguments("https://localhost/definition.json")]
    [Arguments("https://sub.localhost/definition.json")]
    [Arguments("https://printer.local/definition.json")]
    [Arguments("https://metadata/latest")]
    [Arguments("https://public.example/definition.json?answer=canary")]
    [Arguments("https://public.example/definition.json#canary")]
    [Arguments("https://user:synthetic-password@public.example/definition.json")]
    public async Task UnsafeUriIsRejectedBeforeResolutionOrConnection(string uri)
    {
        int resolutions = 0;
        int connections = 0;
        using var fetcher = new DefinitionRemoteFetcher(
            (_, _) => { resolutions++; return ValueTask.FromResult(new[] { PublicAddress }); },
            (_, _, _) => { connections++; throw new SocketException(); },
            TimeProvider.System);

        DefinitionRemoteResult result = await fetcher.FetchAsync(uri);

        await Assert.That(result).IsTypeOf<DefinitionRemoteResult.InvalidUri>();
        await Assert.That(resolutions).IsEqualTo(0);
        await Assert.That(connections).IsEqualTo(0);
    }

    [Test]
    [Arguments("0.0.0.0")]
    [Arguments("127.0.0.1")]
    [Arguments("10.0.0.1")]
    [Arguments("100.64.0.1")]
    [Arguments("169.254.169.254")]
    [Arguments("172.16.0.1")]
    [Arguments("192.0.2.1")]
    [Arguments("192.168.1.1")]
    [Arguments("198.18.0.1")]
    [Arguments("203.0.113.1")]
    [Arguments("224.0.0.1")]
    [Arguments("::")]
    [Arguments("::1")]
    [Arguments("::ffff:127.0.0.1")]
    [Arguments("::ffff:0:127.0.0.1")]
    [Arguments("64:ff9b::a9fe:a9fe")]
    [Arguments("100::1")]
    [Arguments("200::1")]
    [Arguments("2001::1")]
    [Arguments("2001:2::1")]
    [Arguments("2001:10::1")]
    [Arguments("2001:20::1")]
    [Arguments("2001:db8::1")]
    [Arguments("2002::1")]
    [Arguments("4000::1")]
    [Arguments("fc00::1")]
    [Arguments("fe80::1")]
    [Arguments("fec0::1")]
    [Arguments("ff02::1")]
    public async Task NonPublicResolutionIsBlockedBeforeSocketConnection(string address)
    {
        int connections = 0;
        using var fetcher = new DefinitionRemoteFetcher(
            (_, _) => ValueTask.FromResult(new[] { IPAddress.Parse(address) }),
            (_, _, _) => { connections++; throw new SocketException(); },
            TimeProvider.System);

        DefinitionRemoteResult result = await fetcher.FetchAsync("https://public.example/definition.json");

        await Assert.That(result).IsTypeOf<DefinitionRemoteResult.BlockedHost>();
        await Assert.That(connections).IsEqualTo(0);
    }

    [Test]
    public async Task MixedPublicAndPrivateResolutionFailsClosed()
    {
        int connections = 0;
        using var fetcher = new DefinitionRemoteFetcher(
            (_, _) => ValueTask.FromResult(new[] { PublicAddress, IPAddress.Loopback }),
            (_, _, _) => { connections++; throw new SocketException(); },
            TimeProvider.System);

        DefinitionRemoteResult result = await fetcher.FetchAsync("https://public.example/definition.json");

        await Assert.That(result).IsTypeOf<DefinitionRemoteResult.BlockedHost>();
        await Assert.That(connections).IsEqualTo(0);
    }

    [Test]
    public async Task DeclaredOversizePayloadIsRejectedWithoutReadingBody()
    {
        await using var server = new TestServer(async (stream, cancellationToken) =>
        {
            await WriteHeaders(stream, "200 OK", "application/json", DefinitionRemoteFetcher.MaximumPayloadBytes + 1L, cancellationToken);
        });
        using var fetcher = Fetcher(server);

        DefinitionRemoteResult result = await fetcher.FetchAsync("https://public.example/definition.json");

        await Assert.That(result).IsTypeOf<DefinitionRemoteResult.PayloadTooLarge>();
    }

    [Test]
    public async Task InfiniteStreamStopsAtPayloadBoundary()
    {
        await using var server = new TestServer(async (stream, cancellationToken) =>
        {
            await WriteHeaders(stream, "200 OK", "application/json", null, cancellationToken);
            byte[] chunk = new byte[81920];
            while (true) await stream.WriteAsync(chunk, cancellationToken);
        });
        using var fetcher = Fetcher(server);

        DefinitionRemoteResult result = await fetcher.FetchAsync("https://public.example/definition.json")
            .WaitAsync(TestDeadline);

        await Assert.That(result).IsTypeOf<DefinitionRemoteResult.PayloadTooLarge>();
    }

    [Test]
    public async Task FifteenSecondTimeoutUsesControlledClock()
    {
        var time = new ManualTimeProvider();
        await using var server = new TestServer((_, cancellationToken) =>
            Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken));
        using var fetcher = Fetcher(server, time);

        Task<DefinitionRemoteResult> pending = fetcher.FetchAsync("https://public.example/definition.json");
        await server.RequestReceived.Task.WaitAsync(TestDeadline);
        time.FireDueTimers();
        DefinitionRemoteResult result = await pending.WaitAsync(TestDeadline);

        await Assert.That(DefinitionRemoteFetcher.RequestTimeout).IsEqualTo(TimeSpan.FromSeconds(15));
        await Assert.That(result).IsTypeOf<DefinitionRemoteResult.NetworkError>();
    }

    [Test]
    public async Task CallerCancellationRemainsCancellation()
    {
        await using var server = new TestServer((_, cancellationToken) =>
            Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken));
        using var fetcher = Fetcher(server);
        using var cancellation = new CancellationTokenSource();

        Task<DefinitionRemoteResult> pending = fetcher.FetchAsync("https://public.example/definition.json", cancellation.Token);
        await server.RequestReceived.Task.WaitAsync(TestDeadline);
        await cancellation.CancelAsync();

        await Assert.That(async () => await pending.WaitAsync(TestDeadline)).Throws<OperationCanceledException>();
    }

    [Test]
    public async Task RedirectIsNotFollowed()
    {
        await using var server = new TestServer(async (stream, cancellationToken) =>
        {
            byte[] headers = Encoding.ASCII.GetBytes(
                "HTTP/1.1 302 Found\r\nLocation: http://127.0.0.1/private\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
            await stream.WriteAsync(headers, cancellationToken);
        });
        using var fetcher = Fetcher(server);

        DefinitionRemoteResult result = await fetcher.FetchAsync("https://public.example/definition.json");

        await Assert.That(result).IsTypeOf<DefinitionRemoteResult.NetworkError>();
        await Assert.That(server.RequestCount).IsEqualTo(1);
    }

    [Test]
    [Arguments("definition.json", "application/json")]
    [Arguments("definition.yaml", "application/yaml")]
    public async Task ValidRemoteDefinitionIsParsed(string path, string contentType)
    {
        Definition expected = EmbeddedDefinitionStoreProviderTests.Definition();
        byte[] body = expected.CanonicalBytes.ToArray();
        await using var server = new TestServer(async (stream, cancellationToken) =>
        {
            await WriteHeaders(stream, "200 OK", contentType, body.Length, cancellationToken);
            await stream.WriteAsync(body, cancellationToken);
        });
        using var fetcher = Fetcher(server);

        DefinitionRemoteResult result = await fetcher.FetchAsync($"https://public.example/{path}");

        await Assert.That(result).IsTypeOf<DefinitionRemoteResult.Success>();
        var success = (DefinitionRemoteResult.Success)result;
        await Assert.That(success.Definition.Digest).IsEqualTo(expected.Digest);
    }

    [Test]
    public async Task InvalidDefinitionReturnsValueSafeParseError()
    {
        byte[] body = Encoding.UTF8.GetBytes("{\"private-canary\":true}");
        await using var server = new TestServer(async (stream, cancellationToken) =>
        {
            await WriteHeaders(stream, "200 OK", "application/json", body.Length, cancellationToken);
            await stream.WriteAsync(body, cancellationToken);
        });
        using var fetcher = Fetcher(server);

        DefinitionRemoteResult result = await fetcher.FetchAsync("https://public.example/definition.json");

        await Assert.That(result).IsTypeOf<DefinitionRemoteResult.ParseError>();
    }

    [Test]
    public async Task EmptyRemoteResponseReturnsParseError()
    {
        await using var server = new TestServer(async (stream, cancellationToken) =>
            await WriteHeaders(stream, "200 OK", "application/json", 0, cancellationToken));
        using var fetcher = Fetcher(server);

        DefinitionRemoteResult result = await fetcher.FetchAsync("https://public.example/definition.json");

        await Assert.That(result).IsTypeOf<DefinitionRemoteResult.ParseError>();
    }

    private static DefinitionRemoteFetcher Fetcher(TestServer server, TimeProvider? timeProvider = null) => new(
        (_, _) => ValueTask.FromResult(new[] { PublicAddress }),
        (_, _, cancellationToken) => ConnectLoopbackAsync(server.Port, cancellationToken),
        timeProvider ?? TimeProvider.System,
        (_, certificate, _, _) => server.Certificate.Equals(certificate));

    private static async ValueTask<Stream> ConnectLoopbackAsync(int port, CancellationToken cancellationToken)
    {
        Socket? socket = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        try
        {
            await socket.ConnectAsync(new IPEndPoint(IPAddress.Loopback, port), cancellationToken);
            var stream = new NetworkStream(socket, ownsSocket: true);
            socket = null;
            return stream;
        }
        finally
        {
            socket?.Dispose();
        }
    }

    private static Task WriteHeaders(
        Stream stream,
        string status,
        string contentType,
        long? contentLength,
        CancellationToken cancellationToken)
    {
        string length = contentLength is null ? string.Empty : $"Content-Length: {contentLength}\r\n";
        byte[] headers = Encoding.ASCII.GetBytes(
            $"HTTP/1.1 {status}\r\nContent-Type: {contentType}\r\n{length}Connection: close\r\n\r\n");
        return stream.WriteAsync(headers, cancellationToken).AsTask();
    }

    private sealed class TestServer : IAsyncDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _lifetime = new();
        private readonly Task _completion;
        private int _requestCount;

        internal TestServer(Func<Stream, CancellationToken, Task> respond)
        {
            Certificate = CreateCertificate();
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            _completion = ServeAsync(respond);
        }

        internal int Port { get; }
        internal X509Certificate2 Certificate { get; }
        internal int RequestCount => _requestCount;
        internal TaskCompletionSource RequestReceived { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        private async Task ServeAsync(Func<Stream, CancellationToken, Task> respond)
        {
            using TcpClient client = await _listener.AcceptTcpClientAsync(_lifetime.Token);
            Interlocked.Increment(ref _requestCount);
            using NetworkStream transport = client.GetStream();
            await using var stream = new SslStream(transport);
            await stream.AuthenticateAsServerAsync(
                new SslServerAuthenticationOptions { ServerCertificate = Certificate }, _lifetime.Token);
            await ReadRequestHeadersAsync(stream, _lifetime.Token);
            RequestReceived.TrySetResult();
            await respond(stream, _lifetime.Token);
        }

        private static X509Certificate2 CreateCertificate()
        {
            using RSA key = RSA.Create(2048);
            var request = new CertificateRequest("CN=public.example", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            var names = new SubjectAlternativeNameBuilder();
            names.AddDnsName("public.example");
            request.CertificateExtensions.Add(names.Build());
            request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
                new OidCollection { new("1.3.6.1.5.5.7.3.1") }, critical: false));
            return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        }

        private static async Task ReadRequestHeadersAsync(Stream stream, CancellationToken cancellationToken)
        {
            byte[] header = new byte[4];
            for (int position = 0; position < 16 * 1024; position++)
            {
                int read = await stream.ReadAsync(header.AsMemory(3, 1), cancellationToken);
                if (read == 0) throw new IOException("Incomplete request headers.");
                if (header is [13, 10, 13, 10]) return;
                header[0] = header[1];
                header[1] = header[2];
                header[2] = header[3];
            }
            throw new IOException("Request headers exceeded the test bound.");
        }

        public async ValueTask DisposeAsync()
        {
            await _lifetime.CancelAsync();
            _listener.Stop();
            try { await _completion.WaitAsync(TestDeadline); }
            catch (Exception failure) when (failure is OperationCanceledException or IOException or SocketException) { }
            Certificate.Dispose();
            _lifetime.Dispose();
        }
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private readonly List<ManualTimer> _timers = [];

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new ManualTimer(callback, state);
            lock (_timers) _timers.Add(timer);
            return timer;
        }

        internal void FireDueTimers()
        {
            ManualTimer[] timers;
            lock (_timers) timers = _timers.ToArray();
            foreach (ManualTimer timer in timers) timer.Fire();
        }

        private sealed class ManualTimer(TimerCallback callback, object? state) : ITimer
        {
            private TimerCallback? _callback = callback;

            public bool Change(TimeSpan dueTime, TimeSpan period) => _callback is not null;
            public void Fire() => _callback?.Invoke(state);
            public void Dispose() => _callback = null;
            public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
        }
    }
}
