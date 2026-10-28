namespace Configurator.Desktop.ViewModels.Fields;

using System.Globalization;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Configurator.Core.Definitions;
using Configurator.Desktop.Resources;
using Configurator.Presentation;

/// <summary>
/// Projects native binding and focus state. The factory coordinates refresh; the form owns all answers and drafts.
/// </summary>
public abstract class FieldViewModel : ObservableObject, IDisposable
{
    private static readonly string[] ConstraintNames = ["minimum", "maximum", "minLength", "maxLength", "minItems", "maxItems"];
    private string _text = string.Empty;
    private bool _provided;
    private bool _hasError;
    private bool _isVisible = true;
    private bool _isRequired;
    private bool _editing;
    private int _focusRequestVersion;
    private int _supplyFocusRequestVersion;
    private DefinitionFormModel? _form;
    private DefinitionFieldInput? _input;
    private FieldViewModel? _root;
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
        RequiresMasking = inheritedMasking || parent?.RequiresMasking == true || HasNonPublicSchema(schema);
        _isRequired = required;
        ClearCommand = new RelayCommand(Clear, () => !IsDisposed);
        SupplyCommand = new RelayCommand(Supply, () => !IsDisposed && IsContainer);
        UseRecommendedCommand = new RelayCommand(UseRecommended, () => !IsDisposed && HasRecommended);
        RemoveCommand = new RelayCommand(() =>
        {
            if (Parent is ListFieldViewModel list) list.RemoveItemCommand.Execute(this);
        }, () => !IsDisposed && Parent is ListFieldViewModel list && list.RemoveItemCommand.CanExecute(this));
    }

    protected DefinitionNode Schema { get; }
    protected FieldViewModel? Parent { get; }
    public bool IsListItem { get; }
    protected bool IsDisposed { get; private set; }
    protected bool IsProvided => _provided;
    protected FieldViewModel Root => _root ?? this;
    protected DefinitionFormModel Form => Root._form!;
    internal virtual IEnumerable<FieldViewModel> Descendants => [];
    internal string EditorIdentity { get; set; } = string.Empty;
    public string Id => _id;
    public string Title
    {
        get
        {
            if (Parent is null && _input is not null) return _input.Definition.Title;
            if (ReadMetadata("title") is { Length: > 0 } title) return title;
            if (IsListItem) return $"{Parent!.Title} item";
            string inferred = string.IsNullOrWhiteSpace(_name) ? "Setting" : _name;
            if (inferred.Any(char.IsControl)) inferred = JsonSerializer.Serialize(inferred);
            return inferred.Length <= 256 ? inferred : inferred[..256];
        }
    }
    public string Name => Title;
    public string ClearLabel => DesktopActionText.ClearLabel;
    public string ClearName => DesktopActionText.ClearName(Title);
    public string ClearHelp => DesktopActionText.ClearHelp;
    public string SupplyLabel => DesktopActionText.SupplyLabel;
    public string SupplyName => DesktopActionText.SupplyName(Title);
    public string SupplyHelp => DesktopActionText.SupplyHelp;
    public bool IsContainer => this is ObjectFieldViewModel or ListFieldViewModel;
    public string RecommendedName => $"Use recommended value for {Title}";
    public string RemoveItemName => $"Remove item from {Parent?.Title ?? Title}";
    public string Help => ReadMetadata("description");
    public string HelpText => Help is { Length: > 0 } help ? help : Title;
    public string Group => string.Empty;
    public string Consequence => string.Empty;
    public string Format => ReadMetadata("format");
    public string ConstraintsSummary => string.Join(" | ", ConstraintNames
        .Where(name => Schema.Keywords.TryGetProperty(name, out _))
        .Select(name => $"{name}: {Schema.Keywords.GetProperty(name).GetRawText()}"));
    public bool RequiresMasking { get; }
    public char PasswordChar => RequiresMasking ? '●' : '\0';
    public bool IsPublic => !RequiresMasking;
    public bool HasProtectedDescendants => RequiresMasking || Descendants.Any(child => child.HasProtectedDescendants);
    public bool HasRecommended => !HasProtectedDescendants && TryExtensionValue("suggestedValue", out _);
    public bool HasError { get => _hasError; private set => SetProperty(ref _hasError, value); }
    public bool IsVisible { get => _isVisible; private set => SetProperty(ref _isVisible, value); }
    public bool IsRequired { get => _isRequired; private set => SetProperty(ref _isRequired, value); }
    public string ErrorText => HasError ? "A valid value is required. Check the declared type and constraints."
        : IsRequired ? "Required" : "Optional";
    public int FocusRequestVersion => _focusRequestVersion;
    public int SupplyFocusRequestVersion => _supplyFocusRequestVersion;
    public IRelayCommand ClearCommand { get; }
    public IRelayCommand SupplyCommand { get; }
    public IRelayCommand UseRecommendedCommand { get; }
    public IRelayCommand RemoveCommand { get; }

    /// <summary>Native draft text; every deliberate edit goes to the shared literal path.</summary>
    public string Text
    {
        get => _text;
        set
        {
            value ??= string.Empty;
            if (IsDisposed || _editing || _text == value || Root._form is null) return;
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
    /// <summary>Signals the View focus bridge; structured nodes route to the first visible descendant.</summary>
    public void RequestFocus()
    {
        if (IsDisposed) return;
        if (Descendants.FirstOrDefault(child => child.IsVisible) is { } first)
        {
            first.RequestFocus();
            return;
        }
        if (IsContainer && (this is not ListFieldViewModel list || !list.AddItemCommand.CanExecute(null)))
        {
            _supplyFocusRequestVersion++;
            OnPropertyChanged(nameof(SupplyFocusRequestVersion));
        }
        else SignalFocus();
    }

    protected void SignalFocus()
    {
        if (IsDisposed) return;
        _focusRequestVersion++;
        OnPropertyChanged(nameof(FocusRequestVersion));
    }

    /// <summary>Projects one settled root and its literal diagnostics without replacing surviving editors.</summary>
    internal void Refresh(SetupFieldState state, IReadOnlyList<DefinitionDiagnostic> diagnostics)
    {
        if (IsDisposed || _input is null) return;
        // A malformed root denies stale collection projection, but its own raw correction text remains available.
        bool valid = _input.TryValue(out JsonElement? value);
        if (!valid) value = null;
        RefreshNode([Id], value, state.IsActive, diagnostics);
        IsRequired = state.IsRequired;
        HasError |= state.IsActive && state.HasError;
        if (!_editing)
        {
            SetProperty(ref _text, _input.Text, nameof(Text));
            NotifyValue();
        }
    }

    private void RefreshNode(IReadOnlyList<string> path, JsonElement? value, bool active,
        IReadOnlyList<DefinitionDiagnostic> diagnostics)
    {
        Load(value);
        IsVisible = Form.IsEditorVisible(path);
        HasError = active && (Form.HasInputError(path) || diagnostics.Any(diagnostic =>
            diagnostic.FieldId == path[0] && diagnostic.RelativePath.Count >= path.Count - 1 &&
            diagnostic.RelativePath.Take(path.Count - 1).SequenceEqual(path.Skip(1))));
        if (!IsContainer && !_editing && Form.ReadDraftText(path) is { } draft)
        {
            SetProperty(ref _text, draft, nameof(Text));
            _provided = true;
            NotifyValue();
        }
        foreach (var child in Descendants)
        {
            int row = this is ListFieldViewModel list ? list.Items.IndexOf(child) : -1;
            IReadOnlyList<string> childPath = [.. path, row >= 0
                ? row.ToString(CultureInfo.InvariantCulture) : child.Id];
            JsonElement? childValue = value is { ValueKind: JsonValueKind.Object } mapping &&
                mapping.TryGetProperty(child.Id, out var property) ? property
                : value is { ValueKind: JsonValueKind.Array } items && row >= 0 ? items[row] : null;
            child.RefreshNode(childPath, childValue, active, diagnostics);
        }
        OnPropertyChanged(nameof(ErrorText));
        OnPropertyChanged(nameof(HasProtectedDescendants));
        OnPropertyChanged(nameof(HasRecommended));
        UseRecommendedCommand.NotifyCanExecuteChanged();
        RemoveCommand.NotifyCanExecuteChanged();
    }

    protected IReadOnlyList<string> FullPath() => Parent is null ? [Id]
        : [.. Parent.FullPath(), Parent is ListFieldViewModel list
            ? list.Items.IndexOf(this).ToString(CultureInfo.InvariantCulture) : Id];

    private void Clear()
    {
        if (IsDisposed) return;
        if (IsListItem && Schema.Type == DefinitionNodeType.Text)
            Form.EditValue(FullPath(), "");
        else Form.SetValue(FullPath(), null);
        RequestFocus();
    }
    private void UseRecommended()
    {
        if (IsDisposed || !HasRecommended) return;
        if (TryExtensionValue("suggestedValue", out var suggested)) Form.SetValue(FullPath(), suggested);
        RequestFocus();
    }
    private void Supply()
    {
        if (IsDisposed) return;
        bool supplied = this is ObjectFieldViewModel ? Form.SupplyObject(FullPath()) : Form.SupplyList(FullPath());
        if (supplied) RequestFocus();
    }
    private string ReadMetadata(string name) => Schema.Keywords.TryGetProperty(name, out var value) ? value.GetString() ?? "" : "";
    private bool TryExtensionValue(string name, out JsonElement value)
    {
        value = default;
        return Schema.Keywords.TryGetProperty("x-configurator", out var extension) &&
            extension.TryGetProperty(name, out value);
    }
    private static bool HasNonPublicSchema(DefinitionNode schema) =>
        schema.Keywords.TryGetProperty("writeOnly", out var writeOnly) && writeOnly.GetBoolean() ||
        !schema.Keywords.TryGetProperty("x-configurator", out var extension) ||
        !extension.TryGetProperty("sensitivity", out var sensitivity) || sensitivity.GetString() != "public" ||
        schema.Properties.Values.Any(HasNonPublicSchema) ||
        schema.Items is { } item && HasNonPublicSchema(item) ||
        schema.AdditionalProperties is { } additional && HasNonPublicSchema(additional) ||
        schema.Variants.Any(HasNonPublicSchema);
    internal static string Display(JsonElement value) => value.ValueKind == JsonValueKind.String ? value.GetString()! : value.GetRawText();

    public void Dispose()
    {
        if (IsDisposed) return;
        IsDisposed = true;
        foreach (var child in Descendants) child.Dispose();
        _form = null;
        _input = null;
        _root = null;
        _text = string.Empty;
        ClearCommand.NotifyCanExecuteChanged();
        SupplyCommand.NotifyCanExecuteChanged();
        UseRecommendedCommand.NotifyCanExecuteChanged();
        GC.SuppressFinalize(this);
    }
}
