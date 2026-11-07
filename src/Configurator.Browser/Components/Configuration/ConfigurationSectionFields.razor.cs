namespace Configurator.Browser.Components.Configuration;

using System.Globalization;
using Configurator.Core.Definitions;
using Configurator.Presentation;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

public partial class ConfigurationSectionFields
{
    [Inject] private IJSRuntime Javascript { get; set; } = null!;
    [Parameter, EditorRequired] public DefinitionFormModel Form { get; set; } = null!;
    [Parameter] public IReadOnlyList<FieldState> Fields { get; set; } = [];
    [Parameter] public bool LiteralRoot { get; set; }
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
    private DefinitionFormModel? _observedForm;
    private DefinitionFormProjection? _observedProjection;
    private bool _observedLiteralRoot;
    private CancellationToken _observedCancellation;
    private CancellationTokenSource _mountWait = new();
    private long _observedRevision = -1;
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
        bool viewChanged = !ReferenceEquals(_observedProjection, Form.Projection) ||
            _observedLiteralRoot != LiteralRoot || !_activeFields.Select(field => field.Id).SequenceEqual(activeFields.Select(field => field.Id));
        bool discovering = Discovering;
        ConfigurationMountPlan previous = replacement ? ConfigurationMountPlan.Empty : _plan;
        bool reconcile = replacement || viewChanged || _observedRevision != Form.InputRevision ||
            _observedCancellation != MountCancellation;
        // Retire private routes before borrowing any changed current state. The
        // partial published plan supplies survivor identities to full reconciliation.
        if (reconcile)
        {
            _discovery?.Clear();
            _discovery = null;
        }
        _activeFields = activeFields;
        if (replacement)
        {
            _discovery = new();
            _plan = _discovery.Start(Form, _activeFields, LiteralRoot);
        }
        else if (reconcile)
            _plan = ConfigurationMountPlan.Reconcile(Form, _activeFields, LiteralRoot, previous);
        _planRevision = Form.InputRevision;
        bool topologyChanged = !ReferenceEquals(previous, _plan);
        _settlementPending |= topologyChanged;
        bool focusChanged = FocusField is not null &&
            (_observedFocusField != FocusField || _observedFocusGeneration != FocusGeneration);
        bool superseded = replacement || _observedCancellation != MountCancellation ||
            _observedRevision != Form.InputRevision || viewChanged || topologyChanged || focusChanged;
        if (superseded)
        {
            ++_mountGeneration;
            _mountWait.Cancel();
            _mountWait.Dispose();
            _mountWait = CancellationTokenSource.CreateLinkedTokenSource(MountCancellation);
            _observedForm = Form;
            _observedProjection = Form.Projection;
            _observedLiteralRoot = LiteralRoot;
            _observedCancellation = MountCancellation;
            _observedRevision = Form.InputRevision;
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

    private void MountBatch(int maximumFields)
    {
        DefinitionPath? priority = FocusField is null ? null : LiteralRoot
            ? DefinitionPath.FromSegments(FocusPath)
            : DefinitionPath.FromSegments(new[] { Form.GetFieldEditor(FocusField).Input.Definition.Name }.Concat(FocusPath.Skip(1)));
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
                // Child after-render actions can focus a surviving control after this
                // parent's render, including removals that admit no new descendants.
                await module.InvokeVoidAsync("nextTask").AsTask().WaitAsync(cancellation);
                if (!IsCurrentMount(generation, cancellation)) return;
                // Reveal current focus only after leaving that renderer turn.
                await module.InvokeVoidAsync("revealFocusedControl", _mountElement, generation.ToString(CultureInfo.InvariantCulture))
                    .AsTask().WaitAsync(cancellation);
                if (!IsCurrentMount(generation, cancellation)) return;
                _settlementPending = false;
                _renderAcknowledged = true;
                _scheduledGeneration = null;
                StateHasChanged();
                return;
            }
            // Paint the initial controls once, then yield later bounded increments
            // to browser tasks without imposing one display frame per ten nodes.
            await module.InvokeVoidAsync(_firstBoundaryPending ? "nextFrame" : "nextTask").AsTask().WaitAsync(cancellation);
            if (!IsCurrentMount(generation, cancellation)) return;
            _firstBoundaryPending = false;
            MountBatch(FollowingBatchSize);
            _scheduledGeneration = null;
            StateHasChanged();
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // Supersession retires the continuation even if the browser frame is still held.
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
