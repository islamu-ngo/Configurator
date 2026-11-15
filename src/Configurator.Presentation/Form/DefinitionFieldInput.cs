namespace Configurator.Presentation;

using System.ComponentModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using Configurator.Core.Definitions;

public sealed class DefinitionFieldInput : ObservableObject
{
    private readonly object _gate;
    private readonly Func<DefinitionFieldState> _readState;
    private readonly Action<Func<bool>> _changed;
    private readonly Func<string, JsonElement?, bool> _allowsValue;
    private readonly Func<JsonElement?> _readEffective;
    private readonly Action<IReadOnlyList<string>, bool> _recordOverride;
    private readonly Action<Action> _notify;
    private DefinitionFieldState State => _readState();

    internal DefinitionFieldInput(FieldDefinition definition, object gate, Func<DefinitionFieldState> readState,
        Action<Func<bool>> changed,
        Func<string, JsonElement?, bool> allowsValue, Func<JsonElement?> readEffective,
        Action<IReadOnlyList<string>, bool> recordOverride, Action<Action> notify)
    {
        Definition = definition;
        _gate = gate;
        _readState = readState;
        _changed = changed;
        _allowsValue = allowsValue;
        _readEffective = readEffective;
        _recordOverride = recordOverride;
        _notify = notify;
    }

    public FieldDefinition Definition { get; }
    public string Title => Definition.Title;
    public string? Help => Definition.Help;
    public bool RequiresMasking => Definition.Sensitivity != FieldSensitivity.Public;
    public bool HasAnswerBudgetError => InputDiagnosticCode == "answer-budget-exceeded";
    public string? InputDiagnosticCode
    {
        get
        {
            lock (_gate)
            {
                DefinitionFieldState state = State;
                return state.InputDiagnosticCode ?? (state.HasInvalidPaths ? "input-type-invalid" : null);
            }
        }
    }
    internal JsonElement? Value => State.Value;
    internal long Version => State.Version;
    internal bool IsPristineDefault => State.Value is null && State.Origin == DefinitionAnswerOrigin.Omitted
        && !State.HasOrigins && InputDiagnosticCode is null;
    public IReadOnlyList<string> Choices =>
        Array.AsReadOnly(Definition.Choices.Select(Display).ToArray());

    internal IEnumerable<DefinitionDiagnostic> InputDiagnostics
    {
        get
        {
            DefinitionFieldState state = State;
            if (state.InputDiagnosticCode is not null)
                yield return new(state.InputDiagnosticCode, Definition.Id);
            if (!state.HasInvalidPaths) yield break;
            foreach (string[] path in state.InvalidPaths)
                if (state.InputDiagnosticCode is null || path.Length > 1)
                    yield return new("input-type-invalid", Definition.Id, path.Skip(1).ToArray());
        }
    }

    public string Text
    {
        get
        {
            lock (_gate)
                return !HasInputError([Definition.Id]) && _readEffective() is { } effective ? Display(effective) : State.Text;
        }
        set => _changed(() =>
        {
            bool valid = TryValue(value, out JsonElement? parsed);
            if (!_allowsValue(value, valid ? parsed : State.Value))
            {
                if (!valid) SetDraftValidity([Definition.Id], false);
                return true;
            }
            SetDraftValidity([Definition.Id], valid);
            if (valid)
            {
                ClearDrafts([Definition.Id]);
                State.Value = parsed?.Clone();
                RecordOrigin([Definition.Id], parsed is null ? DefinitionAnswerOrigin.Omitted : DefinitionAnswerOrigin.Explicit);
            }
            SetProperty(ref State.Text, value);
            State.Touch();
            return true;
        });
    }

    internal bool TrySetValue(JsonElement? value, bool recordOrigin = true)
    {
        if (value is { } typed && !(typed.ValueKind == JsonValueKind.Null && Definition.Node.IsNullable) &&
            (Definition.Node.Type == DefinitionNodeType.Mapping && typed.ValueKind != JsonValueKind.Object ||
            Definition.Node.Type == DefinitionNodeType.Sequence && typed.ValueKind != JsonValueKind.Array)) return false;
        string text = value is { } supplied ? Display(supplied) : string.Empty;
        bool accepted = false;
        _changed(() =>
        {
            accepted = _allowsValue(text, value);
            if (accepted)
            {
                State.Value = value?.Clone();
                if (recordOrigin) RecordOrigin([Definition.Id],
                    value is null ? DefinitionAnswerOrigin.Omitted : DefinitionAnswerOrigin.Explicit);
                SetInputDiagnostic(null);
                if (!SetProperty(ref State.Text, text, nameof(Text))) OnPropertyChanged(nameof(Text));
                State.Touch();
            }
            return true;
        });
        return accepted;
    }

    internal void SetImported(JsonElement value)
    {
        SetInputDiagnostic(null);
        if (State.HasInvalidPaths) State.InvalidPaths.Clear();
        if (State.HasDraftTexts) State.DraftTexts.Clear();
        RecordOrigin([Definition.Id], DefinitionAnswerOrigin.Imported);
        RetainValue(value);
    }

