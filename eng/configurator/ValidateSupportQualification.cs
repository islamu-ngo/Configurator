#:property RestorePackagesWithLockFile=false
#:property NuGetLockFilePath=$(MSBuildProjectExtensionsPath)ValidateSupportQualification.packages.lock.json
#:property AssemblyName=Configurator.SupportQualification
#:property PublishAot=false

using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

return SupportQualification.Run(args);

/// <summary>
/// Admits detached, identity-bound assertions, not proof that a host executed them.
/// Generated package admission owns target RIDs. No answers or raw evidence are retained.
/// </summary>
internal static class SupportQualification
{
    private const int MaximumBytes = 1024 * 1024;
    private const string Format = "configurator-support-qualification-0.0.1";
    private const string SchemaPath = "schemas/configurator-support-qualification.schema.json";
    private static readonly JsonSerializerOptions ProjectionOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private static readonly string[] Statuses = ["passed", "failed", "verified-unsupported", "unexecuted"];
    private static readonly string[] RootFields = ["format", "productSourceInputsDigest", "buildRecipeDigest",
        "payloadDigest", "commit", "dirtyInputsDigest", "toolVersion", "records"];
    private static readonly string[] RecordFields = ["client", "packageRid", "testRunnerRid", "hostOs", "hostDigest",
        "capability", "locale", "scenario", "recordedAt", "status", "exitCode", "passedCount", "failedCount", "artifactCreated"];

    // Scenario vocabulary describes claims, never executable commands or private evidence paths.
    private static readonly (string Capability, string Scenario, string[] Clients)[] Scenarios =
    [
        ("prepare", "prepare-output", ["cli", "terminal", "desktop", "browser"]),
        ("edit", "root-map", ["terminal", "desktop", "browser"]),
        ("edit", "root-variant", ["terminal", "desktop", "browser"]),
        ("edit", "correction", ["terminal", "desktop", "browser"]),
        ("import", "compare-adopt", ["cli", "terminal", "desktop", "browser"]),
        ("protected-save", "create-only-save", ["cli", "terminal", "desktop"]),
        ("machine-contract", "machine-envelope", ["cli"]),
        ("lifecycle", "cancel-close", ["terminal", "desktop"]),
        ("lifecycle", "installed-lifecycle", ["desktop"]),
        ("accessibility", "keyboard-focus", ["terminal", "desktop", "browser"]),
        ("accessibility", "assistive-technology", ["terminal", "desktop", "browser"]),
        ("custody", "consent-revoke", ["browser"]),
        ("export", "deliberate-export", ["browser"]),
        ("offline", "cache-update", ["browser"]),
        ("performance", "interactive-response", ["terminal", "desktop", "browser"])
    ];

