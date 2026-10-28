using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Configurator.Core.Definitions;
using Configurator.Core.Definitions.Authoring;

if (args is ["--renew-store-families"])
{
    var prepared = new List<(string PreviousSource, string Source, string Runtime, string[] Retired,
        byte[] Bytes, SetupDefinition Definition)>();
    foreach (var (example, previousSource, source, retired) in new[]
    {
        ("examples/minimal-self-hosted/configurator.yaml", "store/minimal-self-hosted/configurator.yaml",
            "store/minimal-self-hosted/configurator.json", new[] { "store/minimal-self-hosted/releases/2.json", "store/minimal-self-hosted/releases/3.json" }),
        ("examples/islamu-event/configurator.json", "store/islamu-event/configurator.json",
            "store/islamu-event/configurator.json", new[] { "store/islamu-event/releases/5.json", "store/islamu-event/releases/6.json" })
    })
    {
        byte[] previousBytes = File.ReadAllBytes(previousSource);
        using JsonDocument previous = JsonDocument.Parse(previousSource.EndsWith(".yaml", StringComparison.Ordinal)
            ? DefinitionProfile.CanonicalizeYaml(previousBytes) : DefinitionProfile.CanonicalizeJson(previousBytes));
        if (previous.RootElement.GetProperty("$schema").GetString() != "urn:islamu:configurator:authoring:v1")
        {
            Console.Error.WriteLine("store-v1-cutover-required");
            return 1;
        }
        Guid previousFamily = previous.RootElement.GetProperty("identity").GetProperty("familyId").GetGuid();
        foreach (string path in retired)
        {
            using JsonDocument history = JsonDocument.Parse(File.ReadAllBytes(path));
            if (history.RootElement.GetProperty("schemaVersion").GetInt32() != 1 ||
                history.RootElement.GetProperty("familyId").GetGuid() != previousFamily)
            {
                Console.Error.WriteLine("store-v1-history-mismatch");
                return 1;
            }
        }
        byte[] exampleBytes = File.ReadAllBytes(example);
        DefinitionAuthoringDocument admitted = example.EndsWith(".yaml", StringComparison.Ordinal)
            ? DefinitionAuthoringCodec.ParseYaml(exampleBytes) : DefinitionAuthoringCodec.ParseJson(exampleBytes);
        JsonObject document = JsonNode.Parse(admitted.CanonicalBytes.Span)!.AsObject();
        JsonElement metadata = previous.RootElement.GetProperty("project");
        if (document["project"]!["id"]!.GetValue<string>() != metadata.GetProperty("id").GetString())
        {
            Console.Error.WriteLine("store-project-mismatch");
            return 1;
        }
        foreach (string member in new[] { "description", "repository", "definitionLicense" })
            document["project"]![member] = metadata.GetProperty(member).GetString();
        byte[] bytes = DefinitionAuthoringIdentityEditor.Fork(
            Encoding.UTF8.GetBytes(document.ToJsonString(new JsonSerializerOptions { WriteIndented = true })), false, Guid.NewGuid());
        SetupDefinition definition = SetupDefinitionSourceCodec.ParseJson(bytes);
        string runtime = Path.Combine(Path.GetDirectoryName(source)!, "releases/1.json");
        if (File.Exists(runtime))
        {
            Console.Error.WriteLine("store-fresh-release-exists");
            return 1;
        }
        prepared.Add((previousSource, source, runtime, retired, bytes, definition));
    }
    foreach (var entry in prepared)
    {
        File.WriteAllBytes(entry.Source, entry.Bytes);
        if (entry.Source != entry.PreviousSource) File.Delete(entry.PreviousSource);
        using (var output = new FileStream(entry.Runtime, FileMode.CreateNew, FileAccess.Write))
            output.Write(entry.Definition.CanonicalBytes.Span);
        foreach (string retired in entry.Retired) File.Delete(retired);
        Console.WriteLine($"{entry.Source}: family={entry.Definition.FamilyId:D}; revision=1; digest={entry.Definition.Digest.Value}");
    }
    return 0;
}
if (args.Length == 0)
{
    Console.Write(DefinitionSchema.Generate());
    return 0;
}
if (args is ["--write-examples"] or ["--renew-example-families"] or ["--check-examples"])
{
    var prepared = new List<(string Source, string Runtime, byte[] Bytes, SetupDefinition Definition)>();
    foreach ((string source, string runtime) in new[]
    {
        ("examples/minimal-self-hosted/configurator.yaml", "examples/minimal-self-hosted/setup-definition.json"),
        ("examples/grafana-13.2.2/configurator.yaml", "examples/grafana-13.2.2/setup-definition.json"),
        ("examples/islamu-event/configurator.json", "examples/islamu-event/definition.json")
    })
    {
        byte[] bytes = File.ReadAllBytes(source);
        bool yaml = source.EndsWith(".yaml", StringComparison.Ordinal);
        if (args[0] == "--renew-example-families")
        {
            bytes = DefinitionAuthoringIdentityEditor.Fork(bytes, yaml, Guid.NewGuid());
        }
        SetupDefinition definition = yaml ? SetupDefinitionSourceCodec.ParseYaml(bytes) : SetupDefinitionSourceCodec.ParseJson(bytes);
        prepared.Add((source, runtime, bytes, definition));
    }
    foreach (var example in prepared)
    {
        if (args[0] == "--check-examples")
        {
            if (!File.ReadAllBytes(example.Runtime).AsSpan().SequenceEqual(example.Definition.CanonicalBytes.Span))
            {
                Console.Error.WriteLine($"Example runtime is stale: {example.Runtime}");
                return 1;
            }
            continue;
        }
        if (args[0] == "--renew-example-families") File.WriteAllBytes(example.Source, example.Bytes);
        File.WriteAllBytes(example.Runtime, example.Definition.CanonicalBytes.ToArray());
        Console.WriteLine($"{example.Source}: family={example.Definition.FamilyId:D}; revision={example.Definition.Revision}; digest={example.Definition.Digest.Value}");
    }
    if (args[0] == "--check-examples") Console.WriteLine("Example source/runtime agreement: 3/3.");
    return 0;
}
if (args is ["--write"] or ["--check"])
{
    bool write = args[0] == "--write";
    bool matches = true;
    foreach ((string schemaPath, string content) in new[]
    {
        ("schemas/setup-definition-v2.schema.json", DefinitionSchema.Generate()),
        ("schemas/configurator-authoring-v2.schema.json", DefinitionAuthoringSchema.Generate()),
        ("schemas/configurator-answers-v1.schema.json", DefinitionSchema.GenerateAnswers())
    })
    {
        if (write) File.WriteAllText(schemaPath, content);
        else matches &= File.Exists(schemaPath) && File.ReadAllText(schemaPath) == content;
    }
    foreach (string retired in new[] { "schemas/setup-definition-v1.schema.json", "schemas/configurator-authoring-v1.schema.json" })
    {
        if (write) File.Delete(retired);
        else matches &= !File.Exists(retired);
    }
    Console.WriteLine(write ? "Definition schemas generated." : matches ? "Definition schemas are current." : "Definition schemas are missing or stale.");
    return matches ? 0 : 1;
}
Console.Error.WriteLine("Usage: Configurator.DefinitionSchemaGenerator [--write|--check|--write-examples|--renew-example-families|--check-examples|--renew-store-families]");
return 2;
