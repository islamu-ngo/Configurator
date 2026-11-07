#:property RestorePackagesWithLockFile=false
#:property AssemblyName=Configurator.TestEvidence
#:property RootNamespace=Configurator.Ci
#:property PublishAot=false

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.Runtime.InteropServices;
using System.Text.Json.Schema;
using System.Text.Json.Nodes;

return args is ["--self-test"] ? Evidence.SelfTest() : Evidence.Run(args);

internal static class Evidence
{
    private const int MaximumCases = 100_000;
    private const long MaximumXmlBytes = 64 * 1024 * 1024;
    private const long MaximumArchiveBytes = 128 * 1024 * 1024;
    private static readonly string[] Owners =
    [
        "Configurator.Core", "Configurator.Presentation", "Configurator.Artifacts",
        "Configurator.Cli", "Configurator.Terminal", "Configurator.Desktop",
        "Configurator.Browser", "Configurator.Architecture",
        "Configurator.AgentWorkflow", "Configurator.ReleaseEngineering"
    ];

    internal static int Run(string[] arguments)
    {
        int originalExit = 1;
        try
        {
            if (arguments is ["--schema-write"] or ["--schema-check"])
            {
                string path = "schemas/configurator-test-evidence.schema.json";
                string schema = Schema();
                if (arguments[0] == "--schema-write") File.WriteAllText(path, schema);
                else Require(File.ReadAllText(path) == schema);
                Console.WriteLine("EVIDENCE_SCHEMA_VALID");
                return 0;
            }
            if (arguments is ["--check-report", var reportPath])
            {
                ReadReport(reportPath);
                Console.WriteLine("EVIDENCE_REPORT_VALID");
                return 0;
            }
            if (arguments is ["--archive", var input, "--output", var archive])
            {
                Archive(input, archive);
                return 0;
            }
            if (arguments is ["--archive-check", var archivePath])
            {
                CheckArchive(archivePath);
                Console.WriteLine("EVIDENCE_ARCHIVE_VALID");
                return 0;
            }
            // Recover the command status before parsing other inputs, including invalid ones.
            int exitIndex = Array.IndexOf(arguments, "--exit-code");
            if (exitIndex >= 0 && exitIndex + 1 < arguments.Length
                && int.TryParse(arguments[exitIndex + 1], out int parsedExit) && parsedExit is >= 0 and <= 255)
                originalExit = parsedExit;
            var options = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int index = 0; index < arguments.Length; index += 2)
            {
                Require(index + 1 < arguments.Length && options.TryAdd(arguments[index], arguments[index + 1]));
            }
            Require(options.Count is 7 or 8 && options.Keys.All(key => key is
                "--owner" or "--test-runner-rid" or "--package-rid" or "--run" or "--input" or "--output" or "--exit-code" or "--coverage"));
            string owner = options["--owner"], rid = options["--test-runner-rid"], run = options["--run"];
            Require(Owners.Contains(owner, StringComparer.Ordinal));
            Require(Regex.IsMatch(rid, @"^(linux|osx|win)-(x64|arm64)$", RegexOptions.CultureInvariant));
            Require(Regex.IsMatch(run, @"^[A-Za-z0-9][A-Za-z0-9-]{0,95}$", RegexOptions.CultureInvariant));
            Require(options["--coverage"] is "required" or "none");
            Require(int.TryParse(options["--exit-code"], out int commandExit) && commandExit is >= 0 and <= 255);
            originalExit = commandExit;
            var diagnostics = new List<string>();
            string actualRid = RuntimeInformation.RuntimeIdentifier;
            Require(IsRunner(actualRid));
            if (rid != actualRid) diagnostics.Add("RUNNER_HOST_MISMATCH");
            string? packageRid = options.GetValueOrDefault("--package-rid");
            if (packageRid is not null) Require(PackageRids().Contains(packageRid, StringComparer.Ordinal));
            TestReport? tests = null;
            CoverageReport? coverage = null;
            try { tests = ReadTests(Path.Combine(options["--input"], "results.trx"), owner); }
            catch (Exception error) when (IsInputError(error)) { diagnostics.Add("TEST_REPORT_INVALID"); }
            if (options["--coverage"] == "required")
            {
                try { coverage = ReadCoverage(Path.Combine(options["--input"], "coverage.cobertura.xml"), owner); }
                catch (Exception error) when (IsInputError(error))
                {
                    diagnostics.Add(error is EvidenceInputException invalid ? invalid.Code : "COVERAGE_REPORT_INVALID");
                }
            }
            if (commandExit == 0 && tests is { Failed: > 0 }) diagnostics.Add("TEST_EXIT_INCONSISTENT");
            var report = new EvidenceReport(1, owner, actualRid, packageRid, run, commandExit,
                diagnostics.Count == 0 ? "validated" : "invalid",
                commandExit == 0 && tests is { Failed: 0 } && diagnostics.Count == 0 ? "passed" : "failed",
                tests, options["--coverage"] == "none" ? "unmeasured" : coverage is null ? "invalid" : "measured",
                coverage, diagnostics);
            Directory.CreateDirectory(options["--output"]);
            string destination = Path.Combine(options["--output"], $"{owner}-{actualRid}-{run}.json");
            // Never replace a previous selection, retry, owner or host report.
            using (var stream = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                JsonSerializer.Serialize(stream, report, EvidenceJsonContext.Default.EvidenceReport);
            Console.WriteLine(diagnostics.Count == 0 ? "EVIDENCE_VALIDATED" : "EVIDENCE_INVALID");
            return commandExit != 0 ? commandExit : diagnostics.Count == 0 ? 0 : 1;
        }
        catch (Exception error) when (IsInputError(error) || error is KeyNotFoundException or ArgumentException)
        {
            // Exception messages, arguments and paths are never publication data.
            Console.Error.WriteLine("EVIDENCE_PREPARATION_FAILED");
            return originalExit != 0 ? originalExit : 1;
        }
    }

