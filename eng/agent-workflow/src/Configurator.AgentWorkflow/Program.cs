using System.Text;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.RepresentationModel;

namespace Configurator.AgentWorkflow;

public static class Program
{
    private const int Invalid = 2;
    private const int Usage = 64;
    private const int MaximumYamlBytes = 4 * 1024 * 1024;
    private static readonly string[] ProjectRoots = ["tests", "eng"];

    public static int Main(string[] args)
    {
        return args switch
        {
            ["validate-intents", string path] => ValidateIntents(path),
            ["validate-commit", "--", .. var command] => ValidateCommit(command),
            ["--help"] or ["-h"] or [] => WriteHelp(),
            _ => WriteFailure("invalid_command", Usage),
        };
    }

    private static int ValidateIntents(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            long length = stream.Length;
            if (length is <= 0 or > MaximumYamlBytes)
            {
                return WriteFailure("invalid_intents_yaml", Invalid);
            }

            var bytes = new byte[(int)length];
            stream.ReadExactly(bytes);
            if (stream.ReadByte() != -1) return WriteFailure("invalid_intents_yaml", Invalid);
            string text = new UTF8Encoding(false, true).GetString(bytes);
            var parser = new Parser(new StringReader(text));
            int events = 0, depth = 0;
            while (parser.MoveNext())
            {
                if (++events > 100_000 || parser.Current is AnchorAlias ||
                    parser.Current is NodeEvent node && (!node.Anchor.IsEmpty || !node.Tag.IsEmpty))
                    return WriteFailure("invalid_intents_yaml", Invalid);
                if (parser.Current is MappingStart or SequenceStart && ++depth > 64)
                    return WriteFailure("invalid_intents_yaml", Invalid);
                if (parser.Current is MappingEnd or SequenceEnd) depth--;
            }
            var yaml = new YamlStream();
            yaml.Load(new StringReader(text));
            if (yaml.Documents.Count != 1)
                return WriteFailure("invalid_intents_yaml", Invalid);
            string? root = ContractRoot(path);
            if (root is null) return WriteFailure("invalid_intents_reference", Invalid);
            using JsonDocument schema = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, ".agents/contract/schema.json")));
            YamlNode document = yaml.Documents[0].RootNode;
            if (!MatchesSchema(document, schema.RootElement, schema.RootElement))
                return WriteFailure("invalid_intents_contract", Invalid);
            return ReferencesExist((YamlMappingNode)document, root)
                ? WriteSuccess("intents_valid")
                : WriteFailure("invalid_intents_reference", Invalid);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or
            YamlException or DecoderFallbackException or JsonException or RegexMatchTimeoutException)
        {
            return WriteFailure("invalid_intents_yaml", Invalid);
        }
    }

    private static string? ContractRoot(string path)
    {
        for (DirectoryInfo? directory = Directory.GetParent(Path.GetFullPath(path)); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, ".agents/contract/schema.json")))
                return directory.FullName;
        return null;
    }

    // This interpreter implements the closed vocabulary used by the repository's
    // intent schema. Repository references are checked separately, never executed.
    private static bool MatchesSchema(YamlNode node, JsonElement schema, JsonElement root)
    {
        if (schema.TryGetProperty("$ref", out JsonElement reference))
            schema = root.GetProperty("$defs").GetProperty(reference.GetString()!["#/$defs/".Length..]);
        switch (schema.GetProperty("type").GetString())
        {
            case "object":
                if (node is not YamlMappingNode mapping) return false;
                JsonElement properties = schema.GetProperty("properties");
                if (schema.TryGetProperty("required", out JsonElement required) &&
                    required.EnumerateArray().Any(key => !mapping.Children.ContainsKey(new YamlScalarNode(key.GetString()))))
                    return false;
                foreach ((YamlNode key, YamlNode value) in mapping.Children)
                    if (key is not YamlScalarNode { Value: { } name } ||
                        !properties.TryGetProperty(name, out JsonElement child) || !MatchesSchema(value, child, root))
                        return false;
                return true;
            case "array":
                return node is YamlSequenceNode sequence &&
                    (!schema.TryGetProperty("minItems", out JsonElement minimumItems) || sequence.Children.Count >= minimumItems.GetInt32()) &&
                    sequence.Children.All(child => MatchesSchema(child, schema.GetProperty("items"), root));
            case "integer":
                return node is YamlScalarNode { Style: ScalarStyle.Plain, Value: { } integer } &&
                    Regex.IsMatch(integer, "^-?(0|[1-9][0-9]*)$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)) &&
                    long.TryParse(integer, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long number) &&
                    (!schema.TryGetProperty("minimum", out JsonElement minimum) || number >= minimum.GetInt64());
            case "string":
                if (node is not YamlScalarNode { Value: { } text } scalar ||
                    scalar.Style == ScalarStyle.Plain && (text is "true" or "false" or "null" or "~" ||
                        Regex.IsMatch(text, "^-?(0|[1-9][0-9]*)(\\.[0-9]+)?([eE][+-]?[0-9]+)?$",
                            RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)))) return false;
                return (!schema.TryGetProperty("minLength", out JsonElement length) || text.EnumerateRunes().Count() >= length.GetInt32()) &&
                    (!schema.TryGetProperty("enum", out JsonElement choices) || choices.EnumerateArray().Any(choice => choice.GetString() == text)) &&
                    (!schema.TryGetProperty("pattern", out JsonElement pattern) ||
                        Regex.IsMatch(text, pattern.GetString()!, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)));
            default:
                return false;
        }
    }

    private static bool ReferencesExist(YamlMappingNode document, string root)
    {
        var intents = (YamlSequenceNode)document.Children[new YamlScalarNode("intents")];
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (YamlMappingNode intent in intents.Children.Cast<YamlMappingNode>())
            if (!ids.Add(((YamlScalarNode)intent.Children[new YamlScalarNode("id")]).Value!)) return false;
        foreach (YamlMappingNode intent in intents.Children.Cast<YamlMappingNode>())
        {
            foreach (string field in new[] { "must_read_docs", "docs_to_update", "load_rules" })
                if (Strings(intent, field).Any(path => !SafeRelativePath(path) || !File.Exists(Path.Combine(root, path)))) return false;
            if (Strings(intent, "load_skills").Any(skill => !File.Exists(Path.Combine(root, ".agents/skills", skill, "SKILL.md")))) return false;
            if (Strings(intent, "related_agents").Any(agent => !File.Exists(Path.Combine(root, ".agents/agents", agent + ".md")))) return false;
            if (Strings(intent, "related_intents").Any(id => !ids.Contains(id))) return false;
            foreach (string field in new[] { "paths_in_scope", "paths_forbidden" })
                if (Strings(intent, field).Any(path => !SafeRelativePath(path))) return false;
            foreach (string project in Strings(intent, "minimum_tests"))
            {
                var options = new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint };
                if (!ProjectRoots.Any(directory => Directory.Exists(Path.Combine(root, directory)) &&
                    Directory.EnumerateFiles(Path.Combine(root, directory), project + ".csproj", options).Any())) return false;
            }
            if (intent.Children.TryGetValue(new YamlScalarNode("routing"), out YamlNode? routingNode))
            {
                var routing = (YamlMappingNode)routingNode;
                foreach (YamlMappingNode secondary in ((YamlSequenceNode)routing.Children[new YamlScalarNode("secondary")]).Children.Cast<YamlMappingNode>())
                    if (!ids.Contains(((YamlScalarNode)secondary.Children[new YamlScalarNode("intent")]).Value!) ||
                        Strings(secondary, "when_paths").Any(path => !SafeRelativePath(path))) return false;
                foreach (YamlMappingNode conflict in ((YamlSequenceNode)routing.Children[new YamlScalarNode("conflict_overrides")]).Children.Cast<YamlMappingNode>())
                    if (!ids.Contains(((YamlScalarNode)conflict.Children[new YamlScalarNode("secondary_intent")]).Value!)) return false;
            }
        }
        return true;
    }

    private static IEnumerable<string> Strings(YamlMappingNode mapping, string key) =>
        mapping.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? node)
            ? ((YamlSequenceNode)node).Children.Cast<YamlScalarNode>().Select(value => value.Value!)
            : [];

    private static bool SafeRelativePath(string path) =>
        !string.IsNullOrWhiteSpace(path) && !Path.IsPathRooted(path) && !path.Contains('\\') &&
        !path.Contains(':') && !path.Any(char.IsControl) &&
        path.Split('/').All(segment => segment.Length > 0 && segment is not "." and not "..");

    private static int ValidateCommit(string[] command)
    {
        int separator = Array.LastIndexOf(command, "--");
        if (command is not ["git", "commit", ..] || separator < 2 || separator == command.Length - 1 ||
            Array.IndexOf(command, "--") != separator ||
            command[2..separator].Any(option => option is "-a" or "--all" or "--pathspec-from-file" or "--pathspec-file-nul" ||
                option.StartsWith("--pathspec-from-file=", StringComparison.Ordinal)))
        {
            return WriteFailure("unsafe_commit_paths", Invalid);
        }

        string[] paths = command[(separator + 1)..];
        return paths.All(IsLiteralFilePath) && paths.Distinct(StringComparer.Ordinal).Count() == paths.Length
            ? WriteSuccess("commit_paths_literal")
            : WriteFailure("unsafe_commit_paths", Invalid);
    }

    private static bool IsLiteralFilePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path is "." or ".." || path[0] == ':' ||
            Path.IsPathRooted(path) || path.Contains('\\') || path.EndsWith('/') ||
            path.IndexOfAny(['*', '?', '[', ']', '\0']) >= 0 || path.Any(char.IsControl) || Directory.Exists(path))
        {
            return false;
        }

        return path.Split('/', StringSplitOptions.None)
            .All(segment => segment.Length > 0 && segment is not "." and not "..");
    }

    private static int WriteHelp()
    {
        Console.Out.WriteLine("usage: islamu-agent-workflow validate-intents <intents.yaml> | validate-commit -- git commit <options> -- <literal-file>...");
        return 0;
    }

    private static int WriteSuccess(string code)
    {
        Console.Out.WriteLine(code);
        return 0;
    }

    private static int WriteFailure(string code, int exitCode)
    {
        Console.Error.WriteLine(code);
        return exitCode;
    }
}
