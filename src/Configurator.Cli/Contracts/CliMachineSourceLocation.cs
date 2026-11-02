namespace Configurator.Cli;

public sealed record CliMachineSourceLocation(string SchemaPath, int DocumentIndex,
    int StartLine, int StartColumn, int EndLine, int EndColumn);
