namespace Configurator.Core.Profiles;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Configurator.Core.Definitions;

/// <summary>
/// Runs ordinary Core validation within this call and publishes metadata only. Transient managed JSON copies have
/// the existing runtime erasure limitation; they are never retained in a plan or used as the blind payload owner.
/// </summary>
public static class ProfileUpgradePlanner
{
    /// <summary>Publishes fixed schema coordinates of retained protected roots, without values or dynamic keys.</summary>
    public static IReadOnlyList<DefinitionPath> RetainedFieldPaths(UnlockedProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        Definition source = profile.Definition;
        return Array.AsReadOnly(Plan(profile, source).Fields
            .Where(field => field.Eligibility is not (ProfileCarryEligibility.Missing or ProfileCarryEligibility.Removed))
            .Select(field => DefinitionPath.Parse(field.FieldPath))
            .Where(path => path.Segments.Count == 1 &&
                source.Configuration.Properties.TryGetValue(path.Segments[0], out DefinitionNode? node) &&
                (DefinitionSensitivityPolicy.SensitivityOf(source.Configuration) != FieldSensitivity.Public ||
                    FieldDefinition.EffectiveSensitivity(node) != FieldSensitivity.Public))
            .ToArray());
    }

    public static ProfileUpgradePlan Plan(UnlockedProfile profile, Definition target)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(target);
        profile.EnsureAlive();
        IReadOnlyList<DefinitionSchemaDelta> deltas = DefinitionSchemaDiff.Compare(profile.Definition, target);
        byte[] payload = profile.CopyPayload();
        try
        {
            using JsonDocument document = JsonDocument.Parse(payload, ProfilePayloadCodec.DocumentOptions);
            byte[] encoded = Encoding.UTF8.GetBytes(document.RootElement.GetProperty("answers").GetRawText());
            try
            {
                AnswerSnapshot answers = AnswerSnapshotCodec.ParseJson(encoded);
                ProfileUpgradePlan plan = Evaluate(profile.Definition, target, new DefinitionEvaluationInput(answers), deltas);
                profile.EnsureAlive();
                return plan;
            }
            finally { CryptographicOperations.ZeroMemory(encoded); }
        }
        finally { CryptographicOperations.ZeroMemory(payload); }
    }

    /// <summary>
    /// Evaluates a private candidate containing current replacements. Required additions elsewhere may be
    /// satisfied by those replacements; the retained field's own required status and secret policy still bind.
    /// </summary>
    internal static ProfileUpgradePlan Plan(UnlockedProfile profile, Definition target,
        DefinitionEvaluationInput candidate, CancellationToken cancellationToken)
    {
        profile.EnsureAlive();
        ProfileUpgradePlan plan = Evaluate(profile.Definition, target, candidate,
            DefinitionSchemaDiff.Compare(profile.Definition, target), true, cancellationToken);
        profile.EnsureAlive();
        return plan;
    }

    /// <summary>A comparison baseline is not a restore source. Without its exact old schema, structural comparisons are unavailable.</summary>
    public static ProfileUpgradePlan Plan(CompareBaselineSnapshot baseline, Definition target, Definition? source = null)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(target);
        CheckIdentity(baseline, source, target);
        return new(source is not null, source is null ? [] : DefinitionSchemaDiff.Compare(source, target),
            target.Configuration.Properties.Keys.Select(name => new ProfileDeltaSummary("/" + DefinitionSchemaDiff.Escape(name),
                ProfileCarryEligibility.RevalidationRequired)));
    }

    /// <summary>Compares only admitted fixed fields. Protected equality requires a live matching commitment key and context.</summary>
    public static ProfileUpgradePlan Compare(CompareBaselineSnapshot before, CompareBaselineSnapshot after,
        Definition? source, Definition target, string environmentContext, ProfileSecretCommitment? heldKey = null)
    {
        CheckIdentity(before, source, target);
        CheckIdentity(after, target, target);
        var fields = new List<ProfileDeltaSummary>();
        foreach (string name in (source?.Configuration.Properties.Keys ?? []).Union(target.Configuration.Properties.Keys, StringComparer.Ordinal)
            .Order(StringComparer.Ordinal))
        {
            string path = "/" + DefinitionSchemaDiff.Escape(name);
            ProfileComparisonResult comparison = ProfileComparisonResult.Unavailable;
            if (target.Configuration.Properties.TryGetValue(name, out DefinitionNode? right))
            {
                DefinitionNode? left = source?.Configuration.Properties.GetValueOrDefault(name);
                bool oldPublic = left is not null && source is not null
                    ? FieldDefinition.EffectiveSensitivity(left, DefinitionSensitivityPolicy.SensitivityOf(source.Configuration)) == FieldSensitivity.Public
                    : before.Structure.GetProperty("public").GetBoolean() &&
                        before.Structure.TryGetProperty("properties", out JsonElement properties) &&
                        properties.TryGetProperty(name, out JsonElement property) && property.GetProperty("public").GetBoolean() &&
                        property.GetProperty("type").GetString() is not ("object" or "array");
                if (oldPublic &&
                    FieldDefinition.EffectiveSensitivity(right, DefinitionSensitivityPolicy.SensitivityOf(target.Configuration)) == FieldSensitivity.Public &&
                    (left is null || CompleteProjection(left)) && CompleteProjection(right) &&
                    before.PublicValues.TryGetProperty(name, out JsonElement oldValue) &&
                    after.PublicValues.TryGetProperty(name, out JsonElement newValue))
                    comparison = JsonElement.DeepEquals(oldValue, newValue) ? ProfileComparisonResult.Unchanged : ProfileComparisonResult.Changed;
                else comparison = CompareBaselineCodec.CompareProtected(before, after, path, environmentContext, heldKey);
            }
            fields.Add(new(path, ProfileCarryEligibility.RevalidationRequired, comparison));
        }
        return new(source is not null, source is null ? [] : DefinitionSchemaDiff.Compare(source, target), fields);

        static bool CompleteProjection(DefinitionNode node) => node.AdditionalProperties is null &&
            node.Properties.Values.All(CompleteProjection) && node.Variants.All(CompleteProjection) &&
            (node.Items is null || CompleteProjection(node.Items));
    }

    private static ProfileUpgradePlan Evaluate(Definition source, Definition target, DefinitionEvaluationInput answers,
        IReadOnlyList<DefinitionSchemaDelta> deltas, bool scoped = false, CancellationToken cancellationToken = default)
    {
        DefinitionValidationResult validation = DefinitionValidator.Validate(target, answers, cancellationToken);
        var fields = new List<ProfileDeltaSummary>();
        foreach (string name in source.Configuration.Properties.Keys.Union(target.Configuration.Properties.Keys, StringComparer.Ordinal)
            .Order(StringComparer.Ordinal))
        {
            string path = "/" + DefinitionSchemaDiff.Escape(name);
            ProfileCarryEligibility eligibility;
            if (!target.Configuration.Properties.TryGetValue(name, out DefinitionNode? node)) eligibility = ProfileCarryEligibility.Removed;
            else if (!answers.Values.TryGetValue(name, out JsonElement value)) eligibility = ProfileCarryEligibility.Missing;
            else if (!source.Configuration.Properties.TryGetValue(name, out DefinitionNode? oldNode))
                eligibility = ProfileCarryEligibility.RevalidationRequired;
            else if (FieldDefinition.EffectiveSensitivity(oldNode, DefinitionSensitivityPolicy.SensitivityOf(source.Configuration)) !=
                FieldDefinition.EffectiveSensitivity(node, DefinitionSensitivityPolicy.SensitivityOf(target.Configuration)))
                eligibility = ProfileCarryEligibility.ReplacementRequired;
            else if ((FieldDefinition.EffectiveSensitivity(oldNode, DefinitionSensitivityPolicy.SensitivityOf(source.Configuration)) ==
                    FieldSensitivity.Secret ||
                    FieldDefinition.EffectiveSensitivity(node, DefinitionSensitivityPolicy.SensitivityOf(target.Configuration)) ==
                    FieldSensitivity.Secret) &&
                deltas.Any(delta => delta.SchemaPath == "/configuration/properties/" + DefinitionSchemaDiff.Escape(name) ||
                    delta.SchemaPath.StartsWith("/configuration/properties/" + DefinitionSchemaDiff.Escape(name) + "/", StringComparison.Ordinal) ||
                    delta.SchemaPath == "/configuration" &&
                    (!scoped || delta.Kind != DefinitionSchemaChange.Required ||
                        source.Configuration.Required.Contains(name, StringComparer.Ordinal) !=
                        target.Configuration.Required.Contains(name, StringComparer.Ordinal))))
                eligibility = ProfileCarryEligibility.ReplacementRequired;
            else if (deltas.Any(delta => delta.Kind == DefinitionSchemaChange.Sensitivity &&
                delta.SchemaPath.StartsWith("/configuration/properties/" + DefinitionSchemaDiff.Escape(name) + "/", StringComparison.Ordinal)))
                eligibility = ProfileCarryEligibility.ReplacementRequired;
            else if (Collections(oldNode) || Collections(node)) eligibility = ProfileCarryEligibility.RevalidationRequired;
            else
            {
                FieldState? state = validation.Fields.FirstOrDefault(field => field.Definition.Name == name);
                eligibility = state is null || validation.HasUndecidedDependencies || validation.ConfigurationContext is { IsReady: false }
                    ? ProfileCarryEligibility.RevalidationRequired
                    : !state.IsActive ? ProfileCarryEligibility.Inactive
                    : state.HasError || !DefinitionValidator.ValueValid(node, value) ? ProfileCarryEligibility.Invalid
                    : !validation.IsReady || validation.Nodes.Any(child => child.FieldId == state.Id && !child.IsActive)
                        ? ProfileCarryEligibility.RevalidationRequired
                    : ProfileCarryEligibility.Eligible;
            }
            fields.Add(new(path, eligibility));
        }
        return new(true, deltas, fields);
    }

    private static bool Collections(DefinitionNode node) => node.Type == DefinitionNodeType.Sequence ||
        node.AdditionalProperties is not null || node.Variants.Count != 0 || node.Properties.Values.Any(Collections);

    private static void CheckIdentity(CompareBaselineSnapshot baseline, Definition? source, Definition target)
    {
        if (baseline.FamilyId != target.FamilyId) throw new DefinitionException("family-mismatch");
        if (source is null)
        {
            if (baseline.DefinitionRevision == target.Revision && baseline.DefinitionDigest != target.Digest)
                throw new DefinitionException("revision-conflict");
            return;
        }
        if (baseline.FamilyId != source.FamilyId || baseline.DefinitionRevision != source.Revision ||
            baseline.DefinitionDigest != source.Digest) throw new DefinitionException("source-schema-mismatch");
        _ = DefinitionSchemaDiff.Compare(source, target);
    }
}
