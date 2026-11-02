using System.Buffers.Text;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Configurator.Core.Definitions;

namespace Configurator.Core.Secrets;

/// <summary>Mutable operation output with explicit ownership and redacted diagnostics.</summary>
public sealed class SecretValue : IDisposable
{
    private char[]? _characters;
    public SecretValue(ReadOnlySpan<char> value) => _characters = value.ToArray();
    public string CopyValue() => new(_characters ?? throw new ObjectDisposedException(nameof(SecretValue)));
    internal ReadOnlySpan<char> Characters => _characters ?? throw new ObjectDisposedException(nameof(SecretValue));
    public byte[] CopyUtf8Bytes() => Encoding.UTF8.GetBytes(_characters ?? throw new ObjectDisposedException(nameof(SecretValue)));
    public void Dispose()
    {
        char[]? characters = Interlocked.Exchange(ref _characters, null);
        if (characters is not null) CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(characters.AsSpan()));
    }
    public override string ToString() => "SecretValue:Redacted";
}

public sealed class SecretGenerationResult : IDisposable
{
    public SecretGenerationResult(SecretValue? output, SecretValue? generatedPassword = null, string? errorCode = null)
    {
        Output = output;
        GeneratedPassword = generatedPassword;
        ErrorCode = errorCode;
    }
    public SecretValue? Output { get; }
    public SecretValue? GeneratedPassword { get; }
    public string? ErrorCode { get; }
    public bool Succeeded => Output is not null && ErrorCode is null;
    public void Dispose() { Output?.Dispose(); GeneratedPassword?.Dispose(); }
    public override string ToString() => $"SecretGenerationResult:Succeeded={Succeeded}";
}

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
        if (specification.Acquisition != "local" || specification.Generation is null)
            return Denied("secret-generation-unavailable");
        SecretValue? original = null;
        try
        {
            string value;
            switch (specification.Kind)
            {
                case "password": value = Password(specification); break;
                case "opaque-token":
                case "symmetric-key": value = RandomBytes(specification, entropy); break;
                case "private-key": value = SecretKeyMaterial.Generate(specification); break;
                case "password-hash":
                case "digest":
                    if (specification.Generation.Input == "generated")
                    {
                        if (!input.IsEmpty || specification.Generation.Password is not { Kind: "password" } password)
                            return Denied("secret-input-invalid");
                        using var generated = Generate(password, entropy: entropy);
                        if (!generated.Succeeded) return Denied(generated.ErrorCode!);
                        original = new SecretValue(generated.Output!.Characters);
                        input = original.Characters;
                    }
                    else if (specification.Generation.Input != "entered") return Denied("secret-input-invalid");
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
        int floor = specification.Algorithm switch { "HS384" => 48, "HS512" => 64, "HS256" => 32, _ => 16 };
        int exact = specification.Algorithm switch { "AES128" => 16, "AES192" => 24, "AES256" => 32, _ => 0 };
        if (length < floor || length > 4096 || length < specification.Constraints.MinBytes
            || length > specification.Constraints.MaxBytes || exact != 0 && length != exact)
            throw new ArgumentException("secret-generation-policy-invalid");
        byte[] bytes = new byte[length];
        try
        {
            FillRandom(bytes, entropy);
            return specification.Encoding switch
            {
                "base64" => Convert.ToBase64String(bytes),
                "base64url" => Base64Url.EncodeToString(bytes),
                "hex" => Convert.ToHexStringLower(bytes),
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
