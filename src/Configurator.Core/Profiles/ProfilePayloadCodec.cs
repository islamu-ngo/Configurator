namespace Configurator.Core.Profiles;

using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Configurator.Core.Composition;
using Configurator.Core.Definitions;

/// <summary>Application data inside authenticated encryption; this is not another encrypted wire format.</summary>
public static class ProfilePayloadCodec
{
    internal static JsonDocumentOptions DocumentOptions => new()
    {
        AllowDuplicateProperties = false,
        MaxDepth = CompositionLimits.DefaultNestingDepth + 4
    };

    public static byte[] Serialize(Definition definition, AnswerSnapshot answers, ProfileProtectionMode mode,
        Guid profileId, Guid snapshotId, string definitionSource, string environmentContext,
        ProfileSecretCommitment? comparisonKey = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(answers);
        CheckMode(mode);
        if (profileId == Guid.Empty || snapshotId == Guid.Empty ||
            string.IsNullOrEmpty(definitionSource) || definitionSource.Length > 2048 ||
            string.IsNullOrEmpty(environmentContext) || environmentContext.Length > 256)
            throw new ProfileException(ProfileFailureCode.InvalidPayload);
        try
        {
            DefinitionProfile.ValidateScalar(definitionSource);
            DefinitionProfile.ValidateScalar(environmentContext);
        }
        catch (DefinitionException)
        {
            throw new ProfileException(ProfileFailureCode.InvalidPayload);
        }
        byte[] definitionBytes = definition.CanonicalBytes.ToArray();
        byte[] answerBytes = AnswerSnapshotCodec.Serialize(answers);
        var buffer = new ArrayBufferWriter<byte>(ProfileContainerPolicy.MaximumPayloadBytes);
        try
        {
            if (definitionBytes.Length + answerBytes.Length +
                6 * (Encoding.UTF8.GetByteCount(definitionSource) + Encoding.UTF8.GetByteCount(environmentContext)) +
                1024 > ProfileContainerPolicy.MaximumPayloadBytes)
                throw new ProfileException(ProfileFailureCode.LimitExceeded);
            using (var writer = new Utf8JsonWriter(buffer))
            {
                writer.WriteStartObject();
                writer.WriteString("profileContractVersion", ConfiguratorVersion.Current);
                writer.WriteString("purpose", "configuration-profile");
                writer.WriteString("mode", mode.ToString());
                writer.WriteString("profileId", profileId);
                writer.WriteString("snapshotId", snapshotId);
                writer.WritePropertyName("definition");
                writer.WriteRawValue(definitionBytes, skipInputValidation: true);
                writer.WriteString("definitionSource", definitionSource);
                writer.WriteString("environmentContext", environmentContext);
                writer.WritePropertyName("comparisonKey");
                if (comparisonKey is null) writer.WriteNullValue();
                else comparisonKey.WritePayload(writer);
                writer.WritePropertyName("answers");
                writer.WriteRawValue(answerBytes, skipInputValidation: true);
                writer.WriteEndObject();
            }
            return buffer.WrittenSpan.ToArray();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(answerBytes);
            CryptographicOperations.ZeroMemory(definitionBytes);
            buffer.Clear();
        }
    }

    public static void Validate(ReadOnlyMemory<byte> payload) => _ = Read(payload);

