using System.Security.Cryptography;
using Configurator.Core.Definitions;
using Configurator.Core.Secrets;

namespace Configurator.Core.Tests.Secrets;

public sealed class SecretGenerationTests
{
    [Test]
    public async Task PasswordSatisfiesCategoriesExclusionsAndDisposal()
    {
        var specification = new DefinitionSecretSpecification
        {
            Kind = "password",
            Acquisition = "local",
            MinLength = 32,
            MaxLength = 64,
            Constraints = new() { MinLowercase = 2, MinUppercase = 2, MinDigits = 2, MinSymbols = 2, AllowedSymbols = "!@", ForbiddenCharacters = "0O", AllowWhitespace = false },
            Generation = new() { Length = 32 },
        };
        using var result = SecretGenerator.Generate(specification);
        await Assert.That(result.Succeeded).IsTrue();
        string value = result.Output!.CopyValue();
        await Assert.That(value.Length).IsEqualTo(32);
        await Assert.That(value.Count(char.IsAsciiLetterLower) >= 2 && value.Count(char.IsAsciiLetterUpper) >= 2 && value.Count(char.IsAsciiDigit) >= 2 && value.Count(c => c is '!' or '@') >= 2).IsTrue();
        await Assert.That(value.Any(c => c is '0' or 'O' || char.IsWhiteSpace(c))).IsFalse();
        await Assert.That(result.Output.ToString()).IsEqualTo("SecretValue:Redacted");
        result.Dispose();
        await Assert.That(() => result.Output.CopyValue()).Throws<ObjectDisposedException>();
    }

    [Test]
    public async Task TokenIsCanonicalFreshAndCarries256Bits()
    {
        var specification = new DefinitionSecretSpecification { Kind = "opaque-token", Acquisition = "local", Encoding = "hex", Generation = new() { Bytes = 32 } };
        using var entropy = new SequenceEntropy();
        using var first = SecretGenerator.Generate(specification, entropy: entropy);
        using var second = SecretGenerator.Generate(specification, entropy: entropy);
        await Assert.That(first.Succeeded && second.Succeeded).IsTrue();
        await Assert.That(first.Output!.CopyValue().Length).IsEqualTo(64);
        await Assert.That(first.Output.CopyValue().All(c => char.IsAsciiDigit(c) || c is >= 'a' and <= 'f')).IsTrue();
        await Assert.That(first.Output.CopyValue() == second.Output!.CopyValue()).IsFalse();
    }

    [Test]
    public async Task InfeasibleOrUnauthorizedGenerationFailsWithoutOutput()
    {
        var specification = new DefinitionSecretSpecification { Kind = "password", Acquisition = "local", Generation = new() { Length = 4 } };
        using var shortResult = SecretGenerator.Generate(specification);
        using var issuerResult = SecretGenerator.Generate(specification with { Acquisition = "issuer" });
        using var absentResult = SecretGenerator.Generate(specification with { Generation = null });
        await Assert.That(shortResult.Succeeded || issuerResult.Succeeded || absentResult.Succeeded).IsFalse();
    }

    [Test]
    public async Task DeterministicEntropyHasIndependentBase64UrlEncoding()
    {
        using var entropy = new ZeroEntropy();
        var specification = new DefinitionSecretSpecification { Kind = "opaque-token", Acquisition = "local", Encoding = "base64url", Generation = new() { Bytes = 32 } };
        using var result = SecretGenerator.Generate(specification, entropy: entropy);
        await Assert.That(result.Output!.CopyValue()).IsEqualTo("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA");
    }

    [Test]
    public async Task GeneratedPasswordHasSeparateDisposableCustody()
    {
        var password = new DefinitionSecretSpecification { Kind = "password", Acquisition = "local", Generation = new() };
        var specification = new DefinitionSecretSpecification
        {
            Kind = "digest",
            Acquisition = "local",
            Algorithm = "SHA256",
            Encoding = "hex",
            Generation = new() { Input = "generated", Password = password },
        };
        using var result = SecretGenerator.Generate(specification);
        await Assert.That(result.Succeeded).IsTrue();
        await Assert.That(result.GeneratedPassword).IsNotNull();
        await Assert.That(result.Output!.CopyValue().Length).IsEqualTo(64);
        await Assert.That(result.GeneratedPassword!.CopyValue().Length).IsEqualTo(32);
        result.Dispose();
        await Assert.That(() => result.GeneratedPassword.CopyValue()).Throws<ObjectDisposedException>();
    }

    private sealed class ZeroEntropy : RandomNumberGenerator
    {
        public override void GetBytes(byte[] data) => data.AsSpan().Clear();
        public override void GetBytes(Span<byte> data) => data.Clear();
    }

    private sealed class SequenceEntropy : RandomNumberGenerator
    {
        private byte _next = 1;
        public override void GetBytes(byte[] data) => GetBytes(data.AsSpan());
        public override void GetBytes(Span<byte> data) => data.Fill(_next++);
    }
}
