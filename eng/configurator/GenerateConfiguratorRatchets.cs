#:property RestorePackagesWithLockFile=false
#:property AssemblyName=Configurator.RatchetGenerator
#:property RootNamespace=Configurator.Engineering
#:project ../../src/Configurator.Core/Configurator.Core.csproj
#:project ../../src/Configurator.Cli/Configurator.Cli.csproj

using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Configurator.Core.Definitions;
using Configurator.Cli;

if (args is not ["--write"] and not ["--check"])
{
    Console.Error.WriteLine("Usage: GenerateConfiguratorRatchets.cs (--write|--check)");
    return 64;
}

string root = Directory.GetCurrentDirectory();
if (!File.Exists(Path.Combine(root, "Configurator.slnx")))
    throw new InvalidOperationException("Run from the Configurator repository root.");

string outputRoot = Path.Combine(root, "eng/configurator/generated");
var outputs = new Dictionary<string, JsonObject>(StringComparer.Ordinal)
{
    ["browser-release-capabilities.json"] = new()
    {
        ["schemaVersion"] = 1,
        ["target"] = "browser",
        ["targetEnabled"] = true,
        ["capabilities"] = new JsonObject
        {
            ["publicMetadata"] = true, ["secretEntry"] = true,
            ["secretEntryRequiresWorkspaceConsent"] = true,
            ["privateExport"] = true, ["privateExportRequiresUserAction"] = true,
            ["persistence"] = false, ["answerPersistence"] = false,
            ["publicAssetCache"] = true, ["explicitPublicDefinitionCache"] = true,
            ["nativeProtectedSave"] = false,
            ["targetAuthentication"] = false, ["targetApply"] = false,
            ["telemetry"] = false
        }
    },
    ["frozen-contract-baseline.json"] = new()
    {
        ["schemaVersion"] = 1,
        ["definitionSchemaSha256"] = Hash(Encoding.UTF8.GetBytes(DefinitionSchema.Generate())),
        ["authoringSchemaSha256"] = Hash(Encoding.UTF8.GetBytes(Configurator.Core.Definitions.Authoring.DefinitionAuthoringSchema.Generate())),
        ["commandSchemaSha256"] = Hash(SetupCliCommandSchemaMetadata.GenerateSchema()),
        ["commandFamilies"] = new JsonArray(SetupCliCommandSchemaMetadata.Families.Select(value => (JsonNode?)JsonValue.Create(value)).ToArray()),
        ["targetApply"] = false,
        ["signedPublicationEnabled"] = false
    }
};

var surfaces = new JsonObject();
foreach (string surface in new[] { "cli", "terminal", "desktop" })
{
    string temporary = Path.Combine(Path.GetTempPath(), $"configurator-contract-{Guid.NewGuid():N}.json");
    try
    {
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false };
        foreach (string argument in new[]
        {
            "msbuild", "eng/configurator/Configurator.Release.proj", "-nologo", "-verbosity:quiet",
            "-target:WriteConfiguratorReleaseContract", $"-property:ConfiguratorSurface={surface}",
            $"-property:ConfiguratorContractOutput={temporary}"
        }) start.ArgumentList.Add(argument);
        using Process process = Process.Start(start)!;
        process.WaitForExit();
        if (process.ExitCode != 0) return process.ExitCode;
        surfaces[surface] = JsonNode.Parse(File.ReadAllText(temporary));
    }
    finally { if (File.Exists(temporary)) File.Delete(temporary); }
}
outputs["native-release-capabilities.json"] = new JsonObject
{
    ["schemaVersion"] = 1, ["surfaces"] = surfaces,
    ["protectedExportRequiresSupportedFilesystem"] = true,
    ["targetApply"] = false, ["signedPublicationEnabled"] = false
};

foreach ((string name, JsonObject value) in outputs)
{
    value["_metadata"] = new JsonObject { ["generatedBy"] = "eng/configurator/GenerateConfiguratorRatchets.cs" };
    byte[] bytes = Encoding.UTF8.GetBytes(value.ToJsonString(new JsonSerializerOptions { WriteIndented = true })
        .Replace("\r\n", "\n", StringComparison.Ordinal) + "\n");
    string path = Path.Combine(outputRoot, name);
    if (args[0] == "--check")
    {
        if (!File.Exists(path) || !File.ReadAllBytes(path).AsSpan().SequenceEqual(bytes))
        {
            Console.Error.WriteLine($"Configurator ratchet missing or stale: {name}");
            return 1;
        }
    }
    else
    {
        Directory.CreateDirectory(outputRoot);
        File.WriteAllBytes(path, bytes);
    }
}
Console.WriteLine(args[0] == "--check" ? "Configurator ratchets are current (3/3)." : "Generated Configurator ratchets (3/3).");
return 0;

static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
