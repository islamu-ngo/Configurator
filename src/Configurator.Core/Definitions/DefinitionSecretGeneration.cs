namespace Configurator.Core.Definitions;

public sealed record DefinitionSecretGeneration
{
    public int Length { get; init; } = 32;
    public int Bytes { get; init; } = 32;
    public DefinitionSecretInput Input { get; init; } = DefinitionSecretInput.Entered;
    public DefinitionSecretEncoding InputEncoding { get; init; } = DefinitionSecretEncoding.Utf8;
    public DefinitionSecretSpecification? Password { get; init; }
}
