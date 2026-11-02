namespace Configurator.Core.Definitions;

using System.Text.Json;
using Configurator.Core.Environment;

/// <summary>A captured, value-free subset of direct Environment mappings.</summary>
public sealed class DefinitionEditableProjection
{
    private readonly IReadOnlyDictionary<string, DefinitionNode> _schemas;

    private DefinitionEditableProjection(Definition definition, string artifactId, long inputRevision,
        IEnumerable<DefinitionEditableCoverage> coverage, IReadOnlyDictionary<string, DefinitionNode> schemas,
        IEnumerable<DefinitionPath> controllers)
    {
        FamilyId = definition.FamilyId;
        DefinitionRevision = definition.Revision;
        DefinitionDigest = definition.Digest;
        ArtifactId = artifactId;
        InputRevision = inputRevision;
        Coverage = Array.AsReadOnly(coverage.ToArray());
        Mappings = Array.AsReadOnly(Coverage.Where(item => item.Kind == DefinitionEditableCoverageKind.Editable)
            .Select(item => new DefinitionEditableMapping(item.Key, item.Path!)).ToArray());
        _schemas = schemas;
        Controllers = Array.AsReadOnly(controllers.OrderBy(path => path.ToPointer(), StringComparer.Ordinal).ToArray());
    }

    public Guid FamilyId { get; }
    public long DefinitionRevision { get; }
    public ArtifactDigest DefinitionDigest { get; }
    public string ArtifactId { get; }
    public long InputRevision { get; }
    public IReadOnlyList<DefinitionEditableMapping> Mappings { get; }
    public IReadOnlyList<DefinitionEditableCoverage> Coverage { get; }
    public IReadOnlyList<DefinitionPath> Controllers { get; }
    public bool IsSupported => Mappings.Count != 0;

    /// <summary>Checks identity, revision and recomputed coverage. The workspace owner must also fence its own lifetime and consent.</summary>
    public bool IsCurrent(Definition definition, AnswerSnapshot answers,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(answers);
        cancellationToken.ThrowIfCancellationRequested();
        return definition.FamilyId == FamilyId && definition.Revision == DefinitionRevision
            && definition.Digest == DefinitionDigest && answers.InputRevision == InputRevision
            && Coverage.SequenceEqual(Capture(definition, ArtifactId, answers, cancellationToken).Coverage);
    }

