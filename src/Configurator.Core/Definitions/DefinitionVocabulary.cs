namespace Configurator.Core.Definitions;

internal static class DefinitionVocabulary
{
    internal static readonly string[] FieldTypes = ["string", "boolean", "integer", "number", "array", "object"];
    internal static readonly string[] Sensitivities = ["public", "sensitive", "secret"];
    internal static readonly string[] Formats = Enum.GetValues<DefinitionArtifactFormat>().Select(value => value.ToToken()).ToArray();
    internal static readonly string[] TargetUpdateModes = Enum.GetValues<DefinitionTargetUpdateMode>().Select(value => value.ToToken()).ToArray();
    internal static readonly string[] RestartRequirements = Enum.GetValues<DefinitionRestartRequirement>().Select(value => value.ToToken()).ToArray();
    internal static readonly string[] ProjectionOperations = ["literal", "copy", "omit", "index", "object", "list", "repeat"];
}
