namespace Configurator.Desktop.ViewModels;

using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Configurator.Core.Definitions;
using Configurator.Desktop.Resources;
using Configurator.Desktop.ViewModels.Fields;
using Configurator.Presentation;

/// <summary>
/// Metadata-only contextual inspector displaying schema rules, validation bounds, and output bindings.
/// Never reads or displays answer snapshot values or draft inputs.
/// </summary>
public sealed partial class DesktopInspectorViewModel : ObservableObject, IDisposable
{
    private readonly FormWorkspaceViewModel _workspace;
    private DefinitionFormModel? _form;
    private DefinitionPath? _inspectedPath;
    private bool _disposed;

    [ObservableProperty]
    private bool _isOpen;
    [ObservableProperty]
    private bool _hasSelection;
    [ObservableProperty]
    private string _title = string.Empty;
    [ObservableProperty]
    private string _pathDisplay = string.Empty;
    [ObservableProperty]
    private string _wireToken = string.Empty;
    [ObservableProperty]
    private string _typeDisplay = string.Empty;
    [ObservableProperty]
    private string _sensitivityBadge = string.Empty;
    [ObservableProperty]
    private string _description = string.Empty;
    [ObservableProperty]
    private string _instructions = string.Empty;
    [ObservableProperty]
    private string _units = string.Empty;
    [ObservableProperty]
    private string _defaultText = string.Empty;
    [ObservableProperty]
    private string _ifOmittedDisplay = string.Empty;
    [ObservableProperty]
    private string _constraintsSummary = string.Empty;
    [ObservableProperty]
    private bool _isDeprecated;
    [ObservableProperty]
    private string _deprecationGuidance = string.Empty;
    [ObservableProperty]
    private bool _isController;
    [ObservableProperty]
    private string _controllerImpact = string.Empty;
    [ObservableProperty]
    private string _controlledBySummary = string.Empty;
    [ObservableProperty]
    private string _outputBindingsSummary = string.Empty;
    [ObservableProperty]
    private IReadOnlyList<DefinitionGuidanceLink> _links = [];

    public DesktopInspectorViewModel(FormWorkspaceViewModel workspace)
    {
        _workspace = workspace;
        _workspace.PropertyChanged += WorkspacePropertyChanged;
        AttachForm(_workspace.Form);
    }

    public DesktopStrings Strings => _workspace.Strings;
    public bool HasWireToken => WireToken.Length > 0;
    public bool HasDescription => Description.Length > 0;
    public bool HasInstructions => Instructions.Length > 0;
    public bool HasUnits => Units.Length > 0;
    public bool HasDefaultText => DefaultText.Length > 0;
    public bool HasIfOmitted => IfOmittedDisplay.Length > 0;
    public bool HasConstraints => ConstraintsSummary.Length > 0;
    public bool HasControllerImpact => ControllerImpact.Length > 0;
    public bool HasControlledBy => ControlledBySummary.Length > 0;
    public bool HasOutputBindings => OutputBindingsSummary.Length > 0;
    public bool HasLinks => Links.Count > 0;

    [RelayCommand]
    public void Toggle()
    {
        if (_disposed) return;
        IsOpen = !IsOpen;
    }

    [RelayCommand]
    public void Close() => IsOpen = false;

    public void InspectField(FieldViewModel? field)
    {
        if (_disposed) return;
        InspectPath(field?.EditorPath);
    }

    public void InspectPath(DefinitionPath? path)
    {
        if (_disposed) return;
        _inspectedPath = path;
        Refresh();
    }

