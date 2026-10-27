#:property RestorePackagesWithLockFile=false
#:property AssemblyName=Configurator.TestEvidence
#:property RootNamespace=Configurator.Ci

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

return args is ["--self-test"] ? Evidence.SelfTest() : Evidence.Run(args);

internal static class Evidence
{
    private const int MaximumCases = 100_000;
    private const long MaximumXmlBytes = 64 * 1024 * 1024;
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
            Require(options.Count == 7 && options.Keys.All(key => key is
                "--owner" or "--rid" or "--run" or "--input" or "--output" or "--exit-code" or "--coverage"));
            string owner = options["--owner"], rid = options["--rid"], run = options["--run"];
            Require(Owners.Contains(owner, StringComparer.Ordinal));
            Require(Regex.IsMatch(rid, @"^(linux|osx|win)-(x64|arm64)$", RegexOptions.CultureInvariant));
            Require(Regex.IsMatch(run, @"^[A-Za-z0-9][A-Za-z0-9-]{0,95}$", RegexOptions.CultureInvariant));
            Require(options["--coverage"] is "required" or "none");
            Require(int.TryParse(options["--exit-code"], out int commandExit) && commandExit is >= 0 and <= 255);
            originalExit = commandExit;
            var diagnostics = new List<string>();
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
            var report = new EvidenceReport(1, owner, rid, run, commandExit,
                diagnostics.Count == 0 ? "validated" : "invalid",
                commandExit == 0 && tests is { Failed: 0 } && diagnostics.Count == 0 ? "passed" : "failed",
                tests, options["--coverage"] == "none" ? "unmeasured" : coverage is null ? "invalid" : "measured",
                coverage, diagnostics);
            Directory.CreateDirectory(options["--output"]);
            string destination = Path.Combine(options["--output"], $"{owner}-{rid}-{run}.json");
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
        error is IOException or UnauthorizedAccessException or XmlException or InvalidDataException or EvidenceInputException
            or FormatException or OverflowException or InvalidOperationException;

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
        foreach (XElement result in results)
        {
            string identity = result.Attribute("testId")?.Value ?? "";
            string execution = result.Attribute("executionId")?.Value ?? "";
            Require(identities.Contains(identity) && execution.Length is > 0 and <= 4096 && executions.Add(execution));
            string outcome = result.Attribute("outcome")?.Value ?? "";
            Require(outcome is "Passed" or "Failed" or "NotExecuted");
            Require(TimeSpan.TryParse(result.Attribute("duration")?.Value, CultureInfo.InvariantCulture, out TimeSpan duration)
                && duration >= TimeSpan.Zero);
            // Even data-driven display names and IDs can contain argument values.
            string digest = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
            cases.Add(new CaseReport(digest, outcome, duration.TotalMilliseconds));
        }
        long passed = cases.Count(item => item.Outcome == "Passed");
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
                ["--owner", "Configurator.Core", "--rid", "linux-x64", "--run", run,
                 "--input", input, "--output", output, "--exit-code", exit, "--coverage", mode];
            JsonElement Report(string run)
            {
                using var parsed = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(output, $"Configurator.Core-linux-x64-{run}.json")));
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
            byte[] retained = File.ReadAllBytes(Path.Combine(output, "Configurator.Core-linux-x64-failed.json"));
            Check(Run(Arguments("failed")) == 7);
            Check(retained.SequenceEqual(File.ReadAllBytes(Path.Combine(output, "Configurator.Core-linux-x64-failed.json"))));
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

    internal sealed record EvidenceReport(int SchemaVersion, string Owner, string Rid, string Run, int CommandExitCode,
        string EvidenceStatus, string TestStatus, TestReport? Tests, string CoverageStatus, CoverageReport? Coverage,
        IReadOnlyList<string> Diagnostics);
    internal sealed record CaseReport(string IdentitySha256, string Outcome, double DurationMilliseconds);
    internal sealed record TestReport(string Assembly, long Passed, long Failed, long Skipped,
        double DurationMilliseconds, IReadOnlyList<CaseReport> Cases);
    internal sealed record CoverageReport(string Assembly, long CoveredLines, long TotalLines, long CoveredBranches, long TotalBranches);
    private sealed class EvidenceInputException(string code) : Exception
    {
        internal string Code { get; } = code;
    }
}

[JsonSerializable(typeof(Evidence.EvidenceReport))]
[JsonSourceGenerationOptions(WriteIndented = true)]
internal partial class EvidenceJsonContext : JsonSerializerContext;
