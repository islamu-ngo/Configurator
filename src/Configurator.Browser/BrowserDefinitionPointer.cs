namespace Configurator.Browser;

using System.Net;
using System.Net.Sockets;
using Configurator.Core;

/// <summary>Public launch metadata only; answers never belong in a source pointer.</summary>
public sealed record BrowserDefinitionPointer(Uri Source, string? Digest, string? AppVersion, string? Entry, string? Locale)
{
    public static BrowserDefinitionPointer Parse(string fragment, string query = "")
    {
        ArgumentNullException.ThrowIfNull(fragment);
        if (query.Length != 0 || fragment.Length > 4096) throw new BrowserLaunchException("source-invalid");
        if (fragment.StartsWith('#')) fragment = fragment[1..];
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string part in fragment.Split('&'))
        {
            int separator = part.IndexOf('=', StringComparison.Ordinal);
            if (separator <= 0 || !ValidEscapes(part)) throw new BrowserLaunchException("source-invalid");
            string name = Uri.UnescapeDataString(part[..separator]);
            string value = Uri.UnescapeDataString(part[(separator + 1)..]);
            int maximum = name switch { "src" => 2048, "digest" => 64, "appVersion" => 64, "entry" => 128, "locale" => 35, _ => 0 };
            if (value.Length == 0 || value.Length > maximum || !values.TryAdd(name, value)
                || name != "src" && !value.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '-' or '_'))
                throw new BrowserLaunchException("source-invalid");
        }
        if (!values.TryGetValue("src", out string? source)
            || !Uri.TryCreate(source, UriKind.Absolute, out Uri? uri)) throw new BrowserLaunchException("source-invalid");
        ValidateSource(uri);
        string? digest = values.GetValueOrDefault("digest");
        if (digest is not null)
        {
            if (!ArtifactDigest.TryParse(digest, out ArtifactDigest parsed)) throw new BrowserLaunchException("digest-invalid");
            digest = parsed.Value;
        }
        return new(uri, digest, values.GetValueOrDefault("appVersion"), values.GetValueOrDefault("entry"), values.GetValueOrDefault("locale"));
    }

    internal static void ValidateSource(Uri source)
    {
        if (!source.IsAbsoluteUri || source.Scheme != Uri.UriSchemeHttps || source.UserInfo.Length != 0
            || source.Query.Length != 0 || source.Fragment.Length != 0 || source.AbsoluteUri.Length > 2048)
            throw new BrowserLaunchException("source-invalid");
        string host = source.IdnHost.TrimEnd('.').Trim('[', ']');
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".local", StringComparison.OrdinalIgnoreCase)) throw new BrowserLaunchException("source-invalid");
        if (IPAddress.TryParse(host, out IPAddress? address))
        {
            if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
            byte[] bytes = address.GetAddressBytes();
            bool publicAddress = address.AddressFamily == AddressFamily.InterNetwork
                ? bytes[0] is not (0 or 10 or 127) && bytes[0] < 224
                    && !(bytes[0] == 169 && bytes[1] == 254)
                    && !(bytes[0] == 172 && bytes[1] is >= 16 and <= 31)
                    && !(bytes[0] == 192 && bytes[1] == 168)
                    && !(bytes[0] == 100 && bytes[1] is >= 64 and <= 127)
                    && !(bytes[0] == 198 && bytes[1] is 18 or 19)
                : (bytes[0] & 0xe0) == 0x20;
            if (!publicAddress) throw new BrowserLaunchException("source-invalid");
        }
        else if (!host.Contains('.', StringComparison.Ordinal)) throw new BrowserLaunchException("source-invalid");
    }

    private static bool ValidEscapes(string text)
    {
        for (int index = 0; index < text.Length; index++)
        {
            if (text[index] != '%') continue;
            if (index + 2 >= text.Length || !Uri.IsHexDigit(text[index + 1]) || !Uri.IsHexDigit(text[index + 2])) return false;
            index += 2;
        }
        return true;
    }
}
