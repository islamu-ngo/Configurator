namespace Configurator.Presentation.Tests;

using System.Text;
using System.Text.Json;
using Configurator.Core.Definitions;

public sealed class DefinitionPresentationOperationTests
{
    [Test]
    public async Task CapturedAnswersRemainStableAfterCallerChangesItsDictionary()
    {
        SetupDefinition definition = Definition();
        using JsonDocument initial = JsonDocument.Parse("\"local-service\"");
        using JsonDocument changed = JsonDocument.Parse("\"changed-service\"");
        var values = new Dictionary<string, JsonElement>
        {
            ["service.name"] = initial.RootElement
        };
        var operation = new DefinitionPresentationOperation(
            definition, "config", new SetupAnswerSnapshot(7, values));
        values["service.name"] = changed.RootElement;

        SetupPresentationOutcome outcome = await operation.ExecuteAsync(CancellationToken.None);
        var metadata = (DefinitionPreparationMetadata)outcome.CoreResult;

        await Assert.That(metadata.Succeeded).IsTrue();
        await Assert.That(metadata.InputRevision).IsEqualTo(7);
        await Assert.That(metadata.DefinitionDigest).IsEqualTo(definition.Digest);
        await Assert.That(Encoding.UTF8.GetString(outcome.Output.Span))
            .IsEqualTo("{\"name\":\"local-service\"}");
        await Assert.That(metadata.ToString().Contains("local-service", StringComparison.Ordinal))
            .IsFalse();
    }

    [Test]
    public async Task InvalidAnswersProduceDiagnosticsWithoutPreparedOutput()
    {
        var operation = new DefinitionPresentationOperation(
            Definition(), "config",
            new SetupAnswerSnapshot(1, new Dictionary<string, JsonElement>()));

        SetupPresentationOutcome outcome = await operation.ExecuteAsync(CancellationToken.None);
        var metadata = (DefinitionPreparationMetadata)outcome.CoreResult;

        await Assert.That(metadata.Succeeded).IsFalse();
        await Assert.That(metadata.DiagnosticCodes.Count > 0).IsTrue();
        await Assert.That(outcome.Output.IsEmpty).IsTrue();
    }

    private static SetupDefinition Definition() => SetupDefinitionCodec.ParseJson(
        Encoding.UTF8.GetBytes("""
        {
          "schemaVersion": 1,
          "familyId": "e201806e-18ec-40ee-8540-f5d1fa742ce1",
          "revision": 1,
          "applicationId": "example.service",
          "applicationVersions": ["1.0"],
          "publisher": "example",
          "title": "Example service",
          "entryPoints": ["setup"],
          "locales": ["en"],
          "fields": [
            {"id":"service.name","type":"string","required":true,"sensitivity":"public","disclosure":"guided","title":"Service name"}
          ],
          "artifacts": [
            {
              "id":"config","format":"json","fileName":"service.json",
              "applySemantics":"create","restart":"process",
              "projection":{"op":"object","members":{"name":{"op":"copy","field":"service.name"}}}
            }
          ]
        }
        """));
}
