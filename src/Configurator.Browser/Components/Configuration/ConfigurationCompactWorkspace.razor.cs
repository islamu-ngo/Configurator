namespace Configurator.Browser.Components.Configuration;

using System.Globalization;
using Configurator.Browser.Resources;
using Configurator.Core.Definitions;
using Configurator.Presentation;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

public partial class ConfigurationCompactWorkspace
{
    [Inject] private IJSRuntime Javascript { get; set; } = null!;
    [Parameter, EditorRequired] public DefinitionFormModel Form { get; set; } = null!;
    [Parameter] public CancellationToken MountCancellation { get; set; }
    [Parameter] public DefinitionValidationResult? Validation { get; set; }
    [Parameter] public string? FocusField { get; set; }
    [Parameter] public long FocusGeneration { get; set; }
    [Parameter] public IReadOnlyList<string> FocusPath { get; set; } = [];
    [Parameter] public bool ShowMatchesOnly { get; set; }
    [Parameter] public EventCallback<bool> ShowMatchesOnlyChanged { get; set; }
    [Parameter] public EventCallback<string> SectionJumpRequested { get; set; }

    private const int FirstBatchSize = 6;
    private const int FollowingBatchSize = 10;
    private ConfigurationMountPlan _plan = ConfigurationMountPlan.Empty;
    private ConfigurationMountPlan.InitialDiscovery? _discovery;
    private long _planRevision = -1;
    private FieldState[] _activeFields = [];
    private Dictionary<string, int> _fieldIndexById = new(StringComparer.Ordinal);
    private DefinitionFormModel? _observedForm;
    private bool _observedLiteralRoot;
    private CancellationToken _observedCancellation;
    private CancellationTokenSource _mountWait = new();
    private long _observedRevision = -1;
    private string? _observedExperienceId;
    private FieldDisclosure? _observedDisclosure;
    private string? _observedFocusField;
    private long _observedFocusGeneration;
    private Task<IJSObjectReference>? _module;
    private ElementReference _mountElement;
    private long _mountGeneration;
    private long? _scheduledGeneration;
    private bool _disposed;
    private bool _renderAcknowledged;
    private bool _settlementPending;
    private bool _firstBoundaryPending = true;
    private bool _toolbarSynced;

    private bool LiteralRoot => !DefinitionEditingSupport.HasPropertyRoot(Form.Definition);
    private int PendingCount => _plan.PendingCount;
    private bool Discovering => _discovery is not null;
    private bool Busy => Discovering || PendingCount > 0 || !_renderAcknowledged;

    private IReadOnlyList<DefinitionFormSection>? _cachedApplicableSource;
    private long _cachedApplicableRevision = -1;
    private int _cachedApplicableSlotCount = -1;
    private DefinitionFormSection[]? _cachedApplicableSections;

    private DefinitionFormSection[] ApplicableSections
    {
        get
        {
            var projection = Form.Projection;
            if (ReferenceEquals(_cachedApplicableSource, projection.Sections) &&
                _cachedApplicableRevision == _planRevision &&
                _cachedApplicableSlotCount == _activeFields.Length &&
                _cachedApplicableSections is not null)
                return _cachedApplicableSections;

            string? outputField = Form.Definition.Presentation?.OutputSelector is { } selector
                ? DefinitionProfile.FieldId(selector.Location.Segments[0]) : null;
            _cachedApplicableSource = projection.Sections;
            _cachedApplicableRevision = _planRevision;
            _cachedApplicableSlotCount = _activeFields.Length;
            _cachedApplicableSections = projection.Sections
                .Where(section => section.IsApplicable &&
                    projection.SectionStatuses.GetValueOrDefault(section.Id)?.ConfigurationStatus != SectionConfigurationStatus.OutsideSelectedScope &&
                    section.Fields.Any(fieldId => fieldId != outputField && _fieldIndexById.ContainsKey(fieldId)))
                .ToArray();
            return _cachedApplicableSections;
        }
    }

    public bool IsSettled => !Busy;
    private DefinitionFormProjection? _cachedMatchProjection;
    private HashSet<string>? _cachedMatchedFieldIds;
    private DefinitionFormModel? _cachedLayoutForm;
    private string? _cachedLayoutExperienceId;
    private FieldDisclosure? _cachedLayoutDisclosure;
    private long _cachedLayoutRevision = -1;
    private int _cachedLayoutSlotCount = -1;
    private readonly Dictionary<string, CompactSectionLayout> _cachedSectionLayouts = new(StringComparer.Ordinal);

