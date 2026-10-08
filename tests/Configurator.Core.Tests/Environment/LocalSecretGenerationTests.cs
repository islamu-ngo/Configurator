using System.Reflection;
using System.Security.Cryptography;
using Configurator.Core.Environment;

namespace Configurator.Core.Tests.EnvironmentTests;

public sealed class LocalSecretGenerationTests
{
    [Test]
    public async Task DeclaredSecretGenerationRetainsEntropyFloorFreshnessAndDisposal()
    {
        using var entropy = new SequenceEntropy();
        var constructor = typeof(LocalSecretGenerator).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic, null, [typeof(RandomNumberGenerator)], null)!;
        using var generator = (LocalSecretGenerator)constructor.Invoke([entropy]);
        using var first = generator.Generate(Field(EnvironmentVariableSensitivity.Secret), LocalSecretGenerationProfile.OpaqueUrlSafe256);
        using var second = generator.Generate(Field(EnvironmentVariableSensitivity.Secret), LocalSecretGenerationProfile.OpaqueUrlSafe256);
        using var denied = generator.Generate(Field(EnvironmentVariableSensitivity.Public), LocalSecretGenerationProfile.OpaqueUrlSafe256);
        await Assert.That(first.Output!.CharacterCount).IsEqualTo(43);
        await Assert.That(first.Output.CopyValue() == second.Output!.CopyValue()).IsFalse();
        await Assert.That(first.Output.CopyValue().All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_')).IsTrue();
        await Assert.That(denied.Succeeded).IsFalse();
        await Assert.That(first.Output.ToString()).IsEqualTo("GeneratedDotenvValue:Redacted");
        first.Dispose();
        await Assert.That(() => first.Output.CopyValue()).Throws<ObjectDisposedException>();
    }

    private static EnvironmentVariableDefinition Field(EnvironmentVariableSensitivity sensitivity) =>
        new("SERVICE_TOKEN", EnvironmentVariableCategory.Security, sensitivity,
            EnvironmentVariableRequirement.Required, null, 1, EnvironmentActivationExpression.Capability("service"),
            "text", new(EnvironmentGenerationSurface.Dotenv, 1, null, false),
            EnvironmentRestartBehavior.Process, new("title", "help", "service"));

    private sealed class SequenceEntropy : RandomNumberGenerator
    {
        private byte _next = 1;
        public override void GetBytes(byte[] data) => GetBytes(data.AsSpan());
        public override void GetBytes(Span<byte> data) => data.Fill(_next++);
    }
}
