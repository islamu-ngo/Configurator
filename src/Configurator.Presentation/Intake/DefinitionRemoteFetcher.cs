namespace Configurator.Presentation.Intake;

using System.Buffers;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Text.Json;
using Configurator.Core;
using Configurator.Core.Definitions;

/// <summary>Fetches bounded public HTTPS definitions through connection-time destination checks.</summary>
public sealed class DefinitionRemoteFetcher : IDefinitionRemoteFetcher
{
    public const int MaximumPayloadBytes = 4 * 1024 * 1024;
    private const int ReadChunkBytes = 64 * 1024;
    public static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);
    public static readonly TimeSpan ConnectionLifetime = TimeSpan.FromMinutes(5);

    private readonly Func<string, CancellationToken, ValueTask<IPAddress[]>> _resolveAddresses;
    private readonly Func<IPAddress, int, CancellationToken, ValueTask<Stream>> _connect;
    private readonly TimeProvider _timeProvider;
    private readonly HttpClient _client;
    private bool _disposed;

    public DefinitionRemoteFetcher()
        : this(ResolveAddressesAsync, OpenSocketAsync, TimeProvider.System) { }

    internal DefinitionRemoteFetcher(
        Func<string, CancellationToken, ValueTask<IPAddress[]>> resolveAddresses,
        Func<IPAddress, int, CancellationToken, ValueTask<Stream>> connect,
        TimeProvider timeProvider,
        RemoteCertificateValidationCallback? certificateValidation = null)
    {
        _resolveAddresses = resolveAddresses;
        _connect = connect;
        _timeProvider = timeProvider;
        SocketsHttpHandler? handler = new()
        {
            AllowAutoRedirect = false,
            UseCookies = false,
            UseProxy = false,
            PooledConnectionLifetime = ConnectionLifetime,
            ConnectCallback = ConnectAsync
        };
        if (certificateValidation is not null)
            handler.SslOptions.RemoteCertificateValidationCallback = certificateValidation;
        try
        {
            _client = new HttpClient(handler, disposeHandler: true) { Timeout = Timeout.InfiniteTimeSpan };
            handler = null;
        }
        finally
        {
            handler?.Dispose();
        }
    }

    public async Task<DefinitionRemoteResult> FetchAsync(string uri, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!PublicRemoteEndpointPolicy.TryValidatePublicHttpsUri(uri, out Uri? endpoint))
            return new DefinitionRemoteResult.InvalidUri();

        using var timeout = new CancellationTokenSource(RequestTimeout, _timeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
            using HttpResponseMessage response = await _client.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, linked.Token);
            if (!response.IsSuccessStatusCode)
                return new DefinitionRemoteResult.NetworkError();
            if (response.Content.Headers.ContentLength > MaximumPayloadBytes)
                return new DefinitionRemoteResult.PayloadTooLarge();

            byte[]? payload = await ReadBoundedBodyAsync(response.Content, linked.Token);
            if (payload is null)
                return new DefinitionRemoteResult.PayloadTooLarge();

            return ParseDefinition(endpoint, response.Content.Headers.ContentType?.MediaType, payload);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return new DefinitionRemoteResult.NetworkError();
        }
        catch (Exception failure) when (ContainsBlockedHostFailure(failure))
        {
            return new DefinitionRemoteResult.BlockedHost();
        }
        catch (Exception failure) when (failure is HttpRequestException or SocketException or IOException)
        {
            return new DefinitionRemoteResult.NetworkError();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _client.Dispose();
        _disposed = true;
    }

    private static async Task<byte[]?> ReadBoundedBodyAsync(HttpContent content, CancellationToken cancellationToken)
    {
        await using Stream body = await content.ReadAsStreamAsync(cancellationToken);
        int initialCapacity = content.Headers.ContentLength is > 0 and <= MaximumPayloadBytes
            ? (int)content.Headers.ContentLength.Value
            : 0;
        using var output = initialCapacity > 0 ? new MemoryStream(initialCapacity) : new MemoryStream();
        byte[] buffer = ArrayPool<byte>.Shared.Rent(ReadChunkBytes);
        try
        {
            while (true)
            {
                int remainingPlusSentinel = MaximumPayloadBytes + 1 - (int)output.Length;
                int count = Math.Min(buffer.Length, remainingPlusSentinel);
                int read = await body.ReadAsync(buffer.AsMemory(0, count), cancellationToken);
                if (read == 0) break;
                output.Write(buffer, 0, read);
                if (output.Length > MaximumPayloadBytes)
                    return null;
            }
            return output.ToArray();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static DefinitionRemoteResult ParseDefinition(Uri endpoint, string? mediaType, byte[] payload)
    {
        if (payload.Length == 0)
            return new DefinitionRemoteResult.ParseError();

        bool yaml = mediaType?.Contains("yaml", StringComparison.OrdinalIgnoreCase) == true
            || endpoint.AbsolutePath.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase)
            || endpoint.AbsolutePath.EndsWith(".yml", StringComparison.OrdinalIgnoreCase);

        try
        {
            Definition definition = yaml
                ? DefinitionSourceCodec.ParseYaml(payload)
                : DefinitionSourceCodec.ParseJson(payload);
            return new DefinitionRemoteResult.Success(definition);
        }
        catch (Exception failure) when (failure is DefinitionException or JsonException or InvalidOperationException)
        {
            return new DefinitionRemoteResult.ParseError();
        }
    }

    private async ValueTask<Stream> ConnectAsync(
        SocketsHttpConnectionContext context,
        CancellationToken cancellationToken)
    {
        IPAddress[] addresses = await _resolveAddresses(context.DnsEndPoint.Host, cancellationToken);
        if (addresses.Length == 0 || addresses.Any(address => !PublicRemoteEndpointPolicy.IsPublicAddress(address)))
            throw new BlockedRemoteHostException();

        Exception? lastFailure = null;
        foreach (IPAddress address in addresses.Distinct())
        {
            try
            {
                return await _connect(address, context.DnsEndPoint.Port, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception failure) when (failure is SocketException or IOException)
            {
                lastFailure = failure;
            }
        }
        throw new HttpRequestException("No admitted remote connection was available.", lastFailure);
    }

    private static bool ContainsBlockedHostFailure(Exception failure)
    {
        for (Exception? current = failure; current is not null; current = current.InnerException)
        {
            if (current is BlockedRemoteHostException)
                return true;
        }
        return false;
    }

    private static async ValueTask<IPAddress[]> ResolveAddressesAsync(string host, CancellationToken cancellationToken) =>
        await Dns.GetHostAddressesAsync(host, cancellationToken);

    private static async ValueTask<Stream> OpenSocketAsync(
        IPAddress address,
        int port,
        CancellationToken cancellationToken)
    {
        Socket? socket = new(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        try
        {
            await socket.ConnectAsync(new IPEndPoint(address, port), cancellationToken);
            var stream = new NetworkStream(socket, ownsSocket: true);
            socket = null;
            return stream;
        }
        finally
        {
            socket?.Dispose();
        }
    }

    private sealed class BlockedRemoteHostException : Exception;
}
