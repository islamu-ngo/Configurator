using System.Text;
using System.Text.Json;
using Configurator.Core.Artifacts;
using Configurator.Core.Definitions;

namespace Configurator.Core.Tests.Definitions;

public sealed class RecursiveConformanceTests
{
    [Test]
    public async Task LiteralRecursiveCorpusSpecifiesCompleteAndDraftOutput()
    {
        using Stream casesStream = Resource("Conformance.Cases");
        using JsonDocument cases = JsonDocument.Parse(casesStream);
        foreach (JsonElement item in cases.RootElement.GetProperty("cases").EnumerateArray())
        {
            string identity = item.GetProperty("id").GetString()!;
            using Stream definitionStream = Resource(item.GetProperty("definition").GetString() switch
            {
                "root-map" => "Conformance.RootMap",
                "root-variant" => "Conformance.RootVariant",
                "synthetic-library" => "Conformance.Library",
                _ => throw new InvalidDataException("Unknown conformance definition.")
            });
            using var bytes = new MemoryStream();
            definitionStream.CopyTo(bytes);
            Definition definition = DefinitionSourceCodec.ParseJson(bytes.ToArray());
            var answers = new AnswerSnapshot(1,
                item.GetProperty("values").EnumerateObject().ToDictionary(
                    property => property.Name, property => property.Value.Clone(), StringComparer.Ordinal),
                item.GetProperty("omitted").EnumerateArray().Select(path => DefinitionPath.Parse(path.GetString()!)));
            await Assert.That(DefinitionValidator.Validate(definition, answers).IsReady)
                .IsEqualTo(item.GetProperty("ready").GetBoolean()).Because(identity);
            DefinitionArtifactResult complete = DefinitionArtifactComposer.Compose(definition, "config", answers);
            using DefinitionArtifact? artifact = complete.Artifact;
            string? expected = item.GetProperty("expectedJson").GetString();
            await Assert.That(complete.Succeeded).IsEqualTo(expected is not null).Because(identity);
            await Assert.That(artifact is null ? null : Encoding.UTF8.GetString(artifact.Bytes.Span))
                .IsEqualTo(expected).Because(identity);
            using DefinitionDraftArtifactResult draft = DefinitionDraftArtifactComposer.Compose(definition, "config", answers);
            string? expectedDraft = item.GetProperty("expectedDraftJson").GetString();
            await Assert.That(draft.Succeeded).IsEqualTo(expectedDraft is not null).Because(identity);
            await Assert.That(draft.Artifact is null ? null : Encoding.UTF8.GetString(draft.Artifact.Bytes.Span))
                .IsEqualTo(expectedDraft).Because(identity);
        }
    }

    private static Stream Resource(string name) =>
        typeof(RecursiveConformanceTests).Assembly.GetManifestResourceStream(name)
        ?? throw new InvalidDataException("Missing conformance resource.");
}
