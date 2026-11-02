using System.Text.Json;

namespace Configurator.Cli;

public sealed record CliMachineSchema(string Kind, string Id, string Digest, string Profile,
    string AuthoringContractVersion, string RuntimeSchemaVersion, string AnswerContractVersion,
    IReadOnlyList<string> Capabilities, JsonElement Document);
