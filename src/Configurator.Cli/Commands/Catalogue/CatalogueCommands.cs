namespace Configurator.Cli.Commands;

internal static class CatalogueCommands
{
    internal static CliFamilyDescriptor Descriptor { get; } = CliCommandRegistry.Family(
        "catalogue",
        CliCommandRegistry.Operation<CatalogueListSettings>("list"),
        CliCommandRegistry.Operation<CatalogueItemSettings>("describe"));
}
