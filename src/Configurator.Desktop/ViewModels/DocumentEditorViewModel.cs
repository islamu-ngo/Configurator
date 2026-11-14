namespace Configurator.Desktop.ViewModels;

using System.ComponentModel;
using System.Security.Cryptography;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Configurator.Core.Definitions;
using Configurator.Desktop.Messages;
using Configurator.Desktop.Resources;
using Configurator.Presentation;

/// <summary>Projected change line item shown prior to confirming document application.</summary>
public sealed record DocumentChangePreviewItem(
    DefinitionPath Path,
    string PathText,
    DefinitionDocumentChangeKind Kind,
    string KindText);

/// <summary>
/// Manages generation-fenced canonical JSON and dotenv document sessions with pre-apply diff preview.
/// </summary>
public sealed partial class DocumentEditorViewModel : ObservableObject, IDisposable
{
    private readonly FormWorkspaceViewModel _workspace;
    private readonly DesktopNavigationViewModel _navigation;
    private readonly IMessenger _messenger;
    private DefinitionFormModel? _form;
    private DefinitionDocumentSession? _session;
    private DefinitionDocumentParseResult? _activeParse;
    private long _currentGeneration;
    private long _previewGeneration;
    private long _previewInputRevision;
    private bool _updatingText;
    private bool _disposed;

