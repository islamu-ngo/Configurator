namespace Configurator.Browser.Worker.Profiles;

using System.Security.Cryptography;
using Configurator.Browser.Profiles;
using Configurator.Core;
using Configurator.Core.Definitions;
using Configurator.Core.Profiles;

/// <summary>Owns admitted transient input copies for one command, never a second editable answer tree.</summary>
public sealed class ProfileWorkerInput : IDisposable
{
    public ProfileWorkerInput(ReadOnlyMemory<byte> payload = default, ReadOnlyMemory<byte> credential = default,
        ReadOnlyMemory<byte> definition = default, ReadOnlyMemory<byte> answers = default,
        IReadOnlyList<ReadOnlyMemory<byte>>? recipients = null)
    {
        if (payload.Length > ProfileContainerPolicy.MaximumContainerBytes ||
            credential.Length > ProfileContainerPolicy.MaximumPassphraseUtf8Bytes ||
            definition.Length > ProfileContainerPolicy.MaximumPayloadBytes ||
            answers.Length > ProfileContainerPolicy.MaximumPayloadBytes ||
            recipients?.Count > ProfileContainerPolicy.MaximumRecipients ||
            recipients?.Any(bytes => bytes.Length > ProfileContainerPolicy.MaximumPassphraseUtf8Bytes) == true)
            throw new ProfileException(ProfileFailureCode.LimitExceeded);
        Payload = payload.ToArray();
        Credential = credential.ToArray();
        Definition = definition.ToArray();
        Answers = answers.ToArray();
        Recipients = recipients?.Select(bytes => bytes.ToArray()).ToArray() ?? [];
    }
    public byte[] Payload { get; }
    public byte[] Credential { get; }
    public byte[] Definition { get; }
    public byte[] Answers { get; }
    public IReadOnlyList<byte[]> Recipients { get; }
    public void Dispose()
    {
        CryptographicOperations.ZeroMemory(Payload);
        CryptographicOperations.ZeroMemory(Credential);
        CryptographicOperations.ZeroMemory(Definition);
        CryptographicOperations.ZeroMemory(Answers);
        foreach (byte[] bytes in Recipients) CryptographicOperations.ZeroMemory(bytes);
    }
    public override string ToString() => "ProfileWorkerInput:Restricted";
}

/// <summary>Only explicit download or inspect operations may attach owned restricted bytes.</summary>
public sealed class ProfileWorkerResult(ProfileWorkerReply reply, byte[]? output = null) : IDisposable
{
    private byte[]? _output = output;
    public ProfileWorkerReply Reply { get; } = reply;
    public ReadOnlyMemory<byte> Output => _output ?? ReadOnlyMemory<byte>.Empty;
    public void Dispose()
    {
        byte[]? bytes = Interlocked.Exchange(ref _output, null);
        if (bytes is not null) CryptographicOperations.ZeroMemory(bytes);
    }
    public override string ToString() => nameof(ProfileWorkerResult);
}

/// <summary>Dedicated worker custody. Caller-supplied authority is fenced again by the owning Browser client.</summary>
public sealed class ProfileWorkerSession(IProfileContainerCodec codec) : IDisposable
{
    private UnlockedProfile? _profile;
    private Guid _reference;
    private bool _adopted;
    private ProfileWorkerAuthority? _authority;
    private long _request;
    private int _active;
    private int _disposed;
    private readonly HashSet<DefinitionPath> _excluded = [];

