#:property RestorePackagesWithLockFile=false
#:property NuGetLockFilePath=$(MSBuildProjectExtensionsPath)BuildInteractiveWorkFixtures.packages.lock.json
#:property JsonSerializerIsReflectionEnabledByDefault=true
#:property PublishAot=false
#:property AssemblyName=Configurator.InteractiveWorkFixtures
#:project ../../src/Configurator.Core/Configurator.Core.csproj

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Configurator.Core.Artifacts;
using Configurator.Core.Definitions;
using Configurator.Core.Definitions.Authoring;

return InteractiveFixtures.Run(args);

/// <summary>
/// Builds public synthetic inputs and independent output specifications, not timing evidence.
/// Existing destinations are refused; each file is create-only. A write failure may leave
/// an incomplete directory, which must never be reused as a completed fixture set.
/// </summary>
internal static class InteractiveFixtures
{
    private static readonly string[] ExpectedLocales = ["ar", "en"];
    // These independently authored dimensions are checked against the generated trees.
    // Schema nodes include the configuration root and collection item templates;
    // answer nodes include the JSON root, containers and every scalar.
    private static readonly Specification[] Specifications =
    [
        new("few-root-deep-tree", "ea050001-4ef0-4c80-9f10-000000000001", 1, 137, 137, 128, 0, 0, 1),
        new("wide-nested-object", "ea050002-4ef0-4c80-9f10-000000000002", 1, 514, 514, 512, 0, 0, 1),
        new("large-map-list", "ea050003-4ef0-4c80-9f10-000000000003", 2, 9, 1539, 1024, 512, 0, 1),
        new("many-active-corrections", "ea050004-4ef0-4c80-9f10-000000000004", 1, 258, 258, 256, 0, 256, 1),
        new("multi-artifact", "ea050005-4ef0-4c80-9f10-000000000005", 1, 130, 130, 128, 0, 0, 8)
    ];

    internal static int Run(string[] args)
    {
        if (args is not ["--self-test"] && args is not ["--output", _])
        {
            Console.Error.WriteLine("Usage: BuildInteractiveWorkFixtures.cs --self-test | --output NEW_DIRECTORY");
            return 64;
        }
        if (args is ["--output", var operand] && (string.IsNullOrWhiteSpace(operand) || operand.StartsWith("--", StringComparison.Ordinal)))
        {
            Console.Error.WriteLine("FIXTURE_ARGUMENT_REJECTED");
            return 64;
        }
        try
        {
            Fixture[] fixtures = Specifications.Select(Build).ToArray();
            if (args is ["--self-test"]) return SelfTest(fixtures);
            WriteDirectory(args[1], fixtures);
            foreach (Fixture fixture in fixtures) Console.WriteLine(fixture.Receipt.ToJsonString());
            Console.WriteLine("INTERACTIVE_FIXTURES_COMPLETE fixtures=5");
            return 0;
        }
        catch (DefinitionException exception)
        {
            Console.Error.WriteLine("FIXTURE_CORE_REJECTED code=" + exception.Code);
            return 1;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
            or ArgumentException or NotSupportedException or JsonException or InvalidOperationException)
        {
            Console.Error.WriteLine("FIXTURE_GENERATION_REJECTED");
            return 1;
        }
    }

