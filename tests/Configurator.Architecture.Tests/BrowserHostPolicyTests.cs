using System.Net;
using System.Text.Json;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Configurator.Architecture.Tests;

[Category("Host")]
public sealed class BrowserHostPolicyTests
{
    private static HttpClient CreateClient() => new()
    {
        BaseAddress = new Uri(Environment.GetEnvironmentVariable("CONFIGURATOR_HOST_URL")
            ?? throw new InvalidOperationException("CONFIGURATOR_HOST_URL must identify the running qualification Host."))
    };

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
    [Arguments("/store/example")]
    [Arguments("/launch/example")]
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
    public async Task PublishedApplicationInventorySupportsWasmAndOptionalStore()
    {
        using HttpClient client = CreateClient();
        string manifest = (await client.GetStringAsync("/service-worker-assets.js")).Trim();
        using JsonDocument document = JsonDocument.Parse(manifest["self.assetsManifest = ".Length..^1]);
        bool foundWasm = false;
        foreach (JsonElement asset in document.RootElement.GetProperty("assets").EnumerateArray())
        {
            string path = "/" + asset.GetProperty("url").GetString();
            using HttpResponseMessage response = await client.GetAsync(path);
            await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK).Because(path);
            if (path.EndsWith(".wasm", StringComparison.Ordinal))
            {
                foundWasm = true;
                await Assert.That(response.Content.Headers.ContentType?.MediaType).IsEqualTo("application/wasm");
            }
            if (path.StartsWith("/definition-store/", StringComparison.Ordinal))
                await Assert.That(response.Content.Headers.ContentType?.MediaType).IsEqualTo("application/json");
        }
        await Assert.That(foundWasm).IsTrue();
    }
}
