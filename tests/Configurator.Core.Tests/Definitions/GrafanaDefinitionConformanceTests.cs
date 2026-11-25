using Configurator.Core.Artifacts;
using Configurator.Core.Definitions;
using Configurator.Core.Definitions.Authoring;

namespace Configurator.Core.Tests.Definitions;

public sealed class GrafanaDefinitionConformanceTests
{
    [Test]
    [Arguments("environment", "GrafanaExpected.Environment", "22f36118ea95ec3e98322c8d3700b725b37c54e792dc35ff1b7061a575093a0e")]
    [Arguments("datasource", "GrafanaExpected.Datasource", "0f097bbcc96b617a693d6237fcbc0a09a1463ffc16d6ad0b2452071535721a8b")]
    public async Task SyntheticAnswersProducePinnedRestrictedBytes(string artifactId, string expectedResource, string digest)
    {
        Definition definition = DefinitionCodec.ParseJson(Resource("GrafanaDefinition"));
        AnswerSnapshot snapshot = AnswerSnapshotCodec.ParseJson(Resource("GrafanaAnswers"), 1);

        DefinitionArtifactResult result = DefinitionArtifactComposer.Compose(definition, artifactId, snapshot);

        await Assert.That(result.Succeeded).IsTrue();
        await Assert.That(result.Artifact!.Bytes.Span.SequenceEqual(Resource(expectedResource))).IsTrue();
        await Assert.That(result.Artifact.Digest.Value).IsEqualTo(digest);
        await Assert.That(result.Artifact.DefinitionDigest).IsEqualTo(definition.Digest);
        await Assert.That(result.Artifact.Sensitivity).IsEqualTo(ArtifactSensitivity.Restricted);
    }

    [Test]
    public async Task GrafanaDefinitionPassesStrictQualityLintAndSyntheticFixtures()
    {
        Definition definition = DefinitionCodec.ParseJson(Resource("GrafanaDefinition"));
        await Assert.That(DefinitionQualityLinter.Lint(definition)).IsEmpty();

        string repoRoot = FindRepositoryRoot();
        string fixturesPath = Path.Combine(repoRoot, "examples", "grafana-13.2.2", "fixtures.json");
        await Assert.That(File.Exists(fixturesPath)).IsTrue();
        DefinitionFixtureDocument suite = DefinitionFixtureDocument.ParseJson(File.ReadAllBytes(fixturesPath));
        DefinitionFixtureRunReport report = DefinitionFixtureRunner.Run(definition, suite);
        await Assert.That(report.Passed).IsTrue();
        await Assert.That(report.TotalCases).IsGreaterThanOrEqualTo(3);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Configurator.slnx")))
            current = current.Parent;
        return current?.FullName ?? throw new DirectoryNotFoundException("Configurator.slnx not found.");
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
