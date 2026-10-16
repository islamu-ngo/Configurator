using System.Text.Json;
using Configurator.Core.Definitions.Authoring;

namespace Configurator.Cli;

/// <summary>Supplies bounded local schema documents without granting Core filesystem or network authority.</summary>
internal static class DefinitionSchemaSourceFiles
{
    internal static async ValueTask<DefinitionSchemaImportResult> ImportAsync(string path, bool acceptBoundedSemantics,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        path = DefinitionSourceFiles.SafePath(path);
        string root = Path.GetDirectoryName(path)!;
        byte[] source = await DefinitionSourceFiles.ReadAsync(path, cancellationToken).ConfigureAwait(false);
        var references = new Dictionary<string, ReadOnlyMemory<byte>>(StringComparer.Ordinal);
        var pending = new Queue<(string Name, ReadOnlyMemory<byte> Bytes)>();
        pending.Enqueue(("", source));
        int total = source.Length;
        while (pending.TryDequeue(out var item))
        {
            cancellationToken.ThrowIfCancellationRequested();
            using JsonDocument document = JsonDocument.Parse(item.Bytes, new JsonDocumentOptions { MaxDepth = 64 });
            foreach (string reference in References(document.RootElement, cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
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
                byte[] bytes = await DefinitionSourceFiles.ReadAsync(candidate, cancellationToken).ConfigureAwait(false);
                if (bytes.Length > DefinitionSourceFiles.MaximumBytes - total) throw new DefinitionFileException("schema-reference-bound");
                total += bytes.Length;
                references.Add(name, bytes);
                pending.Enqueue((name, bytes));
            }
        }
        cancellationToken.ThrowIfCancellationRequested();
        DefinitionSchemaImportResult result = DefinitionSchemaImporter.Import(source, references, acceptBoundedSemantics);
        cancellationToken.ThrowIfCancellationRequested();
        return result;
    }

    private static IEnumerable<string> References(JsonElement value, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (value.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in value.EnumerateObject())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (property.Name == "$ref" && property.Value.ValueKind == JsonValueKind.String)
                    yield return property.Value.GetString()!;
                else if (property.Name is not ("default" or "examples"))
                    foreach (string reference in References(property.Value, cancellationToken)) yield return reference;
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
            foreach (JsonElement item in value.EnumerateArray())
                foreach (string reference in References(item, cancellationToken)) yield return reference;
    }
}
