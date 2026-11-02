namespace Configurator.Cli.Commands;

internal static class DoctorCommands
{
    internal static CliFamilyDescriptor Descriptor { get; } = CliCommandRegistry.Family(
        "doctor",
        CliCommandRegistry.Operation<DoctorSettings>("doctor"));
}
