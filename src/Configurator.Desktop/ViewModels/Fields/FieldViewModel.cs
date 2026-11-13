namespace Configurator.Desktop.ViewModels.Fields;

using System.Globalization;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Configurator.Core.Definitions;
using Configurator.Desktop.Resources;
using Configurator.Desktop.Services;
using Configurator.Presentation;

/// <summary>
/// Projects native binding and focus state. The factory coordinates refresh; the form owns all answers and drafts.
/// </summary>
public abstract class FieldViewModel : ObservableObject, IDisposable
{
    private readonly bool _declaresProtectedChain;
    private readonly bool _requiresMasking;
    private string _text = string.Empty;
    private bool _provided;
    private bool _hasError;
    private bool _isVisible = true;
    private bool _isRequired;
    private bool _editing;
    private int _focusRequestVersion;
    private int _supplyFocusRequestVersion;
    private long _focusInputRevision = -1;
    private string? _focusIdentity;
    private bool _wasEdited;
    private bool _feedbackRequested;
    private DefinitionFormModel? _form;
    private DefinitionFieldInput? _input;
    private FieldViewModel? _root;
    private DefinitionEditorPresentation? _presentation;
    private readonly string _name;
    private string _id;

    protected FieldViewModel(DefinitionNode schema, string name, bool required,
        FieldViewModel? parent, bool listItem, bool inheritedMasking = false)
    {
        Schema = schema;
        _name = name;
        _id = name;
        Parent = parent;
        IsListItem = listItem;
        _declaresProtectedChain = inheritedMasking || parent?._declaresProtectedChain == true ||
            DefinitionSensitivityPolicy.SensitivityOf(schema) != FieldSensitivity.Public;
        _requiresMasking = _declaresProtectedChain || HasNonPublicSchema(schema);
        _isRequired = required;
        ClearCommand = new RelayCommand(Clear, () => !IsDisposed && CanClear);
        BlurCommand = new RelayCommand(() =>
        {
            if (!_wasEdited || IsDisposed) return;
            _feedbackRequested = true;
            OnPropertyChanged(nameof(ShowError));
        });
        SupplyCommand = new RelayCommand(Supply, () => !IsDisposed && CanEdit && IsContainer);
        ApplySuggestionCommand = new RelayCommand(ApplySuggestion, () => !IsDisposed && HasSuggestion);
        ResetCommand = new RelayCommand(() => { if (!IsDisposed) { Form.ResetValue(FullPath()); RequestFocus(); } }, () => !IsDisposed && Validation?.CanReset == true);
        SetNullCommand = new RelayCommand(() => { if (!IsDisposed) { Form.SetNull(FullPath()); RequestFocus(); } }, () => !IsDisposed && CanSetNull);
        RemoveCommand = new RelayCommand(() =>
        {
            if (Parent is ListFieldViewModel list) list.RemoveItemCommand.Execute(this);
            else if (Parent is MapFieldViewModel map) map.RemoveEntryCommand.Execute(this);
        }, () => !IsDisposed && (Parent is ListFieldViewModel list && list.RemoveItemCommand.CanExecute(this) ||
            Parent is MapFieldViewModel map && map.RemoveEntryCommand.CanExecute(this)));
    }

