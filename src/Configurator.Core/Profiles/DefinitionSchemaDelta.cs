namespace Configurator.Core.Profiles;

/// <summary>A public schema coordinate and change category; no defaults, examples or answer values.</summary>
public sealed record DefinitionSchemaDelta(string SchemaPath, DefinitionSchemaChange Kind);

public enum DefinitionSchemaChange
{
    AddedRequired,
    AddedOptional,
    Removed,
    Required,
    Type,
    Range,
    Choice,
    Activation,
    Sensitivity,
    SecretPolicy,
    Collection,
    Variant,
    OtherConstraint
}
