#:property RestorePackagesWithLockFile=false
#:property PublishAot=false
#:property NuGetLockFilePath=obj/Configurator.DefinitionDelivery/packages.lock.json
#:property JsonSerializerIsReflectionEnabledByDefault=true
#:property AssemblyName=Configurator.DefinitionDelivery
#:property RootNamespace=Configurator.Engineering
#:project ../../src/Configurator.Core/Configurator.Core.csproj

using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Configurator.Core;
using Configurator.Core.Definitions;
using Configurator.Core.Definitions.Authoring;

return DefinitionDeliveryTool.Run(args);

internal static class DefinitionDeliveryTool
{
    private const int MaximumDocumentBytes = 4 * 1024 * 1024;
    private const int MaximumReceiptBytes = 64 * 1024;
    private const int MaximumSourceUrlLength = 2048;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    internal static int Run(string[] arguments)
    {
        try
        {
            if (arguments is ["--self-test"])
                return SelfTest();

            DeliveryRequest request = ParseArguments(arguments);
            Execute(request);
            Console.WriteLine(request.CheckOnly ? "delivery-receipt-current" : "delivery-receipt-written");
            return 0;
        }
        catch (DeliveryException exception)
        {
            Console.Error.WriteLine(exception.Code);
            return exception.ExitCode;
        }
        catch (DefinitionException exception)
        {
            Console.Error.WriteLine(exception.Code);
            return 1;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException
            or ArgumentException or InvalidOperationException or FormatException)
        {
            Console.Error.WriteLine("delivery-invalid");
            return 1;
        }
    }

    private static DeliveryRequest ParseArguments(string[] arguments)
    {
        if (arguments.Length == 0)
            throw new DeliveryException("delivery-arguments-invalid", 64);

        string? source = null;
        string? definition = null;
        string? receipt = null;
        string? sourceUrl = null;
        string? appVersion = null;
        string? entry = null;
        string? locale = null;
        bool write = false;
        bool check = false;
        var seen = new HashSet<string>(StringComparer.Ordinal);

        for (int index = 0; index < arguments.Length; index++)
        {
            string arg = arguments[index];
            if (arg is "--write" or "--check")
            {
                if (!seen.Add(arg)) throw new DeliveryException("delivery-arguments-invalid", 64);
                if (arg == "--write") write = true;
                else check = true;
                continue;
            }

            if (!seen.Add(arg) || index + 1 >= arguments.Length)
                throw new DeliveryException("delivery-arguments-invalid", 64);

            string value = arguments[++index];
            if (string.IsNullOrEmpty(value))
                throw new DeliveryException("delivery-arguments-invalid", 64);

            switch (arg)
            {
                case "--source": source = value; break;
                case "--definition": definition = value; break;
                case "--receipt": receipt = value; break;
                case "--source-url": sourceUrl = value; break;
                case "--app-version": appVersion = value; break;
                case "--entry": entry = value; break;
                case "--locale": locale = value; break;
                default: throw new DeliveryException("delivery-arguments-invalid", 64);
            }
        }

        if (write == check
            || source is null || definition is null || receipt is null
            || sourceUrl is null || appVersion is null || entry is null || locale is null)
            throw new DeliveryException("delivery-arguments-invalid", 64);

        return new DeliveryRequest(source, definition, receipt, sourceUrl, appVersion, entry, locale, check);
    }

