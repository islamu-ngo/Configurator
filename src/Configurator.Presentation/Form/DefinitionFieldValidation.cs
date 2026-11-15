namespace Configurator.Presentation;

using System.Text.Json;
using Configurator.Core.Definitions;

public enum DefinitionFieldCorrectionReason { Required, Type, Constraint, Duplicate }
public enum DefinitionFieldBoundKind { Minimum, Maximum, ExclusiveMinimum, ExclusiveMaximum, MinLength, MaxLength, MinItems, MaxItems, MinProperties, MaxProperties }
public sealed record DefinitionFieldBound(DefinitionFieldBoundKind Kind, decimal Value);

/// <summary>Value-free per-field UI stamp used to skip unchanged scalar field re-renders safely.</summary>
public readonly record struct DefinitionScalarFieldStamp(
    bool Exists,
    bool SupportsSkip,
    long FieldVersion,
    long BulkEpoch,
    string Locale,
    WorkspaceNamingMode NamingMode,
    DefinitionSessionPrivacy SessionPrivacy,
    bool OptionalEditorsExpanded,
    bool IsVisible,
    bool HasNodeState,
    bool NodeActive,
    bool NodeRequired,
    bool NodeProvided,
    bool NodeReady,
    bool NodeHasError,
    bool NodeObligation,
    bool NodeObligationReady,
    bool NodeMissing,
    FieldSensitivity NodeSensitivity,
    DefinitionAnswerOrigin Origin,
    bool HasInputError,
    bool IsSecretBlocked,
    bool ProfileBlocks,
    bool RequiresMasking,
    BrowserProfileProjection? BrowserProfile);

/// <summary>Value-safe current editor affordances and authored requirements; contains no private coordinates or supplied values.</summary>
public sealed record DefinitionFieldValidation(
    bool CanEdit, bool CanApplySuggestion, bool CanSetNull, bool CanReset, bool CanOmit,
    bool RequiresMasking, bool HasProtectedDescendants,
    IReadOnlyList<DefinitionFieldCorrectionReason> Reasons,
    IReadOnlyList<DefinitionFieldBound> Bounds, IReadOnlyList<JsonElement> AllowedValues);

public sealed partial class DefinitionFormModel
{
    private static readonly (DefinitionFieldBoundKind Kind, string JsonName)[] s_boundKinds =
    [
        (DefinitionFieldBoundKind.Minimum, "minimum"),
        (DefinitionFieldBoundKind.Maximum, "maximum"),
        (DefinitionFieldBoundKind.ExclusiveMinimum, "exclusiveMinimum"),
        (DefinitionFieldBoundKind.ExclusiveMaximum, "exclusiveMaximum"),
        (DefinitionFieldBoundKind.MinLength, "minLength"),
        (DefinitionFieldBoundKind.MaxLength, "maxLength"),
        (DefinitionFieldBoundKind.MinItems, "minItems"),
        (DefinitionFieldBoundKind.MaxItems, "maxItems"),
        (DefinitionFieldBoundKind.MinProperties, "minProperties"),
        (DefinitionFieldBoundKind.MaxProperties, "maxProperties")
    ];

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

    private bool CanEditCore(IReadOnlyList<string> path, bool literal)
    {
        bool found = literal
            ? TryRootMetadata(path, out DefinitionFormIndex.Node node, out bool inheritedSecret, out _)
            : TryMetadataNode(path, out node, out inheritedSecret, out _);
        if (!found) return false;
        DefinitionPath dataPath = literal ? DefinitionPath.FromSegments(path) : DataPath(path);
        DefinitionNodeState? state = Facts.EditorState(dataPath);
        return ComputeEditAccess(path, literal, node, dataPath, state, inheritedSecret, out _, out _);
    }

    private bool ComputeEditAccess(
        IReadOnlyList<string> path,
        bool literal,
        DefinitionFormIndex.Node node,
        DefinitionPath dataPath,
        DefinitionNodeState? state,
        bool inheritedSecret,
        out bool editable,
        out bool writable)
    {
        bool configurationRoot = literal && path.Count == 0;
        editable = configurationRoot || IsEditorActive(dataPath);
        writable = (configurationRoot || state is not null || editable) &&
            (!Definition.Configuration.Keywords.TryGetProperty("readOnly", out JsonElement rootReadOnly) ||
                !rootReadOnly.GetBoolean());
        for (int length = literal ? 0 : 1; length <= path.Count; length++)
        {
            IReadOnlyList<string> prefix;
            if (length == path.Count)
                prefix = path;
            else if (length == 0)
                prefix = Array.Empty<string>();
            else if (length == 1 && !literal)
                prefix = _index!.RootPaths.GetValueOrDefault(path[0]) ?? [path[0]];
            else
            {
                var slice = new string[length];
                for (int i = 0; i < length; i++) slice[i] = path[i];
                prefix = slice;
            }
            bool hasOwner = literal ? TryRootMetadata(prefix, out var owner, out _)
                : TryMetadataNode(prefix, out owner, out _);
            if (!hasOwner) { editable = false; continue; }
            writable &= !owner.Metadata.TryGetProperty("readOnly", out JsonElement readOnly) || !readOnly.GetBoolean();
        }
        writable &= SessionPrivacy != DefinitionSessionPrivacy.BrowserNoSecrets ||
            !DefinitionSecretPolicy.IsSecret(node.Schema, inheritedSecret);
        writable &= !ProfileBlocks(dataPath);
        editable &= writable;
        return editable;
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
        ComputeEditAccess(path, literal, node, dataPath, state, inheritedSecret, out bool editable, out bool writable);
        var reasons = new List<DefinitionFieldCorrectionReason>();
        if (state is { IsActive: true, IsRequired: true, IsMissing: true }) reasons.Add(DefinitionFieldCorrectionReason.Required);
        if ((literal ? _editing.RootInput.InvalidPaths.Any(invalid => invalid.SequenceEqual(path))
            : HasInputError(path)) && state?.IsActive != false)
            reasons.Add(DefinitionFieldCorrectionReason.Type);
        reasons.AddRange(Facts.CorrectionReasons(dataPath));
        if (state is { HasError: true } && reasons.Count == 0) reasons.Add(DefinitionFieldCorrectionReason.Constraint);
        var bounds = new List<DefinitionFieldBound>();
        foreach (var (kind, jsonName) in s_boundKinds)
        {
            if (schema.Keywords.TryGetProperty(jsonName, out JsonElement value) && value.TryGetDecimal(out decimal bound))
                bounds.Add(new(kind, bound));
        }
        IReadOnlyList<JsonElement> allowed = !masking && schema.Keywords.TryGetProperty("enum", out var values)
            ? Array.AsReadOnly(values.EnumerateArray().Select(value => value.Clone()).ToArray()) : [];
        // Dynamic eligibility and values are resolved only during explicit review/adoption, never while rendering.
        bool suggestion = editable && (schema.PresentationMetadata.SuggestedSource is not null
            || !masking && schema.Keywords.TryGetProperty("x-configurator", out var extension)
                && extension.TryGetProperty("suggestedValue", out _));
        // Reset removes an override/omission; it never supplies an inactive value or seeds an absent container.
        bool arrayItem = node.Parent?.Schema.Type == DefinitionNodeType.Sequence;
        return new(editable, suggestion, editable && schema.IsNullable, writable && !arrayItem,
            editable && dataPath.Segments.Count > 0 && !arrayItem,
            masking, masking,
            reasons.Count == 0 ? [] : Array.AsReadOnly(reasons.Distinct().ToArray()),
            bounds.Count == 0 ? [] : bounds.AsReadOnly(),
            allowed);
    }

}
