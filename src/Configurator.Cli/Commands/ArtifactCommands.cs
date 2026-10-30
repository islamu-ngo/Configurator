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
        SetupCliCommandRegistry.Operation<ArtifactInputSettings>("check-existing"),
        SetupCliCommandRegistry.Operation<ArtifactCompareSettings>("compare"),
        SetupCliCommandRegistry.Operation<ArtifactOutputSettings>("prefill"),
        SetupCliCommandRegistry.Operation<BundleInputSettings>("bundle-check"),
        SetupCliCommandRegistry.Operation<BundleOutputSettings>("bundle-create"),
        SetupCliCommandRegistry.Operation<ArtifactOutputSettings>("preview"));
}