    public void Refresh()
    {
        if (_disposed || _form is null || _inspectedPath is null)
        {
            ClearSelection();
            return;
        }

        DefinitionEditorPresentation? presentation = _form.GetEditorPresentation(_inspectedPath);
        DefinitionFieldValidation validation = _form.GetFieldValidation(_inspectedPath);
        DefinitionDiscoveryEntry? metadata = _form.GetFieldMetadata(_inspectedPath);
        if (presentation is null && metadata is null)
        {
            ClearSelection();
            return;
        }

        HasSelection = true;
        Title = presentation?.Title ?? metadata?.Title ?? string.Empty;
        PathDisplay = _inspectedPath.Segments.Count == 0
            ? "/"
            : "/" + string.Join("/", _inspectedPath.Segments);
        bool masked = validation.RequiresMasking;
        WireToken = masked ? string.Empty : (presentation?.WireToken ?? string.Empty);

        DefinitionNodeType? nodeType = presentation?.Schema.Type ?? metadata?.Type;
        TypeDisplay = nodeType?.ToString() ?? string.Empty;

        FieldSensitivity sensitivity = metadata?.Sensitivity ?? (masked ? FieldSensitivity.Secret : FieldSensitivity.Public);
        SensitivityBadge = Strings[sensitivity == FieldSensitivity.Public
            ? "ExplorerSensitivityPublic"
            : "ExplorerSensitivityPrivate"];

        Description = presentation?.Description ?? metadata?.Help ?? string.Empty;
        Instructions = presentation?.Instructions ?? metadata?.Instructions ?? string.Empty;
        Units = presentation?.Units ?? string.Empty;
        DefaultText = masked ? string.Empty : (presentation?.DefaultText ?? string.Empty);
        IfOmittedDisplay = presentation?.IfOmittedDisplay ?? string.Empty;

        var bounds = validation.Bounds.Select(bound =>
            PresentationStrings.Format("Field" + bound.Kind, _form.Locale, bound.Value));
        string? format = presentation is not null &&
                         presentation.Schema.Keywords.TryGetProperty("format", out var formatEl) &&
                         formatEl.ValueKind == System.Text.Json.JsonValueKind.String
            ? formatEl.GetString()
            : null;
        string[] constraintParts = (format is not null ? new[] { format } : Array.Empty<string>())
            .Concat(bounds)
            .ToArray();
        ConstraintsSummary = string.Join(" | ", constraintParts);

        IsDeprecated = presentation?.IsDeprecated == true;
        DeprecationGuidance = presentation?.DeprecationGuidance ?? string.Empty;
        IsController = presentation?.IsController == true;
        ControllerImpact = presentation?.ControllerImpactSummary ?? string.Empty;
        ControlledBySummary = metadata is { ActiveControllerFields.Count: > 0 }
            ? string.Join(", ", metadata.ActiveControllerFields)
            : string.Empty;

        string[] artifacts = metadata?.OutputArtifactIds.ToArray() ?? [];
        string[] bindings = metadata?.BindingKeys.ToArray() ?? [];
        OutputBindingsSummary = artifacts.Length > 0 || bindings.Length > 0
            ? string.Join(" | ", new[]
            {
                artifacts.Length > 0 ? string.Join(", ", artifacts) : string.Empty,
                bindings.Length > 0 ? string.Join(", ", bindings) : string.Empty
            }.Where(s => s.Length > 0))
            : string.Empty;

        Links = presentation?.Links ?? [];
        NotifyDerivedProperties();
    }

    private void ClearSelection()
    {
        HasSelection = false;
        Title = string.Empty;
        PathDisplay = string.Empty;
        WireToken = string.Empty;
        TypeDisplay = string.Empty;
        SensitivityBadge = string.Empty;
        Description = string.Empty;
        Instructions = string.Empty;
        Units = string.Empty;
        DefaultText = string.Empty;
        IfOmittedDisplay = string.Empty;
        ConstraintsSummary = string.Empty;
        IsDeprecated = false;
        DeprecationGuidance = string.Empty;
        IsController = false;
        ControllerImpact = string.Empty;
        ControlledBySummary = string.Empty;
        OutputBindingsSummary = string.Empty;
        Links = [];
        NotifyDerivedProperties();
    }

    private void NotifyDerivedProperties()
    {
        OnPropertyChanged(nameof(HasWireToken));
        OnPropertyChanged(nameof(HasDescription));
        OnPropertyChanged(nameof(HasInstructions));
        OnPropertyChanged(nameof(HasUnits));
        OnPropertyChanged(nameof(HasDefaultText));
        OnPropertyChanged(nameof(HasIfOmitted));
        OnPropertyChanged(nameof(HasConstraints));
        OnPropertyChanged(nameof(HasControllerImpact));
        OnPropertyChanged(nameof(HasControlledBy));
        OnPropertyChanged(nameof(HasOutputBindings));
        OnPropertyChanged(nameof(HasLinks));
    }

    private void AttachForm(DefinitionFormModel? next)
    {
        if (ReferenceEquals(_form, next)) return;
        if (_form is not null) _form.ViewChanged -= FormViewChanged;
        _form = next;
        _inspectedPath = null;
        if (_form is not null) _form.ViewChanged += FormViewChanged;
        Refresh();
    }

    private void WorkspacePropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (_disposed) return;
        if (args.PropertyName == nameof(FormWorkspaceViewModel.Form)) AttachForm(_workspace.Form);
        else if (args.PropertyName is nameof(FormWorkspaceViewModel.Strings) or nameof(FormWorkspaceViewModel.NamingMode))
        {
            OnPropertyChanged(nameof(Strings));
            Refresh();
        }
    }

    private void FormViewChanged(object? sender, EventArgs args) => Refresh();

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _workspace.PropertyChanged -= WorkspacePropertyChanged;
        if (_form is not null) _form.ViewChanged -= FormViewChanged;
    }
}
