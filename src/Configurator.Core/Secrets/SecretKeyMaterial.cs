using System.Buffers;
using System.Buffers.Text;
using System.Formats.Asn1;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Configurator.Core.Definitions;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.Sec;
using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Prng;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Utilities;

namespace Configurator.Core.Secrets;

/// <summary>Portable private-key encoding with bounded parsing and local mathematical checks.</summary>
public static class SecretKeyMaterial
{
    private const string InvalidFormat = "secret-key-format-invalid";
    private const string RsaOid = "1.2.840.113549.1.1.1";
    private const string EcOid = "1.2.840.10045.2.1";

    public static string Generate(DefinitionSecretSpecification specification)
    {
        ArgumentNullException.ThrowIfNull(specification);
        if (!ValidPolicy(specification) || specification.Acquisition != "local" || specification.Generation is null)
            throw new CryptographicException("secret-generation-unavailable");

        var random = new SecureRandom(new PlatformRandom());
        AsymmetricCipherKeyPair pair;
        if (IsRsa(specification))
        {
            var generator = new RsaKeyPairGenerator();
            generator.Init(new RsaKeyGenerationParameters(BigInteger.ValueOf(65537), random, specification.Constraints.KeySize, 100));
            pair = generator.GenerateKeyPair();
        }
        else
        {
            var generator = new ECKeyPairGenerator();
            generator.Init(new ECKeyGenerationParameters(CurveOid(specification), random));
            pair = generator.GenerateKeyPair();
        }

        if (specification.Encoding == "jwk") return WriteJwk(specification, pair);
        byte[] encoded = PrivateKeyInfoFactory.CreatePrivateKeyInfo(pair.Private).GetEncoded();
        try { return PemEncoding.WriteString("PRIVATE KEY", encoded); }
        finally { CryptographicOperations.ZeroMemory(encoded); }
    }

    public static string? Validate(DefinitionSecretSpecification specification, string value)
    {
        ArgumentNullException.ThrowIfNull(specification);
        ArgumentNullException.ThrowIfNull(value);
        if (!ValidPolicy(specification)) return "secret-key-policy-invalid";
        if (value.Length > 65536 || Encoding.UTF8.GetByteCount(value) > 65536) return InvalidFormat;
        try
        {
            bool valid = specification.Encoding == "jwk"
                ? ValidateJwk(specification, value)
                : ValidatePem(specification, value);
            return valid ? null : InvalidFormat;
        }
        catch (Exception exception) when (exception is ArgumentException or ArithmeticException or
            InvalidOperationException or JsonException or AsnContentException or KeyNotFoundException or
            IOException or CryptoException or FormatException)
        {
            return InvalidFormat;
        }
    }

    private static bool ValidPolicy(DefinitionSecretSpecification specification)
    {
        if (specification.Kind != "private-key" || specification.Encoding is not ("pkcs8-pem" or "jwk")) return false;
        if (IsRsa(specification)) return specification.Constraints.KeySize is 2048 or 3072 or 4096 && specification.Constraints.Curve is null;
        return specification.Algorithm is "ES256" or "ES384" or "ES512" &&
            (specification.Constraints.Curve is null || specification.Constraints.Curve == CurveName(specification));
    }

    private static bool IsRsa(DefinitionSecretSpecification specification) => specification.Algorithm is "RS256" or "PS256";
    private static string CurveName(DefinitionSecretSpecification specification) => specification.Algorithm switch
    {
        "ES256" => "P-256",
        "ES384" => "P-384",
        "ES512" => "P-521",
        _ => throw new ArgumentException("secret-key-policy-invalid"),
    };
    private static DerObjectIdentifier CurveOid(DefinitionSecretSpecification specification) => specification.Algorithm switch
    {
        "ES256" => SecObjectIdentifiers.SecP256r1,
        "ES384" => SecObjectIdentifiers.SecP384r1,
        "ES512" => SecObjectIdentifiers.SecP521r1,
        _ => throw new ArgumentException("secret-key-policy-invalid"),
    };
    private static int CoordinateBytes(DefinitionSecretSpecification specification) => specification.Algorithm switch
    {
        "ES256" => 32,
        "ES384" => 48,
        "ES512" => 66,
        _ => throw new ArgumentException("secret-key-policy-invalid"),
    };

