namespace Configurator.Core.Definitions;

public sealed record DefinitionSecretConstraints
{
    public DefinitionSecretCharacterSet CharacterSet { get; init; } = DefinitionSecretCharacterSet.Unicode;
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
    public DefinitionSecretCurve? Curve { get; init; }
    public int MemoryKiB { get; init; } = 19456;
    public int Iterations { get; init; } = 2;
    public int Parallelism { get; init; } = 1;
    public int SaltBytes { get; init; } = 16;
    public int DerivedBytes { get; init; } = 32;
    public int Cost { get; init; } = 12;
}
