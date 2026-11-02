namespace Configurator.Core.Definitions;

/// <summary>Public, bounded secret policy. It carries no secret values or target authority.</summary>
public sealed record DefinitionSecretSpecification
{
    public required string Kind { get; init; }
    public required string Acquisition { get; init; }
    public string? Algorithm { get; init; }
    public string? Encoding { get; init; }
    public int MinLength { get; init; }
    public int? MaxLength { get; init; }
    public DefinitionSecretConstraints Constraints { get; init; } = new();
    public DefinitionSecretGeneration? Generation { get; init; }
}

public sealed record DefinitionSecretConstraints
{
    public string CharacterSet { get; init; } = "unicode";
    public bool AllowWhitespace { get; init; } = true;
    public int? MaxUtf8Bytes { get; init; }
    public int MinLowercase { get; init; }
    public int MinUppercase { get; init; }
    public int MinDigits { get; init; }
    public int MinSymbols { get; init; }
    public string AllowedSymbols { get; init; } = "!#$%&*+-=?@_";
    public string ForbiddenCharacters { get; init; } = "";
    public int MinBytes { get; init; }
    public int MaxBytes { get; init; } = 4096;
    public int KeySize { get; init; } = 2048;
    public string? Curve { get; init; }
    public int MemoryKiB { get; init; } = 19456;
    public int Iterations { get; init; } = 2;
    public int Parallelism { get; init; } = 1;
    public int SaltBytes { get; init; } = 16;
    public int DerivedBytes { get; init; } = 32;
    public int Cost { get; init; } = 12;
}

public sealed record DefinitionSecretGeneration
{
    public int Length { get; init; } = 32;
    public int Bytes { get; init; } = 32;
    public string Input { get; init; } = "entered";
    public string InputEncoding { get; init; } = "utf8";
    public DefinitionSecretSpecification? Password { get; init; }
}
