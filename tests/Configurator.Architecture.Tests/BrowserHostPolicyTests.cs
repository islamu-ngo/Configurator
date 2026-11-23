using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Configurator.Architecture.Tests;

[Category("Host")]
public sealed class BrowserHostPolicyTests
{
    private static Uri HostUri => new(Environment.GetEnvironmentVariable("CONFIGURATOR_HOST_URL")
        ?? throw new InvalidOperationException("CONFIGURATOR_HOST_URL must identify the running qualification Host."));

    private static HttpClient CreateClient() => new()
    {
        BaseAddress = HostUri
    };

    private static string BuiltHostAssemblyPath => Path.Combine(
        ReleaseTestPaths.Root,
        "src/Configurator.Browser.Host/bin/Release/net10.0/Configurator.Browser.Host.dll");

    [Test]
    public async Task ResponsesDenyFramingAndRetainPrivacyHeaders()
    {
        using HttpClient client = CreateClient();
        using HttpResponseMessage response = await client.GetAsync("/");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(string.Join(";", response.Headers.GetValues("Content-Security-Policy")))
            .Contains("frame-ancestors 'none'");
        await Assert.That(response.Headers.GetValues("X-Content-Type-Options").Single()).IsEqualTo("nosniff");
        await Assert.That(response.Headers.GetValues("Referrer-Policy").Single()).IsEqualTo("no-referrer");
    }

