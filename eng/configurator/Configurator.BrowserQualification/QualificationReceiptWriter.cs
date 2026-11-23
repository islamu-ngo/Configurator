using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Configurator.BrowserQualification;

/// <summary>
/// Value-free outcome of a single qualification check executed in a real browser engine.
/// </summary>
internal sealed record CheckOutcome(
    string CheckId,
    string Engine,
    string Locale,
    bool Passed,
    double DurationMs,
    string? DiagnosticCode);

/// <summary>
/// Per-engine launch and check execution summary.
/// </summary>
internal sealed record EngineRunSummary(
    string Engine,
    bool Launched,
    string? EngineVersion,
    string? LaunchFailureCode,
    IReadOnlyList<CheckOutcome> Checks);

/// <summary>
/// Complete multi-engine browser qualification report.
/// </summary>
internal sealed record QualificationReport(
    string Format,
    string BaseUrl,
    string RecordedAt,
    int MinimumExpectedChecks,
    IReadOnlyList<string> RequestedEngines,
    bool Passed,
    IReadOnlyList<EngineRunSummary> Engines);

/// <summary>
/// Independently supplied digests bound into emitted support qualification receipts.
/// </summary>
internal sealed record ReceiptIdentity(
    string ProductSourceInputsDigest,
    string BuildRecipeDigest,
    string PayloadDigest,
    string Commit,
    string? DirtyInputsDigest,
    string HostDigest,
    string TestRunnerRid);

/// <summary>
/// Emits and validates detached receipts matching <c>configurator-support-qualification-0.0.1</c>
/// consumed by <c>eng/configurator/ValidateSupportQualification.cs</c>.
/// </summary>
internal static class QualificationReceiptWriter
{
    internal const string ReceiptFormat = "configurator-support-qualification-0.0.1";
    internal const string ReportFormat = "configurator-browser-qualification-report-0.0.1";
    private const string ToolVersion = "0.0.1";

    private static readonly JsonSerializerOptions IndentedJson = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly string[] ValidStatuses =
    [
        "passed", "failed", "verified-unsupported", "unexecuted"
    ];

    private static readonly (string Capability, string Scenario, string[] CheckIds, bool CreatesArtifact, bool ExecutedInArabic)[] BrowserScenarios =
    [
        ("prepare", "prepare-output", ["Q05-complete-and-draft-outputs"], false, false),
        ("edit", "root-map", ["Q03-typed-presence-omission"], false, false),
        ("edit", "root-variant", ["Q03-typed-presence-omission"], false, false),
        ("edit", "correction", ["Q08-invalid-edit-fencing"], false, false),
        ("import", "compare-adopt", ["Q06-nested-import-conflicts"], false, false),
        ("accessibility", "keyboard-focus", ["Q13-keyboard-focus-rtl-and-mobile-reflow", "Q14-wave2-contrast-and-focus-rings"], false, true),
        ("accessibility", "assistive-technology", [], false, false),
        ("custody", "consent-revoke", ["Q04-secret-consent-and-hosted-custody", "Q11-custody-sink-canary-checks"], false, true),
        ("export", "deliberate-export", ["Q07-deliberate-reveal-copy-download"], true, false),
        ("offline", "cache-update", ["Q10-offline-retry-delete-and-capacity", "Q12-two-tab-update-safety"], false, false),
        ("performance", "interactive-response", ["Q15-scale-definition-admission-and-discovery"], false, false)
    ];

    /// <summary>
    /// Enforces fail-closed gate rules across requested engines and required check counts.
    /// </summary>
    internal static (bool Passed, string? FailureCode) ValidateRunGate(
        QualificationReport report,
        int minimumExpectedChecks,
        IReadOnlyList<string> expectedEngines)
    {
        if (minimumExpectedChecks < QualificationChecks.RequiredCheckIds.Count)
            return (false, "minimum-checks-below-contract");
        if (expectedEngines.Count == 0)
            return (false, "engines-empty");

        var byEngine = report.Engines.ToDictionary(engine => engine.Engine, StringComparer.Ordinal);
        foreach (string expected in expectedEngines)
        {
            if (!byEngine.TryGetValue(expected, out EngineRunSummary? summary))
                return (false, "engine-missing");
            if (!summary.Launched)
                return (false, "engine-launch-failed");

            var distinctChecks = summary.Checks
                .Select(check => check.CheckId)
                .Distinct(StringComparer.Ordinal)
                .ToHashSet(StringComparer.Ordinal);

            if (distinctChecks.Count < minimumExpectedChecks)
                return (false, "insufficient-checks");

            foreach (string requiredId in QualificationChecks.RequiredCheckIds)
                if (!distinctChecks.Contains(requiredId))
                    return (false, "missing-required-check");

            if (summary.Checks.Any(check => !check.Passed))
                return (false, "check-failed");
        }

        return (true, null);
    }

