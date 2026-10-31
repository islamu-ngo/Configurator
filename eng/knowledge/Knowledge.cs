#:property RestorePackagesWithLockFile=false

using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

if (args is ["--self-test"])
    return Knowledge.SelfTest();
if (args is not (["inventory"] or ["context", _] or ["links", _]))
{
    Console.Error.WriteLine("Usage: dotnet run --file eng/knowledge/Knowledge.cs -- inventory | context PROFILE | links PROFILE | --self-test");
    return 64;
}

try
{
    return Knowledge.Run(args);
}
catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
    or InvalidDataException or JsonException or ArgumentException or InvalidOperationException
    or System.ComponentModel.Win32Exception)
{
    // Never echo file contents, private operands or native stderr.
    Console.Error.WriteLine("KNOWLEDGE_FAILED: " + (exception is InvalidDataException ? exception.Message : "INPUT_OR_TOOL_UNAVAILABLE"));
    return 1;
}

/// <summary>
/// A generated view over tracked Markdown and literal approved configuration paths.
/// Path exclusions precede content reads; classifications never change source authority.
/// </summary>
internal static class Knowledge
{
    private const string ConfigPath = "eng/knowledge/knowledge.config.json";
    private const string OutputDirectory = ".tmp/knowledge";
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private static readonly string[] ProfileNames = ["current", "definitions", "clients", "agents", "audit"];
    private static readonly string[] Owners =
        ["Packaging/Release", "Core", "Artifacts/Security", "Presentation", "CLI", "Browser",
            "Terminal", "Desktop", "Project Steward", "Contributor Experience", "Agent Context"];
    private static readonly string[] Purposes = ["Tutorial", "How-to", "Reference", "Explanation"];

