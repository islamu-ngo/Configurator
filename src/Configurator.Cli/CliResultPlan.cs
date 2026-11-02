namespace Configurator.Cli;

internal static class CliResultPlan
{
    // Reserve optional detail and bounded operational diagnostics before any publication.
    internal static void EnsureCapacity(CliInvocation invocation, CliCommand command, CliCommandResult result)
    {
        CliCommandResult largest = result with
        {
            Exit = CliExitCode.Internal,
            Completion = "cancelled",
            Cleanup = "not-required",
            Artifacts = result.Artifacts.Select(artifact => artifact with
            {
                WriteStatus = "not-attempted",
                Installation = "not-established",
                Verification = "not-applicable"
            }).ToArray(),
            Diagnostics = Enumerable.Range(0, 16).Select(_ => new CliMachineDiagnostic(new string('x', 96),
                "/" + new string('x', 255), "warning")).ToArray()
        };
        byte[] bytes = CliMachineOutput.Serialize(command with { Detail = null }, largest, 65_536);
        if (bytes.Length + 8192 > invocation.Io.MaximumOutputBytes) throw new IOException("receipt-capacity-unavailable");
    }
}
