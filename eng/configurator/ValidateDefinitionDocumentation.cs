#:property RestorePackagesWithLockFile=false

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

if (args is ["--self-test"])
    return DocumentationCheck.SelfTest();
bool linksOnly = args.Length > 0 && args[0] == "--links-only";
if (linksOnly) args = args[1..];
if (args.Length < 2 || args.Any(argument => argument.StartsWith("--", StringComparison.Ordinal)))
{
    Console.Error.WriteLine("Usage: dotnet run --file eng/configurator/ValidateDefinitionDocumentation.cs -- [--links-only] ROOT FILE... | --self-test");
    return 64;
}

return DocumentationCheck.Run(args[0], args[1..], Console.Out, Console.Error, checkMetadata: !linksOnly);

/// <summary>
/// Checks explicit Markdown operands, not a recursive documentation crawl or a full Markdown renderer.
/// Links use inline Markdown syntax; anchors use ATX headings outside fenced and inline code.
/// </summary>
internal sealed class DocumentationCheck(string root, TextWriter errors)
{
    private readonly Dictionary<string, Document> documents = new(StringComparer.Ordinal);
    private int errorCount;
    private int linkCount;

    internal static int Run(string rootOperand, string[] operands, TextWriter output, TextWriter errors, bool checkMetadata = true)
    {
        try
        {
            string rootPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootOperand));
            if (!Directory.Exists(rootPath))
                throw new InvalidDataException("ROOT_MISSING");
            var check = new DocumentationCheck(rootPath, errors);
            foreach (string operand in operands)
            {
                try
                {
                    string path = check.LocalPath(operand, rootPath);
                    if (!IsMarkdown(path))
                        throw new InvalidDataException("INPUT_NOT_MARKDOWN");
                    Document document = check.Load(path);
                    if (checkMetadata) check.Metadata(path, document);
                    foreach ((int line, string target) in document.Links)
                        check.Link(path, line, target);
                }
                catch (Exception exception) when (IsInputFailure(exception))
                {
                    check.Error(operand, 0, FailureCode(exception));
                }
            }
            output.WriteLine($"DOC_CHECK files={operands.Length} local_links={check.linkCount} errors={check.errorCount}");
            return check.errorCount == 0 ? 0 : 1;
        }
        catch (Exception exception) when (IsInputFailure(exception))
        {
            errors.WriteLine(FailureCode(exception));
            return 1;
        }
    }

    private static bool IsInputFailure(Exception exception) =>
        exception is InvalidDataException or IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException;

    private static string FailureCode(Exception exception) =>
        exception is InvalidDataException ? exception.Message : "INPUT_UNAVAILABLE";

    private void Error(string path, int line, string code)
    {
        string displayPath = Path.IsPathFullyQualified(path) ? Path.GetRelativePath(root, path) : path;
        errors.WriteLine($"{code} {displayPath}:{line}");
        errorCount++;
    }

    private static bool IsMarkdown(string path) =>
        Path.GetExtension(path).Equals(".md", StringComparison.OrdinalIgnoreCase);

    private string LocalPath(string operand, string relativeTo)
    {
        string path = Path.GetFullPath(operand, relativeTo);
        string relative = Path.GetRelativePath(root, path);
        if (relative == ".." || relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            || Path.IsPathRooted(relative))
            throw new InvalidDataException("PATH_OUTSIDE_ROOT");
        // Do not let a local link traverse a symlink into an unrequested tree.
        string current = root;
        foreach (string segment in relative.Split(Path.DirectorySeparatorChar))
        {
            current = Path.Combine(current, segment);
            if (new FileInfo(current).LinkTarget is not null)
                throw new InvalidDataException("SYMLINK_UNSUPPORTED");
        }
        return path;
    }

    private Document Load(string path)
    {
        if (documents.TryGetValue(path, out Document? cached))
            return cached;
        string[] lines = File.ReadAllLines(path, Encoding.UTF8);
        var anchors = new HashSet<string>(StringComparer.Ordinal);
        var links = new List<(int Line, string Target)>();
        char fence = '\0';
        int fenceLength = 0;
        int fenceLine = 0;
        int start = 0;
        if (lines.Length > 0 && lines[0] == "---")
        {
            start = Array.IndexOf(lines, "---", 1);
            if (start < 0)
                throw new InvalidDataException("FRONTMATTER_UNCLOSED");
            start++;
        }
        for (int index = start; index < lines.Length; index++)
        {
            string line = lines[index];
            Match marker = Regex.Match(line, @"^ {0,3}(`{3,}|~{3,})(.*)$");
            if (marker.Success)
            {
                string run = marker.Groups[1].Value;
                string tail = marker.Groups[2].Value;
                if (fence == '\0')
                {
                    if (run[0] == '`' && tail.Contains('`'))
                        Error(path, index + 1, "FENCE_INFO_INVALID");
                    fence = run[0];
                    fenceLength = run.Length;
                    fenceLine = index + 1;
                }
                else if (run[0] == fence && run.Length >= fenceLength && string.IsNullOrWhiteSpace(tail))
                {
                    fence = '\0';
                }
                continue;
            }
            if (fence != '\0')
                continue;
            Match heading = Regex.Match(line, @"^ {0,3}#{1,6}[ \t]+(.+?)\s*#*\s*$");
            if (heading.Success)
            {
                string text = Regex.Replace(heading.Groups[1].Value, @"<[^>]*>", "");
                text = Regex.Replace(text, @"!?\[([^\]]*)\]\([^)]*\)", "$1");
                string slug = Regex.Replace(text.ToLowerInvariant(), @"[^\p{L}\p{M}\p{N}_ -]", "").Replace(' ', '-');
                string anchor = slug;
                for (int suffix = 1; !anchors.Add(anchor); suffix++)
                    anchor = $"{slug}-{suffix}";
            }
            // Backtick spans may contain fake links. A closing run must match the opening width.
            line = Regex.Replace(line, @"(?<!`)(`+)(?!`)(.*?)\1(?!`)", "", RegexOptions.None);
            foreach (Match link in Regex.Matches(line, @"(?<!!)(?<!\\)!?\[[^\]\n]*\]\("))
            {
                int cursor = link.Index + link.Length;
                int depth = 0;
                bool angle = cursor < line.Length && line[cursor] == '<';
                if (angle) cursor++;
                int targetStart = cursor;
                while (cursor < line.Length)
                {
                    char character = line[cursor];
                    if (character == '\\' && cursor + 1 < line.Length) { cursor += 2; continue; }
                    if (angle ? character == '>' : character == ')' && depth == 0 || char.IsWhiteSpace(character))
                        break;
                    if (!angle && character == '(') depth++;
                    if (!angle && character == ')') depth--;
                    cursor++;
                }
                string target = line[targetStart..cursor];
                bool angleClosed = !angle || cursor < line.Length && line[cursor] == '>';
                if (angle && angleClosed) cursor++;
                string remainder = line[cursor..];
                if (!angleClosed || !Regex.IsMatch(remainder, "^\\s*(?:\"[^\"]*\"|'[^']*')?\\s*\\)"))
                    Error(path, index + 1, "LINK_SYNTAX_UNSUPPORTED");
                else
                    links.Add((index + 1, Regex.Replace(target, @"\\([\\() <>])", "$1")));
            }
        }
        if (fence != '\0')
            Error(path, fenceLine, "FENCE_UNCLOSED");
        var document = new Document(lines, anchors, links);
        documents.Add(path, document);
        return document;
    }

    private void Link(string source, int line, string target)
    {
        if (target.StartsWith("//", StringComparison.Ordinal)
            || Regex.IsMatch(target, @"^[A-Za-z][A-Za-z0-9+.-]*:"))
            return; // External URIs are deliberately not requested or validated.
        linkCount++;
        try
        {
            int hash = target.IndexOf('#');
            string pathPart = hash < 0 ? target : target[..hash];
            int query = pathPart.IndexOf('?');
            if (query >= 0) pathPart = pathPart[..query];
            string fragment = hash < 0 ? "" : Uri.UnescapeDataString(target[(hash + 1)..]);
            pathPart = Uri.UnescapeDataString(pathPart);
            string destination = pathPart.Length == 0 ? source
                : pathPart.StartsWith('/') ? LocalPath(pathPart.TrimStart('/'), root)
                : LocalPath(pathPart, Path.GetDirectoryName(source)!);
            if (!File.Exists(destination) && !Directory.Exists(destination))
                throw new InvalidDataException("LINK_MISSING");
            if (fragment.Length != 0)
            {
                if (!IsMarkdown(destination) || !File.Exists(destination))
                    throw new InvalidDataException("ANCHOR_TARGET_UNSUPPORTED");
                if (!Load(destination).Anchors.Contains(fragment))
                    throw new InvalidDataException("ANCHOR_MISSING");
            }
        }
        catch (Exception exception) when (IsInputFailure(exception))
        {
            Error(source, line, FailureCode(exception));
        }
    }

    private void Metadata(string path, Document document)
    {
        string[] lines = document.Lines;
        if (lines.Any(line => line.Contains("<!-- ABOUT" + "ME:", StringComparison.Ordinal)))
            Error(path, 0, "SYNTHETIC_METADATA");
        if (lines.Length > 0 && lines[0] == "---")
        {
            int end = Array.IndexOf(lines, "---", 1);
            var fields = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string line in lines[1..end])
            {
                Match field = Regex.Match(line, @"^([A-Za-z][A-Za-z0-9_-]*):[ \t]*(.+)$");
                if (!field.Success || !fields.TryAdd(field.Groups[1].Value, field.Groups[2].Value.Trim()))
                    Error(path, 0, "FRONTMATTER_INVALID");
            }
            if (!fields.TryGetValue("description", out string? description)
                || description.Trim('\'', '"', ' ').Length == 0)
                Error(path, 0, "DESCRIPTION_MISSING");
        }
        string relative = Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/');
        bool internalDocument = relative.StartsWith("docs/internal/", StringComparison.Ordinal)
            && !relative.StartsWith("docs/internal/adr/", StringComparison.Ordinal);
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal);
        // Natural metadata belongs below the title and before the first prose paragraph.
        int title = Array.FindIndex(lines, line => line.StartsWith("# ", StringComparison.Ordinal));
        if (title < 0)
        {
            Error(path, 0, "TITLE_MISSING");
            return;
        }
        for (int index = title + 1; index < lines.Length; index++)
        {
            if (string.IsNullOrWhiteSpace(lines[index])) continue;
            Match field = Regex.Match(lines[index], @"^> \*\*([^*]+):\*\*[ \t]*(.*)$");
            if (!field.Success) break;
            if (!metadata.TryAdd(field.Groups[1].Value, field.Groups[2].Value.Trim())
                || field.Groups[2].Value.Trim().Length == 0)
                Error(path, index + 1, "METADATA_INVALID");
        }
        if (internalDocument)
        {
            foreach (string name in new[] { "Audience", "Status", "Owner", "Last Verified", "Source Anchors" })
                if (!metadata.ContainsKey(name))
                    Error(path, 0, "METADATA_MISSING");
        }
        if (metadata.TryGetValue("Last Verified", out string? date)
            && !DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            Error(path, 0, "METADATA_DATE_INVALID");
    }

    internal static int SelfTest()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"configurator-doc-check-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            Directory.CreateDirectory(Path.Combine(directory, "docs/internal"));
            const string valid = """
                ---
                description: "Synthetic documentation fixture."
                ---
                # Fixture
                ## Repeat
                ## Repeat
                ## Repeat-1
                [one](#repeat) [two](#repeat-1) [collision](#repeat-1-1)
                [encoded](other%20page.md#caf%C3%A9) [root](/other%20page.md#caf%C3%A9)
                [parentheses](file(1).txt "title") [directory](docs/)
                [external](https://invalid.example/not-requested)
                `[ignored](missing.md)` ``[ignored](missing.md)` ``
                ````markdown
                ```
                [ignored](missing.md)
                ````
                ~~~text
                [ignored](missing.md)
                ~~~
                """;
            File.WriteAllText(Path.Combine(directory, "page.md"), valid);
            File.WriteAllText(Path.Combine(directory, "other page.md"), "# Other\n## Caf\u00e9\n");
            File.WriteAllText(Path.Combine(directory, "file(1).txt"), "fixture");
            const string internalText = """
                # Internal

                > **Audience:** Contributors
                > **Status:** Draft
                > **Owner:** Engineering
                > **Last Verified:** 2026-10-28
                > **Source Anchors:** `page.md`
                """;
            File.WriteAllText(Path.Combine(directory, "docs/internal/page.md"), internalText);
            int cases = 0;
            Expect(["page.md", "docs/internal/page.md"], 0, "errors=0");
            Reject(valid + "\n[broken](#absent)", "ANCHOR_MISSING");
            Reject(valid + "\n[broken](missing.md)", "LINK_MISSING");
            Reject(valid + "\n[escape](../outside.md)", "PATH_OUTSIDE_ROOT");
            Reject(valid + "\n[asset](file(1).txt#anchor)", "ANCHOR_TARGET_UNSUPPORTED");
            Reject(valid + "\n~~~\n```", "FENCE_UNCLOSED");
            Reject("---\ndescription: text\n# Unclosed\n", "FRONTMATTER_UNCLOSED");
            Reject("---\ndescription: ''\n---\n# Empty\n", "DESCRIPTION_MISSING");
            Reject("---\ndescription: first\ndescription: second\n---\n# Duplicate\n", "FRONTMATTER_INVALID");
            Reject("# Fixture\n[broken](<missing file.md)\n", "LINK_SYNTAX_UNSUPPORTED");
            Reject("# Fixture\n<!-- ABOUT" + "ME: synthetic -->\n", "SYNTHETIC_METADATA");
            File.WriteAllText(Path.Combine(directory, "docs/internal/page.md"), "# Internal\n");
            Expect(["docs/internal/page.md"], 1, "METADATA_MISSING");
            File.WriteAllText(Path.Combine(directory, "docs/internal/page.md"),
                internalText.Replace("2026-10-28", "2026-02-30", StringComparison.Ordinal));
            Expect(["docs/internal/page.md"], 1, "METADATA_DATE_INVALID");
            Expect(["missing.md"], 1, "INPUT_UNAVAILABLE");
            Expect(["file(1).txt"], 1, "INPUT_NOT_MARKDOWN");
            Expect(["../outside.md"], 1, "PATH_OUTSIDE_ROOT");
            File.WriteAllText(Path.Combine(directory, "docs/internal/page.md"), "# Different metadata contract\n[valid](../../page.md#repeat)\n");
            Expect(["docs/internal/page.md"], 0, "errors=0", checkMetadata: false);
            File.WriteAllText(Path.Combine(directory, "docs/internal/page.md"), "# Different metadata contract\n[broken](#absent)\n");
            Expect(["docs/internal/page.md"], 1, "ANCHOR_MISSING", checkMetadata: false);
            File.WriteAllText(Path.Combine(directory, "docs/internal/page.md"), "# Different metadata contract\n```\n");
            Expect(["docs/internal/page.md"], 1, "FENCE_UNCLOSED", checkMetadata: false);
            Console.WriteLine($"configurator-definition-documentation-self-test-valid: {cases} cases");
            return 0;

            void Reject(string text, string code)
            {
                File.WriteAllText(Path.Combine(directory, "invalid.md"), text);
                Expect(["invalid.md"], 1, code);
            }

            void Expect(string[] operands, int expectedExit, string expectedCode, bool checkMetadata = true)
            {
                using var output = new StringWriter(CultureInfo.InvariantCulture);
                using var errors = new StringWriter(CultureInfo.InvariantCulture);
                int exit = Run(directory, operands, output, errors, checkMetadata);
                string result = output.ToString() + errors;
                if (exit != expectedExit || !result.Contains(expectedCode, StringComparison.Ordinal))
                    throw new InvalidOperationException($"documentation-self-test-failed: case {cases + 1}\n{result}");
                cases++;
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private sealed record Document(string[] Lines, HashSet<string> Anchors, List<(int Line, string Target)> Links);
}
