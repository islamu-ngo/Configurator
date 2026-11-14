namespace Configurator.Browser.Profiles;

using System.Security.Cryptography;
using System.Text.Json;
using Configurator.Core.Definitions;
using Configurator.Core.Profiles;
using Microsoft.JSInterop;

/// <summary>
/// Browser transport for one owned dedicated worker. The callback reads the actual workspace's authority.
/// Retirement terminates the worker synchronously; it promises neither physical WebCrypto termination nor erasure.
/// </summary>
public sealed class ProfileWorkerClient(IJSInProcessRuntime javascript, Func<ProfileWorkerAuthority> currentAuthority)
    : IDisposable
{
    private static int _admittedOperation;
    private IJSInProcessObjectReference? _bridge;
    private string _channel = Guid.NewGuid().ToString("N");
    private long _request;
    private bool _disposed;

    public Task<ProfileWorkerReply> OpenAsync(ReadOnlyMemory<byte> container, int recipientIndex,
        ReadOnlyMemory<byte> passphraseUtf8, Func<ProfileWorkerReply, bool> admitHistoricalIdentity,
        CancellationToken cancellationToken = default) =>
        RunAsync(new(Capture(), 0, "unlock") { RecipientIndex = recipientIndex }, container, passphraseUtf8,
            null, null, [], null, cancellationToken, historicalAdmission: admitHistoricalIdentity);

    public Task<ProfileWorkerReply> PlanAsync(Guid profileReference, Definition admittedTarget,
        CancellationToken cancellationToken = default) =>
        RunAsync(new(Capture(), 0, "plan") { ProfileReference = profileReference }, default, default,
            admittedTarget, null, [], null, cancellationToken);

    /// <summary>Retires the worker's whole retained root before an ordinary replacement can be accepted.</summary>
    public async Task<ProfileWorkerReply> RetireAsync(Guid profileReference, DefinitionPath path,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(path);
        DiscardOutput();
        try
        {
            return await RunAsync(new(Capture(), 0, "retire-field")
            {
                ProfileReference = profileReference, ExcludedPaths = [path.ToPointer()]
            }, default, default, null, null, [], null, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            Retire();
            throw;
        }
    }

    public Task<ProfileWorkerReply> DownloadArtifactAsync(Guid profileReference, Definition admittedTarget,
        AnswerSnapshot ordinaryPublicAndReplacementAnswers, string artifactId,
        IReadOnlySet<DefinitionPath>? excludedRetainedPaths = null, CancellationToken cancellationToken = default) =>
        RunAsync(new(Capture(), 0, "download")
        {
            ProfileReference = profileReference, ArtifactId = artifactId,
            ExcludedPaths = Paths(excludedRetainedPaths)
        }, default, default, admittedTarget, ordinaryPublicAndReplacementAnswers, [], null, cancellationToken);

    public Task<ProfileWorkerReply> SealAsync(Guid profileReference, Definition admittedTarget,
        AnswerSnapshot ordinaryPublicAndReplacementAnswers, Guid newSnapshotId,
        IReadOnlyList<ReadOnlyMemory<byte>> passphrases, IReadOnlyList<ReadOnlyMemory<byte>> confirmations,
        IReadOnlySet<DefinitionPath>? excludedRetainedPaths = null, CancellationToken cancellationToken = default) =>
        RunAsync(new(Capture(), 0, "seal")
        {
            ProfileReference = profileReference, SnapshotId = newSnapshotId, ExcludedPaths = Paths(excludedRetainedPaths)
        }, default, default, admittedTarget, ordinaryPublicAndReplacementAnswers,
            passphrases, confirmations, cancellationToken);

    public Task<ProfileWorkerReply> CreateAsync(Definition admittedDefinition, AnswerSnapshot answers,
        ProfileProtectionMode mode, Guid profileId, Guid snapshotId, string definitionSource, string environmentContext,
        IReadOnlyList<ReadOnlyMemory<byte>> passphrases, IReadOnlyList<ReadOnlyMemory<byte>> confirmations,
        CancellationToken cancellationToken = default) =>
        RunAsync(new(Capture(), 0, "create")
        {
            ProfileId = profileId, SnapshotId = snapshotId, Mode = mode.ToString(),
            DefinitionSource = definitionSource, EnvironmentContext = environmentContext
        }, default, default, admittedDefinition, answers, passphrases, confirmations, cancellationToken);

    public Task<ProfileWorkerReply> VerifyRecoveryAsync(ProfileRecoveryReference trustedReference,
        ReadOnlyMemory<byte> exactContainer, int recipientIndex, ReadOnlyMemory<byte> passphraseUtf8,
        CancellationToken cancellationToken = default)
    {
        if (!trustedReference.PortableRecoveryConfigured ||
            recipientIndex < (trustedReference.DeviceRecipientId is null ? 0 : 1))
            throw new ProfileException(ProfileFailureCode.ModeRestricted);
        return RunAsync(new(Capture(), 0, "recover")
        {
            ProfileId = trustedReference.ProfileId, SnapshotId = trustedReference.SnapshotId,
            TrustedDigest = trustedReference.Digest.Value, RecipientIndex = recipientIndex,
            DeviceRecipientId = trustedReference.DeviceRecipientId,
            PortableRecoveryConfigured = trustedReference.PortableRecoveryConfigured
        }, exactContainer, passphraseUtf8, null, null, [], null, cancellationToken);
    }

    /// <summary>Explicit inspectable-mode disclosure into the caller's consented editable answer flow.</summary>
    public async Task<AnswerSnapshot> InspectAsync(Guid profileReference, CancellationToken cancellationToken = default)
    {
        AnswerSnapshot? disclosed = null;
        await RunAsync(new(Capture(), 0, "inspect") { ProfileReference = profileReference }, default, default,
            null, null, [], null, cancellationToken, bytes =>
            {
                disclosed = AnswerSnapshotCodec.ParseJson(bytes, currentAuthority().AnswerRevision);
            }).ConfigureAwait(false);
        return disclosed ?? throw new ProfileException(ProfileFailureCode.ModeRestricted);
    }

    /// <summary>Call directly from consent revocation, workspace replacement and disposal, before awaiting cleanup.</summary>
    public void Retire()
    {
        string retired = _channel;
        _channel = Guid.NewGuid().ToString("N");
        _bridge?.InvokeVoid("retire", retired);
    }

    public void DiscardOutput() => _bridge?.InvokeVoid("discard", _channel);

    private ProfileWorkerAuthority Capture()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ProfileWorkerAuthority authority = currentAuthority();
        if (!authority.ConsentGranted || authority.WorkspaceId == Guid.Empty || authority.ConsentEpoch <= 0)
            throw new ProfileException(ProfileFailureCode.ModeRestricted);
        return authority;
    }

    private async Task<ProfileWorkerReply> RunAsync(ProfileWorkerRequest command, ReadOnlyMemory<byte> payload,
        ReadOnlyMemory<byte> credential, Definition? definition, AnswerSnapshot? answers,
        IReadOnlyList<ReadOnlyMemory<byte>> recipients, IReadOnlyList<ReadOnlyMemory<byte>>? confirmations,
        CancellationToken cancellationToken, Action<ReadOnlyMemory<byte>>? disclose = null,
        Func<ProfileWorkerReply, bool>? historicalAdmission = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Interlocked.CompareExchange(ref _admittedOperation, 1, 0) != 0)
            throw new ProfileException(ProfileFailureCode.Busy);
        string channel = _channel;
        bool opening = command.Operation == "unlock";
        bool adopted = false;
        byte[] data = [], password = [], schema = [], values = [];
        var materials = new List<byte[]>();
        try
        {
            CheckCurrent(command.Authority, channel, cancellationToken);
            if (payload.Length > ProfileContainerPolicy.MaximumContainerBytes)
                throw new ProfileException(ProfileFailureCode.LimitExceeded);
            if (credential.Length > ProfileContainerPolicy.MaximumPassphraseUtf8Bytes)
                throw new ProfileException(ProfileFailureCode.InvalidPassphrase);
            data = payload.ToArray();
            password = credential.ToArray();
            if (command.Operation is "unlock" or "recover")
            {
                int count = ProfileContainerPolicy.Admit(data);
                if (command.RecipientIndex < 0 || command.RecipientIndex >= count)
                    throw new ProfileException(ProfileFailureCode.InvalidContainer);
                ProfileContainerPolicy.ValidatePassphrase(password, creating: false);
            }
            if (command.Operation is "seal" or "create")
            {
                int count = recipients.Count;
                if (count is < 1 or > ProfileContainerPolicy.MaximumRecipients ||
                    confirmations is null || confirmations.Count != count)
                    throw new ProfileException(ProfileFailureCode.InvalidPassphrase);
                for (int index = 0; index < count; index++)
                {
                    ReadOnlyMemory<byte> supplied = recipients[index];
                    ReadOnlyMemory<byte> confirmation = confirmations[index];
                    if (supplied.Length is 0 or > ProfileContainerPolicy.MaximumPassphraseUtf8Bytes ||
                        confirmation.Length is 0 or > ProfileContainerPolicy.MaximumPassphraseUtf8Bytes)
                        throw new ProfileException(ProfileFailureCode.InvalidPassphrase);
                    byte[] snapshot = supplied.ToArray();
                    materials.Add(snapshot);
                    byte[] confirmed = confirmation.ToArray();
                    try
                    {
                        ProfileContainerPolicy.ValidatePassphrase(snapshot, creating: true);
                        if (!CryptographicOperations.FixedTimeEquals(snapshot, confirmed))
                            throw new ProfileException(ProfileFailureCode.InvalidPassphrase);
                    }
                    finally { CryptographicOperations.ZeroMemory(confirmed); }
                }
            }
            if (answers is not null && answers.InputRevision != command.Authority.AnswerRevision)
                throw new ProfileException(ProfileFailureCode.ModeRestricted);
            command = command with { RequestId = checked(++_request) };
            schema = definition?.CanonicalBytes.ToArray() ?? [];
            values = answers is null ? [] : AnswerSnapshotCodec.Serialize(answers);
            if (_bridge is null)
            {
                using IJSInProcessObjectReference module = await javascript.InvokeAsync<IJSInProcessObjectReference>("import",
                    "./js/profile-worker-host.js").ConfigureAwait(false);
                CheckCurrent(command.Authority, channel, cancellationToken);
                _bridge = module.Invoke<IJSInProcessObjectReference>("createBridge");
            }
            CheckCurrent(command.Authority, channel, cancellationToken);
            using CancellationTokenRegistration registration = cancellationToken.Register(() =>
            {
                if (_channel == channel) Retire();
            });
            await _bridge.InvokeVoidAsync("open", channel).ConfigureAwait(false);
            CheckCurrent(command.Authority, channel, cancellationToken);
            string response = await _bridge.InvokeAsync<string>("execute", channel,
                JsonSerializer.Serialize(command, ProfileWorkerJsonContext.Default.ProfileWorkerRequest),
                data, password, schema, values, materials.ToArray()).ConfigureAwait(false);
            CheckCurrent(command.Authority, channel, cancellationToken);
            if (response.Length > ProfileContainerPolicy.MaximumContainerBytes)
                throw new ProfileException(ProfileFailureCode.LimitExceeded);
            ProfileWorkerReply reply = JsonSerializer.Deserialize(response, ProfileWorkerJsonContext.Default.ProfileWorkerReply)
                ?? throw new ProfileException(ProfileFailureCode.InvalidPayload);
            if (reply.Authority != command.Authority || reply.RequestId != command.RequestId)
                throw new ProfileException(ProfileFailureCode.ModeRestricted);
            if (command.Operation == "unlock" && reply.Status == "ok")
            {
                try
                {
                    if (reply.OutputKind is not null || reply.PublicValues is not null ||
                        historicalAdmission is null || !historicalAdmission(reply))
                        throw new ProfileException(ProfileFailureCode.ModeRestricted);
                    CheckCurrent(command.Authority, channel, cancellationToken);
                    command = new(command.Authority, checked(++_request), "adopt")
                    {
                        ProfileReference = reply.ProfileReference
                    };
                    response = await _bridge.InvokeAsync<string>("execute", channel,
                        JsonSerializer.Serialize(command, ProfileWorkerJsonContext.Default.ProfileWorkerRequest),
                        Array.Empty<byte>(), Array.Empty<byte>(), Array.Empty<byte>(), Array.Empty<byte>(),
                        Array.Empty<byte[]>()).ConfigureAwait(false);
                    CheckCurrent(command.Authority, channel, cancellationToken);
                    if (response.Length > ProfileContainerPolicy.MaximumContainerBytes)
                        throw new ProfileException(ProfileFailureCode.LimitExceeded);
                    reply = JsonSerializer.Deserialize(response, ProfileWorkerJsonContext.Default.ProfileWorkerReply)
                        ?? throw new ProfileException(ProfileFailureCode.InvalidPayload);
                    if (reply.Authority != command.Authority || reply.RequestId != command.RequestId ||
                        reply.ProfileReference != command.ProfileReference || reply.Status != "ok")
                        throw new ProfileException(ProfileFailureCode.ModeRestricted);
                    adopted = true;
                }
                catch
                {
                    if (_channel == channel) Retire();
                    throw;
                }
            }
            if (reply.OutputKind is not null)
            {
                if (reply.Status != "ok") throw new ProfileException(ProfileFailureCode.ModeRestricted);
                switch (command.Operation)
                {
                    case "download" when reply.OutputKind == "artifact":
                    case "seal" or "create" when reply.OutputKind == "profile":
                        _bridge.InvokeVoid("download", channel, command.RequestId, reply.OutputKind);
                        break;
                    case "inspect" when reply.OutputKind == "inspect" && disclose is not null:
                        byte[] bytes = _bridge.Invoke<byte[]>("takeDisclosure", channel, command.RequestId);
                        try
                        {
                            CheckCurrent(command.Authority, channel, cancellationToken);
                            disclose(bytes);
                        }
                        finally { CryptographicOperations.ZeroMemory(bytes); }
                        break;
                    default: throw new ProfileException(ProfileFailureCode.ModeRestricted);
                }
            }
            return reply;
        }
        catch (JSException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new ProfileException(ProfileFailureCode.ModeRestricted);
        }
        finally
        {
            try
            {
                if (opening && !adopted && _channel == channel) Retire();
                _bridge?.InvokeVoid("discard", channel);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(data);
                CryptographicOperations.ZeroMemory(password);
                CryptographicOperations.ZeroMemory(schema);
                CryptographicOperations.ZeroMemory(values);
                foreach (byte[] bytes in materials) CryptographicOperations.ZeroMemory(bytes);
                Volatile.Write(ref _admittedOperation, 0);
            }
        }
    }

    private void CheckCurrent(ProfileWorkerAuthority captured, string channel, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_channel != channel || currentAuthority() != captured || !captured.ConsentGranted)
            throw new ProfileException(ProfileFailureCode.ModeRestricted);
    }

    private static string[] Paths(IReadOnlySet<DefinitionPath>? paths) =>
        paths?.Select(path => path.ToPointer()).ToArray() ?? [];

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Retire();
        _bridge?.Dispose();
        _bridge = null;
    }
}
