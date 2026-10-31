#:property RestorePackagesWithLockFile=false
#:property JsonSerializerIsReflectionEnabledByDefault=true
#:property AssemblyName=Configurator.ImportComparisonFixtures
#:property RootNamespace=Configurator.Engineering
#:project ../../src/Configurator.Core/Configurator.Core.csproj

using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Configurator.Core.Definitions;

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: PrepareImportComparisonFixtures.cs <new-output-directory>");
    return 64;
}

string output = Path.GetFullPath(args[0]);
Directory.CreateDirectory(output);
var properties = new JsonObject();
for (int index = 0; index < 8; index++) properties.Add("p" + index, new JsonObject { ["type"] = "integer" });
JsonObject maximum = Source(new JsonObject
{
    ["type"] = "array",
    ["items"] = new JsonObject
    {
        ["type"] = "object", ["additionalProperties"] = false, ["properties"] = properties
    }
});
var rows = new JsonArray();
for (int index = 0; index < 4096; index++)
{
    var row = new JsonObject();
    for (int property = 0; property < 8; property++)
        row.Add("p" + property, index == 0 && property == 0 ? JsonValue.Create("synthetic-invalid") : JsonValue.Create(1));
    rows.Add((JsonNode)row);
}
WriteDefinition("maximum", maximum);
Write("maximum-input.json", Encoding.UTF8.GetBytes(new JsonObject { ["rows"] = rows }.ToJsonString()));

JsonObject findings = Source(new JsonObject { ["type"] = "array", ["items"] = new JsonObject { ["type"] = "integer" } });
var invalid = new JsonArray();
for (int index = 0; index < 3000; index++) invalid.Add((JsonNode)JsonValue.Create("synthetic-private-value")!);
WriteDefinition("findings", findings);
Write("findings-input.json", Encoding.UTF8.GetBytes(new JsonObject { ["rows"] = invalid }.ToJsonString()));
Write("malformed-input.json", """{"synthetic-obsolete-credential":"synthetic-private-value" """u8.ToArray());
Write("unknown-input.json", """{"synthetic-obsolete-credential":"synthetic-private-value"}"""u8.ToArray());
return 0;

JsonObject Source(JsonObject schema) => new()
{
    ["$schema"] = "urn:islamu:configurator:authoring:v2",
    ["identity"] = new JsonObject { ["familyId"] = Guid.NewGuid().ToString(), ["revision"] = 1 },
    ["project"] = new JsonObject { ["id"] = "comparison-probe", ["name"] = "Synthetic comparison probe" },
    ["applicationVersions"] = new JsonArray("1"),
    ["configuration"] = new JsonObject
    {
        ["type"] = "object", ["additionalProperties"] = false,
        ["x-configurator"] = new JsonObject { ["sensitivity"] = "public" },
        ["properties"] = new JsonObject { ["rows"] = schema }
    },
    ["outputs"] = new JsonObject
    {
        ["config.json"] = new JsonObject
        {
            ["id"] = "config", ["format"] = "json",
            ["projection"] = new JsonObject
            {
                ["op"] = "object", ["members"] = new JsonObject
                {
                    ["rows"] = new JsonObject
                    {
                        ["op"] = "copy",
                        ["reference"] = new JsonObject { ["scope"] = "root", ["path"] = "/rows" }
                    }
                }
            }
        }
    }
};

void WriteDefinition(string name, JsonObject source)
{
    byte[] bytes = Encoding.UTF8.GetBytes(source.ToJsonString());
    SetupDefinition definition = SetupDefinitionSourceCodec.ParseJson(bytes);
    Write(name + "-source.json", bytes);
    Write(name + "-definition.json", definition.CanonicalBytes.ToArray());
}

void Write(string name, byte[] bytes)
{
    using var stream = new FileStream(Path.Combine(output, name), FileMode.CreateNew, FileAccess.Write, FileShare.None);
    stream.Write(bytes);
    Console.WriteLine($"COMPARISON_FIXTURE name={name} bytes={bytes.Length} sha256={Convert.ToHexStringLower(SHA256.HashData(bytes))}");
}
