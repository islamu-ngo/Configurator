using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Configurator.Core.Definitions.Authoring;

/// <summary>A value-free account of one public schema concept. Document zero is the supplied root.</summary>
public sealed record DefinitionSchemaTransformation(string Classification, string Code, string Location, int DocumentOrdinal);
