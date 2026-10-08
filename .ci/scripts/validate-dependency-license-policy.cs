#:property RestorePackagesWithLockFile=false
#:property AssemblyName=Configurator.DependencyLicensePolicy
#:property RootNamespace=Configurator.Ci

using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Linq;

if (args.Length > 2) return 64;
string root = Path.GetFullPath(args.Length > 0 ? args[0] : ".");
using JsonDocument policy = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root, ".ci/policy/dependency-licenses.json")));
var allowed = policy.RootElement.GetProperty("allowedExpressions").EnumerateArray()
    .Select(item => item.GetString()!).ToHashSet(StringComparer.Ordinal);
string cache = args.Length == 2 ? Path.GetFullPath(args[1])
    : Environment.GetEnvironmentVariable("NUGET_PACKAGES")
        ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget/packages");
var packages = new HashSet<(string Id, string Version)>();
var failures = new List<string>();
foreach (string directory in new[] { "src", "tests", "eng" })
{
    foreach (string path in Directory.EnumerateFiles(Path.Combine(root, directory), "packages.lock.json", SearchOption.AllDirectories)
        .Where(path => !path.Split(Path.DirectorySeparatorChar).Any(part => part is "bin" or "obj" or "feed" or "tests")))
    {
        using JsonDocument locked = JsonDocument.Parse(File.ReadAllBytes(path));
        foreach (JsonProperty framework in locked.RootElement.GetProperty("dependencies").EnumerateObject())
            foreach (JsonProperty dependency in framework.Value.EnumerateObject())
                if (dependency.Value.TryGetProperty("resolved", out JsonElement version))
                    packages.Add((dependency.Name.ToLowerInvariant(), version.GetString()!.ToLowerInvariant()));
    }
}
if (packages.Count == 0) failures.Add("No dependency locks found.");
string packageOutput = Path.Combine(root, "artifacts/configurator");
if (Directory.Exists(packageOutput))
    foreach (string inventory in Directory.EnumerateFiles(packageOutput, "packages.json", SearchOption.AllDirectories)
        .Where(path => Path.GetFileName(Path.GetDirectoryName(path)) == "dependency-notices"))
    {
        using JsonDocument published = JsonDocument.Parse(File.ReadAllBytes(inventory));
        foreach (JsonElement entry in published.RootElement.EnumerateArray())
        {
            string[] parts = entry.GetString()!.Split('/');
            if (parts.Length != 2) throw new InvalidDataException("Invalid packaged dependency identity.");
            packages.Add((parts[0].ToLowerInvariant(), parts[1].ToLowerInvariant()));
        }
    }
foreach ((string id, string version) in packages.OrderBy(item => item.Id, StringComparer.Ordinal).ThenBy(item => item.Version, StringComparer.Ordinal))
{
    string path = Path.Combine(cache, id, version, id + ".nuspec");
    if (!File.Exists(path))
    {
        failures.Add($"{id} {version}: missing restored metadata");
        continue;
    }
    XDocument nuspec = XDocument.Load(path);
    XElement? metadata = nuspec.Descendants().FirstOrDefault(element => element.Name.LocalName == "metadata");
    XElement? license = metadata?.Elements().FirstOrDefault(element => element.Name.LocalName == "license");
    if (license?.Attribute("type")?.Value == "expression" && allowed.Contains(license.Value.Trim())) continue;
    string hash = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
    bool approved = policy.RootElement.GetProperty("metadataApprovals").EnumerateArray().Any(item =>
        item.GetProperty("id").GetString() == id && item.GetProperty("version").GetString() == version
        && item.GetProperty("nuspecSha256").GetString() == hash
        && allowed.Contains(item.GetProperty("license").GetString()!)
        && license?.Attribute("type")?.Value == "file"
        && Path.GetFileName(license.Value) == license.Value
        && File.Exists(Path.Combine(Path.GetDirectoryName(path)!, license.Value))
        && Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(path)!, license.Value))))
            == item.GetProperty("licenseFileSha256").GetString());
    if (!approved) failures.Add($"{id} {version}: unapproved license metadata '{license?.Value ?? "missing"}' (nuspec {hash})");
}
foreach (string failure in failures) Console.Error.WriteLine(failure);
if (failures.Count != 0) return 1;
Console.WriteLine($"MIT-compatible dependency policy passed ({packages.Count} package/version pairs).");
return 0;
