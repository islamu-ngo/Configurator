using System.Text;
using System.Text.Json;
using Configurator.Core.Definitions;

namespace Configurator.Core.Tests.Definitions;

public sealed class DefinitionAdmissionTests
{
    [Test]
    public async Task NeutralDefinitionCanonicalIdentitySeparatesFamilyRevisionAndContent()
    {
        SetupDefinition first = DefinitionFixture.Parse();
        SetupDefinition reordered = SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(
            DefinitionFixture.Json.Replace("\"revision\":1,", "\"revision\":1.0,", StringComparison.Ordinal)));
        SetupDefinition revised = DefinitionFixture.Parse(DefinitionFixture.Json.Replace(
            "\"revision\":1,", "\"revision\":2,", StringComparison.Ordinal));
        await Assert.That(first.FamilyId).IsEqualTo(revised.FamilyId);
        await Assert.That(first.Digest).IsEqualTo(reordered.Digest);
        await Assert.That(first.Digest == revised.Digest).IsFalse();
        await Assert.That(first.CanonicalBytes.Span.SequenceEqual(reordered.CanonicalBytes.Span)).IsTrue();
        await Assert.That(SetupDefinitionCodec.ParseYaml(first.CanonicalBytes).Digest).IsEqualTo(first.Digest);
    }

    [Test]
    public async Task AdmissionRejectsDuplicateMembersUnknownVocabularyAndHostAuthority()
    {
        foreach (string invalid in new[]
        {
            DefinitionFixture.Json.Replace("\"revision\":1,", "\"revision\":1,\"revision\":2,", StringComparison.Ordinal),
            DefinitionFixture.Json.Replace("\"revision\":1,", "\"revision\":1,\"Revision\":2,", StringComparison.Ordinal),
            DefinitionFixture.Json.Replace("\"schemaVersion\":1", "\"schemaVersion\":2", StringComparison.Ordinal),
            DefinitionFixture.Json.Replace("\"schemaVersion\":1", "\"schemaVersion\":1,\"exec\":\"shell\"", StringComparison.Ordinal),
            DefinitionFixture.Json.Replace("\"op\":\"copy\"", "\"op\":\"fetch\"", StringComparison.Ordinal),
            DefinitionFixture.Json.Replace("\"fileName\":\"service.json\"", "\"fileName\":\"../service.json\"", StringComparison.Ordinal),
            DefinitionFixture.Json.Replace("\"type\":\"string\"", "\"type\":\"string\",\"validator\":\"custom-code\"", StringComparison.Ordinal),
            DefinitionFixture.Json.Replace("\"op\":\"copy\",\"field\":\"service.name\"", "\"op\":\"index\"", StringComparison.Ordinal),
            DefinitionFixture.Json.Replace("\"id\":\"service.name\"", "\"id\":\"service.name\",\"activeWhen\":{\"op\":\"present\",\"field\":\"service.name\"}", StringComparison.Ordinal)
        })
            await Assert.That(() => DefinitionFixture.Parse(invalid)).Throws<SetupDefinitionException>();
    }

    [Test]
    public async Task RegistryPinsExactContentAndRejectsConflictingRevisions()
    {
        SetupDefinition first = DefinitionFixture.Parse();
        SetupDefinition second = DefinitionFixture.Parse(DefinitionFixture.Json.Replace(
            "\"revision\":1,", "\"revision\":2,", StringComparison.Ordinal));
        var registry = new SetupDefinitionRegistry([first, second]);
        await Assert.That(() => registry.Resolve(first.FamilyId)).Throws<SetupDefinitionException>();
        await Assert.That(registry.Resolve(first.FamilyId, "1.0").Digest).IsEqualTo(second.Digest);
        await Assert.That(registry.Resolve(first.FamilyId, "1.0", first.Digest).Digest).IsEqualTo(first.Digest);
        await Assert.That(() => registry.Resolve(first.FamilyId, "9.0")).Throws<SetupDefinitionException>();
        SetupDefinition conflict = DefinitionFixture.Parse(DefinitionFixture.Json.Replace(
            "\"publisher\":\"example\"", "\"publisher\":\"other\"", StringComparison.Ordinal));
        await Assert.That(() => new SetupDefinitionRegistry([first, conflict])).Throws<SetupDefinitionException>();
    }

    [Test]
    public async Task DefinitionsRejectSensitiveDefaultsAndClosedYamlViolations()
    {
        string secretDefault = DefinitionFixture.Json.Replace(
            "\"sensitivity\":\"public\"", "\"sensitivity\":\"secret\",\"default\":\"marker\"", StringComparison.Ordinal);
        await Assert.That(() => DefinitionFixture.Parse(secretDefault)).Throws<SetupDefinitionException>();
        foreach (string yaml in new[] { "a: &anchor value\nb: *anchor", "---\na: 1\n---\nb: 2", "a: !custom value" })
            await Assert.That(() => SetupDefinitionCodec.ParseYaml(Encoding.UTF8.GetBytes(yaml)))
                .Throws<SetupDefinitionException>();
        await Assert.That(() => SetupDefinitionCodec.ParseJson(new byte[4_194_305]))
            .Throws<SetupDefinitionException>();
    }
}

internal static class DefinitionFixture
{
    internal const string Json = """
        {"schemaVersion":1,"familyId":"69cdb122-41d2-4fab-9b82-7929a889c726","revision":1,
         "applicationId":"example.service","applicationVersions":["1.0"],"publisher":"example",
         "title":"Example service","entryPoints":["setup"],"locales":["en"],
         "fields":[{"id":"service.name","type":"string","required":true,"sensitivity":"public",
         "disclosure":"advanced","title":"Service name","minLength":1,"maxLength":80}],
         "artifacts":[{"id":"config","format":"json","fileName":"service.json","applySemantics":"create",
         "restart":"process","projection":{"op":"object","members":{"name":{"op":"copy","field":"service.name"}}}}]}
        """;

    internal static SetupDefinition Parse(string? json = null) =>
        SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(json ?? Json));

    internal static SetupAnswerSnapshot Answers(string json = """{"service.name":"sample"}""", long revision = 7)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return new SetupAnswerSnapshot(revision, document.RootElement.EnumerateObject()
            .ToDictionary(property => property.Name, property => property.Value.Clone(), StringComparer.Ordinal));
    }
}
