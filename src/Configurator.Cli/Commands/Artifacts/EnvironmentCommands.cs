namespace Configurator.Cli.Commands;

internal static class EnvironmentCommands
{
    internal static CliFamilyDescriptor Descriptor { get; } = CliCommandRegistry.Family(
        "env",
        CliCommandRegistry.Operation<ArtifactOutputSettings>("render"),
        CliCommandRegistry.Operation<ArtifactInputSettings>("validate"));
}
