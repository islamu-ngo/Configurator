using System.Text.Json;
using Configurator.Core.Artifacts;
using Configurator.Core.Definitions;

namespace Configurator.Core.Tests.Definitions;

public sealed class ExternalDefinitionConformanceTests
{
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
        SetupDefinition definition = SetupDefinitionCodec.ParseJson(Resource("EventDefinition"));
        using JsonDocument answers = JsonDocument.Parse(Resource("EventAnswers." + answerName + ".json"));
        var snapshot = new SetupAnswerSnapshot(7, answers.RootElement.EnumerateObject()
            .ToDictionary(item => item.Name, item => item.Value.Clone(), StringComparer.Ordinal));
        DefinitionArtifactResult result = DefinitionArtifactComposer.Compose(definition, artifactId, snapshot);

        await Assert.That(result.Succeeded).IsTrue();
        await Assert.That(result.Artifact!.DefinitionDigest).IsEqualTo(definition.Digest);
        await Assert.That(result.Artifact.Sensitivity).IsEqualTo(SetupArtifactSensitivity.Restricted);
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
        SetupDefinition native = SetupDefinitionCodec.ParseJson(Resource("EventDefinition"));
        SetupDefinition redelivered = SetupDefinitionCodec.ParseJson(native.CanonicalBytes);
        var registry = new SetupDefinitionRegistry([native, redelivered]);

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
