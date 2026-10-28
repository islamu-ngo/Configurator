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
    [Parameter, EditorRequired] public DefinitionNode Schema { get; set; } = null!;
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
    private DefinitionEditorPresentation? _presentation;
    private bool _actionsMounted;
    private long _actionGeneration;
    private string _newMapKey = "";
    private bool _invalidMapKey;
    private string? _focusMapEntry;
    private long _mapFocusGeneration;
    private DefinitionSessionPrivacy _lastPrivacy;

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
            && !(parameters.TryGetValue<DefinitionNode>(nameof(Schema), out var suppliedSchema) && !ReferenceEquals(Schema, suppliedSchema))
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
        (diagnostic.Location is { } location
            ? Form.TryResolveCorrection(diagnostic.FieldId, location, out IReadOnlyList<string> resolved) &&
                resolved.Take(Path.Count).SequenceEqual(Path, StringComparer.Ordinal)
            : diagnostic.RelativePath.Take(Path.Count - 1).SequenceEqual(Path.Skip(1)));
    private string ControlId => _controlId ??= Form.GetEditorIdentity(Path);
    private string Type => Schema.JsonType;
    private string Title => TitleOverride ?? _presentation?.Title ?? (Schema.Keywords.TryGetProperty("title", out var value) ? value.GetString()! : "Item");
    private string? Help => _presentation?.Description ?? (Schema.Keywords.TryGetProperty("description", out var value) ? value.GetString() : null);
    private DefinitionNode ObjectSchema => Value is { } value ? DefinitionValidator.SelectVariant(Schema, value) ?? Schema : Schema;
    private IEnumerable<JsonProperty> MapEntries => ObjectSchema.AdditionalProperties is not null &&
        Value is { ValueKind: JsonValueKind.Object } value
            ? value.EnumerateObject().Where(property => !ObjectSchema.Properties.ContainsKey(property.Name)) : [];
    private string VariantSelection
    {
        get
        {
            if (Value is not { } value || DefinitionValidator.SelectVariant(Schema, value) is not { } selected) return "";
            for (int index = 0; index < Schema.Variants.Count; index++)
                if (ReferenceEquals(Schema.Variants[index], selected)) return ChoiceIdentity(index);
            return "";
        }
    }
    private bool HasHelp => !string.IsNullOrWhiteSpace(Help)
        || InputPurposeHelp is not null || IsMasked && (Type == "boolean" || HasChoices);
    private bool Required => Form.GetEditorState(Path)?.IsRequired ?? RequiredOverride ?? false;
    private bool HasOptionalChildren => ContainsOptionalProperty(ObjectSchema);
    private bool OptionalEditorsExpanded => Form.IsEditorExpanded(Path);
    private void ExpandEditor() => Form.SetEditorExpanded(Path, true);
    private void ToggleOptionalEditors() => Form.SetEditorExpanded(Path, !OptionalEditorsExpanded);
    private static JsonElement? Metadata(DefinitionNode schema, string name) =>
        schema.Keywords.TryGetProperty("x-configurator", out var metadata) && metadata.TryGetProperty(name, out var value)
            ? value : null;
    private static bool ContainsOptionalProperty(DefinitionNode schema) =>
        schema.Properties.Any(child =>
            (!schema.Required.Contains(child.Key, StringComparer.Ordinal) &&
             Metadata(child.Value, "detail")?.GetString() != "basic") ||
            ContainsOptionalProperty(child.Value));
    private bool HasChoices => Schema.Keywords.TryGetProperty("enum", out _);
    private JsonElement Choices => Schema.Keywords.GetProperty("enum");
    private static string ChoiceIdentity(int index) => index.ToString(System.Globalization.CultureInfo.InvariantCulture);
    private string ChoiceLabel(int index, JsonElement value)
    {
        string label = _presentation?.Choices[index].Label ?? Display(value);
        return label.Length == 0 ? "Empty string" : label;
    }
    private string Selection
    {
        get
        {
            if (Value is not { } value) return "";
            if (!HasChoices) return Display(value);
            int index = 0;
            foreach (JsonElement choice in Choices.EnumerateArray())
            {
                if (JsonElement.DeepEquals(choice, value)) return ChoiceIdentity(index);
                index++;
            }
            return "";
        }
    }
    private static bool DeclaresSecret(DefinitionNode schema) =>
        schema.Keywords.TryGetProperty("writeOnly", out var writeOnly) && writeOnly.GetBoolean()
        || Metadata(schema, "sensitivity")?.GetString() == "secret";
    private bool IsSecret => InheritedSecret || DeclaresSecret(Schema)
        || Path.Count == 1 && DeclaresSecret(Form.Definition.Configuration);
    private bool SecretsEnabled => Form.SessionPrivacy == DefinitionSessionPrivacy.BrowserSecretsEnabled;
    private bool IsMasked => IsSecret || InheritedSensitive || Metadata(Schema, "sensitivity")?.GetString() != "public"
        || Path.Count == 1 && Metadata(Form.Definition.Configuration, "sensitivity")?.GetString() != "public";
    private int MaximumItems => Schema.Keywords.TryGetProperty("maxItems", out var value) ? value.GetInt32() : SetupCompositionLimits.DefaultSequenceEntries;
    private int Count => Value is { ValueKind: JsonValueKind.Array } value ? value.GetArrayLength() : 0;
    private string Text => !Form.CanEdit(Path) ? string.Empty : Path.Count == 1
        ? _rootInput!.Text
        : Form.ReadDraftText(Path) ?? (Value is { } value ? Display(value) : string.Empty);
    private string Autocomplete => Form.CanEdit(Path) && Metadata(Schema, "credential") is { } credential
        && credential.GetProperty("kind").GetString() == "password"
        ? credential.GetProperty("acquisition").GetString() switch
        {
            "create" => "section-" + ControlId + " new-password",
            "existing" => "section-" + ControlId + " current-password",
            _ => "off"
        } : "off";
    private string? InputPurposeHelp => Metadata(Schema, "credential") is { } credential
        ? credential.GetProperty("acquisition").GetString() switch
        {
            "issuer" => "Obtain this value from its issuer, then paste it or map a password manager custom field. Do not substitute a locally generated password.",
            "create" => "Choose a new value that meets the target's requirements. A password manager may help create and retain it.",
            _ => "Use the value already accepted by your target. A password manager may fill or paste it."
        } + " Your password manager has independent custody; clearing this workspace does not clear its vault."
        : null;
    private DefinitionNode ItemSchema => Form.GetListItemMetadata(Path);
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
        _presentation = Form.GetEditorPresentation(Path);
        bool actionsMounted = Form.IsEditorVisible(Path) && Form.CanEdit(Path);
        if (_actionsMounted != actionsMounted)
        {
            _actionsMounted = actionsMounted;
            ++_actionGeneration;
        }
        if (!actionsMounted || !ReferenceEquals(_rootForm, Form) || _lastPrivacy != Form.SessionPrivacy)
        {
            _newMapKey = "";
            _focusMapEntry = null;
        }
        _lastPrivacy = Form.SessionPrivacy;
        bool controlMounted = Form.IsEditorVisible(Path) && Form.CanEdit(Path) && Type is not ("object" or "array");
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
        if (HasChoices)
        {
            if (!int.TryParse(selection, out int index) || index < 0 ||
                index >= Choices.GetArrayLength()) return false;
            _invalid = !Form.SetValue(Path, Choices[index]);
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
    private bool IsCurrentAction(DefinitionFormModel form, IReadOnlyList<string> path, long revision, long generation) =>
        !_disposed && _actionsMounted && generation == _actionGeneration && ReferenceEquals(Form, form) &&
        Form.InputRevision == revision && Path.SequenceEqual(path) && Form.CanEdit(Path);

    private void OmitCurrent(DefinitionFormModel form, IReadOnlyList<string> path, long revision, long generation)
    {
        if (IsCurrentAction(form, path, revision, generation)) Omit();
    }
    private void UpdateMapKey(string? key, DefinitionFormModel form, IReadOnlyList<string> path, long revision, long generation)
    {
        if (!IsCurrentAction(form, path, revision, generation)) return;
        _newMapKey = key ?? "";
        _invalidMapKey = false;
    }

    private void Reset(DefinitionFormModel form, IReadOnlyList<string> path, long revision, long generation)
    {
        if (IsCurrentAction(form, path, revision, generation)) { Form.ResetValue(Path); _invalid = false; _ = NotifyValidityAsync(); }
    }
    private void Suggest(DefinitionFormModel form, IReadOnlyList<string> path, long revision, long generation)
    {
        if (IsCurrentAction(form, path, revision, generation)) { Form.ApplySuggestion(Path); _invalid = false; _ = NotifyValidityAsync(); }
    }
    private void SupplyNull(DefinitionFormModel form, IReadOnlyList<string> path, long revision, long generation)
    {
        if (IsCurrentAction(form, path, revision, generation)) { Form.SetNull(Path); _invalid = false; _ = NotifyValidityAsync(); }
    }
    private void ChooseVariant(string? choice, DefinitionFormModel form, IReadOnlyList<string> path, long revision, long generation)
    {
        if (IsCurrentAction(form, path, revision, generation) &&
            int.TryParse(choice, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int index))
        {
            Form.SelectVariant(Path, index);
            _ = NotifyValidityAsync();
        }
    }
    private void AddMapEntry(DefinitionFormModel form, IReadOnlyList<string> path, long revision, long generation)
    {
        if (!IsCurrentAction(form, path, revision, generation) || ObjectSchema.AdditionalProperties is not { } entrySchema) return;
        if (Value is { ValueKind: JsonValueKind.Object } current && current.TryGetProperty(_newMapKey, out _))
        {
            _invalidMapKey = true;
            return;
        }
        string[] entryPath = [.. Path, _newMapKey];
        try { _ = DefinitionPath.FromSegments(entryPath); }
        catch (ArgumentException) { _invalidMapKey = true; return; }
        JsonElement initial = entrySchema.Type switch
        {
            DefinitionNodeType.Text => JsonSerializer.SerializeToElement(""),
            DefinitionNodeType.Boolean => JsonSerializer.SerializeToElement(false),
            DefinitionNodeType.WholeNumber or DefinitionNodeType.Number => JsonSerializer.SerializeToElement(0),
            DefinitionNodeType.Sequence => JsonSerializer.SerializeToElement(Array.Empty<object>()),
            _ => JsonSerializer.SerializeToElement(new Dictionary<string, object>())
        };
        _invalidMapKey = !Form.SetValue(entryPath, initial);
        if (!_invalidMapKey)
        {
            _focusMapEntry = _newMapKey;
            ++_mapFocusGeneration;
            _newMapKey = "";
        }
        _ = NotifyValidityAsync();
    }
    private void RemoveMapEntry(string name, DefinitionFormModel form, IReadOnlyList<string> path, long revision, long generation)
    {
        if (IsCurrentAction(form, path, revision, generation))
        {
            Form.EditValue([.. Path, name], null);
            _focusMapEntry = null;
            _ = NotifyValidityAsync();
        }
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
            if (Type is "object" or "array") await _group.FocusAsync(preventScroll: true);
            else await FocusScalarAsync(preventScroll: true);
        }
        if (_focusObjectChildren) { _focusObjectChildren = false; if (!CanFocus(Schema, Path)) await _group.FocusAsync(preventScroll: true); }
        if (FocusFirstControl && !_focusedForRequest && Form.CanEdit(Path) && Type != "object" && (Type != "array" || Count == 0))
        {
            _focusedForRequest = true;
            if (Type == "array") await _addButton.FocusAsync();
            else await FocusScalarAsync();
        }
        if (!_focusAdd) return;
        _focusAdd = false;
        await _addButton.FocusAsync(preventScroll: true);
    }
    private async Task FocusScalarAsync(bool preventScroll = false)
    {
        if (_textControl is not null && (Type != "boolean" && !HasChoices || IsMasked))
            await _textControl.FocusAsync();
        else if (_choiceControl is not null) await _choiceControl.FocusAsync();
    }
    private bool CanFocus(DefinitionNode schema, IReadOnlyList<string> path)
    {
        if (FocusPath.Count > 0 && !FocusPath.Take(path.Count).SequenceEqual(path)) return false;
        if (!Form.CanEdit(path)) return false;
        if (schema.Type != DefinitionNodeType.Mapping) return true;
        return schema.Properties.Any(child => CanFocus(child.Value, path.Append(child.Key).ToArray()));
    }
    private static string Display(JsonElement value) => value.ValueKind == JsonValueKind.String ? value.GetString()! : value.GetRawText();
    public void Dispose()
    {
        _disposed = true;
        _newMapKey = "";
        _focusMapEntry = null;
        if (_invalid) { _invalid = false; _ = NotifyValidityAsync(); }
        GC.SuppressFinalize(this);
    }
}
