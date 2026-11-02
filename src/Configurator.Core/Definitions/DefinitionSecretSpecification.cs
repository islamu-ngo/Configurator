namespace Configurator.Core.Definitions;

/// <summary>Public, bounded secret policy. It carries no secret values or target authority.</summary>
public sealed record DefinitionSecretSpecification
{
    public required DefinitionSecretKind Kind { get; init; }
    public required DefinitionSecretAcquisition Acquisition { get; init; }
    public DefinitionSecretAlgorithm? Algorithm { get; init; }
    public DefinitionSecretEncoding? Encoding { get; init; }
    public int MinLength { get; init; }
    public int? MaxLength { get; init; }
    public DefinitionSecretConstraints Constraints { get; init; } = new();
    public DefinitionSecretGeneration? Generation { get; init; }
}