    private static bool IsInputError(Exception error) =>
        error is IOException or UnauthorizedAccessException or XmlException or JsonException or InvalidDataException or EvidenceInputException
            or FormatException or OverflowException or InvalidOperationException;

    private static bool IsRunner(string rid) =>
        Regex.IsMatch(rid, @"^(linux|osx|win)-(x64|arm64)$", RegexOptions.CultureInvariant);

    private static string[] PackageRids()
    {
        using var admission = JsonDocument.Parse(File.ReadAllBytes("eng/configurator/generated/native-release-capabilities.json"));
        return admission.RootElement.GetProperty("surfaces").EnumerateObject()
            .SelectMany(surface => surface.Value.GetProperty("runtimeIdentifiers").EnumerateArray())
            .Select(item => item.GetString()!).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
    }

    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static bool IsHash(string value) =>
        Regex.IsMatch(value, "^[0-9a-f]{64}$", RegexOptions.CultureInvariant);

    private static byte[] ReadBounded(string path)
    {
        var info = new FileInfo(path);
        Require(info.LinkTarget is null);
        using var input = File.OpenRead(path);
        long length = input.Length;
        Require(length is > 0 and <= MaximumXmlBytes);
        var bytes = new byte[(int)length];
        input.ReadExactly(bytes);
        Require(input.ReadByte() == -1);
        return bytes;
    }

    private static EvidenceReport ReadReport(string path)
    {
        byte[] bytes = ReadBounded(path);
        return ReadReport(bytes);
    }

    private static EvidenceReport ReadReport(byte[] bytes)
    {
        var report = JsonSerializer.Deserialize(bytes, EvidenceJsonContext.Default.EvidenceReport);
        Require(report is not null);
        // Canonical round-trip rejects unknown, duplicate, omitted and noncanonical fields.
        Require(bytes.SequenceEqual(JsonSerializer.SerializeToUtf8Bytes(report, EvidenceJsonContext.Default.EvidenceReport)));
        Validate(report!);
        return report!;
    }

    private static readonly string[] DiagnosticCodes =
    [
        "TEST_REPORT_INVALID", "TEST_EXIT_INCONSISTENT", "RUNNER_HOST_MISMATCH",
        "REPORT_INVALID", "COVERAGE_REPORT_INVALID", "COVERAGE_OWNER_INVALID",
        "COVERAGE_BRANCH_FORMAT_INVALID", "COVERAGE_DUPLICATE_LINE_INVALID",
        "COVERAGE_LINES_MISSING", "COVERAGE_BRANCHES_MISSING"
    ];

