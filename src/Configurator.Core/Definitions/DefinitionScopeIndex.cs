namespace Configurator.Core.Definitions;

using System.Collections.Frozen;
using System.Collections.ObjectModel;
using System.Text.Json;

/// <summary>Indexes admitted projection dependencies without retaining any answer-dependent state.</summary>
internal sealed class DefinitionScopeIndex
{
    private readonly FrozenDictionary<string, IReadOnlyList<DefinitionScopeReference>> _artifacts;

    internal DefinitionScopeIndex(IEnumerable<ArtifactDefinition> artifacts)
    {
        _artifacts = artifacts.ToFrozenDictionary(artifact => artifact.Id,
            artifact => (IReadOnlyList<DefinitionScopeReference>)References(artifact.Projection),
            StringComparer.Ordinal);
    }

    internal IReadOnlyList<DefinitionScopeReference> ArtifactReferences(string id) =>
        _artifacts.TryGetValue(id, out var references) ? references : [];

    private static ReadOnlyCollection<DefinitionScopeReference> References(JsonElement projection)
    {
        var references = new List<DefinitionScopeReference>();
        Visit(projection, []);
        return references.AsReadOnly();

        void Visit(JsonElement node, IReadOnlyList<DefinitionScopeSegment> item)
        {
            if (node.TryGetProperty("when", out var condition)) Predicate(condition, item);
            switch (node.GetProperty("op").GetString())
            {
                case "copy":
                    references.Add(new(Reference(node.GetProperty("reference"), item), true));
                    break;
                case "repeat":
                    IReadOnlyList<DefinitionScopeSegment> source = Reference(node.GetProperty("reference"), item);
                    references.Add(new(source, false));
                    Visit(node.GetProperty("item"), Array.AsReadOnly(source.Append(new DefinitionScopeSegment("", true)).ToArray()));
                    break;
                case "object":
                    foreach (JsonProperty member in node.GetProperty("members").EnumerateObject()) Visit(member.Value, item);
                    break;
                case "list":
                    foreach (JsonElement child in node.GetProperty("items").EnumerateArray()) Visit(child, item);
                    break;
            }
        }

        void Predicate(JsonElement condition, IReadOnlyList<DefinitionScopeSegment> item)
        {
            if (condition.TryGetProperty("operands", out var operands))
                foreach (JsonElement operand in operands.EnumerateArray()) Predicate(operand, item);
            else references.Add(new(Reference(condition.GetProperty("reference"), item), true));
        }

        static ReadOnlyCollection<DefinitionScopeSegment> Reference(JsonElement reference, IReadOnlyList<DefinitionScopeSegment> item)
        {
            IEnumerable<DefinitionScopeSegment> prefix = reference.GetProperty("scope").GetString() == "item" ? item : [];
            return Array.AsReadOnly(prefix.Concat(DefinitionPath.Parse(reference.GetProperty("path").GetString()!)
                .Segments.Select(segment => new DefinitionScopeSegment(segment))).ToArray());
        }
    }
}

internal sealed record DefinitionScopeReference(IReadOnlyList<DefinitionScopeSegment> Segments, bool Descendants);

internal readonly record struct DefinitionScopeSegment(string Name, bool Repeated = false);