    internal void RetainValue(JsonElement value)
    {
        State.Value = value.Clone();
        if (!SetProperty(ref State.Text, Display(value), nameof(Text))) OnPropertyChanged(nameof(Text));
    }

    internal IEnumerable<string[]> PrivateStatePaths()
    {
        if (State.InputDiagnosticCode is not null) yield return [Definition.Id];
        if (State.HasDraftTexts)
            foreach ((string[] path, _) in State.DraftTexts) yield return path;
        if (State.HasInvalidPaths)
            foreach (string[] path in State.InvalidPaths) yield return path;
        if (State.HasOrigins)
            foreach ((string[] path, _) in State.Origins) yield return path;
    }

    internal void RetainPublic(JsonElement? value, Func<string[], bool> clearDraft, Func<string[], bool> forgetOrigin)
    {
        State.Value = value?.Clone();
        if (State.HasDraftTexts) State.DraftTexts.RemoveAll(draft => clearDraft(draft.Path));
        if (State.HasInvalidPaths) State.InvalidPaths.RemoveAll(path => clearDraft(path));
        if (State.HasOrigins) State.Origins.RemoveAll(origin => forgetOrigin(origin.Path));
        string text = value is { } retained ? Display(retained) : string.Empty;
        if (!SetProperty(ref State.Text, text, nameof(Text))) OnPropertyChanged(nameof(Text));
        if (clearDraft([Definition.Id])) SetInputDiagnostic(null);
    }

    internal void NotifyDocumentChanged()
    {
        OnPropertyChanged(nameof(Text));
        OnPropertyChanged(nameof(InputDiagnosticCode));
    }

    internal void Clear()
    {
        State.Value = null;
        if (State.HasInvalidPaths) State.InvalidPaths.Clear();
        if (State.HasDraftTexts) State.DraftTexts.Clear();
        RecordOrigin([Definition.Id], DefinitionAnswerOrigin.Omitted);
        if (!SetProperty(ref State.Text, string.Empty, nameof(Text))) OnPropertyChanged(nameof(Text));
        SetInputDiagnostic(null);
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e) =>
        _notify(() => base.OnPropertyChanged(e));

    protected override void OnPropertyChanging(PropertyChangingEventArgs e) =>
        _notify(() => base.OnPropertyChanging(e));

    public bool TryValue(out JsonElement? value)
    {
        lock (_gate)
        {
            bool valid = !HasInputError([Definition.Id]);
            value = valid ? _readEffective() : null;
            return valid;
        }
    }

    internal bool HasInputError(IReadOnlyList<string> path) =>
        path.Count == 1 && State.InputDiagnosticCode is not null ||
        State.HasInvalidPaths && State.InvalidPaths.Any(existing => existing.SequenceEqual(path));

