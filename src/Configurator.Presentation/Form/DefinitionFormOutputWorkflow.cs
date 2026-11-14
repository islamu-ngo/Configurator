namespace Configurator.Presentation;

using System.Globalization;
using System.Text.Json;
using Configurator.Core.Definitions;

public sealed partial class DefinitionFormModel
{
    private readonly object _outputWorkflowOwner = new();
    private long _outputExportGeneration;
    private DefinitionOutputExportContext? _outputExportContext;
    private readonly List<(DefinitionOutputExportContext Context, List<DefinitionOutputDelivery> Deliveries)> _outputExports = [];

    /// <summary>Captures the complete eligible set from an adapter's successful preparation.</summary>
    public DefinitionOutputExportContext CaptureOutputExportContext(long preparedInputRevision,
        IEnumerable<string> eligibleArtifactIds)
    {
        ArgumentNullException.ThrowIfNull(eligibleArtifactIds);
        using var mutation = BeginMutation();
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (preparedInputRevision != InputRevision)
            throw new InvalidOperationException("export-context-stale");
        string[] artifacts = eligibleArtifactIds.Take(Definition.Artifacts.Count + 1).ToArray();
        string[] eligible = EligibleArtifacts.Select(artifact => artifact.Id).ToArray();
        if (artifacts.Length == 0 || artifacts.Length != artifacts.Distinct(StringComparer.Ordinal).Count()
            || !artifacts.ToHashSet(StringComparer.Ordinal).SetEquals(eligible))
            throw new ArgumentException("export-artifacts-invalid", nameof(eligibleArtifactIds));
        _outputExportGeneration = checked(_outputExportGeneration + 1);
        return _outputExportContext = new(_outputWorkflowOwner, _outputExportGeneration, Definition.Digest,
            SelectedOutputOption(), preparedInputRevision, artifacts);
    }

    /// <summary>Records acknowledged actions only while their prepared authority remains current.</summary>
    public bool RecordOutputExport(DefinitionOutputExportContext context,
        IEnumerable<string> successfulArtifactIds, DefinitionOutputDeliveryKind deliveryKind)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(successfulArtifactIds);
        using var mutation = BeginMutation();
        if (_disposed || !Enum.IsDefined(deliveryKind) || !ReferenceEquals(context.Owner, _outputWorkflowOwner)
            || !ReferenceEquals(context, _outputExportContext) || context.Generation != _outputExportGeneration
            || context.DefinitionDigest != Definition.Digest || context.InputRevision != InputRevision
            || context.Option != SelectedOutputOption())
            return false;
        string[] successes = successfulArtifactIds.Take(context.ArtifactIds.Count + 1).ToArray();
        if (successes.Length == 0 || successes.Length != successes.Distinct(StringComparer.Ordinal).Count()
            || successes.Any(id => !context.ArtifactIds.Contains(id, StringComparer.Ordinal)))
            return false;
        int index = _outputExports.FindIndex(item => item.Context.Option == context.Option);
        List<DefinitionOutputDelivery> deliveries;
        if (index >= 0 && _outputExports[index].Context.InputRevision == context.InputRevision
            && _outputExports[index].Context.ArtifactIds.ToHashSet(StringComparer.Ordinal).SetEquals(context.ArtifactIds))
            deliveries = _outputExports[index].Deliveries;
        else
        {
            deliveries = [];
            if (index >= 0) _outputExports.RemoveAt(index);
            _outputExports.Add((context, deliveries));
        }
        foreach (string id in successes)
        {
            var delivery = new DefinitionOutputDelivery(id, deliveryKind);
            if (!deliveries.Contains(delivery)) deliveries.Add(delivery);
        }
        OnPropertyChanged(nameof(OutputWorkflow));
        return true;
    }

    /// <summary>Retires preparation authority while retaining already acknowledged physical actions.</summary>
    public void InvalidateOutputExports()
    {
        using var mutation = BeginMutation();
        RetireOutputWorkflow(clearHistory: false);
    }

    private void RetireOutputWorkflow(bool clearHistory)
    {
        _outputExportContext = null;
        _outputExportGeneration = checked(_outputExportGeneration + 1);
        if (clearHistory) _outputExports.Clear();
        OnPropertyChanged(nameof(OutputWorkflow));
    }

    private string? SelectedOutputOption() =>
        Definition.Presentation?.OutputSelector is { } selector
        && Validate().TryGetEffectiveValue(selector.Location, out JsonElement selected)
        && selected.ValueKind == JsonValueKind.String ? selected.GetString() : null;

    public WorkspaceOutputWorkflow OutputWorkflow
    {
        get
        {
            lock (_gate)
            {
                if (_disposed) return new([], []);
                var tasks = new List<DefinitionOutputTaskState>();
                foreach (var receipt in _outputExports)
                {
                    bool stale = receipt.Context.InputRevision != InputRevision;
                    bool complete = receipt.Context.ArtifactIds.All(id =>
                        receipt.Deliveries.Any(delivery => delivery.ArtifactId == id));
                    tasks.Add(new(receipt.Context.Option,
                        stale ? DefinitionOutputExportStatus.ExportedStale : complete
                            ? DefinitionOutputExportStatus.ExportedCurrent : DefinitionOutputExportStatus.Partial,
                        Array.AsReadOnly(receipt.Context.ArtifactIds.ToArray()),
                        Array.AsReadOnly(receipt.Deliveries.ToArray())));
                }
                DefinitionOutputSelector? selector = Definition.Presentation?.OutputSelector;
                foreach (DefinitionOutputOption option in selector?.Options ?? [])
                    if (!tasks.Any(task => task.Option == option.Value))
                        tasks.Add(new(option.Value, DefinitionOutputExportStatus.NotExported,
                            option.ArtifactIds, []));
                string? selected = SelectedOutputOption();
                var proposals = new List<DefinitionOutputNextActionState>();
                if (selector is { Options.Count: > 1 }
                    && tasks.Any(task => task.Option == selected
                        && task.Status is DefinitionOutputExportStatus.Partial or DefinitionOutputExportStatus.ExportedCurrent))
                {
                    int optionIndex = selector.Options.ToList().FindIndex(option => option.Value == selected);
                    DefinitionOutputOption? option = optionIndex < 0 ? null : selector.Options[optionIndex];
                    foreach (var item in (option?.NextActions ?? []).Select((action, index) => (Action: action, Index: index))
                        .OrderBy(item => item.Action.Priority switch { "recommended" => 0, "optional" => 2, _ => 1 }))
                    {
                        DefinitionOutputNextAction action = item.Action;
                        if (action.EvaluateProposal(Validate()) != true
                            || tasks.Any(task => task.Option == action.TargetOption
                                && task.Status == DefinitionOutputExportStatus.ExportedCurrent))
                            continue;
                        string prefix = "/presentation/outputSelector/options/" + optionIndex.ToString(CultureInfo.InvariantCulture)
                            + "/nextActions/" + item.Index.ToString(CultureInfo.InvariantCulture);
                        proposals.Add(new(action.TargetOption,
                            _localization.Text(Locale, prefix + "/title", action.Title)!,
                            _localization.Text(Locale, prefix + "/description", action.Description), action.Priority));
                    }
                }
                return new(tasks.AsReadOnly(), proposals.AsReadOnly());
            }
        }
    }
}
