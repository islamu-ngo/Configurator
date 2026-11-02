using System.Text.Json.Nodes;
using Configurator.Core.Definitions;

namespace Configurator.Core.Tests.Definitions;

public sealed class DefinitionSecretAdmissionTests
{
    [Test]
    [Arguments("kind", "\"Password\"")]
    [Arguments("kind", "\"unknown\"")]
    [Arguments("kind", "0")]
    [Arguments("kind", "null")]
    [Arguments("acquisition", "\"Local\"")]
    [Arguments("acquisition", "0")]
    [Arguments("acquisition", "null")]
    public async Task ClosedPolicyTokensRejectUnknownCasingNumericAndNull(string member, string token)
    {
        JsonObject source = JsonNode.Parse(DefinitionFixture.Json)!.AsObject();
        JsonObject secret = JsonNode.Parse("""{"kind":"password","acquisition":"existing"}""")!.AsObject();
        secret[member] = JsonNode.Parse(token);
        source["configuration"]!["properties"]!["service.name"]!["x-configurator"] = new JsonObject { ["secret"] = secret };
        await Assert.That(() => DefinitionFixture.Parse(source.ToJsonString())).Throws<SetupDefinitionException>();
    }

    [Test]
    public async Task SecretMetadataEstablishesSensitivityWithoutAnExplicitLabel()
    {
        string json = DefinitionFixture.Json.Replace("\"sensitivity\":\"public\",\"detail\":\"advanced\"",
            "\"secret\":{\"kind\":\"password\",\"acquisition\":\"existing\"}", StringComparison.Ordinal);
        SetupDefinition definition = DefinitionFixture.Parse(json);
        await Assert.That(definition.Fields.Single().Sensitivity).IsEqualTo(SetupFieldSensitivity.Secret);
    }

    [Test]
    public async Task ContradictoryPublicSecretDeclarationRejects()
    {
        string json = DefinitionFixture.Json.Replace("\"detail\":\"advanced\"",
            "\"secret\":{\"kind\":\"password\",\"acquisition\":\"existing\"}", StringComparison.Ordinal);
        await Assert.That(() => DefinitionFixture.Parse(json)).Throws<SetupDefinitionException>();
    }

    [Test]
    [Arguments("public", false)]
    [Arguments("sensitive", false)]
    [Arguments("secret", true)]
    public async Task ReferencesCannotLowerSensitivityOrReplaceAnInheritedPolicy(string sensitivity, bool conflict)
    {
        JsonObject source = JsonNode.Parse(DefinitionFixture.Json)!.AsObject();
        JsonObject configuration = source["configuration"]!.AsObject();
        configuration["$defs"] = JsonNode.Parse("""{"password":{"type":"string","x-configurator":{"secret":{"kind":"password","acquisition":"existing"}}}}""");
        var metadata = new JsonObject { ["sensitivity"] = sensitivity };
        if (conflict) metadata["secret"] = JsonNode.Parse("""{"kind":"password","acquisition":"issuer"}""");
        configuration["properties"]!["service.name"] = new JsonObject { ["$ref"] = "#/$defs/password", ["x-configurator"] = metadata };
        await Assert.That(() => DefinitionFixture.Parse(source.ToJsonString())).Throws<SetupDefinitionException>();
    }

    [Test]
    public async Task ReferencedSecretRetainsTheSecretFloor()
    {
        JsonObject source = JsonNode.Parse(DefinitionFixture.Json)!.AsObject();
        JsonObject configuration = source["configuration"]!.AsObject();
        configuration["$defs"] = JsonNode.Parse("""{"password":{"type":"string","x-configurator":{"secret":{"kind":"password","acquisition":"existing"}}}}""");
        configuration["properties"]!["service.name"] = new JsonObject { ["$ref"] = "#/$defs/password" };
        await Assert.That(DefinitionFixture.Parse(source.ToJsonString()).Fields.Single().Sensitivity).IsEqualTo(SetupFieldSensitivity.Secret);
    }

