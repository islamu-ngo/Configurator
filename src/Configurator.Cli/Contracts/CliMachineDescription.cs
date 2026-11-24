using System.Text.Json.Serialization;

namespace Configurator.Cli;

public sealed record CliMachineFieldDescription(
    string Id,
    string SchemaPointer,
    string Kind,
    bool Nullable,
    bool Required,
    string Sensitivity,
    string Detail,
    bool PolicyDeclared,
    bool ConditionalActivation,
    bool ConditionalRequirement,
    bool HasChoices)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Acquisition { get; init; }
}

public sealed record CliMachineDefinitionDescription(
    string FamilyId,
    long Revision,
    string DefinitionDigest,
    string ApplicationId,
    string Profile,
    IReadOnlyList<string> RequiredCapabilities,
    IReadOnlyList<string> ArtifactIds,
    IReadOnlyList<string> Locales,
    IReadOnlyList<string> ApplicationVersions,
    IReadOnlyList<string> EntryPoints,
    int Offset,
    int Limit,
    int Total,
    string Digest,
    IReadOnlyList<CliMachineFieldDescription> Fields)
{
    public int RetainedCount => Total;
    public int ReturnedCount => Fields.Count;
    public bool Terminal => Offset + Fields.Count >= Total;
    public int? NextOffset => Terminal ? null : Offset + Fields.Count;
}
