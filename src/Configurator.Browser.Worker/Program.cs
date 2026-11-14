using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text.Json;
using Configurator.Browser.Profiles;
using Configurator.Browser.Worker.Interop;
using Configurator.Browser.Worker.Profiles;
using Configurator.Core.Profiles;

namespace Configurator.Browser.Worker;

internal static class Program
{
    public static void Main() { }
}

    [SupportedOSPlatform("browser")]
    public static partial class ProfileWorkerEntry
    {
        private static readonly ProfileWorkerSession Session = new(new BrowserProfileContainerCodec(new ProfileCryptoInterop()));
        private static readonly List<byte[]> Recipients = [];

        [JSExport]
        public static void AddRecipient(byte[] recipient)
        {
            try
            {
                if (Recipients.Count >= ProfileContainerPolicy.MaximumRecipients)
                    throw new ProfileException(ProfileFailureCode.LimitExceeded);
                ProfileContainerPolicy.ValidatePassphrase(recipient, creating: true);
                Recipients.Add(recipient.ToArray());
            }
            finally { CryptographicOperations.ZeroMemory(recipient); }
        }

        [JSExport]
        public static void ClearRecipients()
        {
            foreach (byte[] recipient in Recipients) CryptographicOperations.ZeroMemory(recipient);
            Recipients.Clear();
        }

        [JSExport]
        [return: JSMarshalAs<JSType.Promise<JSType.String>>]
        public static async Task<string> Execute(string metadata, byte[] payload, byte[] credential,
            byte[] definition, byte[] answers)
        {
            ProfileWorkerRequest? request = null;
            try
            {
                if (metadata.Length > 256 * 1024) throw new ProfileException(ProfileFailureCode.LimitExceeded);
                request = JsonSerializer.Deserialize(metadata, ProfileWorkerJsonContext.Default.ProfileWorkerRequest)
                    ?? throw new ProfileException(ProfileFailureCode.InvalidPayload);
                using var input = new ProfileWorkerInput(payload, credential, definition, answers,
                    Recipients.Select(bytes => (ReadOnlyMemory<byte>)bytes).ToArray());
                using ProfileWorkerResult result = await Session.ExecuteAsync(request, input).ConfigureAwait(false);
                if (!result.Output.IsEmpty)
                {
                    byte[] copy = result.Output.ToArray();
                    try { ProfileCryptoImports.SetOutput(copy); }
                    finally { CryptographicOperations.ZeroMemory(copy); }
                }
                return JsonSerializer.Serialize(result.Reply, ProfileWorkerJsonContext.Default.ProfileWorkerReply);
            }
            catch (Exception exception)
            {
                // This is the external boundary. Neither library nor parser exception text crosses it.
                if (request is null) throw new ProfileException(ProfileFailureCode.InvalidPayload);
                string code = exception switch
                {
                    ProfileException profile => profile.Code.ToString(),
                    ProfileRecoveryException recovery => recovery.Failure.ToString(),
                    OperationCanceledException or ObjectDisposedException => "retired",
                    _ => "invalid"
                };
                return JsonSerializer.Serialize(new ProfileWorkerReply(request.Authority, request.RequestId, code),
                    ProfileWorkerJsonContext.Default.ProfileWorkerReply);
            }
            finally
            {
                ClearRecipients();
                CryptographicOperations.ZeroMemory(payload);
                CryptographicOperations.ZeroMemory(credential);
                CryptographicOperations.ZeroMemory(definition);
                CryptographicOperations.ZeroMemory(answers);
            }
        }
    }
