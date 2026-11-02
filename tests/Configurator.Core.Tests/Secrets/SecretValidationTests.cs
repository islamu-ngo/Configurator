namespace Configurator.Core.Tests.Secrets;

using Configurator.Core.Definitions;
using Configurator.Core.Secrets;

public sealed class SecretValidationTests
{
    [Test]
    public async Task DirectPoliciesCannotBypassBoundedPasswordVocabulary()
    {
        var specification = new DefinitionSecretSpecification { Kind = "password", Acquisition = "existing" };
        await Assert.That(SecretValueValidator.Validate(specification with
        {
            Constraints = new() { ForbiddenCharacters = new string('x', 1025) }
        }, "synthetic")).Contains("secret-policy-invalid");
        await Assert.That(SecretValueValidator.Validate(specification with
        {
            Constraints = new() { CharacterSet = "unrecognized" }
        }, "synthetic")).Contains("secret-policy-invalid");
    }
    [Test]
    public async Task PasswordRulesCountScalarsAndUtf8BytesWithoutNormalization()
    {
        DefinitionSecretSpecification policy = new()
        {
            Kind = "password",
            Acquisition = "existing",
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
            Kind = "symmetric-key",
            Acquisition = "existing",
            Algorithm = "HS512",
            Encoding = "base64"
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
        DefinitionSecretSpecification unknown = new() { Kind = "unknown", Acquisition = "existing" };
        await Assert.That(SecretValueValidator.Validate(unknown, "synthetic")
            .Contains("secret-profile-unavailable")).IsTrue();
        DefinitionSecretSpecification hash = unknown with { Kind = "password-hash", Algorithm = "argon2id" };
        await Assert.That(SecretValueValidator.Validate(hash, new string('A', 4097)).Count).IsGreaterThan(0);
        await Assert.That(SecretValueValidator.Validate(hash, "$argon2id$v=19$m=4194304,t=999,p=64$c2FsdA$aGFzaA").Count)
            .IsGreaterThan(0);
    }

    [Test]
    public async Task LowercaseHexAndUnpaddedBase64UrlAreRequired()
    {
        DefinitionSecretSpecification token = new()
        {
            Kind = "opaque-token",
            Acquisition = "issuer",
            Encoding = "hex",
            Constraints = new() { MinBytes = 1, MaxBytes = 2 }
        };
        await Assert.That(SecretValueValidator.Validate(token, "ab").Count).IsEqualTo(0);
        await Assert.That(SecretValueValidator.Validate(token, "AB").Contains("secret-encoding-invalid")).IsTrue();
        token = token with { Encoding = "base64url" };
        await Assert.That(SecretValueValidator.Validate(token, "YQ").Count).IsEqualTo(0);
        await Assert.That(SecretValueValidator.Validate(token, "YR").Contains("secret-encoding-invalid")).IsTrue();
        await Assert.That(SecretValueValidator.Validate(token, "YQ==").Contains("secret-encoding-invalid")).IsTrue();
    }
}
