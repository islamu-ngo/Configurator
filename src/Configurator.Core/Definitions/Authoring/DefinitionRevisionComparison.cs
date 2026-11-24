namespace Configurator.Core.Definitions.Authoring;

using System.Globalization;
using System.Text.Json;

public enum DefinitionRevisionImpactKind
{
    Behavioral,
    DisplayOnly
}

public static class DefinitionRevisionChangeCategory
{
    public const string FieldAdded = "field-added";
    public const string FieldRemoved = "field-removed";
    public const string TypeChanged = "type-changed";
    public const string SensitivityChanged = "sensitivity-changed";
    public const string RequirednessChanged = "requiredness-changed";
    public const string ActivationChanged = "activation-changed";
    public const string ConstraintChanged = "constraint-changed";
    public const string InitialValueChanged = "initial-value-changed";
    public const string SecretPolicyChanged = "secret-policy-changed";
    public const string OutputAdded = "output-added";
    public const string OutputRemoved = "output-removed";
    public const string OutputMappingChanged = "output-mapping-changed";
    public const string DeploymentChanged = "deployment-changed";
    public const string CapabilityChanged = "capability-changed";
    public const string DisplayTextChanged = "display-text-changed";
    public const string PresentationChanged = "presentation-changed";
    public const string LocalizationChanged = "localization-changed";
    public const string AnnotationsChanged = "annotations-changed";
}

/// <summary>One value-free semantic difference between two admitted definitions.</summary>
public sealed record DefinitionRevisionChange(
    DefinitionRevisionImpactKind Impact,
    string Category,
    string Path,
    string? Detail = null);

public sealed class DefinitionRevisionComparisonReport
{
    internal DefinitionRevisionComparisonReport(
        Definition baseline,
        Definition candidate,
        IReadOnlyList<DefinitionRevisionChange> changes)
    {
        BaselineFamilyId = baseline.FamilyId;
        BaselineRevision = baseline.Revision;
        BaselineDigest = baseline.Digest.Value;
        CandidateFamilyId = candidate.FamilyId;
        CandidateRevision = candidate.Revision;
        CandidateDigest = candidate.Digest.Value;
        SameFamily = baseline.FamilyId == candidate.FamilyId;
        CanonicalIdentical = baseline.Digest == candidate.Digest;
        Changes = changes;
        HasBehavioralChanges = changes.Any(static change => change.Impact == DefinitionRevisionImpactKind.Behavioral);
        HasDisplayOnlyChanges = changes.Any(static change => change.Impact == DefinitionRevisionImpactKind.DisplayOnly);
    }

    public Guid BaselineFamilyId { get; }
    public long BaselineRevision { get; }
    public string BaselineDigest { get; }
    public Guid CandidateFamilyId { get; }
    public long CandidateRevision { get; }
    public string CandidateDigest { get; }
    public bool SameFamily { get; }
    public bool CanonicalIdentical { get; }
    public bool HasBehavioralChanges { get; }
    public bool HasDisplayOnlyChanges { get; }
    public IReadOnlyList<DefinitionRevisionChange> Changes { get; }
}

/// <summary>
/// Compares two admitted definitions by exact logical field and artifact identities, separating behavioral
/// contract impacts from display-only edits without guessing renames or reading answers.
/// </summary>
public static class DefinitionRevisionComparison
{
    private static readonly string[] ConstraintKeywords =
    [
        "enum", "const", "minimum", "maximum", "exclusiveMinimum", "exclusiveMaximum", "multipleOf",
        "minLength", "maxLength", "minItems", "maxItems", "minProperties", "maxProperties", "uniqueItems", "format"
    ];

    private static readonly string[] ExtensionConstraintKeywords =
    [
        "notBlank", "uniqueBy"
    ];

    private static readonly string[] DisplayKeywords =
    [
        "title", "description", "examples", "readOnly", "deprecated"
    ];

    private static readonly string[] ExtensionDisplayKeywords =
    [
        "detail", "guidance", "choices", "units", "propertyOrder", "order", "placeholder", "defaultText",
        "deprecationMessage", "widget", "rows", "syntax", "booleanLabels", "itemTitlePointer", "displayRawKey"
    ];

    private static readonly string[] ExtensionValueKeywords =
    [
        "initialValue", "suggestedValue", "suggestedFrom"
    ];

