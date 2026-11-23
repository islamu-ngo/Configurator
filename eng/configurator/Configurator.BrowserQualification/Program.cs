using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Configurator.BrowserQualification;

/// <summary>
/// Unified Playwright and Chromium CDP qualification runner for published static Browser builds.
/// </summary>
internal static class Program
{
    private const string SyntheticDigestA = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string SyntheticDigestB = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private const string SyntheticCommit = "1111111111111111111111111111111111111111";

    internal static async Task<int> Main(string[] args)
    {
        if (args is ["--self-test"])
            return RunSelfTest();

        if (!TryParseArguments(args, out CliOptions? options, out string? error))
        {
            Console.Error.WriteLine(error ?? "Invalid arguments.");
            PrintUsage();
            return 64;
        }

        if (options!.MeasureInteractive)
            return await RunInteractiveMeasurementAsync(options);

        return await RunQualificationAsync(options);
    }

    private static async Task<int> RunQualificationAsync(CliOptions options)
    {
        QualificationReport report = await QualificationChecks.RunAllEnginesAsync(
            options.BaseUrl!,
            options.Engines,
            options.MinimumExpectedChecks);

        if (!string.IsNullOrWhiteSpace(options.OutputPath))
        {
            EnsureParentDirectory(options.OutputPath);
            await File.WriteAllTextAsync(options.OutputPath, QualificationReceiptWriter.SerializeReport(report));
        }

        if (!string.IsNullOrWhiteSpace(options.ReceiptOutputPath))
        {
            var identity = new ReceiptIdentity(
                options.SourceDigest!,
                options.BuildDigest!,
                options.PayloadDigest!,
                options.Commit!,
                options.DirtyDigest,
                options.HostDigest!,
                options.TestRunnerRid);
            string receiptJson = QualificationReceiptWriter.BuildSupportReceiptJson(report, identity);
            EnsureParentDirectory(options.ReceiptOutputPath);
            await File.WriteAllTextAsync(options.ReceiptOutputPath, receiptJson);
        }

        (bool passed, string? failureCode) = QualificationReceiptWriter.ValidateRunGate(
            report,
            options.MinimumExpectedChecks,
            options.Engines);

        Console.WriteLine($"QUALIFICATION_SUMMARY passed={passed} engines={string.Join(",", options.Engines)} failure={failureCode ?? "none"}");
        return passed ? 0 : 1;
    }

    private static async Task<int> RunInteractiveMeasurementAsync(CliOptions options)
    {
        (string interactionJson, string qf01Json, bool qf01Passed) = await CompoundRootBenchmarks.RunAsync(
            options.BaseUrl!,
            options.FixturesDirectory,
            options.FixtureDigest!,
            options.PayloadDigest!,
            options.HostDigest!,
            options.IncludeExtended);

        if (!string.IsNullOrWhiteSpace(options.InteractionOutputPath))
        {
            EnsureParentDirectory(options.InteractionOutputPath);
            await File.WriteAllTextAsync(options.InteractionOutputPath, interactionJson);
        }

        if (!string.IsNullOrWhiteSpace(options.Qf01OutputPath))
        {
            EnsureParentDirectory(options.Qf01OutputPath);
            await File.WriteAllTextAsync(options.Qf01OutputPath, qf01Json);
        }

        if (!CompoundRootBenchmarks.ValidateInteractionPayload(interactionJson, out string? failureCode))
        {
            Console.Error.WriteLine($"INTERACTIVE_MEASUREMENT_INVALID_PAYLOAD reason={failureCode}");
            return 1;
        }

        Console.WriteLine($"INTERACTIVE_MEASUREMENT_COMPLETE qf01Passed={qf01Passed}");
        return 0;
    }

