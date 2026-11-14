namespace Configurator.Core.Secrets;

using Configurator.Core.Definitions;

/// <summary>
/// Schema-derived bounds and 128-bit entropy floor for local secret generation options.
/// Clients use this contract to constrain UI controls without duplicating cryptographic policy math.
/// </summary>
public sealed record SecretGenerationBounds
{
    public const int AbsoluteMinPasswordLength = 1;
    public const int AbsoluteMaxPasswordLength = 1024;
    public const int AbsoluteMinBytes = 16;
    public const int AbsoluteMaxBytes = 4096;
    public const int MaxAdditionalForbiddenCharacters = 256;
    public const double MinimumEntropyBits = 128.0;

    internal const string LowercaseAlphabet = "abcdefghijklmnopqrstuvwxyz";
    internal const string UppercaseAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    internal const string DigitAlphabet = "0123456789";

    private SecretGenerationBounds() { }

    public DefinitionSecretKind Kind { get; init; }
    public bool SupportsOptions { get; init; }
    public bool IsPasswordBased { get; init; }
    public bool IsByteTokenBased { get; init; }
    public int MinLength { get; init; }
    public int MaxLength { get; init; }
    public int DefaultLength { get; init; }
    public int MinBytes { get; init; }
    public int MaxBytes { get; init; }
    public int DefaultBytes { get; init; }
    public bool AllowsLowercaseToggle { get; init; }
    public bool AllowsUppercaseToggle { get; init; }
    public bool AllowsDigitsToggle { get; init; }
    public bool AllowsSymbolsToggle { get; init; }
    public bool RequireLowercase { get; init; }
    public bool RequireUppercase { get; init; }
    public bool RequireDigits { get; init; }
    public bool RequireSymbols { get; init; }
    public string EffectiveAllowedSymbols { get; init; } = "";
    public string ExcludedCharacters { get; init; } = "";
    public int RequiredCharacterCount { get; init; }
    public int EffectiveAlphabetSize { get; init; }
    public int ExactByteCount { get; init; }

    internal DefinitionSecretSpecification? PasswordSpecification { get; init; }
    internal string LowerPool { get; init; } = "";
    internal string UpperPool { get; init; } = "";
    internal string DigitPool { get; init; } = "";
    internal string SymbolPool { get; init; } = "";

    public static SecretGenerationBounds From(DefinitionSecretSpecification specification)
    {
        ArgumentNullException.ThrowIfNull(specification);
        if (!DefinitionSecretVocabulary.IsDefined(specification) ||
            specification.Acquisition != DefinitionSecretAcquisition.Local ||
            specification.Generation is null)
        {
            return new SecretGenerationBounds { Kind = specification.Kind, SupportsOptions = false };
        }

        if (specification.Kind == DefinitionSecretKind.Password)
            return FromPassword(specification.Kind, specification);

        if (specification.Kind is DefinitionSecretKind.Digest or DefinitionSecretKind.PasswordHash &&
            specification.Generation.Input == DefinitionSecretInput.Generated &&
            specification.Generation.Password is { Kind: DefinitionSecretKind.Password } password)
        {
            return FromPassword(specification.Kind, password);
        }

        if (specification.Kind is DefinitionSecretKind.OpaqueToken or DefinitionSecretKind.SymmetricKey)
        {
            int floor = specification.Algorithm switch
            {
                DefinitionSecretAlgorithm.Hs384 => 48,
                DefinitionSecretAlgorithm.Hs512 => 64,
                DefinitionSecretAlgorithm.Hs256 => 32,
                _ => AbsoluteMinBytes
            };
            int exact = specification.Algorithm switch
            {
                DefinitionSecretAlgorithm.Aes128 => 16,
                DefinitionSecretAlgorithm.Aes192 => 24,
                DefinitionSecretAlgorithm.Aes256 => 32,
                _ => 0
            };
            int minBytes = Math.Clamp(Math.Max(floor, specification.Constraints.MinBytes), AbsoluteMinBytes, AbsoluteMaxBytes);
            int maxBytes = Math.Min(AbsoluteMaxBytes, specification.Constraints.MaxBytes);
            if (exact != 0)
            {
                bool exactValid = exact >= minBytes && exact <= maxBytes;
                return new SecretGenerationBounds
                {
                    Kind = specification.Kind,
                    SupportsOptions = false,
                    IsByteTokenBased = true,
                    MinBytes = exact,
                    MaxBytes = exact,
                    DefaultBytes = exactValid ? exact : 0,
                    ExactByteCount = exact
                };
            }

            bool feasible = minBytes <= maxBytes;
            int defaultBytes = feasible
                ? Math.Clamp(specification.Generation.Bytes, minBytes, maxBytes)
                : minBytes;
            return new SecretGenerationBounds
            {
                Kind = specification.Kind,
                SupportsOptions = feasible,
                IsByteTokenBased = true,
                MinBytes = minBytes,
                MaxBytes = maxBytes,
                DefaultBytes = defaultBytes
            };
        }

        return new SecretGenerationBounds { Kind = specification.Kind, SupportsOptions = false };
    }

