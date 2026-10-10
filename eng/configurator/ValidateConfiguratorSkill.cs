#:property RestorePackagesWithLockFile=false

using System.Text;

bool portable = args.Length == 2 && args[0] == "--portable";
if (args is ["--self-test"])
{
    string valid = "---\nname: example\ndescription: Create a configuration source.\nlicense: MIT\n---\nUse the CLI.\n";
    Validate(valid, "example", true);
    foreach (string invalid in new[] { valid.Replace("example", "Example", StringComparison.Ordinal),
        valid.Replace("description: Create a configuration source.\n", "", StringComparison.Ordinal),
        valid.Replace("license: MIT", "priority: high", StringComparison.Ordinal),
        valid.Replace("name: example", "name: different", StringComparison.Ordinal) })
    {
        bool rejected = false;
        try { Validate(invalid, "example", true); }
        catch (InvalidDataException) { rejected = true; }
        if (!rejected) throw new InvalidOperationException("skill-self-test-failed");
    }
    Console.WriteLine("configurator-skill-self-test-valid: 5 cases");
    return 0;
}
if (!portable && args.Length != 1)
{
    Console.Error.WriteLine("Usage: dotnet run --file eng/configurator/ValidateConfiguratorSkill.cs -- [--portable] <SKILL.md> | --self-test");
    return 64;
}

try
{
    string path = Path.GetFullPath(args[portable ? 1 : 0]);
    string text = await File.ReadAllTextAsync(path, Encoding.UTF8);
    string? parent = Path.GetDirectoryName(path);
    if (parent is null)
        throw new InvalidDataException("skill-parent-missing");
    Validate(text, Path.GetFileName(parent), portable);
    Console.WriteLine("configurator-skill-valid");
    return 0;
}
catch (InvalidDataException exception)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}
catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
{
    Console.Error.WriteLine("skill-input-unavailable");
    return 1;
}

static void Validate(string text, string directoryName, bool portable)
{
    string normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal);
    if (normalized.Contains("<!-- ABOUT" + "ME:", StringComparison.Ordinal))
        throw new InvalidDataException("skill-synthetic-header-forbidden");
    if (!normalized.StartsWith("---\n", StringComparison.Ordinal))
        throw new InvalidDataException("skill-frontmatter-missing");

    int end = normalized.IndexOf("\n---\n", 4, StringComparison.Ordinal);
    if (end < 0)
        throw new InvalidDataException("skill-frontmatter-unterminated");

    var fields = new Dictionary<string, string>(StringComparer.Ordinal);
    foreach (string line in normalized[4..end].Split('\n'))
    {
        int separator = line.IndexOf(':', StringComparison.Ordinal);
        if (separator <= 0)
            throw new InvalidDataException("skill-frontmatter-invalid");
        string key = line[..separator].Trim();
        string value = line[(separator + 1)..].Trim().Trim('"');
        if (!fields.TryAdd(key, value) || value.Length == 0)
            throw new InvalidDataException("skill-frontmatter-invalid");
    }

    Require(fields, "name", directoryName);
    if (portable)
    {
        if (directoryName.Length is < 1 or > 64 || directoryName[0] == '-'
            || directoryName[^1] == '-' || directoryName.Contains("--", StringComparison.Ordinal)
            || directoryName.Any(character => character is not (>= 'a' and <= 'z' or >= '0' and <= '9' or '-')))
            throw new InvalidDataException("skill-name-invalid");
        // shortcut: portable metadata is flat scalar YAML; extend when shipping a skill with structured metadata.
        if (fields.Keys.Except(["name", "description", "license", "compatibility", "allowed-tools"], StringComparer.Ordinal).Any())
            throw new InvalidDataException("skill-portable-field-unsupported");
        if (fields.TryGetValue("compatibility", out string? compatibility) && compatibility.Length > 500)
            throw new InvalidDataException("skill-compatibility-invalid");
    }
    else
    {
        RequireOneOf(fields, "type", ["guardrail", "pattern", "reference", "workflow"]);
        RequireOneOf(fields, "enforcement", ["block", "suggest", "inform"]);
        RequireOneOf(fields, "priority", ["critical", "high", "medium", "low"]);
    }
    if (!fields.TryGetValue("description", out string? description)
        || description.Length < (portable ? 1 : 20) || (portable && description.Length > 1024))
        throw new InvalidDataException("skill-description-invalid");
    if (normalized.Split('\n').Length > 250)
        throw new InvalidDataException("skill-line-limit-exceeded");

}

static void Require(
    IReadOnlyDictionary<string, string> fields,
    string name,
    string expected)
{
    if (!fields.TryGetValue(name, out string? actual)
        || !string.Equals(actual, expected, StringComparison.Ordinal))
    {
        throw new InvalidDataException($"skill-{name}-invalid");
    }
}

static void RequireOneOf(
    IReadOnlyDictionary<string, string> fields,
    string name,
    IReadOnlyCollection<string> allowed)
{
    if (!fields.TryGetValue(name, out string? actual)
        || !allowed.Contains(actual, StringComparer.Ordinal))
    {
        throw new InvalidDataException($"skill-{name}-invalid");
    }
}
