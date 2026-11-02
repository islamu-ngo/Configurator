namespace Configurator.Core.Definitions;

using System.Text.Json;
using Configurator.Core.Composition;

public enum DefinitionImportFindingKind
{
    MissingRequired, InvalidSuppliedValue, UnrecognizedInput, StructuralOutputChange, Unassessed, ContextDerivedValue
}
