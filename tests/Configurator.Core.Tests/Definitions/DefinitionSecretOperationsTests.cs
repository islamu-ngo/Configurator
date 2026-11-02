namespace Configurator.Core.Tests.Definitions;

using System.Text.Json;
using System.Text.Json.Nodes;
using Configurator.Core.Definitions;

public sealed class DefinitionSecretOperationsTests
{
    [Test]
    public async Task ReplacingAnEscapedMapLeafPreservesOtherAnswersAndTheOriginalSnapshot()
    {
        Definition definition = Definition();
        AnswerSnapshot original = DefinitionFixture.Answers("""{"service.name":"sample","accounts":{"a/b~c":"old","0":"retained"}}""");
        DefinitionValidationResult validation = DefinitionValidator.Validate(definition, original);
        DefinitionPath path = DefinitionPath.Parse("/accounts/a~1b~0c");
        await Assert.That(DefinitionSecretOperations.Resolve(definition, original.Omitted, validation, path, out var location)).IsNotNull();
        // Canonical ordinal ordering places the literal key "0" before "a/b~c".
        await Assert.That(location.Single()).IsEqualTo(DefinitionLocationSegment.MapEntry(1));
        AnswerSnapshot updated = DefinitionSecretOperations.Replace(original, validation, path, JsonSerializer.SerializeToElement("replacement"));
        await Assert.That(updated.Values["accounts"].GetProperty("a/b~c").GetString()).IsEqualTo("replacement");
        await Assert.That(updated.Values["accounts"].GetProperty("0").GetString()).IsEqualTo("retained");
        await Assert.That(updated.Values["service.name"].GetString()).IsEqualTo("sample");
        await Assert.That(original.Values["accounts"].GetProperty("a/b~c").GetString()).IsEqualTo("old");
        await Assert.That(updated.InputRevision).IsEqualTo(8);
    }

    [Test]
    public async Task ReadOnlyAndOmittedAncestorsCannotGrantSecretAuthority()
    {
        Definition definition = Definition(readOnly: true);
        AnswerSnapshot answers = DefinitionFixture.Answers("""{"accounts":{"entry":"old"}}""");
        await Assert.That(DefinitionSecretOperations.Resolve(definition, answers.Omitted, DefinitionValidator.Validate(definition, answers),
            DefinitionPath.Parse("/accounts/entry"), out _)).IsNull();
        definition = Definition();
        answers = new(0, new Dictionary<string, JsonElement>(), [DefinitionPath.Parse("/accounts")]);
        await Assert.That(DefinitionSecretOperations.Resolve(definition, answers.Omitted, DefinitionValidator.Validate(definition, answers),
            DefinitionPath.Parse("/accounts/entry"), out _)).IsNull();
    }

    [Test]
    public async Task ExplicitLeafReplacementRemovesOnlyThatOmission()
    {
        Definition definition = Definition();
        AnswerSnapshot answers = new(0, new Dictionary<string, JsonElement>(),
            [DefinitionPath.Parse("/service.name"), DefinitionPath.Parse("/accounts")]);
        AnswerSnapshot updated = DefinitionSecretOperations.Replace(answers, DefinitionValidator.Validate(definition, answers),
            DefinitionPath.Parse("/service.name"), JsonSerializer.SerializeToElement("replacement"));
        await Assert.That(updated.Omitted.Select(path => path.ToPointer())).IsEquivalentTo(["/accounts"]);
        await Assert.That(answers.Omitted.Count).IsEqualTo(2);
    }

    [Test]
    public async Task ArrayReplacementUsesCanonicalIndicesAndPreservesOtherRows()
    {
        Definition definition = Definition();
        AnswerSnapshot answers = DefinitionFixture.Answers("""{"rows":[{"value":"first"},{"value":"second"}]}""");
        DefinitionValidationResult validation = DefinitionValidator.Validate(definition, answers);
        AnswerSnapshot updated = DefinitionSecretOperations.Replace(answers, validation,
            DefinitionPath.Parse("/rows/1/value"), JsonSerializer.SerializeToElement("replacement"));
        await Assert.That(updated.Values["rows"][0].GetProperty("value").GetString()).IsEqualTo("first");
        await Assert.That(updated.Values["rows"][1].GetProperty("value").GetString()).IsEqualTo("replacement");
        await Assert.That(answers.Values["rows"][1].GetProperty("value").GetString()).IsEqualTo("second");
        await Assert.That(() => DefinitionSecretOperations.Replace(answers, validation,
            DefinitionPath.Parse("/rows/01/value"), JsonSerializer.SerializeToElement("rejected")))
            .Throws<DefinitionException>();
    }

    private static Definition Definition(bool readOnly = false)
    {
        JsonNode source = JsonNode.Parse(DefinitionFixture.Json)!;
        source["configuration"]!["properties"]!["accounts"] = JsonNode.Parse("""
            {"type":"object","additionalProperties":{"type":"string","x-configurator":{
             "secret":{"kind":"password","acquisition":"local","generation":{"length":32}}}}}
            """);
        source["configuration"]!["properties"]!["accounts"]!["readOnly"] = readOnly;
        return DefinitionFixture.Parse(source.ToJsonString());
    }
}
