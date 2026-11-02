using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Configurator.Core.Definitions;
using Configurator.Core.Secrets;

namespace Configurator.Core.Tests.Secrets;

public sealed class SecretKeyMaterialTests
{
    [Test]
    public async Task MalformedAndOversizedKeysFailWithoutAcceptedMaterial()
    {
        var policy = Policy("RS256", "pkcs8-pem");
        await Assert.That(SecretKeyMaterial.Validate(policy, new string('x', 65537))).IsEqualTo("secret-key-format-invalid");
        await Assert.That(SecretKeyMaterial.Validate(policy, "-----BEGIN PUBLIC KEY-----\nAA==\n-----END PUBLIC KEY-----")).IsEqualTo("secret-key-format-invalid");
        await Assert.That(SecretKeyMaterial.Validate(Policy("ES256", "jwk"), "{}")).IsEqualTo("secret-key-format-invalid");
    }

    [Test]
    [Arguments("ES256", "P-256", 32)]
    [Arguments("ES384", "P-384", 48)]
    [Arguments("ES512", "P-521", 66)]
    public async Task EcJwkHasFixedWidthAndInteroperates(string algorithm, string curve, int width)
    {
        var policy = Policy(algorithm, "jwk", curve);
        string value = SecretKeyMaterial.Generate(policy);
        var json = JsonNode.Parse(value)!.AsObject();
        await Assert.That(SecretKeyMaterial.Validate(policy, value)).IsNull();
        foreach (string name in new[] { "x", "y", "d" })
            await Assert.That(Base64Url.DecodeFromChars(json[name]!.GetValue<string>()).Length).IsEqualTo(width);
        using var key = ECDsa.Create(new ECParameters
        {
            Curve = ECCurve.CreateFromFriendlyName(curve switch { "P-256" => "nistP256", "P-384" => "nistP384", _ => "nistP521" }),
            D = Base64Url.DecodeFromChars(json["d"]!.GetValue<string>()),
            Q = new ECPoint { X = Base64Url.DecodeFromChars(json["x"]!.GetValue<string>()), Y = Base64Url.DecodeFromChars(json["y"]!.GetValue<string>()) },
        });
        byte[] data = [1, 2, 3];
        await Assert.That(key.VerifyData(data, key.SignData(data, HashAlgorithmName.SHA512), HashAlgorithmName.SHA512)).IsTrue();
        json["d"] = Base64Url.EncodeToString(new byte[width]);
        await Assert.That(SecretKeyMaterial.Validate(policy, json.ToJsonString())).IsEqualTo("secret-key-format-invalid");
        json["d"] = "AQ";
        await Assert.That(SecretKeyMaterial.Validate(policy, json.ToJsonString())).IsEqualTo("secret-key-format-invalid");
    }

    [Test]
    [Arguments("ES256", "P-256", "nistP256", 32)]
    [Arguments("ES384", "P-384", "nistP384", 48)]
    [Arguments("ES512", "P-521", "nistP521", 66)]
    public async Task EcLeadingZeroPrivateScalarIsRequiredAndPointMustMatch(string algorithm, string curve, string nativeCurve, int width)
    {
        byte[] scalar = new byte[width];
        scalar[^1] = 1;
        using var key = ECDsa.Create(new ECParameters { Curve = ECCurve.CreateFromFriendlyName(nativeCurve), D = scalar });
        ECParameters coordinates = key.ExportParameters(false);
        var json = new JsonObject
        {
            ["kty"] = "EC",
            ["alg"] = algorithm,
            ["crv"] = curve,
            ["x"] = Base64Url.EncodeToString(coordinates.Q.X!),
            ["y"] = Base64Url.EncodeToString(coordinates.Q.Y!),
            ["d"] = Base64Url.EncodeToString(scalar),
        };
        var policy = Policy(algorithm, "jwk", curve);
        await Assert.That(SecretKeyMaterial.Validate(policy, json.ToJsonString())).IsNull();
        scalar[^1] = 2;
        json["d"] = Base64Url.EncodeToString(scalar);
        await Assert.That(SecretKeyMaterial.Validate(policy, json.ToJsonString())).IsNotNull();
        json["d"] = "AQ";
        await Assert.That(SecretKeyMaterial.Validate(policy, json.ToJsonString())).IsNotNull();
        CryptographicOperations.ZeroMemory(scalar);
    }

    [Test]
    [Arguments("RS256", 2048)]
    [Arguments("PS256", 3072)]
    [Arguments("RS256", 4096)]
    public async Task RsaPemInteroperatesAndRejectsWrongPolicy(string algorithm, int bits)
    {
        var policy = Policy(algorithm, "pkcs8-pem") with { Constraints = new() { KeySize = bits } };
        string value = SecretKeyMaterial.Generate(policy);
        await Assert.That(SecretKeyMaterial.Validate(policy, value)).IsNull();
        using var key = RSA.Create();
        key.ImportFromPem(value);
        byte[] data = [4, 5, 6];
        var padding = algorithm == "PS256" ? RSASignaturePadding.Pss : RSASignaturePadding.Pkcs1;
        await Assert.That(key.VerifyData(data, key.SignData(data, HashAlgorithmName.SHA256, padding), HashAlgorithmName.SHA256, padding)).IsTrue();
        await Assert.That(SecretKeyMaterial.Validate(Policy("ES256", "pkcs8-pem", "P-256"), value)).IsNotNull();
        await Assert.That(SecretKeyMaterial.Validate(policy with { Constraints = new() { KeySize = bits == 2048 ? 3072 : 2048 } }, value)).IsNotNull();
    }