    /// <summary>
    /// Consumes the owner, including on invalid requests or failure, and returns an immutable retained subset.
    /// Each requested path retires its entire root field; an empty path retires all roots, including defaults.
    /// Requests must be nonempty and bounded; a named root must exist in the source schema or stored values.
    /// Source identities, definition, provenance, mode and comparison key remain unchanged as source metadata.
    /// The successor requires a fresh snapshot identity when sealed. Callers retain replacement/consent authority
    /// and must dispose their prior scopes and outputs. Owned bytes clear; managed JSON copies are not erasable.
    /// </summary>
    public static UnlockedProfile RetireRetainedFields(UnlockedProfile profile,
        IReadOnlyCollection<DefinitionPath> paths)
    {
        ArgumentNullException.ThrowIfNull(profile);
        byte[] payload = profile.TakePayload();
        ArrayBufferWriter<byte>? buffer = null;
        byte[]? successorPayload = null;
        try
        {
            ArgumentNullException.ThrowIfNull(paths);
            if (paths.Count == 0) throw new ProfileException(ProfileFailureCode.InvalidPayload);
            if (paths.Count > CompositionLimits.DefaultSequenceEntries)
                throw new ProfileException(ProfileFailureCode.LimitExceeded);
            var roots = new HashSet<string>(StringComparer.Ordinal);
            bool retireAll = false;
            int count = 0;
            foreach (DefinitionPath path in paths)
            {
                if (++count > CompositionLimits.DefaultSequenceEntries)
                    throw new ProfileException(ProfileFailureCode.LimitExceeded);
                if (path is null) throw new ProfileException(ProfileFailureCode.InvalidPayload);
                if (path.Segments.Count == 0) retireAll = true;
                else roots.Add(path.Segments[0]);
            }
            if (count == 0) throw new ProfileException(ProfileFailureCode.InvalidPayload);
            using JsonDocument document = JsonDocument.Parse(payload, DocumentOptions);
            JsonElement answers = document.RootElement.GetProperty("answers");
            JsonElement values = answers.GetProperty("values");
            if (roots.Any(root => !profile.Definition.Configuration.Properties.ContainsKey(root) &&
                !values.TryGetProperty(root, out _)))
                throw new ProfileException(ProfileFailureCode.InvalidPayload);
            var omissions = new HashSet<DefinitionPath>();
            if (answers.TryGetProperty("omitted", out JsonElement omitted))
                foreach (JsonElement item in omitted.EnumerateArray())
                    omissions.Add(DefinitionPath.Parse(item.GetString()!));
            if (retireAll)
            {
                roots.UnionWith(profile.Definition.Configuration.Properties.Keys);
                roots.UnionWith(values.EnumerateObject().Select(property => property.Name));
                roots.UnionWith(omissions.Where(path => path.Segments.Count != 0).Select(path => path.Segments[0]));
            }
            omissions.RemoveWhere(path => path.Segments.Count != 0 && roots.Contains(path.Segments[0]));
            foreach (string root in roots)
                omissions.Add(DefinitionPath.FromSegments([root]));
            if (omissions.Count > CompositionLimits.DefaultSequenceEntries)
                throw new ProfileException(ProfileFailureCode.LimitExceeded);

            buffer = new ArrayBufferWriter<byte>(ProfileContainerPolicy.MaximumPayloadBytes);
            using (var writer = new Utf8JsonWriter(buffer))
            {
                writer.WriteStartObject();
                foreach (JsonProperty property in document.RootElement.EnumerateObject())
                {
                    if (!property.NameEquals("answers"))
                    {
                        property.WriteTo(writer);
                        continue;
                    }
                    writer.WritePropertyName("answers");
                    writer.Flush();
                    int answerStart = buffer.WrittenCount;
                    writer.WriteStartObject();
                    writer.WriteString("answerContractVersion", AnswerSnapshotCodec.ContractVersion);
                    writer.WriteStartObject("values");
                    foreach (JsonProperty value in values.EnumerateObject())
                        if (!roots.Contains(value.Name)) value.WriteTo(writer);
                    writer.WriteEndObject();
                    writer.WriteStartArray("omitted");
                    foreach (DefinitionPath path in omissions.OrderBy(path => path.ToPointer(), StringComparer.Ordinal))
                        writer.WriteStringValue(path.ToPointer());
                    writer.WriteEndArray();
                    writer.WriteEndObject();
                    writer.Flush();
                    // Enforce the answer budget without publishing an ordinary retained-value AnswerSnapshot.
                    byte[] canonicalAnswers = DefinitionProfile.CanonicalizeJson(buffer.WrittenMemory[answerStart..]);
                    CryptographicOperations.ZeroMemory(canonicalAnswers);
                }
                writer.WriteEndObject();
            }
            if (buffer.WrittenCount > ProfileContainerPolicy.MaximumPayloadBytes)
                throw new ProfileException(ProfileFailureCode.LimitExceeded);
            successorPayload = buffer.WrittenSpan.ToArray();
            var successor = new UnlockedProfile(successorPayload, profile.Definition, profile.ProfileId,
                profile.SnapshotId, profile.Mode, isSourceSubset: true);
            successorPayload = null;
            return successor;
        }
        catch (DefinitionException) { throw new ProfileException(ProfileFailureCode.LimitExceeded); }
        finally
        {
            if (successorPayload is not null) CryptographicOperations.ZeroMemory(successorPayload);
            buffer?.Clear();
            CryptographicOperations.ZeroMemory(payload);
        }
    }

