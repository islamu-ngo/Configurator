namespace Configurator.Core.Definitions;

using System.Collections.Frozen;

/// <summary>
/// Derives deterministic display text for untitled technical identifiers. Explicit authored text always wins;
/// symbolic literals such as versions, CIDR ranges, paths and media types are returned unchanged.
/// </summary>
/// <remarks>
/// Humanization is presentation-only: wire names, enum tokens, canonical bytes and digests never change.
/// Identifier words are Title Cased, while recognized technical acronyms and product names keep their
/// canonical spelling (<c>PUBLIC_BASE_URL</c> becomes <c>Public Base URL</c>).
/// </remarks>
public static class DefinitionDisplayText
{
    /// <summary>Separator rendered between hierarchy levels such as <c>Logging__LogLevel</c>.</summary>
    public const string HierarchySeparator = " / ";

    private const string ContainerPrefix = "env.";
    private const int MaximumIdentifierLength = 256;

    private static readonly FrozenDictionary<string, string> CanonicalWords = new Dictionary<string, string>(
        StringComparer.OrdinalIgnoreCase)
    {
        ["aes"] = "AES", ["ai"] = "AI", ["api"] = "API", ["apis"] = "APIs", ["ascii"] = "ASCII", ["aws"] = "AWS",
        ["bff"] = "BFF", ["oci"] = "OCI", ["rpo"] = "RPO", ["rto"] = "RTO", ["sse"] = "SSE", ["tms"] = "TMS",
        ["cdn"] = "CDN", ["cidr"] = "CIDR", ["cli"] = "CLI", ["cors"] = "CORS", ["cpu"] = "CPU", ["csp"] = "CSP",
        ["csrf"] = "CSRF", ["css"] = "CSS", ["csv"] = "CSV", ["db"] = "DB", ["dns"] = "DNS", ["dsn"] = "DSN",
        ["ecdsa"] = "ECDSA", ["fqdn"] = "FQDN", ["ftp"] = "FTP", ["gcm"] = "GCM", ["gcp"] = "GCP", ["gdpr"] = "GDPR",
        ["gpu"] = "GPU", ["grpc"] = "gRPC", ["guid"] = "GUID", ["hmac"] = "HMAC", ["html"] = "HTML", ["http"] = "HTTP",
        ["https"] = "HTTPS", ["id"] = "ID", ["ids"] = "IDs", ["imap"] = "IMAP", ["io"] = "IO", ["ip"] = "IP",
        ["ips"] = "IPs", ["ipv4"] = "IPv4", ["ipv6"] = "IPv6", ["iso"] = "ISO", ["json"] = "JSON", ["jwks"] = "JWKS",
        ["jwt"] = "JWT", ["k8s"] = "K8s", ["ldap"] = "LDAP", ["llm"] = "LLM", ["md5"] = "MD5", ["mcp"] = "MCP",
        ["mfa"] = "MFA", ["mime"] = "MIME", ["oidc"] = "OIDC", ["os"] = "OS", ["otp"] = "OTP", ["pdf"] = "PDF",
        ["pii"] = "PII", ["pwa"] = "PWA", ["ram"] = "RAM", ["rsa"] = "RSA", ["s3"] = "S3", ["saml"] = "SAML",
        ["sdk"] = "SDK", ["sftp"] = "SFTP", ["sha1"] = "SHA1", ["sha256"] = "SHA256", ["sha512"] = "SHA512",
        ["smtp"] = "SMTP", ["sms"] = "SMS", ["sql"] = "SQL", ["ssh"] = "SSH", ["ssl"] = "SSL", ["sso"] = "SSO",
        ["tcp"] = "TCP", ["tls"] = "TLS", ["totp"] = "TOTP", ["ttl"] = "TTL", ["udp"] = "UDP", ["ui"] = "UI",
        ["uri"] = "URI", ["uris"] = "URIs", ["url"] = "URL", ["urls"] = "URLs", ["urn"] = "URN", ["utc"] = "UTC",
        ["utf8"] = "UTF-8", ["uuid"] = "UUID", ["ux"] = "UX", ["vapid"] = "VAPID", ["vm"] = "VM", ["vpn"] = "VPN",
        ["wasm"] = "WASM", ["xml"] = "XML", ["xss"] = "XSS", ["yaml"] = "YAML", ["yml"] = "YAML", ["toml"] = "TOML",
        ["aspnet"] = "ASP.NET", ["aspnetcore"] = "ASP.NET Core", ["dotnet"] = ".NET", ["github"] = "GitHub",
        ["gitlab"] = "GitLab", ["graphql"] = "GraphQL", ["javascript"] = "JavaScript", ["linkedin"] = "LinkedIn",
        ["macos"] = "macOS", ["ios"] = "iOS", ["mariadb"] = "MariaDB", ["minio"] = "MinIO", ["mongodb"] = "MongoDB",
        ["mysql"] = "MySQL", ["nodejs"] = "Node.js", ["oauth"] = "OAuth", ["oauth2"] = "OAuth2", ["openai"] = "OpenAI",
        ["opentelemetry"] = "OpenTelemetry", ["postgresql"] = "PostgreSQL", ["rabbitmq"] = "RabbitMQ",
        ["atproto"] = "AT Protocol", ["otel"] = "OTel", ["posthog"] = "PostHog",
        ["signalr"] = "SignalR", ["sqlite"] = "SQLite", ["typescript"] = "TypeScript", ["webauthn"] = "WebAuthn",
        ["youtube"] = "YouTube"
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>Returns explicit authored text when present, otherwise the humanized identifier.</summary>
    public static string Resolve(string? explicitText, string identifier) =>
        string.IsNullOrWhiteSpace(explicitText) ? Humanize(identifier) : explicitText;

    /// <summary>
    /// Converts <c>SCREAMING_SNAKE_CASE</c>, <c>snake_case</c>, <c>kebab-case</c>, <c>camelCase</c> and
    /// <c>PascalCase</c> identifiers to Title Case. Text that is not a bounded ASCII identifier passes through.
    /// </summary>
    public static string Humanize(string identifier)
    {
        ArgumentNullException.ThrowIfNull(identifier);
        string candidate = identifier.StartsWith(ContainerPrefix, StringComparison.Ordinal)
            && identifier.Length > ContainerPrefix.Length ? identifier[ContainerPrefix.Length..] : identifier;
        if (!IsIdentifier(candidate)) return identifier;
        string[] levels = candidate.Split(["__", "."], StringSplitOptions.None);
        if (levels.Any(level => level.Length == 0 || level.All(char.IsAsciiDigit))) return identifier;
        var rendered = new string[levels.Length];
        var words = new List<string>(8);
        for (int index = 0; index < levels.Length; index++)
        {
            words.Clear();
            foreach (string part in levels[index].Split(['_', '-'], StringSplitOptions.RemoveEmptyEntries))
                AddWords(words, part);
            if (words.Count == 0) return identifier;
            rendered[index] = string.Join(' ', words);
        }
        return string.Join(HierarchySeparator, rendered);
    }

    private static bool IsIdentifier(string text) =>
        text.Length is > 0 and <= MaximumIdentifierLength && char.IsAsciiLetter(text[0])
        && text.All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-' or '.');

    /// <summary>Splits one delimiter-free part at camel-case and acronym boundaries.</summary>
    private static void AddWords(List<string> words, string part)
    {
        if (CanonicalWords.TryGetValue(part, out string? whole))
        {
            words.Add(whole);
            return;
        }
        int start = 0;
        for (int index = 1; index < part.Length; index++)
        {
            char previous = part[index - 1];
            char current = part[index];
            bool nextIsLower = index + 1 < part.Length && char.IsAsciiLetterLower(part[index + 1]);
            bool boundary = char.IsAsciiLetterUpper(current)
                && (char.IsAsciiLetterLower(previous)
                    || (char.IsAsciiLetterUpper(previous) || char.IsAsciiDigit(previous)) && nextIsLower);
            if (!boundary) continue;
            words.Add(Word(part[start..index]));
            start = index;
        }
        words.Add(Word(part[start..]));
    }
    private static string Word(string word)
    {
        if (CanonicalWords.TryGetValue(word, out string? canonical)) return canonical;
        if (word.All(char.IsAsciiDigit)) return word;
        return string.Create(word.Length, word, static (span, source) =>
        {
            span[0] = char.ToUpperInvariant(source[0]);
            for (int index = 1; index < source.Length; index++) span[index] = char.ToLowerInvariant(source[index]);
        });
    }
}