    private static void Validate(EvidenceReport report)
    {
        Require(report.SchemaVersion == 1 && Owners.Contains(report.Owner, StringComparer.Ordinal)
            && IsRunner(report.TestRunnerRid) && Regex.IsMatch(report.Run, "^[A-Za-z0-9][A-Za-z0-9-]{0,95}$", RegexOptions.CultureInvariant)
            && report.CommandExitCode is >= 0 and <= 255);
        Require(report.PackageRid is null || PackageRids().Contains(report.PackageRid, StringComparer.Ordinal));
        Require(report.Diagnostics is { Count: <= 10 }
            && report.Diagnostics.Distinct(StringComparer.Ordinal).Count() == report.Diagnostics.Count
            && report.Diagnostics.All(code => DiagnosticCodes.Contains(code, StringComparer.Ordinal)));
        Require(report.EvidenceStatus == (report.Diagnostics.Count == 0 ? "validated" : "invalid"));
        Require((report.Tests is null) == report.Diagnostics.Contains("TEST_REPORT_INVALID", StringComparer.Ordinal));
        if (report.Tests is { } tests)
        {
            Require(tests.Assembly == report.Owner + ".Tests" && tests.Cases is { Count: > 0 and <= MaximumCases }
                && double.IsFinite(tests.DurationMilliseconds) && tests.DurationMilliseconds >= 0);
            Require(tests.Cases.All(item => item is not null && IsHash(item.IdentitySha256)
                && item.Outcome is "Passed" or "Failed" or "NotExecuted"
                && double.IsFinite(item.DurationMilliseconds) && item.DurationMilliseconds >= 0));
            Require(tests.Cases.Select(item => item.IdentitySha256).Distinct(StringComparer.Ordinal).Count() == tests.Cases.Count);
            Require(tests.Passed == tests.Cases.Count(item => item.Outcome == "Passed")
                && tests.Failed == tests.Cases.Count(item => item.Outcome == "Failed")
                && tests.Skipped == tests.Cases.Count(item => item.Outcome == "NotExecuted"));
        }
        Require(report.Diagnostics.Contains("TEST_EXIT_INCONSISTENT", StringComparer.Ordinal)
            == (report.CommandExitCode == 0 && report.Tests is { Failed: > 0 }));
        Require(report.TestStatus == (report.CommandExitCode == 0 && report.Tests is { Failed: 0 }
            && report.Diagnostics.Count == 0 ? "passed" : "failed"));
        bool coverageError = report.Diagnostics.Any(code => code.StartsWith("COVERAGE_", StringComparison.Ordinal) || code == "REPORT_INVALID");
        Require(report.CoverageStatus is "measured" or "unmeasured" or "invalid");
        Require((report.CoverageStatus == "invalid") == coverageError);
        Require((report.Coverage is not null) == (report.CoverageStatus == "measured"));
        if (report.Coverage is { } coverage)
            Require(coverage.Assembly == report.Owner && coverage.TotalLines is > 0 and <= 1_000_000
                && coverage.CoveredLines >= 0 && coverage.CoveredLines <= coverage.TotalLines
                && coverage.TotalBranches > 0 && coverage.CoveredBranches >= 0 && coverage.CoveredBranches <= coverage.TotalBranches);
    }

    private static void Create(string path, byte[] bytes)
    {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        stream.Write(bytes);
    }

