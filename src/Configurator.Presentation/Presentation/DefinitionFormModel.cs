namespace Configurator.Presentation;

using System.Globalization;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using Configurator.Core.Definitions;

/// <summary>
/// Keeps native field editing independent of a target application and freezes
/// the admitted definition for the lifetime of the form.
/// </summary>
public sealed class DefinitionFormModel : ObservableObject, IDisposable
{
    private long _inputRevision;
    private SetupDisplayMode _displayMode = SetupDisplayMode.Guided;
    private bool _disposed;

    public DefinitionFormModel(SetupDefinition definition)
    {
        Definition = definition;
        Fields = Array.AsReadOnly(definition.Fields.Select(field =>
            new DefinitionFieldInput(field, OnInputChanged)).ToArray());
    }

    public SetupDefinition Definition { get; }
    public IReadOnlyList<DefinitionFieldInput> Fields { get; }
    public long InputRevision => _inputRevision;
    public event EventHandler? InputChanged;

    public SetupDisplayMode DisplayMode
    {
        get => _displayMode;
        set
        {
            if (SetProperty(ref _displayMode, value))
                OnPropertyChanged(nameof(VisibleFields));
        }
    }

    public IReadOnlyList<DefinitionFieldInput> VisibleFields
    {
        get
        {
            var states = DefinitionValidator.Validate(Definition, Snapshot()).Fields;
            return Array.AsReadOnly(Fields.Where(input => states.Any(state =>
                state.Id == input.Definition.Id && state.IsActive &&
                state.IsVisible(DisplayMode))).ToArray());
        }
    }

    public bool HasInputErrors
    {
        get
        {
            var active = DefinitionValidator.Validate(Definition, Snapshot()).Fields
                .Where(state => state.IsActive)
                .Select(state => state.Id)
                .ToHashSet(StringComparer.Ordinal);
            return Fields.Any(input => active.Contains(input.Definition.Id) &&
                !input.TryValue(out _));
        }
    }

    public SetupAnswerSnapshot Snapshot()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var values = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var field in Fields)
        {
            if (field.TryValue(out JsonElement? value) && value is { } supplied)
                values.Add(field.Definition.Id, supplied);
        }
        return new SetupAnswerSnapshot(_inputRevision, values);
    }

    public DefinitionValidationResult Validate() =>
        DefinitionValidator.Validate(Definition, Snapshot());

    private void OnInputChanged()
    {
        checked { _inputRevision++; }
        OnPropertyChanged(nameof(InputRevision));
        OnPropertyChanged(nameof(HasInputErrors));
        OnPropertyChanged(nameof(VisibleFields));
        InputChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var field in Fields) field.Clear();
        InputChanged = null;
    }
}

public sealed class DefinitionFieldInput : ObservableObject
{
    private readonly Action _changed;
    private string _text;

    internal DefinitionFieldInput(SetupFieldDefinition definition, Action changed)
    {
        Definition = definition;
        _changed = changed;
        _text = definition.Default is { } value ? Display(value) : string.Empty;
    }

    public SetupFieldDefinition Definition { get; }
    public string Title => Definition.Title;
    public string? Help => Definition.Help;
    public bool RequiresMasking => Definition.Sensitivity != SetupFieldSensitivity.Public;
    public IReadOnlyList<string> Choices =>
        Array.AsReadOnly(Definition.Choices.Select(Display).ToArray());

    public string Text
    {
        get => _text;
        set
        {
            if (SetProperty(ref _text, value))
                _changed();
        }
    }

    internal void Clear()
    {
        _text = string.Empty;
        OnPropertyChanged(nameof(Text));
    }

    public bool TryValue(out JsonElement? value)
    {
        value = null;
        if (_text.Length == 0) return true;
        if (_text.Length > 65_536) return false;
        switch (Definition.Type)
        {
            case "string":
                value = JsonSerializer.SerializeToElement(_text);
                return true;
            case "enum":
                foreach (var choice in Definition.Choices)
                    if (Display(choice) == _text)
                    {
                        value = choice.Clone();
                        return true;
                    }
                return false;
            case "boolean":
                if (!bool.TryParse(_text, out bool boolean)) return false;
                value = JsonSerializer.SerializeToElement(boolean);
                return true;
            case "integer":
                if (!long.TryParse(_text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long integer))
                    return false;
                value = JsonSerializer.SerializeToElement(integer);
                return true;
            case "number":
                if (!decimal.TryParse(_text, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal number))
                    return false;
                value = JsonSerializer.SerializeToElement(number);
                return true;
            case "list":
            case "object":
                try
                {
                    using JsonDocument document = JsonDocument.Parse(
                        _text, new JsonDocumentOptions { MaxDepth = 32 });
                    JsonValueKind required = Definition.Type == "list"
                        ? JsonValueKind.Array : JsonValueKind.Object;
                    if (document.RootElement.ValueKind != required) return false;
                    value = document.RootElement.Clone();
                    return true;
                }
                catch (JsonException)
                {
                    return false;
                }
            default:
                return false;
        }
    }

    private static string Display(JsonElement value) =>
        value.ValueKind == JsonValueKind.String ? value.GetString()! : value.GetRawText();
}