    public static DefinitionRevisionComparisonReport Compare(
        Definition baseline,
        Definition candidate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(candidate);
        cancellationToken.ThrowIfCancellationRequested();

        if (baseline.Digest == candidate.Digest)
            return new DefinitionRevisionComparisonReport(baseline, candidate, Array.Empty<DefinitionRevisionChange>());

        var changes = new List<DefinitionRevisionChange>();

        CompareNodes(
            baseline.Configuration,
            candidate.Configuration,
            "/configuration",
            FieldSensitivity.Public,
            FieldSensitivity.Public,
            changes,
            cancellationToken);

        CompareArtifacts(baseline, candidate, changes, cancellationToken);
        CompareRootDocuments(baseline.Document, candidate.Document, changes);

        changes.Sort(static (left, right) =>
        {
            int impact = left.Impact.CompareTo(right.Impact);
            if (impact != 0) return impact;
            int path = StringComparer.Ordinal.Compare(left.Path, right.Path);
            if (path != 0) return path;
            int category = StringComparer.Ordinal.Compare(left.Category, right.Category);
            return category != 0 ? category : StringComparer.Ordinal.Compare(left.Detail, right.Detail);
        });

        return new DefinitionRevisionComparisonReport(baseline, candidate, changes.AsReadOnly());
    }

