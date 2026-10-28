using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.Json;
using Configurator.Artifacts;
using Configurator.Cli;
using Configurator.Core;
using Configurator.Core.Artifacts;
using Configurator.Core.Definitions;
using Configurator.Core.Definitions.Authoring;
using Configurator.Presentation;
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
    [Arguments("Browser.Host")]
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
    [Arguments("Browser.Host")]
    public async Task CompiledProductClosureHasNoEventOrTestDependency(string owner)
    {
        string path = Path.Combine(ReleaseTestPaths.Root, $"src/Configurator.{owner}/bin/Release/net10.0/Configurator.{owner}.dll");
        using var stream = File.OpenRead(path);
        using var pe = new PEReader(stream);
        MetadataReader metadata = pe.GetMetadataReader();
        await Assert.That(metadata.GetString(metadata.GetAssemblyDefinition().Name)).IsEqualTo($"Configurator.{owner}");
        string[] references = metadata.AssemblyReferences.Select(handle => metadata.GetString(metadata.GetAssemblyReference(handle).Name)).ToArray();
        await Assert.That(FirstPartyDependencyViolations(owner, metadata)).IsEmpty();
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
            await Assert.That(references.Any(name => name.StartsWith("Avalonia", StringComparison.Ordinal)
                || name is "Terminal.Gui" or "Spectre.Console")).IsFalse();
        }
    }

    [Test]
    [Arguments("Core", typeof(ProtectedArtifactWriter), "Configurator.Artifacts")]
    [Arguments("Artifacts", typeof(SetupPresentationWorkspace), "Configurator.Presentation")]
    [Arguments("Presentation", typeof(ProtectedArtifactWriter), "Configurator.Artifacts")]
    [Arguments("Cli", typeof(SetupPresentationWorkspace), "Configurator.Presentation")]
    [Arguments("Terminal", typeof(SetupCliCommandSchemaMetadata), "Configurator.Cli")]
    [Arguments("Desktop", typeof(SetupCliCommandSchemaMetadata), "Configurator.Cli")]
    [Arguments("Browser", typeof(ProtectedArtifactWriter), "Configurator.Artifacts")]
    [Arguments("Browser.Host", typeof(ProtectedArtifactWriter), "Configurator.Artifacts")]
    public async Task CompiledDependencyGuardRejectsForbiddenLocalEdges(
        string owner, Type forbiddenDependency, string forbiddenAssembly)
    {
        Type[] allowedTypes = AllowedFirstPartyDependencies(owner).Select(name => name switch
        {
            "Configurator.Core" => typeof(SetupDefinition),
            "Configurator.Artifacts" => typeof(ProtectedArtifactWriter),
            "Configurator.Presentation" => typeof(SetupPresentationWorkspace),
            _ => throw new InvalidOperationException($"No compiled canary type for {name}.")
        }).ToArray();
        using var allowedImage = CompileDependencyCanary(owner, allowedTypes);
        using var allowedPe = new PEReader(allowedImage);
        await Assert.That(FirstPartyDependencyViolations(owner, allowedPe.GetMetadataReader())).IsEmpty();

        using var forbiddenImage = CompileDependencyCanary(owner, allowedTypes.Append(forbiddenDependency));
        using var forbiddenPe = new PEReader(forbiddenImage);
        MetadataReader metadata = forbiddenPe.GetMetadataReader();
        await Assert.That(metadata.AssemblyReferences
            .Select(handle => metadata.GetString(metadata.GetAssemblyReference(handle).Name)))
            .Contains(forbiddenAssembly);
        await Assert.That(FirstPartyDependencyViolations(owner, metadata))
            .IsEquivalentTo([$"Configurator.{owner} -> {forbiddenAssembly} is forbidden"]);
    }

    private static string[] AllowedFirstPartyDependencies(string owner) => owner switch
    {
        "Core" or "Browser.Host" => [],
        "Artifacts" => ["Configurator.Core"],
        "Presentation" => ["Configurator.Core"],
        "Cli" => ["Configurator.Core", "Configurator.Artifacts"],
        "Terminal" => ["Configurator.Core", "Configurator.Artifacts", "Configurator.Presentation"],
        "Desktop" => ["Configurator.Core", "Configurator.Artifacts", "Configurator.Presentation"],
        "Browser" => ["Configurator.Core", "Configurator.Presentation"],
        _ => throw new ArgumentOutOfRangeException(nameof(owner), owner, "Unknown product owner.")
    };

    private static string[] FirstPartyDependencyViolations(string owner, MetadataReader metadata)
    {
        string[] allowed = AllowedFirstPartyDependencies(owner);
        string[] actual = metadata.AssemblyReferences
            .Select(handle => metadata.GetString(metadata.GetAssemblyReference(handle).Name))
            .Where(name => name.StartsWith("Configurator.", StringComparison.Ordinal)).ToArray();
        return actual.Except(allowed, StringComparer.Ordinal)
            .Select(name => $"Configurator.{owner} -> {name} is forbidden")
            .Concat(allowed.Except(actual, StringComparer.Ordinal)
                .Select(name => $"Configurator.{owner} -> {name} is missing")).ToArray();
    }

    private static MemoryStream CompileDependencyCanary(string owner, IEnumerable<Type> dependencies)
    {
        var assembly = new PersistedAssemblyBuilder(new AssemblyName($"Configurator.{owner}"), typeof(object).Assembly);
        ModuleBuilder module = assembly.DefineDynamicModule("DependencyCanary");
        TypeBuilder type = module.DefineType("DependencyCanary", TypeAttributes.Public);
        int index = 0;
        foreach (Type dependency in dependencies)
            type.DefineField($"Dependency{index++}", dependency, FieldAttributes.Public);
        type.CreateType();
        var image = new MemoryStream();
        assembly.Save(image);
        image.Position = 0;
        return image;
    }

    [Test]
    public async Task NeutralDefinitionComposesWithoutEventAndRemainsRestricted()
    {
        SetupDefinition definition = SetupDefinitionCodec.ParseJson("""
            {"schemaVersion":2,"profile":"bounded-v2","familyId":"9284f310-9901-411b-8024-c14444dce5db","revision":1,
             "project":{"id":"neutral.service","name":"Neutral","publisher":"Example"},
             "applicationVersions":["1"],"entryPoints":["setup"],"locales":["en"],
             "requiredCapabilities":["projection.copy","projection.object","schema.enum","schema.object","schema.string"],
             "configuration":{"type":"object","additionalProperties":false,"required":["mode"],
                "x-configurator":{"sensitivity":"public"},"properties":{
                   "mode":{"type":"string","title":"Mode","enum":["local","remote"],
                      "x-configurator":{"sensitivity":"public","detail":"basic"}}}},
             "artifacts":[{"id":"config","format":"json","fileName":"service.json",
                "targetUpdateMode":"create","restartRequirement":"process",
                "projection":{"op":"object","members":{"mode":{"op":"copy","reference":{"scope":"root","path":"/mode"}}}}}]}
            """u8.ToArray());
        var answers = new SetupAnswerSnapshot(1, new Dictionary<string, JsonElement>
        {
            ["mode"] = JsonSerializer.SerializeToElement("local")
        });
        DefinitionArtifactResult result = DefinitionArtifactComposer.Compose(definition, "config", answers);
        await Assert.That(result.Succeeded).IsTrue();
        await Assert.That(result.Artifact!.Bytes.Span.SequenceEqual("""{"mode":"local"}"""u8)).IsTrue();
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
        await Assert.That(baseline.RootElement.GetProperty("authoringSchemaSha256").GetString())
            .IsEqualTo(ReleaseTestPaths.Hash(System.Text.Encoding.UTF8.GetBytes(DefinitionAuthoringSchema.Generate())));
        await Assert.That(baseline.RootElement.GetProperty("commandSchemaSha256").GetString())
            .IsEqualTo(ReleaseTestPaths.Hash(SetupCliCommandSchemaMetadata.GenerateSchema()));
        await Assert.That(File.ReadAllText(Path.Combine(ReleaseTestPaths.Root, "schemas/setup-definition-v2.schema.json")))
            .IsEqualTo(DefinitionSchema.Generate());
        await Assert.That(File.ReadAllText(Path.Combine(ReleaseTestPaths.Root, "schemas/configurator-authoring-v2.schema.json")))
            .IsEqualTo(DefinitionAuthoringSchema.Generate());
        await Assert.That(File.ReadAllText(Path.Combine(ReleaseTestPaths.Root, "schemas/configurator-answers-v1.schema.json")))
            .IsEqualTo(DefinitionSchema.GenerateAnswers());
        await Assert.That(File.ReadAllBytes(Path.Combine(ReleaseTestPaths.Root, SetupCliCommandSchemaMetadata.RelativePath)))
            .IsEquivalentTo(SetupCliCommandSchemaMetadata.GenerateSchema());
        await Assert.That(baseline.RootElement.GetProperty("signedPublicationEnabled").GetBoolean()).IsFalse();
    }
}
