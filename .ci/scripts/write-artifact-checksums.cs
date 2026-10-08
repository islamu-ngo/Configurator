#:property RestorePackagesWithLockFile=false
#:property AssemblyName=Configurator.ArtifactChecksums
#:property RootNamespace=Configurator.Ci

using System.Security.Cryptography;
using System.Text;

if (args is not [var directory, var output] || !Directory.Exists(directory)) return 64;
string root = Path.GetFullPath(directory);
string destination = Path.GetFullPath(output);
var pending = new Stack<string>();
var lines = new SortedDictionary<string, string>(StringComparer.Ordinal);
var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
pending.Push(root);
while (pending.TryPop(out string? current))
{
    if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
        throw new InvalidDataException("Artifact links are not checksum inputs.");
    foreach (string entry in Directory.EnumerateFileSystemEntries(current))
    {
        if (Path.GetFullPath(entry) == destination) continue;
        string relative = Path.GetRelativePath(root, entry).Replace('\\', '/');
        if (!aliases.Add(relative) || relative.Length > 4096 || relative.Any(character => char.IsControl(character) || character == '\\'))
            throw new InvalidDataException("Ambiguous artifact path.");
        FileAttributes attributes = File.GetAttributes(entry);
        if ((attributes & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Artifact link rejected.");
        if ((attributes & FileAttributes.Directory) != 0) { pending.Push(entry); continue; }
        if (lines.Count >= 4096 || new FileInfo(entry).Length > 1_073_741_824)
            throw new InvalidDataException("Artifact bounds exceeded.");
        using Stream stream = File.OpenRead(entry);
        lines.Add(relative, Convert.ToHexStringLower(SHA256.HashData(stream)) + "  " + relative);
    }
}
if (lines.Count == 0) return 1;
Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
File.WriteAllText(destination, string.Join('\n', lines.Values) + "\n", new UTF8Encoding(false));
Console.WriteLine($"Wrote {lines.Count} artifact checksums.");
return 0;
