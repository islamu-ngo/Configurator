namespace Configurator.Cli.Commands;

internal static class ArtifactCommands
{
    internal static CliFamilyDescriptor Descriptor { get; } = CliCommandRegistry.Family(
        "artifact",
        CliCommandRegistry.Operation<ArtifactOutputSettings>("create"),
        CliCommandRegistry.Operation<ArtifactInputSettings>("open"),
        CliCommandRegistry.Operation<ArtifactInputSettings>("validate"),
        CliCommandRegistry.Operation<ArtifactOutputSettings>("format"),
        CliCommandRegistry.Operation<ArtifactDiffSettings>("diff"),
        CliCommandRegistry.Operation<ArtifactInputSettings>("coverage"),
        CliCommandRegistry.Operation<ArtifactOutputSettings>("export"),
        CliCommandRegistry.Operation<ArtifactImportSettings>("import"),
        CliCommandRegistry.Operation<ArtifactInputSettings>("check-existing"),
        CliCommandRegistry.Operation<ArtifactCompareSettings>("compare"),
        CliCommandRegistry.Operation<ArtifactOutputSettings>("prefill"),
        CliCommandRegistry.Operation<BundleInputSettings>("bundle-check"),
        CliCommandRegistry.Operation<BundleOutputSettings>("bundle-create"),
        CliCommandRegistry.Operation<ArtifactOutputSettings>("preview"));
}
