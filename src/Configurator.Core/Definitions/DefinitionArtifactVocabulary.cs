namespace Configurator.Core.Definitions;

/// <summary>Exact artifact wire spellings; undefined enum values are never serialized.</summary>
public static class DefinitionArtifactVocabulary
{
    internal static DefinitionArtifactFormat ReadFormat(string token) => token switch
    {
        "dotenv" => DefinitionArtifactFormat.Dotenv,
        "json" => DefinitionArtifactFormat.Json,
        "yaml" => DefinitionArtifactFormat.Yaml,
        "markdown" => DefinitionArtifactFormat.Markdown,
        _ => throw new DefinitionException("format-invalid")
    };

    internal static DefinitionTargetUpdateMode ReadTargetUpdateMode(string token) => token switch
    {
        "unspecified" => DefinitionTargetUpdateMode.Unspecified,
        "create" => DefinitionTargetUpdateMode.Create,
        "merge" => DefinitionTargetUpdateMode.Merge,
        "replace" => DefinitionTargetUpdateMode.Replace,
        "import" => DefinitionTargetUpdateMode.Import,
        "reconcile" => DefinitionTargetUpdateMode.Reconcile,
        "generationOnly" => DefinitionTargetUpdateMode.GenerationOnly,
        _ => throw new DefinitionException("target-update-invalid")
    };

    internal static DefinitionRestartRequirement ReadRestartRequirement(string token) => token switch
    {
        "unspecified" => DefinitionRestartRequirement.Unspecified,
        "none" => DefinitionRestartRequirement.None,
        "process" => DefinitionRestartRequirement.Process,
        "deployment" => DefinitionRestartRequirement.Deployment,
        _ => throw new DefinitionException("restart-invalid")
    };

    public static string ToToken(this DefinitionArtifactFormat value) => value switch
    {
        DefinitionArtifactFormat.Dotenv => "dotenv",
        DefinitionArtifactFormat.Json => "json",
        DefinitionArtifactFormat.Yaml => "yaml",
        DefinitionArtifactFormat.Markdown => "markdown",
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    public static string ToToken(this DefinitionTargetUpdateMode value) => value switch
    {
        DefinitionTargetUpdateMode.Unspecified => "unspecified",
        DefinitionTargetUpdateMode.Create => "create",
        DefinitionTargetUpdateMode.Merge => "merge",
        DefinitionTargetUpdateMode.Replace => "replace",
        DefinitionTargetUpdateMode.Import => "import",
        DefinitionTargetUpdateMode.Reconcile => "reconcile",
        DefinitionTargetUpdateMode.GenerationOnly => "generationOnly",
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    public static string ToToken(this DefinitionRestartRequirement value) => value switch
    {
        DefinitionRestartRequirement.Unspecified => "unspecified",
        DefinitionRestartRequirement.None => "none",
        DefinitionRestartRequirement.Process => "process",
        DefinitionRestartRequirement.Deployment => "deployment",
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };
}
