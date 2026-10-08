namespace Configurator.Terminal;

using System.Security.Cryptography;
using Configurator.Core;
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
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length is <= 0 or > 4 * 1024 * 1024)
            throw new InvalidOperationException("definition-limit");
        byte[] bytes = new byte[(int)stream.Length];
        try
        {
            stream.ReadExactly(bytes);
            if (stream.ReadByte() != -1)
                throw new InvalidOperationException("definition-changed");
            return System.IO.Path.GetExtension(path).ToLowerInvariant() switch
            {
                ".json" => SetupDefinitionCodec.ParseJson(bytes),
                ".yaml" or ".yml" => SetupDefinitionCodec.ParseYaml(bytes),
                _ => throw new InvalidOperationException("definition-format")
            };
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
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
