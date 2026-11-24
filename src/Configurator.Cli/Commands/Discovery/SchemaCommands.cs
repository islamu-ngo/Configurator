namespace Configurator.Cli.Commands;

internal sealed class SchemaListSettings : CliCommandSettings;

internal static class SchemaCommands
{
    internal static CliFamilyDescriptor Descriptor { get; } = CliCommandRegistry.Family(
        "schema",
        CliCommandRegistry.Operation<SchemaListSettings>("list"),
        CliCommandRegistry.Operation<SchemaExportSettings>("export"));
}