    /// <summary>Explicit inspectable-mode disclosure; clients must also establish their workspace consent.</summary>
    public static AnswerSnapshot InspectAnswers(UnlockedProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        profile.EnsureAlive();
        if (profile.Mode != ProfileProtectionMode.InspectableEncrypted)
            throw new ProfileException(ProfileFailureCode.ModeRestricted);
        byte[] payload = profile.CopyPayload();
        try
        {
            using JsonDocument document = JsonDocument.Parse(payload, DocumentOptions);
            byte[] encoded = Encoding.UTF8.GetBytes(document.RootElement.GetProperty("answers").GetRawText());
            try
            {
                AnswerSnapshot answers = AnswerSnapshotCodec.ParseJson(encoded);
                profile.EnsureAlive();
                return answers;
            }
            finally { CryptographicOperations.ZeroMemory(encoded); }
        }
        finally { CryptographicOperations.ZeroMemory(payload); }
    }

    /// <summary>Snapshots authenticated bytes before publication; failures retain no caller-owned plaintext.</summary>
    public static UnlockedProfile Admit(ReadOnlyMemory<byte> authenticatedPayload)
    {
        if (authenticatedPayload.Length is 0 or > ProfileContainerPolicy.MaximumPayloadBytes)
            throw new ProfileException(ProfileFailureCode.LimitExceeded);
        byte[] owned = authenticatedPayload.ToArray();
        try
        {
            var state = Read(owned);
            return new UnlockedProfile(owned, state.Definition, state.ProfileId, state.SnapshotId, state.Mode);
        }
        catch
        {
            CryptographicOperations.ZeroMemory(owned);
            throw;
        }
    }

    private static (Definition Definition, Guid ProfileId, Guid SnapshotId, ProfileProtectionMode Mode)
        Read(ReadOnlyMemory<byte> payload)
    {
        if (payload.Length is 0 or > ProfileContainerPolicy.MaximumPayloadBytes)
            throw new ProfileException(ProfileFailureCode.LimitExceeded);
        try
        {
            using JsonDocument document = JsonDocument.Parse(payload, DocumentOptions);
            JsonElement root = document.RootElement;
            string[] fields = ["profileContractVersion", "purpose", "mode", "profileId", "snapshotId",
                "definition", "definitionSource", "environmentContext", "comparisonKey", "answers"];
            if (root.ValueKind != JsonValueKind.Object ||
                root.EnumerateObject().Count() != fields.Length ||
                root.EnumerateObject().Any(property => !fields.Contains(property.Name, StringComparer.Ordinal)) ||
                root.GetProperty("profileContractVersion").GetString() != ConfiguratorVersion.Current ||
                root.GetProperty("purpose").GetString() != "configuration-profile")
                throw new ProfileException(ProfileFailureCode.InvalidPayload);
            ProfileProtectionMode mode = root.GetProperty("mode").GetString() switch
            {
                nameof(ProfileProtectionMode.ImportUpgradeOnly) => ProfileProtectionMode.ImportUpgradeOnly,
                nameof(ProfileProtectionMode.InspectableEncrypted) => ProfileProtectionMode.InspectableEncrypted,
                _ => throw new ProfileException(ProfileFailureCode.InvalidPayload)
            };
            Guid profileId = root.GetProperty("profileId").GetGuid();
            Guid snapshotId = root.GetProperty("snapshotId").GetGuid();
            if (profileId == Guid.Empty || snapshotId == Guid.Empty ||
                root.GetProperty("definitionSource").GetString() is not { Length: > 0 and <= 2048 } ||
                root.GetProperty("environmentContext").GetString() is not { Length: > 0 and <= 256 })
                throw new ProfileException(ProfileFailureCode.InvalidPayload);
            using ProfileSecretCommitment? comparisonKey = ProfileSecretCommitment.ReadPayload(
                root.GetProperty("comparisonKey"));
            byte[] definitionBytes = Encoding.UTF8.GetBytes(root.GetProperty("definition").GetRawText());
            byte[] answerBytes = Encoding.UTF8.GetBytes(root.GetProperty("answers").GetRawText());
            try
            {
                Definition definition = DefinitionCodec.ParseJson(definitionBytes);
                _ = AnswerSnapshotCodec.ParseJson(answerBytes);
                return (definition, profileId, snapshotId, mode);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(definitionBytes);
                CryptographicOperations.ZeroMemory(answerBytes);
            }
        }
        catch (ProfileException) { throw; }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or KeyNotFoundException
            or ArgumentException or FormatException or OverflowException or DefinitionException)
        {
            throw new ProfileException(ProfileFailureCode.InvalidPayload);
        }
    }

    private static void CheckMode(ProfileProtectionMode mode)
    {
        if (mode is not (ProfileProtectionMode.ImportUpgradeOnly or ProfileProtectionMode.InspectableEncrypted))
            throw new ProfileException(ProfileFailureCode.InvalidPayload);
    }
}
