#:property RestorePackagesWithLockFile=false
#:property NuGetLockFilePath=$(MSBuildProjectExtensionsPath)VerifyPackageProvenance.packages.lock.json
#:property AssemblyName=Configurator.PackageProvenance
#:property PublishAot=false
#:property AllowUnsafeBlocks=true

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

return PackageProvenance.Run(args);

/// <summary>
/// Checks detached local integrity, relying on the native capture owner's verification.
/// This does not authenticate a publisher or establish the capture owner's consumption proof.
/// Canonical JSON uses declaration-order camelCase fields, compact UTF-8, no BOM/newline;
/// inventories use ordinal path order and hashes cover exact file bytes.
/// </summary>
internal static partial class PackageProvenance
{
    private const int MaximumRecords = 10000;
    private const long MaximumFileBytes = 512L * 1024 * 1024;
    private const long MaximumTotalBytes = 4L * 1024 * 1024 * 1024;
    private const int MaximumJsonBytes = 16 * 1024 * 1024;
    private const string SchemaPath = "schemas/configurator-package-provenance.schema.json";
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver()
    };
    private static readonly string[] Owners = ["Core", "Presentation", "Artifacts", "Cli", "Terminal", "Desktop",
        "Browser", "Architecture", "AgentWorkflow", "ReleaseEngineering"];
    private static readonly string[] Statuses = ["passed", "failed", "verified-unsupported", "unexecuted"];

    internal static int Run(string[] args)
    {
        try
        {
            if (args is ["--schema-write"] or ["--schema-check"])
            {
                string schema = Schema();
                string path = Path.Combine(Root(), SchemaPath);
                if (args[0] == "--schema-write") File.WriteAllText(path, schema);
                else Require(File.ReadAllText(path) == schema);
                Console.WriteLine("PACKAGE_PROVENANCE_SCHEMA_CURRENT");
                return 0;
            }
            if (args is ["--self-test"]) return SelfTest();
            string root = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory))!;
            Require(args.Length > 0 && args[0] is "--write" or "--verify");
            bool write = args[0] == "--write";
            var options = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = 1; i < args.Length; i += 2)
                Require(i + 1 < args.Length && options.TryAdd(args[i], args[i + 1]));
            string[] fields = ["--package", "--manifest", "--capture", "--source", "--build", "--payload",
                "--payload-prefix", "--evidence", "--evidence-digest", "--checks", "--required-checks", "--client", "--package-rid"];
            Require(options.Count == fields.Length + (write ? 0 : 1)
                && fields.All(options.ContainsKey)
                && options.Keys.All(key => fields.Contains(key) || (!write && key == "--expected-digest")));
            foreach (string key in new[] { "--source", "--build", "--payload", "--evidence-digest" })
                Require(Digest(options[key]));
            if (!write) Require(Digest(options["--expected-digest"]));
            string package = Path.GetFullPath(options["--package"]), evidence = Path.GetFullPath(options["--evidence"]),
                manifestPath = Path.GetFullPath(options["--manifest"]), session = Path.GetFullPath(options["--capture"]);
            Require(!Overlap(package, evidence) && !Within(manifestPath, package) && !Within(manifestPath, evidence)
                && !Overlap(session, package) && !Overlap(session, evidence) && !Within(manifestPath, session)
                && !Within(Path.GetFullPath(options["--checks"]), package) && !Within(Path.GetFullPath(options["--checks"]), evidence));
            DirectorySafe(package);
            DirectorySafe(evidence);
            Ancestors(Path.GetDirectoryName(manifestPath)!);
            string prefix = options["--payload-prefix"];
            Require(prefix.StartsWith("generated/repo/", StringComparison.Ordinal) && prefix.EndsWith('/')
                && Relative(prefix[..^1]));
            string[] required = options["--required-checks"] == "none" ? [] : options["--required-checks"].Split(',');
            Require(required.Length <= Owners.Length && required.All(Owner)
                && required.SequenceEqual(required.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)));
            string client = options["--client"], rid = options["--package-rid"];
            AdmitTarget(root, client, rid);

            Native(root, "--verify", session);
            byte[] captureBytes = ReadJson(Path.Combine(session, "capture.json"));
            using var capture = Document(captureBytes);
            var captured = capture.RootElement;
            Require(Text(captured, "productSourceInputsDigest") == options["--source"]
                && Text(captured, "buildRecipeDigest") == options["--build"]);
            if (client != "browser")
                Require(prefix.Split('/').Contains(rid, StringComparer.Ordinal)
                    && captured.GetProperty("settings").EnumerateArray().Any(setting =>
                        Text(setting, "name") == "RuntimeIdentifier" && setting.GetProperty("value").ValueKind == JsonValueKind.String
                        && setting.GetProperty("value").GetString() == rid));
            Entry[] payload = Inventory(package);
            Require(payload.Length > 0 && Hash(Canonical(payload)) == options["--payload"]);
            Entry[] published = captured.GetProperty("outputs").EnumerateArray()
                .Where(file => Text(file, "path").StartsWith(prefix, StringComparison.Ordinal))
                .Select(file => new Entry(Text(file, "path")[prefix.Length..], file.GetProperty("length").GetInt64(),
                    Text(file, "sha256"))).OrderBy(file => file.Path, StringComparer.Ordinal).ToArray();
            Require(Canonical(payload).AsSpan().SequenceEqual(Canonical(published)));
            // Even renamed detached objects cannot enter a payload inventory.
            foreach (Entry file in payload)
                if (file.Length is > 0 and <= MaximumJsonBytes)
                    RejectDetachedPayload(ReadJson(Path.Combine(package, file.Path)), file.Path.EndsWith(".json", StringComparison.OrdinalIgnoreCase));

            var statuses = Statuses.ToDictionary(status => status, _ => 0, StringComparer.Ordinal);
            var identities = new HashSet<string>(StringComparer.Ordinal);
            var records = new List<EvidenceEntry>();
            foreach (Entry entry in Inventory(evidence))
            {
                Require(entry.Path.EndsWith(".json", StringComparison.Ordinal) && entry.Length <= MaximumJsonBytes);
                string path = Path.Combine(evidence, entry.Path);
                byte[] bytes = ReadJson(path);
                using var document = Document(bytes);
                var record = document.RootElement;
                string kind;
                if (record.TryGetProperty("format", out _))
                {
                    kind = "support";
                    Process(root, "support/Configurator.SupportQualification.dll", "--receipt", path,
                        "--source", options["--source"], "--build", options["--build"], "--payload", options["--payload"]);
                    foreach (var outcome in record.GetProperty("records").EnumerateArray())
                    {
                        Require(Text(outcome, "client") == client && Text(outcome, "packageRid") == rid);
                        string identity = string.Join('/', new[] { client, rid, Text(outcome, "capability"),
                            Text(outcome, "locale"), Text(outcome, "scenario") });
                        Require(identities.Add("support/" + identity));
                        statuses[Text(outcome, "status")]++;
                    }
                }
                else if (record.TryGetProperty("Owner", out _))
                {
                    kind = "report";
                    Process(root, "evidence/Configurator.TestEvidence.dll", "--check-report", path);
                    Require(identities.Add("report/" + Text(record, "Owner") + "/" + Text(record, "TestRunnerRid") + "/" + Text(record, "Run")));
                }
                else if (record.TryGetProperty("testCaptureDigest", out _))
                {
                    kind = "execution";
                    Execution(record);
                    Require(identities.Add("execution/" + Text(record, "owner")));
                }
                else throw new InvalidDataException();
                records.Add(new EvidenceEntry(entry.Path, entry.Length, entry.Sha256, kind));
            }
            Require(records.Count > 0 && Hash(Canonical(records.ToArray())) == options["--evidence-digest"]);
            CheckInput[] checks = Parse<CheckInput[]>(ReadJson(options["--checks"]));
            Require(checks.Length == required.Length
                && checks.Select(check => check.Owner).Order(StringComparer.Ordinal).SequenceEqual(required));
            var bindings = new List<CheckBinding>();
            foreach (CheckInput check in checks.OrderBy(check => check.Owner, StringComparer.Ordinal))
            {
                Require(Relative(check.Execution) && Relative(check.Report)
                    && records.Any(item => item.Path == check.Execution && item.Kind == "execution")
                    && records.Any(item => item.Path == check.Report && item.Kind == "report")
                    && !Overlap(Path.GetFullPath(check.Session), package) && !Overlap(Path.GetFullPath(check.Session), evidence)
                    && !Within(manifestPath, Path.GetFullPath(check.Session)));
                string executionPath = Path.Combine(evidence, check.Execution), reportPath = Path.Combine(evidence, check.Report);
                Native(root, "--verify-check", check.Session, executionPath, reportPath);
                byte[] testBytes = ReadJson(Path.Combine(check.Session, "capture.json"));
                using var testCapture = Document(testBytes);
                using var execution = Document(ReadJson(executionPath));
                JsonElement run = execution.RootElement;
                string testProject = check.Owner is "Configurator.AgentWorkflow" ? "eng/agent-workflow/tests/"
                    : check.Owner is "Configurator.ReleaseEngineering" ? "eng/release/tests/" : "tests/";
                testProject += check.Owner + ".Tests/" + check.Owner + ".Tests.csproj";
                Require(Text(testCapture.RootElement, "project") == "repo/" + testProject
                    && Text(testCapture.RootElement, "target") == "Build"
                    && Text(run.GetProperty("executable"), "path").EndsWith("/" + check.Owner + ".Tests.dll", StringComparison.Ordinal));
                Require(Text(run, "owner") == check.Owner && Text(run, "profile") == check.Owner + ".full-owner.v1"
                    && Text(run, "scope") == "full-owner" && Text(run, "outcome") == "passed"
                    && run.GetProperty("commandExitCode").GetInt32() == 0 && run.GetProperty("passed").GetInt64() > 0
                    && run.GetProperty("failed").GetInt64() == 0 && run.GetProperty("skipped").GetInt64() == 0
                    && Text(run, "testCaptureDigest") == Hash(testBytes)
                    && Text(run, "safeReportHash") == Hash(ReadJson(reportPath)));
                CompareProjection(captured, testCapture.RootElement, check.Owner);
                bindings.Add(new CheckBinding(check.Owner, check.Execution, check.Report, Hash(testBytes)));
            }
            // Executions not admitted by the independent required set never become claims.
            Require(records.Where(item => item.Kind == "execution").All(item => bindings.Any(binding => binding.Execution == item.Path)));
            var manifest = new Manifest("0.0.1", client, rid, options["--source"], options["--build"],
                options["--payload"], options["--evidence-digest"], Hash(captureBytes), prefix,
                payload, records.ToArray(), bindings.ToArray());
            byte[] canonical = Canonical(manifest);
            if (write)
            {
                using var output = new FileStream(manifestPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                output.Write(canonical);
                Console.WriteLine("PACKAGE_PROVENANCE_WRITTEN " + Hash(canonical));
            }
            else
            {
                byte[] supplied = ReadJson(manifestPath);
                _ = Parse<Manifest>(supplied);
                Require(Hash(supplied) == options["--expected-digest"] && supplied.AsSpan().SequenceEqual(canonical));
                Console.WriteLine("PACKAGE_PROVENANCE_VALID " + Hash(canonical));
            }
            Console.WriteLine(JsonSerializer.Serialize(statuses));
            return 0;
        }
        catch (Exception error) when (error is InvalidDataException or IOException or UnauthorizedAccessException or JsonException
            or InvalidOperationException or ArgumentException or NotSupportedException or FormatException
            or OverflowException or System.ComponentModel.Win32Exception or KeyNotFoundException)
        {
            Console.Error.WriteLine("PACKAGE_PROVENANCE_REJECTED");
            return 1;
        }
    }

    private static void CompareProjection(JsonElement publication, JsonElement tests, string owner)
    {
        JsonElement[] product = publication.GetProperty("productSourceProjection").EnumerateArray().ToArray();
        JsonElement[] check = tests.GetProperty("productSourceProjection").EnumerateArray().ToArray();
        string ownerRoot = "repo/src/" + owner + "/";
        if (Owners.Take(7).Any(name => owner == "Configurator." + name))
            Require(check.Any(item => Text(item, "path").StartsWith(ownerRoot, StringComparison.Ordinal)));
        // A test graph may include more product projects than a publication graph.
        // Compare each whole product-project projection that both graphs actually consume.
        string[] roots = check.Select(item => Text(item, "path").Split('/'))
            .Where(parts => parts.Length > 3 && parts[0] == "repo" && parts[1] == "src")
            .Select(parts => string.Join('/', parts.Take(3)) + "/").Distinct(StringComparer.Ordinal).ToArray();
        foreach (string root in roots)
        {
            JsonElement[] expected = product.Where(item => Text(item, "path").StartsWith(root, StringComparison.Ordinal)).ToArray();
            if (expected.Length == 0) continue;
            JsonElement[] actual = check.Where(item => Text(item, "path").StartsWith(root, StringComparison.Ordinal)).ToArray();
            Require(Canonical(expected).AsSpan().SequenceEqual(Canonical(actual)));
        }
        foreach (var input in product)
        {
            string path = Text(input, "path");
            foreach (var match in check.Where(item => Text(item, "path") == path))
                Require(Canonical(input).AsSpan().SequenceEqual(Canonical(match)));
        }
    }

    private static void AdmitTarget(string root, string client, string rid)
    {
        using var admission = Document(ReadJson(Path.Combine(root, "admission/native-release-capabilities.json")));
        if (client == "browser")
        {
            using var browser = Document(ReadJson(Path.Combine(root, "admission/browser-release-capabilities.json")));
            Require(browser.RootElement.GetProperty("targetEnabled").GetBoolean() && rid == "browser-wasm");
        }
        else Require(admission.RootElement.GetProperty("surfaces").TryGetProperty(client, out var surface)
            && surface.GetProperty("runtimeIdentifiers").EnumerateArray().Any(item => item.GetString() == rid));
    }

    private static void Execution(JsonElement record)
    {
        string[] fields = ["schemaVersion", "safeReportHash", "profile", "owner", "scope", "testCaptureDigest",
            "executable", "dependencies", "commandExitCode", "passed", "failed", "skipped", "outcome", "runnerRid"];
        Require(record.EnumerateObject().Select(item => item.Name).Order(StringComparer.Ordinal)
            .SequenceEqual(fields.Order(StringComparer.Ordinal)));
        Require(Text(record, "schemaVersion") == "0.0.1" && Owner(Text(record, "owner"))
            && Digest(Text(record, "safeReportHash")) && Digest(Text(record, "testCaptureDigest"))
            && Text(record, "scope") is "focused" or "full-owner" && Text(record, "outcome") is "passed" or "failed");
        // Full semantics and binary/dependency/report identities are verified by --verify-check.
    }

    private static void RejectDetachedPayload(byte[] bytes, bool jsonFile)
    {
        JsonDocument document;
        try { document = Document(bytes); }
        catch (JsonException) when (!jsonFile) { return; }
        using var owned = document;
        JsonElement value = document.RootElement;
        if (value.ValueKind != JsonValueKind.Object) return;
        foreach (string field in new[] { "productSourceInputsDigest", "payloadDigest", "evidenceSetDigest",
            "provenanceDigest", "safeReportHash", "testCaptureDigest", "EvidenceStatus" })
            Require(!value.TryGetProperty(field, out _));
    }

    private static Entry[] Inventory(string root)
    {
        var entries = new List<Entry>();
        long total = 0;
        int visited = 0;
        void Walk(string directory, int depth)
        {
            Require(depth <= 32);
            foreach (string path in Directory.EnumerateFileSystemEntries(directory))
            {
                Require(++visited <= MaximumRecords);
                var type = Stat(path);
                if ((type.Mode & 0xf000) == 0x4000) { Walk(path, depth + 1); continue; }
                Require((type.Mode & 0xf000) == 0x8000 && type.Links == 1);
                string relative = Path.GetRelativePath(root, path).Replace('\\', '/');
                Require(Relative(relative));
                using var stream = File.OpenRead(path);
                long length = stream.Length;
                Require(length <= MaximumFileBytes && (total += length) <= MaximumTotalBytes);
                string digest = Convert.ToHexStringLower(SHA256.HashData(stream));
                Require(stream.Position == length && stream.Length == length);
                entries.Add(new Entry(relative, length, digest));
            }
        }
        Walk(root, 0);
        return entries.OrderBy(item => item.Path, StringComparer.Ordinal).ToArray();
    }

    private static byte[] ReadJson(string path)
    {
        Ancestors(path);
        var identity = Stat(path);
        Require((identity.Mode & 0xf000) == 0x8000 && identity.Links == 1);
        using var input = File.OpenRead(path);
        Require(input.Length is > 0 and <= MaximumJsonBytes);
        var bytes = new byte[checked((int)input.Length)];
        input.ReadExactly(bytes);
        Require(input.ReadByte() == -1);
        return bytes;
    }

    private static JsonDocument Document(byte[] bytes)
    {
        var document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 32 });
        void Visit(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (var property in element.EnumerateObject()) { Require(names.Add(property.Name)); Visit(property.Value); }
            }
            else if (element.ValueKind == JsonValueKind.Array) foreach (var item in element.EnumerateArray()) Visit(item);
        }
        try { Visit(document.RootElement); return document; }
        catch { document.Dispose(); throw; }
    }
    private static T Parse<T>(byte[] bytes)
    {
        using var document = Document(bytes);
        return JsonSerializer.Deserialize<T>(document.RootElement, Json) ?? throw new InvalidDataException();
    }
    private static byte[] Canonical<T>(T value) => JsonSerializer.SerializeToUtf8Bytes(value, Json);
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static string Text(JsonElement value, string field) => value.GetProperty(field).GetString() ?? throw new InvalidDataException();
    private static bool Digest(string value) => value.Length == 64 && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static bool Owner(string value) => Owners.Any(owner => value == "Configurator." + owner);
    private static bool Relative(string value) => value.Length is > 0 and <= 1024 && !value.StartsWith('/')
        && !value.Contains('\\') && !value.Contains(':') && value.All(c => c is >= '!' and <= '~')
        && value.Split('/').All(part => part.Length > 0 && part is not ("." or ".."));
    private static void Require(bool condition) { if (!condition) throw new InvalidDataException(); }
    private static bool Within(string path, string root) => path == root || path.StartsWith(root.TrimEnd('/') + "/", StringComparison.Ordinal);
    private static bool Overlap(string left, string right) => Within(left, right) || Within(right, left);
    private static void DirectorySafe(string path) { Ancestors(path); Require((Stat(path).Mode & 0xf000) == 0x4000); }
    private static void Ancestors(string path)
    {
        for (string? current = Path.GetFullPath(path); current is not null; current = Path.GetDirectoryName(current))
            Require((Stat(current).Mode & 0xf000) != 0xa000);
    }
    private static string Root()
    {
        var current = new DirectoryInfo(Environment.CurrentDirectory);
        while (!File.Exists(Path.Combine(current.FullName, "Configurator.slnx")))
            current = current.Parent ?? throw new InvalidDataException();
        return current.FullName;
    }
    private static void Native(string root, params string[] args) =>
        Process(root, "capture/Configurator.BuildCapture.dll", args);
    private static void Process(string root, string executable, params string[] args)
    {
        string dotnet = Path.GetFullPath(Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "..", "..", "..",
            OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet"));
        var start = new ProcessStartInfo(dotnet) { WorkingDirectory = root, UseShellExecute = false,
            RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string name in start.Environment.Keys.Where(name =>
            name.StartsWith("DOTNET_", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("COMPlus_", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("CORECLR_", StringComparison.OrdinalIgnoreCase)).ToArray())
            start.Environment.Remove(name);
        start.ArgumentList.Add(Path.Combine(root, executable));
        foreach (string arg in args) start.ArgumentList.Add(arg);
        using var process = System.Diagnostics.Process.Start(start) ?? throw new InvalidDataException();
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(180000))
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
            throw new InvalidDataException();
        }
        _ = stdout.GetAwaiter().GetResult();
        _ = stderr.GetAwaiter().GetResult();
        Require(process.ExitCode == 0);
    }

    // Fixed Linux statx ABI permits rejecting FIFO/socket/device and hard-link entries
    // before opening them. Other hosts fail closed; this is a local Linux mechanism.
    [StructLayout(LayoutKind.Explicit, Size = 256)]
    private struct Identity
    {
        [FieldOffset(0)] internal uint Mask;
        [FieldOffset(16)] internal uint Links;
        [FieldOffset(28)] internal ushort Mode;
    }
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [LibraryImport("libc", EntryPoint = "statx", StringMarshalling = StringMarshalling.Utf8, SetLastError = true)]
    private static partial int StatAt(int directory, string path, int flags, uint mask, out Identity identity);
    private static Identity Stat(string path)
    {
        Require(OperatingSystem.IsLinux());
        Require(StatAt(-100, path, 0x100, 0x3ff, out var identity) == 0 && (identity.Mask & 7) == 7);
        return identity;
    }

    private static string Schema()
    {
        JsonNode schema = Json.GetJsonSchemaAsNode(typeof(Manifest));
        void Constrain(JsonNode node)
        {
            if (node is JsonObject obj)
            {
                if (obj["type"]?.ToJsonString() == "\"array\"") obj["maxItems"] = MaximumRecords;
                if (obj["properties"] is JsonObject properties)
                    foreach (var (name, value) in properties)
                    {
                        if (value is not JsonObject field) continue;
                        if (name == "schemaVersion") field["const"] = "0.0.1";
                        if (name.EndsWith("Digest", StringComparison.Ordinal) || name.EndsWith("Sha256", StringComparison.Ordinal) || name == "sha256")
                            field["pattern"] = "^[0-9a-f]{64}$";
                        if (name == "length") { field["minimum"] = 0; field["maximum"] = MaximumFileBytes; }
                        if (name == "path") { field["maxLength"] = 1024; field["minLength"] = 1; }
                        if (name == "kind") field["enum"] = new JsonArray("support", "report", "execution");
                    }
                foreach (var child in obj.Select(pair => pair.Value).OfType<JsonNode>().ToArray()) Constrain(child);
            }
            else if (node is JsonArray array) foreach (var child in array.OfType<JsonNode>()) Constrain(child);
        }
        Constrain(schema);
        schema["$schema"] = "https://json-schema.org/draft/2020-12/schema";
        schema["title"] = "configurator-package-provenance";
        return schema.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n";
    }

    private static int SelfTest()
    {
        Require(Hash([]) == "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855");
        Require(System.Text.Encoding.UTF8.GetString(Canonical(new Entry("a", 0, new string('0', 64))))
            == "{\"path\":\"a\",\"length\":0,\"sha256\":\"" + new string('0', 64) + "\"}");
        foreach (string invalid in new[] { "", "../a", "/a", "a//b", "a\\b", "a/./b", "a:b" }) Require(!Relative(invalid));
        foreach (string invalid in new[] { "{}", "{\"a\":1,\"a\":2}" })
        {
            bool rejected = false;
            try { _ = Parse<Manifest>(System.Text.Encoding.UTF8.GetBytes(invalid)); }
            catch (Exception error) when (error is JsonException or InvalidDataException) { rejected = true; }
            Require(rejected);
        }
        Console.WriteLine("PACKAGE_PROVENANCE_SELF_TEST_PASSED 11");
        return 0;
    }

    private sealed record Entry(string Path, long Length, string Sha256);
    private sealed record EvidenceEntry(string Path, long Length, string Sha256, string Kind);
    private sealed record CheckInput(string Owner, string Session, string Execution, string Report);
    private sealed record CheckBinding(string Owner, string Execution, string Report, string TestCaptureSha256);
    private sealed record Manifest(string SchemaVersion, string Client, string PackageRid,
        string ProductSourceInputsDigest, string BuildRecipeDigest, string PayloadDigest, string EvidenceSetDigest,
        string PublicationCaptureSha256, string PayloadPrefix, Entry[] Payload, EvidenceEntry[] Evidence, CheckBinding[] RequiredChecks);
}