    /// <summary>Evaluates candidate options against both schema bounds and the 128-bit entropy floor.</summary>
    public SecretGenerationAssessment Evaluate(SecretGenerationOptions? options)
    {
        if (!SupportsOptions)
        {
            if (ExactByteCount != 0)
                return new(true, true, true, true, ExactByteCount * 8.0, 256, null);
            return new(false, false, false, false, 0, 0, "secret-generation-unavailable");
        }

        if (IsByteTokenBased)
        {
            int targetBytes = options?.Bytes ?? DefaultBytes;
            bool inBounds = targetBytes >= MinBytes && targetBytes <= MaxBytes;
            double bytesEntropyBits = targetBytes * 8.0;
            bool meetsByteEntropy = targetBytes >= AbsoluteMinBytes && bytesEntropyBits >= MinimumEntropyBits;
            string? byteError = !inBounds ? "secret-options-out-of-bounds"
                : !meetsByteEntropy ? "secret-generation-entropy-insufficient"
                : null;
            return new(byteError is null, inBounds, true, meetsByteEntropy, bytesEntropyBits, 256, byteError);
        }

        DefinitionSecretSpecification spec = PasswordSpecification!;
        DefinitionSecretConstraints policy = spec.Constraints;
        SecretGenerationOptions resolved = options ?? SecretGenerationOptions.FromBounds(this);

        if (resolved.AdditionalForbiddenCharacters.Length > MaxAdditionalForbiddenCharacters ||
            resolved.AdditionalForbiddenCharacters.Any(c => char.IsControl(c)))
            return new(false, false, false, false, 0, 0, "secret-options-out-of-bounds");

        bool useLower = resolved.IncludeLowercase ?? (LowerPool.Length > 0);
        bool useUpper = resolved.IncludeUppercase ?? (UpperPool.Length > 0);
        bool useDigits = resolved.IncludeDigits ?? (DigitPool.Length > 0);
        bool useSymbols = resolved.IncludeSymbols ?? (SymbolPool.Length > 0);

        if (!useLower && RequireLowercase ||
            !useUpper && RequireUppercase ||
            !useDigits && RequireDigits ||
            !useSymbols && RequireSymbols)
        {
            return new(false, true, false, false, 0, 0, "secret-generation-policy-invalid");
        }

        string extraExcluded = resolved.AdditionalForbiddenCharacters;
        string lower = useLower ? FilterExtra(LowerPool, extraExcluded) : "";
        string upper = useUpper ? FilterExtra(UpperPool, extraExcluded) : "";
        string digits = useDigits ? FilterExtra(DigitPool, extraExcluded) : "";
        string symbols = useSymbols ? FilterExtra(SymbolPool, extraExcluded) : "";

        bool categoryValid =
            (!RequireLowercase || lower.Length > 0) &&
            (!RequireUppercase || upper.Length > 0) &&
            (!RequireDigits || digits.Length > 0) &&
            (!RequireSymbols || symbols.Length > 0);
        if (!categoryValid)
            return new(false, true, false, false, 0, 0, "secret-generation-policy-invalid");

        int alphabetSize = lower.Length + upper.Length + digits.Length + symbols.Length;
        int targetLength = resolved.Length ?? DefaultLength;
        int schemaMinLength = Math.Max(AbsoluteMinPasswordLength, Math.Max(spec.MinLength, RequiredCharacterCount));
        int schemaMaxLength = Math.Min(spec.MaxLength ?? AbsoluteMaxPasswordLength,
            Math.Min(AbsoluteMaxPasswordLength, policy.MaxUtf8Bytes ?? AbsoluteMaxPasswordLength));
        bool inSchemaBounds = targetLength >= schemaMinLength && targetLength <= schemaMaxLength;

        int freePositions = Math.Max(0, targetLength - RequiredCharacterCount);
        double entropy = alphabetSize < 2 ? 0.0 : freePositions * Math.Log2(alphabetSize);
        bool meetsEntropy = alphabetSize >= 2 && targetLength >= MinLength && entropy >= MinimumEntropyBits;

        string? code = !inSchemaBounds ? "secret-options-out-of-bounds"
            : !meetsEntropy ? "secret-generation-entropy-insufficient"
            : null;
        return new(code is null, inSchemaBounds, categoryValid, meetsEntropy, entropy, alphabetSize, code);
    }