    private static int RunSelfTest()
    {
        int failures = 0;

        void AssertCase(string name, bool condition)
        {
            Console.WriteLine($"SELF_TEST case={name} passed={condition}");
            if (!condition) failures++;
        }

        // 1. Complete 16-check multi-engine report passes run gate and receipt validation.
        QualificationReport completeReport = BuildSyntheticReport(QualificationChecks.DefaultEngines, QualificationChecks.RequiredCheckIds, allPassed: true);
        (bool gatePassed, string? gateError) = QualificationReceiptWriter.ValidateRunGate(completeReport, 16, QualificationChecks.DefaultEngines);
        AssertCase("complete-16-checks-three-engines-passes", gatePassed && gateError is null);

        var identity = new ReceiptIdentity(
            SyntheticDigestA,
            SyntheticDigestA,
            SyntheticDigestA,
            SyntheticCommit,
            null,
            SyntheticDigestA,
            "linux-x64");
        string validReceipt = QualificationReceiptWriter.BuildSupportReceiptJson(completeReport, identity);
        AssertCase("valid-support-receipt-passes",
            QualificationReceiptWriter.ValidateReceiptContract(validReceipt, SyntheticDigestA, SyntheticDigestA, SyntheticDigestA));

        // 2. Fewer than 16 checks must fail closed.
        QualificationReport fifteenChecks = BuildSyntheticReport(
            QualificationChecks.DefaultEngines,
            QualificationChecks.RequiredCheckIds.Take(15).ToArray(),
            allPassed: true);
        (bool fifteenPassed, string? fifteenError) = QualificationReceiptWriter.ValidateRunGate(fifteenChecks, 16, QualificationChecks.DefaultEngines);
        AssertCase("missing-check-id-fails-closed", !fifteenPassed && fifteenError == "insufficient-checks");

        // 3. Skipped / missing engine must fail closed.
        QualificationReport skippedEngine = BuildSyntheticReport(["chromium", "firefox"], QualificationChecks.RequiredCheckIds, allPassed: true);
        (bool skippedPassed, string? skippedError) = QualificationReceiptWriter.ValidateRunGate(skippedEngine, 16, QualificationChecks.DefaultEngines);
        AssertCase("skipped-engine-fails-closed", !skippedPassed && skippedError == "engine-missing");

        // 4. Unlaunched engine must fail closed.
        QualificationReport failedLaunch = completeReport with
        {
            Engines =
            [
                completeReport.Engines[0],
                completeReport.Engines[1],
                new EngineRunSummary("webkit", false, null, "engine-launch-failed", [])
            ]
        };
        (bool failedLaunchPassed, string? failedLaunchError) = QualificationReceiptWriter.ValidateRunGate(failedLaunch, 16, QualificationChecks.DefaultEngines);
        AssertCase("unlaunched-engine-fails-closed", !failedLaunchPassed && failedLaunchError == "engine-launch-failed");

        // 5. Digest mismatch on support receipt must fail closed.
        AssertCase("receipt-payload-digest-mismatch-fails-closed",
            !QualificationReceiptWriter.ValidateReceiptContract(validReceipt, SyntheticDigestA, SyntheticDigestA, SyntheticDigestB));

        // 6. Malformed receipt JSON and false AT claim must fail closed.
        AssertCase("malformed-receipt-fails-closed",
            !QualificationReceiptWriter.ValidateReceiptContract("{not-json", SyntheticDigestA, SyntheticDigestA, SyntheticDigestA));
        string falseAtReceipt = validReceipt.Replace(
            """
                "capability": "accessibility",
                "locale": "en",
                "scenario": "assistive-technology",
            """,
            """
                "capability": "accessibility",
                "locale": "en",
                "scenario": "assistive-technology",
            """,
            StringComparison.Ordinal);
        JsonObject atMutated = JsonNode.Parse(falseAtReceipt)!.AsObject();
        foreach (JsonNode? node in atMutated["records"]!.AsArray())
        {
            if (node?["scenario"]?.GetValue<string>() == "assistive-technology")
            {
                node["status"] = "passed";
                node["exitCode"] = 0;
                node["passedCount"] = 1;
                break;
            }
        }
        AssertCase("automated-assistive-tech-claim-fails-closed",
            !QualificationReceiptWriter.ValidateReceiptContract(atMutated.ToJsonString(), SyntheticDigestA, SyntheticDigestA, SyntheticDigestA));

        // 7. Synthetic 36-cohort x 46-sample interaction payload passes; < 36 cohorts, < 46 samples, untrusted, or unordered clocks fail closed.
        JsonObject validInteraction = BuildSyntheticInteractionPayload();
        AssertCase("valid-36-cohort-46-sample-interaction-passes",
            CompoundRootBenchmarks.ValidateInteractionPayload(validInteraction.ToJsonString()));

        JsonObject tooFewCohorts = validInteraction.DeepClone().AsObject();
        tooFewCohorts["cohorts"]!.AsArray().RemoveAt(0);
        AssertCase("under-36-cohorts-fails-closed",
            !CompoundRootBenchmarks.ValidateInteractionPayload(tooFewCohorts.ToJsonString()));

        JsonObject tooFewSamples = validInteraction.DeepClone().AsObject();
        tooFewSamples["cohorts"]![0]!["samples"]!.AsArray().RemoveAt(45);
        AssertCase("under-46-samples-fails-closed",
            !CompoundRootBenchmarks.ValidateInteractionPayload(tooFewSamples.ToJsonString()));

        JsonObject untrustedSample = validInteraction.DeepClone().AsObject();
        untrustedSample["cohorts"]![0]!["samples"]![6]!["trusted"] = false;
        AssertCase("untrusted-sample-fails-closed",
            !CompoundRootBenchmarks.ValidateInteractionPayload(untrustedSample.ToJsonString()));

        JsonObject unorderedClocks = validInteraction.DeepClone().AsObject();
        unorderedClocks["cohorts"]![0]!["samples"]![6]!["feedbackMs"] = 5000;
        AssertCase("unordered-clocks-fails-closed",
            !CompoundRootBenchmarks.ValidateInteractionPayload(unorderedClocks.ToJsonString()));

        Console.WriteLine($"BROWSER_QUALIFICATION_SELF_TEST failures={failures}");
        return failures == 0 ? 0 : 1;
    }

