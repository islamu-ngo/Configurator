#:property RestorePackagesWithLockFile=false
#:property AssemblyName=Configurator.NugetVulnerabilityPolicy
#:property RootNamespace=Configurator.Ci

using System.Text.Json;

if (args is not [var path] || !File.Exists(path)) return 64;
using JsonDocument report = JsonDocument.Parse(File.ReadAllBytes(path));
JsonElement root = report.RootElement;
if (!root.TryGetProperty("version", out JsonElement version) || version.GetInt32() != 1
    || !root.TryGetProperty("projects", out JsonElement projects)
    || projects.ValueKind != JsonValueKind.Array || projects.GetArrayLength() == 0)
{
    Console.Error.WriteLine("Missing or unsupported NuGet audit report.");
    return 1;
}
foreach (JsonElement project in projects.EnumerateArray())
    if (!project.TryGetProperty("path", out JsonElement projectPath)
        || projectPath.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(projectPath.GetString())
        || (project.TryGetProperty("frameworks", out JsonElement frameworks)
            && frameworks.ValueKind != JsonValueKind.Array))
    {
        Console.Error.WriteLine("NuGet audit did not establish a project dependency graph.");
        return 1;
    }
int findings = Visit(root);
Console.WriteLine(findings == 0 ? "No NuGet vulnerabilities detected." : $"NuGet audit rejected {findings} vulnerability or error entries.");
return findings == 0 ? 0 : 1;

static int Visit(JsonElement node)
{
    int findings = 0;
    if (node.ValueKind == JsonValueKind.Array)
        foreach (JsonElement item in node.EnumerateArray()) findings += Visit(item);
    else if (node.ValueKind == JsonValueKind.Object)
        foreach (JsonProperty property in node.EnumerateObject())
        {
            if (property.Name is "vulnerabilities" or "errors")
            {
                if (property.Value.ValueKind != JsonValueKind.Array) return 1;
                findings += property.Value.GetArrayLength();
                foreach (JsonElement item in property.Value.EnumerateArray()) Console.Error.WriteLine(item);
            }
            else if (property.Name == "problems")
            {
                if (property.Value.ValueKind != JsonValueKind.Array) return 1;
                foreach (JsonElement item in property.Value.EnumerateArray())
                {
                    findings++;
                    Console.Error.WriteLine(item);
                }
            }
            else findings += Visit(property.Value);
        }
    return findings;
}
