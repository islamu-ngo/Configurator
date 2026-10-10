using Configurator.Core.Definitions;
using Configurator.Core.Definitions.Authoring;

if (args.Length == 0)
{
    Console.Write(DefinitionSchema.Generate());
    return 0;
}
if (args is ["--write"] or ["--check"])
{
    bool write = args[0] == "--write";
    bool matches = true;
    foreach ((string schemaPath, string content) in new[]
    {
        ("schemas/setup-definition-v1.schema.json", DefinitionSchema.Generate()),
        ("schemas/configurator-authoring-v1.schema.json", DefinitionAuthoringSchema.Generate())
    })
    {
        if (write) File.WriteAllText(schemaPath, content);
        else matches &= File.Exists(schemaPath) && File.ReadAllText(schemaPath) == content;
    }
    Console.WriteLine(write ? "Definition schemas generated." : matches ? "Definition schemas are current." : "Definition schemas are missing or stale.");
    return matches ? 0 : 1;
}
Console.Error.WriteLine("Usage: Configurator.DefinitionSchemaGenerator [--write|--check]");
return 2;