    private static QualificationReport BuildSyntheticReport(
        IReadOnlyList<string> engines,
        IReadOnlyList<string> checkIds,
        bool allPassed)
    {
        var summaries = new List<EngineRunSummary>(engines.Count);
        foreach (string engine in engines)
        {
            var checks = checkIds
                .Select(id => new CheckOutcome(id, engine, "en", allPassed, 12.5, allPassed ? null : "synthetic-failure"))
                .ToArray();
            summaries.Add(new EngineRunSummary(engine, true, "0.0.1-synthetic", null, checks));
        }

        return new QualificationReport(
            QualificationReceiptWriter.ReportFormat,
            "http://127.0.0.1:8080",
            "2026-11-23T00:00:00Z",
            16,
            engines,
            allPassed,
            summaries);
    }

    private static JsonObject BuildSyntheticInteractionPayload()
    {
        var cohorts = new JsonArray();
        foreach (string journey in CompoundRootBenchmarks.CoreJourneys)
            foreach (int rate in new[] { 1, 4 })
                foreach (string direction in new[] { "forward", "reverse" })
                {
                    var samples = new JsonArray();
                    for (int index = 0; index < CompoundRootBenchmarks.RequiredSamplesPerCohort; index++)
                    {
                        double baseMs = index * 1000.0;
                        samples.Add(new JsonObject
                        {
                            ["phase"] = index == 0 ? "first" : index <= 5 ? "warmup" : "measured",
                            ["index"] = index == 0 ? 1 : index <= 5 ? index : index - 5,
                            ["trusted"] = true,
                            ["status"] = "complete",
                            ["startMs"] = baseMs,
                            ["feedbackMs"] = baseMs + 10,
                            ["contentMs"] = baseMs + 20,
                            ["completionMs"] = baseMs + 30,
                            ["settledMs"] = baseMs + 40,
                            ["maximumTaskMs"] = 12
                        });
                    }

                    cohorts.Add(new JsonObject
                    {
                        ["journey"] = journey,
                        ["direction"] = direction,
                        ["rate"] = rate,
                        ["status"] = "complete",
                        ["warmups"] = 5,
                        ["contentBudgetMs"] = 100 * rate,
                        ["samples"] = samples
                    });
                }

        return new JsonObject
        {
            ["format"] = CompoundRootBenchmarks.InteractionFormat,
            ["synthetic"] = true,
            ["fixture"] = SyntheticDigestA,
            ["payload"] = SyntheticDigestA,
            ["host"] = SyntheticDigestA,
            ["status"] = "complete",
            ["cohorts"] = cohorts
        };
    }

