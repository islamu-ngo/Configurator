#:property RestorePackagesWithLockFile=false

using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

if (args is not (["--write"] or ["--check"] or ["--self-test"]))
{
    Console.Error.WriteLine("Usage: dotnet run --file eng/knowledge/RenderCommandReference.cs -- --write | --check | --self-test");
    return 64;
}

try
{
    if (args is ["--self-test"]) return CommandReference.SelfTest();
    DirectoryInfo? root = new(Environment.CurrentDirectory);
    while (root is not null && !File.Exists(Path.Combine(root.FullName, "Configurator.slnx")))
        root = root.Parent;
    if (root is null) throw new InvalidDataException("ROOT_UNAVAILABLE");
    string input = Path.Combine(root.FullName, CommandReference.InputPath);
    string output = Path.Combine(root.FullName, CommandReference.OutputPath);
    using var stream = File.OpenRead(input);
    if (stream.Length > CommandReference.MaximumInputBytes)
        throw new InvalidDataException("INPUT_TOO_LARGE");
    byte[] source = new byte[checked((int)stream.Length)];
    stream.ReadExactly(source);
    if (stream.ReadByte() != -1) throw new InvalidDataException("INPUT_TOO_LARGE");
    string result = CommandReference.Process(source, args[0],
        () => File.Exists(output) ? File.ReadAllBytes(output) : null,
        bytes => File.WriteAllBytes(output, bytes));
    if (result == "STALE")
    {
        Console.Error.WriteLine("COMMAND_REFERENCE_FAILED: STALE");
        return 1;
    }
    Console.WriteLine("COMMAND_REFERENCE_" + result);
    return 0;
}
catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
    or InvalidDataException or JsonException or ArgumentException or InvalidOperationException or OverflowException)
{
    // Only locally authored codes may reach diagnostics, never input or exception text.
    string code = exception is InvalidDataException ? exception.Message
        : exception is JsonException ? "INVALID_JSON" : "INPUT_OR_OUTPUT_UNAVAILABLE";
    Console.Error.WriteLine("COMMAND_REFERENCE_FAILED: " + code);
    return 1;
}

/// <summary>
/// Projects the public metadata slice, not JSON Schema semantics or CLI behavior.
/// Validation and rendering finish before either the output reader or writer runs.
/// </summary>
internal static class CommandReference
{
    internal const string InputPath = "schemas/islamu-configurator-command-v4.schema.json";
    internal const string OutputPath = "docs/public/cli-command-reference.md";
    internal const int MaximumInputBytes = 2 * 1024 * 1024;
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private static readonly string[] MetadataFields =
    [
        "about", "generatedBy", "authoringContractVersion", "authoringDiscriminator",
        "runtimeSchemaVersion", "commandOptions", "repeatableOptions",
        "maximumArtifactSelections", "maximumOutputBytes", "maximumDetailBytes", "commands"
    ];

    internal static string Process(byte[] source, string mode, Func<byte[]?> read, Action<byte[]> write)
    {
        byte[] expected = Render(source);
        if (mode == "--check")
        {
            byte[]? actual = read();
            return actual is not null && actual.AsSpan().SequenceEqual(expected) ? "CURRENT" : "STALE";
        }
        if (mode != "--write") throw new InvalidDataException("MODE_UNSUPPORTED");
        write(expected);
        return "WRITTEN";
    }