    private static void CompareNodes(
        DefinitionNode baseline,
        DefinitionNode candidate,
        string path,
        FieldSensitivity baselineInherited,
        FieldSensitivity candidateInherited,
        List<DefinitionRevisionChange> changes,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        FieldSensitivity baselineSensitivity = EffectiveSensitivity(baseline, baselineInherited);
        FieldSensitivity candidateSensitivity = EffectiveSensitivity(candidate, candidateInherited);

        if (baseline.Type != candidate.Type || baseline.IsNullable != candidate.IsNullable)
            changes.Add(new DefinitionRevisionChange(
                DefinitionRevisionImpactKind.Behavioral,
                DefinitionRevisionChangeCategory.TypeChanged,
                path,
                "modified"));

        if (baselineSensitivity != candidateSensitivity)
            changes.Add(new DefinitionRevisionChange(
                DefinitionRevisionImpactKind.Behavioral,
                DefinitionRevisionChangeCategory.SensitivityChanged,
                path,
                SensitivityName(baselineSensitivity) + "->" + SensitivityName(candidateSensitivity)));

        ComparePresenceProperty(
            ExtensionMember(baseline.Keywords, "applicableWhen"),
            ExtensionMember(candidate.Keywords, "applicableWhen"),
            DefinitionRevisionChangeCategory.ActivationChanged,
            path,
            changes);

        ComparePresenceProperty(
            ExtensionMember(baseline.Keywords, "requiredWhen"),
            ExtensionMember(candidate.Keywords, "requiredWhen"),
            DefinitionRevisionChangeCategory.RequirednessChanged,
            path,
            changes);

        if (!OptionalElementEquals(Property(baseline.Keywords, "if"), Property(candidate.Keywords, "if"))
            || !OptionalElementEquals(Property(baseline.Keywords, "then"), Property(candidate.Keywords, "then"))
            || !OptionalElementEquals(Property(baseline.Keywords, "else"), Property(candidate.Keywords, "else"))
            || !OptionalElementEquals(Property(baseline.Keywords, "dependentRequired"), Property(candidate.Keywords, "dependentRequired")))
        {
            changes.Add(new DefinitionRevisionChange(
                DefinitionRevisionImpactKind.Behavioral,
                DefinitionRevisionChangeCategory.RequirednessChanged,
                path,
                "modified"));
        }

        if (HasConstraintDifference(baseline.Keywords, candidate.Keywords))
            changes.Add(new DefinitionRevisionChange(
                DefinitionRevisionImpactKind.Behavioral,
                DefinitionRevisionChangeCategory.ConstraintChanged,
                path,
                "modified"));

        if (HasInitialValueDifference(baseline.Keywords, candidate.Keywords))
            changes.Add(new DefinitionRevisionChange(
                DefinitionRevisionImpactKind.Behavioral,
                DefinitionRevisionChangeCategory.InitialValueChanged,
                path,
                "modified"));

        CompareSecretPolicy(baseline.Keywords, candidate.Keywords, path, changes);

        if (HasDisplayDifference(baseline.Keywords, candidate.Keywords))
            changes.Add(new DefinitionRevisionChange(
                DefinitionRevisionImpactKind.DisplayOnly,
                DefinitionRevisionChangeCategory.DisplayTextChanged,
                path,
                "modified"));

        // Compare declared properties by exact key only (never guess renames).
        var propertyNames = new SortedSet<string>(baseline.Properties.Keys, StringComparer.Ordinal);
        foreach (string name in candidate.Properties.Keys)
            propertyNames.Add(name);

        foreach (string name in propertyNames)
        {
            string childPath = path + "/properties/" + Escape(name);
            bool inBase = baseline.Properties.TryGetValue(name, out DefinitionNode? baseChild);
            bool inCand = candidate.Properties.TryGetValue(name, out DefinitionNode? candChild);
            if (!inBase)
            {
                changes.Add(new DefinitionRevisionChange(
                    DefinitionRevisionImpactKind.Behavioral,
                    DefinitionRevisionChangeCategory.FieldAdded,
                    childPath,
                    "added"));
                continue;
            }

            if (!inCand)
            {
                changes.Add(new DefinitionRevisionChange(
                    DefinitionRevisionImpactKind.Behavioral,
                    DefinitionRevisionChangeCategory.FieldRemoved,
                    childPath,
                    "removed"));
                continue;
            }

            bool baseRequired = baseline.RequiredSet.Contains(name);
            bool candRequired = candidate.RequiredSet.Contains(name);
            if (baseRequired != candRequired)
                changes.Add(new DefinitionRevisionChange(
                    DefinitionRevisionImpactKind.Behavioral,
                    DefinitionRevisionChangeCategory.RequirednessChanged,
                    childPath,
                    candRequired ? "added" : "removed"));

            CompareNodes(baseChild!, candChild!, childPath, baselineSensitivity, candidateSensitivity, changes, cancellationToken);
        }

        // Compare array items
        if (baseline.Items is not null && candidate.Items is not null)
        {
            CompareNodes(baseline.Items, candidate.Items, path + "/items", baselineSensitivity, candidateSensitivity, changes, cancellationToken);
        }
        else if (baseline.Items is not null || candidate.Items is not null)
        {
            changes.Add(new DefinitionRevisionChange(
                DefinitionRevisionImpactKind.Behavioral,
                DefinitionRevisionChangeCategory.ConstraintChanged,
                path + "/items",
                candidate.Items is not null ? "added" : "removed"));
        }

        // Compare typed map additionalProperties
        if (baseline.AdditionalProperties is not null && candidate.AdditionalProperties is not null)
        {
            CompareNodes(baseline.AdditionalProperties, candidate.AdditionalProperties, path + "/additionalProperties",
                baselineSensitivity, candidateSensitivity, changes, cancellationToken);
        }
        else if (baseline.AdditionalProperties is not null || candidate.AdditionalProperties is not null)
        {
            changes.Add(new DefinitionRevisionChange(
                DefinitionRevisionImpactKind.Behavioral,
                DefinitionRevisionChangeCategory.ConstraintChanged,
                path + "/additionalProperties",
                candidate.AdditionalProperties is not null ? "added" : "removed"));
        }

        // Compare discriminated oneOf branches
        if (baseline.Variants.Count > 0 || candidate.Variants.Count > 0)
        {
            int shared = Math.Min(baseline.Variants.Count, candidate.Variants.Count);
            if (baseline.Variants.Count != candidate.Variants.Count
                || !string.Equals(baseline.Discriminator, candidate.Discriminator, StringComparison.Ordinal))
            {
                changes.Add(new DefinitionRevisionChange(
                    DefinitionRevisionImpactKind.Behavioral,
                    DefinitionRevisionChangeCategory.ConstraintChanged,
                    path + "/oneOf",
                    "modified"));
            }

            for (int index = 0; index < shared; index++)
            {
                CompareNodes(
                    baseline.Variants[index],
                    candidate.Variants[index],
                    path + "/oneOf/" + index.ToString(CultureInfo.InvariantCulture),
                    baselineSensitivity,
                    candidateSensitivity,
                    changes,
                    cancellationToken);
            }
        }
    }

