#:property RestorePackagesWithLockFile=false
#:property AssemblyName=Configurator.DependencyNotices
#:property RootNamespace=Configurator.Ci

using System.Text.Json;
using System.Diagnostics;

if (args is not [var assetsPath, var outputPath, var projectPath, var runtime]) return 64;
using JsonDocument assets = JsonDocument.Parse(File.ReadAllBytes(assetsPath));
string[] caches = assets.RootElement.GetProperty("packageFolders").EnumerateObject().Select(item => item.Name).ToArray();
DirectoryInfo? root = new(Path.GetDirectoryName(Path.GetFullPath(projectPath))!);
while (root is not null && !File.Exists(Path.Combine(root.FullName, "Configurator.slnx"))) root = root.Parent;
if (root is null) throw new InvalidDataException("Configurator repository root is missing.");
string supplementalRoot = Path.Combine(root.FullName, "eng/configurator/dependency-notices");
int count = 0;
var packages = new List<string>();
foreach (JsonProperty library in assets.RootElement.GetProperty("libraries").EnumerateObject())
{
    if (library.Value.GetProperty("type").GetString() != "package") continue;
    string relative = library.Value.GetProperty("path").GetString()!;
    packages.Add(library.Name);
    string? package = caches.Select(cache => Path.Combine(cache, relative)).FirstOrDefault(Directory.Exists);
    if (package is null) throw new InvalidDataException("Restored package metadata is missing.");
    string destination = Path.Combine(outputPath, relative);
    string contentHash = library.Value.GetProperty("sha512").GetString()
        ?? throw new InvalidDataException("Restored package content identity is missing.");
    if (!WriteSupplementalNotices(supplementalRoot, library.Name, package, destination, contentHash))
        return RejectSupplementalReview(library.Name);
    Directory.CreateDirectory(destination);
    foreach (string nuspec in Directory.EnumerateFiles(package, "*.nuspec"))
        File.Copy(nuspec, Path.Combine(destination, Path.GetFileName(nuspec)), overwrite: true);
    foreach (JsonElement entry in library.Value.GetProperty("files").EnumerateArray())
    {
        string file = entry.GetString()!;
        string name = Path.GetFileName(file);
        if (!name.Contains("license", StringComparison.OrdinalIgnoreCase)
            && !name.Contains("notice", StringComparison.OrdinalIgnoreCase)
            && !name.StartsWith("COPYING", StringComparison.OrdinalIgnoreCase)
            && !name.StartsWith("COPYRIGHT", StringComparison.OrdinalIgnoreCase)) continue;
        string source = Path.Combine(package, file);
        if (!File.Exists(source)) throw new InvalidDataException("A package notice is missing.");
        string target = Path.Combine(destination, file);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.Copy(source, target, overwrite: true);
    }
    count++;
}
if (count == 0) return 1;
// SDK runtime packs are download dependencies, not ordinary assets libraries.
var start = new ProcessStartInfo("dotnet")
{
    UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true
};
foreach (string argument in new[] { "msbuild", projectPath, "-nologo", "-verbosity:quiet",
    "-t:ProcessFrameworkReferences", "-getItem:RuntimePack,FrameworkReference",
    $"-p:RuntimeIdentifier={runtime}", "-p:SelfContained=true" })
    start.ArgumentList.Add(argument);
