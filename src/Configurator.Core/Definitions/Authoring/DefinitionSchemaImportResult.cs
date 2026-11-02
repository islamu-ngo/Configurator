using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Configurator.Core.Definitions.Authoring;

/// <summary>Snapshots a configuration schema and its explicit transformation ledger.</summary>
public sealed class DefinitionSchemaImportResult
{
    internal DefinitionSchemaImportResult(JsonElement configuration, IEnumerable<DefinitionSchemaTransformation> transformations)
    {
        Configuration = configuration.Clone();
        Transformations = Array.AsReadOnly(transformations.ToArray());
    }
    public JsonElement Configuration { get; }
    public IReadOnlyList<DefinitionSchemaTransformation> Transformations { get; }
}
