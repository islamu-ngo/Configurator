namespace Configurator.Core.Tests.Profiles;

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Configurator.Core.Definitions;
using Configurator.Core.Definitions.Authoring;
using Configurator.Core.Profiles;

public sealed class CompareBaselineCodecTests
{
    private static readonly Guid ProfileId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid SnapshotId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly Guid KeyId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private const string Context = "synthetic-environment";

    [Test]
    [Arguments("synthetic-secret-sentinel")]
    [Arguments("synthetic-default-sentinel")]
    [Arguments("synthetic-ancestor-sentinel")]
    [Arguments("synthetic-dynamic-key-sentinel")]
    [Arguments("synthetic-sequence-sentinel")]
    [Arguments("synthetic-variant-sentinel")]
    public async Task SerializedComparisonOmitsProtectedValuesDefaultsAndPrivateKeys(string sentinel)
    {
        CompareBaselineSnapshot snapshot = Create();
        string encoded = Encoding.UTF8.GetString(CompareBaselineCodec.Serialize(snapshot));
        await Assert.That(encoded.Contains(sentinel, StringComparison.Ordinal)).IsFalse();
        await Assert.That(snapshot.PublicValues.GetProperty("public").GetString()).IsEqualTo("visible");
    }

    [Test]
    public async Task PublicDescendantsCannotOverrideASensitiveAncestor()
    {
        CompareBaselineSnapshot snapshot = Create(rootSensitivity: "sensitive");
        await Assert.That(snapshot.PublicValues.EnumerateObject().Any()).IsFalse();
        await Assert.That(snapshot.Structure.TryGetProperty("properties", out _)).IsFalse();
    }

    [Test]
    public async Task PublicNestedValuesRemainAvailableWithoutTheirProtectedSiblings()
    {
        CompareBaselineSnapshot snapshot = Create();
        JsonElement nested = snapshot.PublicValues.GetProperty("nested");
        await Assert.That(nested.GetProperty("visible").GetString()).IsEqualTo("child");
        await Assert.That(nested.TryGetProperty("token", out _)).IsFalse();
        await Assert.That(snapshot.PublicValues.GetProperty("map").EnumerateObject().Any()).IsFalse();
        await Assert.That(snapshot.PublicValues.TryGetProperty("sequence", out _)).IsFalse();
    }

    [Test]
    public async Task CommitmentMatchesAnIndependentOpenSslFramingVectorAndNeverExportsItsKey()
    {
        using var key = Key();
        CompareBaselineSnapshot snapshot = Create(key: key);
        await Assert.That(snapshot.Commitments["/token"])
            .IsEqualTo("638b303e0aac94c45fec10cb8276eadfc37be696e5b916437e13dd87c13cb1c6");
        string encoded = Encoding.UTF8.GetString(CompareBaselineCodec.Serialize(snapshot));
        await Assert.That(encoded.Contains(Context, StringComparison.Ordinal)).IsFalse();
        await Assert.That(encoded.Contains(Convert.ToBase64String(Enumerable.Range(0, 32).Select(value => (byte)value).ToArray()),
            StringComparison.Ordinal)).IsFalse();
    }