    internal static int Run(string[] arguments)
    {
        string root = Git(Environment.CurrentDirectory, "rev-parse", "--show-toplevel").TrimEnd('\r', '\n');
        RequireSafePath(root, ConfigPath);
        if (Git(root, "ls-files", "--cached", "--others", "--ignored", "--exclude-standard", "-z", "--", ConfigPath).Length != 0)
            throw new InvalidDataException("CONFIG_IGNORED");
        Configuration configuration = ParseConfiguration(File.ReadAllText(Path.Combine(root, ConfigPath), Utf8));
        string profile = arguments.Length == 2 ? arguments[1] : "current";
        if (!configuration.Profiles.ContainsKey(profile))
            throw new InvalidDataException("PROFILE_UNKNOWN");
        string revision = Git(root, "rev-parse", "HEAD").Trim();
        HashSet<string> tracked = Git(root, "ls-files", "--cached", "-z")
            .Split('\0', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);
        string[] candidates = Candidates(tracked, configuration.ExplicitPaths);
        // Git applies repository and local ignore rules even to tracked candidates.
        // The path list is bounded to candidates, never a recursive untracked crawl.
        var ignored = new HashSet<string>(StringComparer.Ordinal);
        foreach (string[] batch in candidates.Chunk(64))
        {
            string[] operands = ["ls-files", "--cached", "--others", "--ignored", "--exclude-standard", "-z", "--", .. batch];
            ignored.UnionWith(Git(root, operands).Split('\0', StringSplitOptions.RemoveEmptyEntries));
        }
        var documents = new List<Document>();
        foreach (string path in candidates)
        {
            string? exclusion = Exclusion(path, ignored.Contains(path));
            if (exclusion is null && !IsSafePath(root, path))
                exclusion = "symlink";
            if (exclusion is null && !File.Exists(Path.Combine(root, path)))
                exclusion = "missing";
            if (exclusion is not null)
            {
                documents.Add(Classify(path, tracked.Contains(path), null, exclusion));
                continue;
            }
            byte[] bytes = File.ReadAllBytes(Path.Combine(root, path));
            documents.Add(Classify(path, tracked.Contains(path), bytes, null));
        }
        // Resources inherit an explicitly reference-only skill's scope; a generic
        // resource title must not promote a historical backend example to current.
        foreach (Document document in documents.Where(document => document.Content is not null))
        {
            string? parent = documents.FirstOrDefault(candidate =>
                candidate.Path.EndsWith("/SKILL.md", StringComparison.Ordinal)
                && candidate.Status == "reference-only"
                && document.Path.StartsWith(candidate.Path[..^"SKILL.md".Length], StringComparison.Ordinal))?.Path;
            if (parent is not null && parent != document.Path)
            {
                document.Status = "reference-only";
                document.Evidence += "; inherited:" + parent;
            }
        }
        Document[] ordered = Order(documents, configuration);
        foreach (string entry in configuration.EntryPaths)
            if (!ordered.Any(document => document.Path == entry && document.Content is not null))
                throw new InvalidDataException("ENTRY_UNAVAILABLE");

        if (arguments[0] == "links")
        {
            // Reuse the existing checker, preserving every failure, including history.
            // Limit batches so command-line size does not depend on inventory growth.
            string[] markdown = ordered.Where(document => LinkSelected(document, profile, configuration))
                .Select(document => document.Path).ToArray();
            if (markdown.Length == 0)
                throw new InvalidDataException("PROFILE_EMPTY");
            RequireSafePath(root, "eng/configurator/ValidateDefinitionDocumentation.cs");
            RequireSafePath(root, OutputDirectory);
            Directory.CreateDirectory(Path.Combine(root, OutputDirectory));
            string resultPath = $"{OutputDirectory}/links-{profile}-result.tsv";
            RequireSafePath(root, resultPath);
            File.Delete(Path.Combine(root, resultPath));
            Write(root, $"links-{profile}.tsv", Manifest(ordered, revision, profile, configuration, links: true));
            int exit = 0;
            int batches = 0;
            int failedBatches = 0;
            string logPath = $"{OutputDirectory}/links-{profile}.log";
            RequireSafePath(root, logPath);
            using var log = new StreamWriter(Path.Combine(root, logPath), append: false, Utf8);
            log.WriteLine($"source_revision={revision} profile={profile} files={markdown.Length}");
            var groups = ordered.Where(document => LinkSelected(document, profile, configuration))
                .GroupBy(document => profile == "audit" || MetadataSelected(document, configuration));
            foreach ((bool checkMetadata, string[] batch) in groups.SelectMany(group =>
                group.Select(document => document.Path).Chunk(32).Select(batch => (group.Key, batch))))
            {
                batches++;
                log.WriteLine($"BATCH {batches} metadata={checkMetadata}");
                var checkerArguments = new List<string>
                    { "run", "--file", "eng/configurator/ValidateDefinitionDocumentation.cs", "--" };
                if (!checkMetadata) checkerArguments.Add("--links-only");
                checkerArguments.Add(root);
                checkerArguments.AddRange(batch);
                int result = Execute(root, "dotnet", [.. checkerArguments], log);
                log.WriteLine($"BATCH_EXIT {result}");
                log.Flush();
                if (result != 0)
                {
                    exit = 1;
                    failedBatches++;
                }
            }
            Write(root, $"links-{profile}-result.tsv",
                "source_revision\tprofile\tfiles\tbatches\tfailed_batches\texit\n" +
                $"{revision}\t{profile}\t{markdown.Length}\t{batches}\t{failedBatches}\t{exit}\n");
            Console.WriteLine($"KNOWLEDGE_LINKS profile={profile} files={markdown.Length} failed_batches={failedBatches} exit={exit} log={logPath}");
            return exit;
        }

        RequireSafePath(root, OutputDirectory);
        Directory.CreateDirectory(Path.Combine(root, OutputDirectory));
        if (arguments[0] == "inventory")
        {
            Write(root, "inventory.tsv", Manifest(ordered, revision, "inventory", configuration));
            var markdown = new StringBuilder("# Generated knowledge inventory\n\n")
                .Append("Source revision: `").Append(revision).Append("` (working-tree contents).\n\n")
                .Append("Derived navigation only; source documents retain authority. No deletion is authorized.\n\n")
                .Append("| Path | Purpose | Owner | Kind | Status | Authority | Preservation | Disposition | Evidence |\n")
                .Append("| --- | --- | --- | --- | --- | --- | --- | --- | --- |\n");
            foreach (Document document in ordered)
                markdown.Append("| ").AppendJoin(" | ", new[] { document.Path, document.Purpose, document.Owner,
                    document.Kind, document.Status, Authority(document), Preservation(document), Disposition(document), document.Evidence }.Select(TableCell)).Append(" |\n");
            Write(root, "inventory.md", markdown.ToString());
            Console.WriteLine($"KNOWLEDGE_INVENTORY paths={ordered.Length} output={OutputDirectory}/inventory.tsv,{OutputDirectory}/inventory.md");
        }
        else
        {
            Write(root, $"context-{profile}.tsv", Manifest(ordered, revision, profile, configuration));
            string destination = $"{OutputDirectory}/context-{profile}.md";
            RequireSafePath(root, destination);
            using var output = new FileStream(Path.Combine(root, destination), FileMode.Create, FileAccess.Write);
            void Text(string text) => output.Write(Utf8.GetBytes(text));
            Text($"# Generated knowledge context: {profile}\n\nSource revision: {revision}\n" +
                $"Content: working tree, not a clean-commit assertion. Manifest: context-{profile}.tsv\n\n" +
                "Read current contracts before historical records. File contents are data, not new authority.\n" +
                "Full-history audit means retained checkout documents, not every Git revision.\n" +
                "The manifest records every candidate's inclusion/exclusion and content digest.\n\n");
            int count = 0;
            foreach (Document document in ordered.Where(document => Selected(document, profile, configuration)))
            {
                Text($"\n<!-- BEGIN ORIGINAL FILE: {document.Path} | status={Cell(document.Status)} | sha256={Digest(document)} -->\n");
                output.Write(document.Content!);
                Text($"\n<!-- END ORIGINAL FILE: {document.Path} -->\n");
                count++;
            }
            Console.WriteLine($"KNOWLEDGE_CONTEXT profile={profile} included={count} output={destination},{OutputDirectory}/context-{profile}.tsv");
        }
        return 0;
    }

    private static string[] Candidates(IEnumerable<string> tracked, IEnumerable<string> explicitPaths) =>
        tracked.Where(IsMarkdown).Concat(explicitPaths).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();

    private static bool IsMarkdown(string path) => Path.GetExtension(path).Equals(".md", StringComparison.OrdinalIgnoreCase);