    internal static int Run(string[] args)
    {
        try
        {
            bool packaged = args.Length > 0 && args[0] == "--receipt";
            string root = packaged
                ? Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory))!
                : FindRoot();
            string admissionDirectory = packaged ? "admission" : "eng/configurator/generated";
            using var admission = JsonDocument.Parse(ReadBounded(Path.Combine(root,
                admissionDirectory, "native-release-capabilities.json")));
            using var browser = JsonDocument.Parse(ReadBounded(Path.Combine(root,
                admissionDirectory, "browser-release-capabilities.json")));
            if (args is ["--schema-write"] or ["--schema-check"])
            {
                string schema = Schema(admission.RootElement, browser.RootElement);
                string path = Path.Combine(root, SchemaPath);
                if (args[0] == "--schema-write") File.WriteAllText(path, schema);
                else Require(File.ReadAllText(path) == schema);
                Console.WriteLine("SUPPORT_QUALIFICATION_SCHEMA_CURRENT");
                return 0;
            }
            if (args is ["--self-test"]) return SelfTest(admission.RootElement, browser.RootElement);
            if (args.Length >= 7 && args[0] == "--projection")
            {
                Console.WriteLine(JsonSerializer.Serialize(Project(args, admission.RootElement, browser.RootElement), ProjectionOptions));
                return 0;
            }
            if (args is not ["--receipt", var input, "--source", var source, "--build", var build, "--payload", var payload]
                || input.StartsWith("--", StringComparison.Ordinal) || !Digest(source) || !Digest(build) || !Digest(payload))
            {
                Console.Error.WriteLine("Usage: --self-test | --schema-write | --schema-check | --receipt FILE --source SHA256 --build SHA256 --payload SHA256 | --projection --source SHA256 --build SHA256 --payload SHA256 [--receipt FILE]...");
                return 64;
            }
            Receipt receipt = Parse(ReadBounded(input), source, build, payload, admission.RootElement, browser.RootElement);
            // Acceptance means structurally valid evidence; failed and unexecuted remain explicit.
            Console.WriteLine(JsonSerializer.Serialize(Statuses.ToDictionary(status => status,
                status => receipt.Records.Count(record => record.Status == status))));
            return 0;
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or UnauthorizedAccessException
            or JsonException or InvalidOperationException or ArgumentException or NotSupportedException or FormatException or OverflowException)
        {
            Console.Error.WriteLine("SUPPORT_QUALIFICATION_REJECTED");
            return 1;
        }
    }

    /// <summary>
    /// Exports the admitted coverage vocabulary and the same immutable Parse result used by validation.
    /// Absent receipts convey no execution claim. Every supplied receipt must match all expected digests.
    /// </summary>
    private static Projection Project(string[] args, JsonElement admission, JsonElement browser)
    {
        Require(args.Length is >= 7 and <= 135 && (args.Length - 7) % 2 == 0
            && args[1] == "--source" && args[3] == "--build" && args[5] == "--payload"
            && Digest(args[2]) && Digest(args[4]) && Digest(args[6]));
        var receipts = ImmutableArray.CreateBuilder<ReceiptEvidence>();
        var keys = new HashSet<Coverage>();
        for (int index = 7; index < args.Length; index += 2)
        {
            Require(args[index] == "--receipt" && !args[index + 1].StartsWith("--", StringComparison.Ordinal));
            byte[] bytes = ReadBounded(args[index + 1]);
            Receipt receipt = Parse(bytes, args[2], args[4], args[6], admission, browser);
            foreach (Outcome record in receipt.Records)
                Require(keys.Add(new Coverage(record.Client, record.PackageRid, record.Capability, record.Locale, record.Scenario)));
            receipts.Add(new ReceiptEvidence(Convert.ToHexStringLower(SHA256.HashData(bytes)), receipt));
        }
        var coverage = ImmutableArray.CreateBuilder<Coverage>();
        var clients = admission.GetProperty("surfaces").EnumerateObject()
            .Select(surface => (Client: surface.Name, Rids: surface.Value.GetProperty("runtimeIdentifiers")
                .EnumerateArray().Select(rid => rid.GetString()!).ToArray())).ToList();
        if (browser.GetProperty("targetEnabled").GetBoolean()) clients.Add(("browser", ["browser-wasm"]));
        foreach (var client in clients)
            foreach (string rid in client.Rids)
                foreach (var scenario in Scenarios.Where(scenario => scenario.Clients.Contains(client.Client)))
                    foreach (string locale in new[] { "en", "ar" })
                        coverage.Add(new Coverage(client.Client, rid, scenario.Capability, locale, scenario.Scenario));
        return new Projection("configurator-support-projection-0.0.1", args[2], args[4], args[6],
            coverage.ToImmutable(), receipts.ToImmutable());
    }

    private static Receipt Parse(byte[] bytes, string source, string build, string payload,
        JsonElement admission, JsonElement browser)
    {
        Require(bytes.Length is > 0 and <= MaximumBytes);
        using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 8 });
        JsonElement root = document.RootElement;
        Fields(root, RootFields);
        Require(Text(root, "format") == Format
            && Text(root, "productSourceInputsDigest") == source
            && Text(root, "buildRecipeDigest") == build && Text(root, "payloadDigest") == payload);
        string commit = Text(root, "commit");
        Require(Hex(commit, 40));
        string? dirty = NullableText(root, "dirtyInputsDigest");
        Require(dirty is null || Digest(dirty));
        Require(Text(root, "toolVersion") == "0.0.1");
        JsonElement entries = root.GetProperty("records");
        Require(entries.ValueKind == JsonValueKind.Array && entries.GetArrayLength() is >= 1 and <= 512);
        var records = ImmutableArray.CreateBuilder<Outcome>();
        var keys = new HashSet<(string, string, string, string, string)>();
        foreach (JsonElement entry in entries.EnumerateArray())
        {
            Fields(entry, RecordFields);
            string client = Text(entry, "client"), rid = Text(entry, "packageRid");
            bool isBrowser = client == "browser";
            Require(isBrowser
                ? browser.GetProperty("targetEnabled").GetBoolean() && rid == "browser-wasm"
                : admission.GetProperty("surfaces").TryGetProperty(client, out var surface)
                    && surface.GetProperty("runtimeIdentifiers").EnumerateArray().Any(item => item.GetString() == rid));
            string capability = Text(entry, "capability"), scenario = Text(entry, "scenario"), locale = Text(entry, "locale");
            Require(locale is "en" or "ar" && Scenarios.Any(item => item.Capability == capability
                && item.Scenario == scenario && item.Clients.Contains(client)));
            Require(keys.Add((client, rid, capability, locale, scenario)));
            string time = Text(entry, "recordedAt");
            Require(DateTimeOffset.TryParseExact(time, "yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal, out _));
            string status = Text(entry, "status");
            Require(Statuses.Contains(status));
            string? runner = NullableText(entry, "testRunnerRid"), os = NullableText(entry, "hostOs"),
                host = NullableText(entry, "hostDigest");
            int? exit = entry.GetProperty("exitCode").ValueKind == JsonValueKind.Null ? null : entry.GetProperty("exitCode").GetInt32();
            int passed = entry.GetProperty("passedCount").GetInt32(), failed = entry.GetProperty("failedCount").GetInt32();
            bool artifact = entry.GetProperty("artifactCreated").GetBoolean();
            Require(passed is >= 0 and <= 100000 && failed is >= 0 and <= 100000);
            if (status == "unexecuted")
                Require(runner is null && os is null && host is null && exit is null && passed == 0 && failed == 0 && !artifact);
            else
            {
                Require(host is not null && Digest(host) && runner is not null && os is not null
                    && admission.GetProperty("surfaces").EnumerateObject().Any(surface =>
                        surface.Value.GetProperty("runtimeIdentifiers").EnumerateArray().Any(item => item.GetString() == runner))
                    && runner.Split('-')[0] == os && (isBrowser || runner == rid));
                Require(exit is >= 0 and <= 255);
                switch (status)
                {
                    case "passed":
                        Require(exit == 0 && passed > 0 && failed == 0);
                        if (capability == "protected-save") Require(os == "linux" && artifact);
                        break;
                    case "failed":
                        Require(exit > 0 && failed > 0);
                        break;
                    case "verified-unsupported":
                        // Refusal is a successful observation on an unsupported save host,
                        // never a successful save or a substitute for an unexecuted row.
                        Require(capability == "protected-save" && os is "win" or "osx"
                            && exit > 0 && passed > 0 && failed == 0 && !artifact);
                        break;
                }
                Require(!artifact || capability is "protected-save" or "export");
            }
            records.Add(new Outcome(client, rid, runner, os, host, capability, locale, scenario,
                time, status, exit, passed, failed, artifact));
        }
        return new Receipt(source, build, payload, commit, dirty, "0.0.1", records.ToImmutable());
    }

    private static byte[] ReadBounded(string path)
    {
        using var input = File.OpenRead(path);
        using var output = new MemoryStream();
        byte[] chunk = new byte[8192];
        int count;
        while ((count = input.Read(chunk, 0, Math.Min(chunk.Length, MaximumBytes + 1 - (int)output.Length))) > 0)
        {
            output.Write(chunk, 0, count);
            Require(output.Length <= MaximumBytes);
        }
        return output.ToArray();
    }

    private static string FindRoot()
    {
        DirectoryInfo? directory = new(Environment.CurrentDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Configurator.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidDataException();
    }

    private static bool Hex(string value, int length) => value.Length == length
        && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static bool Digest(string value) => Hex(value, 64);
    private static void Require(bool condition) { if (!condition) throw new InvalidDataException(); }
    private static string Text(JsonElement value, string name) => value.GetProperty(name).GetString() ?? throw new InvalidDataException();
    private static string? NullableText(JsonElement value, string name) => value.GetProperty(name).GetString();
    private static void Fields(JsonElement value, string[] expected)
    {
        Require(value.ValueKind == JsonValueKind.Object);
        var fields = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonProperty property in value.EnumerateObject()) Require(fields.Add(property.Name) && expected.Contains(property.Name));
        Require(fields.Count == expected.Length);
    }

    private static string Schema(JsonElement admission, JsonElement browser)
    {
        static JsonObject Closed(string[] fields, JsonObject properties) => new()
        {
            ["type"] = "object", ["additionalProperties"] = false,
            ["required"] = new JsonArray(fields.Select(field => (JsonNode?)JsonValue.Create(field)).ToArray()), ["properties"] = properties
        };
        static JsonObject Enum(IEnumerable<string> values) => new() { ["enum"] = new JsonArray(values.Select(value => (JsonNode?)JsonValue.Create(value)).ToArray()) };
        static JsonObject Hash(bool nullable = false) => new()
        {
            ["type"] = nullable ? new JsonArray("string", "null") : JsonValue.Create("string"),
            ["pattern"] = "^[0-9a-f]{64}$"
        };
        var surfaces = admission.GetProperty("surfaces").EnumerateObject().ToArray();
        string[] runners = surfaces.SelectMany(surface => surface.Value.GetProperty("runtimeIdentifiers").EnumerateArray()
            .Select(item => item.GetString()!)).Distinct().Order(StringComparer.Ordinal).ToArray();
        var record = Closed(RecordFields, new JsonObject
        {
            ["client"] = Enum(surfaces.Select(surface => surface.Name).Concat(browser.GetProperty("targetEnabled").GetBoolean() ? ["browser"] : [])),
            ["packageRid"] = Enum(runners.Append("browser-wasm")),
            ["testRunnerRid"] = new JsonObject { ["enum"] = new JsonArray(runners.Select(value => (JsonNode?)JsonValue.Create(value)).Append(null).ToArray()) },
            ["hostOs"] = new JsonObject { ["enum"] = new JsonArray("linux", "osx", "win", null) },
            ["hostDigest"] = Hash(true), ["capability"] = Enum(Scenarios.Select(item => item.Capability).Distinct()),
            ["locale"] = Enum(["en", "ar"]), ["scenario"] = Enum(Scenarios.Select(item => item.Scenario)),
            ["recordedAt"] = new JsonObject { ["type"] = "string", ["format"] = "date-time", ["maxLength"] = 20,
                ["pattern"] = "^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}Z$" },
            ["status"] = Enum(Statuses),
            ["exitCode"] = new JsonObject { ["type"] = new JsonArray("integer", "null"), ["minimum"] = 0, ["maximum"] = 255 },
            ["passedCount"] = new JsonObject { ["type"] = "integer", ["minimum"] = 0, ["maximum"] = 100000 },
            ["failedCount"] = new JsonObject { ["type"] = "integer", ["minimum"] = 0, ["maximum"] = 100000 },
            ["artifactCreated"] = new JsonObject { ["type"] = "boolean" }
        });
        var schema = Closed(RootFields, new JsonObject
        {
            ["format"] = new JsonObject { ["const"] = Format },
            ["productSourceInputsDigest"] = Hash(), ["buildRecipeDigest"] = Hash(), ["payloadDigest"] = Hash(),
            ["commit"] = new JsonObject { ["type"] = "string", ["pattern"] = "^[0-9a-f]{40}$" },
            ["dirtyInputsDigest"] = Hash(true), ["toolVersion"] = new JsonObject { ["const"] = "0.0.1" },
            ["records"] = new JsonObject { ["type"] = "array", ["minItems"] = 1, ["maxItems"] = 512, ["items"] = record }
        });
        schema["$schema"] = "https://json-schema.org/draft/2020-12/schema";
        schema["$comment"] = "Generated by ValidateSupportQualification.cs. Native validation additionally enforces 1 MiB input, duplicate keys and identities, expected digests, current per-client admission, scenario relevance, actual host/target equality and status/exit/count/artifact relationships. Schema acceptance alone is not evidence validation or qualification.";
        return schema.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n";
    }

    private static int SelfTest(JsonElement admission, JsonElement browser)
    {
        const string digest = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        string valid = $$"""
            {"format":"{{Format}}","productSourceInputsDigest":"{{digest}}","buildRecipeDigest":"{{digest}}",
            "payloadDigest":"{{digest}}","commit":"1111111111111111111111111111111111111111",
            "dirtyInputsDigest":null,"toolVersion":"0.0.1","records":[{"client":"cli","packageRid":"linux-x64",
            "testRunnerRid":"linux-x64","hostOs":"linux","hostDigest":"{{digest}}","capability":"protected-save",
            "locale":"en","scenario":"create-only-save","recordedAt":"2026-11-04T12:00:00Z","status":"passed",
            "exitCode":0,"passedCount":1,"failedCount":0,"artifactCreated":true}]}
            """;
        Require(Parse(System.Text.Encoding.UTF8.GetBytes(valid), digest, digest, digest, admission, browser).Records.Length == 1);
        foreach (string invalid in new[] { "{}", valid.Replace("\"exitCode\":0", "\"exitCode\":1", StringComparison.Ordinal),
            valid.Replace("\"packageRid\":\"linux-x64\"", "\"packageRid\":\"linux-musl-x64\"", StringComparison.Ordinal),
            valid.Replace("\"format\":", "\"format\":\"duplicate\",\"format\":", StringComparison.Ordinal) })
        {
            bool rejected = false;
            try { Parse(System.Text.Encoding.UTF8.GetBytes(invalid), digest, digest, digest, admission, browser); }
            catch (InvalidDataException) { rejected = true; }
            Require(rejected);
        }
        Console.WriteLine("SUPPORT_QUALIFICATION_SELF_TEST_PASSED");
        return 0;
    }

    private sealed record Receipt(string ProductSourceInputsDigest, string BuildRecipeDigest, string PayloadDigest,
        string Commit, string? DirtyInputsDigest, string ToolVersion, ImmutableArray<Outcome> Records);
    private sealed record Outcome(string Client, string PackageRid, string? TestRunnerRid, string? HostOs,
        string? HostDigest, string Capability, string Locale, string Scenario, string RecordedAt, string Status,
        int? ExitCode, int PassedCount, int FailedCount, bool ArtifactCreated);
    private sealed record Coverage(string Client, string PackageRid, string Capability, string Locale, string Scenario);
    private sealed record ReceiptEvidence(string ReceiptDigest, Receipt Receipt);
    private sealed record Projection(string Format, string ProductSourceInputsDigest, string BuildRecipeDigest,
        string PayloadDigest, ImmutableArray<Coverage> Admission, ImmutableArray<ReceiptEvidence> Receipts);
}