    private static void Execute(DeliveryRequest request)
    {
        ValidateSourceUrl(request.SourceUrl);

        string sourcePath = Path.GetFullPath(request.SourcePath);
        string definitionPath = Path.GetFullPath(request.DefinitionPath);
        string receiptPath = Path.GetFullPath(request.ReceiptPath);

        RequireUnlinked(sourcePath);
        RequireUnlinked(definitionPath);
        RequireUnlinked(receiptPath);

        if (!File.Exists(sourcePath))
            throw new DeliveryException("delivery-source-missing");
        if (!File.Exists(definitionPath))
            throw new DeliveryException("delivery-definition-missing");

        byte[] sourceBytes = ReadBounded(sourcePath, MaximumDocumentBytes, "document-limit");
        byte[] definitionBytes = ReadBounded(definitionPath, MaximumDocumentBytes, "document-limit");

        string extension = Path.GetExtension(sourcePath).ToLowerInvariant();
        bool isYaml = extension is ".yaml" or ".yml";
        if (!isYaml && extension != ".json")
            throw new DeliveryException("delivery-source-format");

        DefinitionAuthoringDocument authoring = isYaml
            ? DefinitionAuthoringCodec.ParseYaml(sourceBytes)
            : DefinitionAuthoringCodec.ParseJson(sourceBytes);
        Definition compiled = DefinitionAuthoringCompiler.Compile(authoring);
        Definition runtime = DefinitionCodec.ParseJson(definitionBytes);

        if (compiled.FamilyId != runtime.FamilyId
            || compiled.Revision != runtime.Revision
            || compiled.Digest != runtime.Digest
            || !compiled.CanonicalBytes.Span.SequenceEqual(runtime.CanonicalBytes.Span)
            || !definitionBytes.AsSpan().SequenceEqual(compiled.CanonicalBytes.Span))
            throw new DeliveryException("delivery-definition-mismatch");

        if (!compiled.ApplicationVersions.Contains(request.ApplicationVersion, StringComparer.Ordinal)
            || !compiled.EntryPoints.Contains(request.Entry, StringComparer.Ordinal)
            || !compiled.Locales.Contains(request.Locale, StringComparer.Ordinal))
            throw new DeliveryException("delivery-selector-undeclared");

        byte[] expectedReceipt = BuildReceipt(compiled, sourceBytes, isYaml ? "yaml" : "json", request);

        if (request.CheckOnly)
        {
            if (!File.Exists(receiptPath))
                throw new DeliveryException("delivery-receipt-missing");
            byte[] existing = ReadBounded(receiptPath, MaximumReceiptBytes, "delivery-receipt-limit");
            if (!existing.AsSpan().SequenceEqual(expectedReceipt))
                throw new DeliveryException("delivery-receipt-stale");
            return;
        }

        WriteAtomic(receiptPath, expectedReceipt);
    }

    private static byte[] BuildReceipt(Definition definition, ReadOnlySpan<byte> sourceBytes, string sourceFormat, DeliveryRequest request)
    {
        string fragment =
            $"src={Uri.EscapeDataString(request.SourceUrl)}" +
            $"&digest={definition.Digest.Value}" +
            $"&appVersion={Uri.EscapeDataString(request.ApplicationVersion)}" +
            $"&entry={Uri.EscapeDataString(request.Entry)}" +
            $"&locale={Uri.EscapeDataString(request.Locale)}";

        var artifactIds = new JsonArray();
        foreach (string id in definition.Artifacts.Select(static artifact => artifact.Id).Order(StringComparer.Ordinal))
            artifactIds.Add(id);

        var receipt = new JsonObject
        {
            ["applicationVersion"] = request.ApplicationVersion,
            ["artifactIds"] = artifactIds,
            ["authoringContractVersion"] = ConfiguratorVersion.Current,
            ["authorityClaim"] = "none-local-canonical-agreement-only",
            ["browserLaunchFragment"] = fragment,
            ["definitionDigest"] = definition.Digest.Value,
            ["entry"] = request.Entry,
            ["familyId"] = definition.FamilyId.ToString("D"),
            ["locale"] = request.Locale,
            ["profile"] = DefinitionProfile.Id,
            ["projectId"] = definition.ApplicationId,
            ["receiptContractVersion"] = ConfiguratorVersion.Current,
            ["revision"] = definition.Revision,
            ["runtimeSchemaVersion"] = ConfiguratorVersion.Current,
            ["signatureStatus"] = "unsigned",
            ["sourceFormat"] = sourceFormat,
            ["sourceSha256"] = Sha256Hex(sourceBytes),
            ["sourceUrl"] = request.SourceUrl,
            ["trustBoundary"] = "unsigned-local-verification-only"
        };

        string json = receipt.ToJsonString(JsonOptions).Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";
        return Encoding.UTF8.GetBytes(json);
    }

