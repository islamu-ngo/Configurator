namespace Configurator.Core.Tests.Secrets;

using Configurator.Core.Definitions;
using Configurator.Core.Secrets;

public sealed class SecretValidationTests
{
    [Test]
    public async Task DirectPoliciesCannotBypassBoundedPasswordVocabulary()
    {
        var specification = new DefinitionSecretSpecification { Kind = DefinitionSecretKind.Password, Acquisition = DefinitionSecretAcquisition.Existing };
        await Assert.That(SecretValueValidator.Validate(specification with
        {
            Constraints = new() { ForbiddenCharacters = new string('x', 1025) }
        }, "synthetic")).Contains("secret-policy-invalid");
        await Assert.That(SecretValueValidator.Validate(specification with
        {
            Constraints = new() { CharacterSet = (DefinitionSecretCharacterSet)999 }
        }, "synthetic")).Contains("secret-policy-invalid");
    }
    [Test]
    public async Task PasswordRulesCountScalarsAndUtf8BytesWithoutNormalization()
    {
        DefinitionSecretSpecification policy = new()
        {
            Kind = DefinitionSecretKind.Password,
            Acquisition = DefinitionSecretAcquisition.Existing,
            MinLength = 3,
            Constraints = new() { MaxUtf8Bytes = 4, AllowWhitespace = false, MinSymbols = 1 }
        };
        IReadOnlyList<string> failures = SecretValueValidator.Validate(policy, "😀 ");
        await Assert.That(failures.Contains("secret-too-short")).IsTrue();
        await Assert.That(failures.Contains("secret-utf8-limit")).IsTrue();
        await Assert.That(failures.Contains("secret-whitespace-forbidden")).IsTrue();
        await Assert.That(failures.Contains("secret-symbol-required")).IsTrue();
    }

    [Test]
    public async Task SymmetricKeyUsesDecodedByteLengthAndCanonicalEncoding()
    {
        DefinitionSecretSpecification policy = new()
        {
            Kind = DefinitionSecretKind.SymmetricKey,
            Acquisition = DefinitionSecretAcquisition.Existing,
            Algorithm = DefinitionSecretAlgorithm.Hs512,
            Encoding = DefinitionSecretEncoding.Base64
        };
        await Assert.That(SecretValueValidator.Validate(policy, new string('A', 84) + "AA==")
            .Contains("secret-key-size-invalid")).IsFalse();
        await Assert.That(SecretValueValidator.Validate(policy, new string('A', 80))
            .Contains("secret-key-size-invalid")).IsTrue();
        await Assert.That(SecretValueValidator.Validate(policy, "YQ==\n")
            .Contains("secret-encoding-invalid")).IsTrue();
        await Assert.That(SecretValueValidator.Validate(policy, "YR==")
            .Contains("secret-encoding-invalid")).IsTrue();
    }

    [Test]
    public async Task UnknownProfilesAndOversizedEnvelopesFailClosed()
    {
        DefinitionSecretSpecification unknown = new() { Kind = (DefinitionSecretKind)999, Acquisition = DefinitionSecretAcquisition.Existing };
        await Assert.That(SecretValueValidator.Validate(unknown, "synthetic")
            .Contains("secret-profile-unavailable")).IsTrue();
        DefinitionSecretSpecification hash = unknown with { Kind = DefinitionSecretKind.PasswordHash, Algorithm = DefinitionSecretAlgorithm.Argon2id };
        await Assert.That(SecretValueValidator.Validate(hash, new string('A', 4097)).Count).IsGreaterThan(0);
        await Assert.That(SecretValueValidator.Validate(hash, "$argon2id$v=19$m=4194304,t=999,p=64$c2FsdA$aGFzaA").Count)
            .IsGreaterThan(0);
    }

    [Test]
    public async Task LowercaseHexAndUnpaddedBase64UrlAreRequired()
    {
        DefinitionSecretSpecification token = new()
        {
            Kind = DefinitionSecretKind.OpaqueToken,
            Acquisition = DefinitionSecretAcquisition.Issuer,
            Encoding = DefinitionSecretEncoding.Hex,
            Constraints = new() { MinBytes = 1, MaxBytes = 2 }
        };
        await Assert.That(SecretValueValidator.Validate(token, "ab").Count).IsEqualTo(0);
        await Assert.That(SecretValueValidator.Validate(token, "AB").Contains("secret-encoding-invalid")).IsTrue();
        token = token with { Encoding = DefinitionSecretEncoding.Base64Url };
        await Assert.That(SecretValueValidator.Validate(token, "YQ").Count).IsEqualTo(0);
        await Assert.That(SecretValueValidator.Validate(token, "YR").Contains("secret-encoding-invalid")).IsTrue();
        await Assert.That(SecretValueValidator.Validate(token, "YQ==").Contains("secret-encoding-invalid")).IsTrue();
    }
}
