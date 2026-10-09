using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.Json;
using Configurator.Artifacts;
using Configurator.Cli;
using Configurator.Core;
using Configurator.Core.Artifacts;
using Configurator.Core.Definitions;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Configurator.Architecture.Tests;

public sealed class StandaloneArchitectureTests
{
    [Test]
    [Arguments("Core")]
    [Arguments("Artifacts")]
    [Arguments("Presentation")]
    [Arguments("Cli")]
    [Arguments("Terminal")]
    [Arguments("Desktop")]
    [Arguments("Browser")]
    public async Task EvaluatedSourceAndProjectInputsRemainInsideConfigurator(string owner)
    {
        var result = await ReleaseTestPaths.Run("dotnet", "msbuild",
            $"src/Configurator.{owner}/Configurator.{owner}.csproj", "-nologo",
            "-verbosity:quiet", "-getItem:Compile,ProjectReference,EmbeddedResource");
        await Assert.That(result.ExitCode).IsEqualTo(0).Because(result.Output);
        using JsonDocument evaluation = JsonDocument.Parse(result.Output);
        string rootPrefix = Path.TrimEndingDirectorySeparator(ReleaseTestPaths.Root) + Path.DirectorySeparatorChar;
        foreach (JsonProperty group in evaluation.RootElement.GetProperty("Items").EnumerateObject())
            foreach (JsonElement item in group.Value.EnumerateArray())
                await Assert.That(item.GetProperty("FullPath").GetString()!.StartsWith(rootPrefix, StringComparison.Ordinal))
                    .IsTrue().Because($"Evaluated {group.Name} for {owner} escaped Configurator.");
    }

    [Test]
    [Arguments("Core")]
    [Arguments("Artifacts")]
    [Arguments("Presentation")]
    [Arguments("Cli")]
    [Arguments("Terminal")]
    [Arguments("Desktop")]
    [Arguments("Browser")]
    public async Task CompiledProductClosureHasNoEventOrTestDependency(string owner)
    {
        string path = Path.Combine(ReleaseTestPaths.Root, $"src/Configurator.{owner}/bin/Release/net10.0/Configurator.{owner}.dll");
        using var stream = File.OpenRead(path);
        using var pe = new PEReader(stream);
        MetadataReader metadata = pe.GetMetadataReader();
        await Assert.That(metadata.GetString(metadata.GetAssemblyDefinition().Name)).IsEqualTo($"Configurator.{owner}");
        string[] references = metadata.AssemblyReferences.Select(handle => metadata.GetString(metadata.GetAssemblyReference(handle).Name)).ToArray();
        await Assert.That(references.Where(name => name.StartsWith("Event.", StringComparison.Ordinal)
            || name.StartsWith("Explore.", StringComparison.Ordinal)
            || name.Contains("Tests", StringComparison.Ordinal)
            || name.StartsWith("TUnit", StringComparison.Ordinal)
            || name is "Avalonia.Headless" or "Avalonia.Fonts.Inter")).IsEmpty();
        if (owner == "Core")
            await Assert.That(references.Where(name => !name.StartsWith("System.", StringComparison.Ordinal)
                && name is not ("System" or "netstandard" or "YamlDotNet"))).IsEmpty();
        if (owner == "Browser")
        {
            await Assert.That(references.Where(name => name.StartsWith("Configurator.", StringComparison.Ordinal)))
                .IsEquivalentTo(["Configurator.Core", "Configurator.Presentation"]);
            await Assert.That(references.Any(name => name.StartsWith("Avalonia", StringComparison.Ordinal)
                || name is "Terminal.Gui" or "ISLAMU.Terminal.Gui" or "Spectre.Console")).IsFalse();
        }
        if (owner == "Presentation")
            await Assert.That(references.Where(name => name.StartsWith("Configurator.", StringComparison.Ordinal)))
                .IsEquivalentTo(["Configurator.Core"]);
    }

    [Test]
    public async Task NeutralDefinitionComposesWithoutEventAndRemainsRestricted()
    {
        SetupDefinition definition = SetupDefinitionCodec.ParseJson("""
            {"schemaVersion":1,"familyId":"9284f310-9901-411b-8024-c14444dce5db","revision":1,
             "applicationId":"neutral.service","applicationVersions":["1"],"publisher":"Example",
             "title":"Neutral","entryPoints":["setup"],"locales":["en"],
             "fields":[{"id":"mode","type":"enum","title":"Mode","sensitivity":"public","disclosure":"essential","choices":["local","remote"],"required":true}],
             "artifacts":[{"id":"config","format":"json","fileName":"service.json","applySemantics":"create","restart":"process",
                "projection":{"op":"object","members":{"mode":{"op":"copy","field":"mode"}}}}]}
            """u8.ToArray());
        var answers = new SetupAnswerSnapshot(1, new Dictionary<string, JsonElement>
        {
            ["mode"] = JsonSerializer.SerializeToElement("local")
        });
        DefinitionArtifactResult result = DefinitionArtifactComposer.Compose(definition, "config", answers);
        await Assert.That(result.Succeeded).IsTrue();
        await Assert.That(result.Artifact!.Sensitivity).IsEqualTo(SetupArtifactSensitivity.Restricted);
        using var publicStream = new MemoryStream();
        await Assert.That(() => ProtectedArtifactWriter.WritePublic(publicStream, SetupArtifactKind.Configuration, result.Artifact.Bytes))
            .Throws<IOException>();
        await Assert.That(publicStream.Length).IsEqualTo(0);
        await Assert.That(new ProtectedArtifactWriter().IsAvailable).IsEqualTo(OperatingSystem.IsLinux());
    }

    [Test]
    public async Task GeneratedContractMatchesCompiledSchemas()
    {
        using JsonDocument baseline = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(ReleaseTestPaths.Root, "eng/configurator/generated/frozen-contract-baseline.json")));
        await Assert.That(baseline.RootElement.GetProperty("definitionSchemaSha256").GetString())
            .IsEqualTo(ReleaseTestPaths.Hash(System.Text.Encoding.UTF8.GetBytes(DefinitionSchema.Generate())));
        await Assert.That(baseline.RootElement.GetProperty("commandSchemaSha256").GetString())
            .IsEqualTo(ReleaseTestPaths.Hash(SetupCliCommandSchemaMetadata.GenerateSchema()));
        await Assert.That(File.ReadAllText(Path.Combine(ReleaseTestPaths.Root, "schemas/setup-definition-v1.schema.json")))
            .IsEqualTo(DefinitionSchema.Generate());
        await Assert.That(File.ReadAllBytes(Path.Combine(ReleaseTestPaths.Root, SetupCliCommandSchemaMetadata.RelativePath)))
            .IsEquivalentTo(SetupCliCommandSchemaMetadata.GenerateSchema());
        await Assert.That(baseline.RootElement.GetProperty("signedPublicationEnabled").GetBoolean()).IsFalse();
    }
}
