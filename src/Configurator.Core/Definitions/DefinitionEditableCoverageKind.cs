namespace Configurator.Core.Definitions;

using System.Text.Json;
using Configurator.Core.Dotenv;
using Configurator.Core.Environment;

public enum DefinitionEditableCoverageKind
{
    Editable,
    ReadOnly,
    Protected,
    Inactive,
    ConditionUnresolved,
    Unsupported,
    Ambiguous,
    Controller,
}
