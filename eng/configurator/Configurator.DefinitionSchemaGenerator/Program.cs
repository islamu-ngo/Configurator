using Configurator.Core.Definitions;

if (args.Length == 0)
{
    Console.Write(DefinitionSchema.Generate());
    return 0;
}
if (args is ["--check", var path])
{
    bool matches = File.Exists(path) && File.ReadAllText(path) == DefinitionSchema.Generate();
    Console.WriteLine(matches ? "Definition schema is current." : "Definition schema is missing or stale.");
    return matches ? 0 : 1;
}
Console.Error.WriteLine("Usage: Configurator.DefinitionSchemaGenerator [--check schema-path]");
return 2;
