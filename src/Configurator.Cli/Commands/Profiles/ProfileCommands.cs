namespace Configurator.Cli.Commands;

internal static class ProfileCommands
{
    internal static CliFamilyDescriptor Descriptor { get; } = CliCommandRegistry.Family("profile",
        CliCommandRegistry.Operation<ProfileSaveSettings>("save"),
        CliCommandRegistry.Operation<ProfileListSettings>("list"),
        CliCommandRegistry.Operation<ProfileInputSettings>("inspect"),
        CliCommandRegistry.Operation<ProfileUnlockSettings>("unlock"),
        CliCommandRegistry.Operation<ProfilePlanSettings>("diff"),
        CliCommandRegistry.Operation<ProfilePlanSettings>("upgrade-plan"),
        CliCommandRegistry.Operation<ProfileAdoptSettings>("adopt"),
        CliCommandRegistry.Operation<ProfileSlotsSettings>("slots"),
        CliCommandRegistry.Operation<ProfileRecoverySettings>("recovery-check"));
}