    internal bool HasRootInputError()
    {
        if (State.InputDiagnosticCode is not null) return true;
        if (!State.HasInvalidPaths) return false;
        foreach (string[] existing in State.InvalidPaths)
        {
            if (existing.Length == 1 && string.Equals(existing[0], Definition.Id, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    internal DefinitionAnswerOrigin OriginAtRoot() => State.Origin;

    internal void SetDraftValidity(IReadOnlyList<string> path, bool valid)
    {
        if (valid && !State.HasInvalidPaths) return;
        if (State.HasInvalidPaths) State.InvalidPaths.RemoveAll(existing => existing.SequenceEqual(path));
        if (!valid) State.InvalidPaths.Add(path.ToArray());
        State.Touch();
        OnPropertyChanged(nameof(InputDiagnosticCode));
    }

    internal bool ClearDrafts(IReadOnlyList<string> path)
    {
        int removedDrafts = State.HasDraftTexts ? State.DraftTexts.RemoveAll(existing => existing.Path.Take(path.Count).SequenceEqual(path)) : 0;
        bool changed = State.HasInvalidPaths && State.InvalidPaths.RemoveAll(existing => existing.Take(path.Count).SequenceEqual(path)) > 0;
        if (removedDrafts > 0 || changed) State.Touch();
        if (changed) OnPropertyChanged(nameof(InputDiagnosticCode));
        return changed;
    }

    internal void ClearDeniedDrafts(Func<IReadOnlyList<string>, bool> canEdit)
    {
        int removedDrafts = State.HasDraftTexts ? State.DraftTexts.RemoveAll(existing => !canEdit(existing.Path)) : 0;
        bool changed = State.HasInvalidPaths && State.InvalidPaths.RemoveAll(path => !canEdit(path)) > 0;
        if (removedDrafts > 0 || changed) State.Touch();
        if (changed) OnPropertyChanged(nameof(InputDiagnosticCode));
    }

    internal string? DraftText(IReadOnlyList<string> path) =>
        path.Count == 1 && HasInputError(path) ? State.Text :
            State.HasDraftTexts ? State.DraftTexts.FirstOrDefault(existing => existing.Path.SequenceEqual(path)).Text : null;

    internal void RetainDraft(IReadOnlyList<string> path, string? text)
    {
        if (State.HasDraftTexts) State.DraftTexts.RemoveAll(existing => existing.Path.SequenceEqual(path));
        if (text is not null && text.Length <= 65_536 &&
            (!State.HasDraftTexts || State.DraftTexts.Sum(existing => existing.Text.Length) + text.Length <= 65_536))
            State.DraftTexts.Add((path.ToArray(), text));
        State.Touch();
    }

    internal DefinitionAnswerOrigin OriginAt(IReadOnlyList<string> path) =>
        path.Count == 1 || !State.HasOrigins ? State.Origin :
        State.Origins.Where(existing => path.Take(existing.Path.Length).SequenceEqual(existing.Path))
            .OrderByDescending(existing => existing.Path.Length).Select(existing => existing.Origin)
            .FirstOrDefault(State.Origin);

    internal void RecordOrigin(IReadOnlyList<string> path, DefinitionAnswerOrigin origin, bool clearDescendants = true)
    {
        if (clearDescendants && origin != DefinitionAnswerOrigin.Defaulted)
            _recordOverride(path, origin == DefinitionAnswerOrigin.Omitted);
        if (State.HasOrigins)
            State.Origins.RemoveAll(existing => existing.Path.SequenceEqual(path) ||
                clearDescendants && existing.Path.Take(path.Count).SequenceEqual(path));
        if (path.Count == 1) State.Origin = origin;
        else State.Origins.Add((path.ToArray(), origin));
        State.Touch();
    }

    internal void ForgetOrigin(IReadOnlyList<string> path)
    {
        if (State.HasOrigins) State.Origins.RemoveAll(existing => existing.Path.Take(path.Count).SequenceEqual(path));
        if (path.Count == 1) State.Origin = DefinitionAnswerOrigin.Omitted;
        State.Touch();
    }

    internal void RemoveItemDrafts(IReadOnlyList<string> path, int index)
    {
        RemoveItemDrafts(State, path, index);
        State.Touch();
        OnPropertyChanged(nameof(InputDiagnosticCode));
    }

    internal static void RemoveItemDrafts(DefinitionFieldState state, IReadOnlyList<string> path, int index)
    {
        if (!state.HasDraftTexts && !state.HasOrigins && !state.HasInvalidPaths) return;
        static bool IsRow(string[] candidate, IReadOnlyList<string> prefix, int row) =>
            candidate.Length > prefix.Count && candidate.Take(prefix.Count).SequenceEqual(prefix) &&
            candidate[prefix.Count] == row.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (state.HasDraftTexts) state.DraftTexts.RemoveAll(existing => IsRow(existing.Path, path, index));
        if (state.HasOrigins) state.Origins.RemoveAll(existing => IsRow(existing.Path, path, index));
        if (state.HasDraftTexts)
            foreach ((string[] existing, _) in state.DraftTexts)
                if (existing.Length > path.Count && existing.Take(path.Count).SequenceEqual(path) &&
                    int.TryParse(existing[path.Count], out int row) && row > index)
                    existing[path.Count] = (row - 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (state.HasOrigins)
            foreach ((string[] existing, _) in state.Origins)
                if (existing.Length > path.Count && existing.Take(path.Count).SequenceEqual(path) &&
                    int.TryParse(existing[path.Count], out int row) && row > index)
                    existing[path.Count] = (row - 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (!state.HasInvalidPaths) return;
        state.InvalidPaths.RemoveAll(existing => existing.Length > path.Count &&
            existing.Take(path.Count).SequenceEqual(path) && existing[path.Count] == index.ToString(System.Globalization.CultureInfo.InvariantCulture));
        foreach (string[] existing in state.InvalidPaths.Where(existing => existing.Length > path.Count &&
            existing.Take(path.Count).SequenceEqual(path)))
            if (int.TryParse(existing[path.Count], out int row) && row > index)
                existing[path.Count] = (row - 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    internal void SetInputDiagnostic(string? code)
    {
        if (State.InputDiagnosticCode == code) return;
        State.InputDiagnosticCode = code;
        State.Touch();
        OnPropertyChanged(nameof(InputDiagnosticCode));
        OnPropertyChanged(nameof(HasAnswerBudgetError));
    }

    internal bool TryValue(string text, out JsonElement? value)
    {
        value = null;
        if (text.Length > 65_536) return false;
        switch (Definition.Type)
        {
            case "array":
            case "object":
                if (text.Length == 0) return true;
                try
                {
                    using JsonDocument document = JsonDocument.Parse(
                        text, new JsonDocumentOptions { MaxDepth = 32 });
                    JsonValueKind required = Definition.Node.Type == DefinitionNodeType.Sequence
                        ? JsonValueKind.Array : JsonValueKind.Object;
                    if (document.RootElement.ValueKind != required &&
                        !(document.RootElement.ValueKind == JsonValueKind.Null && Definition.Node.IsNullable)) return false;
                    value = document.RootElement.Clone();
                    return true;
                }
                catch (JsonException)
                {
                    return false;
                }
            default:
                return DefinitionScalarEditing.TryParse(Definition.Type, text, Definition.Constraints, out value);
        }
    }

    private static string Display(JsonElement value) =>
        value.ValueKind == JsonValueKind.String ? value.GetString()! : value.GetRawText();
}