    /// <summary>Converts only captured editable keys through the strict inverse scalar primitives and declared constraints.</summary>
    public bool TryConvertValue(string key, string text, out JsonElement value)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(text);
        value = default;
        if (!_schemas.TryGetValue(key, out DefinitionNode? schema)
            || !DefinitionImport.TryDotenvValue(schema, text, out JsonElement converted)
            || !DefinitionValidator.ValueValid(schema, converted)) return false;
        value = converted;
        return true;
    }

    public override string ToString() =>
        $"{nameof(DefinitionEditableProjection)}:Revision={InputRevision}:Mappings={Mappings.Count}:Coverage={Coverage.Count}";

    internal static DefinitionEditableProjection Capture(Definition definition, string artifactId,
        AnswerSnapshot answers, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(artifactId);
        ArgumentNullException.ThrowIfNull(answers);
        cancellationToken.ThrowIfCancellationRequested();
        var coverage = new List<DefinitionEditableCoverage>();
        var schemas = new Dictionary<string, DefinitionNode>(StringComparer.Ordinal);
        var controllers = new HashSet<DefinitionPath>();
        ArtifactDefinition? artifact = definition.Artifacts.FirstOrDefault(item => item.Id == artifactId);
        if (artifact is null || artifact.Format != "dotenv" ||
            artifact.Projection.GetProperty("op").GetString() != "object") return Result();

        DefinitionValidationResult validation = DefinitionValidator.ValidateDraft(definition, answers, cancellationToken);
        var unknown = validation.UnresolvedFields.Concat(validation.DeferredFields)
            .Concat(validation.Fields.Where(field => field.HasError).Select(field => field.Definition.Name))
            .ToHashSet(StringComparer.Ordinal);
        var candidates = new List<(string Key, DefinitionPath? Path, DefinitionNode? Schema,
            DefinitionEditableCoverageKind Kind)>();
        JsonElement projection = artifact.Projection;
        CollectProjectionConditions(projection);
        bool? artifactActive = Active(projection);
        foreach (JsonProperty member in projection.GetProperty("members").EnumerateObject())
        {
            cancellationToken.ThrowIfCancellationRequested();
            JsonElement operation = member.Value;
            string op = operation.GetProperty("op").GetString()!;
            DefinitionPath? path = null;
            DefinitionNode? schema = null;
            DefinitionEditableCoverageKind kind = op is "literal" or "omit"
                ? DefinitionEditableCoverageKind.ReadOnly : DefinitionEditableCoverageKind.Unsupported;
            if (op == "copy" && operation.GetProperty("reference").GetProperty("scope").GetString() == "root")
            {
                path = DefinitionPath.Parse(operation.GetProperty("reference").GetProperty("path").GetString()!);
                schema = DefinitionImportProjection.SourceNode(definition.Configuration, path, out _, out bool publicPath);
                bool declared = true;
                DefinitionNode ancestor = definition.Configuration;
                foreach (string segment in path.Segments)
                {
                    CollectSchemaCondition(ancestor);
                    if (ancestor.Variants.Count != 0 || ancestor.Type != DefinitionNodeType.Mapping ||
                        !ancestor.Properties.TryGetValue(segment, out DefinitionNode? child))
                    {
                        declared = false;
                        break;
                    }
                    ancestor = child;
                }
                if (declared) CollectSchemaCondition(ancestor);
                if (schema is not null && declared && path.Segments.Count != 0)
                {
                    kind = !publicPath ? DefinitionEditableCoverageKind.Protected
                        : schema.IsNullable || schema.Type is DefinitionNodeType.Mapping or DefinitionNodeType.Sequence
                            ? DefinitionEditableCoverageKind.Unsupported : DefinitionEditableCoverageKind.Editable;
                    // Node facts come from the sole validator, including inherited activation and sensitivity.
                    if (kind == DefinitionEditableCoverageKind.Editable)
                    {
                        FieldDefinition field = definition.Fields.Single(item => item.Name == path.Segments[0]);
                        DefinitionNodeState? state = validation.Nodes.FirstOrDefault(item => item.FieldId == field.Id
                            && item.RelativePath.SequenceEqual(path.Segments.Skip(1), StringComparer.Ordinal));
                        if (state is null || !state.IsActive)
                            kind = DefinitionEditableCoverageKind.Inactive;
                    }
                }
            }
            bool? active = Active(operation);
            if (kind != DefinitionEditableCoverageKind.Protected)
            {
                if (artifactActive is null || active is null) kind = DefinitionEditableCoverageKind.ConditionUnresolved;
                else if (artifactActive == false || active == false) kind = DefinitionEditableCoverageKind.Inactive;
            }
            if (!DotenvCodec.IsKey(member.Name)) kind = DefinitionEditableCoverageKind.Unsupported;
            candidates.Add((member.Name, path, schema, kind));
        }

        var aliases = candidates.Where(item => item.Path is not null)
            .GroupBy(item => item.Path!).Where(group => group.Count() > 1)
            .Select(group => group.Key).ToHashSet();
        var collisions = candidates.GroupBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1).Select(group => group.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var kind = candidate.Kind;
            if (kind != DefinitionEditableCoverageKind.Protected)
            {
                if (collisions.Contains(candidate.Key) || candidate.Path is { } source && aliases.Contains(source))
                    kind = DefinitionEditableCoverageKind.Ambiguous;
                else if (candidate.Path is { } path && controllers.Any(controller =>
                    DefinitionImportProjection.IsPrefix(controller, path) || DefinitionImportProjection.IsPrefix(path, controller)))
                    kind = DefinitionEditableCoverageKind.Controller;
            }
            coverage.Add(new(candidate.Key, candidate.Path, kind));
            if (kind == DefinitionEditableCoverageKind.Editable) schemas.Add(candidate.Key, candidate.Schema!);
        }
        return Result();

        DefinitionEditableProjection Result() => new(definition, artifactId, answers.InputRevision, coverage, schemas, controllers);

        bool? Active(JsonElement operation) => operation.TryGetProperty("when", out JsonElement condition)
            ? DefinitionValidator.EvaluateCondition(condition, validation.Values, unknown,
                deferredLocations: validation.DeferredLocations) : true;

        void CollectSchemaCondition(DefinitionNode node)
        {
            if (node.Keywords.TryGetProperty("x-configurator", out JsonElement metadata)
                && metadata.TryGetProperty("applicableWhen", out JsonElement condition)) CollectReferences(condition);
        }

        void CollectProjectionConditions(JsonElement operation)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (operation.TryGetProperty("when", out JsonElement condition)) CollectReferences(condition);
            switch (operation.GetProperty("op").GetString())
            {
                case "object":
                    foreach (JsonProperty member in operation.GetProperty("members").EnumerateObject())
                        CollectProjectionConditions(member.Value);
                    break;
                case "list":
                    foreach (JsonElement item in operation.GetProperty("items").EnumerateArray())
                        CollectProjectionConditions(item);
                    break;
                case "repeat": CollectProjectionConditions(operation.GetProperty("item")); break;
            }
        }

        void CollectReferences(JsonElement condition)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (condition.TryGetProperty("operands", out JsonElement operands))
            {
                foreach (JsonElement operand in operands.EnumerateArray()) CollectReferences(operand);
            }
            else if (condition.GetProperty("reference").GetProperty("scope").GetString() == "root")
            {
                DefinitionPath path = DefinitionPath.Parse(condition.GetProperty("reference").GetProperty("path").GetString()!);
                if (!controllers.Add(path)) return;
                // A controller's own applicability can change the captured scope too.
                for (int count = 0; count <= path.Segments.Count; count++)
                {
                    DefinitionNode? node = DefinitionImportProjection.SourceNode(definition.Configuration,
                        DefinitionPath.FromSegments(path.Segments.Take(count)), out _, out _);
                    if (node is not null) CollectSchemaCondition(node);
                }
            }
        }
    }
}

public sealed record DefinitionEditableMapping(string Key, DefinitionPath Path);

public enum DefinitionEditableCoverageKind
{
    Editable,
    ReadOnly,
    Protected,
    Inactive,
    ConditionUnresolved,
    Unsupported,
    Ambiguous,
    Controller,
}

public sealed record DefinitionEditableCoverage(string Key, DefinitionPath? Path, DefinitionEditableCoverageKind Kind);