    [Test]
    [Arguments("{\"type\":\"array\",\"items\":{\"type\":\"string\",\"x-configurator\":{\"secret\":{\"kind\":\"password\",\"acquisition\":\"existing\"}}}}")]
    [Arguments("{\"type\":\"object\",\"additionalProperties\":{\"type\":\"string\",\"x-configurator\":{\"secret\":{\"kind\":\"password\",\"acquisition\":\"existing\"}}}}")]
    public async Task ContainerSensitivityIncludesTypedSecretDescendants(string schema)
    {
        JsonObject source = JsonNode.Parse(DefinitionFixture.Json)!.AsObject();
        source["configuration"]!["properties"]!["service.name"] = JsonNode.Parse(schema);
        SetupDefinition definition = DefinitionFixture.Parse(source.ToJsonString());
        await Assert.That(definition.Fields.Single().Sensitivity).IsEqualTo(SetupFieldSensitivity.Secret);
        await Assert.That(DefinitionSensitivityPolicy.ContainsSecret(definition.Configuration)).IsTrue();
    }

    [Test]
    [Arguments("{\"kind\":\"password\",\"acquisition\":\"local\",\"constraints\":{\"minSymbols\":40},\"generation\":{\"length\":32}}", 80)]
    [Arguments("{\"kind\":\"opaque-token\",\"acquisition\":\"local\",\"encoding\":\"hex\",\"generation\":{\"bytes\":32}}", 32)]
    [Arguments("{\"kind\":\"password-hash\",\"acquisition\":\"local\",\"algorithm\":\"argon2id\",\"constraints\":{\"memoryKiB\":4194304}}", 80)]
    [Arguments("{\"kind\":\"password\",\"acquisition\":\"issuer\",\"generation\":{\"length\":32}}", 80)]
    [Arguments("{\"kind\":\"password\",\"acquisition\":\"existing\",\"constraints\":{\"minSymbols\":1,\"allowedSymbols\":\"!\",\"forbiddenCharacters\":\"!\"}}", 80)]
    [Arguments("{\"kind\":\"digest\",\"acquisition\":\"existing\",\"algorithm\":\"SHA256\",\"encoding\":\"hex\"}", 32)]
    [Arguments("{\"kind\":\"password-hash\",\"acquisition\":\"local\",\"algorithm\":\"argon2id\",\"generation\":{\"input\":\"entered\"}}", 80)]
    public async Task ImpossibleOrHostileRecipesRejectBeforeCollectingAnswers(string secret, int maximum)
    {
        JsonObject source = JsonNode.Parse(DefinitionFixture.Json)!.AsObject();
        JsonObject field = source["configuration"]!["properties"]!["service.name"]!.AsObject();
        field["maxLength"] = maximum;
        field["x-configurator"] = new JsonObject { ["secret"] = JsonNode.Parse(secret) };
        await Assert.That(() => DefinitionFixture.Parse(source.ToJsonString())).Throws<SetupDefinitionException>();
    }

    [Test]
    public async Task GeneratedPasswordInputIsAnAdmittedNestedPolicy()
    {
        JsonObject source = JsonNode.Parse(DefinitionFixture.Json)!.AsObject();
        JsonObject field = source["configuration"]!["properties"]!["service.name"]!.AsObject();
        field["x-configurator"] = JsonNode.Parse("""{"secret":{"kind":"digest","acquisition":"local","algorithm":"SHA256","encoding":"hex","generation":{"input":"generated","password":{"kind":"password","acquisition":"local","generation":{"length":32}}}}}""");
        SetupDefinition definition = DefinitionFixture.Parse(source.ToJsonString());
        await Assert.That(definition.Fields.Single().Node.Secret!.Generation!.Password!.Generation!.Length).IsEqualTo(32);
    }
}
