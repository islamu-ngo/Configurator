using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Configurator.Core.Definitions;
using Configurator.Core.Secrets;
using Org.BouncyCastle.Crypto.Generators;

namespace Configurator.Core.Tests.Secrets;

public sealed class SecretDerivationTests
{
    [Test]
    public async Task HostileHashCostsAndOversizedValuesRejectBeforeDerivation()
    {
        var spec = Hash("argon2id");
        await Assert.That(SecretDerivation.Validate(spec,
            "$argon2id$v=19$m=4194304,t=999,p=64$AAAAAAAAAAAAAAAAAAAAAA$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"))
            .IsEqualTo("secret-cost-out-of-range");
        await Assert.That(SecretDerivation.Validate(spec, new string('x', 4097)))
            .IsEqualTo("secret-hash-format-invalid");
    }

    [Test]
    public async Task BcryptRejectsEmbeddedNullAndOversizedUtf8Input()
    {
        await Assert.That(() => SecretDerivation.Derive(Hash("bcrypt"), "a\0b"))
            .Throws<ArgumentException>().WithMessage("secret-input-invalid");
        await Assert.That(() => SecretDerivation.Derive(Hash("bcrypt"), new string('é', 37)))
            .Throws<ArgumentException>().WithMessage("secret-input-invalid");
    }

    [Test]
    [Arguments("SHA256", "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad")]
    [Arguments("SHA384", "cb00753f45a35e8bb5a03d699ac65007272c32ab0eded1631a8b605a43ff5bed8086072ba1e7cc2358baeca134c825a7")]
    [Arguments("SHA512", "ddaf35a193617abacc417349ae20413112e6fa4e89a97ea20a9eeee64b55d39a2192992a274fc1a836ba3c23a3feebbd454d4423643ce80e2a9ac94fa54ca49f")]
    public async Task Sha2MatchesIndependentPublishedDigest(string algorithm, string expected)
    {
        var spec = new DefinitionSecretSpecification
        {
            Kind = DefinitionSecretKind.Digest,
            Acquisition = DefinitionSecretAcquisition.Local,
            Algorithm = DefinitionSecretVocabulary.ParseAlgorithm(algorithm),
            Encoding = DefinitionSecretEncoding.Hex,
            Generation = new(),
        };
        string result = SecretDerivation.Derive(spec, "abc");
        await Assert.That(result).IsEqualTo(expected);
        await Assert.That(SecretDerivation.Validate(spec, result)).IsNull();
    }

    [Test]
    public async Task IdentityEnvelopeUsesFixedHeaderAndIndependentlyVerifiedSubkey()
    {
        var spec = Hash("aspnet-identity-v3");
        string encoded = SecretDerivation.Derive(spec, "synthetic-fixture");
        byte[] envelope = Convert.FromBase64String(encoded);
        try
        {
            await Assert.That(envelope.Length).IsEqualTo(61);
            await Assert.That(envelope[0]).IsEqualTo((byte)1);
            await Assert.That(BinaryPrimitives.ReadUInt32BigEndian(envelope.AsSpan(1))).IsEqualTo(2u);
            await Assert.That(BinaryPrimitives.ReadUInt32BigEndian(envelope.AsSpan(5))).IsEqualTo(210000u);
            await Assert.That(BinaryPrimitives.ReadUInt32BigEndian(envelope.AsSpan(9))).IsEqualTo(16u);
            byte[] expected = Rfc2898DeriveBytes.Pbkdf2("synthetic-fixture", envelope.AsSpan(13, 16), 210000, HashAlgorithmName.SHA512, 32);
            try { await Assert.That(CryptographicOperations.FixedTimeEquals(expected, envelope.AsSpan(29))).IsTrue(); }
            finally { CryptographicOperations.ZeroMemory(expected); }
            await Assert.That(SecretDerivation.Validate(spec, encoded)).IsNull();
        }
        finally { CryptographicOperations.ZeroMemory(envelope); }
    }