    private static void CompareArtifacts(
        Definition baseline,
        Definition candidate,
        List<DefinitionRevisionChange> changes,
        CancellationToken cancellationToken)
    {
        var baseMap = new Dictionary<string, ArtifactDefinition>(StringComparer.Ordinal);
        foreach (ArtifactDefinition item in baseline.Artifacts) baseMap[item.Id] = item;

        var candMap = new Dictionary<string, ArtifactDefinition>(StringComparer.Ordinal);
        foreach (ArtifactDefinition item in candidate.Artifacts) candMap[item.Id] = item;

        var ids = new SortedSet<string>(baseMap.Keys, StringComparer.Ordinal);
        foreach (string id in candMap.Keys) ids.Add(id);

        foreach (string id in ids)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string path = "/artifacts/" + Escape(id);
            bool inBase = baseMap.TryGetValue(id, out ArtifactDefinition? baseArtifact);
            bool inCand = candMap.TryGetValue(id, out ArtifactDefinition? candArtifact);

            if (!inBase)
            {
                changes.Add(new DefinitionRevisionChange(
                    DefinitionRevisionImpactKind.Behavioral,
                    DefinitionRevisionChangeCategory.OutputAdded,
                    path,
                    "added"));
                continue;
            }

            if (!inCand)
            {
                changes.Add(new DefinitionRevisionChange(
                    DefinitionRevisionImpactKind.Behavioral,
                    DefinitionRevisionChangeCategory.OutputRemoved,
                    path,
                    "removed"));
                continue;
            }

            if (!string.Equals(baseArtifact!.FileName, candArtifact!.FileName, StringComparison.Ordinal)
                || baseArtifact.Format != candArtifact.Format
                || baseArtifact.ApplySemantics != candArtifact.ApplySemantics
                || baseArtifact.Restart != candArtifact.Restart
                || baseArtifact.EnvironmentDialect != candArtifact.EnvironmentDialect
                || !string.Equals(baseArtifact.Surface, candArtifact.Surface, StringComparison.Ordinal)
                || !baseArtifact.ConsumerIds.SequenceEqual(candArtifact.ConsumerIds, StringComparer.Ordinal)
                || !JsonElement.DeepEquals(baseArtifact.Projection, candArtifact.Projection))
            {
                changes.Add(new DefinitionRevisionChange(
                    DefinitionRevisionImpactKind.Behavioral,
                    DefinitionRevisionChangeCategory.OutputMappingChanged,
                    path,
                    "modified"));
            }

            if (!string.Equals(baseArtifact.Title, candArtifact.Title, StringComparison.Ordinal)
                || !string.Equals(baseArtifact.Description, candArtifact.Description, StringComparison.Ordinal))
            {
                changes.Add(new DefinitionRevisionChange(
                    DefinitionRevisionImpactKind.DisplayOnly,
                    DefinitionRevisionChangeCategory.DisplayTextChanged,
                    path,
                    "modified"));
            }
        }
    }

    private static void CompareRootDocuments(
        JsonElement baseline,
        JsonElement candidate,
        List<DefinitionRevisionChange> changes)
    {
        if (!OptionalElementEquals(Property(baseline, "deployment"), Property(candidate, "deployment")))
            changes.Add(new DefinitionRevisionChange(
                DefinitionRevisionImpactKind.Behavioral,
                DefinitionRevisionChangeCategory.DeploymentChanged,
                "/deployment",
                TransitionDetail(Property(baseline, "deployment"), Property(candidate, "deployment"))));

        foreach (string member in new[] { "requiredCapabilities", "applicationVersions", "entryPoints" })
        {
            if (!OptionalElementEquals(Property(baseline, member), Property(candidate, member)))
                changes.Add(new DefinitionRevisionChange(
                    DefinitionRevisionImpactKind.Behavioral,
                    DefinitionRevisionChangeCategory.CapabilityChanged,
                    "/" + member,
                    "modified"));
        }

        if (!OptionalElementEquals(Property(baseline, "project"), Property(candidate, "project")))
            changes.Add(new DefinitionRevisionChange(
                DefinitionRevisionImpactKind.DisplayOnly,
                DefinitionRevisionChangeCategory.DisplayTextChanged,
                "/project",
                "modified"));

        if (!OptionalElementEquals(Property(baseline, "presentation"), Property(candidate, "presentation")))
            changes.Add(new DefinitionRevisionChange(
                DefinitionRevisionImpactKind.DisplayOnly,
                DefinitionRevisionChangeCategory.PresentationChanged,
                "/presentation",
                TransitionDetail(Property(baseline, "presentation"), Property(candidate, "presentation"))));

        if (!OptionalElementEquals(Property(baseline, "locales"), Property(candidate, "locales"))
            || !OptionalElementEquals(Property(baseline, "localization"), Property(candidate, "localization")))
            changes.Add(new DefinitionRevisionChange(
                DefinitionRevisionImpactKind.DisplayOnly,
                DefinitionRevisionChangeCategory.LocalizationChanged,
                "/localization",
                TransitionDetail(Property(baseline, "localization"), Property(candidate, "localization"))));

        if (!OptionalElementEquals(Property(baseline, "annotations"), Property(candidate, "annotations")))
            changes.Add(new DefinitionRevisionChange(
                DefinitionRevisionImpactKind.DisplayOnly,
                DefinitionRevisionChangeCategory.AnnotationsChanged,
                "/annotations",
                TransitionDetail(Property(baseline, "annotations"), Property(candidate, "annotations"))));
    }

    private static void CompareSecretPolicy(
        JsonElement baseline,
        JsonElement candidate,
        string path,
        List<DefinitionRevisionChange> changes)
    {
        JsonElement? baseSecret = ExtensionMember(baseline, "secret");
        JsonElement? candSecret = ExtensionMember(candidate, "secret");
        JsonElement? baseWriteOnly = Property(baseline, "writeOnly");
        JsonElement? candWriteOnly = Property(candidate, "writeOnly");

        if (!OptionalElementEquals(baseSecret, candSecret))
        {
            changes.Add(new DefinitionRevisionChange(
                DefinitionRevisionImpactKind.Behavioral,
                DefinitionRevisionChangeCategory.SecretPolicyChanged,
                path,
                TransitionDetail(baseSecret, candSecret)));
        }
        else if (!OptionalElementEquals(baseWriteOnly, candWriteOnly))
        {
            changes.Add(new DefinitionRevisionChange(
                DefinitionRevisionImpactKind.Behavioral,
                DefinitionRevisionChangeCategory.SecretPolicyChanged,
                path,
                TransitionDetail(baseWriteOnly, candWriteOnly)));
        }
    }

    private static void ComparePresenceProperty(
        JsonElement? baseline,
        JsonElement? candidate,
        string category,
        string path,
        List<DefinitionRevisionChange> changes)
    {
        if (OptionalElementEquals(baseline, candidate)) return;
        changes.Add(new DefinitionRevisionChange(
            DefinitionRevisionImpactKind.Behavioral,
            category,
            path,
            TransitionDetail(baseline, candidate)));
    }

    private static bool HasConstraintDifference(JsonElement baseline, JsonElement candidate)
    {
        foreach (string keyword in ConstraintKeywords)
            if (!OptionalElementEquals(Property(baseline, keyword), Property(candidate, keyword)))
                return true;
        foreach (string keyword in ExtensionConstraintKeywords)
            if (!OptionalElementEquals(ExtensionMember(baseline, keyword), ExtensionMember(candidate, keyword)))
                return true;
        return false;
    }

    private static bool HasInitialValueDifference(JsonElement baseline, JsonElement candidate)
    {
        if (!OptionalElementEquals(Property(baseline, "default"), Property(candidate, "default")))
            return true;
        foreach (string keyword in ExtensionValueKeywords)
            if (!OptionalElementEquals(ExtensionMember(baseline, keyword), ExtensionMember(candidate, keyword)))
                return true;
        return false;
    }

    private static bool HasDisplayDifference(JsonElement baseline, JsonElement candidate)
    {
        foreach (string keyword in DisplayKeywords)
            if (!OptionalElementEquals(Property(baseline, keyword), Property(candidate, keyword)))
                return true;
        foreach (string keyword in ExtensionDisplayKeywords)
            if (!OptionalElementEquals(ExtensionMember(baseline, keyword), ExtensionMember(candidate, keyword)))
                return true;
        return false;
    }

    private static FieldSensitivity EffectiveSensitivity(DefinitionNode node, FieldSensitivity inherited) =>
        (FieldSensitivity)Math.Max((int)inherited, (int)FieldDefinition.SensitivityOf(node));

    private static string SensitivityName(FieldSensitivity sensitivity) => sensitivity switch
    {
        FieldSensitivity.Public => "public",
        FieldSensitivity.Sensitive => "sensitive",
        FieldSensitivity.Secret => "secret",
        _ => "sensitive"
    };

    private static string TransitionDetail(JsonElement? baseline, JsonElement? candidate) =>
        (baseline.HasValue, candidate.HasValue) switch
        {
            (false, true) => "added",
            (true, false) => "removed",
            _ => "modified"
        };

    private static JsonElement? Property(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out JsonElement value) ? value : null;

    private static JsonElement? ExtensionMember(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty("x-configurator", out JsonElement extension)
        && extension.ValueKind == JsonValueKind.Object
        && extension.TryGetProperty(name, out JsonElement value)
            ? value
            : null;

    private static bool OptionalElementEquals(JsonElement? left, JsonElement? right) =>
        (left, right) switch
        {
            (null, null) => true,
            (JsonElement a, JsonElement b) => JsonElement.DeepEquals(a, b),
            _ => false
        };

    private static string Escape(string segment) =>
        segment.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal);
}
