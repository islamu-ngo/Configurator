namespace Configurator.Core.Tests.Profiles;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Configurator.Core.Definitions;
using Configurator.Core.Profiles;
using Configurator.Core.Tests.Definitions;

public sealed class ProfilePayloadCodecTests
{
    [Test]
    public async Task OwnedPayloadIsIsolatedFromCallerMutationAndOrdinarySerialization()
    {
        byte[] bytes = Payload(ProfileProtectionMode.InspectableEncrypted);
        using UnlockedProfile profile = ProfilePayloadCodec.Admit(bytes);
        CryptographicOperations.ZeroMemory(bytes);

        AnswerSnapshot disclosed = ProfilePayloadCodec.InspectAnswers(profile);

        await Assert.That(disclosed.Values["SERVICE_TOKEN"].GetString()).IsEqualTo("synthetic-retained-value");
        await Assert.That(profile.ProfileId).IsEqualTo(Guid.Parse("26f6c2ad-005b-46c9-8dcf-966c2661b8e2"));
        await Assert.That(JsonSerializer.Serialize(profile).Contains("synthetic-retained-value", StringComparison.Ordinal)).IsFalse();
        await Assert.That(profile.ToString()).IsEqualTo("UnlockedProfile:Redacted");
    }

    [Test]
    public async Task BlindPayloadCannotBeDisclosedAsAnOrdinaryAnswerSnapshot()
    {
        byte[] bytes = Payload(ProfileProtectionMode.ImportUpgradeOnly);
        try
        {
            using UnlockedProfile profile = ProfilePayloadCodec.Admit(bytes);
            ProfileException? failure = null;
            try { _ = ProfilePayloadCodec.InspectAnswers(profile); }
            catch (ProfileException exception) { failure = exception; }
            await Assert.That(failure?.Code).IsEqualTo(ProfileFailureCode.ModeRestricted);
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    [Test]
    public async Task DisposalRetiresFurtherDisclosureAndRemainsIdempotent()
    {
        byte[] bytes = Payload(ProfileProtectionMode.InspectableEncrypted);
        UnlockedProfile profile = ProfilePayloadCodec.Admit(bytes);
        CryptographicOperations.ZeroMemory(bytes);
        profile.Dispose();
        profile.Dispose();

        await Assert.That(() => ProfilePayloadCodec.InspectAnswers(profile)).Throws<ObjectDisposedException>();
    }

    [Test]
    [Arguments("purpose", "other-purpose")]
    [Arguments("profileContractVersion", "0.0.0")]
    [Arguments("mode", "CompareOnly")]
    [Arguments("mode", "UnencryptedTarget")]
    [Arguments("mode", "2")]
    [Arguments("profileId", "00000000-0000-0000-0000-000000000000")]
    public async Task AuthenticatedButInvalidApplicationSemanticsAreNotAdopted(string field, string replacement)
    {
        byte[] bytes = Payload(ProfileProtectionMode.InspectableEncrypted);
        var modified = JsonNode.Parse(bytes)!;
        CryptographicOperations.ZeroMemory(bytes);
        modified[field] = replacement;
        byte[] rejected = Encoding.UTF8.GetBytes(modified.ToJsonString());
        try
        {
            await Assert.That(() => ProfilePayloadCodec.Admit(rejected)).Throws<ProfileException>();
        }
        finally { CryptographicOperations.ZeroMemory(rejected); }
    }

    [Test]
    public async Task DuplicatePayloadMembersRejectWithoutDiagnosticValues()
    {
        byte[] bytes = Payload(ProfileProtectionMode.InspectableEncrypted);
        string text = Encoding.UTF8.GetString(bytes);
        CryptographicOperations.ZeroMemory(bytes);
        byte[] duplicate = Encoding.UTF8.GetBytes(text[..^1] + ",\"mode\":\"ImportUpgradeOnly\"}");
        ProfileException? failure = null;
        try { using var profile = ProfilePayloadCodec.Admit(duplicate); }
        catch (ProfileException exception) { failure = exception; }
        finally { CryptographicOperations.ZeroMemory(duplicate); }

        await Assert.That(failure?.Code).IsEqualTo(ProfileFailureCode.InvalidPayload);
        await Assert.That(failure?.ToString().Contains("synthetic-retained-value", StringComparison.Ordinal)).IsFalse();
    }

    [Test]
    [Arguments("missing")]
    [Arguments("empty-id")]
    [Arguments("short")]
    [Arguments("null-material")]
    [Arguments("extra")]
    public async Task AuthenticatedComparisonKeyRequiresItsExactBoundedPayloadShape(string attack)
    {
        byte[] bytes = Payload(ProfileProtectionMode.ImportUpgradeOnly);
        JsonNode root = JsonNode.Parse(bytes)!;
        CryptographicOperations.ZeroMemory(bytes);
        var key = new JsonObject
        {
            ["keyId"] = "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
            ["material"] = Convert.ToBase64String(new byte[32])
        };
        root["comparisonKey"] = key;
        switch (attack)
        {
            case "missing": root.AsObject().Remove("comparisonKey"); break;
            case "empty-id": key["keyId"] = Guid.Empty; break;
            case "short": key["material"] = Convert.ToBase64String(new byte[31]); break;
            case "null-material": key["material"] = null; break;
            case "extra": key["other"] = 1; break;
            default: throw new InvalidOperationException("Unknown synthetic attack.");
        }
        byte[] encoded = Encoding.UTF8.GetBytes(root.ToJsonString());
        try
        {
            ProfileException? failure = null;
            try { using var profile = ProfilePayloadCodec.Admit(encoded); }
            catch (ProfileException exception) { failure = exception; }
            await Assert.That(failure?.Code).IsEqualTo(ProfileFailureCode.InvalidPayload);
        }
        finally { CryptographicOperations.ZeroMemory(encoded); }
    }

    [Test]
    [Arguments(13, false)]
    [Arguments(14, true)]
    public async Task CreationFloorCountsUnicodeScalarsRatherThanUtf16Units(int asciiCount, bool accepted)
    {
        byte[] input = Encoding.UTF8.GetBytes(new string('a', asciiCount) + "\U0001F512");
        ProfileException? failure = null;
        try { ProfileContainerPolicy.ValidatePassphrase(input, creating: true); }
        catch (ProfileException exception) { failure = exception; }
        await Assert.That(failure is null).IsEqualTo(accepted);
    }

    [Test]
    public async Task MalformedUtf8IsNotSilentlyReplacedBeforeDerivation()
    {
        await Assert.That(() => ProfileContainerPolicy.ValidatePassphrase(new byte[] { 0xff }, creating: false))
            .Throws<ProfileException>();
    }

    [Test]
    public async Task IndependentlyProducedJweMetadataAdmitsTwoRecipientsWithoutAnUnlockMaterial()
    {
        int count = ProfileContainerPolicy.Admit(Encoding.UTF8.GetBytes(IndependentContainer));
        await Assert.That(count).IsEqualTo(2);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task BothStandardJsonShapesAdmitExactlyOneRecipient(bool flattened)
    {
        JsonNode root = SingleRecipient(flattened);
        await Assert.That(ProfileContainerPolicy.Admit(Encoding.UTF8.GetBytes(root.ToJsonString()))).IsEqualTo(1);
    }

    [Test]
    [Arguments("mixed")]
    [Arguments("unknown")]
    [Arguments("low-count")]
    [Arguments("null-key")]
    [Arguments("duplicate-algorithm")]
    public async Task FlattenedShapeCannotBypassTheGeneralRecipientPolicy(string attack)
    {
        JsonNode root = SingleRecipient(flattened: true);
        switch (attack)
        {
            case "mixed": root["recipients"] = new JsonArray(); break;
            case "unknown": root["unprotected"] = new JsonObject(); break;
            case "low-count": root["header"]!["p2c"] = 599999; break;
            case "null-key": root["encrypted_key"] = null; break;
            case "duplicate-algorithm":
                root["protected"] = System.Buffers.Text.Base64Url.EncodeToString(
                    """{"enc":"A256GCM","typ":"cfgvault","alg":"PBES2-HS512+A256KW"}"""u8);
                break;
            default: throw new InvalidOperationException("Unknown synthetic attack.");
        }
        byte[] encoded = Encoding.UTF8.GetBytes(root.ToJsonString());
        await Assert.That(() => ProfileContainerPolicy.Admit(encoded)).Throws<ProfileException>();
    }

    [Test]
    [Arguments("root-duplicate")]
    [Arguments("protected-duplicate")]
    [Arguments("depth")]
    [Arguments("property-count")]
    [Arguments("empty-recipients")]
    [Arguments("container-limit")]
    [Arguments("ciphertext-limit")]
    [Arguments("protected-limit")]
    public async Task AmbiguousOrOversizedEnvelopesRejectAtTheCredentialFreeBoundary(string attack)
    {
        JsonNode root = JsonNode.Parse(IndependentContainer)!;
        byte[]? encoded = null;
        switch (attack)
        {
            case "root-duplicate":
                encoded = Encoding.UTF8.GetBytes(IndependentContainer[..^1] + ",\"iv\":\"3CsPCH1amzvN_HlZ\"}");
                break;
            case "protected-duplicate":
                root["protected"] = System.Buffers.Text.Base64Url.EncodeToString(
                    """{"enc":"A256GCM","typ":"cfgvault","enc":"A256GCM"}"""u8);
                break;
            case "depth":
                root["protected"] = System.Buffers.Text.Base64Url.EncodeToString(
                    Encoding.UTF8.GetBytes("{\"enc\":\"A256GCM\",\"typ\":\"cfgvault\",\"nested\":" +
                        new string('[', 9) + "0" + new string(']', 9) + "}"));
                break;
            case "property-count":
                for (int index = 0; index < 17; index++) root["recipients"]![0]!["header"]!["extra" + index] = 0;
                break;
            case "empty-recipients": root["recipients"] = new JsonArray(); break;
            case "container-limit": encoded = new byte[ProfileContainerPolicy.MaximumContainerBytes + 1]; break;
            case "ciphertext-limit":
                root["ciphertext"] = System.Buffers.Text.Base64Url.EncodeToString(
                    new byte[ProfileContainerPolicy.MaximumPayloadBytes + 1]);
                break;
            case "protected-limit":
                root["protected"] = System.Buffers.Text.Base64Url.EncodeToString(new byte[2049]);
                break;
            default: throw new InvalidOperationException("Unknown synthetic attack.");
        }
        encoded ??= Encoding.UTF8.GetBytes(root.ToJsonString());
        await Assert.That(() => ProfileContainerPolicy.Admit(encoded)).Throws<ProfileException>();
    }

    [Test]
    [Arguments("string-count")]
    [Arguments("fractional-count")]
    [Arguments("low-count")]
    [Arguments("high-count")]
    [Arguments("short-salt")]
    [Arguments("long-salt")]
    [Arguments("algorithm")]
    [Arguments("compression")]
    [Arguments("short-iv")]
    [Arguments("short-tag")]
    [Arguments("cross-header")]
    [Arguments("recipient-overflow")]
    [Arguments("external-aad")]
    public async Task UnsafeMetadataRejectsBeforeAnyUnlockMaterialCanBeConsumed(string attack)
    {
        var root = JsonNode.Parse(IndependentContainer)!;
        JsonNode header = root["recipients"]![0]!["header"]!;
        switch (attack)
        {
            case "string-count": header["p2c"] = "600000"; break;
            case "fractional-count": header["p2c"] = 600000.5; break;
            case "low-count": header["p2c"] = 599999; break;
            case "high-count": header["p2c"] = 600001; break;
            case "short-salt": header["p2s"] = "AA"; break;
            case "long-salt": header["p2s"] = System.Buffers.Text.Base64Url.EncodeToString(new byte[65]); break;
            case "algorithm": header["alg"] = "PBES2-HS256+A128KW"; break;
            case "compression":
                root["protected"] = System.Buffers.Text.Base64Url.EncodeToString(
                    """{"enc":"A256GCM","typ":"cfgvault","zip":"DEF"}"""u8);
                break;
            case "short-iv": root["iv"] = "AA"; break;
            case "short-tag": root["tag"] = "AA"; break;
            case "cross-header": header["enc"] = "A256GCM"; break;
            case "recipient-overflow":
                var recipients = root["recipients"]!.AsArray();
                while (recipients.Count < 9) recipients.Add(recipients[0]!.DeepClone());
                break;
            case "external-aad": root["aad"] = ""; break;
            default: throw new InvalidOperationException("Unknown synthetic attack.");
        }
        byte[] encoded = Encoding.UTF8.GetBytes(root.ToJsonString());
        await Assert.That(() => ProfileContainerPolicy.Admit(encoded)).Throws<ProfileException>();
    }

    [Test]
    [Arguments("protected")]
    [Arguments("iv")]
    [Arguments("tag")]
    [Arguments("ciphertext")]
    [Arguments("encrypted_key")]
    [Arguments("p2s")]
    public async Task NullWireMembersProduceAClassifiedRejection(string field)
    {
        var root = JsonNode.Parse(IndependentContainer)!;
        if (field == "encrypted_key") root["recipients"]![0]![field] = null;
        else if (field == "p2s") root["recipients"]![0]!["header"]![field] = null;
        else root[field] = null;
        byte[] encoded = Encoding.UTF8.GetBytes(root.ToJsonString());
        ProfileException? failure = null;
        try { _ = ProfileContainerPolicy.Admit(encoded); }
        catch (ProfileException exception) { failure = exception; }
        await Assert.That(failure?.Code).IsEqualTo(ProfileFailureCode.InvalidContainer);
    }

    [Test]
    public async Task ConcurrentInspectionAndRetirementReturnCompleteDataOrDisposal()
    {
        byte[] original = Payload(ProfileProtectionMode.InspectableEncrypted);
        var root = JsonNode.Parse(original)!;
        CryptographicOperations.ZeroMemory(original);
        root["answers"]!["values"]!["SERVICE_TOKEN"] = new JsonArray(
            Enumerable.Range(0, 25).Select(_ => (JsonNode?)JsonValue.Create(new string('x', 10000))).ToArray());
        byte[] payload = Encoding.UTF8.GetBytes(root.ToJsonString());
        try
        {
            for (int attempt = 0; attempt < 16; attempt++)
            {
                using UnlockedProfile profile = ProfilePayloadCodec.Admit(payload);
                using var start = new Barrier(2);
                Exception? unexpected = null;
                Task inspect = Task.Run(() =>
                {
                    if (!start.SignalAndWait(TimeSpan.FromSeconds(10))) throw new TimeoutException();
                    try
                    {
                        JsonElement value = ProfilePayloadCodec.InspectAnswers(profile).Values["SERVICE_TOKEN"];
                        if (value.GetArrayLength() != 25 ||
                            value.EnumerateArray().Any(item => item.GetString() != new string('x', 10000)))
                            unexpected = new InvalidDataException("Partial profile disclosure.");
                    }
                    catch (ObjectDisposedException) { }
                    catch (Exception exception) { unexpected = exception; }
                });
                Task retire = Task.Run(() =>
                {
                    if (!start.SignalAndWait(TimeSpan.FromSeconds(10))) throw new TimeoutException();
                    profile.Dispose();
                });
                await Task.WhenAll(inspect, retire).WaitAsync(TimeSpan.FromSeconds(10));
                await Assert.That(unexpected?.GetType().Name).IsNull();
            }
        }
        finally { CryptographicOperations.ZeroMemory(payload); }
    }

    // panva/jose 6.2.17, actual Chromium; independent reverse decryption verified natively.
    private const string IndependentContainer = """
        {"ciphertext":"XbYKFSz_9a2WF0EJGdx0Ah0BsL4KqbS8en3Z_qM","iv":"3CsPCH1amzvN_HlZ","tag":"uUSoTHmyCG_ogxhnxmL2GQ","protected":"eyJlbmMiOiJBMjU2R0NNIiwidHlwIjoiY2ZndmF1bHQifQ","recipients":[{"encrypted_key":"0SK8IGiRcXwcakutdL2JdWWwzaV5XXsUtB1KDebMfVUeIv9cuFLcXQ","header":{"alg":"PBES2-HS512+A256KW","p2c":600000,"p2s":"HwLQjX9RLvJRSM3dmUaG9g"}},{"encrypted_key":"I1K_uVEN-ixCZmLagKF8m7vmveVfiCLzZIaEOZ5oR6IESQ2ATBCZlg","header":{"alg":"PBES2-HS512+A256KW","p2c":600000,"p2s":"VtaNxgfJvssIRvPp7oTJqA"}}]}
        """;

    // Metadata admission only: no claim that changing the wire shape authenticates this fixture.
    private static JsonNode SingleRecipient(bool flattened)
    {
        JsonNode root = JsonNode.Parse(IndependentContainer)!;
        JsonNode recipient = root["recipients"]![0]!.DeepClone();
        if (flattened)
        {
            root.AsObject().Remove("recipients");
            root["header"] = recipient["header"]!.DeepClone();
            root["encrypted_key"] = recipient["encrypted_key"]!.DeepClone();
        }
        else root["recipients"] = new JsonArray(recipient);
        return root;
    }

    internal static byte[] Payload(ProfileProtectionMode mode, ProfileSecretCommitment? comparisonKey = null)
    {
        Definition definition = DefinitionAuthoringTests.Compile();
        using JsonDocument values = JsonDocument.Parse("""{"SERVICE_PORT":8080,"SERVICE_TOKEN":"synthetic-retained-value"}""");
        var answers = new AnswerSnapshot(0, values.RootElement.EnumerateObject().ToDictionary(
            property => property.Name, property => property.Value, StringComparer.Ordinal));
        return ProfilePayloadCodec.Serialize(definition, answers, mode,
            Guid.Parse("26f6c2ad-005b-46c9-8dcf-966c2661b8e2"),
            Guid.Parse("0e92b3fa-6dab-45f6-b8a0-715d34b1a92c"), "synthetic-local", "synthetic-environment", comparisonKey);
    }
}
