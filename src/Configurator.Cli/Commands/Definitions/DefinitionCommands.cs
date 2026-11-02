using Spectre.Console.Cli;

namespace Configurator.Cli.Commands;


internal static class DefinitionCommands
{
    internal static CliFamilyDescriptor Descriptor { get; } = CliCommandRegistry.Family(
        "definition",
        CliCommandRegistry.Operation<DefinitionInitSettings>("init"),
        CliCommandRegistry.Operation<DefinitionSourceSettings>("check"),
        CliCommandRegistry.Operation<DefinitionSourceSettings>("lint"),
        CliCommandRegistry.Operation<DefinitionSchemaSettings>("schema"),
        CliCommandRegistry.Operation<DefinitionBuildSettings>("build"),
        CliCommandRegistry.Operation<DefinitionCandidateSettings>("revise"));
}