    private HashSet<string> MatchedFieldIds
    {
        get
        {
            DefinitionFormProjection projection = Form.Projection;
            if (!ReferenceEquals(_cachedMatchProjection, projection) || _cachedMatchedFieldIds is null)
            {
                _cachedMatchProjection = projection;
                var matches = projection.Matches;
                var next = new HashSet<string>(matches.Count, StringComparer.Ordinal);
                foreach (var match in matches)
                    next.Add(match.Entry.FieldId);
                _cachedMatchedFieldIds = next;
            }
            return _cachedMatchedFieldIds;
        }
    }

    private CompactSectionLayout GetSectionLayout(DefinitionFormSection section)
    {
        if (!ReferenceEquals(_cachedLayoutForm, Form) ||
            !string.Equals(_cachedLayoutExperienceId, Form.SelectedExperienceId, StringComparison.Ordinal) ||
            _cachedLayoutDisclosure != Form.DisclosureOverride ||
            _cachedLayoutRevision != _planRevision ||
            _cachedLayoutSlotCount != _activeFields.Length)
        {
            _cachedLayoutForm = Form;
            _cachedLayoutExperienceId = Form.SelectedExperienceId;
            _cachedLayoutDisclosure = Form.DisclosureOverride;
            _cachedLayoutRevision = _planRevision;
            _cachedLayoutSlotCount = _activeFields.Length;
            _cachedSectionLayouts.Clear();
        }
        if (_cachedSectionLayouts.TryGetValue(section.Id, out CompactSectionLayout? layout))
            return layout;

        var sectionIndices = SectionFieldIndices(section).ToArray();
        var groupedIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var group in section.Groups)
        {
            if (!group.IsApplicable) continue;
            foreach (string fieldId in group.Fields)
                groupedIds.Add(fieldId);
        }

        var ungrouped = new List<int>(sectionIndices.Length);
        foreach (int index in sectionIndices)
            if (!groupedIds.Contains(_activeFields[index].Id))
                ungrouped.Add(index);

        var groups = new Dictionary<string, int[]>(section.Groups.Count, StringComparer.Ordinal);
        foreach (var group in section.Groups)
        {
            if (!group.IsApplicable) continue;
            var groupFieldSet = new HashSet<string>(group.Fields, StringComparer.Ordinal);
            var indices = new List<int>(group.Fields.Count);
            foreach (int index in sectionIndices)
                if (groupFieldSet.Contains(_activeFields[index].Id))
                    indices.Add(index);
            groups[group.Id] = indices.ToArray();
        }

        var fieldIds = new HashSet<string>(sectionIndices.Length, StringComparer.Ordinal);
        foreach (int index in sectionIndices)
            fieldIds.Add(_activeFields[index].Id);

