namespace Configurator.Core.Secrets;

using System.Buffers;
using System.Buffers.Text;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Configurator.Core.Definitions;

/// <summary>Checks local policy and serialized formats without generating material or verifying passwords.</summary>
public static class SecretValueValidator
{
    public static IReadOnlyList<string> Validate(DefinitionSecretSpecification specification, string value)
    {
        ArgumentNullException.ThrowIfNull(specification);
        ArgumentNullException.ThrowIfNull(value);
        if (specification.Kind == "password" && !IsPasswordPolicyValid(specification.Constraints))
            return ["secret-policy-invalid"];
        if (specification.Kind == "password-hash" && value.Length > 4096)
            return ["secret-hash-format-invalid"];
        if (specification.Kind == "private-key" && value.Length > 65536)
            return ["secret-key-format-invalid"];
        var failures = new List<string>();
        int scalarLength = value.EnumerateRunes().Count();
        if (scalarLength < specification.MinLength) failures.Add("secret-too-short");
        if (specification.MaxLength is int maximum && scalarLength > maximum) failures.Add("secret-too-long");
        switch (specification.Kind)
        {
            case "password": Password(specification.Constraints, value, failures); break;
            case "opaque-token":
            case "symmetric-key":
            case "digest": Encoded(specification, value, failures); break;
            case "password-hash":
                Add(SecretDerivation.Validate(specification, value));
                break;
            case "private-key":
                Add(SecretKeyMaterial.Validate(specification, value));
                break;
            case "connection-string": break;
            default: failures.Add("secret-profile-unavailable"); break;
        }
        return failures.AsReadOnly();

        void Add(string? code)
        {
            if (code is not null) failures.Add(code);
        }
    }

    internal static bool IsPasswordPolicyValid(DefinitionSecretConstraints policy) =>
        policy.CharacterSet is "unicode" or "ascii-printable"
        && policy.MaxUtf8Bytes is null or >= 1 and <= 65536
        && policy.MinLowercase is >= 0 and <= 1024 && policy.MinUppercase is >= 0 and <= 1024
        && policy.MinDigits is >= 0 and <= 1024 && policy.MinSymbols is >= 0 and <= 1024
        && policy.AllowedSymbols is { Length: <= 94 } && policy.ForbiddenCharacters is { Length: <= 1024 }
        && policy.AllowedSymbols.Distinct().Count() == policy.AllowedSymbols.Length
        && policy.AllowedSymbols.All(c => c is >= '!' and <= '~' && !char.IsAsciiLetterOrDigit(c));

    private static void Password(DefinitionSecretConstraints policy, string value, List<string> failures)
    {
        if (policy.MaxUtf8Bytes is int maximum && Encoding.UTF8.GetByteCount(value) > maximum)
            failures.Add("secret-utf8-limit");
        if (policy.CharacterSet == "ascii-printable" && value.Any(character => character is < ' ' or > '~'))
            failures.Add("secret-character-set-invalid");
        if (!policy.AllowWhitespace && value.EnumerateRunes().Any(Rune.IsWhiteSpace))
            failures.Add("secret-whitespace-forbidden");
        if (value.EnumerateRunes().Any(rune => policy.ForbiddenCharacters.EnumerateRunes().Contains(rune)))
            failures.Add("secret-character-forbidden");
        if (value.Count(char.IsAsciiLetterLower) < policy.MinLowercase) failures.Add("secret-lowercase-required");
        if (value.Count(char.IsAsciiLetterUpper) < policy.MinUppercase) failures.Add("secret-uppercase-required");
        if (value.Count(char.IsAsciiDigit) < policy.MinDigits) failures.Add("secret-digit-required");
        if (value.EnumerateRunes().Count(rune => policy.AllowedSymbols.EnumerateRunes().Contains(rune)) < policy.MinSymbols)
            failures.Add("secret-symbol-required");
    }

    private static void Encoded(DefinitionSecretSpecification specification, string value, List<string> failures)
    {
        if (value.Length > 8192)
        {
            failures.Add("secret-encoding-invalid");
            return;
        }
        Span<byte> bytes = stackalloc byte[4096];
        Span<char> canonical = stackalloc char[8192];
        try
        {
            int length;
            int characters;
            bool valid;
            switch (specification.Encoding)
            {
                case "base64":
                    valid = Convert.TryFromBase64Chars(value, bytes, out length)
                        && Convert.TryToBase64Chars(bytes[..length], canonical, out characters)
                        && value.AsSpan().SequenceEqual(canonical[..characters]);
                    break;
                case "base64url":
                    valid = Base64Url.DecodeFromChars(value, bytes, out int consumed, out length) == OperationStatus.Done
                        && consumed == value.Length
                        && Base64Url.TryEncodeToChars(bytes[..length], canonical, out characters)
                        && value.AsSpan().SequenceEqual(canonical[..characters]);
                    break;
                case "hex":
                    length = value.Length / 2;
                    valid = value.Length % 2 == 0 && value.All(character => char.IsAsciiDigit(character) || character is >= 'a' and <= 'f');
                    break;
                default:
                    failures.Add("secret-profile-unavailable");
                    return;
            }
            if (!valid) { failures.Add("secret-encoding-invalid"); return; }
            if (length < specification.Constraints.MinBytes || length > specification.Constraints.MaxBytes)
                failures.Add("secret-byte-length-invalid");
            int required = specification.Algorithm switch
            {
                "HS256" or "AES256" or "SHA256" => 32,
                "HS384" or "SHA384" => 48,
                "HS512" or "SHA512" => 64,
                "AES128" => 16,
                "AES192" => 24,
                _ => 0
            };
            if (specification.Kind is "symmetric-key" or "digest")
            {
                if (required == 0) failures.Add("secret-profile-unavailable");
                else if (specification.Algorithm!.StartsWith("HS", StringComparison.Ordinal)
                    ? length < required : length != required)
                    failures.Add("secret-key-size-invalid");
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
            CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(canonical));
        }
    }
}
