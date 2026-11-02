namespace Configurator.Core.Definitions;

using System.Text.Json;
using Configurator.Core.Composition;

internal sealed record DefinitionImportObservation(DefinitionImportObservationState State,
    bool CompleteSubtree, bool CompleteContainer, DefinitionImportSourceLocation? Source,
    IReadOnlyList<string>? MapEntries = null);
