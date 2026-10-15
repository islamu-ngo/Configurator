namespace Configurator.Cli;

internal static class SetupCliResultPlan
{
    // Reserve optional detail and bounded operational diagnostics before any publication.
    internal static void EnsureCapacity(SetupCliInvocation invocation, SetupCliCommand command, SetupCliCommandResult result)
    {
        SetupCliCommandResult largest = result with
        {
            Exit = SetupCliExitCode.Internal,
            Completion = "cancelled",
            Cleanup = "not-required",
            Diagnostics = Enumerable.Range(0, 16).Select(_ => new SetupCliMachineDiagnostic(new string('x', 96),
                "/" + new string('x', 255), "warning")).ToArray()
        };
        byte[] bytes = SetupCliMachineOutput.Serialize(command with { Detail = null }, largest, 65_536);
        if (bytes.Length + 8192 > invocation.Io.MaximumOutputBytes) throw new IOException("receipt-capacity-unavailable");
    }
}
