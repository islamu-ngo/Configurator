using System.Text.Json;
using System.Text.Json.Nodes;
using Configurator.Core.Artifacts;
using Configurator.Core.Definitions;

namespace Configurator.Core.Tests.Definitions;

public sealed class ExternalDefinitionConformanceTests
{
    [Test]
    [Arguments("tenant-package.spec.legalDocuments.TenantPrivacyNotice")]
    [Arguments("legal-drafts.spec.instance.legalDocuments.TermsOfService")]
    public async Task LegalDocumentsHaveBoundedRowsWithItemLocalRequirements(string documentKey)
    {
        Definition definition = DefinitionCodec.ParseJson(Resource("EventDefinition"));
        await Assert.That(definition.Configuration.Properties.ContainsKey(documentKey)).IsTrue();
        DefinitionNode document = definition.Configuration.Properties[documentKey];
        await Assert.That(document.Type).IsEqualTo(DefinitionNodeType.Mapping);
        DefinitionNode jurisdictions = document.Properties["jurisdictionAssumptions"];
        await Assert.That(jurisdictions.Type).IsEqualTo(DefinitionNodeType.Sequence);
        await Assert.That(jurisdictions.Items!.Type).IsEqualTo(DefinitionNodeType.Text);
        DefinitionNode localizations = document.Properties["localizations"];
        await Assert.That(localizations.Type).IsEqualTo(DefinitionNodeType.Sequence);
        await Assert.That(localizations.Keywords.GetProperty("maxItems").GetInt32()).IsLessThanOrEqualTo(4096);
        await Assert.That(localizations.Items!.Required.Contains("languageTag")).IsTrue();
        await Assert.That(localizations.Items.Required.Contains("markdown")).IsTrue();
        await Assert.That(localizations.Items.Required.Contains("title")).IsTrue();
    }

    [Test]
    public async Task LegalLanguageRowsRequireTheirOwnContentAndRemovedRowsLeaveOutput()
    {
        Definition definition = DefinitionCodec.ParseJson(Resource("EventDefinition"));
        JsonNode answers = JsonNode.Parse(Resource("EventAnswers.legal-drafts.json"))!;
        JsonArray languages = answers["values"]!["legal-drafts.spec.instance.legalDocuments.TermsOfService"]!["localizations"]!.AsArray();
        languages[1]!.AsObject().Remove("title");
        DefinitionArtifactResult incomplete = DefinitionArtifactComposer.Compose(definition, "legal-drafts",
            AnswerSnapshotCodec.ParseJson(System.Text.Encoding.UTF8.GetBytes(answers.ToJsonString()), 8));
        await Assert.That(incomplete.Succeeded).IsFalse();

        languages.RemoveAt(1);
        DefinitionArtifactResult complete = DefinitionArtifactComposer.Compose(definition, "legal-drafts",
            AnswerSnapshotCodec.ParseJson(System.Text.Encoding.UTF8.GetBytes(answers.ToJsonString()), 9));
        await Assert.That(complete.Succeeded).IsTrue();
        using (complete.Artifact)
        using (JsonDocument output = JsonDocument.Parse(complete.Artifact!.Bytes))
        {
            JsonElement rows = output.RootElement.GetProperty("spec").GetProperty("instance")
                .GetProperty("legalDocuments").GetProperty("TermsOfService").GetProperty("localizations");
            await Assert.That(rows.GetArrayLength()).IsEqualTo(1);
            await Assert.That(rows[0].GetProperty("languageTag").GetString()).IsEqualTo("en");
        }
    }

    [Test]
    [Arguments("environment", "environment", "environment.env")]
    [Arguments("configuration-manifest", "configuration-manifest", "configuration-manifest.json")]
    [Arguments("configuration-manifest", "configuration-manifest-1-tenants", "configuration-manifest-1-tenants.json")]
    [Arguments("configuration-manifest", "configuration-manifest-2-tenants", "configuration-manifest-2-tenants.json")]
    [Arguments("configuration-manifest", "configuration-manifest-3-tenants", "configuration-manifest-3-tenants.json")]
    [Arguments("tenant-package", "tenant-package", "tenant-package.json")]
    [Arguments("operator-identity", "operator-identity", "operator-identity.json")]
    [Arguments("legal-drafts", "legal-drafts", "legal-drafts.json")]
    public async Task ExternalTargetProjectsRestrictedBytesMatchingIndependentHostVectors(
        string artifactId, string answerName, string expectedName)
    {
        Definition definition = DefinitionCodec.ParseJson(Resource("EventDefinition"));
        AnswerSnapshot snapshot = AnswerSnapshotCodec.ParseJson(
            Resource("EventAnswers." + answerName + ".json"), 7);
        DefinitionArtifactResult result = DefinitionArtifactComposer.Compose(definition, artifactId, snapshot);

        await Assert.That(result.Succeeded).IsTrue();
        await Assert.That(result.Artifact!.DefinitionDigest).IsEqualTo(definition.Digest);
        await Assert.That(result.Artifact.Sensitivity).IsEqualTo(ArtifactSensitivity.Restricted);
        await Assert.That(result.Artifact.Bytes.Span.SequenceEqual(Resource("EventExpected." + expectedName))).IsTrue();
        string? directory = System.Environment.GetEnvironmentVariable("CONFIGURATOR_EVENT_ARTIFACT_DIRECTORY");
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
            await File.WriteAllBytesAsync(Path.Combine(directory, expectedName), result.Artifact.Bytes.ToArray());
        }
        result.Artifact.Dispose();
    }

    [Test]
    public async Task RepeatedCanonicalDeliveryResolvesToOneImmutableDefinition()
    {
        Definition native = DefinitionCodec.ParseJson(Resource("EventDefinition"));
        Definition redelivered = DefinitionCodec.ParseJson(native.CanonicalBytes);
        var registry = new DefinitionRegistry([native, redelivered]);

        await Assert.That(native.Digest).IsEqualTo(redelivered.Digest);
        await Assert.That(registry.Definitions.Count).IsEqualTo(1);
        await Assert.That(registry.Resolve(native.FamilyId).Digest).IsEqualTo(native.Digest);
    }

    private static byte[] Resource(string name)
    {
        using Stream stream = typeof(ExternalDefinitionConformanceTests).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException("External fixture resource missing: " + name);
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }
}
