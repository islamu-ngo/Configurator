namespace Configurator.Presentation;

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using CommunityToolkit.Mvvm.ComponentModel;
using Configurator.Core.Definitions;

/// <summary>
/// Keeps field editing independent of a target application and freezes
/// the admitted definition for the lifetime of the form.
/// </summary>
public sealed class DefinitionFormModel : ObservableObject, IDisposable
{
    private readonly object _gate = new();
    private long _inputRevision;
    private SetupDisplayMode _displayMode = SetupDisplayMode.Guided;
    private bool _disposed;

    public DefinitionFormModel(SetupDefinition definition,
        DefinitionSessionPrivacy sessionPrivacy = DefinitionSessionPrivacy.NativeComplete)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (!Enum.IsDefined(sessionPrivacy)) throw new ArgumentOutOfRangeException(nameof(sessionPrivacy));
        SessionPrivacy = sessionPrivacy;
        Definition = definition;
        Fields = Array.AsReadOnly(definition.Fields.Select(field =>
            new DefinitionFieldInput(field, Edit, text => AllowsText(field, text))).ToArray());
    }

    public SetupDefinition Definition { get; }
    public DefinitionSessionPrivacy SessionPrivacy { get; private set; }
    public void SetSecretEntryEnabled(bool enabled)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (SessionPrivacy == DefinitionSessionPrivacy.NativeComplete) return;
            DefinitionSessionPrivacy next = enabled ? DefinitionSessionPrivacy.BrowserSecretsEnabled
                : DefinitionSessionPrivacy.BrowserNoSecrets;
            if (SessionPrivacy == next) return;
            SessionPrivacy = next;
            if (!enabled)
                foreach (DefinitionFieldInput field in Fields)
                {
                    if (!field.TryValue(out JsonElement? value) || value is null)
                    {
                        if (field.Definition.Sensitivity == SetupFieldSensitivity.Secret) field.Clear();
                        continue;
                    }
                    JsonNode? retained = DefinitionSecretPolicy.RemoveSecrets(field.Definition.Constraints,
                        JsonNode.Parse(value.Value.GetRawText()));
                    if (retained is null) field.Clear();
                    else field.SetImported(JsonSerializer.SerializeToElement(retained));
                }
            OnPropertyChanged(nameof(SessionPrivacy));
            OnInputChanged();
        }
    }

    public bool CanEdit(IReadOnlyList<string> path)
    {
        if (SessionPrivacy != DefinitionSessionPrivacy.BrowserNoSecrets) return true;
        return TryMetadata(path, out JsonElement metadata, out bool inheritedSecret) &&
            !DefinitionSecretPolicy.IsSecret(metadata, inheritedSecret);
    }

    public bool SetValue(IReadOnlyList<string> path, JsonElement? value)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (path.Count == 0 || !TryMetadata(path, out _, out _) || value is not null && !CanEdit(path)) return false;
            DefinitionFieldInput field = Fields.Single(item => item.Definition.Id == path[0]);
            if (path.Count == 1) return field.TrySetValue(value);
            bool hasValue = field.TryValue(out JsonElement? existing) && existing is not null;
            if (!hasValue && value is null) return true;
            JsonNode root = hasValue
                ? JsonNode.Parse(existing!.Value.GetRawText())! : field.Definition.Type == "list" ? new JsonArray() : new JsonObject();
            JsonNode parent = root;
            for (int index = 1; index < path.Count - 1; index++)
            {
                string segment = path[index];
                if (parent is JsonArray array)
                {
                    if (!int.TryParse(segment, out int row) || row < 0 || row >= array.Count || array[row] is null) return false;
                    parent = array[row]!;
                }
                else if (parent is JsonObject mapping)
                {
                    if (mapping[segment] is null)
                    {
                        if (value is null) return true;
                        mapping[segment] = new JsonObject();
                    }
                    parent = mapping[segment]!;
                }
                else return false;
            }
            JsonNode? replacement = value is { } supplied ? JsonNode.Parse(supplied.GetRawText()) : null;
            if (parent is JsonObject obj)
            {
                if (replacement is null) obj.Remove(path[^1]);
                else obj[path[^1]] = replacement;
            }
            else if (parent is JsonArray items && int.TryParse(path[^1], out int row) && row >= 0 && row < items.Count)
                items[row] = replacement;
            else return false;
            return field.TrySetValue(JsonSerializer.SerializeToElement(root));
        }
    }

    public bool SupplyObject(IReadOnlyList<string> path)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!TryMetadata(path, out JsonElement metadata, out _) || metadata.GetProperty("type").GetString() != "object") return false;
            if (ReadNode(path) is JsonObject) return true;
            return SetValue(path, DefinitionCollectionItem.CreateObject(metadata));
        }
    }

    public bool SupplyList(IReadOnlyList<string> path)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!TryMetadata(path, out JsonElement metadata, out _) || metadata.GetProperty("type").GetString() != "list") return false;
            if (ReadNode(path) is JsonArray) return true;
            return SetValue(path, JsonSerializer.SerializeToElement(Array.Empty<object>()));
        }
    }

    public bool AddListItem(IReadOnlyList<string> path)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!TryMetadata(path, out JsonElement metadata, out _) || metadata.GetProperty("type").GetString() != "list") return false;
            JsonArray items = ReadNode(path) as JsonArray ?? [];
            int maximum = metadata.TryGetProperty("maxItems", out JsonElement limit) ? limit.GetInt32() : 4096;
            if (items.Count >= maximum) return false;
            string type = metadata.GetProperty("itemType").GetString()!;
            items.Add(type == "object" ? JsonNode.Parse(DefinitionCollectionItem.CreateObject(metadata).GetRawText()) : type switch
            {
                "string" => JsonValue.Create(string.Empty),
                "boolean" => JsonValue.Create(false),
                _ => JsonValue.Create(0)
            });
            return SetValue(path, JsonSerializer.SerializeToElement(items));
        }
    }

    public bool RemoveListItem(IReadOnlyList<string> path, int index)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (ReadNode(path) is not JsonArray items || index < 0 || index >= items.Count) return false;
            items.RemoveAt(index);
            return SetValue(path, JsonSerializer.SerializeToElement(items));
        }
    }

    private JsonNode? ReadNode(IReadOnlyList<string> path)
    {
        if (path.Count == 0 || Fields.FirstOrDefault(item => item.Definition.Id == path[0]) is not { } field ||
            !field.TryValue(out JsonElement? value) || value is null) return null;
        JsonNode? node = JsonNode.Parse(value.Value.GetRawText());
        foreach (string segment in path.Skip(1))
            node = node is JsonObject mapping ? mapping[segment] : node is JsonArray array && int.TryParse(segment, out int row)
                && row >= 0 && row < array.Count ? array[row] : null;
        return node;
    }

    private bool TryMetadata(IReadOnlyList<string> path, out JsonElement metadata, out bool inheritedSecret)
    {
        metadata = default;
        inheritedSecret = false;
        if (path.Count == 0 || Fields.FirstOrDefault(item => item.Definition.Id == path[0]) is not { } field) return false;
        metadata = field.Definition.Constraints;
        foreach (string segment in path.Skip(1))
        {
            inheritedSecret |= DefinitionSecretPolicy.IsSecret(metadata);
            if (metadata.GetProperty("type").GetString() == "list" && int.TryParse(segment, out int index) && index >= 0)
            {
                metadata = JsonSerializer.SerializeToElement(new
                {
                    type = metadata.GetProperty("itemType").GetString(),
                    properties = metadata.TryGetProperty("properties", out JsonElement properties) ? properties : JsonSerializer.SerializeToElement(Array.Empty<object>()),
                    sensitivity = "public"
                });
                continue;
            }
            if (!metadata.TryGetProperty("properties", out JsonElement children)) return false;
            JsonElement child = children.EnumerateArray().FirstOrDefault(item => item.GetProperty("id").GetString() == segment);
            if (child.ValueKind == JsonValueKind.Undefined) return false;
            metadata = child;
        }
        return true;
    }

    private bool AllowsText(SetupFieldDefinition field, string text)
    {
        DefinitionFieldInput input = Fields.Single(item => item.Definition.Id == field.Id);
        bool fitsBudget = text.Length <= 65_536;
        string? rejection = fitsBudget ? null : "answer-budget-exceeded";
        if (fitsBudget)
        {
            var values = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            foreach (DefinitionFieldInput current in Fields)
                if (current.Definition.Id != field.Id && current.TryValue(out JsonElement? existing) && existing is { } supplied)
                    values.Add(current.Definition.Id, supplied);
            if (input.TryValue(text, out JsonElement? candidate) && candidate is { } value) values[field.Id] = value;
            try { _ = new SetupAnswerSnapshot(_inputRevision, values); }
            catch (SetupDefinitionException exception)
            {
                fitsBudget = false;
                rejection = exception.Code is "answer-limit" or "scalar-invalid" or "document-limit"
                    ? "answer-budget-exceeded" : "answer-invalid";
            }
        }
        input.SetInputDiagnostic(rejection);
        if (!fitsBudget) return false;
        if (SessionPrivacy != DefinitionSessionPrivacy.BrowserNoSecrets || text.Length == 0) return true;
        JsonElement metadata = field.Constraints;
        if (DefinitionSecretPolicy.IsSecret(metadata)) return false;
        if (field.Type is not ("object" or "list")) return true;
        try
        {
            using JsonDocument parsed = JsonDocument.Parse(text, new JsonDocumentOptions { MaxDepth = 32 });
            return !DefinitionSecretPolicy.ContainsSecret(metadata, parsed.RootElement);
        }
        catch (JsonException) { return false; }
    }
    public IReadOnlyList<DefinitionFieldInput> Fields { get; }
    public long InputRevision => _inputRevision;
    public bool HasAnswerBudgetError => Fields.Any(input => input.HasAnswerBudgetError);
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
                (state.IsVisible(DisplayMode) || input.InputDiagnosticCode is not null ||
                    !input.TryValue(out _)))).ToArray());
        }
    }

    public bool HasInputErrors
    {
        get
        {
            if (Fields.Any(input => input.InputDiagnosticCode is not null)) return true;
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
        lock (_gate)
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
    }

    public DefinitionValidationResult Validate() =>
        DefinitionValidator.Validate(Definition, Snapshot());

    public async Task<DefinitionImportResult> ImportAsync(string artifactId,
        Func<CancellationToken, Task<ReadOnlyMemory<byte>>> read,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(read);
        SetupAnswerSnapshot original = Snapshot();
        if (cancellationToken.IsCancellationRequested)
            return new(false, null, "import-cancelled");
        ReadOnlyMemory<byte> bytes;
        try { bytes = await read(cancellationToken); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new(false, null, "import-cancelled");
        }
        try
        {
            if (cancellationToken.IsCancellationRequested) return new(false, null, "import-cancelled");
            DefinitionImportResult result = DefinitionImport.Assess(Definition, artifactId, bytes, original);
            if (!result.Succeeded) return result;
            lock (_gate)
            {
                if (_disposed || cancellationToken.IsCancellationRequested || _inputRevision != original.InputRevision)
                    return new(false, null, "import-stale");
                if (SessionPrivacy == DefinitionSessionPrivacy.BrowserNoSecrets && Fields.Any(field =>
                    result.Answers!.Values.TryGetValue(field.Definition.Id, out JsonElement value) &&
                    DefinitionSecretPolicy.ContainsSecret(field.Definition.Constraints, value)))
                    return new(false, null, "import-secret-consent-required");
                foreach (DefinitionFieldInput field in Fields)
                    if (result.Answers!.Values.TryGetValue(field.Definition.Id, out JsonElement value))
                        field.SetImported(value);
                OnInputChanged();
                return result;
            }
        }
        finally
        {
            if (System.Runtime.InteropServices.MemoryMarshal.TryGetArray(bytes, out ArraySegment<byte> buffer))
                System.Security.Cryptography.CryptographicOperations.ZeroMemory(buffer.AsSpan());
        }
    }

    private void Edit(Func<bool> edit)
    {
        lock (_gate)
        {
            if (_disposed) return;
            bool hadAdmissionError = Fields.Any(input => input.InputDiagnosticCode is not null);
            if (edit() || hadAdmissionError != Fields.Any(input => input.InputDiagnosticCode is not null)) OnInputChanged();
        }
    }

    private void OnInputChanged()
    {
        checked { _inputRevision++; }
        OnPropertyChanged(nameof(InputRevision));
        OnPropertyChanged(nameof(HasInputErrors));
        OnPropertyChanged(nameof(HasAnswerBudgetError));
        OnPropertyChanged(nameof(VisibleFields));
        InputChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            foreach (var field in Fields) field.Clear();
            InputChanged = null;
        }
    }
}