    public async ValueTask<ProfileWorkerResult> ExecuteAsync(ProfileWorkerRequest request, ProfileWorkerInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(input);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        if (Interlocked.CompareExchange(ref _active, 1, 0) != 0)
            throw new ProfileException(ProfileFailureCode.Busy);
        byte[]? output = null;
        try
        {
            Admit(request);
            cancellationToken.ThrowIfCancellationRequested();
            ProfileWorkerReply reply = new(request.Authority, request.RequestId, "ok");
            switch (request.Operation)
            {
                case "unlock":
                {
                    if (_profile is not null) throw new ProfileException(ProfileFailureCode.ModeRestricted);
                    UnlockedProfile opened = await codec.OpenAsync(input.Payload, request.RecipientIndex,
                        input.Credential, cancellationToken).ConfigureAwait(false);
                    try
                    {
                        EnsureAlive(cancellationToken);
                        _reference = Guid.NewGuid();
                        reply = Describe(reply, opened);
                        _profile = opened;
                    }
                    catch { opened.Dispose(); throw; }
                    break;
                }
                case "adopt":
                {
                    if (_adopted || _profile is null || request.ProfileReference == Guid.Empty ||
                        request.ProfileReference != _reference)
                        throw new ProfileException(ProfileFailureCode.ModeRestricted);
                    // The real catalogue checks safe historical identity before the client sends this command.
                    CompareBaselineSnapshot baseline = CompareBaselineCodec.Create(_profile);
                    _adopted = true;
                    reply = Describe(reply, _profile) with { PublicValues = baseline.PublicValues };
                    break;
                }
                case "plan":
                {
                    UnlockedProfile profile = RequireProfile(request);
                    Definition target = DefinitionCodec.ParseJson(input.Definition);
                    ProfileUpgradePlan plan = ProfileUpgradePlanner.Plan(profile, target);
                    reply = Describe(reply, profile) with
                    {
                        Fields = plan.Fields.Select(field => new ProfileWorkerField(field.FieldPath,
                            field.Eligibility.ToString(), field.Comparison.ToString())).ToArray()
                    };
                    break;
                }
                case "inspect":
                {
                    UnlockedProfile profile = RequireProfile(request);
                    output = AnswerSnapshotCodec.Serialize(ProfilePayloadCodec.InspectAnswers(profile));
                    reply = Describe(reply, profile) with { OutputKind = "inspect" };
                    break;
                }
                case "retire-field":
                {
                    _ = RequireProfile(request);
                    if (request.ExcludedPaths.Length != 1)
                        throw new ProfileException(ProfileFailureCode.InvalidPayload);
                    RetireFields(request.ExcludedPaths);
                    reply = Describe(reply, RequireProfile(request));
                    break;
                }
                case "download":
                case "seal":
                {
                    _ = RequireProfile(request);
                    RetireFields(request.ExcludedPaths);
                    UnlockedProfile profile = RequireProfile(request);
                    Definition target = DefinitionCodec.ParseJson(input.Definition);
                    AnswerSnapshot answers = AnswerSnapshotCodec.ParseJson(input.Answers, request.Authority.AnswerRevision);
                    using var scope = new ProfileCompositionInput(profile, target, answers, _excluded);
                    reply = Describe(reply, profile);
                    if (request.Operation == "download")
                    {
                        ProfileArtifactResult prepared = ProfileArtifactComposer.Compose(scope,
                            request.ArtifactId ?? throw new ProfileException(ProfileFailureCode.InvalidPayload),
                            cancellationToken);
                        using ProfileArtifact? artifact = prepared.Artifact;
                        if (artifact is null)
                        {
                            reply = reply with { Status = "not-ready", Codes = prepared.Diagnostics.Select(d => d.Code).Distinct().ToArray() };
                            break;
                        }
                        await artifact.WriteToBrowserDownloadAsync((bytes, token) =>
                        {
                            EnsureAlive(token);
                            output = bytes.ToArray();
                            return ValueTask.CompletedTask;
                        }, cancellationToken).ConfigureAwait(false);
                        reply = reply with { OutputKind = "artifact" };
                    }
                    else
                    {
                        using ProfileSealedSnapshot sealedProfile = await ProfileArtifactComposer.SealAsync(scope,
                            codec, request.SnapshotId, Materials(input), cancellationToken: cancellationToken).ConfigureAwait(false);
                        output = sealedProfile.CopyContainer();
                        reply = reply with
                        {
                            SnapshotId = sealedProfile.Reference.SnapshotId, OutputKind = "profile",
                            CiphertextDigest = sealedProfile.Reference.Digest.Value
                        };
                    }
                    break;
                }
                case "create":
                {
                    Definition definition = DefinitionCodec.ParseJson(input.Definition);
                    AnswerSnapshot answers = AnswerSnapshotCodec.ParseJson(input.Answers, request.Authority.AnswerRevision);
                    if (!DefinitionValidator.Validate(definition, answers, cancellationToken: cancellationToken).IsReady)
                        throw new ProfileException(ProfileFailureCode.InvalidPayload);
                    ProfileProtectionMode mode = request.Mode switch
                    {
                        nameof(ProfileProtectionMode.ImportUpgradeOnly) => ProfileProtectionMode.ImportUpgradeOnly,
                        nameof(ProfileProtectionMode.InspectableEncrypted) => ProfileProtectionMode.InspectableEncrypted,
                        _ => throw new ProfileException(ProfileFailureCode.ModeRestricted)
                    };
                    byte[] payload = ProfilePayloadCodec.Serialize(definition, answers, mode, request.ProfileId,
                        request.SnapshotId, request.DefinitionSource ?? "", request.EnvironmentContext ?? "");
                    try { output = await codec.SealAsync(payload, Materials(input), cancellationToken).ConfigureAwait(false); }
                    finally { CryptographicOperations.ZeroMemory(payload); }
                    reply = reply with
                    {
                        ProfileId = request.ProfileId, SnapshotId = request.SnapshotId, Mode = request.Mode,
                        FamilyId = definition.FamilyId, DefinitionRevision = definition.Revision,
                        DefinitionDigest = definition.Digest.Value, OutputKind = "profile",
                        CiphertextDigest = ArtifactDigest.Compute(output).Value
                    };
                    break;
                }
                case "recover":
                {
                    if (!request.PortableRecoveryConfigured ||
                        request.RecipientIndex < (request.DeviceRecipientId is null ? 0 : 1))
                        throw new ProfileRecoveryException(ProfileRecoveryFailure.NotPortableRecipient);
                    if (request.ProfileId == Guid.Empty || request.SnapshotId == Guid.Empty ||
                        !ArtifactDigest.TryParse(request.TrustedDigest, out ArtifactDigest expected) ||
                        ArtifactDigest.Compute(input.Payload) != expected)
                        throw new ProfileRecoveryException(ProfileRecoveryFailure.CiphertextMismatch);
                    using UnlockedProfile recovered = await codec.OpenAsync(input.Payload, request.RecipientIndex,
                        input.Credential, cancellationToken).ConfigureAwait(false);
                    if (recovered.ProfileId != request.ProfileId || recovered.SnapshotId != request.SnapshotId)
                        throw new ProfileRecoveryException(ProfileRecoveryFailure.IdentityMismatch);
                    reply = reply with
                    {
                        ProfileId = recovered.ProfileId, SnapshotId = recovered.SnapshotId,
                        CiphertextDigest = expected.Value, Status = "verified-for-snapshot"
                    };
                    break;
                }
                default: throw new ProfileException(ProfileFailureCode.InvalidPayload);
            }
            EnsureAlive(cancellationToken);
            var result = new ProfileWorkerResult(reply, output);
            output = null;
            return result;
        }
        finally
        {
            if (output is not null) CryptographicOperations.ZeroMemory(output);
            if (Volatile.Read(ref _disposed) != 0) Interlocked.Exchange(ref _profile, null)?.Dispose();
            Volatile.Write(ref _active, 0);
        }
    }