    [Test]
    public async Task EqualityRequiresTheHeldKeyAndMatchingProfileFamilyEnvironmentAndField()
    {
        using var key = Key();
        using var wrongKey = new ProfileSecretCommitment(KeyId, new byte[32]);
        CompareBaselineSnapshot first = Create(key: key);
        CompareBaselineSnapshot unchanged = Create(key: key);
        CompareBaselineSnapshot changed = Create(key: key, token: "synthetic-changed-sentinel");
        await Assert.That(CompareBaselineCodec.CompareProtected(first, unchanged, "/token", Context, key))
            .IsEqualTo(ProfileComparisonResult.Unchanged);
        await Assert.That(CompareBaselineCodec.CompareProtected(first, changed, "/token", Context, key))
            .IsEqualTo(ProfileComparisonResult.Changed);
        foreach (ProfileComparisonResult result in new[]
        {
            CompareBaselineCodec.CompareProtected(first, unchanged, "/token", Context, null),
            CompareBaselineCodec.CompareProtected(first, unchanged, "/token", Context, wrongKey),
            CompareBaselineCodec.CompareProtected(first, unchanged, "/token", "other-environment", key),
            CompareBaselineCodec.CompareProtected(first, unchanged, "/renamed-token", Context, key),
            CompareBaselineCodec.CompareProtected(first, Create(key: key, profileId: SnapshotId), "/token", Context, key),
            CompareBaselineCodec.CompareProtected(first, Create(key: key,
                familyId: Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee")), "/token", Context, key)
        }) await Assert.That(result).IsEqualTo(ProfileComparisonResult.Unavailable);
    }

    [Test]
    public async Task DisposedComparisonKeyCannotAuthorizeANewEqualityClaim()
    {
        var key = Key();
        CompareBaselineSnapshot first = Create(key: key);
        key.Dispose();
        await Assert.That(() => CompareBaselineCodec.CompareProtected(first, first, "/token", Context, key))
            .Throws<ObjectDisposedException>();
    }

    [Test]
    public async Task IndependentlyProtectedComparisonKeyCanProjectABlindProfileWithoutDisclosure()
    {
        byte[] payload;
        using (var key = Key())
            payload = ProfilePayloadCodecTests.Payload(ProfileProtectionMode.ImportUpgradeOnly, key);
        using UnlockedProfile profile = ProfilePayloadCodec.Admit(payload);
        System.Security.Cryptography.CryptographicOperations.ZeroMemory(payload);
        CompareBaselineSnapshot comparison = CompareBaselineCodec.Create(profile);
        await Assert.That(comparison.KeyId).IsEqualTo(KeyId);
        await Assert.That(comparison.Commitments.ContainsKey("/SERVICE_TOKEN")).IsTrue();
        await Assert.That(comparison.PublicValues.TryGetProperty("SERVICE_TOKEN", out _)).IsFalse();
        await Assert.That(Encoding.UTF8.GetString(CompareBaselineCodec.Serialize(comparison))
            .Contains("synthetic-retained-value", StringComparison.Ordinal)).IsFalse();
        await Assert.That(() => ProfilePayloadCodec.InspectAnswers(profile)).Throws<ProfileException>();
        profile.Dispose();
        await Assert.That(() => CompareBaselineCodec.Create(profile)).Throws<ObjectDisposedException>();
    }

    [Test]
    public async Task BoundedRoundTripRejectsDuplicateAndUnexpectedWireMembers()
    {
        CompareBaselineSnapshot first = Create();
        byte[] encoded = CompareBaselineCodec.Serialize(first);
        CompareBaselineSnapshot parsed = CompareBaselineCodec.ParseJson(encoded);
        Array.Fill(encoded, (byte)0);
        await Assert.That(parsed.PublicValues.GetProperty("public").GetString()).IsEqualTo("visible");
        string text = Encoding.UTF8.GetString(CompareBaselineCodec.Serialize(first));
        foreach (string added in new[] { "\"answers\":{}", "\"purpose\":\"configuration-comparison\"" })
        {
            byte[] invalid = Encoding.UTF8.GetBytes(text[..^1] + "," + added + "}");
            await Assert.That(() => CompareBaselineCodec.ParseJson(invalid)).Throws<ProfileException>();
        }
        await Assert.That(() => CompareBaselineCodec.ParseJson(new byte[ProfileContainerPolicy.MaximumPayloadBytes + 1]))
            .Throws<ProfileException>();
    }

    [Test]
    [Arguments("protected-value")]
    [Arguments("dynamic-key")]
    [Arguments("structure-default")]
    [Arguments("protected-parent")]
    public async Task ParsedBaselinesCannotReexportProtectedOrUndeclaredContent(string attack)
    {
        JsonNode root = JsonNode.Parse(CompareBaselineCodec.Serialize(Create()))!;
        switch (attack)
        {
            case "protected-value": root["values"]!["token"] = "synthetic-injected-sentinel"; break;
            case "dynamic-key": root["values"]!["map"]!["synthetic-private-key"] = "synthetic-injected-sentinel"; break;
            case "structure-default": root["structure"]!["default"] = "synthetic-injected-sentinel"; break;
            case "protected-parent":
                root["values"]!["privateGroup"] = new JsonObject { ["publicLeak"] = "synthetic-injected-sentinel" };
                break;
            default: throw new InvalidOperationException("Unknown synthetic attack.");
        }
        byte[] encoded = Encoding.UTF8.GetBytes(root.ToJsonString());
        ProfileException? failure = null;
        try { _ = CompareBaselineCodec.ParseJson(encoded); }
        catch (ProfileException exception) { failure = exception; }
        await Assert.That(failure?.Code).IsEqualTo(ProfileFailureCode.InvalidPayload);
    }

    private static ProfileSecretCommitment Key() =>
        new(KeyId, Enumerable.Range(0, 32).Select(value => (byte)value).ToArray());

    private static CompareBaselineSnapshot Create(ProfileSecretCommitment? key = null, string rootSensitivity = "public",
        string token = "synthetic-secret-sentinel", Guid? profileId = null, Guid? familyId = null)
    {
        JsonNode source = JsonNode.Parse(Source)!;
        source["configuration"]!["x-configurator"]!["sensitivity"] = rootSensitivity;
        if (rootSensitivity != "public")
        {
            source["configuration"]!["properties"]!.AsObject().Remove("variant");
            source["configuration"]!["properties"]!["public"]!.AsObject().Remove("default");
        }
        if (familyId.HasValue) source["identity"]!["familyId"] = familyId.Value;
        Definition definition = DefinitionAuthoringCompiler.Compile(
            DefinitionAuthoringCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString())));
        using JsonDocument document = JsonDocument.Parse("""
            {"public":"visible","token":"synthetic-secret-sentinel",
             "nested":{"visible":"child","token":"synthetic-secret-sentinel"},
             "privateGroup":{"publicLeak":"synthetic-ancestor-sentinel"},
             "map":{"synthetic-dynamic-key-sentinel":"synthetic-secret-sentinel"},
             "sequence":[{"visible":"row","token":"synthetic-sequence-sentinel"}],
             "variant":{"kind":"a","token":"synthetic-variant-sentinel"}}
            """);
        var values = document.RootElement.EnumerateObject().ToDictionary(property => property.Name,
            property => property.Value, StringComparer.Ordinal);
        values["token"] = JsonSerializer.SerializeToElement(token);
        return CompareBaselineCodec.Create(definition, new AnswerSnapshot(0, values),
            profileId ?? ProfileId, SnapshotId, Context, key);
    }

