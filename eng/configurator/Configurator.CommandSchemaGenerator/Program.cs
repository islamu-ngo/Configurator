using Configurator.Cli;

if (args.Length != 1 || args[0] is not ("--write" or "--check" or "--stdout"))
{
    Console.Error.WriteLine("Usage: Configurator.CommandSchemaGenerator (--write|--check|--stdout)");
    return 64;
}

try
{
    byte[] expected = SetupCliCommandSchemaMetadata.GenerateSchema();
    if (args[0] == "--stdout")
    {
        using Stream output = Console.OpenStandardOutput();
        output.Write(expected);
        return 0;
    }
    DirectoryInfo? current = new(Directory.GetCurrentDirectory());
    while (current is not null && !File.Exists(Path.Combine(current.FullName, "Configurator.slnx"))) current = current.Parent;
    if (current is null) throw new InvalidDataException("repository-root-not-found");
    string path = Path.Combine(current.FullName, SetupCliCommandSchemaMetadata.RelativePath.Replace('/', Path.DirectorySeparatorChar));
    if (args[0] == "--check")
    {
        if (!File.Exists(path) || !File.ReadAllBytes(path).AsSpan().SequenceEqual(expected))
        {
            Console.Error.WriteLine("configurator-command-schema-stale");
            return 1;
        }
        Console.WriteLine("configurator-command-schema-current");
        return 0;
    }
    File.WriteAllBytes(path, expected);
    Console.WriteLine("configurator-command-schema-generated");
    return 0;
}
catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
{
    Console.Error.WriteLine("configurator-command-schema-failed");
    return 1;
}
