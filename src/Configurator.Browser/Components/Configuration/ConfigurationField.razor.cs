namespace Configurator.Browser.Components.Configuration;

using System.Text.Json;
using Configurator.Core.Composition;
using Configurator.Core.Definitions;
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
    [Parameter] public bool FocusFirstControl { get; set; }
    [Parameter] public long FocusGeneration { get; set; }
    [Parameter] public int FocusRow { get; set; } = -1;
    private readonly string _controlId = "field-" + Guid.NewGuid().ToString("N");
    private readonly List<Guid> _rowKeys = [];
    private ElementReference _addButton;
    private ElementReference _group;
    private ElementReference _control;
    private MudInput<string>? _input;
    private bool _focusedForRequest;
    private long _observedFocusGeneration;
    private int _focusNewRow = -1;
    private bool _focusObjectChildren;
    private bool _focusAdd;
    private bool _focusAfterOmit;
    private bool _invalid;
    private string? _editing;
    private string? _observedText;
    private string Error => string.Join(" ", Validation?.Diagnostics.Where(item => MatchesDiagnostic(item.FieldId)).Select(item => ConfigurationDiagnosticText.Describe(item.Code)) ?? []);
    private bool MatchesDiagnostic(string? fieldId) => fieldId == Path[0] || fieldId is not null &&
        (Path.Count == 1 ? fieldId.StartsWith(Path[0] + "/", StringComparison.Ordinal) : fieldId == Path[0] + "/" + Path[1]);
    private string ControlId => Path.Count == 1 ? "choice-" + Path[0] : _controlId;
    private string Type => Schema.GetProperty("type").GetString()!;
    private string Title => ConfigurationDiagnosticText.Title(Schema.TryGetProperty("title", out var value) ? value.GetString()! : "Item", Schema.TryGetProperty("id", out var id) ? id.GetString() : null);
    private string? Help => Schema.TryGetProperty("help", out var value) ? value.GetString() : null;
    private string? Consequence => Schema.TryGetProperty("consequence", out var value) ? value.GetString() : null;
    private bool Required => RequiredOverride ?? (Schema.TryGetProperty("required", out var value) && value.GetBoolean());
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
    private bool IsMasked => IsSecret || InheritedSensitive || Schema.TryGetProperty("sensitivity", out var value) && value.GetString() == "sensitive";
    private int MaximumItems => Schema.TryGetProperty("maxItems", out var value) ? value.GetInt32() : SetupCompositionLimits.DefaultSequenceEntries;
    private int Count => Value is { ValueKind: JsonValueKind.Array } value ? value.GetArrayLength() : 0;
    private string Text => !Form.CanEdit(Path) ? string.Empty : Path.Count == 1
        ? Form.Fields.Single(input => input.Definition.Id == Path[0]).Text
        : _editing ?? (Value is { } value ? Display(value) : string.Empty);
    private Dictionary<string, object> InputAttributes => new()
    {
        ["autocomplete"] = "off",
        ["spellcheck"] = "false",
        ["step"] = Type == "integer" ? "1" : "any",
        ["aria-required"] = Required.ToString().ToLowerInvariant()
    };
    private JsonElement ItemSchema => JsonSerializer.SerializeToElement(new Dictionary<string, object?>
    {
        ["type"] = Schema.GetProperty("itemType").GetString(),
        ["required"] = true,
        ["title"] = Title + " item",
        ["sensitivity"] = Schema.GetProperty("sensitivity").GetString(),
        ["properties"] = Schema.TryGetProperty("properties", out var value) ? value : (JsonElement?)null
    });
    private JsonElement? Value
    {
        get
        {
            var input = Form.Fields.Single(inputField => inputField.Definition.Id == Path[0]);
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
        if (!FocusFirstControl || _observedFocusGeneration != FocusGeneration) _focusedForRequest = false;
        _observedFocusGeneration = FocusGeneration;
        while (_rowKeys.Count < Count) _rowKeys.Add(Guid.NewGuid());
        if (_rowKeys.Count > Count) _rowKeys.RemoveRange(Count, _rowKeys.Count - Count);
        if (!Form.CanEdit(Path))
        {
            _editing = null;
            if (_invalid) { _invalid = false; _ = NotifyValidityAsync(); }
        }
        if (_invalid && !Form.HasInputError(Path))
        {
            _invalid = false;
            _editing = null;
            _ = NotifyValidityAsync();
        }
        string? current = Value is { } value ? Display(value) : null;
        if (_observedText != current) _editing = null;
        _observedText = current;
    }
    private void Edit(ChangeEventArgs args)
    {
        string selection = args.Value?.ToString() ?? "";
        if (selection.Length == 0) { Omit(); return; }
        if (HasEmptyChoice)
        {
            if (!int.TryParse(selection, out int index) || index < 0 ||
                index >= Schema.GetProperty("choices").GetArrayLength()) return;
            _invalid = !Form.SetValue(Path, Schema.GetProperty("choices")[index]);
            _editing = null;
            _ = NotifyValidityAsync();
            return;
        }
        EditText(selection);
    }
    private void EditText(string? suppliedText)
    {
        string text = suppliedText ?? string.Empty;
        _editing = text;
        _invalid = !Form.EditValue(Path, text);
        _observedText = Value is { } supplied ? Display(supplied) : null;
        _ = NotifyValidityAsync();
    }
    private Task NotifyValidityAsync() => ValidationChanged.InvokeAsync((ControlId, Path[0], _invalid));
    private void Omit()
    {
        if (!Form.EditValue(Path, null)) return;
        _editing = null;
        _invalid = false;
        _focusAfterOmit = true;
        _ = NotifyValidityAsync();
    }
    private void Add()
    {
        _focusNewRow = Count;
        _rowKeys.Add(Guid.NewGuid());
        if (!Form.AddListItem(Path)) { _rowKeys.RemoveAt(_rowKeys.Count - 1); _focusNewRow = -1; }
    }
    private void SupplyObject() { _focusObjectChildren = true; if (!Form.SupplyObject(Path)) _focusObjectChildren = false; }
    private void SupplyList() { if (Form.SetValue(Path, JsonSerializer.SerializeToElement(Array.Empty<object>()))) _focusAdd = true; }
    private void Remove(int index)
    {
        Guid identity = _rowKeys[index];
        _rowKeys.RemoveAt(index);
        if (!Form.RemoveListItem(Path, index)) { _rowKeys.Insert(index, identity); return; }
        _focusAdd = true;
    }
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        _focusNewRow = -1;
        if (_focusAfterOmit)
        {
            _focusAfterOmit = false;
            if (Type is "object" or "list") await _group.FocusAsync(preventScroll: true);
            else if (_input is not null) await _input.FocusAsync();
            else await _control.FocusAsync(preventScroll: true);
        }
        if (_focusObjectChildren) { _focusObjectChildren = false; if (!CanFocus(Schema, Path)) await _group.FocusAsync(preventScroll: true); }
        if (FocusFirstControl && !_focusedForRequest && Form.CanEdit(Path) && Type != "object" && (Type != "list" || Count == 0))
        {
            _focusedForRequest = true;
            if (Type == "list") await _addButton.FocusAsync(preventScroll: true);
            else if (_input is not null) await _input.FocusAsync();
            else await _control.FocusAsync(preventScroll: true);
        }
        if (!_focusAdd) return;
        _focusAdd = false;
        await _addButton.FocusAsync(preventScroll: true);
    }
    private bool CanFocus(JsonElement schema, IReadOnlyList<string> path)
    {
        if (!Form.CanEdit(path)) return false;
        if (schema.GetProperty("type").GetString() != "object") return true;
        return schema.GetProperty("properties").EnumerateArray().Any(child => CanFocus(child, path.Append(child.GetProperty("id").GetString()!).ToArray()));
    }
    private static string Display(JsonElement value) => value.ValueKind == JsonValueKind.String ? value.GetString()! : value.GetRawText();
    public void Dispose() { if (_invalid) { _invalid = false; _ = NotifyValidityAsync(); } GC.SuppressFinalize(this); }
}
