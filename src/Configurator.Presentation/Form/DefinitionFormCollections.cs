namespace Configurator.Presentation;

using System.Text.Json;
using System.Text.Json.Nodes;
using Configurator.Core.Definitions;

public sealed partial class DefinitionFormModel
{
    /// <summary>Adds one literal map key without replacing an entry or exceeding the admitted bound.</summary>
    public bool AddMapEntry(IReadOnlyList<string> path, string key)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(key);
        using (BeginMutation())
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            string[] entryPath = [.. path, key];
            try { _ = DefinitionPath.FromSegments(entryPath); }
            catch (DefinitionException) { return false; }
            if (!TryMetadataNode(path, out DefinitionFormIndex.Node node, out _) ||
                node.Schema.AdditionalProperties is not { } entry || node.Schema.Properties.ContainsKey(key)) return false;
            JsonObject? current = ReadNode(path) as JsonObject;
            if (current?.ContainsKey(key) == true || (current?.Count ?? 0) >= MaximumMapEntries(node.Schema)) return false;
            JsonElement initial = entry.Type switch
            {
                DefinitionNodeType.Text => JsonSerializer.SerializeToElement(""),
                DefinitionNodeType.Boolean => JsonSerializer.SerializeToElement(false),
                DefinitionNodeType.WholeNumber or DefinitionNodeType.Number => JsonSerializer.SerializeToElement(0),
                DefinitionNodeType.Sequence => JsonSerializer.SerializeToElement(Array.Empty<object>()),
                _ => JsonSerializer.SerializeToElement(new Dictionary<string, object>())
            };
            return SetValue(entryPath, initial);
        }
    }

    private static int MaximumMapEntries(DefinitionNode schema) =>
        schema.Keywords.TryGetProperty("maxProperties", out JsonElement maximum)
            ? maximum.GetInt32() : Configurator.Core.Composition.CompositionLimits.DefaultMappingEntries;

    public bool SupplyObject(IReadOnlyList<string> path)
    {
        using (BeginMutation())
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!TryMetadataNode(path, out DefinitionFormIndex.Node node, out _) ||
                node.Schema.Type != DefinitionNodeType.Mapping) return false;
            if (ReadNode(path) is JsonObject) return true;
            return SetValue(path, DefinitionCollectionItem.CreateObject(node.Schema));
        }
    }

    public bool SupplyList(IReadOnlyList<string> path)
    {
        using (BeginMutation())
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!TryMetadataNode(path, out DefinitionFormIndex.Node node, out _) ||
                node.Schema.Type != DefinitionNodeType.Sequence) return false;
            if (ReadNode(path) is JsonArray) return true;
            return SetValue(path, JsonSerializer.SerializeToElement(Array.Empty<object>()));
        }
    }

    public bool AddListItem(IReadOnlyList<string> path)
    {
        using (BeginMutation())
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!TryMetadataNode(path, out DefinitionFormIndex.Node node, out _) ||
                node.Schema.Type != DefinitionNodeType.Sequence) return false;
            JsonArray items = ReadNode(path) as JsonArray ?? [];
            int maximum = node.Metadata.TryGetProperty("maxItems", out JsonElement limit) ? limit.GetInt32() : 4096;
            if (items.Count >= maximum) return false;
            DefinitionAnswerOrigin[] origins = Enumerable.Range(0, items.Count)
                .Select(index => AnswerOrigin([.. path, index.ToString(System.Globalization.CultureInfo.InvariantCulture)])).ToArray();
            RecordMaterializedOrigins(path);
            DefinitionNode item = node.Schema.Items!;
            items.Add(item.Type switch
            {
                DefinitionNodeType.Mapping => new JsonObject(),
                DefinitionNodeType.Sequence => new JsonArray(),
                DefinitionNodeType.Text => JsonValue.Create(string.Empty),
                DefinitionNodeType.Boolean => JsonValue.Create(false),
                _ => JsonValue.Create(0)
            });
            if (!ApplyValue(path, JsonSerializer.SerializeToElement(items), clearDrafts: false)) return false;
            RetainRowOrigins(path, origins);
            string[] addedPath = [.. path, origins.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)];
            Fields[_index!.FieldPositions[path[0]]].RecordOrigin(addedPath, DefinitionAnswerOrigin.Explicit);
            return true;
        }
    }

    public bool RemoveListItem(IReadOnlyList<string> path, int index)
    {
        using (BeginMutation())
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (ReadNode(path) is not JsonArray items || index < 0 || index >= items.Count) return false;
            DefinitionAnswerOrigin[] origins = Enumerable.Range(0, items.Count)
                .Select(row => AnswerOrigin([.. path, row.ToString(System.Globalization.CultureInfo.InvariantCulture)])).ToArray();
            DefinitionPath[] omissions = Omitted.ToArray();
            RecordMaterializedOrigins(path);
            string identity = GetEditorIdentity(path);
            string removedIdentity = identity + "-row-" +
                _editing.RowIdentity(identity, items.Count, index).ToString(System.Globalization.CultureInfo.InvariantCulture);
            items.RemoveAt(index);
            if (!ApplyValue(path, JsonSerializer.SerializeToElement(items), clearDrafts: false)) return false;
            RetainRowOrigins(path, origins);
            _editing.RemoveRow(identity, index, removedIdentity);
            _view.ClearEditorExpansion(removedIdentity, includeSelf: true);
            _view.ClearPromotions(removedIdentity);
            Fields[_index!.FieldPositions[path[0]]].RemoveItemDrafts(path, index);
            Omitted.Clear();
            foreach (DefinitionPath omitted in omissions)
            {
                if (omitted.Segments.Count <= path.Count || !omitted.Segments.Take(path.Count).SequenceEqual(path) ||
                    !omitted.TryGetArrayIndex(path.Count, out int row))
                {
                    Omitted.Add(omitted);
                    continue;
                }
                if (row == index) continue;
                Omitted.Add(row > index ? DefinitionPath.FromSegments([
                    .. omitted.Segments.Take(path.Count),
                    (row - 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
                    .. omitted.Segments.Skip(path.Count + 1)]) : omitted);
            }
            return true;
        }
    }

}
