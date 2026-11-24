namespace Configurator.Core.Definitions.Authoring;

using Configurator.Core.Artifacts;

public enum DefinitionFixtureCaseOutcome
{
    Passed,
    PassedExpectedRefusal,
    Failed
}

public sealed record DefinitionFixtureCaseResult(
    string CaseId,
    string Kind,
    DefinitionFixtureCaseOutcome Outcome,
    bool ExpectedRefusal,
    string? FailureCode,
    IReadOnlyList<string> ObservedDiagnosticCodes,
    IReadOnlyList<string> ObservedArtifactIds);

public sealed class DefinitionFixtureRunReport
{
    internal DefinitionFixtureRunReport(
        Definition definition,
        DefinitionFixtureDocument suite,
        IReadOnlyList<DefinitionFixtureCaseResult> results)
    {
        DefinitionDigest = definition.Digest.Value;
        FixtureDigest = suite.Digest.Value;
        EngineVersion = ConfiguratorVersion.Current;
        Scope = DefinitionFixtureRunner.ConformanceScope;
        ClaimBoundary = DefinitionFixtureRunner.DefaultClaimBoundary;
        Results = results;
        TotalCases = results.Count;
        int passed = 0;
        int failed = 0;
        int expectedRefusals = 0;
        foreach (DefinitionFixtureCaseResult result in results)
        {
            if (result.Outcome == DefinitionFixtureCaseOutcome.Failed) failed++;
            else passed++;
            if (result.Outcome == DefinitionFixtureCaseOutcome.PassedExpectedRefusal) expectedRefusals++;
        }
        PassedCases = passed;
        FailedCases = failed;
        ExpectedRefusalCases = expectedRefusals;
        Passed = TotalCases > 0 && FailedCases == 0;
    }

    public string DefinitionDigest { get; }
    public string FixtureDigest { get; }
    public string EngineVersion { get; }
    public string Scope { get; }
    public string ClaimBoundary { get; }
    public int TotalCases { get; }
    public int PassedCases { get; }
    public int FailedCases { get; }
    public int ExpectedRefusalCases { get; }
    public bool Passed { get; }
    public IReadOnlyList<DefinitionFixtureCaseResult> Results { get; }
}

/// <summary>
/// Executes bounded data-only synthetic fixture suites against an admitted definition using the
/// standard offline preparation and import engines. Never executes external tools or claims target acceptance.
/// </summary>
public static class DefinitionFixtureRunner
{
    public const string ConformanceScope = "synthetic-data-only-local-conformance";
    public const string DefaultClaimBoundary =
        "Exercises only the declared synthetic fixture cases against the local offline engine; grants no target runtime acceptance, security approval, or publication authority.";

    public static DefinitionFixtureRunReport Run(
        Definition definition,
        DefinitionFixtureDocument suite,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(suite);
        cancellationToken.ThrowIfCancellationRequested();

        var results = new List<DefinitionFixtureCaseResult>(suite.Cases.Count);
        foreach (DefinitionFixtureCase fixtureCase in suite.Cases)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DefinitionFixtureCaseResult result = fixtureCase.Kind == DefinitionFixtureCaseKind.Prepare
                ? RunPrepareCase(definition, fixtureCase, cancellationToken)
                : RunImportCase(definition, fixtureCase, cancellationToken);
            results.Add(result);
        }

