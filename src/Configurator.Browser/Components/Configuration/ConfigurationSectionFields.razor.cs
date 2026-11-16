namespace Configurator.Browser.Components.Configuration;

using System.Globalization;
using Configurator.Core.Definitions;
using Configurator.Presentation;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web.Virtualization;
using Microsoft.JSInterop;

public partial class ConfigurationSectionFields
{
    internal const int DirectSliceCount = 4;
    internal const int VirtualizeRowThreshold = 16;

    [Inject] private IJSRuntime Javascript { get; set; } = null!;
    [Parameter, EditorRequired] public DefinitionFormModel Form { get; set; } = null!;
    [Parameter] public string? SectionId { get; set; }
    [Parameter] public IReadOnlyList<DefinitionFormSectionGroup> Groups { get; set; } = [];
    [Parameter] public IReadOnlyList<FieldState> Fields { get; set; } = [];
    [Parameter] public bool LiteralRoot { get; set; }
    [Parameter] public bool ContinuousMode { get; set; }
    [Parameter] public bool SectionAllRequired { get; set; }
    [Parameter] public CancellationToken MountCancellation { get; set; }
    [Parameter] public DefinitionValidationResult? Validation { get; set; }
    [Parameter] public string? FocusField { get; set; }
    [Parameter] public long FocusGeneration { get; set; }
    [Parameter] public IReadOnlyList<string> FocusPath { get; set; } = [];

    private const int FirstBatchSize = 6;
    private const int FollowingBatchSize = 10;
    private ConfigurationMountPlan _plan = ConfigurationMountPlan.Empty;
    private ConfigurationMountPlan.InitialDiscovery? _discovery;
    private long _planRevision = -1;
    private FieldState[] _activeFields = [];
    private FieldState[] _planFields = [];
    private Dictionary<string, int> _planIndicesByFieldId = new(StringComparer.Ordinal);
    private DefinitionFormModel? _observedForm;
    private DefinitionFormProjection? _observedProjection;
    private bool _observedLiteralRoot;
    private bool _observedContinuousMode;
    private CancellationToken _observedCancellation;
    private CancellationTokenSource _mountWait = new();
    private long _observedRevision = -1;
    private long _sliceTopologyVersion;
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

    internal static bool IsComplexField(DefinitionNode schema) =>
        schema.Type is DefinitionNodeType.Mapping or DefinitionNodeType.Sequence || schema.Variants.Count > 0;

    internal bool IsSettled => !Busy;

    private static int ResolvePinnedFocusIndex(FieldState[] items, string? focusField)
    {
        if (string.IsNullOrEmpty(focusField)) return -1;
        for (int index = 0; index < items.Length; index++)
            if (string.Equals(items[index].Id, focusField, StringComparison.Ordinal))
                return index;
        return -1;
    }

    private static ValueTask<ItemsProviderResult<FieldState>> ProvideSlice(
        FieldState[] slice, ItemsProviderRequest request)
    {
        int start = Math.Clamp(request.StartIndex, 0, slice.Length);
        int count = Math.Clamp(request.Count, 0, slice.Length - start);
        FieldState[] page = count == 0 ? [] : slice[start..(start + count)];
        return ValueTask.FromResult(new ItemsProviderResult<FieldState>(page, slice.Length));
    }

    private int PendingCount => _plan.PendingCount;
    private bool Discovering => _discovery is not null;
    private bool Busy => Discovering || PendingCount > 0 || !_renderAcknowledged;
    private bool IsCurrentMount(long generation, CancellationToken cancellation) =>
        !_disposed && !cancellation.IsCancellationRequested && generation == _mountGeneration &&
        ReferenceEquals(_observedForm, Form) && ReferenceEquals(_observedProjection, Form.Projection) &&
        _planRevision == Form.InputRevision;

