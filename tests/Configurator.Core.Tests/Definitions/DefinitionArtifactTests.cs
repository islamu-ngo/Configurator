using System.Text;
using Configurator.Core.Artifacts;
using Configurator.Core.Definitions;

namespace Configurator.Core.Tests.Definitions;

public sealed class DefinitionArtifactTests
{
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
        await Assert.That(validation.Fields.Single().IsVisible(SetupDisplayMode.Guided)).IsTrue();
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
