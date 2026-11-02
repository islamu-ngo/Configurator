using System.Buffers.Binary;
using System.Buffers.Text;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Configurator.Core.Definitions;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;

namespace Configurator.Core.Secrets;

/// <summary>Explicit bounded derivation; validation inspects envelopes without executing a KDF.</summary>
public static class SecretDerivation
{
    private const string InvalidFormat = "secret-hash-format-invalid";
    private const string InvalidCost = "secret-cost-out-of-range";
    private const string InvalidInput = "secret-input-invalid";
    private const string BcryptAlphabet = "./ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public static string Derive(DefinitionSecretSpecification specification, ReadOnlySpan<char> input,
        RandomNumberGenerator? entropy = null)
    {
        ArgumentNullException.ThrowIfNull(specification);
        if (specification.Acquisition != "local" || specification.Generation is null)
            throw new ArgumentException("secret-generation-unavailable");
        string? policyFailure = ValidatePolicy(specification);
        if (policyFailure is not null) throw new ArgumentException(policyFailure);
        if (input.Length > 65536) throw new ArgumentException(InvalidInput);
        byte[] bytes = DecodeInput(specification, input);
        try
        {
            if (bytes.Length > 65536 || (specification.Algorithm == "bcrypt" &&
                (bytes.Length > 72 || input.Contains('\0')))) throw new ArgumentException(InvalidInput);
            return specification.Kind == "digest"
                ? DeriveDigest(specification, bytes)
                : DeriveHash(specification, bytes, entropy);
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    public static string? Validate(DefinitionSecretSpecification specification, string value)
    {
        ArgumentNullException.ThrowIfNull(specification);
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length > 4096) return InvalidFormat;
        string? failure = ValidatePolicy(specification);
        if (failure is not null) return failure;
        try
        {
            if (specification.Kind == "digest") return ValidateDigest(specification, value);
            return specification.Algorithm switch
            {
                "argon2id" => ValidateArgon(specification.Constraints, value),
                "bcrypt" => ValidateBcrypt(specification.Constraints, value),
                "aspnet-identity-v3" => ValidateIdentity(value),
                "pbkdf2-sha256" or "pbkdf2-sha512" => ValidatePbkdf2(specification, value),
                _ => "secret-profile-unavailable",
            };
        }
        catch (Exception exception) when (exception is JsonException or FormatException or InvalidOperationException or OverflowException)
        {
            return InvalidFormat;
        }
    }

    private static string? ValidatePolicy(DefinitionSecretSpecification specification)
    {
        DefinitionSecretConstraints policy = specification.Constraints;
        if (specification.Kind == "digest")
            return specification.Algorithm is "SHA256" or "SHA384" or "SHA512" &&
                specification.Encoding is "hex" or "base64" or "base64url" ? null : "secret-profile-unavailable";
        if (specification.Kind != "password-hash") return "secret-profile-unavailable";
        return specification.Algorithm switch
        {
            "argon2id" => ArgonCostAllowed(policy.MemoryKiB, policy.Iterations, policy.Parallelism) &&
                LengthsAllowed(policy.SaltBytes, policy.DerivedBytes) ? null : InvalidCost,
            "pbkdf2-sha256" or "pbkdf2-sha512" => PbkdfCostAllowed(specification.Algorithm, policy.Iterations) &&
                LengthsAllowed(policy.SaltBytes, policy.DerivedBytes) ? null : InvalidCost,
            "bcrypt" => policy.Cost is >= 10 and <= 14 ? null : InvalidCost,
            "aspnet-identity-v3" => null,
            _ => "secret-profile-unavailable",
        };
    }

    private static byte[] DecodeInput(DefinitionSecretSpecification specification, ReadOnlySpan<char> input)
    {
        string encoding = specification.Generation!.InputEncoding;
        if (specification.Kind == "password-hash" && encoding != "utf8") throw new ArgumentException(InvalidInput);
        if (encoding == "utf8")
        {
            int length = StrictUtf8.GetByteCount(input);
            if (length > 65536) throw new ArgumentException(InvalidInput);
            byte[] bytes = new byte[length];
            try { StrictUtf8.GetBytes(input, bytes); return bytes; }
            catch { CryptographicOperations.ZeroMemory(bytes); throw; }
        }
        byte[] decoded = encoding switch
        {
            "hex" => Convert.FromHexString(input),
            "base64url" => Base64Url.DecodeFromChars(input),
            "base64" => DecodeBase64(input),
            _ => throw new ArgumentException(InvalidInput),
        };
        if (Encode(decoded, encoding).AsSpan().SequenceEqual(input)) return decoded;
        CryptographicOperations.ZeroMemory(decoded);
        throw new ArgumentException(InvalidInput);
    }

    private static byte[] DecodeBase64(ReadOnlySpan<char> input)
    {
        byte[] buffer = new byte[input.Length / 4 * 3 + 3];
        try
        {
            if (!Convert.TryFromBase64Chars(input, buffer, out int count)) throw new ArgumentException(InvalidInput);
            return buffer.AsSpan(0, count).ToArray();
        }
        finally { CryptographicOperations.ZeroMemory(buffer); }
    }

    private static string DeriveDigest(DefinitionSecretSpecification specification, ReadOnlySpan<byte> input)
    {
        byte[] digest = specification.Algorithm switch
        {
            "SHA256" => SHA256.HashData(input),
            "SHA384" => SHA384.HashData(input),
            "SHA512" => SHA512.HashData(input),
            _ => throw new ArgumentException("secret-profile-unavailable"),
        };
        try { return Encode(digest, specification.Encoding!); }
        finally { CryptographicOperations.ZeroMemory(digest); }
    }

    private static string DeriveHash(DefinitionSecretSpecification specification, byte[] input, RandomNumberGenerator? entropy)
    {
        DefinitionSecretConstraints policy = specification.Constraints;
        bool fixedFormat = specification.Algorithm is "aspnet-identity-v3" or "bcrypt";
        byte[] salt = new byte[fixedFormat ? 16 : policy.SaltBytes];
        byte[] derived = new byte[fixedFormat ? 32 : policy.DerivedBytes];
        try
        {
            SecretGenerator.FillRandom(salt, entropy);
            switch (specification.Algorithm)
            {
                case "bcrypt":
                    return DeriveBcrypt(input, salt, policy.Cost);
                case "argon2id":
                    return DeriveArgon(policy, input, salt, derived);
                case "aspnet-identity-v3":
                    Rfc2898DeriveBytes.Pbkdf2(input, salt, derived, 210000, HashAlgorithmName.SHA512);
                    return EncodeIdentity(salt, derived);
                default:
                    Rfc2898DeriveBytes.Pbkdf2(input, salt, derived, policy.Iterations,
                        specification.Algorithm == "pbkdf2-sha256" ? HashAlgorithmName.SHA256 : HashAlgorithmName.SHA512);
                    return PbkdfEnvelope(specification.Algorithm!, policy.Iterations,
                        Convert.ToBase64String(salt), Convert.ToBase64String(derived));
            }
        }
        finally { CryptographicOperations.ZeroMemory(salt); CryptographicOperations.ZeroMemory(derived); }
    }

    private static string DeriveBcrypt(byte[] input, byte[] salt, int cost)
    {
        char[] characters = StrictUtf8.GetChars(input);
        try { return OpenBsdBCrypt.Generate("2b", characters, salt, cost); }
        finally { CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(characters.AsSpan())); }
    }

    private static string DeriveArgon(DefinitionSecretConstraints policy, byte[] input, byte[] salt, byte[] derived)
    {
        var builder = new Argon2Parameters.Builder(Argon2Parameters.Argon2id)
            .WithVersion(Argon2Parameters.Version13).WithMemoryAsKB(policy.MemoryKiB)
            .WithIterations(policy.Iterations).WithParallelism(policy.Parallelism).WithSalt(salt);
        Argon2Parameters parameters = builder.Build();
        try
        {
            var generator = new Argon2BytesGenerator();
            generator.Init(parameters);
            generator.GenerateBytes(input, derived);
            return FormattableString.Invariant($"$argon2id$v=19$m={policy.MemoryKiB},t={policy.Iterations},p={policy.Parallelism}${Convert.ToBase64String(salt).TrimEnd('=')}${Convert.ToBase64String(derived).TrimEnd('=')}");
        }
        finally { parameters.Clear(); builder.Clear(); }
    }

    private static string EncodeIdentity(ReadOnlySpan<byte> salt, ReadOnlySpan<byte> derived)
    {
        Span<byte> envelope = stackalloc byte[61];
        try
        {
            envelope[0] = 1;
            BinaryPrimitives.WriteUInt32BigEndian(envelope[1..], 2);
            BinaryPrimitives.WriteUInt32BigEndian(envelope[5..], 210000);
            BinaryPrimitives.WriteUInt32BigEndian(envelope[9..], 16);
            salt.CopyTo(envelope[13..29]); derived.CopyTo(envelope[29..]);
            return Convert.ToBase64String(envelope);
        }
        finally { CryptographicOperations.ZeroMemory(envelope); }
    }

    private static string? ValidateArgon(DefinitionSecretConstraints policy, string value)
    {
        string[] pieces = value.Split('$');
        if (pieces.Length != 6 || pieces[0].Length != 0 || pieces[1] != "argon2id" || pieces[2] != "v=19") return InvalidFormat;
        string[] costs = pieces[3].Split(',');
        if (costs.Length != 3 || !ReadParameter(costs[0], "m=", out int memory) ||
            !ReadParameter(costs[1], "t=", out int iterations) || !ReadParameter(costs[2], "p=", out int parallelism)) return InvalidFormat;
        if (!ArgonCostAllowed(memory, iterations, parallelism) || memory != policy.MemoryKiB ||
            iterations != policy.Iterations || parallelism != policy.Parallelism) return InvalidCost;
        return CanonicalBase64(pieces[4], policy.SaltBytes, unpadded: true) &&
            CanonicalBase64(pieces[5], policy.DerivedBytes, unpadded: true) ? null : InvalidFormat;
    }

    private static string? ValidateBcrypt(DefinitionSecretConstraints policy, string value)
    {
        if (value.Length != 60 || !value.StartsWith("$2b$", StringComparison.Ordinal) || value[6] != '$' ||
            !int.TryParse(value.AsSpan(4, 2), NumberStyles.None, CultureInfo.InvariantCulture, out int cost)) return InvalidFormat;
        if (cost is < 10 or > 14 || cost != policy.Cost) return InvalidCost;
        foreach (char character in value.AsSpan(7)) if (!BcryptAlphabet.Contains(character)) return InvalidFormat;
        // Radix64 encodes 16 salt bytes and 23 hash bytes; unused low bits must be zero.
        return BcryptAlphabet.IndexOf(value[28]) % 16 == 0 && BcryptAlphabet.IndexOf(value[59]) % 4 == 0 ? null : InvalidFormat;
    }

    private static string? ValidateIdentity(string value)
    {
        Span<byte> envelope = stackalloc byte[61];
        try
        {
            if (value.Length != 84 || !Convert.TryFromBase64String(value, envelope, out int written) || written != 61 ||
                Convert.ToBase64String(envelope) != value || envelope[0] != 1) return InvalidFormat;
            return BinaryPrimitives.ReadUInt32BigEndian(envelope[1..]) == 2 &&
                BinaryPrimitives.ReadUInt32BigEndian(envelope[5..]) == 210000 &&
                BinaryPrimitives.ReadUInt32BigEndian(envelope[9..]) == 16 ? null : InvalidCost;
        }
        finally { CryptographicOperations.ZeroMemory(envelope); }
    }

    private static string? ValidatePbkdf2(DefinitionSecretSpecification specification, string value)
    {
        using JsonDocument document = JsonDocument.Parse(value, new JsonDocumentOptions { MaxDepth = 2 });
        JsonElement root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Count() != 5) return InvalidFormat;
        string[] expected = ["version", "algorithm", "iterations", "salt", "derivedKey"];
        int index = 0;
        foreach (JsonProperty property in root.EnumerateObject()) if (property.Name != expected[index++]) return InvalidFormat;
        if (!root.GetProperty("version").TryGetInt32(out int version) || version != 1 ||
            root.GetProperty("algorithm").GetString() != specification.Algorithm ||
            !root.GetProperty("iterations").TryGetInt32(out int iterations)) return InvalidFormat;
        if (!PbkdfCostAllowed(specification.Algorithm!, iterations) || iterations != specification.Constraints.Iterations) return InvalidCost;
        string? salt = root.GetProperty("salt").GetString();
        string? derived = root.GetProperty("derivedKey").GetString();
        if (salt is null || derived is null || !CanonicalBase64(salt, specification.Constraints.SaltBytes) ||
            !CanonicalBase64(derived, specification.Constraints.DerivedBytes)) return InvalidFormat;
        return value == PbkdfEnvelope(specification.Algorithm!, iterations, salt, derived) ? null : InvalidFormat;
    }

    private static string? ValidateDigest(DefinitionSecretSpecification specification, string value)
    {
        int size = specification.Algorithm switch { "SHA256" => 32, "SHA384" => 48, _ => 64 };
        if (specification.Encoding == "base64") return CanonicalBase64(value, size) ? null : "secret-encoding-invalid";
        Span<byte> decoded = stackalloc byte[64];
        try
        {
            if (specification.Encoding == "hex")
                return value.Length == size * 2 && value.All(character => char.IsAsciiDigit(character) || character is >= 'a' and <= 'f')
                    ? null : "secret-encoding-invalid";
            if (!Base64Url.TryDecodeFromChars(value, decoded, out int written) || written != size ||
                Base64Url.EncodeToString(decoded[..written]) != value) return "secret-encoding-invalid";
            return null;
        }
        finally { CryptographicOperations.ZeroMemory(decoded); }
    }

    private static bool CanonicalBase64(string value, int bytes, bool unpadded = false)
    {
        if (bytes is < 16 or > 64 || value.Length != (unpadded ? (bytes * 8 + 5) / 6 : (bytes + 2) / 3 * 4)) return false;
        Span<byte> decoded = stackalloc byte[64];
        try
        {
            string padded = unpadded ? value.PadRight((value.Length + 3) / 4 * 4, '=') : value;
            if (!Convert.TryFromBase64String(padded, decoded, out int written) || written != bytes) return false;
            string canonical = Convert.ToBase64String(decoded[..written]);
            return (unpadded ? canonical.TrimEnd('=') : canonical) == value;
        }
        finally { CryptographicOperations.ZeroMemory(decoded); }
    }

    private static bool ReadParameter(string value, string prefix, out int number)
    {
        number = 0;
        return value.StartsWith(prefix, StringComparison.Ordinal) && int.TryParse(value.AsSpan(prefix.Length),
            NumberStyles.None, CultureInfo.InvariantCulture, out number) && value.AsSpan(prefix.Length).SequenceEqual(number.ToString(CultureInfo.InvariantCulture));
    }

    private static bool ArgonCostAllowed(int memory, int iterations, int parallelism) =>
        memory is >= 19456 and <= 65536 && iterations is >= 2 and <= 6 && parallelism is >= 1 and <= 4;
    private static bool LengthsAllowed(int salt, int derived) => salt is >= 16 and <= 64 && derived is >= 16 and <= 64;
    private static bool PbkdfCostAllowed(string algorithm, int iterations) =>
        iterations >= (algorithm == "pbkdf2-sha256" ? 600000 : 210000) && iterations <= 2000000;
    private static string PbkdfEnvelope(string algorithm, int iterations, string salt, string derived) =>
        FormattableString.Invariant($"{{\"version\":1,\"algorithm\":\"{algorithm}\",\"iterations\":{iterations},\"salt\":\"{salt}\",\"derivedKey\":\"{derived}\"}}");
    private static string Encode(ReadOnlySpan<byte> bytes, string encoding) => encoding switch
    {
        "hex" => Convert.ToHexStringLower(bytes),
        "base64" => Convert.ToBase64String(bytes),
        "base64url" => Base64Url.EncodeToString(bytes),
        _ => throw new ArgumentException(InvalidInput),
    };
}