public sealed class DefinitionFieldInput : ObservableObject
{
    private readonly Action<Func<bool>> _changed;
    private readonly Func<string, bool> _allowsText;
    private string _text;

    internal DefinitionFieldInput(SetupFieldDefinition definition, Action<Func<bool>> changed, Func<string, bool> allowsText)
    {
        Definition = definition;
        _changed = changed;
        _allowsText = allowsText;
        _text = definition.Default is { } value ? Display(value) : string.Empty;
    }

    public SetupFieldDefinition Definition { get; }
    public string Title => Definition.Title;
    public string? Help => Definition.Help;
    public bool RequiresMasking => Definition.Sensitivity != SetupFieldSensitivity.Public;
    public bool HasAnswerBudgetError => InputDiagnosticCode == "answer-budget-exceeded";
    public string? InputDiagnosticCode { get; private set; }
    public IReadOnlyList<string> Choices =>
        Array.AsReadOnly(Definition.Choices.Select(Display).ToArray());

    public string Text
    {
        get => _text;
        set => _changed(() => _allowsText(value) && SetProperty(ref _text, value));
    }

    public bool TrySetValue(JsonElement? value)
    {
        if (value is { } typed && (Definition.Type == "object" && typed.ValueKind != JsonValueKind.Object ||
            Definition.Type == "list" && typed.ValueKind != JsonValueKind.Array)) return false;
        string text = value is { } supplied ? Display(supplied) : string.Empty;
        bool accepted = false;
        _changed(() =>
        {
            accepted = _allowsText(text);
            return accepted && SetProperty(ref _text, text, nameof(Text));
        });
        return accepted;
    }