    private static Fixture Build(Specification spec)
    {
        var schema = ObjectSchema("Diagnostic configuration");
        var answers = new JsonObject();
        JsonObject properties = schema["properties"]!.AsObject();
        switch (spec.Id)
        {
            case "few-root-deep-tree":
                JsonObject branch = IntegerObject(128);
                JsonObject values = IntegerAnswers(128);
                for (int level = 7; level >= 1; level--)
                {
                    var parent = ObjectSchema("Nested branch " + Number(level));
                    parent["properties"]!["level" + Number(level)] = branch;
                    parent["required"] = new JsonArray("level" + Number(level));
                    branch = parent;
                    values = new JsonObject { ["level" + Number(level)] = values };
                }
                properties["tree"] = branch;
                answers["tree"] = values;
                break;
            case "wide-nested-object":
                properties["settings"] = IntegerObject(512);
                answers["settings"] = IntegerAnswers(512);
                break;
            case "large-map-list":
                JsonObject map = Node("object", "Public map");
                map["minProperties"] = 255;
                map["maxProperties"] = 257;
                map["additionalProperties"] = RowSchema();
                properties["map"] = map;
                JsonObject list = Node("array", "Public list");
                list["minItems"] = 255;
                list["maxItems"] = 257;
                list["items"] = RowSchema();
                properties["list"] = list;
                var entries = new JsonObject();
                var rows = new JsonArray();
                for (int index = 0; index < 256; index++)
                {
                    entries["entry" + Number(index, "D3")] = RowAnswer();
                    rows.Add(RowAnswer());
                }
                answers["map"] = entries;
                answers["list"] = rows;
                break;
            case "many-active-corrections":
                properties["settings"] = IntegerObject(256);
                answers["settings"] = IntegerAnswers(256);
                break;
            case "multi-artifact":
                properties["settings"] = IntegerObject(128);
                answers["settings"] = IntegerAnswers(128);
                break;
            default:
                throw new InvalidOperationException("fixture-specification");
        }
        schema["required"] = new JsonArray(properties.Select(pair => (JsonNode?)JsonValue.Create(pair.Key)).ToArray());
        var outputs = new JsonObject();
        int outputCount = spec.Id == "multi-artifact" ? 8 : 1;
        for (int index = 0; index < outputCount; index++)
            outputs["diagnostic-" + Number(index, "D2") + ".json"] = new JsonObject
            {
                ["id"] = "diagnostic-" + Number(index, "D2"),
                ["title"] = "Diagnostic output " + Number(index + 1),
                ["format"] = "json"
            };
        var overrides = new JsonObject();
        Translate(schema, "/configuration", overrides);
        for (int index = 0; index < outputCount; index++)
            overrides["/artifacts/" + Number(index) + "/title"] = "\u0645\u062e\u0631\u062c \u062a\u0634\u062e\u064a\u0635\u064a " + Number(index + 1);
        var source = new JsonObject
        {
            ["$schema"] = DefinitionProfile.AuthoringSchemaId,
            ["identity"] = new JsonObject { ["familyId"] = spec.Family, ["revision"] = 1 },
            ["project"] = new JsonObject { ["id"] = "synthetic." + spec.Id, ["name"] = "Public diagnostic: " + spec.Id },
            ["applicationVersions"] = new JsonArray("1"),
            ["configuration"] = schema,
            ["outputs"] = outputs,
            ["localization"] = new JsonObject
            {
                ["defaultLocale"] = "en",
                ["translations"] = new JsonObject
                {
                    ["ar"] = new JsonObject { ["direction"] = "rtl", ["overrides"] = overrides }
                }
            }
        };
        byte[] sourceBytes = Bytes(source);
        Definition definition = DefinitionAuthoringCompiler.Compile(DefinitionAuthoringCodec.ParseJson(sourceBytes));
        Definition admitted = DefinitionCodec.ParseJson(definition.CanonicalBytes);
        Require(admitted.Digest == definition.Digest);
        byte[] answerBytes = Bytes(answers);
        DefinitionValidationResult validation = DefinitionValidator.Validate(definition, Snapshot(answerBytes));
        Require(validation.IsReady);
        Require(properties.Count == spec.RootEntries && SchemaNodes(schema) == spec.SchemaNodes);
        using JsonDocument answerDocument = JsonDocument.Parse(answerBytes);
        Require(AnswerNodes(answerDocument.RootElement) == spec.AnswerNodes
            && AnswerLeaves(answerDocument.RootElement) == spec.AnswerLeaves);
        int collectionEntries = spec.Id == "large-map-list"
            ? answers["map"]!.AsObject().Count + answers["list"]!.AsArray().Count : 0;
        Require(collectionEntries == spec.CollectionEntries);
        var localization = new DefinitionLocalization(definition);
        Require(localization.Locales.SequenceEqual(ExpectedLocales)
            && localization.Direction("ar") == "rtl"
            && localization.Text("ar", "/configuration/title", null) == "\u0625\u0639\u062f\u0627\u062f \u062a\u0634\u062e\u064a\u0635\u064a");
        var files = new SortedDictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["source.json"] = sourceBytes,
            ["definition.json"] = definition.CanonicalBytes.ToArray(),
            ["answers.json"] = answerBytes
        };
        if (spec.Corrections != 0)
        {
            JsonObject invalid = answers.DeepClone().AsObject();
            foreach (string key in invalid["settings"]!.AsObject().Select(pair => pair.Key).ToArray())
                invalid["settings"]![key] = -1;
            byte[] invalidBytes = Bytes(invalid);
            DefinitionValidationResult rejected = DefinitionValidator.Validate(definition, Snapshot(invalidBytes));
            Require(!rejected.IsReady && rejected.Diagnostics.Count == 256
                && rejected.Diagnostics.All(item => item.Code == "value-invalid")
                && rejected.Diagnostics.Select(item => string.Join("/", item.RelativePath)).Distinct(StringComparer.Ordinal).Count() == 256);
            using DefinitionBundleResult rejectedBundle = DefinitionBundleComposer.ComposeActive(definition, Snapshot(invalidBytes));
            Require(!rejectedBundle.Succeeded && rejectedBundle.Artifacts.Count == 0);
            files.Add("answers-invalid.json", invalidBytes);
        }
        byte[] expected = ExpectedOutput(spec.Id);
        using DefinitionBundleResult bundle = DefinitionBundleComposer.ComposeActive(definition, Snapshot(answerBytes));
        Require(bundle.Succeeded && bundle.Artifacts.Count == spec.OutputCount);
        for (int index = 0; index < spec.OutputCount; index++)
        {
            string id = "diagnostic-" + Number(index, "D2");
            DefinitionArtifact artifact = bundle.Artifacts[index];
            Require(artifact.ArtifactId == id && artifact.FileName == id + ".json"
                && artifact.MediaType == "application/json" && artifact.Bytes.Span.SequenceEqual(expected));
            // Write the independent oracle, never the composer's bytes.
            files.Add("expected-" + id + ".json", expected);
        }
        var receipt = new JsonObject
        {
            ["fixture"] = spec.Id,
            ["familyId"] = spec.Family,
            ["revision"] = 1,
            ["definitionSha256"] = Hash(definition.CanonicalBytes.Span),
            ["sourceSha256"] = Hash(sourceBytes),
            ["answersSha256"] = Hash(answerBytes),
            ["rootEntries"] = spec.RootEntries,
            ["schemaNodes"] = spec.SchemaNodes,
            ["answerNodes"] = spec.AnswerNodes,
            ["answerLeaves"] = spec.AnswerLeaves,
            ["collectionEntries"] = spec.CollectionEntries,
            ["invalidAnswerCorrections"] = spec.Corrections,
            ["outputCount"] = spec.OutputCount,
            ["outputBytesEach"] = expected.Length,
            ["expectedOutputSha256Each"] = Hash(expected),
            ["files"] = new JsonArray(files.Select(pair => (JsonNode)new JsonObject
            {
                ["name"] = pair.Key, ["bytes"] = pair.Value.Length, ["sha256"] = Hash(pair.Value)
            }).ToArray())
        };
        return new(spec.Id, files, receipt);
    }

    private static JsonObject Node(string type, string title) => new()
    {
        ["type"] = type, ["title"] = title,
        ["x-configurator"] = new JsonObject { ["sensitivity"] = "public" }
    };

    private static JsonObject ObjectSchema(string title)
    {
        JsonObject schema = Node("object", title);
        schema["additionalProperties"] = false;
        schema["properties"] = new JsonObject();
        return schema;
    }

    private static JsonObject IntegerObject(int count)
    {
        JsonObject schema = ObjectSchema("Public settings");
        var required = new JsonArray();
        for (int index = 0; index < count; index++)
        {
            string name = "p" + Number(index, "D3");
            JsonObject field = Node("integer", "Public setting " + Number(index));
            field["minimum"] = 0;
            field["maximum"] = 9;
            schema["properties"]![name] = field;
            required.Add(name);
        }
        schema["required"] = required;
        return schema;
    }

    private static JsonObject IntegerAnswers(int count)
    {
        var values = new JsonObject();
        for (int index = 0; index < count; index++) values["p" + Number(index, "D3")] = 7;
        return values;
    }

    private static JsonObject RowSchema()
    {
        JsonObject row = ObjectSchema("Public row");
        row["properties"]!["enabled"] = Node("boolean", "Enabled");
        JsonObject weight = Node("integer", "Weight");
        weight["minimum"] = 0;
        weight["maximum"] = 9;
        row["properties"]!["weight"] = weight;
        row["required"] = new JsonArray("enabled", "weight");
        return row;
    }

    private static JsonObject RowAnswer() => new() { ["enabled"] = true, ["weight"] = 7 };

    private static void Translate(JsonObject schema, string pointer, JsonObject overrides)
    {
        overrides[pointer + "/title"] = "\u0625\u0639\u062f\u0627\u062f \u062a\u0634\u062e\u064a\u0635\u064a";
        if (schema["properties"] is JsonObject properties)
            foreach ((string name, JsonNode? child) in properties)
                Translate(child!.AsObject(), pointer + "/properties/" + name, overrides);
        foreach (string name in new[] { "items", "additionalProperties" })
            if (schema[name] is JsonObject child) Translate(child, pointer + "/" + name, overrides);
    }

    /// <summary>Independent literal output grammar: fixed counts, names, values and exact UTF-8 JSON.</summary>
    private static byte[] ExpectedOutput(string id)
    {
        static string Members(int count) => string.Join(",", Enumerable.Range(0, count)
            .Select(index => "\"p" + index.ToString("D3", CultureInfo.InvariantCulture) + "\":7"));
        const string row = """{"enabled":true,"weight":7}""";
        return Encoding.UTF8.GetBytes(id switch
        {
            "few-root-deep-tree" =>
                """{"tree":{"level1":{"level2":{"level3":{"level4":{"level5":{"level6":{"level7":{""" + Members(128) + "}}}}}}}}}",
            "wide-nested-object" => """{"settings":{""" + Members(512) + "}}",
            "large-map-list" => """{"list":[""" + string.Join(",", Enumerable.Repeat(row, 256))
                + """],"map":{""" + string.Join(",", Enumerable.Range(0, 256)
                    .Select(index => "\"entry" + index.ToString("D3", CultureInfo.InvariantCulture) + "\":" + row)) + "}}",
            "many-active-corrections" => """{"settings":{""" + Members(256) + "}}",
            "multi-artifact" => """{"settings":{""" + Members(128) + "}}",
            _ => throw new InvalidOperationException("fixture-specification")
        });
    }

    private static AnswerSnapshot Snapshot(byte[] bytes)
    {
        using JsonDocument document = JsonDocument.Parse(bytes);
        return new(1, document.RootElement.EnumerateObject()
            .ToDictionary(property => property.Name, property => property.Value.Clone(), StringComparer.Ordinal));
    }

    private static int SchemaNodes(JsonObject schema)
    {
        int count = 1;
        if (schema["properties"] is JsonObject properties)
            foreach ((_, JsonNode? child) in properties) count += SchemaNodes(child!.AsObject());
        foreach (string name in new[] { "items", "additionalProperties" })
            if (schema[name] is JsonObject child) count += SchemaNodes(child);
        return count;
    }

    private static int AnswerNodes(JsonElement element) => 1 + (element.ValueKind switch
    {
        JsonValueKind.Object => element.EnumerateObject().Sum(property => AnswerNodes(property.Value)),
        JsonValueKind.Array => element.EnumerateArray().Sum(AnswerNodes),
        _ => 0
    });

    private static int AnswerLeaves(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => element.EnumerateObject().Sum(property => AnswerLeaves(property.Value)),
        JsonValueKind.Array => element.EnumerateArray().Sum(AnswerLeaves),
        _ => 1
    };

    private static void WriteDirectory(string destination, Fixture[] fixtures)
    {
        string output = Path.GetFullPath(destination);
        if (Path.Exists(output)) throw new IOException("destination-exists");
        Directory.CreateDirectory(output);
        foreach (Fixture fixture in fixtures)
        {
            string directory = Path.Combine(output, fixture.Id);
            Directory.CreateDirectory(directory);
            foreach ((string name, byte[] content) in fixture.Files) Write(Path.Combine(directory, name), content);
        }
        // The manifest is the completion record; failures before this leave no success receipt.
        Write(Path.Combine(output, "manifest.json"), Bytes(new JsonObject
        {
            ["format"] = "public-interactive-fixtures-0.0.1",
            ["synthetic"] = true,
            ["status"] = "complete",
            ["limitation"] = "Diagnostic fixtures only; no browser, timing, host acceptance or performance evidence.",
            ["fixtures"] = new JsonArray(fixtures.Select(fixture => (JsonNode)fixture.Receipt.DeepClone()).ToArray())
        }));
    }

    private static void Write(string path, byte[] bytes)
    {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        stream.Write(bytes);
    }

    private static int SelfTest(Fixture[] fixtures)
    {
        Require(fixtures.Length == 5 && fixtures.Select(fixture => fixture.Id).Distinct(StringComparer.Ordinal).Count() == 5);
        Require(Specifications.Select(spec => spec.Family).Distinct(StringComparer.Ordinal).Count() == 5);
        foreach (Specification spec in Specifications)
        {
            Fixture original = fixtures.Single(fixture => fixture.Id == spec.Id);
            Fixture repeated = Build(spec);
            Require(original.Receipt.ToJsonString() == repeated.Receipt.ToJsonString());
            Require(original.Files.All(pair => repeated.Files[pair.Key].AsSpan().SequenceEqual(pair.Value)));
            // A different, still-valid answer must compose different bytes. This
            // catches a composer or fixture that accidentally ignores its answers.
            JsonObject changed = JsonNode.Parse(original.Files["answers.json"])!.AsObject();
            JsonObject parent = changed;
            while (parent.First().Value is JsonObject nested) parent = nested;
            string key = parent.First().Key;
            parent[key] = parent[key]!.GetValueKind() == JsonValueKind.True
                ? JsonValue.Create(false) : JsonValue.Create(8);
            using DefinitionBundleResult changedBundle = DefinitionBundleComposer.ComposeActive(
                DefinitionSourceCodec.ParseJson(original.Files["source.json"]), Snapshot(Bytes(changed)));
            Require(changedBundle.Succeeded && changedBundle.Artifacts.Count == spec.OutputCount
                && changedBundle.Artifacts.All(artifact => !artifact.Bytes.Span.SequenceEqual(ExpectedOutput(spec.Id))));
            if (spec.Id == "large-map-list") VerifyCollectionActions(original);
            Console.WriteLine("FIXTURE_SELF_TEST fixture=" + spec.Id + " passed=true");
        }
        string scratch = Path.Combine(Path.GetTempPath(), "interactive-fixtures-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scratch);
        try
        {
            string destination = Path.Combine(scratch, "generated");
            WriteDirectory(destination, fixtures);
            foreach (Fixture fixture in fixtures)
                foreach ((string name, byte[] bytes) in fixture.Files)
                    Require(File.ReadAllBytes(Path.Combine(destination, fixture.Id, name)).AsSpan().SequenceEqual(bytes));
            byte[] manifest = File.ReadAllBytes(Path.Combine(destination, "manifest.json"));
            bool refused = false;
            try { WriteDirectory(destination, fixtures); }
            catch (IOException) { refused = true; }
            Require(refused && File.ReadAllBytes(Path.Combine(destination, "manifest.json")).AsSpan().SequenceEqual(manifest));
            string empty = Path.Combine(scratch, "existing-empty");
            Directory.CreateDirectory(empty);
            refused = false;
            try { WriteDirectory(empty, fixtures); }
            catch (IOException) { refused = true; }
            Require(refused && !Directory.EnumerateFileSystemEntries(empty).Any());
            string collision = Path.Combine(scratch, "existing-file");
            Write(collision, "synthetic-sentinel"u8.ToArray());
            refused = false;
            try { Write(collision, "replacement"u8.ToArray()); }
            catch (IOException) { refused = true; }
            Require(refused && File.ReadAllBytes(collision).AsSpan().SequenceEqual("synthetic-sentinel"u8));
        }
        finally { Directory.Delete(scratch, recursive: true); }
        Console.WriteLine("FIXTURE_SELF_TEST_COMPLETE fixtures=5 deterministic=true createOnly=true failures=0");
        return 0;
    }

    private static void VerifyCollectionActions(Fixture fixture)
    {
        Definition definition = DefinitionSourceCodec.ParseJson(fixture.Files["source.json"]);
        JsonObject answers = JsonNode.Parse(fixture.Files["answers.json"])!.AsObject();
        JsonObject map = answers["map"]!.AsObject();
        JsonArray list = answers["list"]!.AsArray();
        map["entry256"] = RowAnswer();
        list.Add(RowAnswer());
        Verify(257);
        map.Remove("entry256");
        list.RemoveAt(256);
        Verify(256);
        map.Remove("entry255");
        list.RemoveAt(255);
        Verify(255);
        map["entry255"] = RowAnswer();
        list.Add(RowAnswer());
        Verify(256);

        void Verify(int count)
        {
            using DefinitionBundleResult bundle = DefinitionBundleComposer.ComposeActive(definition, Snapshot(Bytes(answers)));
            Require(bundle.Succeeded && bundle.Artifacts.Count == 1);
            using JsonDocument output = JsonDocument.Parse(bundle.Artifacts[0].Bytes);
            Require(output.RootElement.GetProperty("map").EnumerateObject().Count() == count
                && output.RootElement.GetProperty("list").GetArrayLength() == count);
            if (count == 256)
                Require(bundle.Artifacts[0].Bytes.Span.SequenceEqual(ExpectedOutput("large-map-list")));
        }
    }

    private static byte[] Bytes(JsonNode node) => Encoding.UTF8.GetBytes(node.ToJsonString());
    private static string Number(int value, string format = "D") => value.ToString(format, CultureInfo.InvariantCulture);
    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static void Require(bool condition)
    {
        if (!condition) throw new InvalidOperationException("fixture-specification");
    }

    private sealed record Specification(string Id, string Family, int RootEntries, int SchemaNodes,
        int AnswerNodes, int AnswerLeaves, int CollectionEntries, int Corrections, int OutputCount);
    private sealed record Fixture(string Id, SortedDictionary<string, byte[]> Files, JsonObject Receipt);
}