    private static void Archive(string input, string output)
    {
        Require(new DirectoryInfo(input).LinkTarget is null);
        var records = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);
        var identities = new HashSet<string>(StringComparer.Ordinal);
        long totalBytes = 0;
        foreach (string file in Directory.EnumerateFiles(input).Take(1001))
        {
            Require(records.Count < 1000 && Path.GetExtension(file) == ".json");
            byte[] bytes = ReadBounded(file);
            totalBytes += bytes.Length;
            Require(totalBytes <= MaximumArchiveBytes);
            EvidenceReport report = ReadReport(bytes);
            Require(identities.Add($"{report.Owner}/{report.TestRunnerRid}/{report.Run}"));
            Require(records.TryAdd(Hash(bytes), bytes));
        }
        Require(records.Count > 0 && !Directory.EnumerateDirectories(input).Any());
        var index = new ArchiveIndex(1, records.Keys.ToArray());
        byte[] indexBytes = JsonSerializer.SerializeToUtf8Bytes(index, EvidenceJsonContext.Default.ArchiveIndex);
        string digest = Hash(indexBytes);
        Directory.CreateDirectory(output);
        Require(new DirectoryInfo(output).LinkTarget is null);
        string destination = Path.Combine(output, digest);
        Require(!Directory.Exists(destination));
        Directory.CreateDirectory(destination);
        // Index is the completion marker. Partial failures retain all already written evidence.
        foreach (var record in records) Create(Path.Combine(destination, record.Key + ".json"), record.Value);
        Create(Path.Combine(destination, "index.json"), indexBytes);
        Console.WriteLine("EVIDENCE_ARCHIVED " + digest);
    }

    private static void CheckArchive(string root)
    {
        Require(new DirectoryInfo(root).LinkTarget is null && !Directory.EnumerateFiles(root).Any());
        string[] directories = Directory.EnumerateDirectories(root).Take(1001).ToArray();
        Require(directories.Length is > 0 and <= 1000);
        foreach (string directory in directories)
        {
            long totalBytes = 0;
            string digest = Path.GetFileName(directory);
            Require(IsHash(digest) && new DirectoryInfo(directory).LinkTarget is null
                && !Directory.EnumerateDirectories(directory).Any());
            byte[] bytes = ReadBounded(Path.Combine(directory, "index.json"));
            Require(Hash(bytes) == digest);
            var index = JsonSerializer.Deserialize(bytes, EvidenceJsonContext.Default.ArchiveIndex);
            Require(index is { SchemaVersion: 1, Reports.Count: > 0 and <= 1000 });
            Require(bytes.SequenceEqual(JsonSerializer.SerializeToUtf8Bytes(index, EvidenceJsonContext.Default.ArchiveIndex)));
            Require(index!.Reports.All(IsHash) && index.Reports.SequenceEqual(index.Reports.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)));
            Require(Directory.EnumerateFiles(directory).Count() == index.Reports.Count + 1);
            var identities = new HashSet<string>(StringComparer.Ordinal);
            foreach (string hash in index.Reports)
            {
                string path = Path.Combine(directory, hash + ".json");
                byte[] reportBytes = ReadBounded(path);
                totalBytes += reportBytes.Length;
                Require(totalBytes <= MaximumArchiveBytes && Hash(reportBytes) == hash);
                EvidenceReport report = ReadReport(reportBytes);
                Require(identities.Add($"{report.Owner}/{report.TestRunnerRid}/{report.Run}"));
            }
        }
    }

    private static string Schema()
    {
        JsonNode schema = EvidenceJsonContext.Default.Options.GetJsonSchemaAsNode(typeof(EvidenceReport),
            new JsonSchemaExporterOptions
            {
                TreatNullObliviousAsNonNullable = true,
                TransformSchemaNode = (context, node) =>
                {
                    if (context.TypeInfo.Kind == System.Text.Json.Serialization.Metadata.JsonTypeInfoKind.Object && node is JsonObject obj)
                        obj["additionalProperties"] = false;
                    return node;
                }
            });
        schema["$schema"] = "https://json-schema.org/draft/2020-12/schema";
        schema["$id"] = "https://islamu.org/configurator/schemas/configurator-test-evidence.schema.json";
        var properties = schema["properties"]!.AsObject();
        properties["SchemaVersion"] = new JsonObject { ["const"] = 1 };
        properties["Owner"] = new JsonObject { ["enum"] = new JsonArray(Owners.Select(value => JsonValue.Create(value)).ToArray()) };
        properties["TestRunnerRid"] = new JsonObject { ["type"] = "string", ["pattern"] = "^(linux|osx|win)-(x64|arm64)$" };
        properties["PackageRid"] = new JsonObject { ["enum"] = new JsonArray(PackageRids().Select(value => (JsonNode?)JsonValue.Create(value)).Append(null).ToArray()) };
        properties["Run"] = new JsonObject { ["type"] = "string", ["pattern"] = "^[A-Za-z0-9][A-Za-z0-9-]{0,95}$" };
        properties["CommandExitCode"] = new JsonObject { ["type"] = "integer", ["minimum"] = 0, ["maximum"] = 255 };
        properties["EvidenceStatus"] = new JsonObject { ["enum"] = new JsonArray("validated", "invalid") };
        properties["TestStatus"] = new JsonObject { ["enum"] = new JsonArray("passed", "failed") };
        properties["CoverageStatus"] = new JsonObject { ["enum"] = new JsonArray("measured", "unmeasured", "invalid") };
        properties["Diagnostics"] = new JsonObject
        {
            ["type"] = "array", ["maxItems"] = 10, ["uniqueItems"] = true,
            ["items"] = new JsonObject { ["enum"] = new JsonArray(DiagnosticCodes.Select(value => JsonValue.Create(value)).ToArray()) }
        };
        schema["description"] = "Canonical safe owner report. Cross-field truth, canonical bytes and archive hashes are additionally enforced by prepare-test-evidence.cs --check-report/--archive-check. No payload qualification or custody claim.";
        return schema.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n";
    }

    private static void Require(bool condition, string code = "REPORT_INVALID")
    {
        if (!condition) throw new EvidenceInputException(code);
    }

    private static XDocument ReadXml(string path)
    {
        using var stream = File.OpenRead(path);
        Require(stream.Length is > 0 and <= MaximumXmlBytes);
        using var reader = XmlReader.Create(stream, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = MaximumXmlBytes
        });
        return XDocument.Load(reader);
    }

    private static long Number(XElement element, string name)
    {
        Require(long.TryParse(element.Attribute(name)?.Value, NumberStyles.None, CultureInfo.InvariantCulture, out long value)
            && value >= 0);
        return value;
    }

    private static TestReport ReadTests(string path, string owner)
    {
        XDocument document = ReadXml(path);
        XNamespace ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
        Require(document.Root?.Name == ns + "TestRun");
        XElement root = document.Root!;
        XElement[] definitions = root.Element(ns + "TestDefinitions")?.Elements(ns + "UnitTest").ToArray() ?? [];
        Require(definitions.Length is > 0 and <= MaximumCases);
        var identities = new HashSet<string>(StringComparer.Ordinal);
        foreach (XElement definition in definitions)
        {
            string storage = definition.Attribute("storage")?.Value.Replace('\\', '/') ?? "";
            Require(string.Equals(storage.Split('/')[^1], owner + ".Tests.dll", StringComparison.OrdinalIgnoreCase));
            string identity = definition.Attribute("id")?.Value ?? "";
            Require(identity.Length is > 0 and <= 4096 && identities.Add(identity));
        }
        XElement[] results = root.Element(ns + "Results")?.Elements(ns + "UnitTestResult").ToArray() ?? [];
        Require(results.Length is > 0 and <= MaximumCases);
        var cases = new List<CaseReport>(results.Length);
        var executions = new HashSet<string>(StringComparer.Ordinal);
        var resultIdentities = new HashSet<string>(StringComparer.Ordinal);
        foreach (XElement result in results)
        {
            string identity = result.Attribute("testId")?.Value ?? "";
            string execution = result.Attribute("executionId")?.Value ?? "";
            Require(identities.Contains(identity) && resultIdentities.Add(identity)
                && execution.Length is > 0 and <= 4096 && executions.Add(execution));
            string outcome = result.Attribute("outcome")?.Value ?? "";
            Require(outcome is "Passed" or "Failed" or "NotExecuted");
            Require(TimeSpan.TryParse(result.Attribute("duration")?.Value, CultureInfo.InvariantCulture, out TimeSpan duration)
                && duration >= TimeSpan.Zero);
            // Even data-driven display names and IDs can contain argument values.
            string digest = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
            cases.Add(new CaseReport(digest, outcome, duration.TotalMilliseconds));
        }
        long passed = cases.Count(item => item.Outcome == "Passed");
        Require(identities.SetEquals(resultIdentities));
        long failed = cases.Count(item => item.Outcome == "Failed");
        long skipped = cases.Count(item => item.Outcome == "NotExecuted");
        XElement counters = root.Element(ns + "ResultSummary")?.Element(ns + "Counters") ?? throw new InvalidDataException();
        Require(Number(counters, "total") == results.Length
            && Number(counters, "passed") == passed && Number(counters, "failed") == failed
            && Number(counters, "notExecuted") == skipped && Number(counters, "executed") == passed + failed);
        XElement times = root.Element(ns + "Times") ?? throw new InvalidDataException();
        bool validStart = DateTimeOffset.TryParse(times.Attribute("start")?.Value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var start);
        bool validFinish = DateTimeOffset.TryParse(times.Attribute("finish")?.Value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var finish);
        Require(validStart && validFinish && finish >= start);
        return new TestReport(owner + ".Tests", passed, failed, skipped, (finish - start).TotalMilliseconds, cases);
    }

    private static CoverageReport ReadCoverage(string path, string owner)
    {
        XDocument document = ReadXml(path);
        Require(document.Root?.Name == "coverage");
        XElement[] packages = document.Root!.Element("packages")?.Elements("package").ToArray() ?? [];
        XElement[] selected = packages.Where(package =>
            package.Attribute("name")?.Value is string name && (name == owner || name == owner + ".dll")).ToArray();
        Require(selected.Length == 1, "COVERAGE_OWNER_INVALID");
        // Only the selected product assembly is measured; dependencies/test assemblies do not inflate its denominator.
        var lines = new Dictionary<(string Class, string File, long Line), (bool Hit, long Covered, long Total)>();
        XElement[] classes = selected[0].Element("classes")?.Elements("class").ToArray() ?? [];
        Require(classes.Length is > 0 and <= MaximumCases);
        int inspected = 0;
        foreach (XElement item in classes)
        {
            string file = item.Attribute("filename")?.Value ?? "";
            string type = item.Attribute("name")?.Value ?? "";
            Require(file.Length is > 0 and <= 4096);
            Require(type.Length is > 0 and <= 4096);
            foreach (XElement line in item.Element("lines")?.Elements("line") ?? [])
            {
                Require(++inspected <= 1_000_000);
                long number = Number(line, "number"), hits = Number(line, "hits");
                Require(number > 0);
                long covered = 0, total = 0;
                string branch = line.Attribute("branch")?.Value ?? "false";
                Require(branch is "true" or "false" or "True" or "False");
                if (string.Equals(branch, "true", StringComparison.OrdinalIgnoreCase))
                {
                    Match counts = Regex.Match(line.Attribute("condition-coverage")?.Value ?? "",
                        @"^\d+(?:\.\d+)?%\s+\((\d+)/(\d+)\)$", RegexOptions.CultureInvariant);
                    Require(counts.Success, "COVERAGE_BRANCH_FORMAT_INVALID");
                    covered = long.Parse(counts.Groups[1].Value, CultureInfo.InvariantCulture);
                    total = long.Parse(counts.Groups[2].Value, CultureInfo.InvariantCulture);
                    Require(total > 0 && covered <= total && total <= 1_000_000);
                }
                // Cobertura counts class/line entries, including distinct generated classes sharing a source line.
                var key = (type, file, number);
                if (lines.TryGetValue(key, out var previous))
                {
                    // Ambiguous branch merging cannot produce a trustworthy denominator.
                    Require(previous.Covered == covered && previous.Total == total, "COVERAGE_DUPLICATE_LINE_INVALID");
                    lines[key] = (previous.Hit || hits > 0, covered, total);
                }
                else lines.Add(key, (hits > 0, covered, total));
            }
        }
        Require(lines.Count > 0, "COVERAGE_LINES_MISSING");
        long coveredLines = lines.Values.Count(line => line.Hit);
        long coveredBranches = lines.Values.Sum(line => line.Covered);
        long totalBranches = lines.Values.Sum(line => line.Total);
        Require(totalBranches > 0, "COVERAGE_BRANCHES_MISSING");
        return new CoverageReport(owner, coveredLines, lines.Count, coveredBranches, totalBranches);
    }

    internal static int SelfTest()
    {
        string directory = Path.Combine(Path.GetTempPath(), "configurator-evidence-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string input = Path.Combine(directory, "raw"), output = Path.Combine(directory, "safe");
            Directory.CreateDirectory(input);
            const string canary = "PRIVATE-CANARY-value-and-path";
            const string trx = """
                <TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
                  <Times start="2026-01-01T00:00:00Z" finish="2026-01-01T00:00:01Z" />
                  <TestDefinitions>
                    <UnitTest id="one-PRIVATE-CANARY-value-and-path" storage="/PRIVATE-CANARY-value-and-path/Configurator.Core.Tests.dll" />
                    <UnitTest id="two" storage="Configurator.Core.Tests.dll" />
                    <UnitTest id="three" storage="Configurator.Core.Tests.dll" />
                  </TestDefinitions>
                  <Results>
                    <UnitTestResult testId="one-PRIVATE-CANARY-value-and-path" executionId="1" outcome="Passed" duration="00:00:00.1" testName="PRIVATE-CANARY-value-and-path"><Output><StdOut>PRIVATE-CANARY-value-and-path</StdOut></Output></UnitTestResult>
                    <UnitTestResult testId="two" executionId="2" outcome="Failed" duration="00:00:00.2"><Output><ErrorInfo><Message>PRIVATE-CANARY-value-and-path</Message></ErrorInfo></Output></UnitTestResult>
                    <UnitTestResult testId="three" executionId="3" outcome="NotExecuted" duration="00:00:00" />
                  </Results>
                  <ResultSummary><Counters total="3" executed="2" passed="1" failed="1" notExecuted="1" /></ResultSummary>
                </TestRun>
                """;
            const string cobertura = """
                <coverage><sources><source>/PRIVATE-CANARY-value-and-path</source></sources><packages>
                  <package name="Configurator.Core"><classes><class name="Example" filename="/PRIVATE-CANARY-value-and-path/File.cs"><lines>
                    <line number="10" hits="1" branch="true" condition-coverage="50% (1/2)" />
                    <line number="11" hits="0" branch="false" />
                  </lines></class>
                  <class name="GeneratedExample" filename="/PRIVATE-CANARY-value-and-path/File.cs"><lines>
                    <line number="10" hits="0" branch="true" condition-coverage="0% (0/2)" />
                  </lines></class></classes></package>
                  <package name="Dependency"><classes /></package>
                </packages></coverage>
                """;
            string trxPath = Path.Combine(input, "results.trx"), coveragePath = Path.Combine(input, "coverage.cobertura.xml");
            File.WriteAllText(trxPath, trx);
            File.WriteAllText(coveragePath, cobertura);
            string[] Arguments(string run, string exit = "7", string mode = "required") =>
                ["--owner", "Configurator.Core", "--test-runner-rid", RuntimeInformation.RuntimeIdentifier, "--run", run,
                 "--input", input, "--output", output, "--exit-code", exit, "--coverage", mode];
            JsonElement Report(string run)
            {
                using var parsed = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(output, $"Configurator.Core-{RuntimeInformation.RuntimeIdentifier}-{run}.json")));
                return parsed.RootElement.Clone();
            }
            void Check(bool condition) { if (!condition) throw new InvalidDataException(); }
            int checks = 0;
            void InvalidCoverage(string name, string? content)
            {
                if (content is null) File.Delete(coveragePath); else File.WriteAllText(coveragePath, content);
                Check(Run(Arguments(name)) == 7);
                var report = Report(name);
                Check(report.GetProperty("CoverageStatus").GetString() == "invalid"
                    && report.GetProperty("Coverage").ValueKind == JsonValueKind.Null
                    && report.GetProperty("Tests").GetProperty("Failed").GetInt32() == 1);
                checks++;
            }
            Check(Run(Arguments("failed")) == 7);
            var failed = Report("failed");
            Check(failed.GetProperty("Tests").GetProperty("Passed").GetInt32() == 1
                && failed.GetProperty("Tests").GetProperty("Failed").GetInt32() == 1
                && failed.GetProperty("Tests").GetProperty("Skipped").GetInt32() == 1
                && failed.GetProperty("TestStatus").GetString() == "failed");
            var measured = failed.GetProperty("Coverage");
            Check(measured.GetProperty("CoveredLines").GetInt64() == 1 && measured.GetProperty("TotalLines").GetInt64() == 3
                && measured.GetProperty("CoveredBranches").GetInt64() == 1 && measured.GetProperty("TotalBranches").GetInt64() == 4);
            byte[] retained = File.ReadAllBytes(Path.Combine(output, $"Configurator.Core-{RuntimeInformation.RuntimeIdentifier}-failed.json"));
            Check(Run(Arguments("failed")) == 7);
            Check(retained.SequenceEqual(File.ReadAllBytes(Path.Combine(output, $"Configurator.Core-{RuntimeInformation.RuntimeIdentifier}-failed.json"))));
            checks += 2;
            InvalidCoverage("missing", null);
            InvalidCoverage("empty", "");
            InvalidCoverage("malformed", "<coverage>");
            InvalidCoverage("wrong-owner", cobertura.Replace("Configurator.Core", "Configurator.Presentation", StringComparison.Ordinal));
            InvalidCoverage("no-lines", "<coverage><packages><package name=\"Configurator.Core\"><classes /></package></packages></coverage>");
            InvalidCoverage("bad-branches", cobertura.Replace("(1/2)", "(3/2)", StringComparison.Ordinal));
            InvalidCoverage("duplicate-owner", cobertura.Replace("<package name=\"Dependency\"><classes /></package>",
                "<package name=\"Configurator.Core\"><classes /></package>", StringComparison.Ordinal));
            InvalidCoverage("dtd", "<!DOCTYPE coverage [<!ENTITY leak SYSTEM 'file:///PRIVATE-CANARY-value-and-path'>]><coverage>&leak;</coverage>");
            File.WriteAllText(coveragePath, cobertura);
            Check(Run(Arguments("inconsistent", "0")) == 1);
            Check(Report("inconsistent").GetProperty("EvidenceStatus").GetString() == "invalid");
            checks++;
            File.WriteAllText(trxPath, trx.Replace("Configurator.Core.Tests.dll", "Configurator.Cli.Tests.dll", StringComparison.Ordinal));
            Check(Run(Arguments("wrong-tests", "0")) == 1);
            Check(Report("wrong-tests").GetProperty("Tests").ValueKind == JsonValueKind.Null);
            checks++;
            File.WriteAllText(trxPath, trx.Replace("total=\"3\"", "total=\"4\"", StringComparison.Ordinal));
            Check(Run(Arguments("bad-counts", "0")) == 1);
            checks++;
            File.Delete(trxPath);
            Check(Run(Arguments("missing-tests", "9")) == 9);
            Check(Report("missing-tests").GetProperty("TestStatus").GetString() == "failed");
            checks++;
            string passing = trx.Replace("outcome=\"Failed\"", "outcome=\"Passed\"", StringComparison.Ordinal)
                .Replace("passed=\"1\" failed=\"1\"", "passed=\"2\" failed=\"0\"", StringComparison.Ordinal);
            File.WriteAllText(trxPath, passing);
            Check(Run(Arguments("passed", "0")) == 0);
            Check(Report("passed").GetProperty("TestStatus").GetString() == "passed");
            Check(Run(Arguments("unmeasured", "0", "none")) == 0);
            Check(Report("unmeasured").GetProperty("CoverageStatus").GetString() == "unmeasured");
            checks += 2;
            Check(Run(Arguments("passed", "0")) == 1);
            Check(Run(Arguments("../invalid", "11")) == 11);
            checks += 2;
            string[] mismatchedHost = Arguments("wrong-host", "13", "none");
            mismatchedHost[3] = RuntimeInformation.RuntimeIdentifier == "win-x64" ? "linux-x64" : "win-x64";
            Check(Run(mismatchedHost) == 13);
            Check(Report("wrong-host").GetProperty("TestRunnerRid").GetString() == RuntimeInformation.RuntimeIdentifier
                && Report("wrong-host").GetProperty("EvidenceStatus").GetString() == "invalid"
                && Report("wrong-host").GetProperty("Tests").GetProperty("Passed").GetInt32() == 2);
            Check(Run([.. Arguments("package-target", "0", "none"), "--package-rid", "linux-musl-x64"]) == 0);
            Check(Report("package-target").GetProperty("PackageRid").GetString() == "linux-musl-x64"
                && Report("package-target").GetProperty("TestRunnerRid").GetString() == RuntimeInformation.RuntimeIdentifier);
            checks += 2;
            // A copied archive must work without the original report directory.
            string archive = Path.Combine(directory, "archive"), copy = Path.Combine(directory, "copy");
            Check(Run(["--archive", output, "--output", archive]) == 0);
            Directory.CreateDirectory(copy);
            foreach (string file in Directory.EnumerateFiles(archive, "*", SearchOption.AllDirectories))
            {
                string destination = Path.Combine(copy, Path.GetRelativePath(archive, file));
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(file, destination);
            }
            Check(Run(["--archive-check", copy]) == 0);
            Check(Run(["--archive", output, "--output", archive]) == 1);
            checks += 3;
            string safe = File.ReadAllText(Path.Combine(output, $"Configurator.Core-{RuntimeInformation.RuntimeIdentifier}-passed.json"));
            string altered = Path.Combine(directory, "altered.json");
            foreach (string invalid in new[]
            {
                safe.Replace("\"SchemaVersion\": 1", "\"SchemaVersion\": 2", StringComparison.Ordinal),
                safe.Replace("\"Owner\":", "\"Unknown\": 1, \"Owner\":", StringComparison.Ordinal),
                safe.Replace("\"Owner\":", "\"Owner\": \"Configurator.Core\", \"Owner\":", StringComparison.Ordinal),
                safe.Replace("\"Passed\": 2", "\"Passed\": 3", StringComparison.Ordinal),
                safe.Replace("\"CommandExitCode\": 0", "\"CommandExitCode\": 7", StringComparison.Ordinal),
                safe.Replace("\"TestStatus\": \"passed\"", "\"TestStatus\": \"failed\"", StringComparison.Ordinal),
                safe.Replace("\"CoverageStatus\": \"measured\"", "\"CoverageStatus\": \"unmeasured\"", StringComparison.Ordinal),
                safe.Replace("\"Assembly\": \"Configurator.Core.Tests\"", "\"Assembly\": \"Configurator.Cli.Tests\"", StringComparison.Ordinal),
                safe.Replace("\"TotalLines\": 3", "\"TotalLines\": 0", StringComparison.Ordinal),
                safe.Replace("\"PackageRid\": null", "\"PackageRid\": \"unadmitted-rid\"", StringComparison.Ordinal),
                safe.Replace("\"" + RuntimeInformation.RuntimeIdentifier + "\"", "\"linux-musl-x64\"", StringComparison.Ordinal),
                safe.Replace("\"IdentitySha256\": \"", "\"IdentitySha256\": \"z", StringComparison.Ordinal)
            })
            {
                File.WriteAllText(altered, invalid);
                Check(Run(["--check-report", altered]) == 1);
                checks++;
            }
            var nullIdentity = JsonNode.Parse(safe)!;
            nullIdentity["Tests"]!["Cases"]![0]!["IdentitySha256"] = null;
            File.WriteAllText(altered, nullIdentity.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            Check(Run(["--check-report", altered]) == 1);
            checks++;
            // Coverage parsing permits one million class-line entries, independently
            // of the one hundred thousand test/class ceiling.
            var largeCoverage = JsonNode.Parse(safe)!;
            largeCoverage["Coverage"]!["TotalLines"] = 100001;
            File.WriteAllText(altered, largeCoverage.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            Check(Run(["--check-report", altered]) == 0);
            checks++;
            string archivedReport = Directory.EnumerateFiles(copy, "*.json", SearchOption.AllDirectories)
                .First(file => Path.GetFileName(file) != "index.json");
            File.AppendAllText(archivedReport, " ");
            Check(Run(["--archive-check", copy]) == 1);
            File.Delete(archivedReport);
            Check(Run(["--archive-check", copy]) == 1);
            checks += 2;
            string unsafeInput = Path.Combine(directory, "unsafe-input");
            Directory.CreateDirectory(unsafeInput);
            File.WriteAllText(Path.Combine(unsafeInput, "command.log"), canary);
            Check(Run(["--archive", unsafeInput, "--output", Path.Combine(directory, "unsafe-output")]) == 1);
            Check(!Directory.Exists(Path.Combine(directory, "unsafe-output")));
            checks++;
            foreach (string file in Directory.EnumerateFiles(output))
                Check(!File.ReadAllText(file).Contains(canary, StringComparison.Ordinal));
            Console.WriteLine($"SELF_TEST_PASSED ({checks} synthetic vectors; not measured product coverage)");
            return 0;
        }
        catch (Exception error) when (IsInputError(error))
        {
            Console.Error.WriteLine("SELF_TEST_FAILED");
            return 1;
        }
        finally { Directory.Delete(directory, true); }
    }

    internal sealed record EvidenceReport(int SchemaVersion, string Owner, string TestRunnerRid, string? PackageRid, string Run, int CommandExitCode,
        string EvidenceStatus, string TestStatus, TestReport? Tests, string CoverageStatus, CoverageReport? Coverage,
        IReadOnlyList<string> Diagnostics);
    internal sealed record CaseReport(string IdentitySha256, string Outcome, double DurationMilliseconds);
    internal sealed record TestReport(string Assembly, long Passed, long Failed, long Skipped,
        double DurationMilliseconds, IReadOnlyList<CaseReport> Cases);
    internal sealed record CoverageReport(string Assembly, long CoveredLines, long TotalLines, long CoveredBranches, long TotalBranches);
    internal sealed record ArchiveIndex(int SchemaVersion, IReadOnlyList<string> Reports);
    private sealed class EvidenceInputException(string code) : Exception
    {
        internal string Code { get; } = code;
    }
}

[JsonSerializable(typeof(Evidence.EvidenceReport))]
[JsonSerializable(typeof(Evidence.ArchiveIndex))]
[JsonSourceGenerationOptions(WriteIndented = true)]
internal partial class EvidenceJsonContext : JsonSerializerContext;