    private static bool TryParseArguments(string[] args, out CliOptions? options, out string? error)
    {
        options = null;
        error = null;

        bool measureInteractive = false;
        Uri? baseUrl = null;
        string[] engines = [.. QualificationChecks.DefaultEngines];
        int minimumChecks = QualificationChecks.RequiredCheckIds.Count;
        string? outputPath = null;
        string? receiptOutputPath = null;
        string? sourceDigest = null;
        string? buildDigest = null;
        string? payloadDigest = null;
        string? fixtureDigest = null;
        string? hostDigest = null;
        string? commit = null;
        string? dirtyDigest = null;
        string testRunnerRid = "linux-x64";
        string? fixturesDirectory = null;
        string? interactionOutputPath = null;
        string? qf01OutputPath = null;
        bool includeExtended = true;

        for (int index = 0; index < args.Length; index++)
        {
            string arg = args[index];
            string? NextValue() => index + 1 < args.Length ? args[++index] : null;

            switch (arg)
            {
                case "--measure-interactive":
                    measureInteractive = true;
                    break;
                case "--base-url":
                    if (NextValue() is not { } rawUrl || !Uri.TryCreate(rawUrl, UriKind.Absolute, out baseUrl)
                        || baseUrl.Scheme is not ("http" or "https"))
                    {
                        error = "--base-url requires an absolute http:// or https:// URL.";
                        return false;
                    }
                    break;
                case "--engines":
                    if (NextValue() is not { } rawEngines || string.IsNullOrWhiteSpace(rawEngines))
                    {
                        error = "--engines requires a comma-separated list of engines.";
                        return false;
                    }
                    engines = rawEngines
                        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Select(engine => engine.ToLowerInvariant())
                        .Distinct(StringComparer.Ordinal)
                        .ToArray();
                    break;
                case "--minimum-expected-checks":
                    if (NextValue() is not { } rawMin
                        || !int.TryParse(rawMin, NumberStyles.Integer, CultureInfo.InvariantCulture, out minimumChecks)
                        || minimumChecks < 1)
                    {
                        error = "--minimum-expected-checks requires a positive integer.";
                        return false;
                    }
                    break;
                case "--output":
                    outputPath = NextValue();
                    break;
                case "--receipt-output":
                    receiptOutputPath = NextValue();
                    break;
                case "--source-digest":
                    sourceDigest = NextValue();
                    break;
                case "--build-digest":
                    buildDigest = NextValue();
                    break;
                case "--payload-digest":
                    payloadDigest = NextValue();
                    break;
                case "--fixture-digest":
                    fixtureDigest = NextValue();
                    break;
                case "--host-digest":
                    hostDigest = NextValue();
                    break;
                case "--commit":
                    commit = NextValue();
                    break;
                case "--dirty-digest":
                    string? rawDirty = NextValue();
                    dirtyDigest = string.Equals(rawDirty, "null", StringComparison.OrdinalIgnoreCase) ? null : rawDirty;
                    break;
                case "--test-runner-rid":
                    testRunnerRid = NextValue() ?? string.Empty;
                    break;
                case "--fixtures-dir":
                    fixturesDirectory = NextValue();
                    break;
                case "--interaction-output":
                    interactionOutputPath = NextValue();
                    break;
                case "--qf01-output":
                    qf01OutputPath = NextValue();
                    break;
                case "--include-extended":
                    if (NextValue() is not { } rawExt || !bool.TryParse(rawExt, out includeExtended))
                    {
                        error = "--include-extended requires true or false.";
                        return false;
                    }
                    break;
                default:
                    error = "Unknown argument: " + arg;
                    return false;
            }
        }

        if (baseUrl is null)
        {
            error = "--base-url is required.";
            return false;
        }

        if (measureInteractive)
        {
            if (string.IsNullOrWhiteSpace(fixtureDigest)
                || string.IsNullOrWhiteSpace(payloadDigest)
                || string.IsNullOrWhiteSpace(hostDigest))
            {
                error = "--measure-interactive requires --fixture-digest, --payload-digest, and --host-digest.";
                return false;
            }
        }
        else if (!string.IsNullOrWhiteSpace(receiptOutputPath))
        {
            if (string.IsNullOrWhiteSpace(sourceDigest)
                || string.IsNullOrWhiteSpace(buildDigest)
                || string.IsNullOrWhiteSpace(payloadDigest)
                || string.IsNullOrWhiteSpace(commit)
                || string.IsNullOrWhiteSpace(hostDigest))
            {
                error = "--receipt-output requires --source-digest, --build-digest, --payload-digest, --commit, and --host-digest.";
                return false;
            }
        }

        options = new CliOptions(
            measureInteractive,
            baseUrl,
            engines,
            minimumChecks,
            outputPath,
            receiptOutputPath,
            sourceDigest,
            buildDigest,
            payloadDigest,
            fixtureDigest,
            hostDigest,
            commit,
            dirtyDigest,
            testRunnerRid,
            fixturesDirectory,
            interactionOutputPath,
            qf01OutputPath,
            includeExtended);
        return true;
    }

    private static void EnsureParentDirectory(string path)
    {
        string? directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
    }

    private static void PrintUsage()
    {
        Console.Error.WriteLine(
            "Usage: Configurator.BrowserQualification --self-test"
            + " | --base-url URL [--engines chromium,firefox,webkit] [--minimum-expected-checks 16] [--output FILE] [--receipt-output FILE --source-digest SHA256 --build-digest SHA256 --payload-digest SHA256 --commit SHA1 --host-digest SHA256]"
            + " | --measure-interactive --base-url URL --fixture-digest SHA256 --payload-digest SHA256 --host-digest SHA256 [--fixtures-dir DIR] [--interaction-output FILE] [--qf01-output FILE]");
    }

    private sealed record CliOptions(
        bool MeasureInteractive,
        Uri? BaseUrl,
        IReadOnlyList<string> Engines,
        int MinimumExpectedChecks,
        string? OutputPath,
        string? ReceiptOutputPath,
        string? SourceDigest,
        string? BuildDigest,
        string? PayloadDigest,
        string? FixtureDigest,
        string? HostDigest,
        string? Commit,
        string? DirtyDigest,
        string TestRunnerRid,
        string? FixturesDirectory,
        string? InteractionOutputPath,
        string? Qf01OutputPath,
        bool IncludeExtended);
}
