namespace Configurator.Cli.Commands;

internal static class EnvironmentCommands
{
    internal static SetupCliFamilyDescriptor Descriptor { get; } = SetupCliCommandRegistry.Family(
        "env",
        SetupCliCommandRegistry.Operation<ArtifactOutputSettings>("render"),
        SetupCliCommandRegistry.Operation<ArtifactInputSettings>("validate"));
}
