namespace Configurator.Core.Definitions;

using System.Text.Json;
using Configurator.Core.Composition;

public sealed record DefinitionImportFinding(DefinitionImportFindingKind Kind, string Code,
    DefinitionImportSourceLocation? Source, DefinitionImportSchemaLocation? Schema);
