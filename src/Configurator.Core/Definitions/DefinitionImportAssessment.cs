namespace Configurator.Core.Definitions;

using System.Text.Json;
using Configurator.Core.Composition;

/// <summary>Owns private evidence; only the detached report participates in ordinary serialization.</summary>
public sealed class DefinitionImportAssessment : IDisposable
{
    private Definition? _definition;
    private DefinitionImportEvidence? _evidence;
    private readonly string? _refusal;
    private readonly long? _inputRevision;
    private bool _disposed;

    internal DefinitionImportAssessment(DefinitionImportReport report, Definition? definition = null,
        DefinitionImportEvidence? evidence = null, string? refusal = null, long? inputRevision = null)
    {
        Report = report;
        _definition = definition;
        _evidence = evidence;
        _refusal = refusal;
        _inputRevision = inputRevision;
#if CONFIGURATOR_IMPORT_OWNERSHIP_PROBE
        Configurator.Engineering.ImportComparisonOwnershipProbe.ObserveProposal(this, evidence, evidence?.Answers, evidence?.Assignments);
#endif
    }
    public DefinitionImportReport Report { get; }
    public DefinitionImportResult AssessAutofill(AnswerSnapshot current,
        IReadOnlySet<string>? replaceableDefaults = null, CancellationToken cancellationToken = default) =>
        DefinitionImportWork.CompleteSynchronously(AssessAutofillCoreAsync(current, replaceableDefaults, null, cancellationToken));

    /// <summary>Cooperatively assesses a candidate without mutating the workspace. In-flight work retains its immutable evidence across disposal.</summary>
    public ValueTask<DefinitionImportResult> AssessAutofillAsync(AnswerSnapshot current,
        IReadOnlySet<string>? replaceableDefaults, Func<CancellationToken, ValueTask> cooperativeYield,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cooperativeYield);
        return AssessAutofillCoreAsync(current, replaceableDefaults, cooperativeYield, cancellationToken);
    }

    private async ValueTask<DefinitionImportResult> AssessAutofillCoreAsync(AnswerSnapshot current,
        IReadOnlySet<string>? replaceableDefaults, Func<CancellationToken, ValueTask>? cooperativeYield,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(current);
        cancellationToken.ThrowIfCancellationRequested();
        DefinitionImportEvidence? evidence = _evidence;
        Definition? definition = _definition;
        if (cooperativeYield is not null)
        {
            await cooperativeYield(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
        }
        if (evidence is null || definition is null || _refusal is not null)
            return new(false, null, _refusal ?? Report.Code);
        if (_inputRevision is { } revision && current.InputRevision != revision)
            return new(false, null, "context-stale");
        return await DefinitionImport.MergeAsync(definition, evidence, current, replaceableDefaults, cancellationToken,
            cooperativeYield: cooperativeYield);
    }

    public void Dispose()
    {
        _disposed = true;
#if CONFIGURATOR_IMPORT_OWNERSHIP_PROBE
        Configurator.Engineering.ImportComparisonOwnershipProbe.DisposeProposal(this);
#endif
        _evidence = null;
        _definition = null;
    }
    public override string ToString() => $"{nameof(DefinitionImportAssessment)}:{Report.Code}";
}