    private const string Source = """
        {"$schema":"urn:islamu:configurator:authoring:v0.0.1",
         "identity":{"familyId":"d678ac1c-a240-4429-b932-7ef4638471ea","revision":1},
         "project":{"id":"synthetic.compare","name":"Synthetic comparison"},"applicationVersions":["1.0"],
         "configuration":{"type":"object","additionalProperties":false,"x-configurator":{"sensitivity":"public"},"properties":{
           "public":{"type":"string","default":"synthetic-default-sentinel","x-configurator":{"sensitivity":"public"}},
           "token":{"type":"string","x-configurator":{"sensitivity":"secret"}},
           "nested":{"type":"object","additionalProperties":false,"x-configurator":{"sensitivity":"public"},"properties":{
             "visible":{"type":"string","x-configurator":{"sensitivity":"public"}},
             "token":{"type":"string","x-configurator":{"sensitivity":"secret"}}}},
           "privateGroup":{"type":"object","additionalProperties":false,"x-configurator":{"sensitivity":"sensitive"},"properties":{
             "publicLeak":{"type":"string","x-configurator":{"sensitivity":"public"}}}},
           "map":{"type":"object","x-configurator":{"sensitivity":"public"},"additionalProperties":{
             "type":"string","x-configurator":{"sensitivity":"sensitive"}}},
           "sequence":{"type":"array","x-configurator":{"sensitivity":"public"},"items":{
             "type":"object","additionalProperties":false,"x-configurator":{"sensitivity":"public"},"properties":{
               "visible":{"type":"string","x-configurator":{"sensitivity":"public"}},
               "token":{"type":"string","x-configurator":{"sensitivity":"secret"}}}}},
           "variant":{"type":"object","oneOf":[
             {"type":"object","additionalProperties":false,"required":["kind"],"x-configurator":{"sensitivity":"public"},"properties":{
               "kind":{"type":"string","const":"a","x-configurator":{"sensitivity":"public"}},
               "token":{"type":"string","x-configurator":{"sensitivity":"secret"}}}},
             {"type":"object","additionalProperties":false,"required":["kind"],"x-configurator":{"sensitivity":"public"},"properties":{
               "kind":{"type":"string","const":"b","x-configurator":{"sensitivity":"public"}},
               "token":{"type":"string","x-configurator":{"sensitivity":"public"}}}}
           ],"x-configurator":{"sensitivity":"public"}}
         }},
         "outputs":{".env":{"format":"dotenv","bindings":{"VALUE":"/public"}}}}
        """;
}
