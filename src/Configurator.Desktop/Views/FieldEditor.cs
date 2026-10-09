namespace Configurator.Desktop.Views;

using System.Globalization;
using System.Text.Json;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Media;
using Configurator.Presentation;

/// <summary>Renders scalar controls, nested object fields, and typed editable list rows.</summary>
public sealed class FieldEditor : StackPanel
{
    private static readonly string[] ConstraintNames = ["minimum", "maximum", "minLength", "maxLength", "minItems", "maxItems"];
    private static readonly string[] UnsetChoice = [""];
    private readonly JsonElement _schema;
    private readonly bool _masked;
    private readonly StackPanel _body = new() { Spacing = 6 };
    private readonly TextBlock _error = new() { TextWrapping = TextWrapping.Wrap };
    private readonly List<(string Id, FieldEditor Editor)> _properties = [];
    private readonly List<FieldEditor> _items = [];
    private Control? _input;
    private bool _provided;
    private bool _building;

    internal FieldEditor(JsonElement schema, bool masked, JsonElement? initial)
    {
        _schema = schema;
        _masked = masked || schema.TryGetProperty("sensitivity", out var sensitivity) &&
            sensitivity.GetString() != "public";
        Spacing = 5;
        Children.Add(new TextBlock { Text = Title, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap });
        foreach (string name in new[] { "group", "help", "consequence", "format" })
            if (schema.TryGetProperty(name, out var value))
                Children.Add(new TextBlock { Text = $"{name}: {value.GetString()}", TextWrapping = TextWrapping.Wrap });
        string bounds = string.Join(" | ", ConstraintNames
            .Where(name => schema.TryGetProperty(name, out _))
            .Select(name => $"{name}: {schema.GetProperty(name).GetRawText()}"));
        if (bounds.Length > 0) Children.Add(new TextBlock { Text = bounds, TextWrapping = TextWrapping.Wrap });
        if (_masked) Children.Add(new TextBlock { Text = "Protected input — values are masked." });
        Children.Add(_body);
        Children.Add(_error);
        Build(initial);
        if (!_masked && schema.TryGetProperty("recommended", out var recommended))
        {
            var button = new Button { Content = "_Use recommended value" };
            button.Click += (_, _) => { Build(recommended); _provided = true; Changed?.Invoke(this, EventArgs.Empty); FocusInput(); };
            Children.Add(button);
        }
    }

