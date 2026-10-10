using System.Text;
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
            string json = DefinitionFixture.Json.Replace("\"op\":\"copy\",\"field\":\"service.name\"",
                "\"op\":\"literal\",\"value\":" + number, StringComparison.Ordinal);
            SetupDefinition expected = SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(json));
            string yaml = json.Replace("\"value\":" + number, "value: " + number, StringComparison.Ordinal);
            await Assert.That(SetupDefinitionCodec.ParseYaml(Encoding.UTF8.GetBytes(yaml)).Digest).IsEqualTo(expected.Digest);
        }
    }

    [Test]
    public async Task TypedMappingKeysAndEmptyPlainScalarsReject()
    {
        foreach (string key in new[] { "true", "false", "null", "0", "0.5", "5e-1" })
        {
            string yaml = DefinitionFixture.Json.Replace("\"name\":", key + ":", StringComparison.Ordinal);
            await Assert.That(() => SetupDefinitionCodec.ParseYaml(Encoding.UTF8.GetBytes(yaml)))
                .Throws<SetupDefinitionException>();
        }
        string empty = DefinitionFixture.Json.Replace("\"service.name\"}}", "}}", StringComparison.Ordinal);
        await Assert.That(() => SetupDefinitionCodec.ParseYaml(Encoding.UTF8.GetBytes(empty)))
            .Throws<SetupDefinitionException>();
    }

    [Test]
    public async Task QuotedNullKeyRemainsAnOrdinaryString()
    {
        string json = DefinitionFixture.Json.Replace("\"name\":", "\"null\":", StringComparison.Ordinal);
        await Assert.That(SetupDefinitionCodec.ParseYaml(Encoding.UTF8.GetBytes(json)).Digest)
            .IsEqualTo(SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(json)).Digest);
    }

    [Test]
    public async Task UnsupportedNumbersAndYamlAuthorityReject()
    {
        foreach (string scalar in new[] { "1e-29", "1e29", "12345678901234567890123456789", "01", "+1", ".nan", "0x12", "1_000", "" })
        {
            string yaml = DefinitionFixture.Json.Replace("\"op\":\"copy\",\"field\":\"service.name\"",
                "\"op\":\"literal\", value: " + scalar, StringComparison.Ordinal);
            await Assert.That(() => SetupDefinitionCodec.ParseYaml(Encoding.UTF8.GetBytes(yaml)))
                .Throws<SetupDefinitionException>();
        }
        foreach (string yaml in new[] { "a: &anchor value\nb: *anchor", "a: !custom value", "%YAML 1.2\n---\na: value", "<<: value", "'<<': value", "---\na: one\n---\nb: two", "a: one\na: two", "a: one\nA: two" })
            await Assert.That(() => SetupDefinitionCodec.ParseYaml(Encoding.UTF8.GetBytes(yaml))).Throws<SetupDefinitionException>();
    }
}
