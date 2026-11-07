#:property RestorePackagesWithLockFile=false
#:property NuGetLockFilePath=$(MSBuildProjectExtensionsPath)MeasureInteractiveWork.packages.lock.json
#:property AssemblyName=Configurator.InteractiveMeasurement
#:property PublishAot=false

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

return Measurement.Run(args);

/// <summary>
/// Validates explicitly supplied synthetic records, never drives a browser.
/// Identity operands are independently recorded SHA-256 digests; matching them
/// does not authenticate the producer or prove that trusted events really occurred.
/// </summary>
internal static class Measurement
{
    internal const int MaximumBytes = 16 * 1024 * 1024;
    private const string Digest = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private static readonly string[] Journeys =
    [
        "fresh-environment", "switch-manifest", "ten-scalars", "ten-short-choices",
        "obscure-correction", "import-compare", "deferred-draft",
        "bulk-edit-malformed-recovery", "consent-revoke-editor"
    ];

    internal static int Run(string[] args)
    {
        if (args is ["--self-test"]) return SelfTest();
        if (args is not ["--input", var path, "--fixture", var fixture,
            "--payload", var payload, "--host", var host]
            || path.StartsWith("--", StringComparison.Ordinal)
            || !IsDigest(fixture) || !IsDigest(payload) || !IsDigest(host))
        {
            Console.Error.WriteLine("Usage: dotnet run --file eng/configurator/MeasureInteractiveWork.cs -- --self-test | --input FILE --fixture SHA256 --payload SHA256 --host SHA256");
            return 64;
        }
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            // Read one byte beyond the bound to detect growth without unbounded allocation.
            using var buffer = new MemoryStream();
            byte[] chunk = new byte[8192];
            int count;
            while ((count = stream.Read(chunk, 0, Math.Min(chunk.Length, MaximumBytes + 1 - (int)buffer.Length))) != 0)
            {
                buffer.Write(chunk, 0, count);
                if (buffer.Length > MaximumBytes) throw new InvalidDataException();
            }
            return Validate(buffer.ToArray(), fixture, payload, host, Console.Out);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException
            or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            Console.Error.WriteLine("MEASUREMENT_INPUT_REJECTED");
            return 1;
        }
    }

    private static bool IsDigest(string value) =>
        value.Length == 64 && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static int Validate(byte[] bytes, string fixture, string payload, string host, TextWriter output)
    {
        try
        {
            Require(bytes.Length is > 0 and <= MaximumBytes);
            using JsonDocument document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 8 });
            JsonElement root = document.RootElement;
            Fields(root, "format", "synthetic", "fixture", "payload", "host", "status", "cohorts");
            Require(root.GetProperty("format").GetString() == "synthetic-interaction-0.0.1"
                && root.GetProperty("synthetic").GetBoolean()
                && root.GetProperty("status").GetString() == "complete"
                && root.GetProperty("fixture").GetString() == fixture
                && root.GetProperty("payload").GetString() == payload
                && root.GetProperty("host").GetString() == host);
            JsonElement cohorts = root.GetProperty("cohorts");
            Require(cohorts.ValueKind == JsonValueKind.Array && cohorts.GetArrayLength() is >= 36 and <= 128);
            var keys = new HashSet<(string Journey, string Direction, int Rate)>();
            var reports = new List<object>();
            bool passed = true;
            foreach (JsonElement cohort in cohorts.EnumerateArray())
            {
                Fields(cohort, "journey", "direction", "rate", "status", "warmups", "contentBudgetMs", "samples");
                string journey = cohort.GetProperty("journey").GetString()!;
                string direction = cohort.GetProperty("direction").GetString()!;
                int rate = cohort.GetProperty("rate").GetInt32();
                Require((Journeys.Contains(journey) || journey is "few-root-deep-tree" or "wide-nested-object"
                    or "large-map-list" or "multi-artifact" or "many-active-corrections")
                    && direction is "forward" or "reverse" && rate is 1 or 4
                    && keys.Add((journey, direction, rate))
                    && cohort.GetProperty("status").GetString() == "complete"
                    && cohort.GetProperty("warmups").GetInt32() == 5);
                double contentBudget = Number(cohort, "contentBudgetMs");
                // Conservative inherited control/transition ceiling. A producer must
                // declare any tighter journey limit; the tool cannot infer scenario semantics.
                Require(contentBudget > 0 && contentBudget <= 100 * rate);
                JsonElement samples = cohort.GetProperty("samples");
                Require(samples.ValueKind == JsonValueKind.Array && samples.GetArrayLength() == 46);
                var content = new List<double>();
                var feedback = new List<double>();
                var completion = new List<double>();
                var settlement = new List<double>();
                var tasks = new List<double>();
                var outliers = new List<int>();
                double previousSettlement = 0;
                bool cohortPassed = true;
                int ordinal = 0;
                foreach (JsonElement sample in samples.EnumerateArray())
                {
                    Fields(sample, "phase", "index", "trusted", "status", "startMs", "feedbackMs",
                        "contentMs", "completionMs", "settledMs", "maximumTaskMs");
                    Require(sample.GetProperty("phase").GetString() ==
                        (ordinal == 0 ? "first" : ordinal <= 5 ? "warmup" : "measured")
                        && sample.GetProperty("index").GetInt32() ==
                        (ordinal == 0 ? 1 : ordinal <= 5 ? ordinal : ordinal - 5)
                        && sample.GetProperty("trusted").GetBoolean()
                        && sample.GetProperty("status").GetString() == "complete");
                    double start = Number(sample, "startMs");
                    double acknowledgement = Number(sample, "feedbackMs");
                    double finalContent = Number(sample, "contentMs");
                    double completed = Number(sample, "completionMs");
                    double settled = Number(sample, "settledMs");
                    double task = Number(sample, "maximumTaskMs");
                    Require(previousSettlement <= start && start <= acknowledgement
                        && acknowledgement <= finalContent && finalContent <= completed && completed <= settled);
                    previousSettlement = settled;
                    bool feedbackOutlier = acknowledgement - start >= 50 * rate;
                    bool taskOutlier = ordinal > 0 && rate == 1 && task > 50;
                    if (feedbackOutlier || taskOutlier || finalContent - start >= contentBudget)
                        outliers.Add(ordinal);
                    // First-use and warmup feedback is retained and gated; only warmed
                    // task duration and measured content percentiles have those specific gates.
                    cohortPassed &= !feedbackOutlier && !taskOutlier;
                    if (ordinal >= 6)
                    {
                        content.Add(finalContent - start);
                        feedback.Add(acknowledgement - start);
                        completion.Add(completed - start);
                        settlement.Add(settled - start);
                        tasks.Add(task);
                    }
                    ordinal++;
                }
                cohortPassed &= Percentile(content, .95) < contentBudget;
                passed &= cohortPassed;
                reports.Add(new
                {
                    journey, direction, rate, contentBudgetMs = contentBudget, passed = cohortPassed,
                    content = Statistics(content), feedback = Statistics(feedback),
                    completion = Statistics(completion), settlement = Statistics(settlement),
                    maximumTask = Statistics(tasks), outlierOrdinals = outliers,
                    // All first-use, warmup and measured records survive, even when a
                    // percentile passes with a slow tail. No winsorizing or trimming.
                    samples = samples.Clone()
                });
            }
            foreach (string journey in keys.Select(key => key.Journey).Concat(Journeys).Distinct())
            foreach (int rate in new[] { 1, 4 })
            foreach (string direction in new[] { "forward", "reverse" })
                Require(keys.Contains((journey, direction, rate)));
            output.WriteLine(JsonSerializer.Serialize(new
            {
                format = "synthetic-interaction-summary-0.0.1", synthetic = true,
                fixture, payload, host, passed, cohorts = reports,
                limitation = "Validates supplied synthetic records only; no producer authentication, browser execution, omitted-event detection or real cohort qualification."
            }));
            return passed ? 0 : 2;
        }
        catch (Exception exception) when (exception is JsonException or InvalidDataException
            or InvalidOperationException or FormatException or OverflowException or KeyNotFoundException)
        {
            output.WriteLine("MEASUREMENT_RECORDS_REJECTED");
            return 1;
        }
    }

    private static void Require(bool condition)
    {
        if (!condition) throw new InvalidDataException();
    }

    private static void Fields(JsonElement element, params string[] expected)
    {
        Require(element.ValueKind == JsonValueKind.Object);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonProperty property in element.EnumerateObject())
            Require(expected.Contains(property.Name, StringComparer.Ordinal) && seen.Add(property.Name));
        Require(seen.Count == expected.Length);
    }

    private static double Number(JsonElement element, string name)
    {
        double value = element.GetProperty(name).GetDouble();
        Require(double.IsFinite(value) && value >= 0 && value <= 1_000_000_000);
        return value;
    }

    private static double Percentile(List<double> values, double rank) =>
        values.Order().ElementAt((int)Math.Ceiling(values.Count * rank) - 1);

    private static object Statistics(List<double> values) => new
    {
        count = values.Count, p50Ms = Percentile(values, .5),
        p95Ms = Percentile(values, .95), maximumMs = values.Max()
    };

    private static JsonObject Example()
    {
        var cohorts = new JsonArray();
        foreach (string journey in Journeys)
        foreach (int rate in new[] { 1, 4 })
        foreach (string direction in new[] { "forward", "reverse" })
        {
            var samples = new JsonArray();
            for (int i = 0; i < 46; i++)
                samples.Add(new JsonObject
                {
                    ["phase"] = i == 0 ? "first" : i <= 5 ? "warmup" : "measured",
                    ["index"] = i == 0 ? 1 : i <= 5 ? i : i - 5,
                    ["trusted"] = true, ["status"] = "complete",
                    ["startMs"] = i * 1000, ["feedbackMs"] = i * 1000 + 10,
                    ["contentMs"] = i * 1000 + 20, ["completionMs"] = i * 1000 + 500,
                    ["settledMs"] = i * 1000 + 600, ["maximumTaskMs"] = 20
                });
            cohorts.Add(new JsonObject
            {
                ["journey"] = journey, ["direction"] = direction, ["rate"] = rate,
                ["status"] = "complete", ["warmups"] = 5, ["contentBudgetMs"] = 100 * rate,
                ["samples"] = samples
            });
        }
        return new JsonObject
        {
            ["format"] = "synthetic-interaction-0.0.1", ["synthetic"] = true,
            ["fixture"] = Digest, ["payload"] = Digest, ["host"] = Digest,
            ["status"] = "complete", ["cohorts"] = cohorts
        };
    }

    private static int SelfTest()
    {
        int failures = 0;
        void Check(string name, Action<JsonObject> mutate, int expected)
        {
            JsonObject root = Example();
            mutate(root);
            using var output = new StringWriter(CultureInfo.InvariantCulture);
            int actual = Validate(JsonSerializer.SerializeToUtf8Bytes(root), Digest, Digest, Digest, output);
            bool passed = actual == expected;
            Console.WriteLine($"SELF_TEST case={name} passed={passed}");
            if (!passed) failures++;
        }
        static JsonObject Cohort(JsonObject root) => root["cohorts"]![0]!.AsObject();
        static JsonObject Sample(JsonObject root, int index = 6) => Cohort(root)["samples"]![index]!.AsObject();
        Check("complete-nine-journeys", _ => { }, 0);
        Check("failed-run", r => r["status"] = "failed", 1);
        Check("incomplete-cohort", r => Cohort(r)["status"] = "incomplete", 1);
        Check("failed-sample", r => Sample(r)["status"] = "failed", 1);
        Check("untrusted", r => Sample(r)["trusted"] = false, 1);
        Check("nonfinite", r => Sample(r)["contentMs"] = "NaN", 1);
        Check("negative", r => Sample(r)["startMs"] = -1, 1);
        Check("unordered-clocks", r => Sample(r)["feedbackMs"] = 7000, 1);
        Check("reordered-records", r => Sample(r)["index"] = 2, 1);
        Check("missing-first", r => Cohort(r)["samples"]!.AsArray().RemoveAt(0), 1);
        Check("missing-warmup", r => Cohort(r)["samples"]!.AsArray().RemoveAt(1), 1);
        Check("discarded-outlier", r => Cohort(r)["samples"]!.AsArray().RemoveAt(45), 1);
        Check("extra-sample", r => Cohort(r)["samples"]!.AsArray().Add(Sample(r).DeepClone()), 1);
        Check("missing-journey", r => r["cohorts"]!.AsArray().RemoveAt(0), 1);
        Check("duplicate-cohort", r => r["cohorts"]![1] = Cohort(r).DeepClone(), 1);
        Check("fixture-mismatch", r => r["fixture"] = new string('b', 64), 1);
        Check("payload-mismatch", r => r["payload"] = new string('b', 64), 1);
        Check("host-mismatch", r => r["host"] = new string('b', 64), 1);
        Check("invalid-rate", r => Cohort(r)["rate"] = 2, 1);
        Check("undeclared-warmups", r => Cohort(r)["warmups"] = 4, 1);
        Check("feedback-boundary", r =>
        {
            Sample(r)["feedbackMs"] = 6050;
            Sample(r)["contentMs"] = 6060;
        }, 2);
        Check("warmup-long-task", r => Sample(r, 1)["maximumTaskMs"] = 51, 2);
        Check("measured-long-task", r => Sample(r)["maximumTaskMs"] = 51, 2);
        Check("stricter-content", r => Cohort(r)["contentBudgetMs"] = 19, 2);
        Check("single-outlier-retained", r => Sample(r)["maximumTaskMs"] = 500, 2);
        Check("completion-distinct-from-feedback", r => Sample(r)["completionMs"] = 6500, 0);
        Check("unknown-private-field", r => Sample(r)["answer"] = "synthetic-only", 1);
        Check("overlapping-samples", r => Sample(r)["startMs"] = 5500, 1);
        Check("relaxed-content-budget", r => Cohort(r)["contentBudgetMs"] = 201, 1);
        Check("content-p95-boundary", r =>
        {
            for (int i = 6; i < 46; i++) Sample(r, i)["contentMs"] = i * 1000 + 100;
        }, 2);
        Check("four-times-feedback-boundary", r =>
        {
            JsonObject sample = r["cohorts"]![2]!["samples"]![6]!.AsObject();
            sample["feedbackMs"] = 6200;
            sample["contentMs"] = 6210;
        }, 2);
        void Raw(string name, byte[] bytes)
        {
            using var output = new StringWriter(CultureInfo.InvariantCulture);
            bool passed = Validate(bytes, Digest, Digest, Digest, output) == 1;
            Console.WriteLine($"SELF_TEST case={name} passed={passed}");
            if (!passed) failures++;
        }
        byte[] Encode(string value) => System.Text.Encoding.UTF8.GetBytes(value);
        Raw("malformed-json", Encode("{"));
        Raw("duplicate-properties", Encode(Example().ToJsonString().Replace(
            "\"synthetic\":true", "\"synthetic\":true,\"synthetic\":true", StringComparison.Ordinal)));
        Raw("nonfinite-number", Encode(Example().ToJsonString().Replace(
            "\"maximumTaskMs\":20", "\"maximumTaskMs\":1e999", StringComparison.Ordinal)));
        Raw("bounded-input", new byte[MaximumBytes + 1]);
        // Independent nearest-rank oracle: measured content is 21..60 ms.
        // The single 60 ms tail exceeds the declared 59 ms limit, but p95=58
        // passes; that outlier and its exact raw sample must still survive.
        JsonObject statisticsRoot = Example();
        Cohort(statisticsRoot)["contentBudgetMs"] = 59;
        for (int i = 6; i < 46; i++) Sample(statisticsRoot, i)["contentMs"] = i * 1000 + i + 15;
        using (var output = new StringWriter(CultureInfo.InvariantCulture))
        {
            int result = Validate(JsonSerializer.SerializeToUtf8Bytes(statisticsRoot), Digest, Digest, Digest, output);
            using JsonDocument report = JsonDocument.Parse(output.ToString());
            JsonElement first = report.RootElement.GetProperty("cohorts")[0];
            JsonElement stats = first.GetProperty("content");
            bool passed = result == 0 && stats.GetProperty("count").GetInt32() == 40
                && stats.GetProperty("p50Ms").GetDouble() == 40
                && stats.GetProperty("p95Ms").GetDouble() == 58
                && stats.GetProperty("maximumMs").GetDouble() == 60
                && first.GetProperty("samples").GetArrayLength() == 46
                && first.GetProperty("outlierOrdinals").EnumerateArray().Select(x => x.GetInt32()).SequenceEqual([44, 45])
                && first.GetProperty("completion").GetProperty("maximumMs").GetDouble() == 500;
            Console.WriteLine($"SELF_TEST case=statistics-and-all-outliers-retained passed={passed}");
            if (!passed) failures++;
        }
        Console.WriteLine($"SELF_TEST_COMPLETE failures={failures}");
        return failures == 0 ? 0 : 1;
    }
}