    /// <summary>
    /// Serializes the multi-engine detailed report without any answer or secret data.
    /// </summary>
    internal static string SerializeReport(QualificationReport report) =>
        JsonSerializer.Serialize(report, IndentedJson) + "\n";

    /// <summary>
    /// Builds a strict <c>configurator-support-qualification-0.0.1</c> receipt bound to exact digests.
    /// Automated runs keep human assistive-technology rows explicitly <c>unexecuted</c>.
    /// </summary>
    internal static string BuildSupportReceiptJson(
        QualificationReport report,
        ReceiptIdentity identity,
        bool? interactivePerformancePassed = null)
    {
        if (!IsHex(identity.ProductSourceInputsDigest, 64)
            || !IsHex(identity.BuildRecipeDigest, 64)
            || !IsHex(identity.PayloadDigest, 64)
            || !IsHex(identity.Commit, 40)
            || (identity.DirtyInputsDigest is not null && !IsHex(identity.DirtyInputsDigest, 64))
            || !IsHex(identity.HostDigest, 64))
        {
            throw new ArgumentException("Receipt identity digests must be lowercase hexadecimal of exact contract length.");
        }

        string hostOs = identity.TestRunnerRid.Split('-', 2)[0];
        string recordedAt = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        bool anyEngineLaunched = report.Engines.Any(engine => engine.Launched);
        List<CheckOutcome> allChecks = report.Engines.SelectMany(engine => engine.Checks).ToList();

        var records = new JsonArray();
        foreach ((string capability, string scenario, string[] checkIds, bool createsArtifact, bool executedInArabic) in BrowserScenarios)
        {
            foreach (string locale in new[] { "en", "ar" })
            {
                bool shouldMarkUnexecuted = checkIds.Length == 0
                    || !anyEngineLaunched
                    || (locale == "ar" && !executedInArabic);

                if (capability == "performance" && interactivePerformancePassed.HasValue && locale == "en")
                {
                    bool perfPass = interactivePerformancePassed.Value;
                    records.Add(BuildRecord(
                        identity.TestRunnerRid,
                        hostOs,
                        identity.HostDigest,
                        capability,
                        locale,
                        scenario,
                        recordedAt,
                        perfPass ? "passed" : "failed",
                        perfPass ? 0 : 1,
                        perfPass ? 1 : 0,
                        perfPass ? 0 : 1,
                        false));
                    continue;
                }

                if (shouldMarkUnexecuted)
                {
                    records.Add(BuildRecord(
                        null,
                        null,
                        null,
                        capability,
                        locale,
                        scenario,
                        recordedAt,
                        "unexecuted",
                        null,
                        0,
                        0,
                        false));
                    continue;
                }

                List<CheckOutcome> matching = allChecks
                    .Where(check => checkIds.Contains(check.CheckId, StringComparer.Ordinal))
                    .ToList();

                if (matching.Count == 0)
                {
                    records.Add(BuildRecord(
                        null,
                        null,
                        null,
                        capability,
                        locale,
                        scenario,
                        recordedAt,
                        "unexecuted",
                        null,
                        0,
                        0,
                        false));
                    continue;
                }

                int passedCount = matching.Count(check => check.Passed);
                int failedCount = matching.Count - passedCount;
                bool scenarioPassed = failedCount == 0 && passedCount > 0 && report.Engines.All(engine => engine.Launched);
                int effectiveFailed = scenarioPassed ? 0 : Math.Max(1, failedCount);
                records.Add(BuildRecord(
                    identity.TestRunnerRid,
                    hostOs,
                    identity.HostDigest,
                    capability,
                    locale,
                    scenario,
                    recordedAt,
                    scenarioPassed ? "passed" : "failed",
                    scenarioPassed ? 0 : 1,
                    passedCount,
                    effectiveFailed,
                    scenarioPassed && createsArtifact));
            }
        }

        var root = new JsonObject
        {
            ["format"] = ReceiptFormat,
            ["productSourceInputsDigest"] = identity.ProductSourceInputsDigest,
            ["buildRecipeDigest"] = identity.BuildRecipeDigest,
            ["payloadDigest"] = identity.PayloadDigest,
            ["commit"] = identity.Commit,
            ["dirtyInputsDigest"] = identity.DirtyInputsDigest is null ? null : JsonValue.Create(identity.DirtyInputsDigest),
            ["toolVersion"] = ToolVersion,
            ["records"] = records
        };

        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n";
    }

