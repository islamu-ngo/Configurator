using System.Buffers.Text;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Configurator.Core.Definitions;

namespace Configurator.Core.Secrets;

public static class SecretGenerator
{
    internal static void FillRandom(Span<byte> destination, RandomNumberGenerator? entropy = null)
    {
        if (entropy is null) RandomNumberGenerator.Fill(destination);
        else entropy.GetBytes(destination);
    }
    public static SecretGenerationResult Generate(DefinitionSecretSpecification specification, ReadOnlySpan<char> input = default,
        RandomNumberGenerator? entropy = null)
    {
        ArgumentNullException.ThrowIfNull(specification);
        if (!DefinitionSecretVocabulary.IsDefined(specification))
            return Denied("secret-profile-unavailable");
        if (specification.Acquisition != DefinitionSecretAcquisition.Local || specification.Generation is null)
            return Denied("secret-generation-unavailable");
        SecretValue? original = null;
        try
        {
            string value;
            switch (specification.Kind)
            {
                case DefinitionSecretKind.Password: value = Password(specification); break;
                case DefinitionSecretKind.OpaqueToken:
                case DefinitionSecretKind.SymmetricKey: value = RandomBytes(specification, entropy); break;
                case DefinitionSecretKind.PrivateKey: value = SecretKeyMaterial.Generate(specification); break;
                case DefinitionSecretKind.PasswordHash:
                case DefinitionSecretKind.Digest:
                    if (specification.Generation.Input == DefinitionSecretInput.Generated)
                    {
                        if (!input.IsEmpty || specification.Generation.Password is not { Kind: DefinitionSecretKind.Password } password)
                            return Denied("secret-input-invalid");
                        using var generated = Generate(password, entropy: entropy);
                        if (!generated.Succeeded) return Denied(generated.ErrorCode!);
                        original = new SecretValue(generated.Output!.Characters);
                        input = original.Characters;
                    }
                    else if (specification.Generation.Input != DefinitionSecretInput.Entered) return Denied("secret-input-invalid");
                    value = SecretDerivation.Derive(specification, input, entropy);
                    break;
                default: return Denied("secret-generation-unavailable");
            }
            IReadOnlyList<string> failures = SecretValueValidator.Validate(specification, value);
            if (failures.Count != 0) return Denied(failures[0]);
            var result = new SecretGenerationResult(new SecretValue(value), original);
            original = null;
            return result;
        }
        catch (ArgumentException) { return Denied("secret-input-invalid"); }
        catch (FormatException) { return Denied("secret-input-invalid"); }
        catch (CryptographicException) { return Denied("secret-generation-failed"); }
        catch (NotSupportedException) { return Denied("secret-profile-unavailable"); }
        finally { original?.Dispose(); }
    }

    private static string RandomBytes(DefinitionSecretSpecification specification, RandomNumberGenerator? entropy)
    {
        int length = specification.Generation!.Bytes;
        int floor = specification.Algorithm switch { DefinitionSecretAlgorithm.Hs384 => 48, DefinitionSecretAlgorithm.Hs512 => 64, DefinitionSecretAlgorithm.Hs256 => 32, _ => 16 };
        int exact = specification.Algorithm switch { DefinitionSecretAlgorithm.Aes128 => 16, DefinitionSecretAlgorithm.Aes192 => 24, DefinitionSecretAlgorithm.Aes256 => 32, _ => 0 };
        if (length < floor || length > 4096 || length < specification.Constraints.MinBytes
            || length > specification.Constraints.MaxBytes || exact != 0 && length != exact)
            throw new ArgumentException("secret-generation-policy-invalid");
        byte[] bytes = new byte[length];
        try
        {
            FillRandom(bytes, entropy);
            return specification.Encoding switch
            {
                DefinitionSecretEncoding.Base64 => Convert.ToBase64String(bytes),
                DefinitionSecretEncoding.Base64Url => Base64Url.EncodeToString(bytes),
                DefinitionSecretEncoding.Hex => Convert.ToHexStringLower(bytes),
                _ => throw new ArgumentException("secret-encoding-invalid")
            };
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    private static string Password(DefinitionSecretSpecification specification)
    {
        var policy = specification.Constraints;
        if (!SecretValueValidator.IsPasswordPolicyValid(policy))
            throw new ArgumentException("secret-generation-policy-invalid");
        int length = specification.Generation!.Length;
        int[] minima = [policy.MinLowercase, policy.MinUppercase, policy.MinDigits, policy.MinSymbols];
        long required = minima.Sum(value => (long)value);
        if (length is < 1 or > 1024 || length < specification.MinLength || length > specification.MaxLength
            || length > policy.MaxUtf8Bytes || minima.Any(value => value is < 0 or > 1024) || required > length)
            throw new ArgumentException("secret-generation-policy-invalid");
        string[] categories = ["abcdefghijklmnopqrstuvwxyz", "ABCDEFGHIJKLMNOPQRSTUVWXYZ", "0123456789", policy.AllowedSymbols];
        for (int i = 0; i < categories.Length; i++)
        {
            categories[i] = new string(categories[i].Where(c => c is >= '!' and <= '~'
                && !policy.ForbiddenCharacters.Contains(c) && (i != 3 || !char.IsAsciiLetterOrDigit(c))).Distinct().ToArray());
            if (minima[i] != 0 && categories[i].Length == 0) throw new ArgumentException("secret-generation-policy-invalid");
        }
        string alphabet = string.Concat(categories);
        // Only free draws count toward the bound; category draws and shuffle add no credited entropy.
        if (alphabet.Length < 2 || (length - required) * Math.Log2(alphabet.Length) < 128)
            throw new ArgumentException("secret-generation-entropy-insufficient");
        char[] characters = new char[length];
        try
        {
            int cursor = 0;
            for (int category = 0; category < categories.Length; category++)
                for (int count = 0; count < minima[category]; count++)
                    characters[cursor++] = categories[category][RandomNumberGenerator.GetInt32(categories[category].Length)];
            while (cursor < length) characters[cursor++] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
            for (int index = length - 1; index > 0; index--)
            {
                int other = RandomNumberGenerator.GetInt32(index + 1);
                (characters[index], characters[other]) = (characters[other], characters[index]);
            }
            return new string(characters);
        }
        finally { CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(characters.AsSpan())); }
    }

    private static SecretGenerationResult Denied(string code) => new(null, errorCode: code);
}
