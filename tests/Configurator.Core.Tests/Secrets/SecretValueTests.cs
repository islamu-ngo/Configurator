using System.Security.Cryptography;
using Configurator.Core.Secrets;

namespace Configurator.Core.Tests.Secrets;

public sealed class SecretValueTests
{
    [Test]
    public async Task InputAndExportedCopiesHaveIndependentOwnership()
    {
        char[] input = ['é', 'A'];
        using var secret = new SecretValue(input);
        input.AsSpan().Clear();
        byte[] exported = secret.CopyUtf8Bytes();
        try
        {
            await Assert.That(exported).IsEquivalentTo(new byte[] { 0xc3, 0xa9, 0x41 });
            exported[0] = 0;
            await Assert.That(secret.CopyValue()).IsEqualTo("éA");
        }
        finally { CryptographicOperations.ZeroMemory(exported); }
    }

    [Test]
    public async Task RepeatedDisposalRevokesAllAliasesAndExportMethods()
    {
        using var secret = new SecretValue("synthetic-fixture");
        SecretValue alias = secret;
        secret.Dispose();
        alias.Dispose();
        await Assert.That(() => alias.CopyValue()).Throws<ObjectDisposedException>();
        await Assert.That(() => alias.CopyUtf8Bytes()).Throws<ObjectDisposedException>();
        await Assert.That(alias.ToString()).IsEqualTo("SecretValue:Redacted");
    }

    [Test]
    public async Task ResultDisposalRevokesBothOutputsEvenWhenTheyShareOneOwner()
    {
        using var secret = new SecretValue("synthetic-fixture");
        using var result = new SecretGenerationResult(secret, secret);
        result.Dispose();
        result.Dispose();
        await Assert.That(() => result.Output!.CopyValue()).Throws<ObjectDisposedException>();
        await Assert.That(() => result.GeneratedPassword!.CopyUtf8Bytes()).Throws<ObjectDisposedException>();
    }
}
