namespace Configurator.Core.Definitions.Authoring;

public static class DefinitionAuthoringSchema
{
    /// <summary>Exports the source envelope using the runtime owner's same bounded recursive schema structure.</summary>
    public static string Generate() => DefinitionSchema.Render(DefinitionSchema.Create(source: true));
}
