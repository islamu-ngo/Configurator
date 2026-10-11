namespace Configurator.Desktop.ViewModels.Fields;

using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Configurator.Core.Definitions;
using Configurator.Presentation;

/// <summary>
/// Owns one editor node. Roots subscribe to the shared form; descendants commit through their root.
/// Dispose roots before replacing or disposing the shared form.
/// </summary>
public abstract class FieldViewModel : ObservableObject, IDisposable
{
    private static readonly string[] ConstraintNames = ["minimum", "maximum", "minLength", "maxLength", "minItems", "maxItems"];
    private string _text = string.Empty;
    private bool _provided;
    private bool _structuredInvalid;
    private bool _hasError;
    private bool _isVisible = true;
    private bool _isRequired;
    private bool _synchronizing;
    private int _focusRequestVersion;
    private DesktopDisplayMode _mode;
    private DefinitionFormModel? _form;
    private DefinitionFieldInput? _input;
    private FieldViewModel? _root;

    protected FieldViewModel(JsonElement schema, FieldViewModel? parent, bool listItem)
    {
        Schema = schema;
        Parent = parent;
        IsListItem = listItem;
        RequiresMasking = parent?.RequiresMasking == true || HasNonPublicSchema(schema);
        _isRequired = listItem || schema.TryGetProperty("required", out var required) && required.GetBoolean();
        ClearCommand = new RelayCommand(Clear, () => !IsDisposed);
        UseRecommendedCommand = new RelayCommand(UseRecommended, () => !IsDisposed && HasRecommended);
        RemoveCommand = new RelayCommand(() =>
        {
            if (Parent is ListFieldViewModel list) list.RemoveItemCommand.Execute(this);
        }, () => !IsDisposed && Parent is ListFieldViewModel list && list.RemoveItemCommand.CanExecute(this));
    }

    protected JsonElement Schema { get; }
    protected FieldViewModel? Parent { get; }
    public bool IsListItem { get; }
    protected bool IsDisposed { get; private set; }
    protected bool IsProvided => _provided;
    protected bool IsStructuredInvalid => _structuredInvalid;
    protected FieldViewModel Root => _root ?? this;
    internal virtual IEnumerable<FieldViewModel> Descendants => [];
    public string Id => Schema.GetProperty("id").GetString()!;
    public string Title => Schema.GetProperty("title").GetString()!;
    public string Name => Title;
    public string ClearName => $"Clear {Title}";
    public string RecommendedName => $"Use recommended value for {Title}";
    public string RemoveItemName => $"Remove item from {Parent?.Title ?? Title}";
    public string Help => ReadMetadata("help");
    public string HelpText => ReadMetadata("help") is { Length: > 0 } help ? help : Title;
    public string Group => ReadMetadata("group");
    public string Consequence => ReadMetadata("consequence");
    public string Format => ReadMetadata("format");
    public string ConstraintsSummary => string.Join(" | ", ConstraintNames
        .Where(name => Schema.TryGetProperty(name, out _))
        .Select(name => $"{name}: {Schema.GetProperty(name).GetRawText()}"));
    public bool RequiresMasking { get; }
    public char PasswordChar => RequiresMasking ? '●' : '\0';
    public bool IsPublic => !RequiresMasking;
    public bool HasProtectedDescendants => RequiresMasking || Descendants.Any(child => child.HasProtectedDescendants);
    public bool HasRecommended => !HasProtectedDescendants && Schema.TryGetProperty("recommended", out _);
    public bool HasError { get => _hasError; private set => SetProperty(ref _hasError, value); }
    public bool IsVisible { get => _isVisible; private set => SetProperty(ref _isVisible, value); }
    public bool IsRequired { get => _isRequired; private set => SetProperty(ref _isRequired, value); }
    public string ErrorText => HasError ? "A valid value is required. Check the declared type and constraints."
        : IsRequired ? "Required" : "Optional";
    public int FocusRequestVersion => _focusRequestVersion;
    public IRelayCommand ClearCommand { get; }
    public IRelayCommand UseRecommendedCommand { get; }
    public IRelayCommand RemoveCommand { get; }