    [Test]
    [Arguments("pbkdf2-sha256")]
    [Arguments("pbkdf2-sha512")]
    [Arguments("argon2id")]
    [Arguments("bcrypt")]
    public async Task GeneratedHashHasCanonicalBoundedFormat(string algorithm)
    {
        var spec = Hash(algorithm);
        string encoded = SecretDerivation.Derive(spec, "synthetic-fixture");
        await Assert.That(SecretDerivation.Validate(spec, encoded)).IsNull();
        if (algorithm == "argon2id")
        {
            await Assert.That(encoded.StartsWith("$argon2id$v=19$m=19456,t=2,p=1$", StringComparison.Ordinal)).IsTrue();
            await Assert.That(encoded.Split('$')[4].Contains('=')).IsFalse();
            await Assert.That(encoded.Split('$')[5].Contains('=')).IsFalse();
        }
        else if (algorithm == "bcrypt")
        {
            await Assert.That(encoded.Length).IsEqualTo(60);
            await Assert.That(encoded.StartsWith("$2b$12$", StringComparison.Ordinal)).IsTrue();
            char[] plaintext = "synthetic-fixture".ToCharArray();
            try { await Assert.That(OpenBsdBCrypt.CheckPassword(encoded, plaintext)).IsTrue(); }
            finally { CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(plaintext.AsSpan())); }
        }
        else
        {
            using JsonDocument json = JsonDocument.Parse(encoded);
            await Assert.That(json.RootElement.GetProperty("version").GetInt32()).IsEqualTo(1);
            await Assert.That(json.RootElement.GetProperty("algorithm").GetString()).IsEqualTo(algorithm);
            await Assert.That(json.RootElement.EnumerateObject().Count()).IsEqualTo(5);
        }
    }

    [Test]
    public async Task Argon2MatchesIndependentNativeReferenceVector()
    {
        using var entropy = new FixedSalt();
        string result = SecretDerivation.Derive(Hash("argon2id"), "synthetic-fixture", entropy);
        // Native argon2 20190702: -id -t 2 -k 19456 -p 1 -l 32, ASCII salt 0123456789abcdef.
        await Assert.That(result).IsEqualTo("$argon2id$v=19$m=19456,t=2,p=1$MDEyMzQ1Njc4OWFiY2RlZg$qB4be/2hC0lDB/v2pPoR5du1elQSHn1XRZq5PWLd5QY");
    }

    [Test]
    public async Task Pbkdf2MatchesIndependentOpenSslVector()
    {
        using var entropy = new FixedSalt();
        string result = SecretDerivation.Derive(Hash("pbkdf2-sha256"), "synthetic-fixture", entropy);
        using JsonDocument json = JsonDocument.Parse(result);
        byte[] bytes = Convert.FromBase64String(json.RootElement.GetProperty("derivedKey").GetString()!);
        try
        {
            // OpenSSL kdf PBKDF2: SHA256, 600000 iterations, ASCII salt 0123456789abcdef.
            await Assert.That(Convert.ToHexStringLower(bytes)).IsEqualTo("4b5f47d8f372c86d807af775addedc7e5829cb9d3f2221f47c46839f80e9f5c5");
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    private sealed class FixedSalt : RandomNumberGenerator
    {
        public override void GetBytes(byte[] data) => GetBytes(data.AsSpan());
        public override void GetBytes(Span<byte> data) => Encoding.ASCII.GetBytes("0123456789abcdef", data);
    }

    [Test]
    [Arguments("utf8", "abc")]
    [Arguments("hex", "616263")]
    [Arguments("base64", "YWJj")]
    [Arguments("base64url", "YWJj")]
    public async Task DigestDecodesDeclaredInputWithoutChangingBytes(string encoding, string input)
    {
        var spec = new DefinitionSecretSpecification
        {
            Kind = DefinitionSecretKind.Digest,
            Acquisition = DefinitionSecretAcquisition.Local,
            Algorithm = DefinitionSecretAlgorithm.Sha256,
            Encoding = DefinitionSecretEncoding.Hex,
            Generation = new() { InputEncoding = DefinitionSecretVocabulary.ParseEncoding(encoding) },
        };
        await Assert.That(SecretDerivation.Derive(spec, input))
            .IsEqualTo("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad");
    }

    [Test]
    public async Task EachDerivationObtainsFreshSaltFromEntropySource()
    {
        using var entropy = new SequenceSalt();
        byte[] first = Convert.FromBase64String(SecretDerivation.Derive(Hash("aspnet-identity-v3"), "synthetic-fixture", entropy));
        byte[] second = Convert.FromBase64String(SecretDerivation.Derive(Hash("aspnet-identity-v3"), "synthetic-fixture", entropy));
        try
        {
            await Assert.That(Convert.ToHexStringLower(first.AsSpan(13, 16))).IsEqualTo("01010101010101010101010101010101");
            await Assert.That(Convert.ToHexStringLower(second.AsSpan(13, 16))).IsEqualTo("02020202020202020202020202020202");
        }
        finally { CryptographicOperations.ZeroMemory(first); CryptographicOperations.ZeroMemory(second); }
    }

    private sealed class SequenceSalt : RandomNumberGenerator
    {
        private byte _next = 1;
        public override void GetBytes(byte[] data) => GetBytes(data.AsSpan());
        public override void GetBytes(Span<byte> data) => data.Fill(_next++);
    }

    [Test]
    public async Task DuplicatePbkdf2MembersAndNoncanonicalEncodingsReject()
    {
        const string invalid = "{\"version\":1,\"version\":1,\"algorithm\":\"pbkdf2-sha256\",\"iterations\":600000,\"salt\":\"AAAAAAAAAAAAAAAAAAAAAA==\",\"derivedKey\":\"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=\"}";
        await Assert.That(SecretDerivation.Validate(Hash("pbkdf2-sha256"), invalid)).IsEqualTo("secret-hash-format-invalid");
        await Assert.That(SecretDerivation.Validate(Hash("argon2id"),
            "$argon2id$v=19$m=19456,t=2,p=1$AAAAAAAAAAAAAAAAAAAAAA==$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA="))
            .IsEqualTo("secret-hash-format-invalid");
    }

    private static DefinitionSecretSpecification Hash(string algorithm) => new()
    {
        Kind = DefinitionSecretKind.PasswordHash,
        Acquisition = DefinitionSecretAcquisition.Local,
        Algorithm = DefinitionSecretVocabulary.ParseAlgorithm(algorithm),
        Generation = new(),
        Constraints = new() { Iterations = algorithm == "pbkdf2-sha256" ? 600000 : algorithm == "pbkdf2-sha512" ? 210000 : 2 },
    };
}
