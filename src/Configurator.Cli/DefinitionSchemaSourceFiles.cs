using System.Text.Json;
using Configurator.Core.Definitions.Authoring;

namespace Configurator.Cli;

/// <summary>Supplies bounded local schema documents without granting Core filesystem or network authority.</summary>
internal static class DefinitionSchemaSourceFiles
{
    internal static DefinitionSchemaImportResult Import(string path, bool acceptBoundedSemantics)
    {
        path = DefinitionSourceFiles.SafePath(path);
        string root = Path.GetDirectoryName(path)!;
        byte[] source = DefinitionSourceFiles.Read(path);
        var references = new Dictionary<string, ReadOnlyMemory<byte>>(StringComparer.Ordinal);
        var pending = new Queue<(string Name, ReadOnlyMemory<byte> Bytes)>();
        pending.Enqueue(("", source));
        int total = source.Length;
        while (pending.TryDequeue(out var item))
        {
            using JsonDocument document = JsonDocument.Parse(item.Bytes, new JsonDocumentOptions { MaxDepth = 64 });
            foreach (string reference in References(document.RootElement))
            {
                string name = reference.Split('#')[0];
                if (name.Length == 0) continue;
                if (name.Length > 256 || name.StartsWith('/') || name.Contains(':') || name.Contains('\\') || name.Contains('?') || name.Contains('%')
                    || name.Split('/').Any(part => part is "" or "." or ".." || part.Any(character => character < ' ' || character == 0x7f)))
                    throw new DefinitionFileException("schema-reference-unsupported");
                string prefix = item.Name.Contains('/') ? item.Name[..(item.Name.LastIndexOf('/') + 1)] : "";
                name = prefix + name;
                if (references.ContainsKey(name)) continue;
                if (references.Count >= 63) throw new DefinitionFileException("schema-reference-bound");
                string candidate = DefinitionSourceFiles.SafePath(Path.Combine(root, name));
                string relative = Path.GetRelativePath(root, candidate);
                if (Path.IsPathRooted(relative) || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) || relative == "..")
                    throw new DefinitionFileException("schema-reference-unsupported");
                byte[] bytes = DefinitionSourceFiles.Read(candidate);
                if (bytes.Length > DefinitionSourceFiles.MaximumBytes - total) throw new DefinitionFileException("schema-reference-bound");
                total += bytes.Length;
                references.Add(name, bytes);
                pending.Enqueue((name, bytes));
            }
        }
        return DefinitionSchemaImporter.Import(source, references, acceptBoundedSemantics);
    }

    private static IEnumerable<string> References(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in value.EnumerateObject())
            {
                if (property.Name == "$ref" && property.Value.ValueKind == JsonValueKind.String)
                    yield return property.Value.GetString()!;
                else if (property.Name is not ("default" or "examples"))
                    foreach (string reference in References(property.Value)) yield return reference;
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
            foreach (JsonElement item in value.EnumerateArray())
                foreach (string reference in References(item)) yield return reference;
    }
}
