namespace Configurator.Browser.Components.Configuration;

using System.Text.Json;
using Configurator.Core.Composition;
using Configurator.Core.Definitions;
using Configurator.Browser.Components.Common;
using Configurator.Presentation;
using Microsoft.AspNetCore.Components;
using MudBlazor;

public partial class ConfigurationField
{
    [Parameter, EditorRequired] public DefinitionFormModel Form { get; set; } = null!;
    [Parameter, EditorRequired] public JsonElement Schema { get; set; }
    [Parameter, EditorRequired] public IReadOnlyList<string> Path { get; set; } = [];
    [Parameter] public DefinitionValidationResult? Validation { get; set; }
    [Parameter] public EventCallback<(string Id, string FieldId, bool Invalid)> ValidationChanged { get; set; }
    [Parameter] public bool InheritedSecret { get; set; }
    [Parameter] public bool InheritedSensitive { get; set; }
    [Parameter] public bool? RequiredOverride { get; set; }
    [Parameter] public string? TitleOverride { get; set; }
    [Parameter] public bool FocusFirstControl { get; set; }
    [Parameter] public long FocusGeneration { get; set; }
    [Parameter] public int FocusRow { get; set; } = -1;
    [Parameter] public IReadOnlyList<string> FocusPath { get; set; } = [];
    private ElementReference _addButton;
    private ElementReference _group;
    private MudSelect<string>? _choiceControl;
    private AppTextField? _textControl;
    private bool _focusedForRequest;
    private long _observedFocusGeneration;
    private int _focusNewRow = -1;
    private bool _focusObjectChildren;
    private bool _focusAdd;
    private bool _focusAfterOmit;
    private bool _invalid;
    private bool _disposed;
    private bool _controlMounted;
    private long _controlGeneration;
    private long _lastInputRevision = -1;
    private DefinitionFormModel? _rootForm;
    private DefinitionFieldInput? _rootInput;
    private string? _controlId;
    private DefinitionFormModel? _parameterForm;
    private DefinitionFormProjection? _parameterProjection;
    private long _parameterInputRevision = -1;
    private bool _suppressParameterRender;

    public override Task SetParametersAsync(ParameterView parameters)
    {
        bool Changed<T>(string name, T current) =>
            parameters.TryGetValue<T>(name, out var supplied) && !EqualityComparer<T>.Default.Equals(current, supplied);
        bool PathChanged(string name, IReadOnlyList<string> current) =>
            parameters.TryGetValue<IReadOnlyList<string>>(name, out var supplied) && !current.SequenceEqual(supplied);

        DefinitionFormModel form = parameters.TryGetValue<DefinitionFormModel>(nameof(Form), out var suppliedForm)
            ? suppliedForm : Form;
        _suppressParameterRender = ReferenceEquals(_parameterForm, form)
            && _parameterInputRevision == form.InputRevision
            && ReferenceEquals(_parameterProjection, form.Projection)
            && !Changed(nameof(Schema), Schema)
            && !PathChanged(nameof(Path), Path)
            && !Changed(nameof(Validation), Validation)
            && !Changed(nameof(ValidationChanged), ValidationChanged)
            && !Changed(nameof(InheritedSecret), InheritedSecret)
            && !Changed(nameof(InheritedSensitive), InheritedSensitive)
            && !Changed(nameof(RequiredOverride), RequiredOverride)
            && !Changed(nameof(TitleOverride), TitleOverride)
            && !Changed(nameof(FocusFirstControl), FocusFirstControl)
            && !Changed(nameof(FocusGeneration), FocusGeneration)
            && !Changed(nameof(FocusRow), FocusRow)
            && !PathChanged(nameof(FocusPath), FocusPath);
        try
        {
            Task updating = base.SetParametersAsync(parameters);
            _parameterForm = form;
            _parameterInputRevision = form.InputRevision;
            _parameterProjection = form.Projection;
            return updating;
        }
        finally
        {
            _suppressParameterRender = false;
        }
    }

