namespace Configurator.Browser.Components.Configuration;

using Configurator.Core.Definitions;
using Configurator.Presentation;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

public partial class ConfigurationSectionFields
{
    [Inject] private IJSRuntime Javascript { get; set; } = null!;
    [Parameter, EditorRequired] public DefinitionFormModel Form { get; set; } = null!;
    [Parameter, EditorRequired] public IReadOnlyList<SetupFieldState> Fields { get; set; } = [];
    [Parameter] public CancellationToken MountCancellation { get; set; }
    [Parameter] public DefinitionValidationResult? Validation { get; set; }
    [Parameter] public string? FocusField { get; set; }
    [Parameter] public long FocusGeneration { get; set; }
    [Parameter] public IReadOnlyList<string> FocusPath { get; set; } = [];

    private const int FirstBatchSize = 6;
    private const int FollowingBatchSize = 10;
    private readonly HashSet<string> _mounted = new(StringComparer.Ordinal);
    private readonly HashSet<string> _mountable = new(StringComparer.Ordinal);
    private SetupFieldState[] _activeFields = [];
    private DefinitionFormModel? _observedForm;
    private CancellationToken _observedCancellation;
    private Task<IJSObjectReference>? _module;
    private long _mountGeneration;
    private long? _scheduledGeneration;
    private bool _disposed;

    private int PendingCount => _mountable.Count - _mounted.Count;
    private bool Busy => PendingCount > 0;

    protected override void OnParametersSet()
    {
        bool replacement = !ReferenceEquals(_observedForm, Form);
        bool superseded = replacement || _observedCancellation != MountCancellation;
        if (superseded)
        {
            ++_mountGeneration;
            _observedForm = Form;
            _observedCancellation = MountCancellation;
        }
        if (replacement) _mounted.Clear();

        _activeFields = Fields.Where(state => state.IsActive).ToArray();
        var mountable = _activeFields.Where(state => Form.IsEditorVisible([state.Id]) && Form.CanEdit([state.Id]))
            .Select(state => state.Id).ToHashSet(StringComparer.Ordinal);
        bool visibilityChanged = !_mountable.SetEquals(mountable);
        _mountable.Clear();
        _mountable.UnionWith(mountable);
        _mounted.IntersectWith(_mountable);

        // Retain controls already mounted in this section. Only new editable
        // roots are staged; collapsed/denied roots keep their recovery actions.
        if (superseded && visibilityChanged && PendingCount > 0) MountBatch(FirstBatchSize);
        else if (FocusField is { } focus && _mountable.Contains(focus)) _mounted.Add(focus);
    }

    private void MountBatch(int maximumFields)
    {
        int remaining = maximumFields;
        if (FocusField is { } focus && _mountable.Contains(focus) && _mounted.Add(focus))
            --remaining;
        foreach (var state in _activeFields)
        {
            if (remaining == 0) break;
            if (_mountable.Contains(state.Id) && _mounted.Add(state.Id)) --remaining;
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_disposed || !Busy || _scheduledGeneration == _mountGeneration) return;
        long generation = _mountGeneration;
        DefinitionFormModel form = Form;
        CancellationToken cancellation = MountCancellation;
        _scheduledGeneration = generation;
        _module ??= Javascript.InvokeAsync<IJSObjectReference>("import", "./configuration-rendering.js").AsTask();
        IJSObjectReference module = await _module;
        if (!IsCurrent()) return;
        await module.InvokeVoidAsync("nextFrame");
        if (!IsCurrent()) return;

        MountBatch(FollowingBatchSize);
        _scheduledGeneration = null;
        StateHasChanged();

        bool IsCurrent() => !_disposed && !cancellation.IsCancellationRequested
            && generation == _mountGeneration && ReferenceEquals(form, Form);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        ++_mountGeneration;
        _mounted.Clear();
        _mountable.Clear();
        _activeFields = [];
        _observedForm = null;
        if (_module is not null)
        {
            IJSObjectReference module = await _module;
            await module.DisposeAsync();
        }
        GC.SuppressFinalize(this);
    }
}