    private static string WriteJwk(DefinitionSecretSpecification specification, AsymmetricCipherKeyPair pair)
    {
        var buffer = new ArrayBufferWriter<byte>(8192);
        try
        {
            using (var writer = new Utf8JsonWriter(buffer))
            {
                writer.WriteStartObject();
                writer.WriteString("kty", IsRsa(specification) ? "RSA" : "EC");
                writer.WriteString("alg", specification.Algorithm);
                if (pair.Private is RsaPrivateCrtKeyParameters rsa)
                {
                    WriteInteger(writer, "n", rsa.Modulus);
                    WriteInteger(writer, "e", rsa.PublicExponent);
                    WriteInteger(writer, "d", rsa.Exponent);
                    WriteInteger(writer, "p", rsa.P);
                    WriteInteger(writer, "q", rsa.Q);
                    WriteInteger(writer, "dp", rsa.DP);
                    WriteInteger(writer, "dq", rsa.DQ);
                    WriteInteger(writer, "qi", rsa.QInv);
                }
                else
                {
                    var key = (ECPrivateKeyParameters)pair.Private;
                    var point = ((ECPublicKeyParameters)pair.Public).Q.Normalize();
                    int width = CoordinateBytes(specification);
                    writer.WriteString("crv", CurveName(specification));
                    WriteInteger(writer, "x", point.AffineXCoord.ToBigInteger(), width);
                    WriteInteger(writer, "y", point.AffineYCoord.ToBigInteger(), width);
                    WriteInteger(writer, "d", key.D, width);
                }
                writer.WriteEndObject();
            }
            return Encoding.UTF8.GetString(buffer.WrittenSpan);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(MemoryMarshal.AsMemory(buffer.WrittenMemory).Span);
            buffer.Clear();
        }
    }

