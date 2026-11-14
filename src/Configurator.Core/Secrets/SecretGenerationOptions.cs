namespace Configurator.Core.Secrets;

/// <summary>Operator-selectable options within <see cref="SecretGenerationBounds"/>. Options can tighten, never relax, schema requirements.</summary>
public sealed record SecretGenerationOptions
{
    public int? Length { get; init; }
    public int? Bytes { get; init; }
    public bool? IncludeLowercase { get; init; }
    public bool? IncludeUppercase { get; init; }
    public bool? IncludeDigits { get; init; }
    public bool? IncludeSymbols { get; init; }
    public string AdditionalForbiddenCharacters { get; init; } = "";

    public static SecretGenerationOptions FromBounds(SecretGenerationBounds bounds)
    {
        ArgumentNullException.ThrowIfNull(bounds);
        if (bounds.IsPasswordBased)
        {
            return new SecretGenerationOptions
            {
                Length = bounds.DefaultLength,
                IncludeLowercase = bounds.LowerPool.Length > 0,
                IncludeUppercase = bounds.UpperPool.Length > 0,
                IncludeDigits = bounds.DigitPool.Length > 0,
                IncludeSymbols = bounds.SymbolPool.Length > 0
            };
        }
        if (bounds.IsByteTokenBased)
        {
            return new SecretGenerationOptions { Bytes = bounds.DefaultBytes };
        }
        return new SecretGenerationOptions();
    }
}