    // Every input revision still renders fresh fenced callbacks. View-only
    // parameter updates with unchanged projection need no field-tree work.
    protected override bool ShouldRender() => !_suppressParameterRender;

    private string Error => string.Join(" ", Validation?.Diagnostics.Where(MatchesDiagnostic).Select(item => ConfigurationDiagnosticText.Describe(item.Code)) ?? []);
    private bool MatchesDiagnostic(DefinitionDiagnostic diagnostic) => diagnostic.FieldId == Path[0] &&
        diagnostic.RelativePath.Take(Path.Count - 1).SequenceEqual(Path.Skip(1));
    private string ControlId => _controlId ??= Form.GetEditorIdentity(Path);
    private string Type => Schema.GetProperty("type").GetString()!;
    private string Title => TitleOverride ?? ConfigurationDiagnosticText.Title(Schema.TryGetProperty("title", out var value) ? value.GetString()! : "Item", Schema.TryGetProperty("id", out var id) ? id.GetString() : null);
    private string? Help => Schema.TryGetProperty("help", out var value) ? value.GetString() : null;
    private string? Consequence => Schema.TryGetProperty("consequence", out var value) ? value.GetString() : null;
    private bool HasHelp => !string.IsNullOrWhiteSpace(Help) || !string.IsNullOrWhiteSpace(Consequence)
        || InputPurposeHelp is not null || IsMasked && Type is "boolean" or "enum";
    private bool Required => RequiredOverride ?? (Schema.TryGetProperty("required", out var value) && value.GetBoolean());
    private bool HasOptionalChildren => ContainsOptionalProperty(Schema);
    private bool OptionalEditorsExpanded => Form.IsEditorExpanded(Path);
    private void ExpandEditor() => Form.SetEditorExpanded(Path, true);
    private void ToggleOptionalEditors() => Form.SetEditorExpanded(Path, !OptionalEditorsExpanded);
    private static bool ContainsOptionalProperty(JsonElement schema) =>
        schema.TryGetProperty("properties", out JsonElement properties) && properties.ValueKind == JsonValueKind.Array &&
        properties.EnumerateArray().Any(child =>
            (!(child.TryGetProperty("required", out JsonElement required) && required.GetBoolean()) &&
             !(child.TryGetProperty("disclosure", out JsonElement disclosure) && disclosure.GetString() == "essential")) ||
            ContainsOptionalProperty(child));
    private bool HasEmptyChoice => Type == "enum" && Schema.GetProperty("choices").EnumerateArray()
        .Any(choice => choice.ValueKind == JsonValueKind.String && choice.GetString() == "");
    private string ChoiceIdentity(int index, JsonElement choice) => HasEmptyChoice
        ? index.ToString(System.Globalization.CultureInfo.InvariantCulture) : Display(choice);
    private string Selection
    {
        get
        {
            if (Value is not { } value) return "";
            if (Type != "enum") return Display(value);
            int index = 0;
            foreach (JsonElement choice in Schema.GetProperty("choices").EnumerateArray())
            {
                if (JsonElement.DeepEquals(choice, value)) return ChoiceIdentity(index, choice);
                index++;
            }
            return "";
        }
    }
    private bool IsSecret => InheritedSecret || Schema.TryGetProperty("sensitivity", out var value) && value.GetString() == "secret";
    private bool SecretsEnabled => Form.SessionPrivacy == DefinitionSessionPrivacy.BrowserSecretsEnabled;
    private bool IsMasked => IsSecret || InheritedSensitive || Schema.TryGetProperty("sensitivity", out var value) && value.GetString() == "sensitive";
    private int MaximumItems => Schema.TryGetProperty("maxItems", out var value) ? value.GetInt32() : SetupCompositionLimits.DefaultSequenceEntries;
    private int Count => Value is { ValueKind: JsonValueKind.Array } value ? value.GetArrayLength() : 0;
    private string Text => !Form.CanEdit(Path) ? string.Empty : Path.Count == 1
        ? _rootInput!.Text
        : Form.ReadDraftText(Path) ?? (Value is { } value ? Display(value) : string.Empty);
    private string Autocomplete => Form.CanEdit(Path) && Schema.TryGetProperty("credential", out JsonElement credential)
        && credential.GetProperty("kind").GetString() == "password"
        ? credential.GetProperty("acquisition").GetString() switch
        {
            "create" => "section-" + ControlId + " new-password",
            "existing" => "section-" + ControlId + " current-password",
            _ => "off"
        } : "off";
    private string? InputPurposeHelp => Schema.TryGetProperty("credential", out JsonElement credential)
        ? credential.GetProperty("acquisition").GetString() switch
        {
            "issuer" => "Obtain this value from its issuer, then paste it or map a password manager custom field. Do not substitute a locally generated password.",
            "create" => "Choose a new value that meets the target's requirements. A password manager may help create and retain it.",
            _ => "Use the value already accepted by your target. A password manager may fill or paste it."
        } + " Your password manager has independent custody; clearing this workspace does not clear its vault."
        : null;
    private JsonElement ItemSchema => Form.GetListItemMetadata(Path);
    private JsonElement? Value
    {
        get
        {
            var input = _rootInput!;
            if (!input.TryValue(out JsonElement? supplied) || supplied is not { } value) return null;
            foreach (string segment in Path.Skip(1))
            {
                if (value.ValueKind == JsonValueKind.Object && value.TryGetProperty(segment, out var child)) value = child;
                else if (value.ValueKind == JsonValueKind.Array && int.TryParse(segment, out int index) && index >= 0 && index < value.GetArrayLength()) value = value[index];
                else return null;
            }
            return value;
        }
    }
    protected override void OnParametersSet()
    {
        bool controlMounted = Form.IsEditorVisible(Path) && Form.CanEdit(Path) && Type is not ("object" or "list");
        if (_controlMounted != controlMounted)
        {
            _controlMounted = controlMounted;
            ++_controlGeneration;
        }
        _controlId = null;
        if (!ReferenceEquals(_rootForm, Form) || _rootInput?.Definition.Id != Path[0])
        {
            _rootForm = Form;
            _rootInput = Form.GetFieldEditor(Path[0]).Input;
        }
        if (!FocusFirstControl || _observedFocusGeneration != FocusGeneration) _focusedForRequest = false;
        _observedFocusGeneration = FocusGeneration;
        bool invalid = Form.CanEdit(Path) && Form.HasInputError(Path);
        if (_invalid != invalid)
        {
            _invalid = invalid;
            _ = NotifyValidityAsync();
        }
    }
    private bool Edit(ChangeEventArgs args, DefinitionFormModel expectedForm, IReadOnlyList<string> expectedPath,
        long expectedRevision, long expectedControlGeneration)
    {
        if (_disposed || !_controlMounted || _controlGeneration != expectedControlGeneration || !ReferenceEquals(Form, expectedForm)
            || Form.InputRevision != expectedRevision || !Path.SequenceEqual(expectedPath)) return false;
        string selection = args.Value?.ToString() ?? "";
        if (selection.Length == 0) { Omit(); return true; }
        if (HasEmptyChoice)
        {
            if (!int.TryParse(selection, out int index) || index < 0 ||
                index >= Schema.GetProperty("choices").GetArrayLength()) return false;
            _invalid = !Form.SetValue(Path, Schema.GetProperty("choices")[index]);
            _ = NotifyValidityAsync();
            return true;
        }
        EditText(selection);
        return true;
    }
    private void EditText(string? suppliedText)
    {
        if (!Form.CanEdit(Path)) return;
        string text = suppliedText ?? string.Empty;
        _invalid = !Form.EditValue(Path, text);
        _ = NotifyValidityAsync();
    }
    private bool EditText(string? text, DefinitionFormModel expectedForm, IReadOnlyList<string> expectedPath, long expectedRevision,
        long expectedControlGeneration, bool changeOnly = false)
    {
        if (_disposed || !_controlMounted || _controlGeneration != expectedControlGeneration || !ReferenceEquals(Form, expectedForm) || Form.InputRevision != expectedRevision
            || !Path.SequenceEqual(expectedPath)) return false;
        if (changeOnly && _lastInputRevision == expectedRevision && Text == text) return false;
        EditText(text);
        _lastInputRevision = changeOnly ? -1 : Form.InputRevision;
        return true;
    }
    private Task NotifyValidityAsync() => ValidationChanged.InvokeAsync((ControlId, Path[0], _invalid));
    private (IReadOnlyList<string> ArtifactIds, IReadOnlyList<string> BindingKeys) InspectorBindings()
    {
        var entry = Form.Projection.Entries.FirstOrDefault(item => item.FieldId == Path[0]
            && item.RelativePath.SequenceEqual(Path.Skip(1)));
        return entry is null ? ([], []) : (entry.OutputArtifactIds.ToArray(), entry.BindingKeys.ToArray());
    }
    private void Omit()
    {
        if (!Form.EditValue(Path, null)) return;
        _invalid = false;
        _focusAfterOmit = true;
        _ = NotifyValidityAsync();
    }
    private void Add()
    {
        _focusNewRow = Count;
        if (!Form.AddListItem(Path)) _focusNewRow = -1;
    }
    private void SupplyObject() { _focusObjectChildren = true; if (!Form.SupplyObject(Path)) _focusObjectChildren = false; }
    private void SupplyList() { if (Form.SetValue(Path, JsonSerializer.SerializeToElement(Array.Empty<object>()))) _focusAdd = true; }
    private void Remove(int index)
    {
        if (!Form.RemoveListItem(Path, index)) return;
        _focusAdd = true;
    }
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        _focusNewRow = -1;
        if (_focusAfterOmit)
        {
            _focusAfterOmit = false;
            if (Type is "object" or "list") await _group.FocusAsync(preventScroll: true);
            else await FocusScalarAsync(preventScroll: true);
        }
        if (_focusObjectChildren) { _focusObjectChildren = false; if (!CanFocus(Schema, Path)) await _group.FocusAsync(preventScroll: true); }
        if (FocusFirstControl && !_focusedForRequest && Form.CanEdit(Path) && Type != "object" && (Type != "list" || Count == 0))
        {
            _focusedForRequest = true;
            if (Type == "list") await _addButton.FocusAsync();
            else await FocusScalarAsync();
        }
        if (!_focusAdd) return;
        _focusAdd = false;
        await _addButton.FocusAsync(preventScroll: true);
    }
    private async Task FocusScalarAsync(bool preventScroll = false)
    {
        if (_textControl is not null && (Type is not ("boolean" or "enum") || IsMasked))
            await _textControl.FocusAsync();
        else if (_choiceControl is not null) await _choiceControl.FocusAsync();
    }
    private bool CanFocus(JsonElement schema, IReadOnlyList<string> path)
    {
        if (FocusPath.Count > 0 && !FocusPath.Take(path.Count).SequenceEqual(path)) return false;
        if (!Form.CanEdit(path)) return false;
        if (schema.GetProperty("type").GetString() != "object") return true;
        return schema.GetProperty("properties").EnumerateArray().Any(child => CanFocus(child, path.Append(child.GetProperty("id").GetString()!).ToArray()));
    }
    private static string Display(JsonElement value) => value.ValueKind == JsonValueKind.String ? value.GetString()! : value.GetRawText();
    public void Dispose() { _disposed = true; if (_invalid) { _invalid = false; _ = NotifyValidityAsync(); } GC.SuppressFinalize(this); }
}