    // Qualification copies the publication into an isolated tree and adds these
    // synthetic files before starting the Host; none belongs to its inventory.
    [Test]
    [Arguments("/host-attack.json")]
    [Arguments("/host-attack.unknown")]
    [Arguments("/host-attack")]
    [Arguments("/missing.js")]
    [Arguments("/unrecognized/route")]
    [Arguments("/settings/evil.js")]
    [Arguments("/explore/missing.wasm")]
    public async Task UnpublishedFilesAndUnknownRoutesAreNotServed(string path)
    {
        using HttpClient client = CreateClient();
        using HttpResponseMessage response = await client.GetAsync(path);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    [Arguments("/")]
    [Arguments("/open")]
    [Arguments("/help")]
    [Arguments("/settings")]
    [Arguments("/settings/appearance")]
    [Arguments("/store/example")]
    [Arguments("/launch/example")]
    [Arguments("/explore/example")]
    [Arguments("/explore/9284f310-9901-411b-8024-c14444dce5db")]
    public async Task KnownNavigationSupportsGetAndHead(string path)
    {
        using HttpClient client = CreateClient();
        using HttpResponseMessage get = await client.GetAsync(path);
        using HttpRequestMessage request = new(HttpMethod.Head, path);
        using HttpResponseMessage head = await client.SendAsync(request);
        await Assert.That(get.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(head.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(get.Content.Headers.ContentType?.MediaType).IsEqualTo("text/html");
        await Assert.That(await head.Content.ReadAsByteArrayAsync()).IsEmpty();
    }

    [Test]
    public async Task HealthEndpointSupportsGetAndHeadWithoutCaching()
    {
        using HttpClient client = CreateClient();

        using HttpResponseMessage get = await client.GetAsync("/healthz");
        await Assert.That(get.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(get.Content.Headers.ContentType?.MediaType).IsEqualTo("application/json");
        await Assert.That(get.Headers.CacheControl?.NoStore).IsTrue();
        await Assert.That(string.Join(";", get.Headers.GetValues("Content-Security-Policy")))
            .Contains("frame-ancestors 'none'");
        await Assert.That(get.Headers.GetValues("X-Content-Type-Options").Single()).IsEqualTo("nosniff");
        await Assert.That(get.Headers.GetValues("Referrer-Policy").Single()).IsEqualTo("no-referrer");
        using JsonDocument payload = JsonDocument.Parse(await get.Content.ReadAsStringAsync());
        await Assert.That(payload.RootElement.GetProperty("status").GetString()).IsEqualTo("ok");

        using HttpRequestMessage headRequest = new(HttpMethod.Head, "/healthz");
        using HttpResponseMessage head = await client.SendAsync(headRequest);
        await Assert.That(head.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(head.Headers.CacheControl?.NoStore).IsTrue();
        await Assert.That(await head.Content.ReadAsByteArrayAsync()).IsEmpty();

        using HttpRequestMessage postRequest = new(HttpMethod.Post, "/healthz");
        using HttpResponseMessage post = await client.SendAsync(postRequest);
        await Assert.That(post.StatusCode).IsEqualTo(HttpStatusCode.MethodNotAllowed);
    }

    [Test]
    [Arguments("POST")]
    [Arguments("PUT")]
    [Arguments("DELETE")]
    [Arguments("OPTIONS")]
    public async Task OtherMethodsAreRejected(string method)
    {
        using HttpClient client = CreateClient();
        using HttpRequestMessage request = new(new HttpMethod(method), "/");
        using HttpResponseMessage response = await client.SendAsync(request);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.MethodNotAllowed);
    }

    [Test]
    [Arguments("/service-worker.js", "text/javascript")]
    [Arguments("/service-worker-assets.js", "text/javascript")]
    [Arguments("/manifest.webmanifest", "application/manifest+json")]
    [Arguments("/_framework/blazor.webassembly.js", "text/javascript")]
    [Arguments("/LICENSE", "text/plain")]
    [Arguments("/schemas/setup-definition.schema.json", "application/json")]
    public async Task PublicAssetsHaveExplicitMimeAndRevalidate(string path, string mime)
    {
        using HttpClient client = CreateClient();
        using HttpResponseMessage response = await client.GetAsync(path);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(response.Content.Headers.ContentType?.MediaType).IsEqualTo(mime);
        await Assert.That(response.Headers.CacheControl?.NoCache).IsTrue();
    }

    [Test]
    public async Task PublishedApplicationInventorySupportsWasmAndOmitsStaticStore()
    {
        using HttpClient client = CreateClient();
        string manifest = (await client.GetStringAsync("/service-worker-assets.js")).Trim();
        using JsonDocument document = JsonDocument.Parse(manifest["self.assetsManifest = ".Length..^1]);
        bool foundWasm = false;
        foreach (JsonElement asset in document.RootElement.GetProperty("assets").EnumerateArray())
        {
            string url = asset.GetProperty("url").GetString()!;
            await Assert.That(url.StartsWith("definition-store/", StringComparison.Ordinal)).IsFalse();
            await Assert.That(url).IsNotEqualTo("js/browser-store.js");
            string path = "/" + url;
            using HttpResponseMessage response = await client.GetAsync(path);
            await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK).Because(path);
            if (path.EndsWith(".wasm", StringComparison.Ordinal))
            {
                foundWasm = true;
                await Assert.That(response.Content.Headers.ContentType?.MediaType).IsEqualTo("application/wasm");
            }
        }
        await Assert.That(foundWasm).IsTrue();
    }

    [Test]
    public async Task HttpsRequestsCompressPublicStaticAssetsWithBrotliAndGzip()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), $"configurator-host-https-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);
        Process? hostProcess = null;
        try
        {
            string webRoot = Path.Combine(tempRoot, "wwwroot");
            Directory.CreateDirectory(Path.Combine(webRoot, "_framework"));
            Directory.CreateDirectory(Path.Combine(webRoot, "schemas"));

            byte[] wasmPayload = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("wasm-public-static-asset-segment-", 256)));
            byte[] jsPayload = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("export const segment = 'public-static-js';\n", 160)));
            byte[] manifestPayload = Encoding.UTF8.GetBytes("{\"name\":\"Configurator\",\"padding\":\"" + new string('a', 4096) + "\"}");
            byte[] schemaPayload = Encoding.UTF8.GetBytes("{\"title\":\"Schema\",\"description\":\"" + new string('b', 4096) + "\"}");

            await File.WriteAllTextAsync(Path.Combine(webRoot, "index.html"), "<!doctype html><html><body>ok</body></html>");
            await File.WriteAllBytesAsync(Path.Combine(webRoot, "_framework/sample.wasm"), wasmPayload);
            await File.WriteAllBytesAsync(Path.Combine(webRoot, "sample.js"), jsPayload);
            await File.WriteAllBytesAsync(Path.Combine(webRoot, "manifest.webmanifest"), manifestPayload);
            await File.WriteAllBytesAsync(Path.Combine(webRoot, "schemas/setup-definition.schema.json"), schemaPayload);
            await File.WriteAllTextAsync(
                Path.Combine(webRoot, "service-worker-assets.js"),
                """self.assetsManifest = {"version":"test","assets":[{"url":"index.html","hash":"sha256-a"},{"url":"_framework/sample.wasm","hash":"sha256-b"},{"url":"sample.js","hash":"sha256-c"},{"url":"manifest.webmanifest","hash":"sha256-d"}]};""");

            string pfxPath = Path.Combine(tempRoot, "ephemeral-loopback.pfx");
            string pfxPassword = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
            string expectedThumbprint = WriteEphemeralLoopbackCertificate(pfxPath, pfxPassword);

            int port = ReserveUnusedLoopbackPort();
            Uri httpsBase = new($"https://127.0.0.1:{port}");
            var start = new ProcessStartInfo("dotnet")
            {
                WorkingDirectory = ReleaseTestPaths.Root,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            start.ArgumentList.Add(BuiltHostAssemblyPath);
            start.ArgumentList.Add("--urls");
            start.ArgumentList.Add(httpsBase.GetLeftPart(UriPartial.Authority));
            start.ArgumentList.Add("--webroot");
            start.ArgumentList.Add(webRoot);
            start.Environment["ASPNETCORE_Kestrel__Certificates__Default__Path"] = pfxPath;
            start.Environment["ASPNETCORE_Kestrel__Certificates__Default__Password"] = pfxPassword;

            hostProcess = Process.Start(start) ?? throw new InvalidOperationException("Failed to start HTTPS Host process.");

            using HttpClientHandler handler = new()
            {
                AutomaticDecompression = DecompressionMethods.None,
                ServerCertificateCustomValidationCallback = (_, certificate, _, _) =>
                    certificate is not null
                    && string.Equals(
                        certificate.GetCertHashString(HashAlgorithmName.SHA256),
                        expectedThumbprint,
                        StringComparison.OrdinalIgnoreCase)
            };
            using HttpClient httpsClient = new(handler)
            {
                BaseAddress = httpsBase,
                Timeout = TimeSpan.FromSeconds(10)
            };

            await WaitForHostReady(hostProcess, httpsClient, "/index.html");

            (string Path, string Mime, byte[] Expected)[] targets =
            [
                ("/_framework/sample.wasm", "application/wasm", wasmPayload),
                ("/sample.js", "text/javascript", jsPayload),
                ("/manifest.webmanifest", "application/manifest+json", manifestPayload),
                ("/schemas/setup-definition.schema.json", "application/json", schemaPayload)
            ];

            foreach ((string assetPath, string expectedMime, byte[] expectedBytes) in targets)
            {
                foreach (string encoding in new[] { "br", "gzip" })
                {
                    using HttpRequestMessage request = new(HttpMethod.Get, assetPath);
                    request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue(encoding));
                    using HttpResponseMessage response = await httpsClient.SendAsync(request);
                    await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK).Because($"{assetPath} ({encoding})");
                    await Assert.That(response.Content.Headers.ContentType?.MediaType).IsEqualTo(expectedMime).Because($"{assetPath} ({encoding})");
                    await Assert.That(response.Content.Headers.ContentEncoding).Contains(encoding).Because($"HTTPS compression must encode {assetPath} with {encoding}");
                    await Assert.That(response.Headers.Vary).Contains("Accept-Encoding").Because($"{assetPath} ({encoding})");

                    byte[] wireBytes = await response.Content.ReadAsByteArrayAsync();
                    byte[] decoded = Decompress(wireBytes, encoding);
                    await Assert.That(decoded.SequenceEqual(expectedBytes)).IsTrue().Because($"{assetPath} ({encoding}) decoded payload mismatch");
                }

                using HttpRequestMessage plainRequest = new(HttpMethod.Get, assetPath);
                plainRequest.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("identity"));
                using HttpResponseMessage plainResponse = await httpsClient.SendAsync(plainRequest);
                await Assert.That(plainResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
                await Assert.That(plainResponse.Content.Headers.ContentEncoding).IsEmpty();
                byte[] plainBytes = await plainResponse.Content.ReadAsByteArrayAsync();
                await Assert.That(plainBytes.SequenceEqual(expectedBytes)).IsTrue();
            }
        }
        finally
        {
            if (hostProcess is not null)
            {
                try
                {
                    if (!hostProcess.HasExited)
                    {
                        hostProcess.Kill(entireProcessTree: true);
                        await hostProcess.WaitForExitAsync();
                    }
                }
                catch (InvalidOperationException)
                {
                }
                hostProcess.Dispose();
            }

            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Test]
    public async Task HealthCheckProbeSucceedsOnLoopbackAndFailsWhenStopped()
    {
        Uri hostUri = HostUri;
        string healthUrl = $"{hostUri.GetLeftPart(UriPartial.Authority)}/healthz";
        int exitFromUrl = await RunHealthCheckProcess(
            healthCheckUrl: healthUrl,
            httpPorts: null);
        await Assert.That(exitFromUrl).IsEqualTo(0);

        int exitFromPorts = await RunHealthCheckProcess(
            healthCheckUrl: null,
            httpPorts: hostUri.Port.ToString(CultureInfo.InvariantCulture));
        await Assert.That(exitFromPorts).IsEqualTo(0);

        int stoppedPort = ReserveUnusedLoopbackPort();
        int exitStopped = await RunHealthCheckProcess(
            healthCheckUrl: $"http://127.0.0.1:{stoppedPort}/healthz",
            httpPorts: null);
        await Assert.That(exitStopped).IsEqualTo(1);
    }

    [Test]
    [Arguments("http://169.254.169.254/healthz")]
    [Arguments("http://example.com/healthz")]
    [Arguments("https://127.0.0.1:8080/healthz")]
    [Arguments("file:///etc/passwd")]
    [Arguments("http://user@127.0.0.1:8080/healthz")]
    [Arguments("http://127.0.0.1:8080/healthz?x=1")]
    [Arguments("http://127.0.0.1:8080/healthz#frag")]
    [Arguments("http://127.0.0.1:8080/open")]
    public async Task HealthCheckProbeRejectsUnsafeUrlsFailClosed(string unsafeUrl)
    {
        int exitCode = await RunHealthCheckProcess(healthCheckUrl: unsafeUrl, httpPorts: null);
        await Assert.That(exitCode).IsEqualTo(1);
    }

    [Test]
    public async Task HealthCheckProbeMakesZeroSocketConnectionsForRejectedLoopbackVariants()
    {
        using TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;

        string[] rejectedUrls =
        [
            $"http://127.0.0.1:{port}/open",
            $"http://127.0.0.1:{port}/healthz?probe=1",
            $"http://user@127.0.0.1:{port}/healthz",
            $"https://127.0.0.1:{port}/healthz"
        ];

        foreach (string rejectedUrl in rejectedUrls)
        {
            int exitCode = await RunHealthCheckProcess(healthCheckUrl: rejectedUrl, httpPorts: null);
            await Assert.That(exitCode).IsEqualTo(1).Because(rejectedUrl);
            await Assert.That(listener.Pending()).IsFalse().Because($"Rejected probe URL must not open a socket: {rejectedUrl}");
        }
    }

    private static async Task<int> RunHealthCheckProcess(string? healthCheckUrl, string? httpPorts)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = ReleaseTestPaths.Root,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        start.ArgumentList.Add(BuiltHostAssemblyPath);
        start.ArgumentList.Add("--healthcheck");
        start.Environment.Remove("CONFIGURATOR_HEALTHCHECK_URL");
        start.Environment.Remove("ASPNETCORE_HTTP_PORTS");
        if (healthCheckUrl is not null)
            start.Environment["CONFIGURATOR_HEALTHCHECK_URL"] = healthCheckUrl;
        if (httpPorts is not null)
            start.Environment["ASPNETCORE_HTTP_PORTS"] = httpPorts;

        using Process process = Process.Start(start) ?? throw new InvalidOperationException("Failed to start --healthcheck process.");
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        using CancellationTokenSource deadline = new(TimeSpan.FromSeconds(15));
        try
        {
            await process.WaitForExitAsync(deadline.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            throw new TimeoutException($"--healthcheck exceeded timeout. stdout={await stdout} stderr={await stderr}");
        }

        await Task.WhenAll(stdout, stderr);
        return process.ExitCode;
    }

    private static int ReserveUnusedLoopbackPort()
    {
        using TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static string WriteEphemeralLoopbackCertificate(string pfxPath, string password)
    {
        using RSA rsa = RSA.Create(2048);
        CertificateRequest request = new(
            "CN=127.0.0.1",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        SubjectAlternativeNameBuilder san = new();
        san.AddIpAddress(IPAddress.Loopback);
        san.AddDnsName("localhost");
        request.CertificateExtensions.Add(san.Build());
        using X509Certificate2 certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-5),
            DateTimeOffset.UtcNow.AddHours(1));
        byte[] pfxBytes = certificate.Export(X509ContentType.Pkcs12, password);
        File.WriteAllBytes(pfxPath, pfxBytes);
        return certificate.GetCertHashString(HashAlgorithmName.SHA256);
    }

    private static async Task WaitForHostReady(Process process, HttpClient client, string probePath)
    {
        using CancellationTokenSource deadline = new(TimeSpan.FromSeconds(15));
        while (!deadline.IsCancellationRequested)
        {
            if (process.HasExited)
            {
                string stdout = await process.StandardOutput.ReadToEndAsync();
                string stderr = await process.StandardError.ReadToEndAsync();
                throw new InvalidOperationException($"HTTPS Host exited early ({process.ExitCode}). stdout={stdout} stderr={stderr}");
            }

            try
            {
                using HttpResponseMessage response = await client.GetAsync(probePath, deadline.Token);
                if (response.StatusCode == HttpStatusCode.OK)
                    return;
            }
            catch (HttpRequestException)
            {
            }

            await Task.Delay(50, deadline.Token);
        }

        throw new TimeoutException("Timed out waiting for ephemeral HTTPS Host to become ready.");
    }

    private static byte[] Decompress(byte[] compressed, string encoding)
    {
        using MemoryStream input = new(compressed);
        using Stream decompressor = encoding switch
        {
            "br" => new BrotliStream(input, CompressionMode.Decompress),
            "gzip" => new GZipStream(input, CompressionMode.Decompress),
            _ => throw new ArgumentOutOfRangeException(nameof(encoding), encoding, "Unsupported encoding.")
        };
        using MemoryStream output = new();
        decompressor.CopyTo(output);
        return output.ToArray();
    }
}