    private static byte[] Render(byte[] source)
    {
        if (source.Length > MaximumInputBytes) throw new InvalidDataException("INPUT_TOO_LARGE");
        using JsonDocument document = JsonDocument.Parse(source, new JsonDocumentOptions { MaxDepth = 64 });
        JsonElement root = document.RootElement;
        RejectDuplicates(root);
        RequireKind(root, JsonValueKind.Object);
        Equal(Text(root, "$schema"), "https://json-schema.org/draft/2020-12/schema");
        Equal(Text(root, "$id"), "https://schemas.islamu.org/configurator/command/v4/schema.json");
        Equal(Text(Field(Field(root, "properties"), "schemaVersion"), "const"), "islamu-configurator-command/v4");
        Equal(Text(root, "type"), "object");
        RequireKind(Field(root, "additionalProperties"), JsonValueKind.False);
        if (!Strings(Field(root, "required")).Contains("schemaVersion", StringComparer.Ordinal))
            throw new InvalidDataException("SHAPE_INVALID");
        JsonElement metadata = Field(root, "_metadata");
        Shape(metadata, MetadataFields);
        Equal(Text(metadata, "generatedBy"), "Configurator.CommandSchemaGenerator");
        Equal(Text(metadata, "authoringDiscriminator"), "urn:islamu:configurator:authoring:v2");
        if (Positive(metadata, "authoringContractVersion") != 2 || Positive(metadata, "runtimeSchemaVersion") != 2)
            throw new InvalidDataException("VERSION_UNSUPPORTED");
        string[] about = Strings(Field(metadata, "about"));
        string[] options = Strings(Field(metadata, "commandOptions"));
        string[] repeatable = Strings(Field(metadata, "repeatableOptions"), allowEmpty: true);
        if (options.Any(option => !option.StartsWith("--", StringComparison.Ordinal) || option.Length == 2)
            || repeatable.Except(options, StringComparer.Ordinal).Any())
            throw new InvalidDataException("OPTIONS_INVALID");
        long artifacts = Positive(metadata, "maximumArtifactSelections");
        long output = Positive(metadata, "maximumOutputBytes");
        long detail = Positive(metadata, "maximumDetailBytes");
        if (detail > output) throw new InvalidDataException("BOUNDS_INVALID");
        JsonElement commands = Field(metadata, "commands");
        RequireArray(commands);
        var commandNames = new HashSet<string>(StringComparer.Ordinal);
        var allOptions = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonElement command in commands.EnumerateArray())
        {
            Shape(command, ["name", "operations", "options"]);
            if (!commandNames.Add(Text(command, "name"))) throw new InvalidDataException("DUPLICATE_NAME");
            string[] commandOptions = Strings(Field(command, "options"));
            if (commandOptions.Except(options, StringComparer.Ordinal).Any())
                throw new InvalidDataException("OPTIONS_INVALID");
            allOptions.UnionWith(commandOptions);
            JsonElement operations = Field(command, "operations");
            RequireArray(operations);
            var operationNames = new HashSet<string>(StringComparer.Ordinal);
            var operationOptions = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonElement operation in operations.EnumerateArray())
            {
                Shape(operation, ["name", "options"]);
                if (!operationNames.Add(Text(operation, "name"))) throw new InvalidDataException("DUPLICATE_NAME");
                operationOptions.UnionWith(Strings(Field(operation, "options")));
            }
            if (!operationOptions.SetEquals(commandOptions)) throw new InvalidDataException("OPTIONS_INVALID");
        }
        if (!allOptions.SetEquals(options)) throw new InvalidDataException("OPTIONS_INVALID");

        var text = new StringBuilder("""
            # CLI command metadata reference

            > **Audience:** Adopters | Contributors | Integrators | AI agents
            > **Kind:** Generated reference
            > **Status:** Generated vocabulary; not a qualification or execution receipt
            > **Owner:** CLI
            > **Source Anchors:** `schemas/islamu-configurator-command-v4.schema.json`, `eng/knowledge/RenderCommandReference.cs`

            Generated from the [command schema](../../schemas/islamu-configurator-command-v4.schema.json).
            Do not edit this projection. Run the owning command-schema freshness check first,
            then `dotnet run --file eng/knowledge/RenderCommandReference.cs -- --write`.
            Output is UTF-8 without BOM, with LF newlines and ordinal ordering.

            Use the [maintained CLI guide](cli.md) for option values, requirements, examples,
            confidentiality and exits, and the [internal CLI contract](../internal/CLI_CONTRACT.md)
            for binding, execution, custody and publication mechanisms.
            Option lists below report membership only: command options are the union across
            operations, not permission to use every option on every operation. Metadata does
            not describe required operands, value types, combinations or complete invocation
            grammar. Repeatability does not remove selection bounds. These tables project
            metadata bounds only, not every assertion in the response schema. They neither
            validate answers nor establish target acceptance, apply authority or host support.
            The renderer validates its supported projection shape, not the whole JSON Schema.

            ## Schema identity and metadata bounds

            | Machine field | Value |
            | --- | --- |
            """.Replace("\r\n", "\n", StringComparison.Ordinal)).Append('\n');
        Row(text, "$schema", Text(root, "$schema"));
        Row(text, "$id", Text(root, "$id"));
        Row(text, "schemaVersion", Text(Field(Field(root, "properties"), "schemaVersion"), "const"));
        foreach (string key in new[] { "generatedBy", "authoringDiscriminator" }) Row(text, key, Text(metadata, key));
        foreach (string key in new[] { "authoringContractVersion", "runtimeSchemaVersion" })
            Row(text, key, Positive(metadata, key).ToString(CultureInfo.InvariantCulture));
        Row(text, "maximumArtifactSelections", artifacts.ToString(CultureInfo.InvariantCulture));
        Row(text, "maximumOutputBytes", output.ToString(CultureInfo.InvariantCulture));
        Row(text, "maximumDetailBytes", detail.ToString(CultureInfo.InvariantCulture));
        text.Append("\n## Source annotations\n\n");
        foreach (string annotation in about) text.Append("- ").Append(Escape(annotation)).Append('\n');
        text.Append("\n## Public options\n\n| Option | Repeatable |\n| --- | --- |\n");
        foreach (string option in options.Order(StringComparer.Ordinal))
            Row(text, option, repeatable.Contains(option, StringComparer.Ordinal) ? "true" : "false");
        text.Append("\n## Public commands and operations\n\n| Command | Operation | Options |\n| --- | --- | --- |\n");
        foreach (JsonElement command in commands.EnumerateArray().OrderBy(item => Text(item, "name"), StringComparer.Ordinal))
        {
            Row(text, Text(command, "name"), "(union)", JoinOptions(command));
            foreach (JsonElement operation in Field(command, "operations").EnumerateArray()
                .OrderBy(item => Text(item, "name"), StringComparer.Ordinal))
                Row(text, Text(command, "name"), Text(operation, "name"), JoinOptions(operation));
        }
        return Utf8.GetBytes(text.ToString());
    }

    private static string JoinOptions(JsonElement element) =>
        string.Join(", ", Strings(Field(element, "options")).Order(StringComparer.Ordinal));

    private static void Row(StringBuilder text, params string[] cells) =>
        text.Append("| ").AppendJoin(" | ", cells.Select(Escape)).Append(" |\n");

    // Encode Markdown/HTML punctuation as entities, so input cannot introduce links,
    // markup, table columns or fences. Control characters are rejected at admission.
    private static string Escape(string value)
    {
        var text = new StringBuilder();
        foreach (char character in value)
            if (char.IsAsciiLetterOrDigit(character) || character is ' ' or '-' or '.' or '/' or ':')
                text.Append(character);
            else
                text.Append("&#").Append(((int)character).ToString(CultureInfo.InvariantCulture)).Append(';');
        return text.ToString();
    }

    private static JsonElement Field(JsonElement parent, string name)
    {
        RequireKind(parent, JsonValueKind.Object);
        if (!parent.TryGetProperty(name, out JsonElement value)) throw new InvalidDataException("SHAPE_INVALID");
        return value;
    }

    private static string Text(JsonElement parent, string name) => String(Field(parent, name));

    private static string String(JsonElement value)
    {
        RequireKind(value, JsonValueKind.String);
        string text = value.GetString()!;
        if (string.IsNullOrWhiteSpace(text) || text.Length > 4096 || text.Any(char.IsControl)
            || text.Any(char.IsSurrogate))
            throw new InvalidDataException("TEXT_INVALID");
        return text;
    }

    private static string[] Strings(JsonElement value, bool allowEmpty = false)
    {
        RequireArray(value, allowEmpty);
        string[] values = value.EnumerateArray().Select(String).ToArray();
        if (values.Distinct(StringComparer.Ordinal).Count() != values.Length)
            throw new InvalidDataException("DUPLICATE_VALUE");
        return values;
    }

    private static long Positive(JsonElement parent, string name)
    {
        JsonElement value = Field(parent, name);
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt64(out long number) || number <= 0)
            throw new InvalidDataException("BOUNDS_INVALID");
        return number;
    }

    private static void Equal(string actual, string expected)
    {
        if (actual != expected) throw new InvalidDataException("VERSION_UNSUPPORTED");
    }

    private static void RequireKind(JsonElement value, JsonValueKind kind)
    {
        if (value.ValueKind != kind) throw new InvalidDataException("SHAPE_INVALID");
    }

    private static void RequireArray(JsonElement value, bool allowEmpty = false)
    {
        RequireKind(value, JsonValueKind.Array);
        if (value.GetArrayLength() > 4096 || (!allowEmpty && value.GetArrayLength() == 0))
            throw new InvalidDataException("SHAPE_INVALID");
    }

    private static void Shape(JsonElement value, string[] fields)
    {
        RequireKind(value, JsonValueKind.Object);
        if (!value.EnumerateObject().Select(property => property.Name).ToHashSet(StringComparer.Ordinal).SetEquals(fields))
            throw new InvalidDataException("SHAPE_INVALID");
    }

    private static void RejectDuplicates(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonProperty property in value.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new InvalidDataException("DUPLICATE_PROPERTY");
                RejectDuplicates(property.Value);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
            foreach (JsonElement item in value.EnumerateArray()) RejectDuplicates(item);
    }

    internal static int SelfTest()
    {
        // Independent synthetic machine fields; no repository prose or private fixtures.
        const string fixture = """
            {"$schema":"https://json-schema.org/draft/2020-12/schema",
             "$id":"https://schemas.islamu.org/configurator/command/v4/schema.json",
             "type":"object","additionalProperties":false,"required":["schemaVersion"],
             "properties":{"schemaVersion":{"const":"islamu-configurator-command/v4"}},
             "_metadata":{"about":["synthetic"],"generatedBy":"Configurator.CommandSchemaGenerator",
             "authoringContractVersion":2,"authoringDiscriminator":"urn:islamu:configurator:authoring:v2",
             "runtimeSchemaVersion":2,"maximumArtifactSelections":7,"maximumOutputBytes":101,
             "maximumDetailBytes":23,"commandOptions":["--zeta","--alpha"],"repeatableOptions":["--zeta"],
             "commands":[{"name":"zulu","options":["--zeta"],"operations":[{"name":"send","options":["--zeta"]}]},
             {"name":"alpha","options":["--zeta","--alpha"],"operations":[
             {"name":"show","options":["--zeta","--alpha"]},{"name":"edit","options":["--alpha"]}]}]}}
            """;
        int cases = 0;
        void Expect(bool condition)
        {
            cases++;
            if (!condition) throw new InvalidDataException("SELF_TEST_FAILED_" + cases.ToString(CultureInfo.InvariantCulture));
        }
        byte[] source = Utf8.GetBytes(fixture);
        byte[] first = Render(source);
        Expect(first.AsSpan().SequenceEqual(Render(source)));
        string rendered = Utf8.GetString(first);
        Expect(!rendered.Contains('\r') && rendered.EndsWith('\n') && first[0] != 0xef);
        // Exact machine field rows, not generated explanatory prose.
        foreach (string row in new[]
        {
            "| alpha | edit | --alpha |", "| alpha | show | --alpha&#44; --zeta |",
            "| zulu | send | --zeta |", "| --alpha | false |", "| --zeta | true |",
            "| maximumArtifactSelections | 7 |", "| maximumOutputBytes | 101 |",
            "| maximumDetailBytes | 23 |", "| schemaVersion | islamu-configurator-command/v4 |"
        }) Expect(rendered.Split('\n').Count(line => line == row) == 1);
        Expect(rendered.IndexOf("| alpha | edit", StringComparison.Ordinal)
            < rendered.IndexOf("| zulu | send", StringComparison.Ordinal));
        JsonObject reordered = JsonNode.Parse(fixture)!.AsObject();
        JsonArray commands = reordered["_metadata"]!["commands"]!.AsArray();
        JsonNode last = commands[1]!;
        commands.RemoveAt(1);
        commands.Insert(0, last);
        JsonArray options = reordered["_metadata"]!["commandOptions"]!.AsArray();
        options.Clear();
        options.Add((JsonNode?)JsonValue.Create("--alpha"));
        options.Add((JsonNode?)JsonValue.Create("--zeta"));
        Expect(first.AsSpan().SequenceEqual(Render(Utf8.GetBytes(reordered.ToJsonString()))));
        Expect(Escape("|`[]()<>&\\*_#!\"'") ==
            "&#124;&#96;&#91;&#93;&#40;&#41;&#60;&#62;&#38;&#92;&#42;&#95;&#35;&#33;&#34;&#39;");
        JsonObject escaped = JsonNode.Parse(fixture)!.AsObject();
        escaped["_metadata"]!["commands"]![0]!["name"] = "x|[y]<z>`";
        Expect(Utf8.GetString(Render(Utf8.GetBytes(escaped.ToJsonString())))
            .Contains("| x&#124;&#91;y&#93;&#60;z&#62;&#96; | send | --zeta |", StringComparison.Ordinal));
        byte[]? stored = null;
        int writes = 0;
        void Write(byte[] bytes) { stored = bytes; writes++; }
        Expect(Process(source, "--write", () => stored, Write) == "WRITTEN" && writes == 1);
        Expect(stored is not null && first.AsSpan().SequenceEqual(stored));
        Expect(Process(source, "--check", () => stored, Write) == "CURRENT" && writes == 1);
        Expect(Process(source, "--check", () => null, Write) == "STALE" && writes == 1);
        Expect(Process(source, "--check", () => [1, 2, 3], Write) == "STALE" && writes == 1);
        Expect(Process(source, "--check", () => [.. first, 10], Write) == "STALE" && writes == 1);
        void Reject(byte[] invalid, string code)
        {
            foreach (string mode in new[] { "--write", "--check" })
            {
                bool read = false;
                bool written = false;
                try
                {
                    Process(invalid, mode, () => { read = true; return stored; }, _ => written = true);
                    Expect(false);
                }
                catch (Exception exception) when (exception is InvalidDataException or JsonException)
                {
                    Expect((exception is JsonException ? "INVALID_JSON" : exception.Message) == code);
                    Expect(!read && !written && stored is not null && stored.AsSpan().SequenceEqual(first));
                }
            }
        }
        void Invalid(Action<JsonObject> mutate, string code)
        {
            JsonObject node = JsonNode.Parse(fixture)!.AsObject();
            mutate(node);
            Reject(Utf8.GetBytes(node.ToJsonString()), code);
        }
        Invalid(node => node["$id"] = "synthetic-untrusted-input", "VERSION_UNSUPPORTED");
        Invalid(node => node["$schema"] = "unknown", "VERSION_UNSUPPORTED");
        Invalid(node => node["properties"]!["schemaVersion"]!["const"] = "v5", "VERSION_UNSUPPORTED");
        Invalid(node => node["_metadata"]!["authoringContractVersion"] = 3, "VERSION_UNSUPPORTED");
        Invalid(node => node["_metadata"]!["runtimeSchemaVersion"] = 3, "VERSION_UNSUPPORTED");
        Invalid(node => node["_metadata"]!["authoringDiscriminator"] = "v3", "VERSION_UNSUPPORTED");
        Invalid(node => node["_metadata"]!["generatedBy"] = "unknown", "VERSION_UNSUPPORTED");
        Invalid(node => node["_metadata"]!.AsObject().Remove("commands"), "SHAPE_INVALID");
        Invalid(node => node["_metadata"]!["commands"] = new JsonArray(), "SHAPE_INVALID");
        Invalid(node => node["_metadata"]!["commands"]![0]!["operations"] = false, "SHAPE_INVALID");
        Invalid(node => node["_metadata"]!["commands"]![0]!["extra"] = 1, "SHAPE_INVALID");
        Invalid(node => node["_metadata"]!["commands"]![1]!["name"] = "zulu", "DUPLICATE_NAME");
        Invalid(node => node["_metadata"]!["commandOptions"]![1] = "--zeta", "DUPLICATE_VALUE");
        Invalid(node => node["_metadata"]!["repeatableOptions"]![0] = "--unknown", "OPTIONS_INVALID");
        Invalid(node => node["_metadata"]!["commands"]![0]!["options"]![0] = "--alpha", "OPTIONS_INVALID");
        Invalid(node => node["_metadata"]!["maximumOutputBytes"] = 0, "BOUNDS_INVALID");
        Invalid(node => node["_metadata"]!["maximumDetailBytes"] = 102, "BOUNDS_INVALID");
        Invalid(node => node["_metadata"]!["maximumArtifactSelections"] = "7", "BOUNDS_INVALID");
        Invalid(node => node["_metadata"]!["commands"]![0]!["name"] = "synthetic\ninput", "TEXT_INVALID");
        Invalid(node => node["_metadata"]!["commands"]![0]!["name"] = "", "TEXT_INVALID");
        Reject(Utf8.GetBytes("{\"synthetic\":"), "INVALID_JSON");
        Reject(Utf8.GetBytes(fixture.Replace("\"type\":\"object\"", "\"type\":\"object\",\"type\":\"object\"", StringComparison.Ordinal)),
            "DUPLICATE_PROPERTY");
        Reject(new byte[MaximumInputBytes + 1], "INPUT_TOO_LARGE");
        Console.WriteLine($"COMMAND_REFERENCE_SELF_TEST cases={cases} passed={cases} failed=0");
        return 0;
    }
}