    protected DefinitionNode Schema { get; }
    protected FieldViewModel? Parent { get; }
    public bool IsListItem { get; }
    /// <summary>Opaque automation identity for this retained editor, independent of private paths and answers.</summary>
    public string AutomationId { get; } = "field-" + Guid.NewGuid().ToString("N");
    protected bool IsDisposed { get; private set; }
    protected bool IsProvided => _provided;
    protected FieldViewModel Root => _root ?? this;
    protected DefinitionFormModel Form => Root._form!;
    public DesktopStrings Strings => new(Root._form?.Locale);
    internal ISecretClipboardService? Clipboard { get; set; }
    protected ISecretClipboardService? SecretClipboard => Root.Clipboard;
    internal virtual IEnumerable<FieldViewModel> Descendants => [];
    internal string EditorIdentity { get; set; } = string.Empty;
    internal DefinitionPath EditorPath => FullPath();
    internal FieldViewModel RootEditor => Root;
    internal event Action<FieldViewModel>? FocusRequested;
    internal bool IsFocusRequestCurrent => !IsDisposed && Root._form is { } form &&
        form.InputRevision == _focusInputRevision && form.GetEditorIdentity(FullPath()) == _focusIdentity;
    public string Id => _id;
    public string Title
    {
        get
        {
            if (Parent is RootFieldViewModel) return Parent.Title;
            if (Parent is MapFieldViewModel map && !map.Schema.Properties.ContainsKey(Id))
            {
                string key = string.IsNullOrWhiteSpace(_name) || _name.Any(char.IsControl) ? JsonSerializer.Serialize(_name) : _name;
                return key.Length <= 256 ? key : key[..256];
            }
            if (IsListItem && ReadMetadata("title").Length == 0) return Strings.Format("ItemTitle", Parent!.Title);
            if (_presentation?.Title is { Length: > 0 } localized) return localized;
            if (Parent is null && _input is not null) return _input.Definition.Title;
            if (ReadMetadata("title") is { Length: > 0 } title) return title;
            if (IsListItem) return Strings.Format("ItemTitle", Parent!.Title);
            string inferred = string.IsNullOrWhiteSpace(_name) ? Strings["Setting"] : _name;
            if (inferred.Any(char.IsControl)) inferred = JsonSerializer.Serialize(inferred);
            return inferred.Length <= 256 ? inferred : inferred[..256];
        }
    }
    public string Name => Title;
    public string ClearLabel => Strings.ClearLabel;
    public string ClearName => Strings.ClearName(Title);
    public string ClearHelp => Strings.ClearHelp;
    public string SupplyLabel => Strings.SupplyLabel;
    public string SupplyName => Strings.SupplyName(Title);
    public string SupplyHelp => Strings.SupplyHelp;
    public bool IsContainer => Schema.Type is DefinitionNodeType.Mapping or DefinitionNodeType.Sequence;
    public string SuggestionLabel => Strings.SuggestionLabel;
    public string SuggestionName => Strings.SuggestionName(Title);
    public string ResetLabel => Strings.ResetLabel;
    public string ResetName => Strings.ResetName(Title);
    public string ResetHelp => Strings.ResetHelp;
    public string NullLabel => Strings.NullLabel;
    public string NullName => Strings.NullName(Title);
    private DefinitionFieldValidation? Validation => Root._form?.GetFieldValidation(FullPath());
    public bool CanClear => IsListItem && Schema.Type == DefinitionNodeType.Text
        ? CanEdit : Validation?.CanOmit == true;
    public bool CanReset => Validation?.CanReset == true;
    public bool CanEdit => Validation?.CanEdit == true;
    public bool CanSetNull => Validation?.CanSetNull == true;
    public bool CanRemove => !IsDisposed && (Parent is ListFieldViewModel list && list.RemoveItemCommand.CanExecute(this) ||
        Parent is MapFieldViewModel map && map.RemoveEntryCommand.CanExecute(this));
    public bool HasFieldActions => CanClear || HasSuggestion || CanReset || CanSetNull || CanRemove;
    public string RemoveLabel => Parent is MapFieldViewModel ? Strings["RemoveEntry"] : Strings["RemoveItem"];
    public string RemoveName => Parent is MapFieldViewModel
        ? $"{Strings["RemoveEntry"].Replace("_", "", StringComparison.Ordinal)}: {Title}"
        : RemoveItemName;
    public string RemoveItemName => Strings.Format("RemoveItemName", Parent?.Title ?? Title);
    public string Help => _presentation?.Description ?? ReadMetadata("description");
    public string HelpText
    {
        get
        {
            if (_presentation is not { } display) return Help is { Length: > 0 } help ? help : Title;
            string text = string.Join(Environment.NewLine, new[] { Help, display.Instructions, display.Units }
                .Concat(display.Choices.Select(choice => choice.Explanation))
                .Concat(display.Links.Select(link => link.Title + ": " + link.Url)).Where(item => !string.IsNullOrWhiteSpace(item)));
            return text.Length > 0 ? text : Title;
        }
    }
    public string Format => ReadMetadata("format");
    public string ConstraintsSummary => string.Join(" | ", Validation?.Bounds.Select(bound =>
        PresentationStrings.Format("Field" + bound.Kind, Form.Locale, bound.Value)) ?? []);
    public bool RequiresMasking => Validation?.RequiresMasking ?? _requiresMasking;
    public virtual char PasswordChar => RequiresMasking ? '●' : '\0';
    public bool IsPublic => !RequiresMasking;
    public bool HasProtectedDescendants => Validation?.HasProtectedDescendants ?? RequiresMasking;
    public bool HasSuggestion => Validation?.CanApplySuggestion == true;
    public bool HasError
    {
        get => _hasError;
        private set { if (SetProperty(ref _hasError, value)) OnPropertyChanged(nameof(ShowError)); }
    }
    public bool ShowError => HasError && _feedbackRequested;
    public bool IsVisible { get => _isVisible; private set => SetProperty(ref _isVisible, value); }
    public bool IsRequired { get => _isRequired; private set => SetProperty(ref _isRequired, value); }
    public virtual string ErrorText => Validation is { Reasons.Count: > 0 } facts
        ? PresentationStrings.FieldCorrection(facts, Form.Locale)
        : Strings[HasError ? "InvalidValue" : IsRequired ? "Required" : "Optional"];
    public int FocusRequestVersion => _focusRequestVersion;
    public int SupplyFocusRequestVersion => _supplyFocusRequestVersion;
    public IRelayCommand ClearCommand { get; }
    public IRelayCommand SupplyCommand { get; }
    public IRelayCommand ApplySuggestionCommand { get; }
    public IRelayCommand ResetCommand { get; }
    public IRelayCommand SetNullCommand { get; }
    public IRelayCommand RemoveCommand { get; }
    public IRelayCommand BlurCommand { get; }