    private static SecretGenerationBounds FromPassword(DefinitionSecretKind kind, DefinitionSecretSpecification specification)
    {
        DefinitionSecretConstraints policy = specification.Constraints;
        if (!SecretValueValidator.IsPasswordPolicyValid(policy))
            return new SecretGenerationBounds { Kind = kind, SupportsOptions = false };

        string lower = FilterPool(LowercaseAlphabet, policy.ForbiddenCharacters, symbolsOnly: false);
        string upper = FilterPool(UppercaseAlphabet, policy.ForbiddenCharacters, symbolsOnly: false);
        string digits = FilterPool(DigitAlphabet, policy.ForbiddenCharacters, symbolsOnly: false);
        string symbols = FilterPool(policy.AllowedSymbols, policy.ForbiddenCharacters, symbolsOnly: true);

        int requiredCount = policy.MinLowercase + policy.MinUppercase + policy.MinDigits + policy.MinSymbols;
        int allAlphabetSize = lower.Length + upper.Length + digits.Length + symbols.Length;
        int maxLength = Math.Min(specification.MaxLength ?? AbsoluteMaxPasswordLength,
            Math.Min(AbsoluteMaxPasswordLength, policy.MaxUtf8Bytes ?? AbsoluteMaxPasswordLength));
        int schemaMinLength = Math.Max(AbsoluteMinPasswordLength, Math.Max(specification.MinLength, requiredCount));

        bool categoriesSatisfiable =
            (policy.MinLowercase == 0 || lower.Length > 0) &&
            (policy.MinUppercase == 0 || upper.Length > 0) &&
            (policy.MinDigits == 0 || digits.Length > 0) &&
            (policy.MinSymbols == 0 || symbols.Length > 0);

        if (!categoriesSatisfiable || allAlphabetSize < 2 || schemaMinLength > maxLength)
            return new SecretGenerationBounds { Kind = kind, SupportsOptions = false };

        int entropyFloorLength = schemaMinLength;
        double log2 = Math.Log2(allAlphabetSize);
        while (entropyFloorLength <= maxLength && (entropyFloorLength - requiredCount) * log2 < MinimumEntropyBits)
            entropyFloorLength++;

        bool feasible = entropyFloorLength <= maxLength;
        int defaultLength = feasible
            ? Math.Clamp(specification.Generation?.Length ?? 32, entropyFloorLength, maxLength)
            : entropyFloorLength;

        return new SecretGenerationBounds
        {
            Kind = kind,
            SupportsOptions = feasible,
            IsPasswordBased = true,
            MinLength = entropyFloorLength,
            MaxLength = maxLength,
            DefaultLength = defaultLength,
            AllowsLowercaseToggle = policy.MinLowercase == 0 && lower.Length > 0,
            AllowsUppercaseToggle = policy.MinUppercase == 0 && upper.Length > 0,
            AllowsDigitsToggle = policy.MinDigits == 0 && digits.Length > 0,
            AllowsSymbolsToggle = policy.MinSymbols == 0 && symbols.Length > 0,
            RequireLowercase = policy.MinLowercase > 0,
            RequireUppercase = policy.MinUppercase > 0,
            RequireDigits = policy.MinDigits > 0,
            RequireSymbols = policy.MinSymbols > 0,
            EffectiveAllowedSymbols = symbols,
            ExcludedCharacters = policy.ForbiddenCharacters,
            RequiredCharacterCount = requiredCount,
            EffectiveAlphabetSize = allAlphabetSize,
            PasswordSpecification = specification,
            LowerPool = lower,
            UpperPool = upper,
            DigitPool = digits,
            SymbolPool = symbols
        };
    }

    private static string FilterPool(string source, string forbidden, bool symbolsOnly)
    {
        var buffer = new List<char>(source.Length);
        for (int index = 0; index < source.Length; index++)
        {
            char c = source[index];
            if (c is < '!' or > '~') continue;
            if (symbolsOnly && char.IsAsciiLetterOrDigit(c)) continue;
            if (forbidden.Contains(c, StringComparison.Ordinal)) continue;
            if (!buffer.Contains(c)) buffer.Add(c);
        }
        return new string(buffer.ToArray());
    }

    private static string FilterExtra(string pool, string extraForbidden)
    {
        if (extraForbidden.Length == 0) return pool;
        var buffer = new List<char>(pool.Length);
        for (int index = 0; index < pool.Length; index++)
        {
            char c = pool[index];
            if (!extraForbidden.Contains(c, StringComparison.Ordinal)) buffer.Add(c);
        }
        return new string(buffer.ToArray());
    }
}

/// <summary>Result of validating <see cref="SecretGenerationOptions"/> against <see cref="SecretGenerationBounds"/>.</summary>
public sealed record SecretGenerationAssessment(
    bool IsValid,
    bool MeetsSchemaBounds,
    bool MeetsCategoryPolicy,
    bool MeetsEntropyFloor,
    double EntropyBits,
    int EffectiveAlphabetSize,
    string? ErrorCode);
