using System.Text;
using System.Text.Json;
using Configurator.Core.Artifacts;
using Configurator.Core.Definitions;
using Configurator.Core.Definitions.Authoring;

namespace Configurator.Core.Tests.Definitions;

public sealed class DefinitionAuthoringIdentityTests
{
    internal const string Yaml = """
        # Keep this maintainer comment.
        $schema: urn:islamu:configurator:authoring:v2
        identity:
          familyId: 69cdb122-41d2-4fab-9b82-7929a889c726
          revision: 1 # only this token is managed
        project:
          id: example.service
          name: Example Service
        applicationVersions: ["1.0"]
        configuration:
          type: object
          additionalProperties: false
          required: [SERVICE_TOKEN]
          x-configurator: {sensitivity: public}
          properties:
            SERVICE_PORT:
              type: integer
              x-configurator: {sensitivity: public, initialValue: 8080}
            SERVICE_TOKEN:
              type: string
              x-configurator: {sensitivity: secret, notBlank: true}
        outputs:
          ".env":
            format: dotenv
            bindings:
              SERVICE_PORT: {scope: root, path: /SERVICE_PORT}
              SERVICE_TOKEN: {scope: root, path: /SERVICE_TOKEN}
        """;

    [Test]
    public async Task BothFormatsCompileToOneIdentityAndIndependentOutputBytes()
    {
        SetupDefinition yaml = SetupDefinitionSourceCodec.ParseYaml(Encoding.UTF8.GetBytes(Yaml));
        SetupDefinition json = DefinitionAuthoringTests.Compile();
        await Assert.That(yaml.Digest).IsEqualTo(json.Digest);
        using JsonDocument values = JsonDocument.Parse("""{"SERVICE_PORT":8080,"SERVICE_TOKEN":"synthetic-token"}""");
        var answers = new SetupAnswerSnapshot(1, values.RootElement.EnumerateObject().ToDictionary(value => value.Name, value => value.Value));
        DefinitionArtifactResult result = DefinitionArtifactComposer.Compose(json, "out-.env", answers);
        await Assert.That(result.Succeeded).IsTrue();
        await Assert.That(Encoding.UTF8.GetString(result.Artifact!.Bytes.Span)).IsEqualTo("SERVICE_PORT=8080\nSERVICE_TOKEN=synthetic-token\n");
        result.Artifact.Dispose();
    }

    [Test]
    public async Task UnrelatedInsertionsDoNotChangeFieldOrOutputIdentity()
    {
        SetupDefinition original = DefinitionAuthoringTests.Compile();
        string inserted = DefinitionAuthoringTests.Source.Replace("\"properties\":{", "\"properties\":{\"ANOTHER\":{\"type\":\"boolean\"},", StringComparison.Ordinal);
        SetupDefinition changed = DefinitionAuthoringTests.Compile(inserted);
        await Assert.That(changed.Fields.Single(field => field.Id == "SERVICE_PORT").Id).IsEqualTo(original.Fields[0].Id);
        await Assert.That(changed.Artifacts.Single().Id).IsEqualTo("out-.env");
        await Assert.That(DefinitionProfile.FieldId("service.port")).IsEqualTo("service.port");
        await Assert.That(DefinitionProfile.FieldId("/service/port").Length).IsEqualTo(66);
    }

    [Test]
    public async Task IdentityEditsPreserveCommentsFormattingBomAndNonAsciiText()
    {
        foreach (bool yaml in new[] { true, false })
        {
            string text = yaml ? "\uFEFF" + Yaml.Replace("Example Service", "Service café", StringComparison.Ordinal)
                : "\uFEFF" + DefinitionAuthoringTests.Source.Replace("Example Service", "Service café", StringComparison.Ordinal);
            byte[] revised = DefinitionAuthoringIdentityEditor.UpdateRevision(Encoding.UTF8.GetBytes(text), yaml, 2);
            string expected = yaml ? text.Replace("revision: 1 #", "revision: 2 #", StringComparison.Ordinal)
                : text.Replace("\"revision\":1", "\"revision\":2", StringComparison.Ordinal);
            await Assert.That(Encoding.UTF8.GetString(revised)).IsEqualTo(expected);
        }
    }