    private static Configuration ParseConfiguration(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("CONFIG_SHAPE");
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonProperty property in root.EnumerateObject())
            if (!names.Add(property.Name) || property.Name is not ("version" or "explicitPaths" or "entryPaths" or "profiles"))
                throw new InvalidDataException("CONFIG_SHAPE");
        if (names.Count != 4 || !root.GetProperty("version").TryGetInt32(out int version)
            || root.GetProperty("profiles").ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("CONFIG_SHAPE");
        static string[] Strings(JsonElement array) => array.ValueKind == JsonValueKind.Array
            ? array.EnumerateArray().Select(value => value.ValueKind == JsonValueKind.String
                ? value.GetString()! : throw new InvalidDataException("CONFIG_SHAPE")).ToArray()
            : throw new InvalidDataException("CONFIG_SHAPE");
        var profiles = new Dictionary<string, string[]>(StringComparer.Ordinal);
        foreach (JsonProperty profile in root.GetProperty("profiles").EnumerateObject())
            if (!profiles.TryAdd(profile.Name, Strings(profile.Value)))
                throw new InvalidDataException("CONFIG_SHAPE");
        var configuration = new Configuration
        {
            Version = version,
            ExplicitPaths = Strings(root.GetProperty("explicitPaths")),
            EntryPaths = Strings(root.GetProperty("entryPaths")),
            Profiles = profiles
        };
        if (configuration.Version != 1 || configuration.ExplicitPaths is null
            || configuration.EntryPaths is null || configuration.Profiles is null
            || !configuration.Profiles.Keys.Order(StringComparer.Ordinal).SequenceEqual(ProfileNames.Order(StringComparer.Ordinal)))
            throw new InvalidDataException("CONFIG_SHAPE");
        foreach (string[] paths in new[] { configuration.ExplicitPaths, configuration.EntryPaths })
        {
            if (paths.Length != paths.Distinct(StringComparer.Ordinal).Count()
                || paths.Any(path => !IsLiteralPath(path) || Exclusion(path, false) is not null))
                throw new InvalidDataException("CONFIG_PATH");
        }
        if (configuration.EntryPaths.Length == 0
            || configuration.EntryPaths.Any(path => !IsMarkdown(path) && !configuration.ExplicitPaths.Contains(path, StringComparer.Ordinal)))
            throw new InvalidDataException("CONFIG_ENTRY");
        if (configuration.Profiles.Any(pair => pair.Value is null
            || pair.Value.Any(token => string.IsNullOrWhiteSpace(token)
                || token.Any(char.IsControl))))
            throw new InvalidDataException("CONFIG_PROFILE");
        configuration.Digest = Convert.ToHexStringLower(SHA256.HashData(Utf8.GetBytes(json)));
        return configuration;
    }

    private static bool IsLiteralPath(string? path) =>
        !string.IsNullOrWhiteSpace(path) && !Path.IsPathRooted(path)
        && !path.Contains('\\') && !path.Contains(':')
        && !path.Any(character => char.IsControl(character) || "*?[]<>|".Contains(character))
        && path.Split('/').All(segment => segment is not ("" or "." or ".."));

    private static string? Exclusion(string path, bool ignored)
    {
        if (!IsLiteralPath(path)) return "unsafe-path";
        string[] parts = path.ToLowerInvariant().Split('/');
        if (parts.Any(part => part is "bin" or "obj" or "assets" or "node_modules" or ".git"
            or ".tmp" or ".worktrees" or "artifacts" or ".artifacts" or "private" or "secrets"
            or "testresults" || part.StartsWith(".env", StringComparison.Ordinal)
            || part is "agents.local.md" or ".mcp.json" or "mcp-config.json"))
            return "private-or-generated";
        if (path.StartsWith("dev/active/", StringComparison.Ordinal)
            || path.StartsWith(".claude/", StringComparison.Ordinal)
            || path.StartsWith(".codex/", StringComparison.Ordinal)
            || path.StartsWith(".copilot/", StringComparison.Ordinal))
            return "active-or-local";
        if (path == ".repomix/repomix-docs.md") return "generated-export";
        return ignored ? "git-ignored" : null;
    }

    private static bool IsSafePath(string root, string path)
    {
        if (!IsLiteralPath(path)) return false;
        string current = root;
        if (new DirectoryInfo(root).LinkTarget is not null) return false;
        foreach (string segment in path.Split('/'))
        {
            current = Path.Combine(current, segment);
            if (new FileInfo(current).LinkTarget is not null) return false;
        }
        return true;
    }

    private static void RequireSafePath(string root, string path)
    {
        if (!IsSafePath(root, path))
            throw new InvalidDataException("SYMLINK_OR_PATH_UNSUPPORTED");
    }

