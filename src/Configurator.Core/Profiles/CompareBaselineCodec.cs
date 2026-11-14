namespace Configurator.Core.Profiles;

using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Configurator.Core.Composition;
using Configurator.Core.Definitions;

/// <summary>Comparison files contain no recoverable protected values and are never restore inputs.</summary>
public static class CompareBaselineCodec
{
    /// <summary>Projects an authenticated owner without disclosing its retained answers or comparison key.</summary>
    public static CompareBaselineSnapshot Create(UnlockedProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        byte[] payload = profile.CopyPayload();
        byte[]? answerBytes = null;
        try
        {
            using JsonDocument document = JsonDocument.Parse(payload, ProfilePayloadCodec.DocumentOptions);
            JsonElement root = document.RootElement;
            using ProfileSecretCommitment? key = ProfileSecretCommitment.ReadPayload(root.GetProperty("comparisonKey"));
            answerBytes = JsonSerializer.SerializeToUtf8Bytes(root.GetProperty("answers"), CoreJsonContext.Default.JsonElement);
            AnswerSnapshot answers = AnswerSnapshotCodec.ParseJson(answerBytes);
            CompareBaselineSnapshot comparison = Create(profile.Definition, answers, profile.ProfileId, profile.SnapshotId,
                root.GetProperty("environmentContext").GetString()!, key);
            profile.EnsureAlive();
            return comparison;
        }
        finally
        {
            if (answerBytes is not null) CryptographicOperations.ZeroMemory(answerBytes);
            CryptographicOperations.ZeroMemory(payload);
        }
    }

    public static CompareBaselineSnapshot Create(Definition definition, AnswerSnapshot answers, Guid profileId,
        Guid snapshotId, string environmentContext, ProfileSecretCommitment? commitment = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(answers);
        if (profileId == Guid.Empty || snapshotId == Guid.Empty || environmentContext.Length is 0 or > 256)
            throw new ProfileException(ProfileFailureCode.InvalidPayload);
        byte[] boundedAnswers = AnswerSnapshotCodec.Serialize(answers);
        try
        {
            if (boundedAnswers.Length > ProfileContainerPolicy.MaximumPayloadBytes)
                throw new ProfileException(ProfileFailureCode.LimitExceeded);
            var projection = new Projection(profileId, definition.FamilyId, environmentContext, commitment);
            using JsonDocument document = JsonDocument.Parse(boundedAnswers, ProfilePayloadCodec.DocumentOptions);
            _ = projection.Project(definition.Configuration, document.RootElement.GetProperty("values"),
                DefinitionPath.Parse(""), out JsonNode? projected);
            JsonObject values = projected as JsonObject ?? new JsonObject();
            var root = new JsonObject
            {
                ["comparisonContractVersion"] = ConfiguratorVersion.Current,
                ["purpose"] = "configuration-comparison",
                ["profileId"] = profileId,
                ["snapshotId"] = snapshotId,
                ["familyId"] = definition.FamilyId,
                ["definitionRevision"] = definition.Revision,
                ["definitionDigest"] = definition.Digest.Value,
                ["structure"] = Structure(definition.Configuration),
                ["values"] = values,
                ["keyId"] = commitment is null ? null : JsonValue.Create(commitment.KeyId),
                ["contextTag"] = commitment?.Compute(profileId, definition.FamilyId, environmentContext, null),
                ["commitments"] = projection.Commitments
            };
            return ParseJson(JsonSerializer.SerializeToUtf8Bytes(root, CoreJsonContext.Default.JsonObject));
        }
        finally { CryptographicOperations.ZeroMemory(boundedAnswers); }
    }

