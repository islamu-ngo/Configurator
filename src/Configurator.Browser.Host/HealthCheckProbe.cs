using System.Globalization;
using System.Net;
using System.Text.Json;

namespace Configurator.Browser.Host;

/// <summary>
/// Loopback-only self-probe executed via <c>--healthcheck</c> so container orchestrators can
/// verify liveness without installing <c>curl</c> or opening arbitrary outbound HTTP targets.
/// </summary>
internal static class HealthCheckProbe
{
    private const int DefaultHttpPort = 8080;
    private const int MaximumResponseBytes = 256;
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(2);

    internal static async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        if (!TryResolveTargetUri(
                Environment.GetEnvironmentVariable("CONFIGURATOR_HEALTHCHECK_URL"),
                Environment.GetEnvironmentVariable("ASPNETCORE_HTTP_PORTS"),
                out Uri? targetUri))
        {
            return 1;
        }

        try
        {
            using SocketsHttpHandler handler = new()
            {
                AllowAutoRedirect = false,
                UseProxy = false,
                UseCookies = false
            };
            using HttpClient client = new(handler)
            {
                Timeout = RequestTimeout
            };
            using HttpRequestMessage request = new(HttpMethod.Get, targetUri);
            using HttpResponseMessage response = await client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (response.StatusCode != HttpStatusCode.OK)
                return 1;

            byte[]? payload = await ReadBoundedBytesAsync(response.Content, MaximumResponseBytes, cancellationToken);
            if (payload is null)
                return 1;

            return IsHealthyPayload(payload) ? 0 : 1;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException or JsonException or IOException)
        {
            return 1;
        }
    }

    internal static bool TryResolveTargetUri(string? configuredUrl, string? httpPorts, out Uri? targetUri)
    {
        targetUri = null;

        if (!string.IsNullOrEmpty(configuredUrl))
            return TryParseSafeLoopbackHealthUri(configuredUrl, out targetUri);

        if (!TryResolveHttpPort(httpPorts, out int port))
            return false;

        targetUri = new Uri($"http://127.0.0.1:{port.ToString(CultureInfo.InvariantCulture)}/healthz", UriKind.Absolute);
        return true;
    }

    private static bool TryParseSafeLoopbackHealthUri(string rawUrl, out Uri? targetUri)
    {
        targetUri = null;

        if (rawUrl.Any(char.IsWhiteSpace) || rawUrl.Any(char.IsControl))
            return false;

        if (!Uri.TryCreate(rawUrl, UriKind.Absolute, out Uri? parsed))
            return false;

        if (!string.Equals(parsed.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
            return false;

        if (!parsed.IsLoopback)
            return false;

        if (parsed.Port is < 1 or > 65535)
            return false;

        if (!string.Equals(parsed.AbsolutePath, "/healthz", StringComparison.Ordinal))
            return false;

        if (!string.IsNullOrEmpty(parsed.UserInfo)
            || !string.IsNullOrEmpty(parsed.Query)
            || !string.IsNullOrEmpty(parsed.Fragment))
        {
            return false;
        }

        targetUri = parsed;
        return true;
    }

    private static bool TryResolveHttpPort(string? httpPorts, out int port)
    {
        if (string.IsNullOrWhiteSpace(httpPorts))
        {
            port = DefaultHttpPort;
            return true;
        }

        foreach (string segment in httpPorts.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            if (int.TryParse(segment.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out int candidate)
                && candidate is >= 1 and <= 65535)
            {
                port = candidate;
                return true;
            }
        }

        port = 0;
        return false;
    }

    private static async Task<byte[]?> ReadBoundedBytesAsync(
        HttpContent content,
        int maxBytes,
        CancellationToken cancellationToken)
    {
        if (content.Headers.ContentLength > maxBytes)
            return null;

        await using Stream stream = await content.ReadAsStreamAsync(cancellationToken);
        byte[] buffer = new byte[maxBytes + 1];
        int totalRead = 0;
        while (totalRead < buffer.Length)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(totalRead, buffer.Length - totalRead), cancellationToken);
            if (read == 0)
                break;

            totalRead += read;
            if (totalRead > maxBytes)
                return null;
        }

        return buffer[..totalRead];
    }

    private static bool IsHealthyPayload(byte[] payload)
    {
        using JsonDocument document = JsonDocument.Parse(payload);
        return document.RootElement.ValueKind == JsonValueKind.Object
            && document.RootElement.TryGetProperty("status", out JsonElement status)
            && status.ValueKind == JsonValueKind.String
            && string.Equals(status.GetString(), "ok", StringComparison.Ordinal);
    }
}
