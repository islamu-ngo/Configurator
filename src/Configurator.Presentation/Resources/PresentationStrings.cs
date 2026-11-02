namespace Configurator.Presentation;

using System.Globalization;
using System.Resources;
using Configurator.Core.Definitions;

/// <summary>Resolves provider guidance against the explicit workspace locale without changing process culture.</summary>
public static class PresentationStrings
{
    private static readonly ResourceManager English = new("Configurator.Presentation.Resources.PresentationStrings", typeof(PresentationStrings).Assembly);
    private static readonly ResourceManager Arabic = new("Configurator.Presentation.Resources.PresentationStrings.ar", typeof(PresentationStrings).Assembly);

    public static CultureInfo Culture(string locale)
    {
        try { return CultureInfo.GetCultureInfo(locale); }
        catch (CultureNotFoundException) { return CultureInfo.GetCultureInfo("en"); }
    }

    public static string Get(string key, string locale)
    {
        ArgumentNullException.ThrowIfNull(key);
        ResourceManager resource = Culture(locale).TwoLetterISOLanguageName == "ar" ? Arabic : English;
        return resource.GetString(key, CultureInfo.InvariantCulture)
            ?? English.GetString(key, CultureInfo.InvariantCulture)
            ?? throw new MissingManifestResourceException($"Missing presentation resource: {key}");
    }

    public static string Format(string key, string locale, params object[] arguments) =>
        string.Format(Culture(locale), Get(key, locale), arguments);

    public static string Diagnostic(string code, string locale) => Get(code switch
    {
        "value-required" => "ValueRequired",
        "value-type-invalid" or "input-type-invalid" => "ValueTypeInvalid",
        "value-duplicate" => "ValueDuplicate",
        "value-invalid" or "value-constraint-invalid" => "ValueConstraintInvalid",
        "answer-budget-exceeded" => "AnswerBudgetExceeded",
        "draft-dependency-unresolved" => "DraftDependencyUnresolved",
        "artifact-omitted" => "ArtifactOmitted",
        "secret-hash-format-invalid" => "SecretHashFormatInvalid",
        "secret-cost-out-of-range" => "SecretCostOutOfRange",
        "secret-profile-unavailable" => "SecretProfileUnavailable",
        "secret-input-invalid" => "SecretInputInvalid",
        "secret-input-limit" => "SecretInputLimit",
        "secret-encoding-invalid" => "SecretEncodingInvalid",
        "secret-cancelled" => "SecretCancelled",
        "secret-stale" => "SecretStale",
        "secret-busy" => "SecretBusy",
        "secret-operation-unavailable" or "secret-generation-unavailable" => "SecretOperationUnavailable",
        "secret-generated-value-invalid" => "SecretGeneratedValueInvalid",
        _ => "ValueConstraintInvalid"
    }, locale);

    public static string ImportFinding(DefinitionImportFindingKind kind, string locale) => Get(kind switch
    {
        DefinitionImportFindingKind.MissingRequired => "ImportMissingRequired",
        DefinitionImportFindingKind.InvalidSuppliedValue => "ImportInvalidValue",
        DefinitionImportFindingKind.UnrecognizedInput => "ImportUnrecognizedInput",
        DefinitionImportFindingKind.StructuralOutputChange => "ImportStructuralChange",
        DefinitionImportFindingKind.Unassessed => "ImportUnassessed",
        _ => "ImportInformation"
    }, locale);

    public static string ImportCompatibility(DefinitionImportCompatibility compatibility, string locale) => Get(compatibility switch
    {
        DefinitionImportCompatibility.Compatible => "ImportCompatible",
        DefinitionImportCompatibility.ChangesRequired => "ImportChangesRequired",
        DefinitionImportCompatibility.Incomplete => "ImportIncomplete",
        DefinitionImportCompatibility.Unsupported => "ImportUnsupported",
        _ => "ImportInvalid"
    }, locale);

    public static string ImportAutofill(string? code, string locale) => Get(code switch
    {
        "import-ready" => "ImportReady",
        "answer-conflict" => "ImportAnswerConflict",
        "import-secret-consent-required" => "ImportConsentRequired",
        "import-stale" => "ImportStale",
        "import-workspace-unsupported" => "ImportWorkspaceUnsupported",
        _ => "ImportAutofillRefused"
    }, locale);
}