    [Test]
    public async Task ForkQuotesNewFamilyTokensBeforeYamlResolution()
    {
        var family = Guid.Parse("12345678-1234-4123-8123-123456789012");
        byte[] fork = DefinitionAuthoringIdentityEditor.Fork(Encoding.UTF8.GetBytes(Yaml), true, family);
        await Assert.That(SetupDefinitionSourceCodec.ParseYaml(fork).FamilyId).IsEqualTo(family);
        await Assert.That(Encoding.UTF8.GetString(fork).Contains("# Keep this maintainer comment.", StringComparison.Ordinal)).IsTrue();
    }

    [Test]
    public async Task UnknownOrConflictingDiscriminatorsAndCollisionsReject()
    {
        foreach (string source in new[]
        {
            DefinitionAuthoringTests.Source.Replace("authoring:v2", "authoring:v1", StringComparison.Ordinal),
            DefinitionAuthoringTests.Source.Replace("\"identity\":", "\"schemaVersion\":1,\"identity\":", StringComparison.Ordinal),
            DefinitionAuthoringTests.Source.Replace("\"properties\":{", "\"properties\":{\"SERVICE_PORT\":{\"type\":\"boolean\"},", StringComparison.Ordinal)
        })
            await Assert.That(() => SetupDefinitionSourceCodec.ParseJson(Encoding.UTF8.GetBytes(source))).Throws<SetupDefinitionException>();
    }

    [Test]
    public async Task MultilineLiteralsAndQuotedNumbersRetainStringSemantics()
    {
        const string source = """
            $schema: urn:islamu:configurator:authoring:v2
            identity: {familyId: 69cdb122-41d2-4fab-9b82-7929a889c726, revision: 1}
            project: {id: example.service, name: Example}
            applicationVersions: ["1.0"]
            configuration: {type: object, additionalProperties: false, properties: {}}
            outputs:
              notes.md:
                format: markdown
                projection:
                  op: list
                  items:
                    - op: literal
                      value: |
                        First line
                        Second line
                    - {op: literal, value: "0.5"}
            """;
        SetupDefinition definition = SetupDefinitionSourceCodec.ParseYaml(Encoding.UTF8.GetBytes(source));
        DefinitionArtifactResult result = DefinitionArtifactComposer.Compose(definition, "out-notes.md", new SetupAnswerSnapshot(1, new Dictionary<string, JsonElement>()));
        await Assert.That(result.Succeeded).IsTrue();
        await Assert.That(Encoding.UTF8.GetString(result.Artifact!.Bytes.Span)).IsEqualTo("First line\nSecond line\n0\\.5");
        result.Artifact.Dispose();
    }

    [Test]
    public async Task GeneratedSourceSchemaReferencesResolveWithinOneResource()
    {
        using JsonDocument schema = JsonDocument.Parse(DefinitionAuthoringSchema.Generate());
        var nodes = new Stack<(JsonElement Node, bool DeclarationMap)>();
        nodes.Push((schema.RootElement, false));
        var unresolved = new List<string>();
        while (nodes.TryPop(out var current))
        {
            JsonElement node = current.Node;
            if (node.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement child in node.EnumerateArray()) nodes.Push((child, false));
                continue;
            }
            if (node.ValueKind != JsonValueKind.Object) continue;
            if (!current.DeclarationMap && node.TryGetProperty("$id", out JsonElement id) &&
                (id.ValueKind != JsonValueKind.String || id.GetString() != DefinitionAuthoringCodec.SchemaId))
                unresolved.Add("nested-schema-resource");
            if (!current.DeclarationMap && node.TryGetProperty("$ref", out JsonElement reference))
            {
                if (reference.ValueKind != JsonValueKind.String || !reference.GetString()!.StartsWith("#/", StringComparison.Ordinal))
                {
                    unresolved.Add("invalid-local-reference");
                    continue;
                }
                JsonElement target = schema.RootElement;
                string pointer = reference.GetString()!;
                foreach (string escaped in pointer[2..].Split('/'))
                {
                    string key = escaped.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal);
                    if (!target.TryGetProperty(key, out target))
                    {
                        unresolved.Add(pointer);
                        break;
                    }
                }
            }
            foreach (JsonProperty child in node.EnumerateObject())
                nodes.Push((child.Value, child.Name is "properties" or "$defs" or "patternProperties" or "dependentSchemas"));
        }
        await Assert.That(unresolved).IsEmpty();
    }
}
