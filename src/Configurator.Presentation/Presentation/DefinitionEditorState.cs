namespace Configurator.Presentation;

using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Configurator.Core.Definitions;

public enum DefinitionAnswerOrigin { Omitted, Defaulted, Imported, Explicit }

public sealed partial class DefinitionFormModel
{
    private readonly Dictionary<string, List<long>> _rowIdentities = new(StringComparer.Ordinal);
    private long _nextRowIdentity;

    /// <summary>Reads transient invalid editor text only through the current workspace custody fence.</summary>
    public string? ReadDraftText(IReadOnlyList<string> path)
    {
        ArgumentNullException.ThrowIfNull(path);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!TryMetadata(path, out _, out _) || !CanEdit(path)) return null;
            return Fields.Single(input => input.Definition.Id == path[0]).DraftText(path);
        }
    }

    public DefinitionAnswerOrigin AnswerOrigin(IReadOnlyList<string> path)
    {
        ArgumentNullException.ThrowIfNull(path);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!TryMetadata(path, out JsonElement metadata, out _)) return DefinitionAnswerOrigin.Omitted;
            if (ReadNode(path) is null)
                return path.Count == 1 && metadata.TryGetProperty("default", out _)
                    ? DefinitionAnswerOrigin.Defaulted : DefinitionAnswerOrigin.Omitted;
            return Fields.Single(input => input.Definition.Id == path[0]).OriginAt(path);
        }
    }

    /// <summary>Identifies a logical editor without incorporating answers; surviving rows retain their token.</summary>
    public string GetEditorIdentity(IReadOnlyList<string> path)
    {
        ArgumentNullException.ThrowIfNull(path);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!TryMetadata(path, out _, out _)) throw new ArgumentException("editor-path-invalid", nameof(path));
            static string Encode(string segment) => Convert.ToHexString(Encoding.UTF8.GetBytes(segment));
            string identity = "field-" + Definition.FamilyId.ToString("N") + "-" + Encode(path[0]);
            var prefix = new List<string> { path[0] };
            foreach (string segment in path.Skip(1))
            {
                if (ReadNode(prefix) is JsonArray rows)
                {
                    EnsureRowIdentities(identity, rows.Count);
                    identity += "-row-" + _rowIdentities[identity][int.Parse(segment, CultureInfo.InvariantCulture)]
                        .ToString(CultureInfo.InvariantCulture);
                }
                else identity += "-key-" + Encode(segment);
                prefix.Add(segment);
            }
            return identity;
        }
    }

    private void EnsureRowIdentities(string identity, int count)
    {
        if (!_rowIdentities.TryGetValue(identity, out List<long>? rows))
            _rowIdentities.Add(identity, rows = []);
        while (rows.Count < count) rows.Add(checked(++_nextRowIdentity));
        if (rows.Count > count) rows.RemoveRange(count, rows.Count - count);
    }

    private void ClearRowIdentities(IReadOnlyList<string> path)
    {
        string identity = GetEditorIdentity(path);
        foreach (string key in _rowIdentities.Keys.Where(key => key == identity ||
            key.StartsWith(identity + "-", StringComparison.Ordinal)).ToArray())
            _rowIdentities.Remove(key);
    }

    private void RetainRowOrigins(IReadOnlyList<string> path, DefinitionAnswerOrigin[] origins)
    {
        DefinitionFieldInput input = Fields.Single(item => item.Definition.Id == path[0]);
        for (int index = 0; index < origins.Length; index++)
            input.RecordOrigin([.. path, index.ToString(CultureInfo.InvariantCulture)], origins[index], clearDescendants: false);
        input.RecordOrigin(path, DefinitionAnswerOrigin.Explicit, clearDescendants: false);
    }

    private void RecordMaterializedDefaults(IReadOnlyList<string> path, JsonElement metadata)
    {
        if (metadata.TryGetProperty("default", out _))
        {
            Fields.Single(input => input.Definition.Id == path[0]).RecordOrigin(path, DefinitionAnswerOrigin.Defaulted);
            return;
        }
        if (!metadata.TryGetProperty("properties", out JsonElement properties)) return;
        foreach (JsonElement child in properties.EnumerateArray())
        {
            string[] childPath = [.. path, child.GetProperty("id").GetString()!];
            if (ReadNode(childPath) is not null) RecordMaterializedDefaults(childPath, child);
        }
    }

    /// <summary>Opens exact literal ancestors for correction without changing answers or granting consent.</summary>
    public bool FocusEditor(IReadOnlyList<string> path)
    {
        ArgumentNullException.ThrowIfNull(path);
        using var mutation = BeginMutation();
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!TryMetadata(path, out _, out _) ||
            !Validate().Fields.Any(state => state.Id == path[0] && state.IsActive)) return false;
        DefinitionFormSection? section = Sections.FirstOrDefault(item => item.Fields.Contains(path[0]));
        if (section is not null && !Experiences.Single(item => item.Id == SelectedExperienceId).Sections.Contains(section.Id))
            SelectExperience(Experiences.First(item => item.Sections.Contains(section.Id)).Id);
        DiscoverySelection = new();
        for (int length = 1; length <= path.Count; length++)
            _promotedPaths.Add(GetEditorIdentity(path.Take(length).ToArray()));
        OnPropertyChanged(nameof(DiscoveryQuery));
        OnPropertyChanged(nameof(DiscoverySelection));
        OnViewChanged();
        return true;
    }
}
