using Configurator.Core.Artifacts;
using Configurator.Core.Definitions;

namespace Configurator.Core.Tests.Definitions;

public sealed class GrafanaDefinitionConformanceTests
{
    [Test]
    [Arguments("environment", "GrafanaExpected.Environment", "22f36118ea95ec3e98322c8d3700b725b37c54e792dc35ff1b7061a575093a0e")]
    [Arguments("datasource", "GrafanaExpected.Datasource", "0f097bbcc96b617a693d6237fcbc0a09a1463ffc16d6ad0b2452071535721a8b")]
    public async Task SyntheticAnswersProducePinnedRestrictedBytes(string artifactId, string expectedResource, string digest)
    {
        SetupDefinition definition = SetupDefinitionCodec.ParseJson(Resource("GrafanaDefinition"));
        SetupAnswerSnapshot snapshot = SetupAnswerSnapshotCodec.ParseJson(Resource("GrafanaAnswers"), 1);

        DefinitionArtifactResult result = DefinitionArtifactComposer.Compose(definition, artifactId, snapshot);

        await Assert.That(result.Succeeded).IsTrue();
        await Assert.That(result.Artifact!.Bytes.Span.SequenceEqual(Resource(expectedResource))).IsTrue();
        await Assert.That(result.Artifact.Digest.Value).IsEqualTo(digest);
        await Assert.That(result.Artifact.DefinitionDigest).IsEqualTo(definition.Digest);
        await Assert.That(result.Artifact.Sensitivity).IsEqualTo(SetupArtifactSensitivity.Restricted);
    }

    private static byte[] Resource(string name)
    {
        using Stream input = typeof(GrafanaDefinitionConformanceTests).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException("Example resource missing: " + name);
        using var output = new MemoryStream();
        input.CopyTo(output);
        return output.ToArray();
    }
}