    /// <summary>Scalar editor text; structured nodes serialize declared descendants, never raw JSON controls.</summary>
    public string Text
    {
        get => _text;
        set
        {
            value ??= string.Empty;
            if (IsDisposed || _text == value) return;
            if (this is ObjectFieldViewModel or ListFieldViewModel)
            {
                if (TryParseStructured(value, out var parsed)) Load(parsed);
                else
                {
                    _provided = true;
                    _structuredInvalid = true;
                    SetProperty(ref _text, value);
                }
            }
            else
            {
                SetProperty(ref _text, value);
                _provided = value.Length > 0 || IsListItem;
                NotifyValue();
            }
            Parent?.MarkProvided();
            Commit();
        }
    }

    internal void Attach(DefinitionFormModel form, DefinitionFieldInput input, DesktopDisplayMode mode)
    {
        _root = this;
        _form = form;
        _input = input;
        _mode = mode;
        input.PropertyChanged += InputChanged;
        form.InputChanged += FormChanged;
        LoadInput();
        Refresh(mode);
    }

    internal void AdoptRoot(FieldViewModel root)
    {
        _root = root;
        foreach (var child in Descendants) child.AdoptRoot(root);
    }

    internal virtual void Load(JsonElement? value)
    {
        _provided = value is not null;
        SetProperty(ref _text, value is { } supplied ? Display(supplied) : string.Empty, nameof(Text));
        NotifyValue();
    }

    internal virtual bool TryValue(out JsonElement? value)
    {
        value = null;
        if (!_provided) return true;
        switch (Schema.GetProperty("type").GetString())
        {
            case "string":
                value = JsonSerializer.SerializeToElement(Text);
                return true;
            case "enum":
                foreach (var choice in Schema.GetProperty("choices").EnumerateArray())
                    if (Display(choice) == Text) { value = choice; return true; }
                return false;
            case "boolean":
                if (!bool.TryParse(Text, out bool boolean)) return false;
                value = JsonSerializer.SerializeToElement(boolean);
                return true;
            case "integer":
                if (!long.TryParse(Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long integer)) return false;
                value = JsonSerializer.SerializeToElement(integer);
                return true;
            case "number":
                if (!decimal.TryParse(Text, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal number)) return false;
                value = JsonSerializer.SerializeToElement(number);
                return true;
            default:
                return false;
        }
    }

    protected virtual void NotifyValue() { }
    protected void SetStructuredText(JsonElement? value)
    {
        _structuredInvalid = false;
        _provided = value is not null;
        SetProperty(ref _text, value?.GetRawText() ?? string.Empty, nameof(Text));
    }
    protected void MarkProvided()
    {
        _structuredInvalid = false;
        _provided = true;
        Parent?.MarkProvided();
    }
    protected void Commit()
    {
        if (IsDisposed) return;
        FieldViewModel root = _root ?? this;
        if (root._input is null || root._synchronizing) return;
        root._synchronizing = true;
        try
        {
            string text = root is ObjectFieldViewModel or ListFieldViewModel
                ? root.TryValue(out var value) ? value?.GetRawText() ?? string.Empty : "invalid"
                : root.Text;
            root._input.Text = text;
            if (root._input.Text != text) root.LoadInput();
            else root.UpdateStructuredText();
        }
        finally { root._synchronizing = false; }
        root.Refresh(root._mode);
    }

    private void UpdateStructuredText()
    {
        foreach (var child in Descendants) child.UpdateStructuredText();
        if (this is ObjectFieldViewModel or ListFieldViewModel)
        {
            bool valid = TryValue(out var value);
            SetProperty(ref _text, valid ? value?.GetRawText() ?? string.Empty : "invalid", nameof(Text));
        }
    }

    /// <summary>Signals the View focus bridge; structured nodes route to the first visible descendant.</summary>
    public void RequestFocus()
    {
        if (IsDisposed) return;
        if (Descendants.FirstOrDefault(child => child.IsVisible) is { } first)
        {
            first.RequestFocus();
            return;
        }
        SignalFocus();
    }

    protected void SignalFocus()
    {
        if (IsDisposed) return;
        _focusRequestVersion++;
        OnPropertyChanged(nameof(FocusRequestVersion));
    }