    /// <summary>Native draft text; every deliberate edit goes to the shared literal path.</summary>
    public string Text
    {
        get => _text;
        set
        {
            value ??= string.Empty;
            if (IsDisposed || _editing || Root._form is null || !CanEdit) return;
            if (value != _text) MarkEdited();
            _editing = true;
            try
            {
                SetProperty(ref _text, value);
                _provided = true;
                NotifyValue();
                Form.EditValue(FullPath(), value);
            }
            finally { _editing = false; }
        }
    }

    internal void Attach(DefinitionFormModel form, DefinitionFieldInput input)
    {
        _root = this;
        _form = form;
        _input = input;
        _id = input.Definition.Id;
        AdoptRoot(this);
    }

    internal void AttachRoot(DefinitionFormModel form)
    {
        _root = this;
        _form = form;
        AdoptRoot(this);
    }

    internal void AdoptRoot(FieldViewModel root)
    {
        _root = root;
        foreach (var child in Descendants) child.AdoptRoot(root);
    }

    internal virtual void Load(JsonElement? value)
    {
        if (_editing) return;
        _provided = value is not null;
        SetProperty(ref _text, value is { } supplied ? Display(supplied) : string.Empty, nameof(Text));
        NotifyValue();
    }

    protected virtual void NotifyValue() { }
    internal void MarkEdited() => _wasEdited = true;
    internal void RequestValidationFeedback()
    {
        if (IsDisposed) return;
        _feedbackRequested = true;
        OnPropertyChanged(nameof(ShowError));
        foreach (FieldViewModel child in Descendants) child.RequestValidationFeedback();
    }
    /// <summary>Signals the View focus bridge; structured nodes route to the first visible descendant.</summary>
    public virtual void RequestFocus()
    {
        if (IsDisposed) return;
        RevealForFocus();
        if ((Descendants.FirstOrDefault(child => child.IsVisible && child.HasError) ??
            Descendants.FirstOrDefault(child => child.IsVisible)) is { } first)
        {
            first.RequestFocus();
            return;
        }
        if (IsContainer && (this is not ListFieldViewModel list || !list.AddItemCommand.CanExecute(null)))
        {
            CaptureFocusRequest();
            _supplyFocusRequestVersion++;
            OnPropertyChanged(nameof(SupplyFocusRequestVersion));
        }
        else SignalFocus();
    }

