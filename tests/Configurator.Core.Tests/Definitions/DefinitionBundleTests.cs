using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Configurator.Core.Artifacts;
using Configurator.Core.Composition;
using Configurator.Core.Definitions;

namespace Configurator.Core.Tests.Definitions;

public sealed class DefinitionBundleTests
{
    [Test]
    public async Task DisposalClearsCoreOwnedRestrictedBytesAndRejectsLaterReads()
    {
        using DefinitionBundleResult bundle = DefinitionBundleComposer.Compose(Definition(3),
            ["config", "dotenv1", "yaml2"], DefinitionFixture.Answers());
        byte[][] owned = bundle.Artifacts.Select(artifact =>
            (byte[])typeof(DefinitionArtifact).GetField("_bytes", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(artifact)!).ToArray();
        await Assert.That(owned.All(bytes => bytes.Any(value => value != 0))).IsTrue();
        await Assert.That(bundle.Artifacts[0].ByteLength).IsEqualTo(17);

        bundle.Dispose();
        bundle.Dispose();

        await Assert.That(owned.All(bytes => bytes.All(value => value == 0))).IsTrue();
        foreach (DefinitionArtifact artifact in bundle.Artifacts)
            await Assert.That(() => artifact.Bytes).Throws<ObjectDisposedException>();
    }

    [Test]
    public async Task DeclaredFormatsUseOneSnapshotAndDefinitionOrder()
    {
        SetupDefinition definition = Definition(3);
        DefinitionBundleResult result = DefinitionBundleComposer.Compose(definition,
            ["yaml2", "config", "dotenv1"], DefinitionFixture.Answers());

        await Assert.That(result.Succeeded).IsTrue();
        await Assert.That(string.Join(',', result.Artifacts.Select(item => item.ArtifactId)))
            .IsEqualTo("config,dotenv1,yaml2");
        await Assert.That(Encoding.UTF8.GetString(result.Artifacts[0].Bytes.Span)).IsEqualTo("""{"name":"sample"}""");
        await Assert.That(Encoding.UTF8.GetString(result.Artifacts[1].Bytes.Span)).IsEqualTo("NAME=sample\n");
        await Assert.That(Encoding.UTF8.GetString(result.Artifacts[2].Bytes.Span)).IsEqualTo("\"name\": \"sample\"\n");
        foreach (DefinitionArtifact artifact in result.Artifacts)
        {
            await Assert.That(artifact.InputRevision).IsEqualTo(7L);
            await Assert.That(artifact.DefinitionDigest).IsEqualTo(definition.Digest);
            await Assert.That(artifact.Digest).IsEqualTo(ArtifactDigest.Compute(artifact.Bytes.Span));
            await Assert.That(artifact.Sensitivity).IsEqualTo(SetupArtifactSensitivity.Restricted);
            await Assert.That(artifact.ToString().Contains("sample", StringComparison.Ordinal)).IsFalse();
        }
    }

    [Test]
    public async Task InvalidLaterArtifactLeavesNoPublishablePrefix()
    {
        JsonNode root = JsonNode.Parse(DefinitionFixture.Json)!;
        root["artifacts"]!.AsArray().Add(JsonNode.Parse("""
            {"id":"bad","format":"dotenv","fileName":"bad.env","applySemantics":"create",
             "restart":"none","projection":{"op":"object","members":{"nested":{"op":"object","members":{}}}}}
            """));
        DefinitionBundleResult result = DefinitionBundleComposer.Compose(DefinitionFixture.Parse(root.ToJsonString()),
            ["config", "bad"], DefinitionFixture.Answers());
        await Assert.That(result.Succeeded).IsFalse();
        await Assert.That(result.Artifacts).IsEmpty();
        await Assert.That(result.Diagnostics.Single().Code).IsEqualTo("dotenv-shape");
        await Assert.That(result.ToString().Contains("sample", StringComparison.Ordinal)).IsFalse();
    }

    [Test]
    public async Task AggregateBytesAreBoundedAcrossIndividuallyValidArtifacts()
    {
        SetupDefinition definition = Definition(64);
        string value = new('x', SetupCompositionLimits.DefaultScalarCharacters);
        string answer = JsonSerializer.Serialize(new Dictionary<string, string> { ["service.name"] = value });
        DefinitionBundleResult result = DefinitionBundleComposer.Compose(definition,
            definition.Artifacts.Select(item => item.Id), DefinitionFixture.Answers(answer));
        await Assert.That(result.Succeeded).IsFalse();
        await Assert.That(result.Artifacts).IsEmpty();
        await Assert.That(result.Diagnostics.Single().Code).IsEqualTo("bundle-limit");
    }

    [Test]
    public async Task OversizedSelectionStopsAtDefinitionArtifactCount()
    {
        DefinitionBundleResult result = DefinitionBundleComposer.Compose(DefinitionFixture.Parse(),
            ["config", "extra"], DefinitionFixture.Answers());
        await Assert.That(result.Artifacts).IsEmpty();
        await Assert.That(result.Diagnostics.Single().Code).IsEqualTo("artifact-limit");
    }

    private static SetupDefinition Definition(int count)
    {
        JsonNode root = JsonNode.Parse(DefinitionFixture.Json)!;
        JsonArray artifacts = root["artifacts"]!.AsArray();
        for (int index = 1; index < count; index++)
        {
            JsonNode artifact = artifacts[0]!.DeepClone();
            artifact["id"] = (index % 3) switch { 1 => "dotenv" + index, 2 => "yaml" + index, _ => "config" + index };
            artifact["format"] = count == 64 ? "json" : (index % 3) switch { 1 => "dotenv", 2 => "yaml", _ => "json" };
            artifact["fileName"] = "output" + index + "." + ((index % 3) switch { 1 => "env", 2 => "yaml", _ => "json" });
            if (count != 64 && index % 3 == 1)
                artifact["projection"]!["members"]!["NAME"] = artifact["projection"]!["members"]!["name"]!.DeepClone();
            if (count != 64 && index % 3 == 1)
                artifact["projection"]!["members"]!.AsObject().Remove("name");
            artifacts.Add(artifact);
        }
        if (count == 64) root["fields"]![0]!["maxLength"] = SetupCompositionLimits.DefaultScalarCharacters;
        return DefinitionFixture.Parse(root.ToJsonString());
    }
}
