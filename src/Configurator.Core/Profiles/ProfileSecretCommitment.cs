namespace Configurator.Core.Profiles;

using System.Buffers;
using System.Security.Cryptography;
using System.Text.Json;
using Configurator.Core.Composition;
using Configurator.Core.Definitions;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Macs;
using Org.BouncyCastle.Crypto.Parameters;

/// <summary>Owns an independently supplied comparison key, never a container key or an exportable property.</summary>
public sealed class ProfileSecretCommitment : IDisposable
{
    private byte[]? _key;
    private readonly object _access = new();

    public ProfileSecretCommitment(Guid keyId, ReadOnlySpan<byte> key)
    {
        if (keyId == Guid.Empty || key.Length != 32)
            throw new ProfileException(ProfileFailureCode.InvalidPayload);
        KeyId = keyId;
        _key = key.ToArray();
    }

    public Guid KeyId { get; }

    internal void WritePayload(Utf8JsonWriter writer)
    {
        lock (_access)
        {
            byte[] key = _key ?? throw new ObjectDisposedException(nameof(ProfileSecretCommitment));
            writer.WriteStartObject();
            writer.WriteString("keyId", KeyId);
            writer.WriteBase64String("material", key);
            writer.WriteEndObject();
        }
    }

    internal static ProfileSecretCommitment? ReadPayload(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Null) return null;
        if (value.ValueKind != JsonValueKind.Object || value.EnumerateObject().Count() != 2 ||
            value.EnumerateObject().Any(property => property.Name is not ("keyId" or "material")) ||
            value.GetProperty("material").GetString() is not { Length: 44 } text)
            throw new ProfileException(ProfileFailureCode.InvalidPayload);
        Span<byte> material = stackalloc byte[32];
        try
        {
            if (!Convert.TryFromBase64String(text, material, out int written) || written != material.Length ||
                Convert.ToBase64String(material) != text)
                throw new ProfileException(ProfileFailureCode.InvalidPayload);
            return new ProfileSecretCommitment(value.GetProperty("keyId").GetGuid(), material);
        }
        finally { CryptographicOperations.ZeroMemory(material); }
    }

    internal string Compute(Guid profileId, Guid familyId, string environmentContext, string? field,
        JsonElement value = default)
    {
        if (profileId == Guid.Empty || familyId == Guid.Empty ||
            environmentContext.Length is 0 or > 256 || field?.Length > 4096)
            throw new ProfileException(ProfileFailureCode.InvalidPayload);
        DefinitionProfile.ValidateScalar(environmentContext);
        var framed = new ArrayBufferWriter<byte>();
        byte[]? canonical = null;
        try
        {
            if (field is not null)
                canonical = DefinitionJson.Canonicalize(value, keyPolicy: CompositionKeyPolicy.Exact);
            using (var writer = new Utf8JsonWriter(framed))
            {
                writer.WriteStartArray();
                writer.WriteStringValue(field is null ? "cfgcompare-context:0.0.1" : "cfgcompare-value:0.0.1");
                writer.WriteStringValue(KeyId);
                writer.WriteStringValue(profileId);
                writer.WriteStringValue(familyId);
                writer.WriteStringValue(environmentContext);
                if (field is not null)
                {
                    writer.WriteStringValue(field);
                    writer.WriteRawValue(canonical!, skipInputValidation: true);
                }
                writer.WriteEndArray();
            }
            lock (_access)
            {
                byte[] key = _key ?? throw new ObjectDisposedException(nameof(ProfileSecretCommitment));
                var mac = new HMac(new Sha256Digest());
                mac.Init(new KeyParameter(key));
                mac.BlockUpdate(framed.WrittenSpan);
                byte[] output = new byte[32];
                mac.DoFinal(output);
                try { return Convert.ToHexStringLower(output); }
                finally { CryptographicOperations.ZeroMemory(output); }
            }
        }
        finally
        {
            if (canonical is not null) CryptographicOperations.ZeroMemory(canonical);
            framed.Clear();
        }
    }

    public void Dispose()
    {
        lock (_access)
        {
            if (_key is null) return;
            CryptographicOperations.ZeroMemory(_key);
            _key = null;
        }
    }

    public override string ToString() => "ProfileSecretCommitment:Redacted";
}
