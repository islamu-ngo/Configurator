using Configurator.Core.Definitions;
using Spectre.Console.Cli;

namespace Configurator.Cli.Commands;


internal static class SecretCommands
{
    internal static CliFamilyDescriptor Descriptor { get; } = CliCommandRegistry.Family("secret",
        CliCommandRegistry.Operation<SecretGenerateSettings>("generate"),
        CliCommandRegistry.Operation<SecretDeriveSettings>("derive"));
}