    [ObservableProperty]
    private DefinitionDocumentRepresentation _representation = DefinitionDocumentRepresentation.Canonical;
    [ObservableProperty]
    private DefinitionDocumentApplyMode _applyMode = DefinitionDocumentApplyMode.Merge;
    [ObservableProperty]
    private string? _selectedEnvironmentArtifactId;
    [ObservableProperty]
    private IReadOnlyList<string> _environmentArtifacts = [];
    [ObservableProperty]
    private string _documentText = string.Empty;
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyPreviewCommand))]
    private bool _hasPreview;
    [ObservableProperty]
    private bool _isStale;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasErrorCode))]
    private string _lastErrorCode = string.Empty;
    [ObservableProperty]
    private IReadOnlyList<DocumentChangePreviewItem> _pendingChanges = [];
    [ObservableProperty]
    private int _setCount;
    [ObservableProperty]
    private int _resetCount;
    [ObservableProperty]
    private int _omitCount;
    [ObservableProperty]
    private IReadOnlyList<DefinitionDocumentDiagnostic> _diagnostics = [];

    public DocumentEditorViewModel(
        FormWorkspaceViewModel workspace,
        DesktopNavigationViewModel navigation,
        IMessenger messenger)
    {
        _workspace = workspace;
        _navigation = navigation;
        _messenger = messenger;
        PreviewChangesCommand = new RelayCommand(() => PreviewChanges(), () => CanPreview);
        ApplyPreviewCommand = new RelayCommand(() => ApplyPreview(), () => CanApply);
        _workspace.PropertyChanged += WorkspacePropertyChanged;
        _navigation.PropertyChanged += NavigationPropertyChanged;
        _messenger.Register<DocumentEditorViewModel, FormInputsChangedMessage>(this,
            static (editor, _) => editor.HandleExternalWorkspaceChange());
        _messenger.Register<DocumentEditorViewModel, WorkspaceInvalidatedMessage>(this,
            static (editor, message) => editor.HandleGenerationInvalidated(message.Generation));
        AttachForm(_workspace.Form);
    }

    public DesktopStrings Strings => _workspace.Strings;

    public IReadOnlyList<string> RepresentationOptions => HasEnvironmentArtifacts
        ? [Strings["DocumentCanonicalOption"], Strings["DocumentEnvironmentOption"]]
        : [Strings["DocumentCanonicalOption"]];

    public int RepresentationIndex
    {
        get => Representation == DefinitionDocumentRepresentation.Environment && HasEnvironmentArtifacts ? 1 : 0;
        set
        {
            DefinitionDocumentRepresentation next = value == 1 && HasEnvironmentArtifacts
                ? DefinitionDocumentRepresentation.Environment
                : DefinitionDocumentRepresentation.Canonical;
            Representation = next;
        }
    }

    public IReadOnlyList<string> ApplyModeOptions =>
    [
        Strings["DocumentMergeOption"],
        Strings["DocumentReplaceScopeOption"]
    ];

    public int ApplyModeIndex
    {
        get => ApplyMode == DefinitionDocumentApplyMode.ReplaceRepresentedScope ? 1 : 0;
        set
        {
            if (value is 0 or 1) ApplyMode = (DefinitionDocumentApplyMode)value;
        }
    }

    public bool HasEnvironmentArtifacts => EnvironmentArtifacts.Count > 0;
    public bool IsEnvironmentRepresentation => Representation == DefinitionDocumentRepresentation.Environment && HasEnvironmentArtifacts;
    public bool HasErrorCode => LastErrorCode.Length > 0 && !string.Equals(LastErrorCode, "document-applied", StringComparison.Ordinal);
    public bool HasDiagnostics => Diagnostics.Count > 0;
    public bool HasPendingChanges => PendingChanges.Count > 0;
    public string ChangesCountSummary => Strings.Format("DocumentChangesCounts", SetCount, ResetCount, OmitCount);
    public bool CanReload => !_disposed && _form is not null;
    public bool CanPreview => !_disposed && _form is not null && _session is not null;
    public bool CanApply => !_disposed && _activeParse?.Candidate is not null;
    public IRelayCommand PreviewChangesCommand { get; }
    public IRelayCommand ApplyPreviewCommand { get; }

    [RelayCommand]
    public void ReloadFromWorkspace()
    {
        if (_disposed || _form is null) return;
        OpenSessionFromForm();
    }

    public bool PreviewChanges()
    {
        if (_disposed || _form is null || _session is null)
        {
            LastErrorCode = "document-stale";
            IsStale = true;
            return false;
        }

        ClearPreview();
        IsStale = false;
        _previewGeneration = _currentGeneration;
        _previewInputRevision = _form.InputRevision;

        byte[] utf8 = Encoding.UTF8.GetBytes(DocumentText);
        DefinitionDocumentParseResult parsed;
        try
        {
            parsed = _session.Parse(utf8, ApplyMode);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(utf8);
        }

        Diagnostics = parsed.Diagnostics;
        OnPropertyChanged(nameof(HasDiagnostics));

        if (!parsed.Succeeded || parsed.Candidate is null)
        {
            LastErrorCode = parsed.Diagnostics.Count > 0 ? parsed.Diagnostics[0].Code : "document-parse-failed";
            parsed.Dispose();
            return false;
        }

        _activeParse = parsed;
        DocumentChangePreviewItem[] items = parsed.Candidate.Changes
            .Select(change => new DocumentChangePreviewItem(
                change.Path,
                change.Path.Segments.Count == 0 ? "/" : "/" + string.Join("/", change.Path.Segments),
                change.Kind,
                change.Kind.ToString()))
            .ToArray();
        PendingChanges = Array.AsReadOnly(items);
        SetCount = items.Count(item => item.Kind == DefinitionDocumentChangeKind.Set);
        ResetCount = items.Count(item => item.Kind == DefinitionDocumentChangeKind.Reset);
        OmitCount = items.Count(item => item.Kind == DefinitionDocumentChangeKind.Omit);
        HasPreview = true;
        LastErrorCode = string.Empty;
        OnPropertyChanged(nameof(HasPendingChanges));
        OnPropertyChanged(nameof(ChangesCountSummary));
        ApplyPreviewCommand.NotifyCanExecuteChanged();
        return true;
    }

    public Task<bool> PreviewChangesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(PreviewChanges());
    }

    public bool ApplyPreview()
    {
        if (_disposed || _form is null || _session is null || _activeParse?.Candidate is not { } candidate)
        {
            LastErrorCode = "document-stale";
            IsStale = true;
            ClearPreview();
            return false;
        }

        if (IsStale || _previewGeneration != _currentGeneration || _form.InputRevision != _previewInputRevision)
        {
            IsStale = true;
            LastErrorCode = "document-stale";
            ClearPreview();
            return false;
        }

        DefinitionDocumentApplyResult result = _session.Apply(candidate);
        ClearPreview();

        if (!result.Succeeded)
        {
            LastErrorCode = result.Code;
            IsStale = string.Equals(result.Code, "document-stale", StringComparison.Ordinal);
            return false;
        }

        OpenSessionFromForm();
        LastErrorCode = result.Code;
        _messenger.Send(new StatusChangedMessage(result.Code, _currentGeneration));
        return true;
    }

    partial void OnDocumentTextChanged(string value)
    {
        if (_updatingText || _disposed) return;
        _session?.InvalidateText();
        ClearPreview();
        IsStale = false;
        LastErrorCode = string.Empty;
    }

    partial void OnRepresentationChanged(DefinitionDocumentRepresentation value)
    {
        OnPropertyChanged(nameof(RepresentationIndex));
        OnPropertyChanged(nameof(IsEnvironmentRepresentation));
        if (!_disposed && _form is not null) OpenSessionFromForm();
    }

    partial void OnSelectedEnvironmentArtifactIdChanged(string? value)
    {
        if (!_disposed && _form is not null && Representation == DefinitionDocumentRepresentation.Environment)
            OpenSessionFromForm();
    }

    partial void OnApplyModeChanged(DefinitionDocumentApplyMode value)
    {
        OnPropertyChanged(nameof(ApplyModeIndex));
        ClearPreview();
    }

    private void HandleGenerationInvalidated(long generation)
    {
        _currentGeneration = generation;
        if (_activeParse is not null && _previewGeneration != generation)
            IsStale = true;
    }

    private void HandleExternalWorkspaceChange()
    {
        if (_disposed || _form is null) return;
        if (_activeParse is not null && _form.InputRevision != _previewInputRevision)
            IsStale = true;
    }

    private void AttachForm(DefinitionFormModel? next)
    {
        if (ReferenceEquals(_form, next)) return;
        if (_form is not null) _form.ViewChanged -= FormViewChanged;
        _form = next;
        ClearSession();

        if (_form is null)
        {
            EnvironmentArtifacts = [];
            SelectedEnvironmentArtifactId = null;
            _updatingText = true;
            try { DocumentText = string.Empty; }
            finally { _updatingText = false; }
            NotifyMetadataChanged();
            return;
        }

        _form.ViewChanged += FormViewChanged;
        RefreshEnvironmentArtifacts();
        OpenSessionFromForm();
        NotifyMetadataChanged();
    }

    private void RefreshEnvironmentArtifacts()
    {
        if (_form is null)
        {
            EnvironmentArtifacts = [];
            SelectedEnvironmentArtifactId = null;
            return;
        }
        string[] envIds = _form.Projection.EligibleArtifacts
            .Where(artifact => artifact.Format == DefinitionArtifactFormat.Dotenv)
            .Select(artifact => artifact.Id)
            .ToArray();
        EnvironmentArtifacts = Array.AsReadOnly(envIds);
        SelectedEnvironmentArtifactId = envIds.FirstOrDefault();
        if (Representation == DefinitionDocumentRepresentation.Environment && !HasEnvironmentArtifacts)
            Representation = DefinitionDocumentRepresentation.Canonical;
        OnPropertyChanged(nameof(HasEnvironmentArtifacts));
        OnPropertyChanged(nameof(RepresentationOptions));
        OnPropertyChanged(nameof(IsEnvironmentRepresentation));
    }

    private void OpenSessionFromForm()
    {
        ClearSession();
        if (_form is null) return;
        try
        {
            DefinitionDocumentRepresentation mode = Representation == DefinitionDocumentRepresentation.Environment &&
                                                    SelectedEnvironmentArtifactId is not null
                ? DefinitionDocumentRepresentation.Environment
                : DefinitionDocumentRepresentation.Canonical;
            string? artifactId = mode == DefinitionDocumentRepresentation.Environment
                ? SelectedEnvironmentArtifactId
                : null;
            _session = _form.OpenDocument(mode, artifactId);
            byte[] source = _session.ReadSource();
            try
            {
                _updatingText = true;
                DocumentText = Encoding.UTF8.GetString(source);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(source);
                _updatingText = false;
            }
            LastErrorCode = string.Empty;
            IsStale = false;
        }
        catch (InvalidOperationException exception)
        {
            LastErrorCode = exception.Message;
            _updatingText = true;
            try { DocumentText = string.Empty; }
            finally { _updatingText = false; }
        }
        NotifyCommands();
    }

    private void ClearPreview()
    {
        _activeParse?.Dispose();
        _activeParse = null;
        PendingChanges = [];
        SetCount = 0;
        ResetCount = 0;
        OmitCount = 0;
        HasPreview = false;
        OnPropertyChanged(nameof(HasPendingChanges));
        OnPropertyChanged(nameof(ChangesCountSummary));
        ApplyPreviewCommand.NotifyCanExecuteChanged();
    }

    private void ClearSession()
    {
        ClearPreview();
        _session?.Dispose();
        _session = null;
        Diagnostics = [];
        OnPropertyChanged(nameof(HasDiagnostics));
    }

    private void FormViewChanged(object? sender, EventArgs args) => RefreshEnvironmentArtifacts();

    private void WorkspacePropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (_disposed) return;
        if (args.PropertyName == nameof(FormWorkspaceViewModel.Form)) AttachForm(_workspace.Form);
        else if (args.PropertyName == nameof(FormWorkspaceViewModel.Strings)) NotifyMetadataChanged();
    }

    private void NavigationPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (_disposed) return;
        if (args.PropertyName == nameof(DesktopNavigationViewModel.ConfigureMode) &&
            _navigation.IsDocumentMode && _form is not null && !HasPreview)
        {
            OpenSessionFromForm();
        }
    }

    private void NotifyMetadataChanged()
    {
        OnPropertyChanged(nameof(Strings));
        OnPropertyChanged(nameof(RepresentationOptions));
        OnPropertyChanged(nameof(ApplyModeOptions));
        OnPropertyChanged(nameof(ChangesCountSummary));
        NotifyCommands();
    }

    private void NotifyCommands()
    {
        ReloadFromWorkspaceCommand.NotifyCanExecuteChanged();
        PreviewChangesCommand.NotifyCanExecuteChanged();
        ApplyPreviewCommand.NotifyCanExecuteChanged();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _workspace.PropertyChanged -= WorkspacePropertyChanged;
        _navigation.PropertyChanged -= NavigationPropertyChanged;
        if (_form is not null) _form.ViewChanged -= FormViewChanged;
        _messenger.UnregisterAll(this);
        ClearSession();
        _updatingText = true;
        try { DocumentText = string.Empty; }
        finally { _updatingText = false; }
    }
}
