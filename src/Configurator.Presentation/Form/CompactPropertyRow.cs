namespace Configurator.Presentation;

using Configurator.Core.Definitions;

/// <summary>
/// Consent-safe single-row projection for virtualized compact tables.
/// Enforces BrowserNoSecrets locking without triggering consent, masks private values,
/// and strictly excludes private values and dynamic private map keys from search/sort.
/// </summary>
public sealed record CompactPropertyRow(
    DefinitionPath DataPath,
    string EditorIdentity,
    string SectionId,
    string SectionTitle,
    string DisplayLabel,
    string WireToken,
    string PrimaryLabel,
    string SecondaryLabel,
    DefinitionNodeType NodeType,
    FieldSensitivity Sensitivity,
    bool IsRequired,
    bool IsActive,
    bool HasError,
    bool IsDeferred,
    bool CanEdit,
    bool IsReadOnly,
    bool IsSecretLocked,
    DefinitionAnswerOrigin Origin,
    string ValuePreview,
    string SortKey,
    DefinitionFieldWidget Widget,
    string? Units,
    string? Description,
    string? DefaultText,
    string? IfOmittedDisplay,
    bool IsDeprecated,
    string? DeprecationGuidance,
    IReadOnlyList<DefinitionChoicePresentation> Choices,
    bool HasCopyableWireToken,
    bool IsSensitiveMasked)
{
    /// <summary>Matches query strictly against non-private schema metadata (never private values or private map keys).</summary>
    public bool MatchesQuery(string? query)
    {
        if (string.IsNullOrWhiteSpace(query)) return true;
        string needle = query.Trim();
        return Contains(PrimaryLabel, needle) ||
               Contains(SecondaryLabel, needle) ||
               (HasCopyableWireToken && Contains(WireToken, needle)) ||
               Contains(SectionTitle, needle) ||
               Contains(Units, needle) ||
               Contains(Description, needle) ||
               Contains(DefaultText, needle);
    }

    private static bool Contains(string? haystack, string needle) =>
        haystack is not null && haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);
}
