using Configurator.Core.Artifacts;
using Configurator.Core.Definitions;

namespace Configurator.Core.Tests.Definitions;

public sealed class DefinitionSensitivityTests
{
    [Test]
    public async Task PublicLabelsNeverDemoteAnswerDerivedArtifacts()
    {
        DefinitionArtifactResult result = DefinitionArtifactComposer.Compose(
            DefinitionFixture.Parse(), "config", DefinitionFixture.Answers());
        await Assert.That(result.Artifact!.Sensitivity).IsEqualTo(SetupArtifactSensitivity.Restricted);
        await Assert.That(SetupArtifactPolicy.TryProjectPublic(SetupArtifactKind.Configuration,
            result.Artifact.Bytes, out var bytes)).IsFalse();
        await Assert.That(bytes.IsEmpty).IsTrue();
    }

    [Test]
    public async Task DiagnosticsAndStringRepresentationsNeverEchoAnswers()
    {
        const string marker = "private-answer-marker";
        SetupDefinition definition = DefinitionFixture.Parse(DefinitionFixture.Json.Replace(
            "\"maxLength\":80", "\"maxLength\":2", StringComparison.Ordinal));
        var answers = DefinitionFixture.Answers($$"""{"service.name":"{{marker}}"}""");
        DefinitionArtifactResult result = DefinitionArtifactComposer.Compose(definition, "config", answers);
        await Assert.That(result.Succeeded).IsFalse();
        await Assert.That(result.Diagnostics.Count).IsGreaterThan(0);
        await Assert.That(string.Join("", result.Diagnostics).Contains(marker, StringComparison.Ordinal)).IsFalse();
        await Assert.That(answers.ToString().Contains(marker, StringComparison.Ordinal)).IsFalse();
    }
}