    protected override void OnParametersSet()
    {
        bool replacement = !ReferenceEquals(_observedForm, Form);
        if (replacement) _settlementPending = false;
        FieldState[] activeFields = Fields.Where(state => state.IsActive).ToArray();
        bool activeFieldIdsChanged = !_activeFields.Select(field => field.Id).SequenceEqual(activeFields.Select(field => field.Id));
        bool projectionTriggerChanged = !ContinuousMode || activeFields.Any(state => IsComplexField(Form.GetFieldEditor(state.Id).Schema));
        bool viewChanged = _observedLiteralRoot != LiteralRoot ||
            _observedContinuousMode != ContinuousMode ||
            activeFieldIdsChanged ||
            (projectionTriggerChanged && !ReferenceEquals(_observedProjection, Form.Projection));
        bool discovering = Discovering;
        ConfigurationMountPlan previous = replacement || _observedContinuousMode != ContinuousMode ? ConfigurationMountPlan.Empty : _plan;
        bool reconcile = replacement || viewChanged || _observedRevision != Form.InputRevision ||
            _observedCancellation != MountCancellation;

        if (reconcile)
        {
            _discovery?.Clear();
            _discovery = null;
        }

        if (replacement || _observedContinuousMode != ContinuousMode || activeFieldIdsChanged ||
            _observedFocusField != FocusField || _observedFocusGeneration != FocusGeneration)
        {
            ++_sliceTopologyVersion;
        }

        _activeFields = activeFields;
        _planFields = ContinuousMode && !LiteralRoot
            ? activeFields.Where(state => IsComplexField(Form.GetFieldEditor(state.Id).Schema)).ToArray()
            : activeFields;
        _planIndicesByFieldId = _planFields
            .Select((state, index) => (state.Id, index))
            .ToDictionary(pair => pair.Id, pair => pair.index, StringComparer.Ordinal);

        if (_planFields.Length == 0 && !LiteralRoot)
        {
            _plan = ConfigurationMountPlan.Empty;
        }
        else if (replacement || _observedContinuousMode != ContinuousMode)
        {
            _discovery = new();
            _plan = _discovery.Start(Form, _planFields, LiteralRoot);
        }
        else if (reconcile)
        {
            _plan = ConfigurationMountPlan.Reconcile(Form, _planFields, LiteralRoot, previous);
        }

        _planRevision = Form.InputRevision;
        bool topologyChanged = !ReferenceEquals(previous, _plan);
        _settlementPending |= topologyChanged;
        bool focusChanged = FocusField is not null &&
            (_observedFocusField != FocusField || _observedFocusGeneration != FocusGeneration);
        bool scalarSameTopology = ReferenceEquals(_observedForm, Form) &&
            !replacement && _observedContinuousMode == ContinuousMode && !discovering &&
            PendingCount == 0 && !viewChanged && !focusChanged &&
            _observedCancellation == MountCancellation && _renderAcknowledged &&
            _planFields.Length == 0;
        if (scalarSameTopology)
        {
            _observedProjection = Form.Projection;
            _observedRevision = Form.InputRevision;
        }
        bool superseded = !scalarSameTopology && (replacement || _observedCancellation != MountCancellation ||
            _observedRevision != Form.InputRevision || viewChanged || topologyChanged || focusChanged);
        if (superseded)
        {
            ++_mountGeneration;
            _mountWait.Cancel();
            _mountWait.Dispose();
            _mountWait = CancellationTokenSource.CreateLinkedTokenSource(MountCancellation);
            _observedForm = Form;
            _observedProjection = Form.Projection;
            _observedLiteralRoot = LiteralRoot;
            _observedContinuousMode = ContinuousMode;
            _observedCancellation = MountCancellation;
            _observedRevision = Form.InputRevision;
            _renderAcknowledged = false;
        }
        _observedFocusField = FocusField;
        _observedFocusGeneration = FocusGeneration;
        if (_planFields.Length == 0 && !LiteralRoot)
        {
            _discovery?.Clear();
            _discovery = null;
            _settlementPending = focusChanged;
            _renderAcknowledged = !focusChanged;
        }
        else if (replacement || _observedContinuousMode != ContinuousMode || (topologyChanged && !discovering) || focusChanged)
        {
            _firstBoundaryPending = true;
            MountBatch(FirstBatchSize);
        }
    }

    private void MountBatch(int maximumFields)
    {
        if (_planFields.Length == 0 && !LiteralRoot) return;
        DefinitionPath? priority = FocusField is null ? null : LiteralRoot
            ? DefinitionPath.FromSegments(FocusPath)
            : _planIndicesByFieldId.ContainsKey(FocusField)
                ? DefinitionPath.FromSegments(new[] { Form.GetFieldEditor(FocusField).Input.Definition.Name }.Concat(FocusPath.Skip(1)))
                : null;
        if (_discovery is { } discovery)
        {
            _plan = discovery.Advance(Form, _planFields, LiteralRoot, _plan, maximumFields, priority);
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
        _planFields = [];
        _planIndicesByFieldId.Clear();
        _observedForm = null;
        _observedProjection = null;
        if (_module is not null)
        {
            IJSObjectReference module = await _module;
            await module.DisposeAsync();
        }
        GC.SuppressFinalize(this);
    }
}
