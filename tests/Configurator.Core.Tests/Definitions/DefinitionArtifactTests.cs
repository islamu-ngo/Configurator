using System.Text;
using Configurator.Core.Artifacts;
using Configurator.Core.Definitions;

namespace Configurator.Core.Tests.Definitions;

public sealed class DefinitionArtifactTests
{
    [Test]
    public async Task CheckedNeutralSampleProducesIndependentNativeBytesAndReadiness()
    {
        DirectoryInfo? root = new(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName,
            "examples", "minimal-self-hosted", "setup-definition.json"))) root = root.Parent;
        string sample = Path.Combine(root?.FullName ?? throw new InvalidOperationException("Neutral sample missing."),
            "examples", "minimal-self-hosted");
        SetupDefinition definition = SetupDefinitionCodec.ParseJson(File.ReadAllBytes(
            Path.Combine(sample, "setup-definition.json")));
        SetupAnswerSnapshot answers = SetupAnswerSnapshotCodec.ParseJson(File.ReadAllBytes(
            Path.Combine(sample, "answers", "complete.json")));

        await Assert.That(DefinitionValidator.Validate(definition,
            DefinitionFixture.Answers("""{"service.name":""}""")).IsReady).IsFalse();
        await Assert.That(DefinitionValidator.Validate(definition, answers).IsReady).IsTrue();
        SetupAnswerSnapshot publicOnly = DefinitionFixture.Answers(
            """{"service.mode":"extended","metrics.enabled":true}""");
        DefinitionValidationResult partial = DefinitionValidator.Validate(definition, publicOnly);
        await Assert.That(partial.IsReady).IsFalse();
        await Assert.That(partial.Fields.Single(field => field.Id == "service.credential").IsRequired).IsTrue();
        await Assert.That(partial.Fields.Single(field => field.Id == "service.credential").IsProvided).IsFalse();
        foreach ((string artifactId, string expectedFile) in new[]
        {
            ("environment", "environment.env"),
            ("configuration", "service.json"),
            ("configuration-yaml", "service.yaml")
        })
        {
            DefinitionArtifactResult result = DefinitionArtifactComposer.Compose(definition, artifactId, answers);
            await Assert.That(result.Succeeded).IsTrue();
            byte[] expected = File.ReadAllBytes(Path.Combine(sample, "expected", expectedFile));
            await Assert.That(result.Artifact!.Bytes.Span.SequenceEqual(expected)).IsTrue();
            await Assert.That(result.Artifact.DefinitionDigest).IsEqualTo(definition.Digest);
            await Assert.That(result.Artifact.Sensitivity).IsEqualTo(SetupArtifactSensitivity.Restricted);
        }
    }

    [Test]
    public async Task NeutralProjectionProducesPinnedJsonBytesAndInputRevision()
    {
        SetupDefinition definition = DefinitionFixture.Parse();
        DefinitionArtifactResult result = DefinitionArtifactComposer.Compose(definition, "config", DefinitionFixture.Answers());
        await Assert.That(result.Succeeded).IsTrue();
        await Assert.That(Encoding.UTF8.GetString(result.Artifact!.Bytes.Span)).IsEqualTo("""{"name":"sample"}""");
        await Assert.That(result.Artifact.InputRevision).IsEqualTo(7L);
        await Assert.That(result.Artifact.DefinitionDigest).IsEqualTo(definition.Digest);
        await Assert.That(result.Artifact.Sensitivity).IsEqualTo(SetupArtifactSensitivity.Restricted);
    }

    [Test]
    public async Task MissingRequiredFieldsRemainVisibleAndCannotRender()
    {
        SetupDefinition definition = DefinitionFixture.Parse();
        DefinitionValidationResult validation = DefinitionValidator.Validate(definition, DefinitionFixture.Answers("{}"));
        await Assert.That(validation.IsReady).IsFalse();
        await Assert.That(validation.Fields.Single().HasError).IsTrue();
        await Assert.That(DefinitionArtifactComposer.Compose(definition, "config", DefinitionFixture.Answers("{}")).Artifact)
            .IsNull();
    }

    [Test]
    public async Task NativeFormatsPreserveValueTypingAndCannotInjectSyntax()
    {
        foreach ((string format, string expected) in new[]
        {
            ("json", """{"NAME":"line # text"}"""),
            ("yaml", "\"NAME\": \"line # text\"\n"),
            ("dotenv", "NAME=\"line # text\"\n")
        })
        {
            SetupDefinition definition = DefinitionFixture.Parse(DefinitionFixture.Json
                .Replace("\"format\":\"json\"", $"\"format\":\"{format}\"", StringComparison.Ordinal)
                .Replace("\"name\":{\"op\"", "\"NAME\":{\"op\"", StringComparison.Ordinal));
            DefinitionArtifactResult result = DefinitionArtifactComposer.Compose(definition, "config",
                DefinitionFixture.Answers("""{"service.name":"line # text"}"""));
            await Assert.That(result.Succeeded).IsTrue();
            await Assert.That(Encoding.UTF8.GetString(result.Artifact!.Bytes.Span)).IsEqualTo(expected);
        }
    }
}
