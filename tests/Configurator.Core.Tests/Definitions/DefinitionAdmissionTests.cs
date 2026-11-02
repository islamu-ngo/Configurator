using System.Text;
using System.Text.Json;
using Configurator.Core.Definitions;

namespace Configurator.Core.Tests.Definitions;

public sealed class DefinitionAdmissionTests
{
    [Test]
    public async Task NeutralDefinitionCanonicalIdentitySeparatesFamilyRevisionAndContent()
    {
        Definition first = DefinitionFixture.Parse();
        Definition reordered = DefinitionSourceCodec.ParseJson(Encoding.UTF8.GetBytes(
            DefinitionFixture.Json.Replace("\"revision\":1", "\"revision\":1.0", StringComparison.Ordinal)));
        Definition revised = DefinitionFixture.Parse(DefinitionFixture.Json.Replace(
            "\"revision\":1", "\"revision\":2", StringComparison.Ordinal));
        await Assert.That(first.FamilyId).IsEqualTo(revised.FamilyId);
        await Assert.That(first.Digest).IsEqualTo(reordered.Digest);
        await Assert.That(first.Digest == revised.Digest).IsFalse();
        await Assert.That(first.CanonicalBytes.Span.SequenceEqual(reordered.CanonicalBytes.Span)).IsTrue();
        await Assert.That(DefinitionCodec.ParseYaml(first.CanonicalBytes).Digest).IsEqualTo(first.Digest);
    }

    [Test]
    public async Task AdmissionRejectsDuplicateMembersUnknownVocabularyAndHostAuthority()
    {
        foreach (string invalid in new[]
        {
            DefinitionFixture.Json.Replace("\"revision\":1", "\"revision\":1,\"revision\":2", StringComparison.Ordinal),
            DefinitionFixture.Json.Replace("\"revision\":1", "\"revision\":1,\"Revision\":2", StringComparison.Ordinal),
            DefinitionFixture.Json.Replace("authoring:v0.0.1", "authoring:unsupported", StringComparison.Ordinal),
            DefinitionFixture.Json.Replace("\"$schema\":", "\"exec\":\"shell\",\"$schema\":", StringComparison.Ordinal),
            DefinitionFixture.Json.Replace("\"op\":\"copy\"", "\"op\":\"fetch\"", StringComparison.Ordinal),
            DefinitionFixture.Json.Replace("\"service.json\":", "\"../service.json\":", StringComparison.Ordinal),
            DefinitionFixture.Json.Replace("\"type\":\"string\"", "\"type\":\"string\",\"validator\":\"custom-code\"", StringComparison.Ordinal),
            DefinitionFixture.Json.Replace("\"op\":\"copy\",\"reference\":{\"scope\":\"root\",\"path\":\"/service.name\"}", "\"op\":\"index\"", StringComparison.Ordinal),
            DefinitionFixture.Json.Replace("\"detail\":\"advanced\"", "\"detail\":\"advanced\",\"applicableWhen\":{\"op\":\"exists\",\"reference\":{\"scope\":\"root\",\"path\":\"/service.name\"}}", StringComparison.Ordinal)
        })
            await Assert.That(() => DefinitionFixture.Parse(invalid)).Throws<DefinitionException>();
    }

    [Test]
    public async Task RegistryPinsExactContentAndRejectsConflictingRevisions()
    {
        Definition first = DefinitionFixture.Parse();
        Definition second = DefinitionFixture.Parse(DefinitionFixture.Json.Replace(
            "\"revision\":1", "\"revision\":2", StringComparison.Ordinal));
        var registry = new DefinitionRegistry([first, second]);
        await Assert.That(() => registry.Resolve(first.FamilyId)).Throws<DefinitionException>();
        await Assert.That(registry.Resolve(first.FamilyId, "1.0").Digest).IsEqualTo(second.Digest);
        await Assert.That(registry.Resolve(first.FamilyId, "1.0", first.Digest).Digest).IsEqualTo(first.Digest);
        await Assert.That(() => registry.Resolve(first.FamilyId, "9.0")).Throws<DefinitionException>();
        Definition conflict = DefinitionFixture.Parse(DefinitionFixture.Json.Replace(
            "\"publisher\":\"example\"", "\"publisher\":\"other\"", StringComparison.Ordinal));
        await Assert.That(() => new DefinitionRegistry([first, conflict])).Throws<DefinitionException>();
    }

    [Test]
    public async Task DefinitionsRejectSensitiveDefaultsAndClosedYamlViolations()
    {
        string secretDefault = DefinitionFixture.Json.Replace("\"minLength\":1", "\"default\":\"marker\",\"minLength\":1", StringComparison.Ordinal)
            .Replace("\"sensitivity\":\"public\"", "\"sensitivity\":\"secret\"", StringComparison.Ordinal);
        await Assert.That(() => DefinitionFixture.Parse(secretDefault)).Throws<DefinitionException>();
        foreach (string yaml in new[] { "a: &anchor value\nb: *anchor", "---\na: 1\n---\nb: 2", "a: !custom value" })
            await Assert.That(() => DefinitionCodec.ParseYaml(Encoding.UTF8.GetBytes(yaml)))
                .Throws<DefinitionException>();
        await Assert.That(() => DefinitionCodec.ParseJson(new byte[4_194_305]))
            .Throws<DefinitionException>();
    }
}

internal static class DefinitionFixture
{
    internal const string Json = """
        {"$schema":"urn:islamu:configurator:authoring:v0.0.1",
         "identity":{"familyId":"69cdb122-41d2-4fab-9b82-7929a889c726","revision":1},
         "project":{"id":"example.service","name":"Example service","publisher":"example"},
         "applicationVersions":["1.0"],"entryPoints":["setup"],
         "configuration":{"type":"object","additionalProperties":false,"required":["service.name"],
          "x-configurator":{"sensitivity":"public"},
          "properties":{"service.name":{"type":"string","title":"Service name","minLength":1,"maxLength":80,
            "x-configurator":{"sensitivity":"public","detail":"advanced"}}}},
         "outputs":{"service.json":{"id":"config","format":"json","targetUpdateMode":"create",
          "restartRequirement":"process","projection":{"op":"object","members":{
            "name":{"op":"copy","reference":{"scope":"root","path":"/service.name"}}}}}}}
        """;

    internal static Definition Parse(string? json = null) =>
        DefinitionSourceCodec.ParseJson(Encoding.UTF8.GetBytes(json ?? Json));

    internal static AnswerSnapshot Answers(string json = """{"service.name":"sample"}""", long revision = 7)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return new AnswerSnapshot(revision, document.RootElement.EnumerateObject()
            .ToDictionary(property => property.Name, property => property.Value.Clone(), StringComparer.Ordinal));
    }
}
