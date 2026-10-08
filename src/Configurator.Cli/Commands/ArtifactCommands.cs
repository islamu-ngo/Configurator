namespace Configurator.Cli.Commands;

internal static class ArtifactCommands
{
    internal static SetupCliFamilyDescriptor Descriptor { get; } = SetupCliCommandRegistry.Family(
        "artifact",
        SetupCliCommandRegistry.Operation<ArtifactOutputSettings>("create"),
        SetupCliCommandRegistry.Operation<ArtifactInputSettings>("open"),
        SetupCliCommandRegistry.Operation<ArtifactInputSettings>("validate"),
        SetupCliCommandRegistry.Operation<ArtifactOutputSettings>("format"),
        SetupCliCommandRegistry.Operation<ArtifactDiffSettings>("diff"),
        SetupCliCommandRegistry.Operation<ArtifactInputSettings>("coverage"),
        SetupCliCommandRegistry.Operation<ArtifactOutputSettings>("export"),
        SetupCliCommandRegistry.Operation<ArtifactImportSettings>("import"),
        SetupCliCommandRegistry.Operation<ArtifactOutputSettings>("preview"));
}
