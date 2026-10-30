#:property RestorePackagesWithLockFile=false
#:property AssemblyName=Configurator.CliPackageValidation
#:property RootNamespace=Configurator.Ci

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

return await CliPackageValidation.RunAsync(args);

internal static class CliPackageValidation
{
    private const int MaximumOutputBytes = 65_536;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    internal static async Task<int> RunAsync(string[] arguments)
    {
        try
        {
            string root = Directory.GetCurrentDirectory();
            byte[] schemaBytes = await File.ReadAllBytesAsync(Path.Combine(root, "schemas/islamu-configurator-command-v4.schema.json"));
            using JsonDocument schema = JsonDocument.Parse(schemaBytes);
            if (arguments is ["--self-test"]) return SelfTest(schema.RootElement);
            Require(arguments is ["--package-directory", _, "--expected-rid", _]);
            string directory = Path.GetFullPath(arguments[1]);
            string rid = arguments[3];
            Require(rid == RuntimeInformation.RuntimeIdentifier);
            byte[] contractBytes = await File.ReadAllBytesAsync(Path.Combine(directory, "contracts/package.json"));
            using JsonDocument contract = JsonDocument.Parse(contractBytes);
            JsonElement package = contract.RootElement;
            Require(package.GetProperty("surface").GetString() == "cli");
            Require(package.GetProperty("runtimeIdentifiers").EnumerateArray().Any(item => item.GetString() == rid));
            Require(package.GetProperty("selfContained").GetBoolean() && package.GetProperty("publishSingleFile").GetBoolean());
            Require(!package.GetProperty("publishTrimmed").GetBoolean());
            Require(package.GetProperty("executableName").GetString() == "islamu-configurator");
            Require(package.GetProperty("protectedExportHosts").EnumerateArray().Select(item => item.GetString()).SequenceEqual(["linux"]));
            string executable = Path.Combine(directory, OperatingSystem.IsWindows() ? "islamu-configurator.exe" : "islamu-configurator");
            byte[] packagedSchema = await File.ReadAllBytesAsync(Path.Combine(directory, "schemas/islamu-configurator-command-v4.schema.json"));
            Console.WriteLine($"CLI_PACKAGE_IDENTITY rid={rid} host={RuntimeInformation.RuntimeIdentifier} executableSha256={Hash(await File.ReadAllBytesAsync(executable))} contractSha256={Hash(contractBytes)} packagedSchemaSha256={Hash(packagedSchema)} validatorSchemaSha256={Hash(schemaBytes)}");

            int failures = 0;
            async Task Cohort(string name, Func<Task> check)
            {
                try
                {
                    await check();
                    Console.WriteLine($"CLI_PACKAGE_COHORT {name} passed");
                }
                catch (Exception error) when (IsInputError(error))
                {
                    failures++;
                    // Never copy subprocess output, private paths or exception messages into CI evidence.
                    Console.WriteLine($"CLI_PACKAGE_COHORT {name} failed contract-invalid");
                }
            }

            await Cohort("schema-freshness", () =>
            {
                Require(packagedSchema.AsSpan().SequenceEqual(schemaBytes));
                Require(!File.Exists(Path.Combine(directory, "schemas/islamu-configurator-command-v1.schema.json"))
                    && !File.Exists(Path.Combine(directory, "schemas/islamu-configurator-command-v2.schema.json"))
                    && !File.Exists(Path.Combine(directory, "schemas/islamu-configurator-command-v3.schema.json")));
                return Task.CompletedTask;
            });
            await Cohort("doctor", async () =>
            {
                var result = await ExecuteAsync(directory, executable, ["doctor", "--machine"]);
                using JsonDocument document = Envelope(result, schema.RootElement, "doctor", "doctor", OperatingSystem.IsLinux() ? 0 : 4);
                JsonElement response = document.RootElement;
                if (OperatingSystem.IsLinux())
                {
                    Require(response.GetProperty("artifacts").GetArrayLength() == 1);
                    JsonElement report = response.GetProperty("artifacts")[0];
                    Require(report.GetProperty("kind").GetString() == "doctor-report");
                    Require(report.GetProperty("sensitivity").GetString() == "public");
                    Require(report.GetProperty("digest").GetString() == Hash(schemaBytes));
                    Require(report.GetProperty("writeStatus").GetString() == "none");
                }
                else
                {
                    Require(response.GetProperty("artifacts").GetArrayLength() == 0);
                    Require(response.GetProperty("diagnostics").EnumerateArray()
                        .Any(item => item.GetProperty("code").GetString() == "protected-writer-unavailable"));
                }
            });
            await Cohort("help", async () =>
            {
                var result = await ExecuteAsync(directory, executable, ["--help", "--machine"]);
                using JsonDocument document = Envelope(result, schema.RootElement, "cli", "help", 0);
                JsonElement help = document.RootElement.GetProperty("help");
                Require(help.GetProperty("scope").GetString() == "root");
                Require(help.GetProperty("commandPath").GetArrayLength() == 0);
                Require(help.GetProperty("commands").EnumerateArray().Select(item => item.GetString())
                    .ToHashSet(StringComparer.Ordinal).SetEquals(["artifact", "catalogue", "definition", "doctor", "env"]));
                Require(document.RootElement.GetProperty("artifacts").GetArrayLength() == 0);
            });

            string workspace = Path.Combine(Path.GetTempPath(), "configurator-package-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(workspace);
            try
            {
                const string definition = """
                    {"schemaVersion":2,"profile":"bounded-v2","familyId":"ac899fc7-d7f7-4ef3-9926-7f447387fe8c",
                     "revision":1,"project":{"id":"example.service","name":"Package service","publisher":"example"},"applicationVersions":["1.0"],
                     "entryPoints":["setup"],"locales":["en"],
                     "requiredCapabilities":["projection.copy","projection.object","schema.object","schema.string"],
                      "configuration":{"type":"object","additionalProperties":false,"required":["service.name"],"properties":{
                        "service.name":{"type":"string","title":"Service name","x-configurator":{"sensitivity":"sensitive","detail":"standard"}}}},
                     "artifacts":[{"id":"config","format":"json","fileName":"service.json",
                        "targetUpdateMode":"create","restartRequirement":"process",
                        "projection":{"op":"object","members":{"name":{"op":"copy","reference":{"scope":"root","path":"/service.name"}}}}}]}
                    """;
                const string answers = """{"answerContractVersion":1,"values":{"service.name":"synthetic-package-service"},"omitted":[]}""";
                byte[] expected = """{"name":"synthetic-package-service"}"""u8.ToArray();
                string source = Path.Combine(workspace, "definition.json");
                string input = Path.Combine(workspace, "answers.json");
                string target = Path.Combine(workspace, "service.json");
                await File.WriteAllTextAsync(source, definition);
                await File.WriteAllTextAsync(input, answers);
                string[] common = ["--definition", "definition.json", "--artifact", "config", "--input", "answers.json", "--machine"];
                await Cohort("readonly-validation", async () =>
                {
                    var result = await ExecuteAsync(workspace, executable, ["artifact", "validate", .. common]);
                    using JsonDocument document = Envelope(result, schema.RootElement, "artifact", "validate", 0);
                    Artifact(document.RootElement, expected, "none", "not-applicable", "not-applicable", "input");
                    Require(!File.Exists(target));
                    Require((await File.ReadAllTextAsync(source)) == definition && (await File.ReadAllTextAsync(input)) == answers);
                    Require(Directory.GetFiles(workspace).Length == 2 && Directory.GetDirectories(workspace).Length == 0);
                });
                await Cohort(OperatingSystem.IsLinux() ? "protected-create" : "unsupported-protected-create-refusal", async () =>
                {
                    var result = await ExecuteAsync(workspace, executable, ["artifact", "create", .. common, "--output", "service.json"]);
                    using JsonDocument document = Envelope(result, schema.RootElement, "artifact", "create", OperatingSystem.IsLinux() ? 0 : 74);
                    if (OperatingSystem.IsLinux())
                    {
                        Artifact(document.RootElement, expected, "written", "established", "succeeded", "output");
                        Require((await File.ReadAllBytesAsync(target)).AsSpan().SequenceEqual(expected));
                        Require(File.GetUnixFileMode(target) == (UnixFileMode.UserRead | UnixFileMode.UserWrite));
                        var conflict = await ExecuteAsync(workspace, executable, ["artifact", "create", .. common, "--output", "service.json"]);
                        using JsonDocument refused = Envelope(conflict, schema.RootElement, "artifact", "create", 74);
                        Artifact(refused.RootElement, expected, "conflict", "not-established", "not-attempted", "output");
                        Require((await File.ReadAllBytesAsync(target)).AsSpan().SequenceEqual(expected));
                    }
                    else
                    {
                        Artifact(document.RootElement, expected, "blocked", "not-established", "not-attempted", "output");
                        Require(!File.Exists(target) && Directory.GetFiles(workspace).Length == 2);
                    }
                });
                await Cohort("comparison-and-complete-finding-continuation", async () =>
                {
                    string existing = Path.Combine(workspace, "existing.json");
                    await File.WriteAllBytesAsync(existing, expected);
                    string[] comparison = ["artifact", "compare", "--definition", "definition.json", "--artifact", "config",
                        "--input", "existing.json", "--machine", "--detail", "findings", "--limit", "32"];
                    var compatible = await ExecuteAsync(workspace, executable, comparison);
                    using (JsonDocument document = Envelope(compatible, schema.RootElement, "artifact", "compare", 0))
                        Require(document.RootElement.GetProperty("assessment").GetProperty("compatibility").GetString() == "compatible");
                    var supplied = new JsonObject { ["name"] = "synthetic-package-service" };
                    for (int index = 0; index < 3000; index++) supplied["synthetic-unknown-" + index] = true;
                    byte[] original = Encoding.UTF8.GetBytes(supplied.ToJsonString());
                    await File.WriteAllBytesAsync(existing, original);
                    int offset = 0;
                    string? digest = null;
                    var seen = new HashSet<string>(StringComparer.Ordinal);
                    while (true)
                    {
                        string[] continuation = digest is null ? [] : ["--offset", offset.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            "--expected-detail-digest", digest];
                        var result = await ExecuteAsync(workspace, executable, [.. comparison, .. continuation]);
                        using JsonDocument document = Envelope(result, schema.RootElement, "artifact", "compare", 2);
                        JsonElement report = document.RootElement.GetProperty("assessment");
                        Require(report.GetProperty("retainedCount").GetInt32() == 3000 && report.GetProperty("total").GetInt32() == 3000);
                        JsonElement page = document.RootElement.GetProperty("detail");
                        foreach (JsonElement finding in page.GetProperty("findings").EnumerateArray())
                            Require(seen.Add(finding.GetProperty("source").GetProperty("ordinals").GetRawText()));
                        digest = page.GetProperty("digest").GetString()!;
                        if (page.GetProperty("terminal").GetBoolean()) break;
                        offset = page.GetProperty("nextOffset").GetInt32();
                    }
                    Require(seen.Count == 3000 && (await File.ReadAllBytesAsync(existing)).AsSpan().SequenceEqual(original));
                    var last = await ExecuteAsync(workspace, executable,
                        [.. comparison, "--offset", "3000", "--expected-detail-digest", digest!]);
                    using JsonDocument terminal = Envelope(last, schema.RootElement, "artifact", "compare", 2);
                    Require(terminal.RootElement.GetProperty("detail").GetProperty("terminal").GetBoolean());
                    Require(terminal.RootElement.GetProperty("detail").GetProperty("returnedCount").GetInt32() == 0);
                });
            }
            finally { Directory.Delete(workspace, recursive: true); }
            Console.WriteLine($"CLI_PACKAGE_RESULT {(failures == 0 ? "passed" : "failed")} failedCohorts={failures}");
            return failures == 0 ? 0 : 1;
        }
        catch (Exception error) when (IsInputError(error))
        {
            Console.WriteLine("CLI_PACKAGE_RESULT failed input-or-host-invalid");
            return 1;
        }
    }

    private static void Artifact(JsonElement root, byte[] expected, string write, string installation, string verification, string intent)
    {
        Require(root.GetProperty("preparation").GetString() == "ready");
        Require(root.GetProperty("artifacts").GetArrayLength() == 1);
        JsonElement artifact = root.GetProperty("artifacts")[0];
        Require(artifact.GetProperty("id").GetString() == "config");
        Require(artifact.GetProperty("kind").GetString() == "definition-artifact");
        Require(artifact.GetProperty("mediaType").GetString() == "application/json");
        Require(artifact.GetProperty("sensitivity").GetString() == "restricted");
        Require(artifact.GetProperty("digest").GetString() == Hash(expected));
        Require(artifact.GetProperty("writeStatus").GetString() == write);
        Require(artifact.GetProperty("installation").GetString() == installation);
        Require(artifact.GetProperty("verification").GetString() == verification);
        Require(artifact.GetProperty("pathIntent").GetString() == intent);
    }

    private static JsonDocument Envelope((int ExitCode, byte[] Output, byte[] Error) result, JsonElement schema,
        string family, string operation, int expectedExit)
    {
        byte[] bytes = result.Output;
        Require(result.ExitCode == expectedExit && result.Error.Length == 0);
        Require(bytes.Length is > 0 and <= MaximumOutputBytes && bytes[^1] == '\n');
        Require(bytes.Count(value => value == '\n') == 1 && !bytes.Contains((byte)'\r'));
        Require(!bytes.Any(value => value is 0x1B or 0x7F || value < 0x20 && value != '\n'));
        string text = StrictUtf8.GetString(bytes);
        Require(!text.Contains("synthetic-package-service", StringComparison.Ordinal)
            && !text.Contains("configurator-package-", StringComparison.Ordinal));
        JsonDocument document = JsonDocument.Parse(bytes, new() { MaxDepth = 16 });
        try
        {
            Validate(document.RootElement, schema, schema);
            JsonElement root = document.RootElement;
            Require(root.GetProperty("invocation").GetProperty("commandFamily").GetString() == family);
            Require(root.GetProperty("invocation").GetProperty("operation").GetString() == operation);
            Require(root.GetProperty("exitCode").GetInt32() == expectedExit);
            string category = expectedExit switch { 0 => "success", 2 => "validation", 3 => "incomplete", 4 => "blocked",
                65 => "data", 74 => "io", _ => throw new InvalidDataException() };
            Require(root.GetProperty("status").GetString() == category && root.GetProperty("exitCategory").GetString() == category);
            Require(root.GetProperty("completion").GetString() == (expectedExit == 0 || operation == "compare" && expectedExit is 2 or 3 ? "completed" : "failed"));
            Require(!root.GetProperty("dryRun").GetBoolean());
            Require((operation == "compare" || !root.TryGetProperty("detail", out _)) && !root.TryGetProperty("authoringContractVersion", out _)
                && !root.TryGetProperty("runtimeSchemaVersion", out _) && !root.TryGetProperty("schema", out _));
            Require(root.TryGetProperty("help", out _) == (operation == "help"));
            Require(root.GetProperty("diagnosticsTotal").GetInt32() == root.GetProperty("diagnostics").GetArrayLength()
                + root.GetProperty("diagnosticsOmitted").GetInt32());
            if (expectedExit == 0) Require(root.GetProperty("diagnosticsTotal").GetInt32() == 0);
            else if (operation != "compare") Require(root.GetProperty("diagnosticsTotal").GetInt32() > 0);
            if (root.TryGetProperty("assessment", out JsonElement assessment))
            {
                Require(operation == "compare" && family == "artifact");
                Assessment(root, assessment);
            }
            else Require(operation != "compare");
            if (root.TryGetProperty("detail", out JsonElement detail)) Detail(root, detail);
            foreach (JsonElement diagnostic in root.GetProperty("diagnostics").EnumerateArray())
                if (diagnostic.TryGetProperty("source", out JsonElement source))
                {
                    int startLine = source.GetProperty("startLine").GetInt32();
                    int endLine = source.GetProperty("endLine").GetInt32();
                    Require(endLine > startLine || endLine == startLine
                        && source.GetProperty("endColumn").GetInt32() >= source.GetProperty("startColumn").GetInt32());
                }
            return document;
        }
        catch { document.Dispose(); throw; }
    }

    private static void Assessment(JsonElement root, JsonElement assessment)
    {
        Require(root.GetProperty("preparation").GetString() == "not-evaluated");
        Require(root.GetProperty("readiness").GetProperty("state").GetString() == "unassessed");
        Require(root.GetProperty("artifacts").GetArrayLength() == 0);
        bool complete = assessment.GetProperty("inventory").GetString() == "complete";
        Require(assessment.TryGetProperty("total", out JsonElement total) == complete);
        if (complete) Require(total.GetInt32() == assessment.GetProperty("retainedCount").GetInt32());
        int expected = assessment.GetProperty("compatibility").GetString() switch
        {
            "compatible" => 0, "changes-required" => 2, "incomplete" => 3, "unsupported" => 4, "invalid-input" => 65,
            _ => throw new InvalidDataException()
        };
        Require(root.GetProperty("exitCode").GetInt32() == expected);
        if (expected == 0) Require(complete && assessment.GetProperty("coverage").GetString() == "complete");
    }

    private static void Detail(JsonElement root, JsonElement detail)
    {
        Require(detail.GetProperty("digest").GetString() == root.GetProperty("detailDigest").GetString());
        Require(Encoding.UTF8.GetByteCount(detail.GetRawText())
            + root.GetProperty("diagnostics").EnumerateArray().Sum(item => Encoding.UTF8.GetByteCount(item.GetRawText())) <= 8192);
        int count = detail.GetProperty("diagnostics").GetArrayLength() + detail.GetProperty("coverage").GetArrayLength()
            + detail.GetProperty("findings").GetArrayLength();
        int offset = detail.GetProperty("offset").GetInt32();
        int retained = detail.GetProperty("retainedCount").GetInt32();
        Require(detail.GetProperty("returnedCount").GetInt32() == count && count <= detail.GetProperty("limit").GetInt32());
        Require(offset <= retained && offset + count <= retained);
        bool terminal = offset + count == retained;
        Require(detail.GetProperty("terminal").GetBoolean() == terminal);
        Require(terminal ? detail.GetProperty("nextOffset").ValueKind == JsonValueKind.Null
            : count > 0 && detail.GetProperty("nextOffset").GetInt32() == offset + count);
        if (detail.GetProperty("kind").GetString() == "findings")
        {
            JsonElement assessment = root.GetProperty("assessment");
            Require(detail.GetProperty("diagnostics").GetArrayLength() == 0 && detail.GetProperty("coverage").GetArrayLength() == 0);
            Require(retained == assessment.GetProperty("retainedCount").GetInt32());
            Require(detail.TryGetProperty("total", out JsonElement total) == assessment.TryGetProperty("total", out JsonElement expected));
            if (total.ValueKind != JsonValueKind.Undefined) Require(total.GetInt32() == expected.GetInt32());
            foreach (JsonElement finding in detail.GetProperty("findings").EnumerateArray())
                if (finding.TryGetProperty("schema", out JsonElement location))
                    Require(location.GetProperty("relativePath").EnumerateArray().Sum(item => item.GetString()!.Length) <= 256);
        }
    }

    // Interpret only the bounded vocabulary used by the repository-owned command schema.
    // The schema comes from the checkout, never from the executable being qualified.
    private static void Validate(JsonElement value, JsonElement rule, JsonElement schema)
    {
        if (rule.TryGetProperty("$ref", out JsonElement reference))
        {
            string name = reference.GetString()!;
            if (name == "https://json-schema.org/draft/2020-12/schema")
            {
                Require(value.ValueKind == JsonValueKind.Object);
                return;
            }
            Require(name.StartsWith("#/$defs/", StringComparison.Ordinal));
            Validate(value, schema.GetProperty("$defs").GetProperty(name[8..]), schema);
            return;
        }
        bool Matches(JsonElement candidate)
        {
            try { Validate(value, candidate, schema); return true; }
            catch (Exception exception) when (IsInputError(exception)) { return false; }
        }
        if (rule.TryGetProperty("not", out JsonElement excluded)) Require(!Matches(excluded));
        if (rule.TryGetProperty("oneOf", out JsonElement alternatives)) Require(alternatives.EnumerateArray().Count(Matches) == 1);
        if (rule.TryGetProperty("allOf", out JsonElement requirements))
            foreach (JsonElement requirement in requirements.EnumerateArray()) Validate(value, requirement, schema);
        if (rule.TryGetProperty("if", out JsonElement condition) && Matches(condition)
            && rule.TryGetProperty("then", out JsonElement consequence)) Validate(value, consequence, schema);
        if (rule.TryGetProperty("const", out JsonElement constant)) Require(JsonElement.DeepEquals(value, constant));
        if (rule.TryGetProperty("enum", out JsonElement choices)) Require(choices.EnumerateArray().Any(item => JsonElement.DeepEquals(value, item)));
        if (rule.TryGetProperty("type", out JsonElement type))
            Require(type.GetString() switch
            {
                "object" => value.ValueKind == JsonValueKind.Object,
                "array" => value.ValueKind == JsonValueKind.Array,
                "string" => value.ValueKind == JsonValueKind.String,
                "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
                "integer" => value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out _),
                "null" => value.ValueKind == JsonValueKind.Null,
                _ => false
            });
        if (value.ValueKind == JsonValueKind.Object)
        {
            Require(value.EnumerateObject().Select(item => item.Name).Distinct(StringComparer.Ordinal).Count() == value.EnumerateObject().Count());
            if (rule.TryGetProperty("required", out JsonElement requiredNames))
                foreach (JsonElement required in requiredNames.EnumerateArray()) Require(value.TryGetProperty(required.GetString()!, out _));
            foreach (JsonProperty property in value.EnumerateObject())
            {
                if (rule.TryGetProperty("properties", out JsonElement properties)
                    && properties.TryGetProperty(property.Name, out JsonElement child)) Validate(property.Value, child, schema);
                else if (rule.TryGetProperty("additionalProperties", out JsonElement additional))
                    Require(additional.ValueKind != JsonValueKind.False);
            }
        }
        if (value.ValueKind == JsonValueKind.Array)
        {
            if (rule.TryGetProperty("maxItems", out JsonElement maximum)) Require(value.GetArrayLength() <= maximum.GetInt32());
            if (rule.TryGetProperty("uniqueItems", out JsonElement unique) && unique.GetBoolean())
            {
                JsonElement[] items = value.EnumerateArray().ToArray();
                for (int index = 0; index < items.Length; index++)
                    Require(!items.Take(index).Any(item => JsonElement.DeepEquals(item, items[index])));
            }
            foreach (JsonElement item in value.EnumerateArray()) Validate(item, rule.GetProperty("items"), schema);
        }
        if (value.ValueKind == JsonValueKind.String)
        {
            string text = value.GetString()!;
            if (rule.TryGetProperty("minLength", out JsonElement minimum)) Require(text.Length >= minimum.GetInt32());
            if (rule.TryGetProperty("maxLength", out JsonElement maximum)) Require(text.Length <= maximum.GetInt32());
            if (rule.TryGetProperty("pattern", out JsonElement pattern))
                Require(Regex.IsMatch(text, pattern.GetString()!, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)));
        }
        if (value.ValueKind == JsonValueKind.Number)
        {
            if (rule.TryGetProperty("minimum", out JsonElement minimum)) Require(value.GetDecimal() >= minimum.GetDecimal());
            if (rule.TryGetProperty("maximum", out JsonElement maximum)) Require(value.GetDecimal() <= maximum.GetDecimal());
        }
    }

    private static async Task<(int ExitCode, byte[] Output, byte[] Error)> ExecuteAsync(string directory, string executable, string[] arguments)
    {
        var start = new ProcessStartInfo(executable)
        {
            WorkingDirectory = directory, UseShellExecute = false,
            RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = true
        };
        foreach (string argument in arguments) start.ArgumentList.Add(argument);
        using Process process = Process.Start(start) ?? throw new InvalidDataException();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        async Task<byte[]> ReadAsync(Stream stream)
        {
            using var output = new MemoryStream();
            byte[] buffer = new byte[4096];
            int count;
            while ((count = await stream.ReadAsync(buffer, deadline.Token)) != 0)
            {
                Require(output.Length + count <= MaximumOutputBytes);
                output.Write(buffer, 0, count);
            }
            return output.ToArray();
        }
        Task<byte[]> output = ReadAsync(process.StandardOutput.BaseStream);
        Task<byte[]> error = ReadAsync(process.StandardError.BaseStream);
        process.StandardInput.Close();
        try
        {
            await Task.WhenAll(output, error, process.WaitForExitAsync(deadline.Token));
            return (process.ExitCode, await output, await error);
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            }
        }
    }

    private static int SelfTest(JsonElement schema)
    {
        const string good = """
            {"schemaVersion":"islamu-configurator-command/v4","invocation":{"commandFamily":"artifact","operation":"validate","mode":"machine"},"status":"success","exitCategory":"success","exitCode":0,"dryRun":false,"completion":"completed","preparation":"ready","targetAcceptance":"unassessed","cleanup":"not-required","delivery":"attempt","diagnosticsTotal":0,"diagnosticsOmitted":0,"detailDigest":"0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef","diagnostics":[],"artifacts":[{"id":"config","kind":"definition-artifact","mediaType":"application/json","digest":"0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef","sensitivity":"restricted","pathIntent":"input","writeStatus":"none","installation":"not-applicable","verification":"not-applicable"}],"coverage":{"coveredCount":1,"missingCount":0,"detailDigest":"0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"},"readiness":{"state":"ready","missingCount":0,"blockedCount":0}}
            """;
        int rejected = 0;
        void Check(string text, bool valid, int exit = 0, byte[]? error = null)
        {
            bool accepted = true;
            try { using JsonDocument document = Envelope((exit, Encoding.UTF8.GetBytes(text), error ?? []), schema, "artifact", "validate", 0); }
            catch (Exception exception) when (IsInputError(exception)) { accepted = false; }
            Require(accepted == valid);
            if (!valid) rejected++;
        }
        Check(good + "\n", true);
        foreach (string text in new[] { "", "not-json\n", "[]\n", good, good + "\n\n", "\uFEFF" + good + "\n",
            good + "\r\n", good + "\n{}\n", "\u001b" + good + "\n", new string(' ', MaximumOutputBytes) + good + "\n",
            good.Replace("\"dryRun\":false", "\"dryRun\":false,\"dryRun\":false", StringComparison.Ordinal) + "\n" })
            Check(text, false);
        Check(good + "\n", false, 74);
        Check(good + "\n", false, error: "unexpected"u8.ToArray());
        var mutations = new Action<JsonObject>[]
        {
            obj => obj["unknown"] = true,
            obj => obj.Remove("schemaVersion"),
            obj => obj["schemaVersion"] = "islamu-configurator-command/v1",
            obj => obj["schemaVersion"] = "islamu-configurator-command/v2",
            obj => obj["schemaVersion"] = "islamu-configurator-command/v3",
            obj => obj["invocation"]!["mode"] = "text",
            obj => obj["invocation"]!["operation"] = "create",
            obj => obj["invocation"]!["unknown"] = true,
            obj => obj["status"] = "io",
            obj => obj["exitCode"] = 4,
            obj => obj["dryRun"] = true,
            obj => obj["completion"] = "failed",
            obj => obj["targetAcceptance"] = "accepted",
            obj => obj["delivery"] = "delivered",
            obj => obj["diagnosticsOmitted"] = 1,
            obj => obj["detailDigest"] = "invalid",
            obj => obj["artifacts"]![0]!["digest"] = "invalid",
            obj => obj["artifacts"]![0]!["installation"] = "invalid",
            obj => obj["artifacts"]![0]!.AsObject().Remove("verification"),
            obj => obj["artifacts"]![0]!["value"] = "private",
            obj => obj["coverage"]!["coveredCount"] = -1,
            obj => obj["readiness"]!["missingCount"] = "0",
            obj => obj["help"] = new JsonObject(),
            obj => obj["authoringContractVersion"] = 1,
            obj => obj["diagnostics"] = new JsonArray(new JsonObject { ["code"] = "failure", ["path"] = "not-a-pointer", ["severity"] = "error" })
        };
        foreach (Action<JsonObject> mutate in mutations)
        {
            JsonObject obj = JsonNode.Parse(good)!.AsObject();
            mutate(obj);
            Check(obj.ToJsonString() + "\n", false);
        }
        void CheckRefusal(Action<JsonObject>? mutate, bool valid)
        {
            JsonObject obj = JsonNode.Parse(good)!.AsObject();
            obj["invocation"]!["operation"] = "create";
            obj["status"] = "io";
            obj["exitCategory"] = "io";
            obj["exitCode"] = 74;
            obj["completion"] = "failed";
            obj["cleanup"] = "completed";
            obj["diagnosticsTotal"] = 1;
            obj["diagnostics"] = new JsonArray(new JsonObject { ["code"] = "protected-write-blocked", ["path"] = "", ["severity"] = "error" });
            obj["artifacts"]![0]!["digest"] = "44136fa355b3678a1146ad16f7e8649e94fb4fc21fe77e8310c060f61caaff8a";
            obj["artifacts"]![0]!["pathIntent"] = "output";
            obj["artifacts"]![0]!["writeStatus"] = "blocked";
            obj["artifacts"]![0]!["installation"] = "not-established";
            obj["artifacts"]![0]!["verification"] = "not-attempted";
            mutate?.Invoke(obj);
            bool accepted = true;
            try
            {
                using JsonDocument document = Envelope((74, Encoding.UTF8.GetBytes(obj.ToJsonString() + "\n"), []), schema, "artifact", "create", 74);
                Artifact(document.RootElement, "{}"u8.ToArray(), "blocked", "not-established", "not-attempted", "output");
            }
            catch (Exception exception) when (IsInputError(exception)) { accepted = false; }
            Require(accepted == valid);
            if (!valid) rejected++;
        }
        CheckRefusal(null, true);
        CheckRefusal(obj => obj["artifacts"]![0]!["writeStatus"] = "written", false);
        CheckRefusal(obj => obj["artifacts"]![0]!["installation"] = "established", false);
        CheckRefusal(obj => obj["artifacts"]![0]!["verification"] = "succeeded", false);
        CheckRefusal(obj => obj["artifacts"]![0]!["sensitivity"] = "public", false);
        CheckRefusal(obj => obj["artifacts"]![0]!["digest"] = new string('0', 64), false);
        CheckRefusal(obj => obj["artifacts"] = new JsonArray(), false);
        JsonObject sourceLocation = new()
        {
            ["schemaPath"] = "/configuration", ["documentIndex"] = 0,
            ["startLine"] = 1, ["startColumn"] = 2, ["endLine"] = 1, ["endColumn"] = 3
        };
        JsonObject correctionLocation = new()
        {
            ["inputRevision"] = 0, ["fieldId"] = "service.name", ["segments"] = new JsonArray(
                new JsonObject { ["kind"] = "property", ["propertyName"] = "declared" },
                new JsonObject { ["kind"] = "arrayRow", ["index"] = 0 },
                new JsonObject { ["kind"] = "mapEntry", ["index"] = 4095 })
        };
        CheckRefusal(obj => obj["diagnostics"]![0]!["source"] = sourceLocation.DeepClone(), true);
        CheckRefusal(obj => obj["diagnostics"]![0]!["correction"] = correctionLocation.DeepClone(), true);
        foreach (string name in new[] { "schemaPath", "documentIndex", "startLine", "startColumn", "endLine", "endColumn" })
            CheckRefusal(obj =>
            {
                JsonObject source = sourceLocation.DeepClone().AsObject();
                source.Remove(name);
                obj["diagnostics"]![0]!["source"] = source;
            }, false);
        foreach (Action<JsonObject> mutation in new Action<JsonObject>[]
        {
            source => source["schemaPath"] = "/bad~2",
            source => source["documentIndex"] = -1,
            source => source["endColumn"] = 1,
            source => source["value"] = "private"
        })
            CheckRefusal(obj =>
            {
                JsonObject source = sourceLocation.DeepClone().AsObject();
                mutation(source);
                obj["diagnostics"]![0]!["source"] = source;
            }, false);
        foreach (Action<JsonObject> mutation in new Action<JsonObject>[]
        {
            correction => correction.Remove("inputRevision"),
            correction => correction["inputRevision"] = -1,
            correction => correction["fieldId"] = "/private",
            correction => correction["segments"]![0]!["index"] = 0,
            correction => correction["segments"]![1]!["propertyName"] = "private",
            correction => correction["segments"]![2]!["index"] = 4096,
            correction => correction["segments"]![2]!["kind"] = "map-entry",
            correction => correction["segments"]![2]!["key"] = "private",
            correction => correction["segments"] = new JsonArray(Enumerable.Range(0, 33)
                .Select(_ => (JsonNode?)new JsonObject { ["kind"] = "arrayRow", ["index"] = 0 }).ToArray())
        })
            CheckRefusal(obj =>
            {
                JsonObject correction = correctionLocation.DeepClone().AsObject();
                mutation(correction);
                obj["diagnostics"]![0]!["correction"] = correction;
            }, false);
        CheckRefusal(obj =>
        {
            obj["diagnostics"]![0]!["source"] = sourceLocation.DeepClone();
            obj["diagnostics"]![0]!["correction"] = correctionLocation.DeepClone();
        }, false);
        JsonObject comparison = JsonNode.Parse(good)!.AsObject();
        comparison["invocation"]!["operation"] = "compare";
        comparison["status"] = "validation";
        comparison["exitCategory"] = "validation";
        comparison["exitCode"] = 2;
        comparison["preparation"] = "not-evaluated";
        comparison["readiness"]!["state"] = "unassessed";
        comparison["artifacts"] = new JsonArray();
        comparison["assessment"] = new JsonObject
        {
            ["definitionDigest"] = new string('a', 64), ["artifactId"] = "config",
            ["compatibility"] = "changes-required", ["coverage"] = "complete", ["inventory"] = "complete",
            ["autofill"] = "ineligible", ["retainedCount"] = 3000, ["total"] = 3000
        };
        comparison["detail"] = new JsonObject
        {
            ["kind"] = "findings", ["offset"] = 2999, ["limit"] = 32, ["retainedCount"] = 3000,
            ["total"] = 3000, ["returnedCount"] = 1, ["nextOffset"] = null, ["terminal"] = true,
            ["digest"] = comparison["detailDigest"]!.DeepClone(), ["diagnostics"] = new JsonArray(), ["coverage"] = new JsonArray(),
            ["findings"] = new JsonArray(new JsonObject
            {
                ["classification"] = "unrecognized-input", ["code"] = "input-unrecognized",
                ["source"] = new JsonObject { ["ordinals"] = new JsonArray(2999) }
            })
        };
        int assessmentAccepted = 0;
        void CheckComparison(Action<JsonObject>? mutate, bool valid)
        {
            JsonObject candidate = comparison.DeepClone().AsObject();
            mutate?.Invoke(candidate);
            bool accepted = true;
            try
            {
                using JsonDocument document = Envelope((2, Encoding.UTF8.GetBytes(candidate.ToJsonString() + "\n"), []),
                    schema, "artifact", "compare", 2);
            }
            catch (Exception exception) when (IsInputError(exception)) { accepted = false; }
            Require(accepted == valid);
            if (valid) assessmentAccepted++; else rejected++;
        }
        CheckComparison(null, true);
        CheckComparison(obj =>
        {
            obj["assessment"]!["inventory"] = "stopped";
            obj["assessment"]!["coverage"] = "incomplete";
            obj["assessment"]!.AsObject().Remove("total");
            obj["detail"]!.AsObject().Remove("total");
        }, true);
        foreach (Action<JsonObject> mutate in new Action<JsonObject>[]
        {
            obj => obj["completion"] = "failed",
            obj => obj["preparation"] = "ready",
            obj => obj["assessment"]!["compatibility"] = "compatible",
            obj => obj["assessment"]!["total"] = 4097,
            obj => obj["assessment"]!["inventory"] = "stopped",
            obj => obj["assessment"]!["value"] = "private",
            obj => obj["detail"]!["offset"] = 4097,
            obj => obj["detail"]!["returnedCount"] = 32,
            obj => obj["detail"]!["nextOffset"] = 3000,
            obj => obj["detail"]!["terminal"] = false,
            obj => obj["detail"]!["digest"] = new string('b', 64),
            obj => obj["detail"]!["findings"]![0]!["source"]!["privateKey"] = "private",
            obj => obj["detail"]!["findings"]![0]!["correction"] = correctionLocation.DeepClone(),
            obj => obj["detail"]!["findings"] = new JsonArray()
        }) CheckComparison(mutate, false);
        Console.WriteLine($"CLI_PACKAGE_SELF_TEST passed accepted={4 + assessmentAccepted} rejected={rejected}");
        return 0;
    }

    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static void Require(bool condition)
    {
        if (!condition) throw new InvalidDataException();
    }
    private static bool IsInputError(Exception error) => error is InvalidDataException or IOException or JsonException or InvalidOperationException
        or ArgumentException or KeyNotFoundException or OperationCanceledException or TimeoutException
        or UnauthorizedAccessException or System.ComponentModel.Win32Exception;
}
