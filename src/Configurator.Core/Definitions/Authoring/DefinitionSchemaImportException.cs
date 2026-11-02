using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Configurator.Core.Definitions.Authoring;

public sealed class DefinitionSchemaImportException : Exception
{
    internal DefinitionSchemaImportException(string code, IEnumerable<DefinitionSchemaTransformation>? transformations = null) : base(code)
    {
        Code = code;
        Transformations = Array.AsReadOnly((transformations ?? []).ToArray());
    }
    public string Code { get; }
    public IReadOnlyList<DefinitionSchemaTransformation> Transformations { get; }
}
