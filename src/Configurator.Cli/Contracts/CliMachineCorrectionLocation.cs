namespace Configurator.Cli;

public sealed record CliMachineCorrectionLocation(long InputRevision, string FieldId,
    IReadOnlyList<CliMachineLocationSegment> Segments);