    private static void WriteInteger(Utf8JsonWriter writer, string name, BigInteger integer, int width = 0)
    {
        byte[] bytes = width == 0 ? BigIntegers.AsUnsignedByteArray(integer) : BigIntegers.AsUnsignedByteArray(width, integer);
        try { writer.WriteString(name, Base64Url.EncodeToString(bytes)); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    private static bool ValidateJwk(DefinitionSecretSpecification specification, string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        try
        {
            using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 4 });
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return false;
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonProperty property in root.EnumerateObject())
            {
                if (!names.Add(property.Name)) return false;
                if (property.Name is not ("kty" or "alg" or "kid" or "use" or "key_ops" or "crv" or "x" or "y" or "d" or "n" or "e" or "p" or "q" or "dp" or "dq" or "qi")) return false;
            }
            if (root.TryGetProperty("alg", out JsonElement algorithm) && algorithm.GetString() != specification.Algorithm) return false;
            if (IsRsa(specification))
            {
                if (root.GetProperty("kty").GetString() != "RSA" || names.Overlaps(["crv", "x", "y"])) return false;
                return ValidateRsa(specification, ReadInteger(root, "n"), ReadInteger(root, "e"), ReadInteger(root, "d"),
                    ReadInteger(root, "p"), ReadInteger(root, "q"), ReadInteger(root, "dp"), ReadInteger(root, "dq"), ReadInteger(root, "qi"));
            }
            if (root.GetProperty("kty").GetString() != "EC" || root.GetProperty("crv").GetString() != CurveName(specification) ||
                names.Overlaps(["n", "e", "p", "q", "dp", "dq", "qi"])) return false;
            int width = CoordinateBytes(specification);
            return ValidateEc(specification, ReadInteger(root, "d", width), ReadInteger(root, "x", width), ReadInteger(root, "y", width));
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    private static BigInteger ReadInteger(JsonElement root, string name, int width = 0)
    {
        string encoded = root.GetProperty(name).GetString() ?? throw new FormatException();
        if (encoded.Length is < 1 or > 683 || (width > 0 && encoded.Length != (width * 8 + 5) / 6)) throw new FormatException();
        byte[] bytes = Base64Url.DecodeFromChars(encoded);
        try
        {
            if (Base64Url.EncodeToString(bytes) != encoded || (width > 0 ? bytes.Length != width : bytes.Length > 512 || bytes[0] == 0))
                throw new FormatException();
            return new BigInteger(1, bytes);
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    private static bool ValidatePem(DefinitionSecretSpecification specification, string value)
    {
        ReadOnlySpan<char> text = value.AsSpan().Trim();
        if (!PemEncoding.TryFind(text, out PemFields fields) || !text[fields.Label].SequenceEqual("PRIVATE KEY") ||
            fields.Location.GetOffsetAndLength(text.Length) != (0, text.Length) || fields.DecodedDataLength > 8192) return false;
        byte[] der = new byte[fields.DecodedDataLength];
        byte[]? privateBytes = null;
        try
        {
            if (!Convert.TryFromBase64Chars(text[fields.Base64Data], der, out int written) || written != der.Length) return false;
            var reader = new AsnReader(der, AsnEncodingRules.DER);
            AsnReader envelope = reader.ReadSequence();
            reader.ThrowIfNotEmpty();
            if (envelope.ReadInteger() != 0) return false;
            AsnReader algorithm = envelope.ReadSequence();
            string oid = algorithm.ReadObjectIdentifier();
            if (IsRsa(specification))
            {
                if (oid != RsaOid) return false;
                if (algorithm.HasData) algorithm.ReadNull();
            }
            else if (oid != EcOid || algorithm.ReadObjectIdentifier() != CurveOid(specification).Id) return false;
            algorithm.ThrowIfNotEmpty();
            privateBytes = envelope.ReadOctetString();
            envelope.ThrowIfNotEmpty();
            var keyReader = new AsnReader(privateBytes, AsnEncodingRules.DER);
            AsnReader key = keyReader.ReadSequence();
            keyReader.ThrowIfNotEmpty();
            if (IsRsa(specification))
            {
                if (key.ReadInteger() != 0) return false;
                BigInteger n = ReadDerInteger(key), e = ReadDerInteger(key), d = ReadDerInteger(key);
                BigInteger p = ReadDerInteger(key), q = ReadDerInteger(key), dp = ReadDerInteger(key), dq = ReadDerInteger(key), qi = ReadDerInteger(key);
                key.ThrowIfNotEmpty();
                return ValidateRsa(specification, n, e, d, p, q, dp, dq, qi);
            }
            if (key.ReadInteger() != 1) return false;
            byte[] scalar = key.ReadOctetString();
            try
            {
                if (scalar.Length != CoordinateBytes(specification)) return false;
                var d = new BigInteger(1, scalar);
                var curveTag = new Asn1Tag(TagClass.ContextSpecific, 0, true);
                if (key.HasData && key.PeekTag().HasSameClassAndValue(curveTag))
                {
                    AsnReader curve = key.ReadSequence(curveTag);
                    if (curve.ReadObjectIdentifier() != CurveOid(specification).Id) return false;
                    curve.ThrowIfNotEmpty();
                }
                if (!key.HasData) return ValidateEc(specification, d, null, null);
                AsnReader publicKey = key.ReadSequence(new Asn1Tag(TagClass.ContextSpecific, 1, true));
                byte[] pointBytes = publicKey.ReadBitString(out int unusedBits);
                try
                {
                    publicKey.ThrowIfNotEmpty();
                    key.ThrowIfNotEmpty();
                    int width = CoordinateBytes(specification);
                    if (unusedBits != 0 || pointBytes.Length != 1 + 2 * width || pointBytes[0] != 4) return false;
                    return ValidateEc(specification, d, FromBytes(pointBytes.AsSpan(1, width)), FromBytes(pointBytes.AsSpan(1 + width, width)));
                }
                finally { CryptographicOperations.ZeroMemory(pointBytes); }
            }
            finally { CryptographicOperations.ZeroMemory(scalar); }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(der);
            if (privateBytes is not null) CryptographicOperations.ZeroMemory(privateBytes);
        }
    }

    private static BigInteger ReadDerInteger(AsnReader reader)
    {
        ReadOnlySpan<byte> bytes = reader.ReadIntegerBytes().Span;
        if (bytes.Length is < 1 or > 513 || (bytes[0] & 128) != 0) throw new FormatException();
        if (bytes[0] == 0) bytes = bytes[1..];
        if (bytes.Length > 512) throw new FormatException();
        return FromBytes(bytes);
    }

    private static BigInteger FromBytes(ReadOnlySpan<byte> bytes)
    {
        byte[] copy = bytes.ToArray();
        try { return new BigInteger(1, copy); }
        finally { CryptographicOperations.ZeroMemory(copy); }
    }

    private static bool ValidateRsa(DefinitionSecretSpecification specification, BigInteger n, BigInteger e, BigInteger d,
        BigInteger p, BigInteger q, BigInteger dp, BigInteger dq, BigInteger qi)
    {
        if (n.BitLength != specification.Constraints.KeySize || e.CompareTo(BigInteger.ValueOf(65537)) < 0 || !e.TestBit(0) ||
            e.CompareTo(n) >= 0 || d.SignValue <= 0 || d.CompareTo(n) >= 0 || p.Equals(q) ||
            p.CompareTo(BigInteger.Three) < 0 || q.CompareTo(BigInteger.Three) < 0 ||
            p.CompareTo(n) >= 0 || q.CompareTo(n) >= 0 || !p.TestBit(0) || !q.TestBit(0)) return false;
        if (!p.Multiply(q).Equals(n)) return false;
        BigInteger pMinusOne = p.Subtract(BigInteger.One), qMinusOne = q.Subtract(BigInteger.One);
        BigInteger order = pMinusOne.Divide(pMinusOne.Gcd(qMinusOne)).Multiply(qMinusOne);
        return d.Multiply(e).Mod(order).Equals(BigInteger.One) && dp.Equals(d.Mod(pMinusOne)) &&
            dq.Equals(d.Mod(qMinusOne)) && qi.SignValue > 0 && qi.CompareTo(p) < 0 && q.Multiply(qi).Mod(p).Equals(BigInteger.One);
    }

    private static bool ValidateEc(DefinitionSecretSpecification specification, BigInteger d, BigInteger? x, BigInteger? y)
    {
        X9ECParameters parameters = ECNamedCurveTable.GetByOid(CurveOid(specification));
        if (d.SignValue <= 0 || d.CompareTo(parameters.N) >= 0) return false;
        if (x is null || y is null) return true;
        var supplied = parameters.Curve.ValidatePoint(x, y).Normalize();
        return !supplied.IsInfinity && supplied.Equals(parameters.G.Multiply(d).Normalize());
    }

    private sealed class PlatformRandom : IRandomGenerator
    {
        // Entropy comes freshly from the platform; caller seed material is never authoritative.
        public void AddSeedMaterial(byte[] seed) { }
        public void AddSeedMaterial(long seed) { }
        public void AddSeedMaterial(ReadOnlySpan<byte> seed) { }
        public void NextBytes(byte[] bytes) => SecretGenerator.FillRandom(bytes);
        public void NextBytes(byte[] bytes, int start, int len) => SecretGenerator.FillRandom(bytes.AsSpan(start, len));
        public void NextBytes(Span<byte> bytes) => SecretGenerator.FillRandom(bytes);
    }
}
