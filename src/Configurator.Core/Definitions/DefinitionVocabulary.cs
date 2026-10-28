namespace Configurator.Core.Definitions;

internal static class DefinitionVocabulary
{
    internal static readonly string[] FieldTypes = ["string", "boolean", "integer", "number", "array", "object"];
    internal static readonly string[] Sensitivities = ["public", "sensitive", "secret"];
    internal static readonly string[] Formats = ["dotenv", "json", "yaml", "markdown"];
    internal static readonly string[] TargetUpdateModes = ["unspecified", "create", "merge", "replace", "import", "reconcile", "generationOnly"];
    internal static readonly string[] RestartRequirements = ["unspecified", "none", "process", "deployment"];
    internal static readonly string[] ProjectionOperations = ["literal", "copy", "omit", "index", "object", "list", "repeat"];
}