        layout = new CompactSectionLayout(ungrouped.ToArray(), groups, sectionIndices, fieldIds);
        _cachedSectionLayouts[section.Id] = layout;
        return layout;
    }

    private static bool MountTreesEqual(ConfigurationMountNode a, ConfigurationMountNode b)
    {
        if (a.Identity != b.Identity || a.Admitted != b.Admitted || !a.Location.Equals(b.Location) ||
            a.Children.Count != b.Children.Count)
            return false;
        for (int i = 0; i < a.Children.Count; i++)
            if (!MountTreesEqual(a.Children[i], b.Children[i])) return false;
        return true;
    }

    private static bool MountPlansEquivalent(ConfigurationMountPlan previous, ConfigurationMountPlan current)
    {
        if (ReferenceEquals(previous, current)) return true;
        if (previous.PendingCount > 0 || current.PendingCount > 0) return false;
        if (previous.Roots.Count != current.Roots.Count) return false;
        for (int i = 0; i < previous.Roots.Count; i++)
            if (!MountTreesEqual(previous.Roots[i], current.Roots[i])) return false;
        return true;
    }

    private bool IsFieldHiddenByFilter(string fieldId) =>
        ShowMatchesOnly && (Form.Projection.Matches.Count == 0 || !MatchedFieldIds.Contains(fieldId));

    private bool IsSectionHiddenByFilter(int[] sectionIndices)
    {
        if (!ShowMatchesOnly) return false;
        foreach (int index in sectionIndices)
            if (!IsFieldHiddenByFilter(_activeFields[index].Id))
                return false;
        return true;
    }

    private IEnumerable<int> SectionFieldIndices(DefinitionFormSection section)
    {
        foreach (string fieldId in section.Fields)
        {
            if (_fieldIndexById.TryGetValue(fieldId, out int index))
                yield return index;
        }
    }

    private string Ui(string key) => BrowserStrings.Get(key, Form.Locale);

    private Task OnShowMatchesOnlyChanged(ChangeEventArgs args) =>
        ShowMatchesOnlyChanged.InvokeAsync(args.Value is true);

    public async Task JumpToSectionAsync(string sectionId)
    {
        if (_disposed) return;
        _module ??= Javascript.InvokeAsync<IJSObjectReference>("import", "./js/configuration-rendering.js").AsTask();
        IJSObjectReference module = await _module;
        if (_disposed) return;
        await module.InvokeVoidAsync("revealHeading", _mountElement, ConfigurationCompactSection.SectionHeadingId(sectionId));
        _toolbarSynced = true;
        Form.RecordSectionOpened(sectionId);
        if (SectionJumpRequested.HasDelegate)
            await SectionJumpRequested.InvokeAsync(sectionId);
    }

    private bool IsCurrentMount(long generation, CancellationToken cancellation) =>
        !_disposed && !cancellation.IsCancellationRequested && generation == _mountGeneration &&
        ReferenceEquals(_observedForm, Form) && _planRevision == Form.InputRevision;

    protected override void OnParametersSet()
    {
        bool replacement = !ReferenceEquals(_observedForm, Form);
        if (replacement)
        {
            _settlementPending = false;
            _toolbarSynced = false;
        }
        bool focusChanged = FocusField is not null &&
            (_observedFocusField != FocusField || _observedFocusGeneration != FocusGeneration);
        bool inputsMatch = !replacement &&
            _observedLiteralRoot == LiteralRoot &&
            _observedRevision == Form.InputRevision &&
            _planRevision == Form.InputRevision &&
            string.Equals(_observedExperienceId, Form.SelectedExperienceId, StringComparison.Ordinal) &&
            _observedDisclosure == Form.DisclosureOverride &&
            _observedCancellation == MountCancellation;
        if (inputsMatch)
        {
            _observedFocusField = FocusField;
            _observedFocusGeneration = FocusGeneration;
            if (focusChanged)
            {
                ++_mountGeneration;
                _renderAcknowledged = false;
                _firstBoundaryPending = true;
                MountBatch(FirstBatchSize);
            }
            return;
        }

        FieldState[] activeFields = ComputeActiveFields();
        bool activeFieldsChanged = _observedLiteralRoot != LiteralRoot ||
            !_activeFields.Select(field => field.Id).SequenceEqual(activeFields.Select(field => field.Id));
        bool discovering = Discovering;
        ConfigurationMountPlan previous = replacement ? ConfigurationMountPlan.Empty : _plan;
        bool hardReconcile = replacement || activeFieldsChanged || _observedRevision != Form.InputRevision ||
            _observedCancellation != MountCancellation;

        if (hardReconcile)
        {
            _discovery?.Clear();
            _discovery = null;
        }

        _activeFields = activeFields;
        _fieldIndexById = activeFields
            .Select((state, index) => (state.Id, index))
            .ToDictionary(pair => pair.Id, pair => pair.index, StringComparer.Ordinal);

        if (replacement)
        {
            _discovery = new();
            _plan = _discovery.Start(Form, _activeFields, LiteralRoot);
        }
        else if (hardReconcile || !discovering)
        {
            _plan = ConfigurationMountPlan.Reconcile(Form, _activeFields, LiteralRoot, previous);
        }

        _planRevision = Form.InputRevision;
        bool topologyChanged = !ReferenceEquals(previous, _plan);
        _settlementPending |= topologyChanged;
        bool scalarSameTopology = !replacement && !discovering && PendingCount == 0 && !activeFieldsChanged &&
            !focusChanged && _observedCancellation == MountCancellation && _renderAcknowledged &&
            MountPlansEquivalent(previous, _plan);
        if (scalarSameTopology)
        {
            topologyChanged = false;
            _settlementPending = false;
            _observedForm = Form;
            _observedLiteralRoot = LiteralRoot;
            _observedRevision = Form.InputRevision;
            _observedExperienceId = Form.SelectedExperienceId;
            _observedDisclosure = Form.DisclosureOverride;
        }
        bool superseded = !scalarSameTopology && (replacement || _observedCancellation != MountCancellation ||
            _observedRevision != Form.InputRevision || activeFieldsChanged || topologyChanged || focusChanged);
        if (superseded)
        {
            ++_mountGeneration;
            _mountWait.Cancel();
            _mountWait.Dispose();
            _mountWait = CancellationTokenSource.CreateLinkedTokenSource(MountCancellation);
            _observedForm = Form;
            _observedLiteralRoot = LiteralRoot;
            _observedCancellation = MountCancellation;
            _observedRevision = Form.InputRevision;
            _observedExperienceId = Form.SelectedExperienceId;
            _observedDisclosure = Form.DisclosureOverride;
            _renderAcknowledged = false;
        }
        _observedFocusField = FocusField;
        _observedFocusGeneration = FocusGeneration;
        if (replacement || topologyChanged && !discovering || focusChanged)
        {
            _firstBoundaryPending = true;
            MountBatch(FirstBatchSize);
        }
    }

    private FieldState[] ComputeActiveFields()
    {
        if (LiteralRoot) return [];
        var projection = Form.Projection;
        DefinitionValidationResult validation = Form.Validate();
        string? outputField = Form.Definition.Presentation?.OutputSelector is { } selector
            ? DefinitionProfile.FieldId(selector.Location.Segments[0]) : null;
        var ordered = new List<FieldState>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var section in projection.Sections)
        {
            if (!section.IsApplicable ||
                projection.SectionStatuses.GetValueOrDefault(section.Id)?.ConfigurationStatus == SectionConfigurationStatus.OutsideSelectedScope)
                continue;
            foreach (string fieldId in section.Fields)
            {
                if (fieldId == outputField || !seen.Add(fieldId)) continue;
                if (validation.FindField(fieldId) is { } state && state.IsActive && projection.IsFieldVisible(fieldId))
                    ordered.Add(state);
            }
        }
        return ordered.ToArray();
    }

    private void MountBatch(int maximumFields)
    {
        DefinitionPath? priority = FocusField is null ? null : LiteralRoot
            ? DefinitionPath.FromSegments(FocusPath)
            : _fieldIndexById.ContainsKey(FocusField)
                ? DefinitionPath.FromSegments(new[] { Form.GetFieldEditor(FocusField).Input.Definition.Name }.Concat(FocusPath.Skip(1)))
                : null;
        if (_discovery is { } discovery)
        {
            _plan = discovery.Advance(Form, _activeFields, LiteralRoot, _plan, maximumFields, priority);
            if (discovery.Complete)
            {
                discovery.Clear();
                _discovery = null;
            }
        }
        else
            _plan = _plan.Admit(maximumFields, priority);
        _settlementPending |= Discovering || PendingCount > 0;
        _renderAcknowledged = false;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_disposed || _mountWait.IsCancellationRequested || _planRevision != Form.InputRevision) return;
        if (!Discovering && PendingCount == 0 && (!_settlementPending || _renderAcknowledged))
        {
            if (!_toolbarSynced)
            {
                _toolbarSynced = true;
                try
                {
                    _module ??= Javascript.InvokeAsync<IJSObjectReference>("import", "./js/configuration-rendering.js").AsTask();
                    IJSObjectReference module = await _module.WaitAsync(_mountWait.Token);
                    if (!_disposed && !_mountWait.IsCancellationRequested)
                        await module.InvokeVoidAsync("syncCompactToolbarClearance", _mountElement).AsTask().WaitAsync(_mountWait.Token);
                }
                catch (OperationCanceledException) when (_mountWait.IsCancellationRequested)
                {
                }
            }
            if (!_renderAcknowledged)
            {
                _renderAcknowledged = true;
                StateHasChanged();
            }
            return;
        }
        if (_scheduledGeneration == _mountGeneration) return;
        long generation = _mountGeneration;
        CancellationToken cancellation = _mountWait.Token;
        _scheduledGeneration = generation;
        try
        {
            _module ??= Javascript.InvokeAsync<IJSObjectReference>("import", "./js/configuration-rendering.js").AsTask();
            IJSObjectReference module = await _module.WaitAsync(cancellation);
            if (!IsCurrentMount(generation, cancellation)) return;
            if (!_toolbarSynced)
            {
                _toolbarSynced = true;
                await module.InvokeVoidAsync("syncCompactToolbarClearance", _mountElement).AsTask().WaitAsync(cancellation);
                if (!IsCurrentMount(generation, cancellation)) return;
            }
            if (!Discovering && PendingCount == 0)
            {
                await module.InvokeVoidAsync("nextTask").AsTask().WaitAsync(cancellation);
                if (!IsCurrentMount(generation, cancellation)) return;
                await module.InvokeVoidAsync("revealFocusedControl", _mountElement, generation.ToString(CultureInfo.InvariantCulture))
                    .AsTask().WaitAsync(cancellation);
                if (!IsCurrentMount(generation, cancellation)) return;
                _settlementPending = false;
                _renderAcknowledged = true;
                _scheduledGeneration = null;
                StateHasChanged();
                return;
            }
            await module.InvokeVoidAsync(_firstBoundaryPending ? "nextFrame" : "nextTask").AsTask().WaitAsync(cancellation);
            if (!IsCurrentMount(generation, cancellation)) return;
            _firstBoundaryPending = false;
            MountBatch(FollowingBatchSize);
            _scheduledGeneration = null;
            StateHasChanged();
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        ++_mountGeneration;
        _mountWait.Cancel();
        _mountWait.Dispose();
        _discovery?.Clear();
        _discovery = null;
        _plan = ConfigurationMountPlan.Empty;
        _mountElement = default;
        _settlementPending = false;
        _activeFields = [];
        _fieldIndexById.Clear();
        _observedForm = null;
        if (_module is not null)
        {
            IJSObjectReference module = await _module;
            await module.DisposeAsync();
        }
        GC.SuppressFinalize(this);
    }
}
