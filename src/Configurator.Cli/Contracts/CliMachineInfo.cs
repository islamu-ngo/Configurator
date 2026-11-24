namespace Configurator.Cli;

public sealed record CliMachineSchemaDescriptor(string Kind, string Id, string FileName, string Digest);

public sealed record CliMachineSchemaCatalog(IReadOnlyList<CliMachineSchemaDescriptor> Schemas);

public sealed record CliMachineInfo(
    string Application,
    string Version,
    string Profile,
    string AuthoringContractVersion,
    string RuntimeSchemaVersion,
    string AnswerContractVersion,
    string FixtureContractVersion,
    string CommandEnvelopeVersion,
    bool ProtectedWriteAvailable,
    IReadOnlyList<CliMachineSchemaDescriptor> Schemas,
    IReadOnlyList<string> Capabilities);