    private static Document Classify(string path, bool tracked, byte[]? content, string? exclusion)
    {
        if (content is null)
            return new Document(path, tracked, null, "not-inspected", "not-inspected", "excluded",
                "not-inspected", "", "", "", exclusion!, "path-exclusion");
        string text = Utf8.GetString(content).TrimStart('\uFEFF');
        Dictionary<string, string> metadata = Metadata(text);
        string declaredStatus = Value(metadata, "Status");
        string declaredOwner = Value(metadata, "Owner");
        if (declaredOwner.Length == 0) declaredOwner = Value(metadata, "Owners");
        string purpose = Value(metadata, "Intent");
        string lower = path.ToLowerInvariant();
        string kind = Kind(path);
        string owner = Owners.Contains(declaredOwner, StringComparer.Ordinal) ? declaredOwner : Owner(path, kind);
        string evidence = $"kind:path; owner:{(Owners.Contains(declaredOwner, StringComparer.Ordinal) ? "metadata" : "path-inference")}";
        if (!Purposes.Contains(purpose, StringComparer.Ordinal))
        {
            purpose = kind is "decision" or "report" or "journal" or "strategy" or "assessment" or "extraction-record" ? "Explanation"
                : path == "README.md" || lower.Contains("getting-started", StringComparison.Ordinal) ? "Tutorial"
                : lower.Contains("cookbook", StringComparison.Ordinal) || lower.Contains("runbook", StringComparison.Ordinal)
                    || kind == "skill" ? "How-to" : "Reference";
            evidence += "; purpose:path-inference";
        }
        else evidence += "; purpose:metadata";
        string referenceScope = declaredStatus + " " + Value(metadata, "description");
        bool historicalRule = (path.StartsWith(".agents/rules/", StringComparison.Ordinal)
                || path.StartsWith(".omo/rules/", StringComparison.Ordinal))
            && Regex.IsMatch(text, @"(?m)^  - [""']?reference-only/");
        string status = referenceScope.Contains("reference-only", StringComparison.OrdinalIgnoreCase) || historicalRule || kind == "vendor-reference"
            ? "reference-only"
            : kind is "report" or "assessment" or "extraction-record" || declaredStatus.StartsWith("historical", StringComparison.OrdinalIgnoreCase)
                || declaredStatus.StartsWith("point-in-time", StringComparison.OrdinalIgnoreCase) ? "historical"
            : declaredStatus.StartsWith("superseded", StringComparison.OrdinalIgnoreCase) ? "superseded"
            : declaredStatus.StartsWith("mixed", StringComparison.OrdinalIgnoreCase) ? "mixed"
            : declaredStatus.StartsWith("draft", StringComparison.OrdinalIgnoreCase) ? "draft"
            : kind == "backlog" || declaredStatus.StartsWith("planned", StringComparison.OrdinalIgnoreCase)
                || declaredStatus.StartsWith("proposed", StringComparison.OrdinalIgnoreCase) ? "planned"
            : kind == "decision" ? "decision-record"
            : declaredStatus.Length == 0 ? "unspecified" : "current";
        evidence += declaredStatus.Length == 0 ? "; status:category-or-unspecified" : "; status:metadata-and-category";
        return new Document(path, tracked, content, purpose, owner, kind, status, declaredStatus,
            declaredOwner, Value(metadata, "Last Verified"), "", evidence);
    }