    protected void SignalFocus()
    {
        if (IsDisposed) return;
        RevealForFocus();
        CaptureFocusRequest();
        _focusRequestVersion++;
        OnPropertyChanged(nameof(FocusRequestVersion));
    }

    /// <summary>Reveals only the collection ancestors and pages containing this existing editor.</summary>
    internal void RevealForFocus()
    {
        FieldViewModel child = this;
        for (FieldViewModel? parent = Parent; parent is not null; child = parent, parent = parent.Parent)
        {
            if (parent is ListFieldViewModel list) list.RevealChild(child);
            else if (parent is MapFieldViewModel map) map.RevealChild(child);
            else if (parent is ObjectFieldViewModel mapping) mapping.IsExpanded = true;
            else if (parent is VariantFieldViewModel variant) variant.IsExpanded = true;
        }
    }

    private void CaptureFocusRequest()
    {
        _focusInputRevision = Form.InputRevision;
        _focusIdentity = Form.GetEditorIdentity(FullPath());
        Root.FocusRequested?.Invoke(this);
    }

    /// <summary>Projects one settled root and its literal diagnostics without replacing surviving editors.</summary>
    internal void Refresh(FieldState state)
    {
        if (IsDisposed || _input is null) return;
        // A malformed root denies stale collection projection, but its own raw correction text remains available.
        bool valid = _input.TryValue(out JsonElement? value);
        if (!valid) value = null;
        RefreshNode(FullPath(), value, state.IsActive);
        IsRequired = state.IsRequired;
        HasError |= state.IsActive && state.HasError;
        if (!_editing)
        {
            SetProperty(ref _text, _input.Text, nameof(Text));
            NotifyValue();
        }
    }

    internal void RefreshRoot()
    {
        if (IsDisposed) return;
        RefreshNode(DefinitionPath.FromSegments([]),
            JsonSerializer.SerializeToElement(Form.Snapshot().Values), true);
    }