    /// <summary>Re-evaluates activation, requiredness, errors and disclosure without replacing editor instances.</summary>
    public void Refresh(DesktopDisplayMode mode)
    {
        if (IsDisposed) return;
        _mode = mode;
        if (_form is not null && _input is not null)
        {
            SetupFieldState state = _form.Validate().Fields.Single(field => field.Id == Id);
            IsRequired = state.IsRequired;
            HasError = state.HasError || _input.InputDiagnosticCode is not null || !_input.TryValue(out _);
            IsVisible = state.IsActive && (HasError || IsRequired || HasProtectedDescendants ||
                Consequence.Length > 0 || ReadMetadata("disclosure") == "essential" || mode == DesktopDisplayMode.Advanced ||
                mode == DesktopDisplayMode.Guided && ReadMetadata("disclosure") != "advanced");
            RefreshChildren(mode, HasError);
        }
        else
        {
            RefreshChildren(mode, HasError);
        }
        OnPropertyChanged(nameof(ErrorText));
        OnPropertyChanged(nameof(HasProtectedDescendants));
        OnPropertyChanged(nameof(HasRecommended));
        UseRecommendedCommand.NotifyCanExecuteChanged();
        RemoveCommand.NotifyCanExecuteChanged();
    }

    private void RefreshChildren(DesktopDisplayMode mode, bool rootError)
    {
        foreach (var child in Descendants)
        {
            child.HasError = rootError;
            child.IsVisible = rootError || child.IsRequired || child.HasProtectedDescendants ||
                child.Consequence.Length > 0 || child.ReadMetadata("disclosure") == "essential" || mode == DesktopDisplayMode.Advanced ||
                mode == DesktopDisplayMode.Guided && child.ReadMetadata("disclosure") != "advanced";
            child.RefreshChildren(mode, rootError);
            child.OnPropertyChanged(nameof(ErrorText));
        }
    }

    private void Clear()
    {
        if (IsDisposed) return;
        Load(IsListItem && Schema.GetProperty("type").GetString() == "string"
            ? JsonSerializer.SerializeToElement("") : null);
        Parent?.MarkProvided();
        Commit();
        RequestFocus();
    }
    private void UseRecommended()
    {
        if (IsDisposed || !HasRecommended) return;
        Load(Schema.GetProperty("recommended"));
        Parent?.MarkProvided();
        Commit();
        RequestFocus();
    }
    private void InputChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (_synchronizing || IsDisposed || args.PropertyName != nameof(DefinitionFieldInput.Text)) return;
        LoadInput();
        Refresh(_mode);
    }
    private void FormChanged(object? sender, EventArgs args)
    {
        if (!_synchronizing && !IsDisposed) Refresh(_mode);
    }
    private void LoadInput()
    {
        if (_input is null) return;
        if (_input.TryValue(out var value)) Load(value);
        else
        {
            Load(null);
            SetProperty(ref _text, _input.Text, nameof(Text));
            _provided = true;
            _structuredInvalid = this is ObjectFieldViewModel or ListFieldViewModel;
        }
        AdoptRoot(this);
    }
    private bool TryParseStructured(string text, out JsonElement? value)
    {
        value = null;
        if (text.Length == 0) return true;
        try
        {
            using var document = JsonDocument.Parse(text, new JsonDocumentOptions { MaxDepth = 32 });
            JsonValueKind expected = this is ObjectFieldViewModel ? JsonValueKind.Object : JsonValueKind.Array;
            if (document.RootElement.ValueKind != expected) return false;
            value = document.RootElement.Clone();
            return true;
        }
        catch (JsonException) { return false; }
    }
    private string ReadMetadata(string name) => Schema.TryGetProperty(name, out var value) ? value.GetString() ?? "" : "";
    private static bool HasNonPublicSchema(JsonElement schema) =>
        schema.TryGetProperty("sensitivity", out var sensitivity) && sensitivity.GetString() != "public" ||
        schema.TryGetProperty("properties", out var properties) && properties.EnumerateArray().Any(HasNonPublicSchema);
    internal static string Display(JsonElement value) => value.ValueKind == JsonValueKind.String ? value.GetString()! : value.GetRawText();

    public void Dispose()
    {
        if (IsDisposed) return;
        IsDisposed = true;
        if (_input is not null) _input.PropertyChanged -= InputChanged;
        if (_form is not null) _form.InputChanged -= FormChanged;
        foreach (var child in Descendants) child.Dispose();
        _form = null;
        _input = null;
        _root = null;
        _text = string.Empty;
        ClearCommand.NotifyCanExecuteChanged();
        UseRecommendedCommand.NotifyCanExecuteChanged();
        GC.SuppressFinalize(this);
    }
}
