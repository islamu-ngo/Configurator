namespace Configurator.Presentation;

using System.Text.Json;
using System.Text.Json.Nodes;
using Configurator.Core.Definitions;

public sealed partial class DefinitionFormModel
{
    /// <summary>Adds one literal map key without replacing an entry or exceeding the admitted bound.</summary>
    public bool AddMapEntry(IReadOnlyList<string> path, string key) => AddMapEntry(DataPath(path), key);

    public bool AddMapEntry(DefinitionPath path, string key)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(key);
        using (BeginMutation())
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            DefinitionPath entryPath;
            try { entryPath = DefinitionPath.FromSegments([.. path.Segments, key]); }
            catch (DefinitionException) { return false; }
            if (!TryEditorMetadata(path, out DefinitionFormIndex.Node node, out _) ||
                node.Schema.AdditionalProperties is not { } entry || node.Schema.Properties.ContainsKey(key)) return false;
            JsonElement? current = ReadEditorValue(path);
            if (current is { ValueKind: JsonValueKind.Object } mapping &&
                (mapping.TryGetProperty(key, out _) || mapping.EnumerateObject().Count() >= MaximumMapEntries(node.Schema))) return false;
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

    public bool SupplyObject(IReadOnlyList<string> path) => SupplyObject(DataPath(path));

    public bool SupplyObject(DefinitionPath path)
    {
        using (BeginMutation())
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!TryEditorMetadata(path, out DefinitionFormIndex.Node node, out _) ||
                node.Schema.Type != DefinitionNodeType.Mapping) return false;
            if (ReadEditorValue(path) is { ValueKind: JsonValueKind.Object }) return true;
            return SetValue(path, DefinitionCollectionItem.CreateObject(node.Schema));
        }
    }

    public bool SupplyList(IReadOnlyList<string> path) => SupplyList(DataPath(path));

    public bool SupplyList(DefinitionPath path)
    {
        using (BeginMutation())
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!TryEditorMetadata(path, out DefinitionFormIndex.Node node, out _) ||
                node.Schema.Type != DefinitionNodeType.Sequence) return false;
            if (ReadEditorValue(path) is { ValueKind: JsonValueKind.Array }) return true;
            return SetValue(path, JsonSerializer.SerializeToElement(Array.Empty<object>()));
        }
    }

    public bool AddListItem(IReadOnlyList<string> path) => AddListItem(DataPath(path));

    public bool AddListItem(DefinitionPath path)
    {
        using (BeginMutation())
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!TryEditorMetadata(path, out DefinitionFormIndex.Node node, out _) ||
                node.Schema.Type != DefinitionNodeType.Sequence) return false;
            JsonArray items = ReadEditorValue(path) is { ValueKind: JsonValueKind.Array } value
                ? JsonNode.Parse(value.GetRawText())!.AsArray() : [];
            int maximum = node.Metadata.TryGetProperty("maxItems", out JsonElement limit) ? limit.GetInt32() : 4096;
            if (items.Count >= maximum) return false;
            DefinitionAnswerOrigin[] origins = Enumerable.Range(0, items.Count)
                .Select(index => AnswerOrigin(DefinitionPath.FromSegments([.. path.Segments,
                    index.ToString(System.Globalization.CultureInfo.InvariantCulture)]))).ToArray();
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
            if (!ApplyEditorValue(path, JsonSerializer.SerializeToElement(items), clearDrafts: false)) return false;
            RetainRowOrigins(path, origins);
            DefinitionPath addedPath = DefinitionPath.FromSegments([.. path.Segments,
                origins.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)]);
            if (TryOrdinaryRootPath(addedPath.Segments, out string[] ordinary))
                Fields[_index!.FieldPositions[ordinary[0]]].RecordOrigin(ordinary, DefinitionAnswerOrigin.Explicit);
            else RecordRootOrigin(addedPath.Segments, DefinitionAnswerOrigin.Explicit);
            return true;
        }
    }

    public bool RemoveListItem(IReadOnlyList<string> path, int index) => RemoveListItem(DataPath(path), index);

    public bool RemoveListItem(DefinitionPath path, int index)
    {
        using (BeginMutation())
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (ReadEditorValue(path) is not { ValueKind: JsonValueKind.Array } value ||
                index < 0 || index >= value.GetArrayLength()) return false;
            JsonArray items = JsonNode.Parse(value.GetRawText())!.AsArray();
            DefinitionAnswerOrigin[] origins = Enumerable.Range(0, items.Count)
                .Select(row => AnswerOrigin(DefinitionPath.FromSegments([.. path.Segments,
                    row.ToString(System.Globalization.CultureInfo.InvariantCulture)]))).ToArray();
            var omissions = new List<DefinitionPath>();
            IReadOnlyList<string> dataPath = path.Segments;
            foreach (DefinitionPath omitted in Omitted)
            {
                if (omitted.Segments.Count <= dataPath.Count || !omitted.Segments.Take(dataPath.Count).SequenceEqual(dataPath) ||
                    !omitted.TryGetArrayIndex(dataPath.Count, out int row))
                {
                    omissions.Add(omitted);
                    continue;
                }
                if (row == index) continue;
                omissions.Add(row > index ? DefinitionPath.FromSegments([
                    .. omitted.Segments.Take(dataPath.Count),
                    (row - 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
                    .. omitted.Segments.Skip(dataPath.Count + 1)]) : omitted);
            }
            RecordMaterializedOrigins(path);
            string identity = GetEditorIdentity(path);
            string removedIdentity = identity + "-row-" +
                _editing.RowIdentity(identity, items.Count, index).ToString(System.Globalization.CultureInfo.InvariantCulture);
            items.RemoveAt(index);
            if (!ApplyEditorValue(path, JsonSerializer.SerializeToElement(items), clearDrafts: false, omissions)) return false;
            RetainRowOrigins(path, origins);
            _editing.RemoveRow(identity, index, removedIdentity);
            _view.ClearEditorExpansion(removedIdentity, includeSelf: true);
            _view.ClearPromotions(removedIdentity);
            if (TryOrdinaryRootPath(path.Segments, out string[] ordinary))
                Fields[_index!.FieldPositions[ordinary[0]]].RemoveItemDrafts(ordinary, index);
            else DefinitionFieldInput.RemoveItemDrafts(_editing.RootInput, path.Segments, index);
            Omitted.Clear();
            Omitted.UnionWith(omissions);
            return true;
        }
    }

}