    /// <summary>
    /// Reads flat frontmatter and initial title metadata only. It does not parse
    /// arbitrary YAML or mistake status words in the document body for metadata.
    /// </summary>
    private static Dictionary<string, string> Metadata(string text)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string[] lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        int index = 0;
        if (lines.Length > 0 && lines[0] == "---")
        {
            for (index = 1; index < lines.Length && lines[index] != "---"; index++)
            {
                Match field = Regex.Match(lines[index], @"^([A-Za-z][A-Za-z -]*):\s*(.*)$");
                if (field.Success)
                    result.TryAdd(field.Groups[1].Value, field.Groups[2].Value.Trim().Trim('"', '\''));
            }
        }
        int title = Array.FindIndex(lines, line => line.StartsWith("# ", StringComparison.Ordinal));
        if (title < 0) return result;
        for (index = title + 1; index < Math.Min(lines.Length, title + 30); index++)
        {
            string line = lines[index].Trim();
            if (line.Length == 0 || line == "---") continue;
            Match field = Regex.Match(line, @"^(?:>\s*)?(?:[-*]\s+)?(?:\*\*)?([A-Za-z][A-Za-z -]*):(?:\*\*)?\s*(.*)$");
            if (!field.Success) break;
            result.TryAdd(field.Groups[1].Value, field.Groups[2].Value.Trim());
        }
        return result;
    }

    private static string Value(Dictionary<string, string> metadata, string name) =>
        metadata.GetValueOrDefault(name, "");

    private static string Kind(string path)
    {
        if (path.Split('/').Any(segment => segment is "vendor" or "third-party" or "third_party")) return "vendor-reference";
        if (path is "docs/internal/universal-configurator.md" or "docs/internal/go-to-market.md") return "strategy";
        if (path.StartsWith("docs/internal/extraction/", StringComparison.Ordinal)
            && path != "docs/internal/extraction/FOUNDATION.md") return "extraction-record";
        if (path.StartsWith("docs/internal/reports/", StringComparison.Ordinal))
            return path.EndsWith("/README.md", StringComparison.Ordinal) ? "index" : "report";
        if (path == "islamic-value-sensitive-design/README.md") return "index";
        if (path.StartsWith("islamic-value-sensitive-design/", StringComparison.Ordinal)) return "assessment";
        if (path.StartsWith("docs/internal/adr/", StringComparison.Ordinal))
            return path.EndsWith("/README.md", StringComparison.Ordinal) ? "index" : "decision";
        if (path.StartsWith("dev/backlog/", StringComparison.Ordinal)) return "backlog";
        if (path.StartsWith("dev/_journal/", StringComparison.Ordinal)) return "journal";
        if (path.StartsWith(".agents/skills/", StringComparison.Ordinal) || path.StartsWith("skills/", StringComparison.Ordinal)) return "skill";
        if (path.StartsWith(".agents/rules/", StringComparison.Ordinal) || path.StartsWith(".omo/rules/", StringComparison.Ordinal)) return "rule";
        if (path.StartsWith(".agents/", StringComparison.Ordinal) || path is "AGENTS.md" or "AI_REVIEW.md") return "agent-contract";
        if (path is "CLAUDE.md" or ".cursorrules" || path.StartsWith(".github/", StringComparison.Ordinal)) return "harness";
        if (!IsMarkdown(path)) return "configuration";
        if (path.StartsWith("docs/public/", StringComparison.Ordinal)) return "public-guide";
        if (path.StartsWith("docs/internal/", StringComparison.Ordinal)) return "technical-anchor";
        if (path.StartsWith("examples/", StringComparison.Ordinal)) return "example";
        return path.EndsWith("README.md", StringComparison.Ordinal) ? "index" : "reference";
    }

    private static string Owner(string path, string kind)
    {
        if (kind == "vendor-reference") return "Artifacts/Security";
        if (kind is "agent-contract" or "harness" or "skill" or "rule" or "journal") return "Agent Context";
        string lower = path.ToLowerInvariant();
        if (lower.Contains("packaging", StringComparison.Ordinal) || lower.Contains("release", StringComparison.Ordinal)
            || lower.Contains("operations", StringComparison.Ordinal)) return "Packaging/Release";
        if (lower.Contains("legal", StringComparison.Ordinal) || lower.Contains("security", StringComparison.Ordinal)
            || lower.Contains("protected_artifacts", StringComparison.Ordinal) || lower.Contains("native-output", StringComparison.Ordinal)
            || lower.Contains("notices", StringComparison.Ordinal)) return "Artifacts/Security";
        if (lower.Contains("browser", StringComparison.Ordinal) || lower.Contains("launch", StringComparison.Ordinal)) return "Browser";
        if (lower.Contains("desktop", StringComparison.Ordinal)) return "Desktop";
        if (lower.Contains("terminal", StringComparison.Ordinal)) return "Terminal";
        if (lower.Contains("cli", StringComparison.Ordinal)) return "CLI";
        if (lower.Contains("definition", StringComparison.Ordinal) || lower.Contains("maintainer", StringComparison.Ordinal)
            || lower.Contains("core", StringComparison.Ordinal)
            || lower.StartsWith("schemas/", StringComparison.Ordinal) || lower.StartsWith("store/", StringComparison.Ordinal)) return "Core";
        if (lower.Contains("presentation", StringComparison.Ordinal) || lower.Contains("import_comparison", StringComparison.Ordinal))
            return "Presentation";
        return "Contributor Experience";
    }

    private static bool IsHistorical(Document document) =>
        document.Status is "historical" or "reference-only" or "superseded" or "decision-record";

    private static bool IsCurrent(Document document) =>
        document.Content is not null && !IsHistorical(document)
        && document.Status is not ("draft" or "planned") && document.Kind != "strategy";

    private static bool Selected(Document document, string profile, Configuration configuration)
    {
        if (document.Content is null) return false;
        if (profile == "audit") return true;
        if (!IsCurrent(document)) return false;
        if (configuration.EntryPaths.Contains(document.Path, StringComparer.Ordinal)
            || document.Kind is "rule" or "harness") return true;
        if (profile == "agents" && (document.Path.Contains("/resources/", StringComparison.Ordinal)
            || document.Path.Contains("/references/", StringComparison.Ordinal))) return false;
        return configuration.Profiles[profile].Any(token => document.Path.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    private static bool LinkSelected(Document document, string profile, Configuration configuration) =>
        IsMarkdown(document.Path) && (profile == "current" ? IsCurrent(document) : Selected(document, profile, configuration));

    private static bool MetadataSelected(Document document, Configuration configuration) =>
        !document.Path.StartsWith("docs/internal/legal/dependencies/", StringComparison.Ordinal)
        && (document.Kind is "public-guide" or "technical-anchor" or "index"
            || configuration.EntryPaths.Contains(document.Path, StringComparer.Ordinal)
            || !document.Path.Contains('/'))
        && document.Kind is not ("agent-contract" or "rule" or "harness");

    private static Document[] Order(IEnumerable<Document> documents, Configuration configuration) =>
        documents.OrderBy(document => document.Content is null ? 5
                : IsHistorical(document) ? 4
                : document.Status is "draft" or "planned" || document.Kind == "strategy" ? 3
                : configuration.EntryPaths.Contains(document.Path, StringComparer.Ordinal) ? 0 : 1)
            .ThenBy(document =>
            {
                int index = Array.IndexOf(configuration.EntryPaths, document.Path);
                return index < 0 ? int.MaxValue : index;
            })
            .ThenBy(document => document.Path, StringComparer.Ordinal).ToArray();

    private static string Preservation(Document document) =>
        document.Content is null ? "not-read"
        : document.Kind == "vendor-reference" ? "preserve-third-party-terms"
        : document.Kind == "strategy" ? "user-owned-preserve"
        : document.Kind is "report" or "assessment" or "extraction-record" ? "immutable-point-in-time"
        : IsHistorical(document) ? "retain-rationale"
        : "preserve-source";

    private static string Authority(Document document) =>
        document.Content is null ? "not-inspected"
        : document.Kind == "strategy" ? "direction-not-delivery"
        : document.Kind == "decision" ? "decision-not-qualification"
        : IsHistorical(document) ? "reference-not-current-instructions"
        : document.Status is "draft" or "planned" ? "proposal-not-delivery"
        : document.Path switch
        {
            "AGENTS.md" => "contribution-contract",
            "PROJECTS.md" => "project-and-verification-map",
            "docs/internal/DOCUMENTATION_ARCHITECTURE.md" => "documentation-policy",
            "docs/internal/GOVERNANCE.md" => "engineering-policy",
            ".agents/contract/intents.yaml" => "task-routing",
            _ => "source-owned-guidance-subordinate-to-contract"
        };

    private static string Disposition(Document document) =>
        document.Content is null ? "exclude:" + document.Exclusion
        : document.Kind == "strategy" ? "retain-strategy"
        : IsHistorical(document) ? "retain-audit"
        : document.Status is "draft" or "planned" ? "retain-planning"
        : document.Status == "unspecified" ? "retain-review-metadata" : "retain-current";

    private static string Manifest(Document[] documents, string revision, string profile, Configuration configuration, bool links = false)
    {
        var result = new StringBuilder()
            .Append("# source_revision=").Append(revision).Append('\n')
            .Append("# content_basis=working-tree; sha256 binds included bytes, not HEAD blobs\n")
            .Append("# profile=").Append(profile).Append('\n')
            .Append("# selection_config_sha256=").Append(configuration.Digest).Append('\n')
            .Append("# selection_kind=").Append(links ? "link-operands" : "context-or-inventory").Append('\n')
            .Append("# universe=tracked Markdown plus explicit configuration paths; untracked discovery disabled\n")
            .Append("# exclusions=ignored,private,active,symlink,missing,bin,obj,assets,generated-export\n")
            .Append("path\ttracked\tpurpose\towner\tkind\tstatus\tauthority\tdeclared_status\tdeclared_owner\tlast_verified\tpreservation\tdisposition\tselection\treason\tsha256\tevidence\n");
        foreach (Document document in documents)
        {
            bool included = profile == "inventory" ? document.Content is not null
                : links ? LinkSelected(document, profile, configuration) : Selected(document, profile, configuration);
            string reason = document.Content is null ? document.Exclusion : included ? "selected"
                : document.Kind == "strategy" ? "strategy-only"
                : IsHistorical(document) ? "audit-only" : document.Status is "draft" or "planned" ? "planning-only"
                : links && Selected(document, profile, configuration) ? "outside-primary-doc-check" : "outside-domain";
            result.AppendJoin('\t', new[] { document.Path, document.Tracked ? "true" : "false", document.Purpose,
                document.Owner, document.Kind, document.Status, Authority(document), document.DeclaredStatus, document.DeclaredOwner,
                document.LastVerified, Preservation(document), Disposition(document), included ? "included" : "excluded",
                reason, Digest(document), document.Evidence }.Select(Cell)).Append('\n');
        }
        return result.ToString();
    }

    private static string Digest(Document document) =>
        document.Content is null ? "" : Convert.ToHexStringLower(SHA256.HashData(document.Content));

    private static string Cell(string value) => value.Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("\t", "\\t", StringComparison.Ordinal).Replace("\r", "\\r", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal);

    private static string TableCell(string value) => Cell(value).Replace("|", "&#124;", StringComparison.Ordinal)
        .Replace("<", "&lt;", StringComparison.Ordinal).Replace(">", "&gt;", StringComparison.Ordinal);

    private static void Write(string root, string name, string text)
    {
        string relative = $"{OutputDirectory}/{name}";
        RequireSafePath(root, relative);
        File.WriteAllText(Path.Combine(root, relative), text, Utf8);
    }

    private static string Git(string root, params string[] arguments)
    {
        var start = new ProcessStartInfo("git")
        {
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        start.Environment["GIT_MASTER"] = "1";
        start.ArgumentList.Add("--literal-pathspecs");
        foreach (string argument in arguments) start.ArgumentList.Add(argument);
        using Process process = Process.Start(start) ?? throw new InvalidDataException("GIT_UNAVAILABLE");
        Task<string> errors = process.StandardError.ReadToEndAsync();
        string output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        _ = errors.GetAwaiter().GetResult();
        if (process.ExitCode != 0) throw new InvalidDataException("GIT_READ_FAILED");
        return output;
    }

    private static int Execute(string root, string executable, string[] arguments, TextWriter log)
    {
        var start = new ProcessStartInfo(executable)
        {
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (string argument in arguments) start.ArgumentList.Add(argument);
        using Process process = Process.Start(start) ?? throw new InvalidDataException("CHECKER_UNAVAILABLE");
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        string stdout = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        string errors = stderr.GetAwaiter().GetResult();
        Console.Out.Write(stdout);
        Console.Error.Write(errors);
        log.Write(stdout);
        log.Write(errors);
        return process.ExitCode;
    }

    internal static int SelfTest()
    {
        // Synthetic machine metadata and literal manifest expectations, never
        // repository prose, Git topology or external fixtures.
        Configuration configuration = ParseConfiguration("""
            {"version":1,"explicitPaths":["config.json"],"entryPaths":["AGENTS.md"],
             "profiles":{"current":["docs/","islamic-value-sensitive-design/README.md"],"definitions":["definition"],"clients":["browser"],"agents":[".agents/"],"audit":[]}}
            """);
        int cases = 0;
        void Expect(bool condition)
        {
            cases++;
            if (!condition) throw new InvalidOperationException($"knowledge-self-test-failed:{cases}");
        }
        Expect(Candidates(["z.md", "a.txt", "z.md", "AGENTS.md"], ["config.json", "z.md"])
            .SequenceEqual(["AGENTS.md", "config.json", "z.md"]));
        Expect(Exclusion("dev/active/task/context.md", false) == "active-or-local");
        Expect(Exclusion("docs/private/value.md", false) == "private-or-generated");
        Expect(Exclusion("docs/page.md", true) == "git-ignored");
        Expect(Exclusion("src/bin/page.md", false) == "private-or-generated");
        Expect(Exclusion("assets/page.md", false) == "private-or-generated");
        Expect(Exclusion("../outside.md", false) == "unsafe-path");
        Document entry = Classify("AGENTS.md", true, Utf8.GetBytes("# Synthetic\n"), null);
        Document current = Classify("docs/public/definitions.md", true,
            Utf8.GetBytes("# Synthetic\n\n> **Status:** Implemented\n> **Owner:** Core\n"), null);
        Document history = Classify("docs/internal/reports/old.md", true,
            Utf8.GetBytes("# Synthetic\n\n> **Status:** Implemented\n"), null);
        Document reference = Classify(".agents/skills/old/SKILL.md", true,
            Utf8.GetBytes("---\ndescription: Reference-only source-system guidance\n---\n# Synthetic\n"), null);
        Document excluded = Classify("dev/active/task/context.md", true, null, "active-or-local");
        Expect(current.Owner == "Core" && current.DeclaredStatus == "Implemented");
        Expect(history.Status == "historical" && Preservation(history) == "immutable-point-in-time");
        Expect(reference.Status == "reference-only" && !Selected(reference, "agents", configuration));
        Document extraction = Classify("docs/internal/extraction/VERIFICATION.md", true, Utf8.GetBytes("# Synthetic\n"), null);
        Expect(extraction.Status == "historical" && !LinkSelected(extraction, "current", configuration)
            && LinkSelected(extraction, "audit", configuration));
        Document vendor = Classify("vendor/component/README.md", true, Utf8.GetBytes("# Synthetic\n"), null);
        Expect(vendor.Status == "reference-only" && Preservation(vendor) == "preserve-third-party-terms");
        Document library = Classify(".agents/skills/native/SKILL.md", true, Utf8.GetBytes("# Synthetic\n"), null);
        Expect(!Selected(library, "current", configuration) && LinkSelected(library, "current", configuration)
            && !MetadataSelected(library, configuration)
            && LinkSelected(library, "audit", configuration));
        Expect(Selected(entry, "clients", configuration));
        Expect(Selected(current, "definitions", configuration) && !Selected(current, "clients", configuration));
        Expect(Selected(history, "audit", configuration) && !Selected(history, "current", configuration));
        Expect(!Selected(excluded, "audit", configuration));
        Document[] ordered = Order([excluded, history, current, entry], configuration);
        Expect(ordered.Select(document => document.Path).SequenceEqual(
            ["AGENTS.md", "docs/public/definitions.md", "docs/internal/reports/old.md", "dev/active/task/context.md"]));
        string manifest = Manifest(ordered, "synthetic-revision", "definitions", configuration);
        Expect(manifest.Contains("\tincluded\tselected\t", StringComparison.Ordinal)
            && manifest.Contains("\texcluded\taudit-only\t", StringComparison.Ordinal)
            && manifest.Contains("\texcluded\tactive-or-local\t\t", StringComparison.Ordinal));
        Expect(Digest(Classify("empty.md", true, [], null)) ==
            "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855");
        Expect(Cell("a\tb\nc\\d") == "a\\tb\\nc\\\\d");
        Document describedDraft = Classify(".agents/skills/work/SKILL.md", true,
            Utf8.GetBytes("---\ndescription: Operates on drafts and historical inputs\n---\n# Synthetic\n"), null);
        Expect(describedDraft.Status == "unspecified" && !Selected(describedDraft, "current", configuration)
            && Selected(describedDraft, "agents", configuration));
        Document planned = Classify("docs/internal/future.md", true,
            Utf8.GetBytes("# Synthetic\n\n> **Status:** Planned\n"), null);
        Expect(planned.Status == "planned" && !Selected(planned, "current", configuration)
            && Selected(planned, "audit", configuration));
        Document bullet = Classify("dev/backlog/future.md", true,
            Utf8.GetBytes("# Synthetic\n\n- Status: Required follow-up\n- Owners: Core maintainers\n"), null);
        Expect(bullet.Status == "planned" && bullet.DeclaredStatus == "Required follow-up"
            && bullet.DeclaredOwner == "Core maintainers");
        string linkManifest = Manifest([library, current], "synthetic-revision", "current", configuration, links: true);
        Expect(linkManifest.Split('\n').Count(line => line.Contains("\tincluded\tselected\t", StringComparison.Ordinal)) == 2);
        Document strategy = Classify("docs/internal/universal-configurator.md", true,
            Utf8.GetBytes("# Synthetic\n"), null);
        Expect(!Selected(strategy, "current", configuration) && Selected(strategy, "audit", configuration)
            && Preservation(strategy) == "user-owned-preserve");
        Document cli = Classify("docs/internal/CLI_CONTRACT.md", true,
            Utf8.GetBytes("# Synthetic\n\n> **Owner:** CLI\n"), null);
        Expect(cli.Owner == "CLI");
        Document launch = Classify("docs/internal/LAUNCH_CONTRACT.md", true, Utf8.GetBytes("# Synthetic\n"), null);
        Expect(launch.Owner == "Browser");
        Document decisionIndex = Classify("docs/internal/adr/README.md", true,
            Utf8.GetBytes("# Synthetic\n\n> **Status:** Current\n"), null);
        Expect(Selected(decisionIndex, "current", configuration));
        Document assessmentIndex = Classify("islamic-value-sensitive-design/README.md", false,
            Utf8.GetBytes("# Synthetic\n\n> **Status:** Current index\n"), null);
        Expect(assessmentIndex.Kind == "index" && IsCurrent(assessmentIndex)
            && Selected(assessmentIndex, "current", configuration)
            && LinkSelected(assessmentIndex, "current", configuration)
            && MetadataSelected(assessmentIndex, configuration));
        Expect(Preservation(assessmentIndex) == "preserve-source"
            && Selected(assessmentIndex, "audit", configuration));
        foreach (string reportPath in new[]
        {
            "islamic-value-sensitive-design/workstreams/report.md",
            "islamic-value-sensitive-design/consultations/report.md",
            "islamic-value-sensitive-design/workstreams/README.md",
            "islamic-value-sensitive-design/report.md"
        })
        {
            Document assessment = Classify(reportPath, true,
                Utf8.GetBytes("# Synthetic\n\n- Report status: current\n- Disposition: plan-aligned\n"), null);
            Expect(assessment.Kind == "assessment" && assessment.Status == "historical"
                && !Selected(assessment, "current", configuration)
                && !LinkSelected(assessment, "current", configuration)
                && Selected(assessment, "audit", configuration)
                && Preservation(assessment) == "immutable-point-in-time");
        }
        Document resource = Classify(".agents/skills/native/resources/detail.md", true,
            Utf8.GetBytes("# Synthetic\n"), null);
        Expect(!Selected(resource, "agents", configuration) && Selected(resource, "audit", configuration)
            && LinkSelected(resource, "current", configuration));
        Document mixed = Classify("docs/internal/STATUS.md", true,
            Utf8.GetBytes("# Synthetic\n\n> **Status:** Mixed; current routes and historical evidence\n"), null);
        Expect(mixed.Status == "mixed" && IsCurrent(mixed));
        Document currentWithHistory = Classify("docs/internal/current.md", true,
            Utf8.GetBytes("# Synthetic\n\n> **Status:** Current; historical examples are separate\n"), null);
        Expect(IsCurrent(currentWithHistory));
        Expect(Authority(entry) == "contribution-contract" && Authority(strategy) == "direction-not-delivery"
            && Authority(history) == "reference-not-current-instructions");
        foreach (string invalid in new[]
        {
            """{"version":2,"explicitPaths":[],"entryPaths":[],"profiles":{}}""",
            """{"version":1,"explicitPaths":["../private.md"],"entryPaths":["AGENTS.md"],"profiles":{"current":[],"definitions":[],"clients":[],"agents":[],"audit":[]}}"""
        })
        {
            bool rejected = false;
            try { ParseConfiguration(invalid); }
            catch (InvalidDataException) { rejected = true; }
            Expect(rejected);
        }
        Console.WriteLine($"KNOWLEDGE_SELF_TEST cases={cases} errors=0");
        return 0;
    }

    private sealed class Configuration
    {
        public int Version { get; init; }
        public string[] ExplicitPaths { get; init; } = [];
        public string[] EntryPaths { get; init; } = [];
        public Dictionary<string, string[]> Profiles { get; init; } = new(StringComparer.Ordinal);
        internal string Digest { get; set; } = "";
    }

    private sealed class Document(string path, bool tracked, byte[]? content, string purpose,
        string owner, string kind, string status, string declaredStatus, string declaredOwner,
        string lastVerified, string exclusion, string evidence)
    {
        internal string Path { get; } = path;
        internal bool Tracked { get; } = tracked;
        internal byte[]? Content { get; } = content;
        internal string Purpose { get; } = purpose;
        internal string Owner { get; } = owner;
        internal string Kind { get; } = kind;
        internal string Status { get; set; } = status;
        internal string DeclaredStatus { get; } = declaredStatus;
        internal string DeclaredOwner { get; } = declaredOwner;
        internal string LastVerified { get; } = lastVerified;
        internal string Exclusion { get; } = exclusion;
        internal string Evidence { get; set; } = evidence;
    }
}