    /// <summary>
    /// Fail-closed structural and identity validation of a generated support receipt.
    /// </summary>
    internal static bool ValidateReceiptContract(
        string json,
        string expectedSource,
        string expectedBuild,
        string expectedPayload)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 8 });
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return false;
            if (root.GetProperty("format").GetString() != ReceiptFormat) return false;
            if (root.GetProperty("productSourceInputsDigest").GetString() != expectedSource) return false;
            if (root.GetProperty("buildRecipeDigest").GetString() != expectedBuild) return false;
            if (root.GetProperty("payloadDigest").GetString() != expectedPayload) return false;
            if (!IsHex(root.GetProperty("commit").GetString() ?? string.Empty, 40)) return false;
            if (root.GetProperty("toolVersion").GetString() != ToolVersion) return false;

            JsonElement records = root.GetProperty("records");
            if (records.ValueKind != JsonValueKind.Array || records.GetArrayLength() == 0) return false;

            var seenKeys = new HashSet<(string, string, string, string, string)>();
            foreach (JsonElement record in records.EnumerateArray())
            {
                if (record.ValueKind != JsonValueKind.Object) return false;
                string client = record.GetProperty("client").GetString() ?? string.Empty;
                string rid = record.GetProperty("packageRid").GetString() ?? string.Empty;
                string capability = record.GetProperty("capability").GetString() ?? string.Empty;
                string locale = record.GetProperty("locale").GetString() ?? string.Empty;
                string scenario = record.GetProperty("scenario").GetString() ?? string.Empty;
                string status = record.GetProperty("status").GetString() ?? string.Empty;

                if (client != "browser" || rid != "browser-wasm") return false;
                if (locale is not ("en" or "ar")) return false;
                if (!ValidStatuses.Contains(status, StringComparer.Ordinal)) return false;
                if (!seenKeys.Add((client, rid, capability, locale, scenario))) return false;

                if (capability == "accessibility" && scenario == "assistive-technology" && status != "unexecuted")
                    return false;

                if (status == "unexecuted")
                {
                    if (record.GetProperty("exitCode").ValueKind != JsonValueKind.Null
                        || record.GetProperty("passedCount").GetInt32() != 0
                        || record.GetProperty("failedCount").GetInt32() != 0
                        || record.GetProperty("artifactCreated").GetBoolean())
                        return false;
                }
                else
                {
                    int exitCode = record.GetProperty("exitCode").GetInt32();
                    int passed = record.GetProperty("passedCount").GetInt32();
                    int failed = record.GetProperty("failedCount").GetInt32();
                    if (status == "passed" && (exitCode != 0 || passed <= 0 || failed != 0)) return false;
                    if (status == "failed" && (exitCode <= 0 || failed <= 0)) return false;
                }
            }

            return true;
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            return false;
        }
    }

    internal static bool IsHex(string value, int length) =>
        value.Length == length && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static JsonObject BuildRecord(
        string? testRunnerRid,
        string? hostOs,
        string? hostDigest,
        string capability,
        string locale,
        string scenario,
        string recordedAt,
        string status,
        int? exitCode,
        int passedCount,
        int failedCount,
        bool artifactCreated) =>
        new()
        {
            ["client"] = "browser",
            ["packageRid"] = "browser-wasm",
            ["testRunnerRid"] = testRunnerRid is null ? null : JsonValue.Create(testRunnerRid),
            ["hostOs"] = hostOs is null ? null : JsonValue.Create(hostOs),
            ["hostDigest"] = hostDigest is null ? null : JsonValue.Create(hostDigest),
            ["capability"] = capability,
            ["locale"] = locale,
            ["scenario"] = scenario,
            ["recordedAt"] = recordedAt,
            ["status"] = status,
            ["exitCode"] = exitCode.HasValue ? JsonValue.Create(exitCode.Value) : null,
            ["passedCount"] = passedCount,
            ["failedCount"] = failedCount,
            ["artifactCreated"] = artifactCreated
        };
}
