#:property RestorePackagesWithLockFile=false
#:property AssemblyName=Configurator.DefinitionStoreBuilder
#:property RootNamespace=Configurator.Engineering
#:project ../../src/Configurator.Core/Configurator.Core.csproj

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Globalization;
using Configurator.Core.Definitions;
using Configurator.Core.Definitions.Authoring;

return DefinitionStoreBuilder.Run(args);

internal static class DefinitionStoreBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private const int MaximumMetadataBytes = 16 * 1024;
    private const int MaximumDefinitionBytes = 4 * 1024 * 1024;
    private const int MaximumIndexBytes = 4 * 1024 * 1024;
    private const int MaximumProjects = 1024;
    private const int MaximumEntries = 4096;
    private const int MaximumProjectRevisions = 64;
    private static readonly string[] CurationMembers = ["schemaVersion", "tags", "slug", "slugReason"];

    internal static int Run(string[] arguments)
    {
        try
        {
            if (arguments is ["--self-test"]) return SelfTest();
            if (arguments.Length >= 5 && arguments[0] == "--source" && arguments[2] == "--output"
                && arguments[^1] is "--write" or "--check")
            {
                string schema = "schemas/definition-store-curation-v1.schema.json";
                string? baseline = null;
                bool required = false;
                var options = new HashSet<string>(StringComparer.Ordinal);
                for (int index = 4; index < arguments.Length - 1; index++)
                {
                    string option = arguments[index];
                    if (!options.Add(option)) throw new StoreException("store-arguments-invalid");
                    if (option == "--require-baseline") { required = true; continue; }
                    if (option is not ("--schema" or "--baseline") || ++index >= arguments.Length - 1)
                        throw new StoreException("store-arguments-invalid");
                    if (option == "--schema") schema = arguments[index];
                    else baseline = arguments[index];
                }
                if (required && baseline is null) throw new StoreException("store-baseline-required");
                return Generate(arguments[1], arguments[3], schema, arguments[^1] == "--check", baseline);
            }
            Console.Error.WriteLine("Usage: BuildDefinitionStore.cs (--self-test|--source root --output directory [--schema path] [--baseline inventory] [--require-baseline] (--write|--check))");
            return 64;
        }
        catch (StoreException exception) { Console.Error.WriteLine(exception.Code); return 1; }
        catch (SetupDefinitionException exception) { Console.Error.WriteLine(exception.Code); return 1; }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or ArgumentException
            or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            Console.Error.WriteLine("store-invalid");
            return 1;
        }
    }

    internal static SortedDictionary<string, byte[]> Build(string source, string? baseline = null)
    {
        source = Path.GetFullPath(source);
        RequireUnlinked(source);
        if (!Directory.Exists(source)) throw new StoreException("store-source-unavailable");
        string[] children = Directory.EnumerateFileSystemEntries(source).Take(MaximumProjects + 2).ToArray();
        if (children.Length > MaximumProjects + 1) throw new StoreException("store-project-limit");
        foreach (string child in children) RequireUnlinked(child);
        string[] directories = children.Where(Directory.Exists).Order(StringComparer.Ordinal).ToArray();
        if (directories.Length > MaximumProjects) throw new StoreException("store-project-limit");
        if (children.Any(child => !Directory.Exists(child) && Path.GetFileName(child) != "README.md"))
            throw new StoreException("store-layout-invalid");

        var ids = new HashSet<string>(StringComparer.Ordinal);
        var admitted = new List<(JsonObject Metadata, SetupDefinition[] Definitions)>();
        int entryCount = 0;
        foreach (string directory in directories)
        {
            (JsonObject metadata, SetupDefinition current) = ReadProject(directory);
            string id = metadata["projectId"]!.GetValue<string>();
            if (!ids.Add(id)) throw new StoreException("store-project-duplicate");
            if (id != Path.GetFileName(directory)) throw new StoreException("store-folder-mismatch");
            var definitions = new List<SetupDefinition>();
            string releases = Path.Combine(directory, "releases");
            RequireUnlinked(releases);
            string[] files = Directory.Exists(releases) ? Directory.EnumerateFileSystemEntries(releases)
                .Take(MaximumProjectRevisions + 1).ToArray() : [];
            if (files.Length > MaximumProjectRevisions) throw new StoreException("store-revision-limit");
            foreach (string path in files)
            {
                RequireUnlinked(path);
                string name = Path.GetFileName(path);
                if (!File.Exists(path) || !name.EndsWith(".json", StringComparison.Ordinal)
                    || !long.TryParse(name.AsSpan(0, name.Length - 5), NumberStyles.None, CultureInfo.InvariantCulture, out long revision)
                    || revision < 1 || name != revision.ToString(CultureInfo.InvariantCulture) + ".json")
                    throw new StoreException("store-path-invalid");
                SetupDefinition definition = SetupDefinitionCodec.ParseJson(ReadBounded(path, MaximumDefinitionBytes, "document-limit"));
                CheckPublicFields(definition.Fields);
                if (definition.Revision != revision || definition.FamilyId != current.FamilyId || definition.ApplicationId != current.ApplicationId)
                    throw new StoreException("store-identity-mismatch");
                definitions.Add(definition);
            }
            SetupDefinition? recorded = definitions.SingleOrDefault(definition => definition.Revision == current.Revision);
            if (recorded is null) definitions.Add(current);
            else if (recorded.Digest != current.Digest) throw new StoreException("revision-conflict");
            if (definitions.Count > MaximumProjectRevisions) throw new StoreException("store-revision-limit");
            entryCount += definitions.Count;
            if (entryCount > MaximumEntries) throw new StoreException("store-entry-limit");
            admitted.Add((metadata, definitions.OrderBy(definition => definition.Revision).ToArray()));
        }
        _ = new SetupDefinitionRegistry(admitted.SelectMany(project => project.Definitions));
        if (admitted.Any(project => project.Definitions.Select(definition => definition.Revision).Distinct().Count() != project.Definitions.Length))
            throw new StoreException("store-revision-duplicate");
        if (admitted.Select(project => project.Definitions[0].FamilyId).Distinct().Count() != admitted.Count)
            throw new StoreException("store-project-rebinding");

        var outputs = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);
        var projects = new JsonArray();
        foreach ((JsonObject metadata, SetupDefinition[] definitions) in admitted)
        {
            var revisions = new JsonArray();
            foreach (SetupDefinition definition in definitions)
            {
                string path = "definitions/" + metadata["projectId"]!.GetValue<string>() + "/" + definition.Revision.ToString(CultureInfo.InvariantCulture) + ".json";
                outputs.Add(path, definition.CanonicalBytes.ToArray());
                revisions.Add((JsonNode)new JsonObject
                {
                    ["revision"] = definition.Revision, ["digest"] = definition.Digest.Value,
                    ["publisher"] = definition.Publisher, ["title"] = definition.Title,
                    ["applicationVersions"] = Strings(definition.ApplicationVersions),
                    ["entryPoints"] = Strings(definition.EntryPoints), ["locales"] = Strings(definition.Locales),
                    ["outputNames"] = Strings(definition.Artifacts.Select(artifact => artifact.FileName)), ["snapshotPath"] = path
                });
            }
            projects.Add((JsonNode)new JsonObject
            {
                ["projectId"] = metadata["projectId"]!.DeepClone(), ["name"] = metadata["name"]!.DeepClone(),
                ["description"] = metadata["description"]!.DeepClone(), ["sourceRepository"] = metadata["sourceRepository"]!.DeepClone(),
                ["license"] = metadata["license"]!.DeepClone(), ["tags"] = metadata["tags"]!.DeepClone(),
                ["applicationId"] = definitions[0].ApplicationId, ["familyId"] = definitions[0].FamilyId.ToString("D"), ["revisions"] = revisions
            });
        }
        byte[] index = Encoding.UTF8.GetBytes(new JsonObject { ["schemaVersion"] = 1, ["projects"] = projects }.ToJsonString(JsonOptions) + "\n");
        if (index.Length > MaximumIndexBytes) throw new StoreException("store-index-limit");
        outputs.Add("index.json", index);
        if (baseline is not null) ValidateBaseline(source, Path.GetFullPath(baseline), outputs);
        return outputs;
    }

    private static JsonArray Strings(IEnumerable<string> values) => new(values.Select(value => (JsonNode?)JsonValue.Create(value)).ToArray());

    private static (JsonObject Metadata, SetupDefinition Definition) ReadProject(string directory)
    {
        string[] children = Directory.EnumerateFileSystemEntries(directory).Take(5).ToArray();
        foreach (string child in children) RequireUnlinked(child);
        string[] sources = children.Where(path => Path.GetFileName(path) is "configurator.json" or "configurator.yaml" or "configurator.yml").ToArray();
        if (sources.Length != 1) throw new StoreException("store-source-ambiguous");
        if (children.Any(path => Path.GetFileName(path) is not ("configurator.json" or "configurator.yaml" or "configurator.yml" or "curation.json" or "releases")))
            throw new StoreException("store-layout-invalid");
        if (children.Any(path => Path.GetFileName(path) == "releases" ? !Directory.Exists(path) : !File.Exists(path)))
            throw new StoreException("store-layout-invalid");
        byte[] bytes = ReadBounded(sources[0], MaximumDefinitionBytes, "document-limit");
        DefinitionAuthoringDocument source = Path.GetExtension(sources[0]) == ".json"
            ? DefinitionAuthoringCodec.ParseJson(bytes) : DefinitionAuthoringCodec.ParseYaml(bytes);
        if (!source.Root.TryGetProperty("project", out JsonElement project) || project.ValueKind != JsonValueKind.Object)
            throw new StoreException("store-metadata-invalid");
        string ProjectText(string member, int limit) => project.TryGetProperty(member, out JsonElement value)
            ? ReadText(value, limit) : throw new StoreException("store-metadata-invalid");
        string id = ProjectText("id", 128);
        string slug = string.Join('-', id.ToLowerInvariant().Split(['.', '_', '-'], StringSplitOptions.RemoveEmptyEntries));
        JsonObject curation = File.Exists(Path.Combine(directory, "curation.json")) ? ReadCuration(Path.Combine(directory, "curation.json")) : new();
        if (curation["slug"] is JsonNode exception) slug = exception.GetValue<string>();
        if (!IsSlug(slug, 64)) throw new StoreException("store-slug-invalid");
        string repository = ProjectText("repository", 2048);
        if (!Uri.TryCreate(repository, UriKind.Absolute, out Uri? uri) || uri.Scheme != Uri.UriSchemeHttps
            || uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0 || uri.Host.Length == 0
            || repository.Contains('\\') || repository.Contains('%') || uri.IsLoopback || uri.HostNameType != UriHostNameType.Dns)
            throw new StoreException("store-metadata-invalid");
        var metadata = new JsonObject
        {
            ["projectId"] = slug, ["name"] = ProjectText("name", 128),
            ["description"] = ProjectText("description", 2048),
            ["sourceRepository"] = repository,
            ["license"] = ProjectText("definitionLicense", 128),
            ["tags"] = curation["tags"]?.DeepClone() ?? new JsonArray()
        };
        SetupDefinition definition = DefinitionAuthoringCompiler.Compile(source);
        CheckPublicFields(definition.Fields);
        return (metadata, definition);
    }

    private static JsonObject ReadCuration(string path)
    {
        RequireUnlinked(path);
        byte[] bytes = ReadBounded(path, MaximumMetadataBytes, "store-metadata-limit");
        try
        {
            using JsonDocument document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 4 });
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) throw new StoreException("store-metadata-invalid");
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonProperty property in root.EnumerateObject())
                if (!names.Add(property.Name) || !CurationMembers.Contains(property.Name, StringComparer.Ordinal))
                    throw new StoreException("store-metadata-invalid");
            if (!names.Contains("schemaVersion") || root.GetProperty("schemaVersion").GetInt32() != 1)
                throw new StoreException("store-metadata-invalid");
            if (names.Contains("slug") != names.Contains("slugReason")) throw new StoreException("store-metadata-invalid");
            if (names.Contains("slug"))
            {
                if (!IsSlug(ReadText(root.GetProperty("slug"), 64), 64)
                    || ReadText(root.GetProperty("slugReason"), 512).Length < 16) throw new StoreException("store-metadata-invalid");
            }
            if (root.TryGetProperty("tags", out JsonElement tags))
            {
                if (tags.ValueKind != JsonValueKind.Array || tags.GetArrayLength() > 16) throw new StoreException("store-metadata-invalid");
                var uniqueTags = new HashSet<string>(StringComparer.Ordinal);
                foreach (JsonElement tag in tags.EnumerateArray())
                {
                    string value = ReadText(tag, 32);
                    if (!IsSlug(value, 32) || !uniqueTags.Add(value)) throw new StoreException("store-metadata-invalid");
                }
            }
            return JsonNode.Parse(bytes)!.AsObject();
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or KeyNotFoundException or FormatException)
        { throw new StoreException("store-metadata-invalid"); }
    }

    private static string ReadText(JsonElement value, int maximum)
    {
        if (value.ValueKind != JsonValueKind.String) throw new StoreException("store-metadata-invalid");
        string text = value.GetString()!;
        if (text.Length is < 1 || text.Length > maximum || text != text.Trim() || text.Any(char.IsControl))
            throw new StoreException("store-metadata-invalid");
        return text;
    }

    private static bool IsSlug(string value, int maximum) => value.Length is > 0 && value.Length <= maximum
        && value[0] != '-' && value[^1] != '-' && value.All(character => character is >= 'a' and <= 'z' or >= '0' and <= '9' or '-');

    private static byte[] ReadBounded(string path, int maximum, string limitCode)
    {
        using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var output = new MemoryStream();
        byte[] buffer = new byte[8192];
        int read;
        while ((read = input.Read(buffer, 0, Math.Min(buffer.Length, maximum + 1 - (int)output.Length))) != 0)
        {
            output.Write(buffer, 0, read);
            if (output.Length > maximum) throw new StoreException(limitCode);
        }
        return output.ToArray();
    }

    private static void RequireUnlinked(string path)
    {
        for (string? current = Path.GetFullPath(path); current is not null; current = Path.GetDirectoryName(current))
        {
            FileSystemInfo item = Directory.Exists(current) ? new DirectoryInfo(current) : new FileInfo(current);
            if (item.LinkTarget is not null || item.Exists && (item.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new StoreException("store-path-invalid");
        }
    }

    private static void ValidateBaseline(string source, string baseline, SortedDictionary<string, byte[]> outputs)
    {
        if (Overlaps(source, baseline)) throw new StoreException("store-baseline-invalid");
        RequireUnlinked(baseline);
        string indexPath = Path.Combine(baseline, "index.json");
        RequireUnlinked(indexPath);
        using JsonDocument inventory = JsonDocument.Parse(ReadBounded(indexPath, MaximumIndexBytes, "store-index-limit"));
        using JsonDocument current = JsonDocument.Parse(outputs["index.json"]);
        JsonElement root = inventory.RootElement;
        RequireMembers(root, ["schemaVersion", "projects"]);
        if (root.GetProperty("schemaVersion").GetInt32() != 1) throw new StoreException("store-baseline-invalid");
        JsonElement projects = root.GetProperty("projects");
        if (projects.GetArrayLength() > MaximumProjects) throw new StoreException("store-project-limit");
        var identities = new HashSet<Guid>();
        var slugs = new HashSet<string>(StringComparer.Ordinal);
        var definitions = new List<SetupDefinition>();
        var expected = new HashSet<string>(StringComparer.Ordinal) { "index.json" };
        foreach (JsonElement project in projects.EnumerateArray())
        {
            RequireMembers(project, ["projectId", "name", "description", "sourceRepository", "license", "tags", "applicationId", "familyId", "revisions"]);
            string slug = ReadText(project.GetProperty("projectId"), 64);
            if (!IsSlug(slug, 64) || !slugs.Add(slug)) throw new StoreException("store-baseline-invalid");
            Guid family = project.GetProperty("familyId").GetGuid();
            if (!identities.Add(family)) throw new StoreException("store-baseline-invalid");
            string application = ReadText(project.GetProperty("applicationId"), 128);
            JsonElement retained = current.RootElement.GetProperty("projects").EnumerateArray()
                .SingleOrDefault(item => item.GetProperty("projectId").GetString() == slug);
            if (retained.ValueKind == JsonValueKind.Undefined) throw new StoreException("store-history-missing");
            if (retained.GetProperty("familyId").GetGuid() != family
                || retained.GetProperty("applicationId").GetString() != application)
                throw new StoreException("store-project-rebinding");
            JsonElement revisions = project.GetProperty("revisions");
            if (revisions.GetArrayLength() is < 1 or > MaximumProjectRevisions) throw new StoreException("store-baseline-invalid");
            var numbers = new HashSet<long>();
            foreach (JsonElement revision in revisions.EnumerateArray())
            {
                RequireMembers(revision, ["revision", "digest", "publisher", "title", "applicationVersions", "entryPoints", "locales", "outputNames", "snapshotPath"]);
                if (definitions.Count >= MaximumEntries) throw new StoreException("store-entry-limit");
                long number = revision.GetProperty("revision").GetInt64();
                string relative = "definitions/" + slug + "/" + number.ToString(CultureInfo.InvariantCulture) + ".json";
                if (number < 1 || !numbers.Add(number) || revision.GetProperty("snapshotPath").GetString() != relative)
                    throw new StoreException("store-baseline-invalid");
                string path = Path.Combine(baseline, relative);
                expected.Add(relative);
                RequireUnlinked(path);
                byte[] bytes = ReadBounded(path, MaximumDefinitionBytes, "document-limit");
                SetupDefinition definition = SetupDefinitionCodec.ParseJson(bytes);
                CheckPublicFields(definition.Fields);
                if (definition.FamilyId != family || definition.ApplicationId != application || definition.Revision != number
                    || definition.Digest.Value != revision.GetProperty("digest").GetString()
                    || !bytes.AsSpan().SequenceEqual(definition.CanonicalBytes.Span))
                    throw new StoreException("store-baseline-invalid");
                definitions.Add(definition);
                if (!outputs.TryGetValue(relative, out byte[]? candidate)) throw new StoreException("store-history-missing");
                if (!candidate.AsSpan().SequenceEqual(bytes)) throw new StoreException("store-history-rewritten");
            }
        }
        ValidateInventory(baseline, expected, "store-baseline-invalid");
        _ = new SetupDefinitionRegistry(definitions);
    }

    private static void RequireMembers(JsonElement value, string[] members)
    {
        if (value.ValueKind != JsonValueKind.Object) throw new StoreException("store-baseline-invalid");
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonProperty property in value.EnumerateObject())
            if (!names.Add(property.Name) || !members.Contains(property.Name, StringComparer.Ordinal))
                throw new StoreException("store-baseline-invalid");
        if (names.Count != members.Length) throw new StoreException("store-baseline-invalid");
    }

    private static void ValidateInventory(string root, HashSet<string> expected, string code)
    {
        if (!Directory.Exists(root)) return;
        var pending = new Stack<string>();
        pending.Push(root);
        int fileCount = 0;
        int directoryCount = 0;
        while (pending.TryPop(out string? directory))
        {
            foreach (string path in Directory.EnumerateFileSystemEntries(directory))
            {
                RequireUnlinked(path);
                if (Directory.Exists(path))
                {
                    if (++directoryCount > MaximumProjects + 1) throw new StoreException(code);
                    pending.Push(path);
                }
                else if (++fileCount > MaximumEntries + 1 || !expected.Contains(Path.GetRelativePath(root, path).Replace('\\', '/')))
                    throw new StoreException(code);
            }
        }
    }

    private static void CheckPublicFields(IEnumerable<SetupFieldDefinition> fields)
    {
        foreach (SetupFieldDefinition field in fields)
        {
            if (field.Sensitivity == SetupFieldSensitivity.Secret && field.Choices.Count != 0)
                throw new StoreException("store-public-data-invalid");
            CheckPublicFields(field.Properties);
        }
    }

    private static int Generate(string source, string output, string schema, bool check, string? baseline = null)
    {
        source = Path.GetFullPath(source);
        output = Path.GetFullPath(output);
        schema = Path.GetFullPath(schema);
        if (Overlaps(source, output) || Overlaps(source, schema) || Overlaps(output, schema))
            throw new StoreException("store-output-overlap");
        if (baseline is not null && (Overlaps(Path.GetFullPath(baseline), output) || Overlaps(Path.GetFullPath(baseline), schema)))
            throw new StoreException("store-baseline-invalid");
        SortedDictionary<string, byte[]> files = Build(source, baseline);
        RequireUnlinked(output);
        RequireUnlinked(schema);
        foreach (string relative in files.Keys) RequireUnlinked(Path.Combine(output, relative));
        ValidateInventory(output, files.Keys.ToHashSet(StringComparer.Ordinal), "store-output-stale");
        byte[] schemaBytes = Encoding.UTF8.GetBytes(GenerateSchema().ToJsonString(JsonOptions) + "\n");
        var missingReleases = new List<(string Path, byte[] Bytes)>();
        foreach ((string relative, byte[] bytes) in files.Where(file => file.Key != "index.json"))
        {
            string[] parts = relative.Split('/');
            string release = Path.Combine(source, parts[1], "releases", parts[2]);
            RequireUnlinked(release);
            if (!File.Exists(release)) missingReleases.Add((release, bytes));
        }
        bool Matches(string path, byte[] expected) => File.Exists(path)
            && ReadBounded(path, expected.Length, "store-output-stale").AsSpan().SequenceEqual(expected);
        if (check)
        {
            if (missingReleases.Count != 0 || !Matches(schema, schemaBytes) || files.Any(file => !Matches(Path.Combine(output, file.Key), file.Value)))
                throw new StoreException("store-output-stale");
        }
        else
        {
            foreach ((string path, byte[] bytes) in missingReleases)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                using var release = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                release.Write(bytes);
            }
            Directory.CreateDirectory(Path.GetDirectoryName(schema)!);
            File.WriteAllBytes(schema, schemaBytes);
            foreach ((string relative, byte[] bytes) in files)
            {
                string path = Path.Combine(output, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllBytes(path, bytes);
            }
        }
        Console.WriteLine(check ? $"Definition store is current ({files.Count - 1} revisions)." : $"Definition store generated ({files.Count - 1} revisions).");
        return 0;
    }

    private static bool Overlaps(string first, string second) => first == second
        || first.StartsWith(second + Path.DirectorySeparatorChar, StringComparison.Ordinal)
        || second.StartsWith(first + Path.DirectorySeparatorChar, StringComparison.Ordinal);

    private static JsonObject GenerateSchema()
    {
        JsonObject Text(int maximum) => new() { ["type"] = "string", ["minLength"] = 1, ["maxLength"] = maximum, ["pattern"] = "^\\S(?:[^\\x00-\\x1f\\x7f]*\\S)?$" };
        JsonObject Slug(int maximum) => new() { ["type"] = "string", ["minLength"] = 1, ["maxLength"] = maximum, ["pattern"] = "^[a-z0-9](?:[a-z0-9-]*[a-z0-9])?$" };
        JsonObject Array(JsonObject items, int minimum, int maximum) => new()
        { ["type"] = "array", ["minItems"] = minimum, ["maxItems"] = maximum, ["uniqueItems"] = true, ["items"] = items };
        JsonObject reason = Text(512);
        reason["minLength"] = 16;
        return new JsonObject
        {
            ["$schema"] = "https://json-schema.org/draft/2020-12/schema", ["title"] = "Definition store optional curation v1",
            ["type"] = "object", ["additionalProperties"] = false, ["required"] = Strings(["schemaVersion"]),
            ["dependentRequired"] = new JsonObject { ["slug"] = Strings(["slugReason"]), ["slugReason"] = Strings(["slug"]) },
            ["properties"] = new JsonObject
            {
                ["schemaVersion"] = new JsonObject { ["type"] = "integer", ["const"] = 1 },
                ["slug"] = Slug(64), ["slugReason"] = reason, ["tags"] = Array(Slug(32), 0, 16)
            }
        };
    }

    private static int SelfTest()
    {
        string temporary = Path.Combine(Path.GetTempPath(), "configurator-store-self-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporary);
        int passed = 0;
        int failed = 0;
        try
        {
            void Check(string name, Action<string> assertion)
            {
                string source = Path.Combine(temporary, name);
                Seed(source);
                try { assertion(source); passed++; Console.WriteLine("PASS " + name); }
                catch (Exception exception) when (exception is StoreException or SetupDefinitionException or InvalidOperationException
                    or IOException or UnauthorizedAccessException or JsonException or ArgumentException)
                { failed++; Console.WriteLine("FAIL " + name); }
            }
            void Reject(string name, string code, Action<string> mutate) => Check(name, source =>
            {
                mutate(source);
                try { _ = Build(source); }
                catch (StoreException exception) when (exception.Code == code) { return; }
                catch (SetupDefinitionException exception) when (exception.Code == code) { return; }
                throw new InvalidOperationException("Expected rejection.");
            });
            Check("deterministic-canonical-output", source =>
            {
                SortedDictionary<string, byte[]> first = Build(source);
                SortedDictionary<string, byte[]> second = Build(source);
                if (first.Count != 2 || first.Keys.Except(second.Keys).Any()
                    || first.Any(pair => !pair.Value.AsSpan().SequenceEqual(second[pair.Key])))
                    throw new InvalidOperationException("Output differs.");
                SetupDefinition definition = SetupDefinitionCodec.ParseJson(first["definitions/alpha/2.json"]);
                using JsonDocument index = JsonDocument.Parse(first["index.json"]);
                if (definition.Revision != 2 || definition.ApplicationVersions[0] != "1.0"
                    || index.RootElement.GetProperty("projects")[0].GetProperty("revisions")[0].GetProperty("digest").GetString() != definition.Digest.Value)
                    throw new InvalidOperationException("Identity differs.");
            });
            Reject("escaping-path", "store-path-invalid", source => File.WriteAllText(Path.Combine(source, "alpha/releases/not-a-revision.json"), "{}"));
            Reject("source-ambiguity", "store-source-ambiguous", source => File.WriteAllText(Path.Combine(source, "alpha/configurator.yaml"), "{}"));
            Reject("source-symlink", "store-path-invalid", source =>
            {
                string path = Path.Combine(source, "alpha/configurator.json");
                string outside = source + "-source.json";
                File.Move(path, outside);
                File.CreateSymbolicLink(path, outside);
            });
            Reject("definition-symlink", "store-path-invalid", source =>
            {
                string path = Path.Combine(source, "alpha/releases/2.json");
                string outside = source + "-outside.json";
                File.Move(path, outside);
                File.CreateSymbolicLink(path, outside);
            });
            Reject("directory-symlink", "store-path-invalid", source =>
            {
                string path = Path.Combine(source, "alpha/releases");
                string outside = source + "-outside";
                Directory.Move(path, outside);
                Directory.CreateSymbolicLink(path, outside);
            });
            Reject("unknown-metadata", "store-metadata-invalid", source => Metadata(source, data => data["script"] = "ignored-hook"));
            Reject("contributor-cannot-select-baseline", "store-metadata-invalid", source => Metadata(source, data => data["baseline"] = "releases/2.json"));
            Reject("duplicate-metadata-member", "store-metadata-invalid", source =>
            {
                string path = Path.Combine(source, "alpha/curation.json");
                File.WriteAllText(path, File.ReadAllText(path).Replace("\"schemaVersion\": 1,", "\"schemaVersion\": 1,\"schemaVersion\": 1,", StringComparison.Ordinal));
            });
            Reject("oversized-metadata", "store-metadata-limit", source => File.WriteAllText(Path.Combine(source, "alpha/curation.json"), new string(' ', 16 * 1024 + 1)));
            Reject("unjustified-slug-exception", "store-metadata-invalid", source => Metadata(source, data => data["slug"] = "alpha"));
            Reject("unsafe-repository-url", "store-metadata-invalid", source => Source(source, data => data["project"]!["repository"] = "https://user:synthetic@example.invalid/repository"));
            Reject("missing-license", "store-metadata-invalid", source => Source(source, data => data["project"]!["definitionLicense"] = " "));
            Reject("omitted-license", "store-metadata-invalid", source => Source(source, data => data["project"]!.AsObject().Remove("definitionLicense")));
            Reject("source-expansion-limit", "document-limit", source => Source(source, data =>
            {
                var fields = new JsonObject();
                for (int field = 0; field < 1025; field++) fields.Add("field-" + field.ToString(CultureInfo.InvariantCulture),
                    new JsonObject { ["type"] = "string", ["sensitivity"] = "public" });
                data["fields"] = fields;
            }));
            Reject("duplicate-project-id", "store-project-duplicate", source => SeedProject(source, "beta", "alpha"));
            Reject("family-mismatch", "store-identity-mismatch", source => AddDefinition(source, data => data["familyId"] = "ccdfc000-34f0-4d65-b07d-68d6b5f840ca"));
            Reject("application-mismatch", "store-identity-mismatch", source => AddDefinition(source, data => data["applicationId"] = "example.other"));
            Reject("conflicting-revision", "revision-conflict", source => Definition(source, data => data["title"] = "Changed content"));
            Reject("secret-default", "default-invalid", source => Definition(source, data => { data["fields"]![0]!["sensitivity"] = "secret"; data["fields"]![0]!["default"] = "synthetic-public-canary"; }));
            Reject("nested-secret-default", "default-invalid", source => Definition(source, data => data["fields"] = JsonNode.Parse("""
                [{"id":"credentials","type":"object","title":"Credentials","sensitivity":"public","disclosure":"essential","default":{"token":"synthetic-public-canary"},"properties":[{"id":"token","type":"string","title":"Token","sensitivity":"secret","disclosure":"essential"}]}]
                """)));
            Reject("protected-choices", "store-public-data-invalid", source => Definition(source, data => data["fields"] = JsonNode.Parse("""
                [{"id":"mode","type":"enum","title":"Mode","sensitivity":"secret","disclosure":"essential","choices":["synthetic-public-canary"]}]
                """)));
            Reject("unlisted-answer-file", "store-layout-invalid", source => File.WriteAllText(Path.Combine(source, "alpha/answers.json"), "{}"));
            Reject("revision-count-limit", "store-revision-limit", source =>
            {
                for (int revision = 3; revision <= 67; revision++) File.WriteAllText(Path.Combine(source, "alpha/releases/" + revision.ToString(CultureInfo.InvariantCulture) + ".json"), "{}");
            });
            Reject("project-count-limit", "store-project-limit", source =>
            {
                for (int number = 0; number < MaximumProjects; number++) Directory.CreateDirectory(Path.Combine(source, "project-" + number.ToString(CultureInfo.InvariantCulture)));
            });
            Reject("entry-count-limit", "store-entry-limit", source =>
            {
                for (int number = 0; number < 64; number++)
                {
                    string id = "project-" + number.ToString(CultureInfo.InvariantCulture);
                    SeedProject(source, id, id);
                    string first = Path.Combine(source, id, "releases/2.json");
                    for (int revision = 1; revision <= 64; revision++)
                    {
                        if (revision == 2) continue;
                        string path = Path.Combine(source, id, "releases/" + revision.ToString(CultureInfo.InvariantCulture) + ".json");
                        File.Copy(first, path);
                        Change(path, data => data["revision"] = revision);
                    }
                }
            });
            Reject("definition-size-limit", "document-limit", source => File.WriteAllText(Path.Combine(source, "alpha/releases/2.json"), new string(' ', MaximumDefinitionBytes + 1)));
            Reject("source-size-limit", "document-limit", source => File.WriteAllText(Path.Combine(source, "alpha/configurator.json"), new string(' ', MaximumDefinitionBytes + 1)));
            Reject("index-size-limit", "store-index-limit", source =>
            {
                for (int number = 0; number < 350; number++)
                {
                    string id = "project-" + number.ToString(CultureInfo.InvariantCulture);
                    SeedProject(source, id, id);
                    Change(Path.Combine(source, id, "configurator.json"), data => data["project"]!["description"] = new string('\u0627', 2048));
                }
            });
            Check("freshness-check-and-no-source-output", source =>
            {
                string output = source + "-output";
                string schema = source + "-schema.json";
                _ = Generate(source, output, schema, check: false);
                _ = Generate(source, output, schema, check: true);
                File.AppendAllText(Path.Combine(output, "index.json"), " ");
                try { _ = Generate(source, output, schema, check: true); }
                catch (StoreException exception) when (exception.Code == "store-output-stale")
                {
                    try { _ = Generate(source, source, schema, check: false); }
                    catch (StoreException overlap) when (overlap.Code == "store-output-overlap") { return; }
                }
                throw new InvalidOperationException("Output was not rejected.");
            });
            Check("output-symlink-no-write", source =>
            {
                string outside = source + "-outside";
                Directory.CreateDirectory(outside);
                string output = source + "-output";
                string schema = source + "-schema.json";
                Directory.CreateSymbolicLink(output, outside);
                try { _ = Generate(source, output, schema, check: false); }
                catch (StoreException exception) when (exception.Code == "store-path-invalid")
                {
                    if (!Directory.EnumerateFileSystemEntries(outside).Any() && !File.Exists(schema)) return;
                }
                throw new InvalidOperationException("Output was written.");
            });
            Check("unexpected-output-file", source =>
            {
                string output = source + "-output";
                string schema = source + "-schema.json";
                _ = Generate(source, output, schema, check: false);
                File.WriteAllText(Path.Combine(output, "unexpected.json"), "{}");
                try { _ = Generate(source, output, schema, check: true); }
                catch (StoreException exception) when (exception.Code == "store-output-stale") { return; }
                throw new InvalidOperationException("Unexpected output admitted.");
            });
            Check("empty-store", source =>
            {
                Directory.Delete(Path.Combine(source, "alpha"), recursive: true);
                using JsonDocument index = JsonDocument.Parse(Build(source)["index.json"]);
                if (index.RootElement.GetProperty("projects").GetArrayLength() != 0) throw new InvalidOperationException("Not empty.");
            });
            Check("pinned-history-cannot-be-removed", source =>
            {
                string baseline = source + "-baseline";
                foreach ((string relative, byte[] bytes) in Build(source))
                {
                    string path = Path.Combine(baseline, relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    File.WriteAllBytes(path, bytes);
                }
                Directory.Delete(Path.Combine(source, "alpha"), recursive: true);
                try { _ = Build(source, baseline); }
                catch (StoreException exception) when (exception.Code == "store-history-missing") { return; }
                throw new InvalidOperationException("Pinned history was removed.");
            });
            Check("same-source-and-store-canonical-bytes", source =>
            {
                byte[] bytes = File.ReadAllBytes(Path.Combine(source, "alpha/configurator.json"));
                SetupDefinition definition = DefinitionAuthoringCompiler.Compile(DefinitionAuthoringCodec.ParseJson(bytes));
                if (!Build(source)["definitions/alpha/2.json"].AsSpan().SequenceEqual(definition.CanonicalBytes.Span))
                    throw new InvalidOperationException("Compiler bytes differ.");
            });
            Check("history-union-preserves-raw-release", source =>
            {
                string release = Path.Combine(source, "alpha/releases/2.json");
                byte[] original = File.ReadAllBytes(release);
                Source(source, data => { data["identity"]!["revision"] = 3; data["project"]!["name"] = "Changed public title"; });
                _ = Generate(source, source + "-output", source + "-schema.json", check: false);
                if (!File.ReadAllBytes(release).AsSpan().SequenceEqual(original)
                    || !File.Exists(Path.Combine(source, "alpha/releases/3.json")))
                    throw new InvalidOperationException("Historical bytes changed.");
            });
            Check("pinned-baseline-retains-history-and-adds-current", source =>
            {
                string baseline = source + "-baseline";
                SortedDictionary<string, byte[]> prior = Build(source);
                WriteInventory(baseline, prior);
                Source(source, data => { data["identity"]!["revision"] = 3; data["project"]!["name"] = "Changed public title"; });
                SortedDictionary<string, byte[]> current = Build(source, baseline);
                if (current.Count != 3 || !current.ContainsKey("definitions/alpha/3.json")
                    || !current["definitions/alpha/2.json"].AsSpan().SequenceEqual(prior["definitions/alpha/2.json"]))
                    throw new InvalidOperationException("History union differs.");
            });
            void HistoryReject(string name, string code, Action<string, string> mutate) => Check(name, source =>
            {
                string baseline = source + "-baseline";
                WriteInventory(baseline, Build(source));
                mutate(source, baseline);
                try { _ = Build(source, baseline); }
                catch (StoreException exception) when (exception.Code == code) { return; }
                catch (SetupDefinitionException exception) when (exception.Code == code) { return; }
                throw new InvalidOperationException("Expected history rejection.");
            });
            HistoryReject("history-rewrite", "store-history-rewritten", (source, _) =>
            {
                Definition(source, data => data["title"] = "Changed history");
                Source(source, data => { data["identity"]!["revision"] = 3; data["project"]!["name"] = "Changed public title"; });
            });
            HistoryReject("history-revision-removed", "store-history-missing", (source, _) =>
            {
                File.Delete(Path.Combine(source, "alpha/releases/2.json"));
                Source(source, data => data["identity"]!["revision"] = 3);
            });
            HistoryReject("history-project-rebinding", "store-project-rebinding", (source, _) =>
            {
                string family = Guid.NewGuid().ToString("D");
                Definition(source, data => data["familyId"] = family);
                Source(source, data => data["identity"]!["familyId"] = family);
            });
            HistoryReject("baseline-path-escape", "store-baseline-invalid", (_, baseline) =>
                Change(Path.Combine(baseline, "index.json"), data => data["projects"]![0]!["revisions"]![0]!["snapshotPath"] = "../outside.json"));
            HistoryReject("baseline-digest-tamper", "store-baseline-invalid", (_, baseline) =>
                Change(Path.Combine(baseline, "index.json"), data => data["projects"]![0]!["revisions"]![0]!["digest"] = new string('0', 64)));
            HistoryReject("baseline-omitted-snapshot-inventory", "store-baseline-invalid", (_, baseline) =>
                Change(Path.Combine(baseline, "index.json"), data => data["projects"] = new JsonArray()));
            HistoryReject("baseline-symlink", "store-path-invalid", (_, baseline) =>
            {
                string path = Path.Combine(baseline, "definitions/alpha/2.json");
                string outside = baseline + "-outside.json";
                File.Move(path, outside);
                File.CreateSymbolicLink(path, outside);
            });
            Check("baseline-cannot-be-source", source =>
            {
                try { _ = Build(source, source); }
                catch (StoreException exception) when (exception.Code == "store-baseline-invalid") { return; }
                throw new InvalidOperationException("Contributor selected source baseline.");
            });
            Check("required-baseline-cannot-be-omitted", source =>
            {
                if (Run(["--source", source, "--output", source + "-output", "--require-baseline", "--write"]) != 1)
                    throw new InvalidOperationException("Required baseline omitted.");
            });
            Console.WriteLine($"Definition store self-test: {passed} passed, {failed} failed.");
            return failed == 0 ? 0 : 1;
        }
        finally { Directory.Delete(temporary, recursive: true); }
    }

    private static void Seed(string source) => SeedProject(source, "alpha", "alpha");

    private static void SeedProject(string source, string folder, string projectId)
    {
        string directory = Path.Combine(source, folder);
        Directory.CreateDirectory(Path.Combine(directory, "releases"));
        File.WriteAllText(Path.Combine(directory, "curation.json"), new JsonObject
        {
            ["schemaVersion"] = 1, ["tags"] = new JsonArray("example")
        }.ToJsonString(JsonOptions));
        var authoring = new JsonObject
        {
            ["$schema"] = DefinitionAuthoringCodec.SchemaId,
            ["identity"] = new JsonObject { ["familyId"] = Guid.NewGuid().ToString("D"), ["revision"] = 2 },
            ["project"] = new JsonObject { ["id"] = projectId, ["name"] = "Example service", ["publisher"] = "Example",
                ["description"] = "Synthetic public definition.", ["repository"] = "https://example.invalid/repository", ["definitionLicense"] = "MIT" },
            ["applicationVersions"] = new JsonArray("1.0"),
            ["fields"] = new JsonObject { ["mode"] = new JsonObject { ["type"] = "string", ["title"] = "Mode", ["sensitivity"] = "public", ["disclosure"] = "essential" } },
            ["outputs"] = new JsonObject { ["service.json"] = new JsonObject { ["id"] = "settings", ["format"] = "json" } }
        };
        byte[] bytes = Encoding.UTF8.GetBytes(authoring.ToJsonString(JsonOptions));
        File.WriteAllBytes(Path.Combine(directory, "configurator.json"), bytes);
        File.WriteAllBytes(Path.Combine(directory, "releases/2.json"), DefinitionAuthoringCompiler.Compile(DefinitionAuthoringCodec.ParseJson(bytes)).CanonicalBytes.ToArray());
    }

    private static void Metadata(string source, Action<JsonObject> change) => Change(Path.Combine(source, "alpha/curation.json"), change);
    private static void Source(string source, Action<JsonObject> change) => Change(Path.Combine(source, "alpha/configurator.json"), change);
    private static void Definition(string source, Action<JsonObject> change) => Change(Path.Combine(source, "alpha/releases/2.json"), change);
    private static void Change(string path, Action<JsonObject> change)
    {
        JsonObject value = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        change(value);
        File.WriteAllText(path, value.ToJsonString(JsonOptions));
    }

    private static void AddDefinition(string source, Action<JsonObject> change)
    {
        string first = Path.Combine(source, "alpha/releases/2.json");
        string second = Path.Combine(source, "alpha/releases/3.json");
        File.Copy(first, second);
        Change(second, data => { data["revision"] = 3; change(data); });
    }

    private static void WriteInventory(string directory, SortedDictionary<string, byte[]> files)
    {
        foreach ((string relative, byte[] bytes) in files)
        {
            string path = Path.Combine(directory, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, bytes);
        }
    }
}

internal sealed class StoreException(string code) : Exception(code)
{
    internal string Code { get; } = code;
}