using (Process process = Process.Start(start)!)
{
    Task<string> output = process.StandardOutput.ReadToEndAsync();
    Task<string> error = process.StandardError.ReadToEndAsync();
    using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(2));
    try { await process.WaitForExitAsync(deadline.Token); }
    catch (OperationCanceledException) { process.Kill(entireProcessTree: true); throw; }
    if (process.ExitCode != 0) { Console.Error.WriteLine(await error); return process.ExitCode; }
    using JsonDocument evaluated = JsonDocument.Parse(await output);
    JsonElement items = evaluated.RootElement.GetProperty("Items");
    var frameworks = items.GetProperty("FrameworkReference").EnumerateArray()
        .Select(item => item.GetProperty("Identity").GetString()!).ToHashSet(StringComparer.Ordinal);
    foreach (JsonElement pack in items.GetProperty("RuntimePack").EnumerateArray())
    {
        if (!frameworks.Contains(pack.GetProperty("FrameworkName").GetString()!)) continue;
        string id = pack.GetProperty("NuGetPackageId").GetString()!;
        string version = pack.GetProperty("NuGetPackageVersion").GetString()!;
        string relative = id.ToLowerInvariant() + "/" + version.ToLowerInvariant();
        string? package = caches.Select(cache => Path.Combine(cache, relative)).FirstOrDefault(Directory.Exists);
        if (package is null) throw new InvalidDataException("Selected SDK runtime-pack notices are missing.");
        string destination = Path.Combine(outputPath, relative);
        if (!WriteSupplementalNotices(supplementalRoot, id + "/" + version, package, destination, assetsHash: null))
            return RejectSupplementalReview(id + "/" + version);
        Directory.CreateDirectory(destination);
        foreach (string file in Directory.EnumerateFiles(package, "*", SearchOption.AllDirectories))
        {
            string name = Path.GetFileName(file);
            if (Path.GetExtension(file) != ".nuspec"
                && !name.Contains("license", StringComparison.OrdinalIgnoreCase)
                && !name.Contains("notice", StringComparison.OrdinalIgnoreCase)) continue;
            string target = Path.Combine(destination, Path.GetRelativePath(package, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
        packages.Add(id + "/" + version);
        count++;
    }
}
using (var stream = File.Create(Path.Combine(outputPath, "packages.json")))
{
    using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
    {
        writer.WriteStartArray();
        foreach (string package in packages.Order(StringComparer.Ordinal)) writer.WriteStringValue(package);
        writer.WriteEndArray();
    }
    stream.WriteByte((byte)'\n');
}
Console.WriteLine($"Retained original metadata and license/notice files for {count} dependency packages.");
return 0;

static bool WriteSupplementalNotices(string supplementalRoot, string packageIdentity, string package, string destination, string? assetsHash)
{
    string[] identity = packageIdentity.Split('/');
    string id = identity[0].ToLowerInvariant();
    string version = identity[1].ToLowerInvariant();
    string reviewRoot = Path.Combine(supplementalRoot, id);
    if (!Directory.Exists(reviewRoot)) return true;
    string review = Path.Combine(reviewRoot, version);
    string manifestPath = Path.Combine(review, "manifest.json");
    if (!File.Exists(manifestPath)) return false;
    using JsonDocument manifest = JsonDocument.Parse(File.ReadAllBytes(manifestPath));
    string? reviewedHash = manifest.RootElement.GetProperty("contentHash").GetString();
    string metadataPath = Path.Combine(package, ".nupkg.metadata");
    if (!File.Exists(metadataPath)) return false;
    using JsonDocument metadata = JsonDocument.Parse(File.ReadAllBytes(metadataPath));
    string? restoredHash = metadata.RootElement.GetProperty("contentHash").GetString();
    if (reviewedHash is null || restoredHash != reviewedHash || (assetsHash is not null && assetsHash != reviewedHash)
        || Convert.FromBase64String(reviewedHash).Length != 64)
        return false;
    string[] notices = Directory.EnumerateFiles(review)
        .Where(path => Path.GetFileName(path) is "LICENSE" or "NOTICE" or "THIRD-PARTY-NOTICES.txt").ToArray();
    if (notices.Length == 0) return false;
    foreach (string notice in notices)
    {
        if ((File.GetAttributes(notice) & FileAttributes.ReparsePoint) != 0
            || (!OperatingSystem.IsWindows() && (File.GetUnixFileMode(notice)
                & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) != 0))
            return false;
    }
    string supplementalDestination = Path.Combine(destination, "supplemental");
    Directory.CreateDirectory(supplementalDestination);
    foreach (string notice in notices)
        File.Copy(notice, Path.Combine(supplementalDestination, Path.GetFileName(notice)), overwrite: true);
    return true;
}

static int RejectSupplementalReview(string package)
{
    Console.Error.WriteLine($"Supplemental dependency review is missing or does not match {package}.");
    return 65;
}