    [Test]
    [Arguments("ES256", "P-256")]
    [Arguments("ES384", "P-384")]
    [Arguments("ES512", "P-521")]
    public async Task EcPemInteroperatesAndRejectsWrongCurve(string algorithm, string curve)
    {
        var policy = Policy(algorithm, "pkcs8-pem", curve);
        string value = SecretKeyMaterial.Generate(policy);
        await Assert.That(SecretKeyMaterial.Validate(policy, value)).IsNull();
        using var key = ECDsa.Create();
        key.ImportFromPem(value);
        byte[] data = [7, 8, 9];
        await Assert.That(key.VerifyData(data, key.SignData(data, HashAlgorithmName.SHA512), HashAlgorithmName.SHA512)).IsTrue();
        var wrongPolicy = Policy(algorithm == "ES256" ? "ES384" : "ES256", "pkcs8-pem", algorithm == "ES256" ? "P-384" : "P-256");
        await Assert.That(SecretKeyMaterial.Validate(wrongPolicy, value)).IsNotNull();
    }

    [Test]
    public async Task DuplicateAlgorithmAndAppendedContainersAreRejected()
    {
        var policy = Policy("ES256", "jwk", "P-256");
        string value = SecretKeyMaterial.Generate(policy);
        string duplicate = value.Replace("\"alg\":\"ES256\"", "\"alg\":\"ES256\",\"alg\":\"ES256\"", StringComparison.Ordinal);
        await Assert.That(SecretKeyMaterial.Validate(policy, duplicate)).IsNotNull();
        string wrongAlgorithm = value.Replace("\"alg\":\"ES256\"", "\"alg\":\"ES384\"", StringComparison.Ordinal);
        await Assert.That(SecretKeyMaterial.Validate(policy, wrongAlgorithm)).IsNotNull();
        var pemPolicy = Policy("ES256", "pkcs8-pem", "P-256");
        string pem = SecretKeyMaterial.Generate(pemPolicy);
        await Assert.That(SecretKeyMaterial.Validate(pemPolicy, pem + "\n" + pem)).IsNotNull();
    }

    [Test]
    public async Task RsaJwkRejectsNoncanonicalAndInconsistentCrt()
    {
        var policy = Policy("RS256", "jwk");
        var json = JsonNode.Parse(SecretKeyMaterial.Generate(policy))!.AsObject();
        await Assert.That(SecretKeyMaterial.Validate(policy, json.ToJsonString())).IsNull();
        using var native = RSA.Create(new RSAParameters
        {
            Modulus = Base64Url.DecodeFromChars(json["n"]!.GetValue<string>()),
            Exponent = Base64Url.DecodeFromChars(json["e"]!.GetValue<string>()),
            D = Base64Url.DecodeFromChars(json["d"]!.GetValue<string>()),
            P = Base64Url.DecodeFromChars(json["p"]!.GetValue<string>()),
            Q = Base64Url.DecodeFromChars(json["q"]!.GetValue<string>()),
            DP = Base64Url.DecodeFromChars(json["dp"]!.GetValue<string>()),
            DQ = Base64Url.DecodeFromChars(json["dq"]!.GetValue<string>()),
            InverseQ = Base64Url.DecodeFromChars(json["qi"]!.GetValue<string>()),
        });
        byte[] message = [10, 11, 12];
        await Assert.That(native.VerifyData(message, native.SignData(message, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1),
            HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)).IsTrue();
        string modulus = json["n"]!.GetValue<string>();
        byte[] inconsistentModulus = Base64Url.DecodeFromChars(modulus);
        inconsistentModulus[^1] ^= 2;
        json["n"] = Base64Url.EncodeToString(inconsistentModulus);
        await Assert.That(SecretKeyMaterial.Validate(policy, json.ToJsonString())).IsNotNull();
        CryptographicOperations.ZeroMemory(inconsistentModulus);
        json["n"] = Base64Url.EncodeToString([0, .. Base64Url.DecodeFromChars(modulus)]);
        await Assert.That(SecretKeyMaterial.Validate(policy, json.ToJsonString())).IsNotNull();
        json["n"] = modulus;
        json["e"] = "Ag";
        await Assert.That(SecretKeyMaterial.Validate(policy, json.ToJsonString())).IsNotNull();
        json["e"] = "AQAB";
        json["q"] = json["p"]!.GetValue<string>();
        await Assert.That(SecretKeyMaterial.Validate(policy, json.ToJsonString())).IsNotNull();
        json.Remove("d");
        await Assert.That(SecretKeyMaterial.Validate(policy, json.ToJsonString())).IsNotNull();
    }

    private static DefinitionSecretSpecification Policy(string algorithm, string encoding, string? curve = null) => new()
    {
        Kind = "private-key",
        Acquisition = "local",
        Algorithm = algorithm,
        Encoding = encoding,
        Constraints = new() { KeySize = 2048, Curve = curve },
        Generation = new(),
    };
}
