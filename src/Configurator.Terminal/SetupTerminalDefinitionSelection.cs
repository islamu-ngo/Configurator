namespace Configurator.Terminal;

using Configurator.Artifacts;
using Configurator.Core.Definitions;

internal sealed record SetupTerminalDefinitionSelection(
    string Path, Guid? FamilyId = null, string? ApplicationVersion = null)
{
    internal SetupDefinition Load()
    {
        if (FamilyId is null)
            return ReadDefinition(Path);
        string[] paths = Directory.EnumerateFiles(Path)
            .Where(path => System.IO.Path.GetExtension(path).ToLowerInvariant() is ".json" or ".yaml" or ".yml")
            .Take(4097).ToArray();
        if (paths.Length > 4096)
            throw new InvalidOperationException("registry-limit");
        return new SetupDefinitionRegistry(paths.Select(ReadDefinition))
            .Resolve(FamilyId.Value, ApplicationVersion);
    }

    private static SetupDefinition ReadDefinition(string path)
    {
        using BoundedFileInput input = BoundedFileInput.ReadAsync(path, 4 * 1024 * 1024)
            .GetAwaiter().GetResult();
        return System.IO.Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".json" => SetupDefinitionSourceCodec.ParseJson(input.Bytes),
            ".yaml" or ".yml" => SetupDefinitionSourceCodec.ParseYaml(input.Bytes),
            _ => throw new InvalidOperationException("definition-format")
        };
    }

    internal static bool TryParse(string[] arguments, out SetupTerminalDefinitionSelection? selection)
    {
        selection = null;
        if (arguments is ["--definition", var path] && !string.IsNullOrWhiteSpace(path))
        {
            selection = new(path);
            return true;
        }
        if (arguments.Length is not (4 or 6)
            || arguments[0] != "--registry" || string.IsNullOrWhiteSpace(arguments[1])
            || arguments[2] != "--family" || !Guid.TryParse(arguments[3], out Guid family)
            || family == Guid.Empty
            || arguments.Length == 6 && (arguments[4] != "--app-version" || string.IsNullOrWhiteSpace(arguments[5])))
            return false;
        selection = new(arguments[1], family, arguments.Length == 6 ? arguments[5] : null);
        return true;
    }
}