    private static void ValidateSourceUrl(string sourceUrl)
    {
        if (string.IsNullOrWhiteSpace(sourceUrl)
            || sourceUrl.Length > MaximumSourceUrlLength
            || sourceUrl.Any(char.IsControl)
            || !Uri.TryCreate(sourceUrl, UriKind.Absolute, out Uri? uri)
            || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || !string.IsNullOrEmpty(uri.UserInfo)
            || !string.IsNullOrEmpty(uri.Query)
            || !string.IsNullOrEmpty(uri.Fragment)
            || string.IsNullOrEmpty(uri.IdnHost))
            throw new DeliveryException("delivery-source-url-invalid");

        string host = uri.IdnHost.TrimEnd('.');
        if (string.IsNullOrEmpty(host)
            || host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".local", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".internal", StringComparison.OrdinalIgnoreCase)
            || !host.Contains('.', StringComparison.Ordinal) && !IPAddress.TryParse(host, out _))
            throw new DeliveryException("delivery-source-url-invalid");

        if (IPAddress.TryParse(host, out IPAddress? ip))
        {
            if (IsRestrictedAddress(ip))
                throw new DeliveryException("delivery-source-url-invalid");
        }
    }

    private static bool IsRestrictedAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        if (IPAddress.IsLoopback(address)
            || address.Equals(IPAddress.Any)
            || address.Equals(IPAddress.IPv6Any)
            || address.Equals(IPAddress.None)
            || address.Equals(IPAddress.Broadcast))
            return true;

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            byte[] bytes = address.GetAddressBytes();
            return bytes[0] == 0
                || bytes[0] == 10
                || bytes[0] == 127
                || (bytes[0] == 100 && bytes[1] is >= 64 and <= 127)
                || (bytes[0] == 169 && bytes[1] == 254)
                || (bytes[0] == 172 && bytes[1] is >= 16 and <= 31)
                || (bytes[0] == 192 && bytes[1] == 168)
                || bytes[0] >= 224;
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast || address.IsIPv6Teredo)
                return true;
            byte[] bytes = address.GetAddressBytes();
            return (bytes[0] & 0xFE) == 0xFC; // fc00::/7 ULA
        }

        return true;
    }

    private static byte[] ReadBounded(string path, int maximumBytes, string limitCode)
    {
        using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > maximumBytes)
            throw new DeliveryException(limitCode);
        using var buffer = new MemoryStream((int)Math.Min(stream.Length, maximumBytes));
        byte[] chunk = new byte[16 * 1024];
        int total = 0;
        int read;
        while ((read = stream.Read(chunk, 0, chunk.Length)) > 0)
        {
            total += read;
            if (total > maximumBytes)
                throw new DeliveryException(limitCode);
            buffer.Write(chunk, 0, read);
        }
        return buffer.ToArray();
    }

    private static void WriteAtomic(string destinationPath, byte[] bytes)
    {
        string? directory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
            RequireUnlinked(directory);
        }
        string tempPath = destinationPath + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(flushToDisk: true);
            }
            File.Move(tempPath, destinationPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    private static void RequireUnlinked(string fullPath)
    {
        string? current = fullPath;
        while (!string.IsNullOrEmpty(current))
        {
            if (File.Exists(current) && new FileInfo(current).LinkTarget is not null)
                throw new DeliveryException("delivery-symlink-rejected");
            if (Directory.Exists(current) && new DirectoryInfo(current).LinkTarget is not null)
                throw new DeliveryException("delivery-symlink-rejected");
            string? parent = Path.GetDirectoryName(current);
            if (string.Equals(parent, current, StringComparison.Ordinal))
                break;
            current = parent;
        }
    }

    private static int SelfTest()
    {
        string workspace = Path.Combine(Path.GetTempPath(), "configurator-delivery-selftest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspace);
        try
        {
            string sourcePath = Path.Combine(workspace, "configurator.yaml");
            string defPath = Path.Combine(workspace, "definition.json");
            string receiptPath = Path.Combine(workspace, "release.json");
            const string validUrl = "https://example.invalid/releases/definition.json";

            string validYaml = """
            $schema: urn:islamu:configurator:authoring:v0.0.1
            identity:
              familyId: "12345678-1234-4234-8234-1234567890ab"
              revision: 1
            project:
              id: example.delivery
              name: Delivery Example
            applicationVersions: ["1.2.3"]
            configuration:
              type: object
              additionalProperties: false
              required: [service]
              properties:
                service:
                  type: string
                  minLength: 1
                  x-configurator:
                    sensitivity: public
            outputs:
              config.json:
                id: config
                format: json
                targetUpdateMode: replace
                restartRequirement: process
            """;
            File.WriteAllText(sourcePath, validYaml, new UTF8Encoding(false));
            Definition compiled = DefinitionAuthoringCompiler.Compile(
                DefinitionAuthoringCodec.ParseYaml(File.ReadAllBytes(sourcePath)));
            File.WriteAllBytes(defPath, compiled.CanonicalBytes.ToArray());

            string[] baseArgs(string mode, string? url = null, string? appVer = null, string? entry = null, string? locale = null, string? defOverride = null, string? srcOverride = null) =>
            [
                "--source", srcOverride ?? sourcePath,
                "--definition", defOverride ?? defPath,
                "--receipt", receiptPath,
                "--source-url", url ?? validUrl,
                "--app-version", appVer ?? "1.2.3",
                "--entry", entry ?? "setup",
                "--locale", locale ?? "en",
                mode
            ];

            // 1. Read-only --check rejects missing receipt without creating it.
            ExpectFailure(baseArgs("--check"), "delivery-receipt-missing");
            if (File.Exists(receiptPath)) throw new InvalidOperationException("check-created-file");

            // 2. --write produces deterministic independent receipt vector.
            ExpectSuccess(baseArgs("--write"));
            byte[] receiptBytes = File.ReadAllBytes(receiptPath);
            using (JsonDocument doc = JsonDocument.Parse(receiptBytes))
            {
                JsonElement root = doc.RootElement;
                RequireEqual(root.GetProperty("receiptContractVersion").GetString(), "0.0.1");
                RequireEqual(root.GetProperty("trustBoundary").GetString(), "unsigned-local-verification-only");
                RequireEqual(root.GetProperty("signatureStatus").GetString(), "unsigned");
                RequireEqual(root.GetProperty("authorityClaim").GetString(), "none-local-canonical-agreement-only");
                RequireEqual(root.GetProperty("definitionDigest").GetString(), compiled.Digest.Value);
                RequireEqual(root.GetProperty("sourceSha256").GetString(), Sha256Hex(File.ReadAllBytes(sourcePath)));
                RequireEqual(root.GetProperty("familyId").GetString(), "12345678-1234-4234-8234-1234567890ab");
                RequireEqual(root.GetProperty("revision").GetInt64().ToString(CultureInfo.InvariantCulture), "1");
                RequireEqual(root.GetProperty("browserLaunchFragment").GetString(),
                    $"src={Uri.EscapeDataString(validUrl)}&digest={compiled.Digest.Value}&appVersion=1.2.3&entry=setup&locale=en");
            }

            // 3. --check succeeds on matching receipt and leaves bytes identical.
            ExpectSuccess(baseArgs("--check"));
            if (!File.ReadAllBytes(receiptPath).AsSpan().SequenceEqual(receiptBytes))
                throw new InvalidOperationException("check-modified-receipt");

            // 4. Stale receipt fails --check without modifying receipt bytes.
            byte[] staleBytes = Encoding.UTF8.GetBytes("{\"stale\":true}\n");
            File.WriteAllBytes(receiptPath, staleBytes);
            ExpectFailure(baseArgs("--check"), "delivery-receipt-stale");
            if (!File.ReadAllBytes(receiptPath).AsSpan().SequenceEqual(staleBytes))
                throw new InvalidOperationException("check-overwrote-stale-receipt");

            // Restore valid receipt and verify failed --write preserves prior receipt bytes verbatim.
            File.WriteAllBytes(receiptPath, receiptBytes);

            // 5. Source/runtime mismatch rejected; prior receipt preserved.
            string mismatchedSource = Path.Combine(workspace, "mismatched.yaml");
            File.WriteAllText(mismatchedSource, validYaml.Replace("Delivery Example", "Tampered Example", StringComparison.Ordinal), new UTF8Encoding(false));
            ExpectFailure(baseArgs("--write", srcOverride: mismatchedSource), "delivery-definition-mismatch");
            if (!File.ReadAllBytes(receiptPath).AsSpan().SequenceEqual(receiptBytes))
                throw new InvalidOperationException("mismatch-overwrote-prior-receipt");

            // 6. Undeclared selectors rejected; prior receipt preserved.
            ExpectFailure(baseArgs("--write", appVer: "9.9.9"), "delivery-selector-undeclared");
            ExpectFailure(baseArgs("--write", entry: "undeclared-entry"), "delivery-selector-undeclared");
            ExpectFailure(baseArgs("--write", locale: "fr"), "delivery-selector-undeclared");
            if (!File.ReadAllBytes(receiptPath).AsSpan().SequenceEqual(receiptBytes))
                throw new InvalidOperationException("selector-overwrote-prior-receipt");

            // 7. Non-HTTPS, credentialed, loopback, RFC1918, link-local, query/fragment URLs rejected.
            string[] badUrls =
            [
                "http://example.invalid/definition.json",
                "https://user:pass@example.invalid/definition.json",
                "https://localhost/definition.json",
                "https://sub.localhost/definition.json",
                "https://127.0.0.1/definition.json",
                "https://[::1]/definition.json",
                "https://10.0.0.1/definition.json",
                "https://172.16.0.1/definition.json",
                "https://192.168.1.1/definition.json",
                "https://169.254.169.254/definition.json",
                "https://example.invalid/definition.json?token=abc",
                "https://example.invalid/definition.json#frag"
            ];
            foreach (string badUrl in badUrls)
                ExpectFailure(baseArgs("--write", url: badUrl), "delivery-source-url-invalid");
            if (!File.ReadAllBytes(receiptPath).AsSpan().SequenceEqual(receiptBytes))
                throw new InvalidOperationException("url-overwrote-prior-receipt");

            // 8. Oversized input rejected without touching prior receipt.
            string oversized = Path.Combine(workspace, "oversized.json");
            File.WriteAllBytes(oversized, new byte[MaximumDocumentBytes + 1]);
            ExpectFailure(baseArgs("--write", defOverride: oversized), "document-limit");
            if (!File.ReadAllBytes(receiptPath).AsSpan().SequenceEqual(receiptBytes))
                throw new InvalidOperationException("oversized-overwrote-prior-receipt");

            Console.WriteLine("delivery-selftest-passed");
            return 0;
        }
        finally
        {
            if (Directory.Exists(workspace)) Directory.Delete(workspace, recursive: true);
        }
    }

    private static void ExpectSuccess(string[] args)
    {
        DeliveryRequest request = ParseArguments(args);
        Execute(request);
    }

    private static void ExpectFailure(string[] args, string expectedCode)
    {
        try
        {
            DeliveryRequest request = ParseArguments(args);
            Execute(request);
        }
        catch (DeliveryException exception) when (exception.Code == expectedCode)
        {
            return;
        }
        catch (DefinitionException exception) when (exception.Code == expectedCode)
        {
            return;
        }
        throw new InvalidOperationException($"expected-failure:{expectedCode}");
    }

    private static void RequireEqual(string? actual, string expected)
    {
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
            throw new InvalidOperationException($"mismatch:{actual}!={expected}");
    }

    private static string Sha256Hex(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private sealed record DeliveryRequest(
        string SourcePath,
        string DefinitionPath,
        string ReceiptPath,
        string SourceUrl,
        string ApplicationVersion,
        string Entry,
        string Locale,
        bool CheckOnly);

    private sealed class DeliveryException(string code, int exitCode = 1) : Exception(code)
    {
        public string Code { get; } = code;
        public int ExitCode { get; } = exitCode;
    }
}
