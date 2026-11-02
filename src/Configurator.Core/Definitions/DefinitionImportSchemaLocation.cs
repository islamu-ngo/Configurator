namespace Configurator.Core.Definitions;

using System.Text.Json;
using Configurator.Core.Composition;

/// <summary>Declared schema coordinates stop before dynamic entries; they grant no correction authority.</summary>
public sealed record DefinitionImportSchemaLocation
{
    internal DefinitionImportSchemaLocation(string fieldId, IEnumerable<string> relativePath)
    {
        FieldId = fieldId;
        RelativePath = Array.AsReadOnly(relativePath.ToArray());
    }
    public string FieldId { get; }
    public IReadOnlyList<string> RelativePath { get; }
}