        return new DefinitionFixtureRunReport(definition, suite, results.AsReadOnly());
    }

    private static DefinitionFixtureCaseResult RunPrepareCase(
        Definition definition,
        DefinitionFixtureCase fixtureCase,
        CancellationToken cancellationToken)
    {
        DefinitionFixtureExpectation expect = fixtureCase.Expect;
        bool expectsRefusal = expect.Refusal is not null;

        if (fixtureCase.Draft)
            return RunDraftPrepareCase(definition, fixtureCase, expectsRefusal, cancellationToken);

        try
        {
            using DefinitionBundleResult bundle = fixtureCase.ArtifactId is { } selectedId
                ? DefinitionBundleComposer.Compose(definition, [selectedId], fixtureCase.Answers!, cancellationToken)
                : DefinitionBundleComposer.ComposeActive(definition, fixtureCase.Answers!, cancellationToken);

            IReadOnlyList<string> observedCodes = DistinctCodes(bundle.Diagnostics);
            IReadOnlyList<string> observedIds = bundle.Artifacts.Select(static item => item.ArtifactId).ToArray();
            string? observedRefusal = bundle.Succeeded ? null : ResolvePrepareRefusal(bundle.Diagnostics);

            if (expect.Ready is { } expectedReady && bundle.Succeeded != expectedReady)
                return Fail(fixtureCase.Id, "prepare", expectsRefusal, "readiness-mismatch", observedCodes, observedIds);

            if (expectsRefusal)
            {
                if (!MatchesRefusal(expect.Refusal!, observedRefusal, observedCodes))
                    return Fail(fixtureCase.Id, "prepare", expectsRefusal: true, "refusal-mismatch", observedCodes, observedIds);
            }
            else if (!bundle.Succeeded && expect.Ready != false && expect.DiagnosticCodes.Count == 0)
            {
                return Fail(fixtureCase.Id, "prepare", expectsRefusal: false, observedRefusal ?? "validation-failed", observedCodes, observedIds);
            }

            if (!MatchesDiagnostics(expect.DiagnosticCodes, observedCodes))
                return Fail(fixtureCase.Id, "prepare", expectsRefusal, "diagnostic-mismatch", observedCodes, observedIds);

            string? artifactFailure = VerifyCompleteArtifacts(expect.Artifacts, bundle.Artifacts);
            if (artifactFailure is not null)
                return Fail(fixtureCase.Id, "prepare", expectsRefusal, artifactFailure, observedCodes, observedIds);

            return new DefinitionFixtureCaseResult(
                fixtureCase.Id,
                "prepare",
                expectsRefusal ? DefinitionFixtureCaseOutcome.PassedExpectedRefusal : DefinitionFixtureCaseOutcome.Passed,
                expectsRefusal,
                null,
                observedCodes,
                observedIds);
        }
        catch (DefinitionException exception)
        {
            string[] observedCodes = [exception.Code];
            if (expectsRefusal && string.Equals(expect.Refusal, exception.Code, StringComparison.Ordinal)
                && expect.Ready != true
                && MatchesDiagnostics(expect.DiagnosticCodes, observedCodes))
            {
                return new DefinitionFixtureCaseResult(
                    fixtureCase.Id,
                    "prepare",
                    DefinitionFixtureCaseOutcome.PassedExpectedRefusal,
                    ExpectedRefusal: true,
                    null,
                    observedCodes,
                    Array.Empty<string>());
            }

            return Fail(fixtureCase.Id, "prepare", expectsRefusal,
                expectsRefusal ? "refusal-mismatch" : exception.Code, observedCodes, Array.Empty<string>());
        }
    }

    private static DefinitionFixtureCaseResult RunDraftPrepareCase(
        Definition definition,
        DefinitionFixtureCase fixtureCase,
        bool expectsRefusal,
        CancellationToken cancellationToken)
    {
        DefinitionFixtureExpectation expect = fixtureCase.Expect;
        try
        {
            using DefinitionDraftBundleResult draft = fixtureCase.ArtifactId is { } selectedId
                ? DefinitionDraftArtifactComposer.ComposeBundle(definition, [selectedId], fixtureCase.Answers!, cancellationToken)
                : DefinitionDraftArtifactComposer.ComposeActiveBundle(definition, fixtureCase.Answers!, cancellationToken);

            IReadOnlyList<string> observedCodes = DistinctCodes(draft.Diagnostics);
            IReadOnlyList<string> observedIds = draft.Artifacts.Select(static item => item.ArtifactId).ToArray();
            string? observedRefusal = draft.Succeeded ? null : ResolvePrepareRefusal(draft.Diagnostics);

            if (expect.Ready is { } expectedReady && draft.Succeeded != expectedReady)
                return Fail(fixtureCase.Id, "prepare", expectsRefusal, "readiness-mismatch", observedCodes, observedIds);

            if (expectsRefusal && !MatchesRefusal(expect.Refusal!, observedRefusal, observedCodes))
                return Fail(fixtureCase.Id, "prepare", expectsRefusal: true, "refusal-mismatch", observedCodes, observedIds);

            if (!MatchesDiagnostics(expect.DiagnosticCodes, observedCodes))
                return Fail(fixtureCase.Id, "prepare", expectsRefusal, "diagnostic-mismatch", observedCodes, observedIds);

            string? artifactFailure = VerifyDraftArtifacts(expect.Artifacts, draft);
            if (artifactFailure is not null)
                return Fail(fixtureCase.Id, "prepare", expectsRefusal, artifactFailure, observedCodes, observedIds);

            return new DefinitionFixtureCaseResult(
                fixtureCase.Id,
                "prepare",
                expectsRefusal ? DefinitionFixtureCaseOutcome.PassedExpectedRefusal : DefinitionFixtureCaseOutcome.Passed,
                expectsRefusal,
                null,
                observedCodes,
                observedIds);
        }
        catch (DefinitionException exception)
        {
            string[] observedCodes = [exception.Code];
            if (expectsRefusal && string.Equals(expect.Refusal, exception.Code, StringComparison.Ordinal)
                && expect.Ready != true
                && MatchesDiagnostics(expect.DiagnosticCodes, observedCodes))
            {
                return new DefinitionFixtureCaseResult(
                    fixtureCase.Id,
                    "prepare",
                    DefinitionFixtureCaseOutcome.PassedExpectedRefusal,
                    ExpectedRefusal: true,
                    null,
                    observedCodes,
                    Array.Empty<string>());
            }

            return Fail(fixtureCase.Id, "prepare", expectsRefusal,
                expectsRefusal ? "refusal-mismatch" : exception.Code, observedCodes, Array.Empty<string>());
        }
    }

    private static DefinitionFixtureCaseResult RunImportCase(
        Definition definition,
        DefinitionFixtureCase fixtureCase,
        CancellationToken cancellationToken)
    {
        DefinitionFixtureExpectation expect = fixtureCase.Expect;
        bool expectsRefusal = expect.Refusal is not null;

        using DefinitionImportAssessment assessment = DefinitionImport.Inspect(
            definition,
            fixtureCase.ArtifactId!,
            fixtureCase.ImportBytes!.Value,
            fixtureCase.ContextAnswers,
            cancellationToken);

        DefinitionImportReport report = assessment.Report;
        var observedCodesList = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        if (!string.IsNullOrEmpty(report.Code) && seen.Add(report.Code))
            observedCodesList.Add(report.Code);
        foreach (DefinitionImportFinding finding in report.Findings)
            if (seen.Add(finding.Code))
                observedCodesList.Add(finding.Code);
        IReadOnlyList<string> observedCodes = observedCodesList.AsReadOnly();
        string[] observedArtifacts = [fixtureCase.ArtifactId!];

        if (expect.ImportCompatibility is { } expectedCompat && report.Compatibility != expectedCompat)
            return Fail(fixtureCase.Id, "import", expectsRefusal, "import-compatibility-mismatch", observedCodes, observedArtifacts);

        if (expect.ImportReady is { } expectedImportReady && report.AutofillEligible != expectedImportReady)
            return Fail(fixtureCase.Id, "import", expectsRefusal, "readiness-mismatch", observedCodes, observedArtifacts);

        if (expectsRefusal && !string.Equals(expect.Refusal, report.Code, StringComparison.Ordinal))
            return Fail(fixtureCase.Id, "import", expectsRefusal: true, "refusal-mismatch", observedCodes, observedArtifacts);

        if (!MatchesDiagnostics(expect.DiagnosticCodes, observedCodes))
            return Fail(fixtureCase.Id, "import", expectsRefusal, "diagnostic-mismatch", observedCodes, observedArtifacts);

        return new DefinitionFixtureCaseResult(
            fixtureCase.Id,
            "import",
            expectsRefusal ? DefinitionFixtureCaseOutcome.PassedExpectedRefusal : DefinitionFixtureCaseOutcome.Passed,
            expectsRefusal,
            null,
            observedCodes,
            observedArtifacts);
    }

    private static string? VerifyCompleteArtifacts(
        IReadOnlyList<DefinitionFixtureArtifactExpectation> expected,
        IReadOnlyList<DefinitionArtifact> actual)
    {
        if (expected.Count == 0) return null;
        var actualMap = new Dictionary<string, DefinitionArtifact>(StringComparer.Ordinal);
        foreach (DefinitionArtifact artifact in actual)
            actualMap[artifact.ArtifactId] = artifact;

        foreach (DefinitionFixtureArtifactExpectation expectation in expected)
        {
            if (!actualMap.TryGetValue(expectation.Id, out DefinitionArtifact? artifact))
                return "artifact-missing";
            if (expectation.FileName is not null && !string.Equals(artifact.FileName, expectation.FileName, StringComparison.Ordinal))
                return "artifact-filename-mismatch";
            if (expectation.Deferred == true)
                return "artifact-deferred-mismatch";
            if (expectation.ExpectedSha256 is not null
                && !string.Equals(artifact.Digest.Value, expectation.ExpectedSha256, StringComparison.Ordinal))
                return "artifact-digest-mismatch";
            if (!expectation.MatchesBytes(artifact.Bytes.Span))
                return "artifact-content-mismatch";
        }

        return null;
    }

    private static string? VerifyDraftArtifacts(
        IReadOnlyList<DefinitionFixtureArtifactExpectation> expected,
        DefinitionDraftBundleResult draft)
    {
        if (expected.Count == 0) return null;
        var actualMap = new Dictionary<string, DefinitionArtifact>(StringComparer.Ordinal);
        foreach (DefinitionArtifact artifact in draft.Artifacts)
            actualMap[artifact.ArtifactId] = artifact;

        var deferredArtifactIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (DefinitionDraftCompletion completion in draft.Completions)
            deferredArtifactIds.Add(completion.ArtifactId);

        foreach (DefinitionFixtureArtifactExpectation expectation in expected)
        {
            if (!actualMap.TryGetValue(expectation.Id, out DefinitionArtifact? artifact))
                return "artifact-missing";
            if (expectation.FileName is not null && !string.Equals(artifact.FileName, expectation.FileName, StringComparison.Ordinal))
                return "artifact-filename-mismatch";
            bool isDeferred = deferredArtifactIds.Contains(artifact.ArtifactId);
            if (expectation.Deferred is { } expectedDeferred && isDeferred != expectedDeferred)
                return "artifact-deferred-mismatch";
            if (expectation.ExpectedSha256 is not null
                && !string.Equals(artifact.Digest.Value, expectation.ExpectedSha256, StringComparison.Ordinal))
                return "artifact-digest-mismatch";
            if (!expectation.MatchesBytes(artifact.Bytes.Span))
                return "artifact-content-mismatch";
        }

        return null;
    }

    private static string ResolvePrepareRefusal(IReadOnlyList<DefinitionDiagnostic> diagnostics) =>
        diagnostics.Count > 0 ? "validation-failed" : "preparation-failed";

    private static bool MatchesRefusal(string expectedRefusal, string? observedRefusal, IReadOnlyList<string> observedCodes) =>
        string.Equals(expectedRefusal, observedRefusal, StringComparison.Ordinal)
        || observedCodes.Contains(expectedRefusal, StringComparer.Ordinal);

    private static bool MatchesDiagnostics(IReadOnlyList<string> expectedCodes, IReadOnlyList<string> observedCodes)
    {
        foreach (string expected in expectedCodes)
            if (!observedCodes.Contains(expected, StringComparer.Ordinal))
                return false;
        return true;
    }

    private static IReadOnlyList<string> DistinctCodes(IReadOnlyList<DefinitionDiagnostic> diagnostics)
    {
        if (diagnostics.Count == 0) return Array.Empty<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var ordered = new List<string>(diagnostics.Count);
        foreach (DefinitionDiagnostic diagnostic in diagnostics)
            if (seen.Add(diagnostic.Code))
                ordered.Add(diagnostic.Code);
        return ordered.AsReadOnly();
    }

    private static DefinitionFixtureCaseResult Fail(
        string caseId,
        string kind,
        bool expectsRefusal,
        string failureCode,
        IReadOnlyList<string> observedCodes,
        IReadOnlyList<string> observedArtifacts) =>
        new(caseId, kind, DefinitionFixtureCaseOutcome.Failed, expectsRefusal, failureCode, observedCodes, observedArtifacts);
}
