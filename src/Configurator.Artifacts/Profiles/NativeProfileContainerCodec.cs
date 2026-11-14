namespace Configurator.Artifacts.Profiles;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Configurator.Core.Profiles;
using Jose;

/// <summary>Native execution owner for the one reviewed JWE profile; no per-primitive cryptography lives here.</summary>
public sealed class NativeProfileContainerCodec : IProfileContainerCodec, IDisposable
{
    private static readonly SemaphoreSlim Work = new(1, 1);
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly IJsonMapper Mapper = new StrictMapper();
    private readonly OsKeyringSecretLease? _deviceRecipient;
    private int _disposed;

    /// <summary>Owns the optional opaque native-recipient lease, placed before explicit portable recipients.</summary>
    public NativeProfileContainerCodec(OsKeyringSecretLease? deviceRecipient = null)
    {
        if (deviceRecipient is not null && deviceRecipient.Purpose != OsKeyringPurpose.NativeRecipient)
            throw new ArgumentException("The key purpose does not permit recipient use.", nameof(deviceRecipient));
        _deviceRecipient = deviceRecipient;
        DeviceRecipientId = deviceRecipient?.KeyId;
    }

    public Guid? DeviceRecipientId { get; }

    public async ValueTask<byte[]> SealAsync(ReadOnlyMemory<byte> payload,
        IReadOnlyList<ReadOnlyMemory<byte>> recipientMaterials,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(recipientMaterials);
        EnsureAlive();
        int portableCount = recipientMaterials.Count;
        int deviceCount = DeviceRecipientId is null ? 0 : 1;
        if (payload.Length is 0 or > ProfileContainerPolicy.MaximumPayloadBytes ||
            portableCount > ProfileContainerPolicy.MaximumRecipients - deviceCount ||
            portableCount + deviceCount < 1)
            throw new ProfileException(ProfileFailureCode.LimitExceeded);
        cancellationToken.ThrowIfCancellationRequested();
        if (!Work.Wait(0, cancellationToken)) throw new ProfileException(ProfileFailureCode.Busy);
        byte[]? ownedPayload = null;
        var materials = new List<byte[]>();
        byte[]? result = null;
        try
        {
            ownedPayload = payload.ToArray();
            for (int index = 0; index < portableCount; index++)
            {
                ReadOnlyMemory<byte> supplied = recipientMaterials[index];
                if (supplied.Length is 0 or > ProfileContainerPolicy.MaximumPassphraseUtf8Bytes)
                    throw new ProfileException(ProfileFailureCode.InvalidPassphrase);
                byte[] material = supplied.ToArray();
                materials.Add(material);
                ProfileContainerPolicy.ValidatePassphrase(material, creating: true);
            }
            string? deviceMaterial = null;
            if (_deviceRecipient is not null)
            {
                byte[] encodedKey = _deviceRecipient.CopyRecipientUtf8();
                try { deviceMaterial = StrictUtf8.GetString(encodedKey); }
                finally { CryptographicOperations.ZeroMemory(encodedKey); }
            }
            result = await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                ProfilePayloadCodec.Validate(ownedPayload);
                var recipients = materials.Select(material => new JweRecipient(
                    JweAlgorithm.PBES2_HS512_A256KW, StrictUtf8.GetString(material),
                    new Dictionary<string, object> { ["p2c"] = ProfileContainerPolicy.Iterations })).ToList();
                if (deviceMaterial is not null)
                    recipients.Insert(0, new JweRecipient(JweAlgorithm.PBES2_HS512_A256KW, deviceMaterial,
                        new Dictionary<string, object>
                        {
                            ["p2c"] = ProfileContainerPolicy.Iterations,
                            ["kid"] = DeviceRecipientId!.Value.ToString("N")
                        }));
                string encoded = JWE.EncryptBytes(ownedPayload, recipients, JweEncryption.A256GCM,
                    extraProtectedHeaders: new Dictionary<string, object> { ["typ"] = "cfgvault" },
                    settings: Settings());
                byte[] container = StrictUtf8.GetBytes(encoded);
                try
                {
                    _ = ProfileContainerPolicy.Admit(container);
                    return container;
                }
                catch
                {
                    CryptographicOperations.ZeroMemory(container);
                    throw;
                }
            }, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            EnsureAlive();
            if (_deviceRecipient is not null)
            {
                byte[] alive = _deviceRecipient.CopyRecipientUtf8();
                CryptographicOperations.ZeroMemory(alive);
            }
            byte[] transferred = result;
            result = null;
            return transferred;
        }
        finally
        {
            if (result is not null) CryptographicOperations.ZeroMemory(result);
            if (ownedPayload is not null) CryptographicOperations.ZeroMemory(ownedPayload);
            foreach (byte[] material in materials) CryptographicOperations.ZeroMemory(material);
            Work.Release();
        }
    }

    public async ValueTask<UnlockedProfile> OpenAsync(ReadOnlyMemory<byte> container, int recipientIndex,
        ReadOnlyMemory<byte> passphraseUtf8, CancellationToken cancellationToken = default)
    {
        EnsureAlive();
        if (container.Length is 0 or > ProfileContainerPolicy.MaximumContainerBytes)
            throw new ProfileException(ProfileFailureCode.LimitExceeded);
        if (passphraseUtf8.Length is 0 or > ProfileContainerPolicy.MaximumPassphraseUtf8Bytes)
            throw new ProfileException(ProfileFailureCode.InvalidPassphrase);
        cancellationToken.ThrowIfCancellationRequested();
        if (!Work.Wait(0, cancellationToken)) throw new ProfileException(ProfileFailureCode.Busy);
        byte[]? ownedContainer = null;
        byte[]? material = null;
        UnlockedProfile? result = null;
        try
        {
            ownedContainer = container.ToArray();
            material = passphraseUtf8.ToArray();
            ProfileContainerPolicy.ValidatePassphrase(material, creating: false);
            result = await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                int count = ProfileContainerPolicy.Admit(ownedContainer);
                if (recipientIndex < 0 || recipientIndex >= count)
                    throw new ProfileException(ProfileFailureCode.InvalidContainer);
                string encoded = StrictUtf8.GetString(ownedContainer);
                var token = JWE.Headers(encoded, Settings());
                token.Recipients.RemoveRange(recipientIndex + 1, token.Recipients.Count - recipientIndex - 1);
                token.Recipients.RemoveRange(0, recipientIndex);
                string selected = token.AsString(Mapper);
                using (JsonDocument original = JsonDocument.Parse(ownedContainer))
                using (JsonDocument filtered = JsonDocument.Parse(selected))
                {
                    if (original.RootElement.GetProperty("protected").GetString() !=
                        filtered.RootElement.GetProperty("protected").GetString())
                        throw new ProfileException(ProfileFailureCode.InvalidContainer);
                }
                byte[] plaintext;
                try
                {
                    plaintext = JWE.Decrypt(selected, StrictUtf8.GetString(material),
                        JweAlgorithm.PBES2_HS512_A256KW, JweEncryption.A256GCM, Settings()).PlaintextBytes;
                }
                catch (Exception exception) when (exception is JoseException or CryptographicException)
                {
                    throw new ProfileException(ProfileFailureCode.AuthenticationFailed);
                }
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return ProfilePayloadCodec.Admit(plaintext);
                }
                finally { CryptographicOperations.ZeroMemory(plaintext); }
            }, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            EnsureAlive();
            UnlockedProfile transferred = result;
            result = null;
            return transferred;
        }
        finally
        {
            result?.Dispose();
            if (ownedContainer is not null) CryptographicOperations.ZeroMemory(ownedContainer);
            if (material is not null) CryptographicOperations.ZeroMemory(material);
            Work.Release();
        }
    }

    /// <summary>Authenticates a portable recipient and binds its payload to the expected opaque store identities.</summary>
    public async ValueTask<UnlockedProfile> OpenStoredAsync(IProfileVaultStore store, ProfileRevisionDescriptor revision,
        int recipientIndex, ReadOnlyMemory<byte> passphraseUtf8, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(revision);
        EnsureAlive();
        if (revision.Kind != ProfileFileKind.Encrypted)
            throw new ProfileException(ProfileFailureCode.InvalidContainer);
        if (passphraseUtf8.Length is 0 or > ProfileContainerPolicy.MaximumPassphraseUtf8Bytes)
            throw new ProfileException(ProfileFailureCode.InvalidPassphrase);
        cancellationToken.ThrowIfCancellationRequested();
        byte[] material = passphraseUtf8.ToArray();
        byte[]? container = null;
        UnlockedProfile? opened = null;
        try
        {
            container = await store.ReadAsync(revision, cancellationToken).ConfigureAwait(false);
            opened = await OpenAsync(container, recipientIndex, material, cancellationToken).ConfigureAwait(false);
            BindIdentity(opened, revision);
            cancellationToken.ThrowIfCancellationRequested();
            EnsureAlive();
            UnlockedProfile result = opened;
            opened = null;
            return result;
        }
        finally
        {
            opened?.Dispose();
            CryptographicOperations.ZeroMemory(material);
            if (container is not null) CryptographicOperations.ZeroMemory(container);
        }
    }

    /// <summary>
    /// Uses the library's unauthenticated selected kid only for NativeRecipient lookup, never creation.
    /// Authentication and expected store identity must both succeed before adoption.
    /// </summary>
    public async ValueTask<UnlockedProfile> OpenNativeAsync(IProfileVaultStore store, ProfileRevisionDescriptor revision,
        int recipientIndex, IOsKeyringProvider keyring, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(revision);
        ArgumentNullException.ThrowIfNull(keyring);
        EnsureAlive();
        if (revision.Kind != ProfileFileKind.Encrypted)
            throw new ProfileException(ProfileFailureCode.InvalidContainer);
        cancellationToken.ThrowIfCancellationRequested();
        byte[] container = await store.ReadAsync(revision, cancellationToken).ConfigureAwait(false);
        UnlockedProfile? opened = null;
        try
        {
            int count = ProfileContainerPolicy.Admit(container);
            if (recipientIndex < 0 || recipientIndex >= count)
                throw new ProfileException(ProfileFailureCode.InvalidContainer);
            var token = JWE.Headers(StrictUtf8.GetString(container), Settings());
            if (!token.Recipients[recipientIndex].Header.TryGetValue("kid", out object? label) ||
                label is not string text || !Guid.TryParseExact(text, "N", out Guid keyId) || keyId == Guid.Empty)
                throw new OsKeyringException(OsKeyringStatus.InvalidRequest);
            using OsKeyringResult key = await keyring.LookupAsync(keyId, OsKeyringPurpose.NativeRecipient,
                cancellationToken).ConfigureAwait(false);
            if (key.Status != OsKeyringStatus.Success) throw new OsKeyringException(key.Status);
            if (key.KeyId != keyId || key.Purpose != OsKeyringPurpose.NativeRecipient ||
                key.Lease is not { Purpose: OsKeyringPurpose.NativeRecipient } lease || lease.KeyId != keyId)
                throw new OsKeyringException(OsKeyringStatus.InvalidValue);
            byte[] material = lease.CopyRecipientUtf8();
            ValueTask<UnlockedProfile> opening;
            try { opening = OpenAsync(container, recipientIndex, material, cancellationToken); }
            finally { CryptographicOperations.ZeroMemory(material); }
            opened = await opening.ConfigureAwait(false);
            BindIdentity(opened, revision);
            cancellationToken.ThrowIfCancellationRequested();
            EnsureAlive();
            UnlockedProfile result = opened;
            opened = null;
            return result;
        }
        finally
        {
            opened?.Dispose();
            CryptographicOperations.ZeroMemory(container);
        }
    }

    private static void BindIdentity(UnlockedProfile profile, ProfileRevisionDescriptor revision)
    {
        if (profile.ProfileId != revision.ProfileId || profile.SnapshotId != revision.SnapshotId)
            throw new ProfileRecoveryException(ProfileRecoveryFailure.IdentityMismatch);
    }

    private void EnsureAlive() => ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);

    public void Dispose()
    {
        Interlocked.Exchange(ref _disposed, 1);
        _deviceRecipient?.Dispose();
    }

    private static JwtSettings Settings() => new JwtSettings().RegisterMapper(Mapper).RegisterJwa(
        JweAlgorithm.PBES2_HS512_A256KW,
        new Pbse2HmacShaKeyManagementWithAesKeyWrap(256, new AesKeyWrapManagement(256),
            ProfileContainerPolicy.Iterations, ProfileContainerPolicy.Iterations));

    private sealed class StrictMapper : IJsonMapper
    {
        private readonly JsonMapper _mapper = new();
        public string Serialize(object value) => _mapper.Serialize(value);
        public T Parse<T>(string json)
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                AllowDuplicateProperties = false,
                MaxDepth = 8
            });
            return _mapper.Parse<T>(json);
        }
    }
}
