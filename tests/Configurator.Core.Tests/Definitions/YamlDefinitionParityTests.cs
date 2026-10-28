using System.Text;
using System.Text.Json.Nodes;
using Configurator.Core.Definitions;

namespace Configurator.Core.Tests.Definitions;

public sealed class YamlDefinitionParityTests
{
    [Test]
    public async Task ExactDecimalAndExponentScalarsShareJsonIdentity()
    {
        foreach (string number in new[] { "0.5", "5e-1", "-0", "1e-28" })
        {
            // Literal projections let numeric identity be tested without an integer revision constraint.
            string json = DefinitionFixture.Json.Replace("\"op\":\"copy\",\"reference\":{\"scope\":\"root\",\"path\":\"/service.name\"}",
                "\"op\":\"literal\",\"value\":" + number, StringComparison.Ordinal);
            SetupDefinition expected = SetupDefinitionSourceCodec.ParseJson(Encoding.UTF8.GetBytes(json));
            string yaml = json.Replace("\"value\":" + number, "value: " + number, StringComparison.Ordinal);
            await Assert.That(SetupDefinitionSourceCodec.ParseYaml(Encoding.UTF8.GetBytes(yaml)).Digest).IsEqualTo(expected.Digest);
        }
    }

    [Test]
    public async Task TypedMappingKeysAndEmptyPlainScalarsReject()
    {
        foreach (string key in new[] { "true", "false", "null", "0", "0.5", "5e-1" })
        {
            string yaml = DefinitionFixture.Json.Replace("\"name\":", key + ":", StringComparison.Ordinal);
            await Assert.That(() => SetupDefinitionSourceCodec.ParseYaml(Encoding.UTF8.GetBytes(yaml)))
                .Throws<SetupDefinitionException>();
        }
        string empty = DefinitionFixture.Json.Replace("\"path\":\"/service.name\"", "\"path\":", StringComparison.Ordinal);
        await Assert.That(() => SetupDefinitionSourceCodec.ParseYaml(Encoding.UTF8.GetBytes(empty)))
            .Throws<SetupDefinitionException>();
    }

    [Test]
    public async Task QuotedNullKeyRemainsAnOrdinaryString()
    {
        JsonNode source = JsonNode.Parse(DefinitionFixture.Json)!;
        JsonObject members = source["outputs"]!["service.json"]!["projection"]!["members"]!.AsObject();
        JsonNode value = members["name"]!;
        members.Remove("name");
        members["null"] = value;
        string json = source.ToJsonString();
        await Assert.That(SetupDefinitionSourceCodec.ParseYaml(Encoding.UTF8.GetBytes(json)).Digest)
            .IsEqualTo(SetupDefinitionSourceCodec.ParseJson(Encoding.UTF8.GetBytes(json)).Digest);
    }

    [Test]
    public async Task UnsupportedNumbersAndYamlAuthorityReject()
    {
        foreach (string scalar in new[] { "1e-29", "1e29", "12345678901234567890123456789", "01", "+1", ".nan", "0x12", "1_000", "" })
        {
            string yaml = DefinitionFixture.Json.Replace("\"op\":\"copy\",\"reference\":{\"scope\":\"root\",\"path\":\"/service.name\"}",
                "\"op\":\"literal\", value: " + scalar, StringComparison.Ordinal);
            await Assert.That(() => SetupDefinitionSourceCodec.ParseYaml(Encoding.UTF8.GetBytes(yaml)))
                .Throws<SetupDefinitionException>();
        }
        foreach (string yaml in new[] { "a: &anchor value\nb: *anchor", "a: !custom value", "%YAML 1.2\n---\na: value", "<<: value", "'<<': value", "---\na: one\n---\nb: two", "a: one\na: two", "a: one\nA: two" })
            await Assert.That(() => SetupDefinitionSourceCodec.ParseYaml(Encoding.UTF8.GetBytes(yaml))).Throws<SetupDefinitionException>();
    }

    [Test]
    public async Task ExactDataKeysRemainDistinctWhileExactDuplicatesReject()
    {
        const string properties = """
            "A":{"type":"string"},"a":{"type":"string"},
            "\u00e9":{"type":"string"},"e\u0301":{"type":"string"},
            """;
        string source = DefinitionFixture.Json.Replace("\"properties\":{", "\"properties\":{" + properties, StringComparison.Ordinal);
        SetupDefinition json = SetupDefinitionSourceCodec.ParseJson(Encoding.UTF8.GetBytes(source));
        SetupDefinition yaml = SetupDefinitionSourceCodec.ParseYaml(Encoding.UTF8.GetBytes(source));
        await Assert.That(json.Digest).IsEqualTo(yaml.Digest);
        await Assert.That(json.Configuration.Properties.Count).IsEqualTo(5);
        await Assert.That(json.Configuration.Properties.ContainsKey("A")).IsTrue();
        await Assert.That(json.Configuration.Properties.ContainsKey("a")).IsTrue();
        await Assert.That(json.Configuration.Properties.ContainsKey("\u00e9")).IsTrue();
        await Assert.That(json.Configuration.Properties.ContainsKey("e\u0301")).IsTrue();
        string duplicate = source.Replace("\"a\":{\"type\":\"string\"}", "\"A\":{\"type\":\"string\"}", StringComparison.Ordinal);
        await Assert.That(() => SetupDefinitionSourceCodec.ParseJson(Encoding.UTF8.GetBytes(duplicate))).Throws<SetupDefinitionException>();
        await Assert.That(() => SetupDefinitionSourceCodec.ParseYaml(Encoding.UTF8.GetBytes(duplicate))).Throws<SetupDefinitionException>();
    }
}