    /// <summary>One recursive projection borrows the caller's commitment owner and retains only public output.</summary>
    private sealed class Projection(Guid profileId, Guid familyId, string environmentContext,
        ProfileSecretCommitment? commitment)
    {
        internal JsonObject Commitments { get; } = new();

        internal bool Project(DefinitionNode node, JsonElement supplied, DefinitionPath path,
            out JsonNode? projectedValue, bool stableField = true)
        {
            projectedValue = null;
            if (DefinitionSensitivityPolicy.SensitivityOf(node) != FieldSensitivity.Public)
            {
                Commit(path, supplied, stableField);
                return false;
            }
            if (supplied.ValueKind == JsonValueKind.Null) return node.IsNullable;
            if (node.Variants.Count != 0)
                return DefinitionValidator.SelectVariant(node, supplied) is { } selected &&
                    Project(selected, supplied, path, out projectedValue, stableField);
            if (node.Type == DefinitionNodeType.Mapping)
            {
                if (supplied.ValueKind != JsonValueKind.Object) return false;
                var mapping = new JsonObject();
                foreach (var field in node.Properties)
                    if (supplied.TryGetProperty(field.Key, out JsonElement child) &&
                        Project(field.Value, child, DefinitionPath.FromSegments(path.Segments.Append(field.Key)),
                            out JsonNode? childProjection, stableField))
                        mapping[field.Key] = childProjection;
                // Dynamic names never become comparison paths. Equality may cover this whole fixed container.
                if (node.AdditionalProperties is not null &&
                    supplied.EnumerateObject().Any(property => !node.Properties.ContainsKey(property.Name)))
                    Commit(path, supplied, stableField);
                projectedValue = mapping;
                return true;
            }
            if (node.Type == DefinitionNodeType.Sequence)
            {
                if (supplied.ValueKind != JsonValueKind.Array) return false;
                if (FieldDefinition.EffectiveSensitivity(node) != FieldSensitivity.Public)
                {
                    Commit(path, supplied, stableField);
                    return false;
                }
                var sequence = new JsonArray();
                foreach (JsonElement item in supplied.EnumerateArray())
                {
                    if (!Project(node.Items!, item, path, out JsonNode? itemProjection, stableField: false)) return false;
                    sequence.Add(itemProjection);
                }
                projectedValue = sequence;
                return true;
            }
            if (!DefinitionValidator.ValueValid(node, supplied)) return false;
            projectedValue = JsonNode.Parse(supplied.GetRawText());
            return true;
        }

        private void Commit(DefinitionPath path, JsonElement supplied, bool stableField)
        {
            if (stableField && commitment is not null)
                Commitments[path.ToPointer()] =
                    commitment.Compute(profileId, familyId, environmentContext, path.ToPointer(), supplied);
        }
    }

    public static byte[] Serialize(CompareBaselineSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return JsonSerializer.SerializeToUtf8Bytes(snapshot.Document, CoreJsonContext.Default.JsonElement);
    }

