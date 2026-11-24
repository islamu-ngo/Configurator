namespace Configurator.Cli.Commands;

internal sealed class InfoSettings : CliCommandSettings;

internal static class DiscoveryCommands
{
    internal static CliFamilyDescriptor Descriptor { get; } = CliCommandRegistry.Family(
        "info",
        CliCommandRegistry.Operation<InfoSettings>("info"));
}