    public event EventHandler? Changed;
    public bool HasError { get; private set; }
    public string Title => _schema.GetProperty("title").GetString()!;
    public string AnswerText
    {
        get
        {
            if (!_provided) return string.Empty;
            if (_schema.GetProperty("type").GetString() is "string" or "enum")
                return _input is TextBox text ? text.Text ?? string.Empty :
                    (_input as ComboBox)?.SelectedItem as string ?? string.Empty;
            if (!TryValue(out var value)) return "invalid";
            return value?.GetRawText() ?? string.Empty;
        }
    }
    public void SetError(bool invalid, bool required)
    {
        HasError = invalid;
        _error.Text = invalid ? "A valid value is required. Check the declared type and constraints."
            : required ? "Required" : "Optional";
    }
    public void Clear() { Build(null); _provided = false; Changed?.Invoke(this, EventArgs.Empty); }
    public void FocusInput()
    {
        if (_input is not null) _input.Focus();
        else if (_properties.Count > 0) _properties[0].Editor.FocusInput();
        else if (_items.Count > 0) _items[0].FocusInput();
    }
    private void ChangedInput()
    {
        if (_building || (!_provided && !HasInput())) return;
        _provided = true;
        Changed?.Invoke(this, EventArgs.Empty);
    }
    private bool HasInput() => _schema.GetProperty("type").GetString() switch
    {
        "object" => _properties.Any(item => item.Editor.HasInput()),
        "list" => _items.Count > 0,
        _ => _input is TextBox { Text: { Length: > 0 } } ||
            _input is ComboBox { SelectedItem: string { Length: > 0 } }
    };
    private void Build(JsonElement? initial)
    {
        _building = true;
        _body.Children.Clear();
        _properties.Clear();
        _items.Clear();
        _input = null;
        _provided = initial is not null;
        string type = _schema.GetProperty("type").GetString()!;
        if (type == "object")
        {
            foreach (var property in _schema.GetProperty("properties").EnumerateArray())
            {
                string id = property.GetProperty("id").GetString()!;
                JsonElement? child = initial is { ValueKind: JsonValueKind.Object } obj && obj.TryGetProperty(id, out var value)
                    ? value : property.TryGetProperty("default", out var fallback) ? fallback : null;
                var editor = new FieldEditor(property, _masked, child) { Margin = new Thickness(16, 0, 0, 0) };
                editor.Changed += (_, _) => ChangedInput();
                _properties.Add((id, editor));
                _body.Children.Add(editor);
            }
        }
        else if (type == "list")
        {
            var rows = new StackPanel { Spacing = 6 };
            _body.Children.Add(rows);
            if (initial is { ValueKind: JsonValueKind.Array } array)
                foreach (var item in array.EnumerateArray()) AddItem(rows, item);
            var add = new Button { Content = "_Add item" };
            _input = add;
            AutomationProperties.SetName(add, $"Add item to {Title}");
            add.Click += (_, _) =>
            {
                int maximum = _schema.TryGetProperty("maxItems", out var max) ? max.GetInt32() : 4096;
                if (_items.Count >= maximum) return;
                var editor = AddItem(rows, null);
                ChangedInput();
                editor.FocusInput();
            };
            _body.Children.Add(add);
        }
        else if (type is "boolean" or "enum")
        {
            string[] choices = type == "boolean" ? ["", "true", "false"] :
                UnsetChoice.Concat(_schema.GetProperty("choices").EnumerateArray().Select(Display)).ToArray();
            var combo = new ComboBox
            {
                ItemsSource = choices,
                SelectedItem = initial is { } value ? Display(value) : "",
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch
            };
            if (_masked)
                combo.SelectionBoxItemTemplate = new FuncDataTemplate<string>((_, _) => new TextBlock { Text = "••••••" });
            AutomationProperties.SetName(combo, Title);
            AutomationProperties.SetHelpText(combo, Help);
            combo.SelectionChanged += (_, _) => ChangedInput();
            _input = combo;
            _body.Children.Add(combo);
        }
        else
        {
            var text = new TextBox
            {
                Text = initial is { } value ? Display(value) : string.Empty,
                MaxLength = 65_536,
                PasswordChar = _masked ? '●' : '\0'
            };
            AutomationProperties.SetName(text, Title);
            AutomationProperties.SetHelpText(text, Help);
            text.TextChanged += (_, _) => ChangedInput();
            _input = text;
            _body.Children.Add(text);
        }
        _building = false;
    }
    private FieldEditor AddItem(StackPanel rows, JsonElement? initial)
    {
        string type = _schema.GetProperty("itemType").GetString()!;
        var schema = JsonSerializer.SerializeToElement(new
        {
            id = "item",
            title = $"{Title} item",
            type,
            sensitivity = _masked ? "secret" : "public",
            properties = _schema.TryGetProperty("properties", out var properties) ? properties
                : JsonSerializer.SerializeToElement(Array.Empty<object>())
        });
        if (type == "object" && initial is null) initial = DefinitionCollectionItem.CreateObject(_schema);
        var editor = new FieldEditor(schema, _masked, initial);
        var row = new StackPanel { Spacing = 4 };
        row.Children.Add(editor);
        var remove = new Button { Content = "_Remove item" };
        AutomationProperties.SetName(remove, $"Remove item from {Title}");
        remove.Click += (_, _) =>
        {
            _items.Remove(editor);
            rows.Children.Remove(row);
            ChangedInput();
            _input?.Focus();
        };
        row.Children.Add(remove);
        editor.Changed += (_, _) => ChangedInput();
        _items.Add(editor);
        rows.Children.Add(row);
        return editor;
    }
    private string Help => _schema.TryGetProperty("help", out var help) ? help.GetString()! : Title;
    private bool TryValue(out JsonElement? value)
    {
        value = null;
        if (!_provided) return true;
        string type = _schema.GetProperty("type").GetString()!;
        if (type == "object")
        {
            var values = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            foreach (var (id, editor) in _properties)
            {
                if (!editor.TryValue(out var child)) return false;
                if (child is { } supplied) values.Add(id, supplied);
            }
            value = JsonSerializer.SerializeToElement(values);
            return true;
        }
        if (type == "list")
        {
            var values = new List<JsonElement>();
            foreach (var editor in _items)
            {
                if (!editor.TryValue(out var child) || child is null) return false;
                values.Add(child.Value);
            }
            value = JsonSerializer.SerializeToElement(values);
            return true;
        }
        string text = _input is TextBox box ? box.Text ?? "" : (_input as ComboBox)?.SelectedItem as string ?? "";
        if (text.Length == 0) return true;
        switch (type)
        {
            case "string": case "enum": value = JsonSerializer.SerializeToElement(text); return true;
            case "boolean":
                if (!bool.TryParse(text, out bool boolean)) return false;
                value = JsonSerializer.SerializeToElement(boolean); return true;
            case "integer":
                if (!long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long integer)) return false;
                value = JsonSerializer.SerializeToElement(integer); return true;
            case "number":
                if (!decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal number)) return false;
                value = JsonSerializer.SerializeToElement(number); return true;
            default: return false;
        }
    }
    private static string Display(JsonElement value) => value.ValueKind == JsonValueKind.String
        ? value.GetString()! : value.GetRawText();
}
