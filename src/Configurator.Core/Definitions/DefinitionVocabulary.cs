namespace Configurator.Core.Definitions;

internal static class DefinitionVocabulary
{
    internal static readonly string[] FieldTypes = ["string", "boolean", "integer", "number", "enum", "list", "object"];
    internal static readonly string[] Sensitivities = ["public", "sensitive", "secret"];
    internal static readonly string[] Disclosures = ["essential", "guided", "advanced"];
    internal static readonly string[] Validators = ["none", "hostname", "absolute-uri", "email-address", "port-number", "positive-integer"];
    internal static readonly string[] ItemTypes = ["string", "boolean", "integer", "number", "object"];
    internal static readonly string[] Formats = ["dotenv", "json", "yaml", "markdown"];
    internal static readonly string[] ApplySemantics = ["create", "merge", "replace", "import", "reconcile", "generationOnly"];
    internal static readonly string[] Restarts = ["none", "process", "deployment"];
    internal static readonly string[] ConditionOperations = ["present", "equals", "notEquals", "in", "all", "any", "not"];
    internal static readonly string[] ProjectionOperations = ["literal", "copy", "omit", "index", "object", "list", "repeat"];
}
