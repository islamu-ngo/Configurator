namespace Configurator.Presentation;

using System.Text.Json;
using Configurator.Core.Definitions;

public enum DefinitionFieldCorrectionReason { Required, Type, Constraint, Duplicate }
public enum DefinitionFieldBoundKind { Minimum, Maximum, ExclusiveMinimum, ExclusiveMaximum, MinLength, MaxLength, MinItems, MaxItems, MinProperties, MaxProperties }
public sealed record DefinitionFieldBound(DefinitionFieldBoundKind Kind, decimal Value);

/// <summary>Value-safe current editor affordances and authored requirements; contains no private coordinates or supplied values.</summary>
public sealed record DefinitionFieldValidation(
    bool CanEdit, bool CanApplySuggestion, bool CanSetNull, bool CanReset, bool CanOmit,
    bool RequiresMasking, bool HasProtectedDescendants,
    IReadOnlyList<DefinitionFieldCorrectionReason> Reasons,
    IReadOnlyList<DefinitionFieldBound> Bounds, IReadOnlyList<JsonElement> AllowedValues);

public sealed partial class DefinitionFormModel
{
    public DefinitionFieldValidation GetFieldValidation(IReadOnlyList<string> path)
    {
        ArgumentNullException.ThrowIfNull(path);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return FieldValidation(path, literal: false);
        }
    }

    /// <summary>Uses literal configuration coordinates, including the empty root path.</summary>
    public DefinitionFieldValidation GetFieldValidation(DefinitionPath path)
    {
        ArgumentNullException.ThrowIfNull(path);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return TryOrdinaryRootPath(path.Segments, out string[] ordinary)
                ? FieldValidation(ordinary, literal: false) : FieldValidation(path.Segments, literal: true);
        }
    }

    private DefinitionFieldValidation FieldValidation(IReadOnlyList<string> path, bool literal)
    {
        bool found = literal
            ? TryRootMetadata(path, out DefinitionFormIndex.Node node, out bool inheritedSecret, out bool inheritedProtected)
            : TryMetadataNode(path, out node, out inheritedSecret, out inheritedProtected);
        if (!found) return new(false, false, false, false, false, true, true, [], [], []);
        DefinitionNode schema = node.Schema;
        DefinitionPath dataPath = literal ? DefinitionPath.FromSegments(path) : DataPath(path);
        DefinitionNodeState? state = Facts.EditorState(dataPath);
        // Masking follows declared sensitivity: a non-public declaration on the value or anything above it, or a
        // protected descendant inside the value's own whole representation. Public siblings stay readable.
        bool masking = inheritedProtected || node.IsProtected ||
            state?.Sensitivity is { } sensitivity && sensitivity != FieldSensitivity.Public;
        // Ordinary property validation has no synthetic root node; the configuration root itself is always present.
        bool configurationRoot = literal && path.Count == 0;
        bool editable = configurationRoot || IsEditorActive(dataPath);
        bool writable = (configurationRoot || state is not null || editable) &&
            (!Definition.Configuration.Keywords.TryGetProperty("readOnly", out JsonElement rootReadOnly) ||
                !rootReadOnly.GetBoolean());
        for (int length = literal ? 0 : 1; length <= path.Count; length++)
        {
            string[] prefix = path.Take(length).ToArray();
            bool hasOwner = literal ? TryRootMetadata(prefix, out var owner, out _)
                : TryMetadataNode(prefix, out owner, out _);
            if (!hasOwner) { editable = false; continue; }
            writable &= !owner.Metadata.TryGetProperty("readOnly", out JsonElement readOnly) || !readOnly.GetBoolean();
        }
        writable &= SessionPrivacy != DefinitionSessionPrivacy.BrowserNoSecrets ||
            !DefinitionSecretPolicy.IsSecret(schema, inheritedSecret);
        editable &= writable;
        var reasons = new List<DefinitionFieldCorrectionReason>();
        if (state is { IsActive: true, IsRequired: true, IsMissing: true }) reasons.Add(DefinitionFieldCorrectionReason.Required);
        if ((literal ? _editing.RootInput.InvalidPaths.Any(invalid => invalid.SequenceEqual(path))
            : HasInputError(path)) && state?.IsActive != false)
            reasons.Add(DefinitionFieldCorrectionReason.Type);
        reasons.AddRange(Facts.CorrectionReasons(dataPath));
        if (state is { HasError: true } && reasons.Count == 0) reasons.Add(DefinitionFieldCorrectionReason.Constraint);
        var bounds = new List<DefinitionFieldBound>();
        foreach (DefinitionFieldBoundKind kind in Enum.GetValues<DefinitionFieldBoundKind>())
        {
            string name = char.ToLowerInvariant(kind.ToString()[0]) + kind.ToString()[1..];
            if (schema.Keywords.TryGetProperty(name, out JsonElement value) && value.TryGetDecimal(out decimal bound))
                bounds.Add(new(kind, bound));
        }
        IReadOnlyList<JsonElement> allowed = !masking && schema.Keywords.TryGetProperty("enum", out var values)
            ? Array.AsReadOnly(values.EnumerateArray().Select(value => value.Clone()).ToArray()) : [];
        bool suggestion = editable && !masking &&
            schema.Keywords.TryGetProperty("x-configurator", out var extension) && extension.TryGetProperty("suggestedValue", out _);
        // Reset removes an override/omission; it never supplies an inactive value or seeds an absent container.
        bool arrayItem = node.Parent?.Schema.Type == DefinitionNodeType.Sequence;
        return new(editable, suggestion, editable && schema.IsNullable, writable && !arrayItem,
            editable && dataPath.Segments.Count > 0 && !arrayItem,
            masking, masking, Array.AsReadOnly(reasons.Distinct().ToArray()), bounds.AsReadOnly(), allowed);
    }

}
