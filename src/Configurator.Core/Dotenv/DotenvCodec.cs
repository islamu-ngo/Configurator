namespace Configurator.Core.Dotenv;

using System.Text;
using Configurator.Core.Environment;

public static class DotenvCodec
{
    public const int MaximumFileUtf8Bytes = 1_048_576;
    public const int MaximumLineUtf8Bytes = 16_384;
    public const int MaximumKeyCharacters = 128;
    public const int MaximumValueUtf8Bytes = 8_192;
    public const int MaximumEntryCount = 2_048;
    public const int MaximumEditableDiagnostics = 100;

    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public static DotenvParseResult Parse(ReadOnlyMemory<byte> input) =>
        Parse(input, DotenvPurpose.TemplatePlaceholders);

    public static DotenvParseResult Parse(ReadOnlyMemory<byte> input,
        DotenvPurpose purpose, CancellationToken cancellationToken = default) =>
        ParseCore(input, purpose, null, cancellationToken);

    public static DotenvParseResult Parse(ReadOnlyMemory<byte> input,
        EnvironmentDestinationDialect dialect, DotenvPurpose purpose = DotenvPurpose.EditableValues,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(dialect)) throw new ArgumentOutOfRangeException(nameof(dialect));
        return ParseCore(input, purpose, dialect, cancellationToken);
    }

    private static DotenvParseResult ParseCore(ReadOnlyMemory<byte> input,
        DotenvPurpose purpose, EnvironmentDestinationDialect? dialect, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(purpose)) throw new ArgumentOutOfRangeException(nameof(purpose));
        cancellationToken.ThrowIfCancellationRequested();
        if (input.Length > MaximumFileUtf8Bytes)
            return FailedParse("dotenv-file-too-large", "$.document");
        if (input.Length >= 3 && input.Span[0] == 0xEF
            && input.Span[1] == 0xBB && input.Span[2] == 0xBF)
        {
            if (purpose != DotenvPurpose.EditableValues)
                return FailedParse("dotenv-bom-forbidden", "$.document");
            input = input[3..];
        }

        string text;
        try
        {
            text = StrictUtf8.GetString(input.Span);
        }
        catch (DecoderFallbackException)
        {
            return FailedParse("dotenv-utf8-invalid", "$.document");
        }

        var diagnostics = new List<EnvironmentDiagnostic>();
        var entries = new List<DotenvEntry>();
        var locations = new List<DotenvSourceLocation>();
        bool truncated = false;
        var exactKeys = new HashSet<string>(StringComparer.Ordinal);
        var foldedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int start = 0;
        int lineNumber = 1;
        while (start <= text.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int newline = text.IndexOf('\n', start);
            int end = newline < 0 ? text.Length : newline;
            ReadOnlySpan<char> line = text.AsSpan(start, end - start);
            int sourceLineBytes = StrictUtf8.GetByteCount(line);
            if (purpose == DotenvPurpose.EditableValues && newline >= 0 && line.EndsWith("\r", StringComparison.Ordinal))
                line = line[..^1];
            string path = $"$.lines[{lineNumber}]";
            if (line.IndexOf('\r') >= 0)
                Add(diagnostics, "dotenv-carriage-return-forbidden", path);
            else if (HasControl(line))
                Add(diagnostics, "dotenv-control-character", path);
            else if (sourceLineBytes > MaximumLineUtf8Bytes)
                Add(diagnostics, "dotenv-line-too-large", path);
            else if (!line.IsEmpty && line[0] != '#')
            {
                bool quoteContinues = dialect != EnvironmentDestinationDialect.DockerRun && newline >= 0
                    && line.IndexOf('=') >= 0
                    && line[(line.IndexOf('=') + 1)..].StartsWith("\"", StringComparison.Ordinal)
                    && !line[(line.IndexOf('=') + 2)..].Contains('"')
                    && text.AsSpan(newline + 1).IndexOf('"') >= 0;
                if (quoteContinues) Add(diagnostics, "dotenv-multiline-forbidden", path);
                else
                {
                    int before = entries.Count;
                    ParseLine(line, path, entries, exactKeys, foldedKeys, diagnostics, purpose, dialect);
                    if (entries.Count != before)
                        locations.Add(new(lineNumber, line.IndexOf('=') + 2));
                }
            }
            LimitDiagnostics();

            if (newline < 0) break;
            start = newline + 1;
            lineNumber++;
        }

        if (entries.Count > MaximumEntryCount)
            Add(diagnostics, "dotenv-count-exceeded", "$.document");
        LimitDiagnostics();
        return diagnostics.Count == 0
            ? new DotenvParseResult(new DotenvDocument(entries), [], locations)
            : new DotenvParseResult(null, Ordered(diagnostics), diagnosticsTruncated: truncated);

        void LimitDiagnostics()
        {
            if (purpose != DotenvPurpose.EditableValues || diagnostics.Count <= MaximumEditableDiagnostics) return;
            diagnostics.RemoveRange(MaximumEditableDiagnostics, diagnostics.Count - MaximumEditableDiagnostics);
            truncated = true;
        }
    }

    public static DotenvRenderResult Render(DotenvDocument document, bool finalNewline) =>
        Render(document, finalNewline, DotenvPurpose.TemplatePlaceholders);

    public static DotenvRenderResult Render(DotenvDocument document, bool finalNewline, DotenvPurpose purpose) =>
        RenderCore(document, finalNewline, purpose, null);

    public static DotenvRenderResult Render(DotenvDocument document, bool finalNewline,
        EnvironmentDestinationDialect dialect, DotenvPurpose purpose = DotenvPurpose.EditableValues)
    {
        if (!Enum.IsDefined(dialect)) throw new ArgumentOutOfRangeException(nameof(dialect));
        return RenderCore(document, finalNewline, purpose, dialect);
    }

    private static DotenvRenderResult RenderCore(DotenvDocument document, bool finalNewline,
        DotenvPurpose purpose, EnvironmentDestinationDialect? dialect)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (!Enum.IsDefined(purpose)) throw new ArgumentOutOfRangeException(nameof(purpose));
        DotenvEntry?[] entries = document.Entries.Cast<DotenvEntry?>().ToArray();
        var diagnostics = new List<EnvironmentDiagnostic>();
        var exactKeys = new HashSet<string>(StringComparer.Ordinal);
        var foldedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index < entries.Length; index++)
            ValidateEntry(entries[index], $"$.entries[{index}]", exactKeys, foldedKeys, diagnostics, purpose, dialect);
        if (entries.Length > MaximumEntryCount)
            Add(diagnostics, "dotenv-count-exceeded", "$.document");
        if (diagnostics.Count != 0)
            return new DotenvRenderResult([], Ordered(diagnostics));

        var builder = new StringBuilder();
        int totalBytes = 0;
        DotenvEntry[] ordered = entries.OfType<DotenvEntry>()
            .OrderBy(item => item.Key, StringComparer.Ordinal).ToArray();
        for (int index = 0; index < ordered.Length; index++)
        {
            DotenvEntry entry = ordered[index];
            string? renderedValue = dialect switch
            {
                EnvironmentDestinationDialect.DockerRun => entry.Value,
                EnvironmentDestinationDialect.Compose => RenderComposeValue(entry.Value),
                _ => RenderValue(entry.Value),
            };
            int lineBytes = StrictUtf8.GetByteCount(entry.Key) + 1
                + (renderedValue is null ? 0 : StrictUtf8.GetByteCount(renderedValue));
            if (lineBytes > MaximumLineUtf8Bytes)
                return new DotenvRenderResult([], [Diagnostic("dotenv-line-too-large", "$.document")]);
            totalBytes += lineBytes + (index < ordered.Length - 1 || finalNewline ? 1 : 0);
            if (totalBytes > MaximumFileUtf8Bytes)
                return new DotenvRenderResult([], [Diagnostic("dotenv-file-too-large", "$.document")]);
            builder.Append(entry.Key).Append('=').Append(renderedValue);
            if (index < ordered.Length - 1 || finalNewline) builder.Append('\n');
        }
        try
        {
            byte[] bytes = StrictUtf8.GetBytes(builder.ToString());
            if (bytes.Length > MaximumFileUtf8Bytes)
                return new DotenvRenderResult([], [Diagnostic("dotenv-file-too-large", "$.document")]);
            return new DotenvRenderResult(bytes, []);
        }
        catch (EncoderFallbackException)
        {
            return new DotenvRenderResult([], [Diagnostic("dotenv-utf16-invalid", "$.document")]);
        }
    }

    private static void ParseLine(
        ReadOnlySpan<char> line,
        string path,
        List<DotenvEntry> entries,
        HashSet<string> exactKeys,
        HashSet<string> foldedKeys,
        List<EnvironmentDiagnostic> diagnostics,
        DotenvPurpose purpose,
        EnvironmentDestinationDialect? dialect)
    {
        if (line.StartsWith("export ", StringComparison.Ordinal))
        {
            Add(diagnostics, "dotenv-export-forbidden", path);
            return;
        }
        if (char.IsWhiteSpace(line[0]))
        {
            Add(diagnostics, "dotenv-whitespace-forbidden", path);
            return;
        }
        int equals = line.IndexOf('=');
        if (equals < 0)
        {
            Add(diagnostics, "dotenv-equals-missing", path);
            return;
        }
        ReadOnlySpan<char> keySpan = line[..equals];
        string key = keySpan.ToString();
        if (keySpan.Length > 0 && char.IsWhiteSpace(keySpan[^1]))
            Add(diagnostics, "dotenv-whitespace-forbidden", path, key);
        bool validKey = dialect.HasValue ? IsEnvironmentKey(key) : IsKey(key);
        if (!validKey) Add(diagnostics, "dotenv-key-invalid", path);
        if (!exactKeys.Add(key)) Add(diagnostics, "dotenv-duplicate-key", path, key);
        else if (!foldedKeys.Add(key)) Add(diagnostics, "dotenv-key-case-collision", path);

        ReadOnlySpan<char> source = line[(equals + 1)..];
        string value;
        if (dialect == EnvironmentDestinationDialect.DockerRun) value = source.ToString();
        else if (dialect == EnvironmentDestinationDialect.Compose)
        {
            if (!TryParseComposeValue(source, out value))
            {
                Add(diagnostics, "dotenv-literal-syntax", path);
                return;
            }
        }
        else if (!TryParseValue(source, path, diagnostics, out value)) return;
        if (StrictUtf8.GetByteCount(value) > MaximumValueUtf8Bytes)
        {
            Add(diagnostics, "dotenv-value-too-large", path, key);
            return;
        }
        if (validKey && exactKeys.Contains(key))
            entries.Add(new DotenvEntry(key, value.Length == 0 && purpose == DotenvPurpose.TemplatePlaceholders ? null : value,
                value.Length == 0 && purpose == DotenvPurpose.TemplatePlaceholders
                    ? DotenvEntryKind.EmptyPlaceholder : DotenvEntryKind.LocalHumanValue,
                false, DotenvProvenance.UserInput));
    }

    private static bool TryParseValue(
        ReadOnlySpan<char> source,
        string path,
        List<EnvironmentDiagnostic> diagnostics,
        out string value)
    {
        value = string.Empty;
        if (source.IsEmpty) return true;
        if (ContainsForbiddenShell(source))
        {
            Add(diagnostics, source.IndexOfAny('$', '`') >= 0
                ? "dotenv-expansion-forbidden" : "dotenv-trailing-syntax", path);
            return false;
        }
        if (source[0] == '\'')
        {
            Add(diagnostics, "dotenv-quote-invalid", path);
            return false;
        }
        if (source[0] != '"')
        {
            if (source.IndexOfAny('"', '#') >= 0 || !source.All(IsSafeUnquoted))
            {
                Add(diagnostics, source.IndexOf('"') >= 0 ? "dotenv-quote-invalid" : "dotenv-trailing-syntax", path);
                return false;
            }
            value = source.ToString();
            return true;
        }

        var builder = new StringBuilder(source.Length);
        bool escaped = false;
        for (int index = 1; index < source.Length; index++)
        {
            char character = source[index];
            if (escaped)
            {
                if (character is not '\\' and not '"')
                {
                    Add(diagnostics, "dotenv-escape-invalid", path);
                    return false;
                }
                builder.Append(character);
                escaped = false;
            }
            else if (character == '\\') escaped = true;
            else if (character == '"')
            {
                if (index != source.Length - 1)
                {
                    Add(diagnostics, "dotenv-trailing-syntax", path);
                    return false;
                }
                value = builder.ToString();
                return true;
            }
            else builder.Append(character);
        }
        Add(diagnostics, escaped ? "dotenv-escape-invalid" : "dotenv-quote-invalid", path);
        return false;
    }

    private static void ValidateEntry(
        DotenvEntry? entry,
        string path,
        HashSet<string> exactKeys,
        HashSet<string> foldedKeys,
        List<EnvironmentDiagnostic> diagnostics,
        DotenvPurpose purpose,
        EnvironmentDestinationDialect? dialect)
    {
        if (entry is null)
        {
            Add(diagnostics, "dotenv-entry-null", path);
            return;
        }
        if (!(dialect.HasValue ? IsEnvironmentKey(entry.Key) : IsKey(entry.Key)))
            Add(diagnostics, "dotenv-key-invalid", path);
        if (!exactKeys.Add(entry.Key)) Add(diagnostics, "dotenv-duplicate-key", path, entry.Key);
        else if (!foldedKeys.Add(entry.Key)) Add(diagnostics, "dotenv-key-case-collision", path);
        if (!Enum.IsDefined(entry.Kind)) Add(diagnostics, "dotenv-entry-kind-invalid", path, entry.Key);
        if (!Enum.IsDefined(entry.Provenance)) Add(diagnostics, "dotenv-provenance-invalid", path, entry.Key);
        if (entry.Kind == DotenvEntryKind.EmptyPlaceholder && entry.Value is not null
            || entry.Kind != DotenvEntryKind.EmptyPlaceholder && (entry.Value is null ||
                entry.Value.Length == 0 && purpose == DotenvPurpose.TemplatePlaceholders)
            || purpose == DotenvPurpose.EditableValues && entry.Kind == DotenvEntryKind.EmptyPlaceholder)
            Add(diagnostics, "dotenv-entry-shape-invalid", path, entry.Key);
        if (entry.Value is { } value)
        {
            try
            {
                if (StrictUtf8.GetByteCount(value) > MaximumValueUtf8Bytes)
                    Add(diagnostics, "dotenv-value-too-large", path, entry.Key);
            }
            catch (EncoderFallbackException)
            {
                Add(diagnostics, "dotenv-utf16-invalid", path, entry.Key);
                return;
            }
            if (HasControl(value.AsSpan()) || value.Contains('\r', StringComparison.Ordinal) || value.Contains('\n', StringComparison.Ordinal)
                || !dialect.HasValue && ContainsForbiddenShell(value))
                Add(diagnostics, "dotenv-value-forbidden", path, entry.Key);
        }
    }

    private static string RenderValue(string? value)
    {
        if (value is null) return string.Empty;
        if (value.All(IsSafeUnquoted)) return value;
        return "\"" + value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
    }

    private static string RenderComposeValue(string? value) => value is null ? string.Empty
        : "\"" + value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("$", "$$", StringComparison.Ordinal) + "\"";

    private static bool TryParseComposeValue(ReadOnlySpan<char> source, out string value)
    {
        value = string.Empty;
        source = source.Trim();
        if (source.IsEmpty) return true;
        bool quoted = source[0] is '\'' or '"';
        char quote = quoted ? source[0] : '\0';
        var builder = new StringBuilder(source.Length);
        for (int index = quoted ? 1 : 0; index < source.Length; index++)
        {
            char character = source[index];
            if (quoted && character == quote)
            {
                ReadOnlySpan<char> remainder = source[(index + 1)..].TrimStart();
                if (!remainder.IsEmpty && remainder[0] != '#') return false;
                value = builder.ToString();
                return true;
            }
            if (!quoted && character == '#' && index > 0 && char.IsWhiteSpace(source[index - 1]))
            {
                value = builder.ToString().TrimEnd();
                return true;
            }
            if (character == '\\' && index + 1 < source.Length)
            {
                char next = source[index + 1];
                if (quote == '\'' && next == '\'') { builder.Append(next); index++; continue; }
                if (quote == '"')
                {
                    if (next is not '\\' and not '"') return false;
                    builder.Append(next); index++; continue;
                }
            }
            if (quote != '\'' && character == '$')
            {
                if (index + 1 >= source.Length || source[index + 1] != '$') return false;
                builder.Append('$'); index++; continue;
            }
            if (!quoted && character is '\'' or '"') return false;
            builder.Append(character);
        }
        if (quoted) return false;
        value = builder.ToString();
        return true;
    }

    internal static bool IsEnvironmentKey(string key) => key.Length is > 0 and <= MaximumKeyCharacters
        && (char.IsAsciiLetter(key[0]) || key[0] == '_')
        && key.All(character => char.IsAsciiLetterOrDigit(character) || character == '_');

    internal static bool IsKey(string key) => key.Length is > 0 and <= MaximumKeyCharacters
        && key[0] is >= 'A' and <= 'Z'
        && key.All(character => character is >= 'A' and <= 'Z' or >= '0' and <= '9' or '_');

    private static bool IsSafeUnquoted(char character) => character is >= 'A' and <= 'Z'
        or >= 'a' and <= 'z' or >= '0' and <= '9' or '_' or '-' or '.' or '/' or ':' or ',' or '@' or '+';

    private static bool ContainsForbiddenShell(ReadOnlySpan<char> value) =>
        value.IndexOfAny("$`;|&<>".AsSpan()) >= 0;

    private static bool HasControl(ReadOnlySpan<char> value)
    {
        foreach (char character in value)
            if (char.IsControl(character)) return true;
        return false;
    }

    private static DotenvParseResult FailedParse(string code, string path) =>
        new(null, [Diagnostic(code, path)]);

    private static void Add(List<EnvironmentDiagnostic> values, string code, string path, string? key = null) =>
        values.Add(Diagnostic(code, path, key));

    private static EnvironmentDiagnostic Diagnostic(string code, string path, string? key = null) =>
        new(code, path, key is not null && IsKey(key) ? key : null, "dotenv");

    private static IEnumerable<EnvironmentDiagnostic> Ordered(IEnumerable<EnvironmentDiagnostic> values) =>
        values.OrderBy(item => item.Path, StringComparer.Ordinal).ThenBy(item => item.Code, StringComparer.Ordinal);
}