    private void RefreshNode(DefinitionPath path, JsonElement? value, bool active)
    {
        _presentation = Form.GetEditorPresentation(path);
        if (Form.GetEditorState(path) is { } nodeState)
        {
            IsRequired = nodeState.IsRequired;
            active = nodeState.IsActive;
        }
        Load(value);
        IsVisible = Form.IsEditorVisible(path);
        HasError = active && (Form.HasInputError(path) || Form.GetFieldValidation(path).Reasons.Count > 0);
        if (!IsContainer && !_editing && Form.ReadDraftText(path) is { } draft)
        {
            SetProperty(ref _text, draft, nameof(Text));
            _provided = true;
            NotifyValue();
        }
        foreach (var child in Descendants)
        {
            int row = this is ListFieldViewModel list ? list.Items.IndexOf(child) : -1;
            DefinitionPath childPath = this is RootFieldViewModel ? path :
                DefinitionPath.FromSegments([.. path.Segments, row >= 0
                    ? row.ToString(CultureInfo.InvariantCulture) : child.Id]);
            JsonElement? childValue = this is RootFieldViewModel ? value :
                value is { ValueKind: JsonValueKind.Object } mapping &&
                mapping.TryGetProperty(child.Id, out var property) ? property
                : value is { ValueKind: JsonValueKind.Array } items && row >= 0 ? items[row] : null;
            child.RefreshNode(childPath, childValue, active);
        }
        HasError |= active && Descendants.Any(child => child.IsVisible && child.HasError);
        OnPropertyChanged(nameof(ErrorText));
        OnPropertyChanged(nameof(Strings));
        OnPropertyChanged(nameof(ConstraintsSummary));
        OnPropertyChanged(nameof(ClearLabel));
        OnPropertyChanged(nameof(ClearHelp));
        OnPropertyChanged(nameof(SupplyLabel));
        OnPropertyChanged(nameof(SupplyHelp));
        OnPropertyChanged(nameof(SuggestionLabel));
        OnPropertyChanged(nameof(ResetLabel));
        OnPropertyChanged(nameof(ResetHelp));
        OnPropertyChanged(nameof(NullLabel));
        OnPropertyChanged(nameof(RequiresMasking));
        OnPropertyChanged(nameof(PasswordChar));
        OnPropertyChanged(nameof(IsPublic));
        OnPropertyChanged(nameof(HasProtectedDescendants));
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Help));
        OnPropertyChanged(nameof(HelpText));
        OnPropertyChanged(nameof(ClearName));
        OnPropertyChanged(nameof(SupplyName));
        OnPropertyChanged(nameof(SuggestionName));
        OnPropertyChanged(nameof(ResetName));
        OnPropertyChanged(nameof(NullName));
        OnPropertyChanged(nameof(RemoveLabel));
        OnPropertyChanged(nameof(RemoveName));
        OnPropertyChanged(nameof(RemoveItemName));
        OnPropertyChanged(nameof(HasSuggestion));
        OnPropertyChanged(nameof(CanClear));
        OnPropertyChanged(nameof(CanReset));
        OnPropertyChanged(nameof(CanSetNull));
        OnPropertyChanged(nameof(CanRemove));
        OnPropertyChanged(nameof(HasFieldActions));
        OnPropertyChanged(nameof(CanEdit));
        ClearCommand.NotifyCanExecuteChanged();
        SupplyCommand.NotifyCanExecuteChanged();
        ResetCommand.NotifyCanExecuteChanged();
        SetNullCommand.NotifyCanExecuteChanged();
        ApplySuggestionCommand.NotifyCanExecuteChanged();
        RemoveCommand.NotifyCanExecuteChanged();
        OnRefreshed();
    }

    protected virtual void OnRefreshed() { }

    protected DefinitionPath FullPath() => Parent is RootFieldViewModel
        ? DefinitionPath.FromSegments([])
        : Parent is null
            ? DefinitionPath.FromSegments(_input is null ? [] : [_input.Definition.Name])
            : DefinitionPath.FromSegments([.. Parent.FullPath().Segments, Parent is ListFieldViewModel list
                ? list.Items.IndexOf(this).ToString(CultureInfo.InvariantCulture) : Id]);

    private void Clear()
    {
        if (IsDisposed || !CanClear) return;
        if (IsListItem && Schema.Type == DefinitionNodeType.Text)
            Form.EditValue(FullPath(), "");
        else Form.SetValue(FullPath(), null);
        RequestFocus();
    }
    private void ApplySuggestion()
    {
        if (IsDisposed || !HasSuggestion) return;
        Form.ApplySuggestion(FullPath());
        RequestFocus();
    }
    private void Supply()
    {
        if (IsDisposed || !CanEdit) return;
        bool supplied = Schema.Type == DefinitionNodeType.Mapping ? Form.SupplyObject(FullPath()) : Form.SupplyList(FullPath());
        if (supplied) RequestFocus();
    }
    private string ReadMetadata(string name) => Schema.Keywords.TryGetProperty(name, out var value) ? value.GetString() ?? "" : "";
    private static bool HasNonPublicSchema(DefinitionNode schema) =>
        DefinitionSensitivityPolicy.SensitivityOf(schema) != FieldSensitivity.Public ||
        schema.Properties.Values.Any(HasNonPublicSchema) ||
        schema.Items is { } item && HasNonPublicSchema(item) ||
        schema.AdditionalProperties is { } additional && HasNonPublicSchema(additional) ||
        schema.Variants.Any(HasNonPublicSchema);
    internal static string Display(JsonElement value) => value.ValueKind == JsonValueKind.String ? value.GetString()! : value.GetRawText();

    public virtual void Dispose()
    {
        if (IsDisposed) return;
        IsDisposed = true;
        foreach (var child in Descendants) child.Dispose();
        _form = null;
        _input = null;
        _root = null;
        _text = string.Empty;
        FocusRequested = null;
        ClearCommand.NotifyCanExecuteChanged();
        SupplyCommand.NotifyCanExecuteChanged();
        ApplySuggestionCommand.NotifyCanExecuteChanged();
        ResetCommand.NotifyCanExecuteChanged();
        SetNullCommand.NotifyCanExecuteChanged();
        GC.SuppressFinalize(this);
    }
}