    internal void SetImported(JsonElement value)
    {
        SetInputDiagnostic(null);
        SetProperty(ref _text, Display(value), nameof(Text));
    }

    internal void Clear()
    {
        _text = string.Empty;
        SetInputDiagnostic(null);
        OnPropertyChanged(nameof(Text));
    }

    public bool TryValue(out JsonElement? value)
        => TryValue(_text, out value);

    internal void SetInputDiagnostic(string? code)
    {
        if (InputDiagnosticCode == code) return;
        InputDiagnosticCode = code;
        OnPropertyChanged(nameof(InputDiagnosticCode));
        OnPropertyChanged(nameof(HasAnswerBudgetError));
    }

    internal bool TryValue(string text, out JsonElement? value)
    {
        value = null;
        if (text.Length == 0) return true;
        if (text.Length > 65_536) return false;
        switch (Definition.Type)
        {
            case "string":
                value = JsonSerializer.SerializeToElement(text);
                return true;
            case "enum":
                foreach (var choice in Definition.Choices)
                    if (Display(choice) == text)
                    {
                        value = choice.Clone();
                        return true;
                    }
                return false;
            case "boolean":
                if (!bool.TryParse(text, out bool boolean)) return false;
                value = JsonSerializer.SerializeToElement(boolean);
                return true;
            case "integer":
                if (!long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long integer))
                    return false;
                value = JsonSerializer.SerializeToElement(integer);
                return true;
            case "number":
                if (!decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal number))
                    return false;
                value = JsonSerializer.SerializeToElement(number);
                return true;
            case "list":
            case "object":
                try
                {
                    using JsonDocument document = JsonDocument.Parse(
                        text, new JsonDocumentOptions { MaxDepth = 32 });
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
