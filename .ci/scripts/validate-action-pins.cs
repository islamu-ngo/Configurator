#:property RestorePackagesWithLockFile=false
#:property AssemblyName=Configurator.ActionPinPolicy
#:property RootNamespace=Configurator.Ci

using System.Text.RegularExpressions;

string root = args.Length == 1 ? args[0] : ".github/workflows";
if (!Directory.Exists(root)) return 1;
int references = 0;
var failures = new List<string>();
foreach (string path in Directory.EnumerateFiles(root).Where(path => Path.GetExtension(path) is ".yml" or ".yaml"))
{
    int lineNumber = 0;
    foreach (string line in File.ReadLines(path))
    {
        lineNumber++;
        Match match = Regex.Match(line, """^\s*(?:-\s*)?uses:\s*['"]?([^'"\s#]+)['"]?(.*)$""");
        if (!match.Success) continue;
        string value = match.Groups[1].Value;
        if (value.StartsWith("./", StringComparison.Ordinal)) continue;
        references++;
        if (!Regex.IsMatch(value, @"^[A-Za-z0-9_.-]+/[A-Za-z0-9_./-]+@[0-9a-fA-F]{40}$")
            || !Regex.IsMatch(match.Groups[2].Value, @"#\s*v\d"))
            failures.Add($"{path}:{lineNumber}: external action requires a full SHA and version comment");
    }
}
foreach (string failure in failures) Console.Error.WriteLine(failure);
if (failures.Count != 0 || references == 0) return 1;
Console.WriteLine($"Action pins passed ({references} external references).");
return 0;