    public static CompareBaselineSnapshot ParseJson(ReadOnlyMemory<byte> encoded)
    {
        if (encoded.Length is 0 or > ProfileContainerPolicy.MaximumPayloadBytes)
            throw new ProfileException(ProfileFailureCode.LimitExceeded);
        try
        {
            using JsonDocument document = JsonDocument.Parse(encoded, new JsonDocumentOptions
            {
                AllowDuplicateProperties = false,
                MaxDepth = 2 * CompositionLimits.DefaultNestingDepth + 8
            });
            JsonElement root = document.RootElement;
            string[] names = ["comparisonContractVersion", "purpose", "profileId", "snapshotId", "familyId",
                "definitionRevision", "definitionDigest", "structure", "values", "keyId", "contextTag", "commitments"];
            if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Count() != names.Length ||
                root.EnumerateObject().Any(property => !names.Contains(property.Name, StringComparer.Ordinal)) ||
                root.GetProperty("comparisonContractVersion").GetString() != ConfiguratorVersion.Current ||
                root.GetProperty("purpose").GetString() != "configuration-comparison" ||
                root.GetProperty("profileId").GetGuid() == Guid.Empty ||
                root.GetProperty("snapshotId").GetGuid() == Guid.Empty ||
                root.GetProperty("familyId").GetGuid() == Guid.Empty ||
                root.GetProperty("definitionRevision").GetInt64() < 1 ||
                root.GetProperty("structure").ValueKind != JsonValueKind.Object ||
                root.GetProperty("values").ValueKind != JsonValueKind.Object ||
                root.GetProperty("commitments").ValueKind != JsonValueKind.Object)
                throw new ProfileException(ProfileFailureCode.InvalidPayload);
            JsonElement structure = root.GetProperty("structure");
            var budget = new CompositionBudget(CompositionLimits.Default);
            ValidateStructure(structure, budget, 1);
            JsonElement values = root.GetProperty("values");
            if (structure.GetProperty("type").GetString() != "object" ||
                (structure.GetProperty("public").GetBoolean()
                    ? !ValuesValid(structure, values, budget, 1)
                    : values.EnumerateObject().Any()))
                throw new ProfileException(ProfileFailureCode.InvalidPayload);
            JsonElement key = root.GetProperty("keyId");
            string? context = root.GetProperty("contextTag").GetString();
            if (key.ValueKind == JsonValueKind.Null
                ? context is not null || root.GetProperty("commitments").EnumerateObject().Any()
                : key.GetGuid() == Guid.Empty || !ArtifactDigest.TryParse(context, out _))
                throw new ProfileException(ProfileFailureCode.InvalidPayload);
            int count = 0;
            foreach (JsonProperty property in root.GetProperty("commitments").EnumerateObject())
            {
                if (++count > CompositionLimits.DefaultMappingEntries)
                    throw new ProfileException(ProfileFailureCode.LimitExceeded);
                budget.Scalar(property.Name);
                _ = DefinitionPath.Parse(property.Name);
                if (!ArtifactDigest.TryParse(property.Value.GetString(), out _))
                    throw new ProfileException(ProfileFailureCode.InvalidPayload);
            }
            return new CompareBaselineSnapshot(root);
        }
        catch (ProfileException) { throw; }
        catch (CompositionException) { throw new ProfileException(ProfileFailureCode.LimitExceeded); }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or KeyNotFoundException
            or ArgumentException or FormatException or DefinitionException or OverflowException)
        {
            throw new ProfileException(ProfileFailureCode.InvalidPayload);
        }
    }

    private static void ValidateStructure(JsonElement node, CompositionBudget budget, int depth)
    {
        budget.Node(depth);
        string[] allowed = ["type", "nullable", "public", "properties", "items", "variants"];
        if (node.ValueKind != JsonValueKind.Object ||
            node.EnumerateObject().Any(property => !allowed.Contains(property.Name, StringComparer.Ordinal)) ||
            node.GetProperty("nullable").ValueKind is not (JsonValueKind.True or JsonValueKind.False) ||
            node.GetProperty("public").ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            throw new ProfileException(ProfileFailureCode.InvalidPayload);
        string? type = node.GetProperty("type").GetString();
        if (type is not ("object" or "array" or "string" or "integer" or "number" or "boolean") ||
            !node.GetProperty("public").GetBoolean() && node.EnumerateObject().Count() != 3)
            throw new ProfileException(ProfileFailureCode.InvalidPayload);
        bool properties = node.TryGetProperty("properties", out JsonElement members);
        bool items = node.TryGetProperty("items", out JsonElement item);
        bool variants = node.TryGetProperty("variants", out JsonElement branches);
        if (properties && (type != "object" || members.ValueKind != JsonValueKind.Object ||
                members.EnumerateObject().Count() > CompositionLimits.DefaultMappingEntries) ||
            items && type != "array" || type == "array" && node.GetProperty("public").GetBoolean() && !items ||
            variants && (type != "object" || properties || branches.ValueKind != JsonValueKind.Array ||
                branches.GetArrayLength() is < 2 or > 16))
            throw new ProfileException(ProfileFailureCode.InvalidPayload);
        if (properties)
            foreach (JsonProperty property in members.EnumerateObject())
            {
                budget.Scalar(property.Name);
                DefinitionProfile.ValidateScalar(property.Name);
                ValidateStructure(property.Value, budget, depth + 1);
            }
        if (items) ValidateStructure(item, budget, depth + 1);
        if (variants)
            foreach (JsonElement branch in branches.EnumerateArray())
            {
                ValidateStructure(branch, budget, depth + 1);
                if (branch.GetProperty("type").GetString() != "object")
                    throw new ProfileException(ProfileFailureCode.InvalidPayload);
            }
    }

    private static bool ValuesValid(JsonElement structure, JsonElement value, CompositionBudget budget, int depth)
    {
        budget.Node(depth);
        if (!structure.GetProperty("public").GetBoolean()) return false;
        if (value.ValueKind == JsonValueKind.Null) return structure.GetProperty("nullable").GetBoolean();
        if (structure.TryGetProperty("variants", out JsonElement variants))
            return variants.EnumerateArray().Any(branch => ValuesValid(branch, value, budget, depth + 1));
        switch (structure.GetProperty("type").GetString())
        {
            case "object":
                if (value.ValueKind != JsonValueKind.Object ||
                    value.EnumerateObject().Count() > CompositionLimits.DefaultMappingEntries) return false;
                foreach (JsonProperty property in value.EnumerateObject())
                {
                    budget.Scalar(property.Name);
                    if (!structure.TryGetProperty("properties", out JsonElement properties) ||
                        !properties.TryGetProperty(property.Name, out JsonElement child) ||
                        !ValuesValid(child, property.Value, budget, depth + 1)) return false;
                }
                return true;
            case "array":
                if (value.ValueKind != JsonValueKind.Array ||
                    value.GetArrayLength() > CompositionLimits.DefaultSequenceEntries) return false;
                JsonElement item = structure.GetProperty("items");
                if (ContainsProtected(item)) return false;
                return value.EnumerateArray().All(element => ValuesValid(item, element, budget, depth + 1));
            case "string":
                if (value.ValueKind != JsonValueKind.String) return false;
                budget.Scalar(value.GetString());
                DefinitionProfile.ValidateScalar(value.GetString()!);
                return true;
            case "integer": return value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out _);
            case "number": return value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out _);
            case "boolean": return value.ValueKind is JsonValueKind.True or JsonValueKind.False;
            default: return false;
        }
    }

    private static bool ContainsProtected(JsonElement structure) =>
        !structure.GetProperty("public").GetBoolean() ||
        structure.TryGetProperty("properties", out JsonElement properties) &&
            properties.EnumerateObject().Any(property => ContainsProtected(property.Value)) ||
        structure.TryGetProperty("items", out JsonElement items) && ContainsProtected(items) ||
        structure.TryGetProperty("variants", out JsonElement variants) && variants.EnumerateArray().Any(ContainsProtected);

    public static ProfileComparisonResult CompareProtected(CompareBaselineSnapshot before, CompareBaselineSnapshot after,
        string fieldIdentity, string environmentContext, ProfileSecretCommitment? heldKey)
    {
        if (heldKey is null || before.KeyId != heldKey.KeyId || after.KeyId != heldKey.KeyId ||
            before.ProfileId != after.ProfileId || before.FamilyId != after.FamilyId)
            return ProfileComparisonResult.Unavailable;
        string context = heldKey.Compute(before.ProfileId, before.FamilyId, environmentContext, null);
        if (before.ContextTag != context || after.ContextTag != context ||
            !before.Commitments.TryGetValue(fieldIdentity, out string? left) ||
            !after.Commitments.TryGetValue(fieldIdentity, out string? right))
            return ProfileComparisonResult.Unavailable;
        return left == right ? ProfileComparisonResult.Unchanged : ProfileComparisonResult.Changed;
    }

    private static JsonObject Structure(DefinitionNode node)
    {
        bool isPublic = DefinitionSensitivityPolicy.SensitivityOf(node) == FieldSensitivity.Public;
        var result = new JsonObject { ["type"] = node.JsonType, ["nullable"] = node.IsNullable, ["public"] = isPublic };
        if (!isPublic) return result;
        if (node.Properties.Count != 0)
        {
            var properties = new JsonObject();
            foreach (var property in node.Properties) properties[property.Key] = Structure(property.Value);
            result["properties"] = properties;
        }
        if (node.Items is not null) result["items"] = Structure(node.Items);
        if (node.Variants.Count != 0)
            result["variants"] = new JsonArray(node.Variants.Select(variant => (JsonNode?)Structure(variant)).ToArray());
        return result;
    }
}