    private void Admit(ProfileWorkerRequest request)
    {
        ProfileWorkerAuthority current = request.Authority;
        if (!current.ConsentGranted || current.WorkspaceId == Guid.Empty || current.ConsentEpoch <= 0 ||
            current.Generation < 0 || current.AnswerRevision < 0 || request.RequestId <= _request ||
            request.ExcludedPaths.Length > 4096 || request.ExcludedPaths.Any(path => path is null || path.Length > 4096) ||
            codec.DeviceRecipientId is not null)
            throw new ProfileException(ProfileFailureCode.ModeRestricted);
        if (_authority is { } prior && (current.WorkspaceId != prior.WorkspaceId ||
            current.ConsentEpoch != prior.ConsentEpoch || current.Generation < prior.Generation ||
            current.AnswerRevision < prior.AnswerRevision))
            throw new ProfileException(ProfileFailureCode.ModeRestricted);
        _authority = current;
        _request = request.RequestId;
    }

    private UnlockedProfile RequireProfile(ProfileWorkerRequest request) =>
        _adopted && _profile is { } profile && request.ProfileReference != Guid.Empty && request.ProfileReference == _reference
            ? profile : throw new ProfileException(ProfileFailureCode.ModeRestricted);

    private void RetireFields(string[] paths)
    {
        DefinitionPath[] retiring = paths.Select(DefinitionPath.Parse)
            .Select(path => path.Segments.Count == 0 ? path : DefinitionPath.FromSegments([path.Segments[0]]))
            .Where(path => !_excluded.Contains(path)).Distinct().ToArray();
        if (retiring.Length == 0) return;
        UnlockedProfile predecessor = _profile ?? throw new ProfileException(ProfileFailureCode.ModeRestricted);
        _profile = null;
        _profile = ProfilePayloadCodec.RetireRetainedFields(predecessor, retiring);
        _excluded.UnionWith(retiring);
    }

    private ProfileWorkerReply Describe(ProfileWorkerReply reply, UnlockedProfile profile) => reply with
    {
        ProfileReference = _reference, ProfileId = profile.ProfileId, SnapshotId = profile.SnapshotId,
        Mode = profile.Mode.ToString(), FamilyId = profile.FamilyId, DefinitionRevision = profile.DefinitionRevision,
        DefinitionDigest = profile.DefinitionDigest.Value,
        RetainedPaths = ProfileUpgradePlanner.RetainedFieldPaths(profile)
            .Where(path => !_excluded.Contains(path))
            .Select(path => path.ToPointer()).ToArray()
    };

    private static ReadOnlyMemory<byte>[] Materials(ProfileWorkerInput input) =>
        input.Recipients.Select(bytes => (ReadOnlyMemory<byte>)bytes).ToArray();

    private void EnsureAlive(CancellationToken token)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        token.ThrowIfCancellationRequested();
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        Interlocked.Exchange(ref _profile, null)?.Dispose();
        _reference = Guid.Empty;
    }
}
