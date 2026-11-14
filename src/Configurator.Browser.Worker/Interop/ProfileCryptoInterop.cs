namespace Configurator.Browser.Worker.Interop;

using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using Configurator.Browser.Worker.Profiles;

[SupportedOSPlatform("browser")]
internal sealed class ProfileCryptoInterop : IProfileCrypto
{
    public async ValueTask<byte[]> DecryptAsync(byte[] container, int recipientIndex, byte[] passphrase)
    {
        await ProfileCryptoImports.Decrypt(container, recipientIndex, passphrase).ConfigureAwait(false);
        return ProfileCryptoImports.CopyOutput();
    }

    public async ValueTask<byte[]> EncryptAsync(byte[] payload, IReadOnlyList<byte[]> recipients)
    {
        foreach (byte[] recipient in recipients) ProfileCryptoImports.AddRecipient(recipient);
        await ProfileCryptoImports.Encrypt(payload).ConfigureAwait(false);
        return ProfileCryptoImports.CopyOutput();
    }

    public void Clear() => ProfileCryptoImports.Clear();
}

[SupportedOSPlatform("browser")]
internal static partial class ProfileCryptoImports
{
    [JSImport("addRecipient", "profile-crypto")]
    internal static partial void AddRecipient(byte[] recipient);

    [JSImport("decrypt", "profile-crypto")]
    [return: JSMarshalAs<JSType.Promise<JSType.Void>>]
    internal static partial Task Decrypt(byte[] container, int index, byte[] credential);

    [JSImport("encrypt", "profile-crypto")]
    [return: JSMarshalAs<JSType.Promise<JSType.Void>>]
    internal static partial Task Encrypt(byte[] payload);

    [JSImport("copyOutput", "profile-crypto")]
    internal static partial byte[] CopyOutput();

    [JSImport("clear", "profile-crypto")]
    internal static partial void Clear();

    [JSImport("set", "profile-output")]
    internal static partial void SetOutput(byte[] output);
}
