using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Configurator.Core.Definitions;
using Configurator.Core.Definitions.Authoring;

namespace Configurator.Cli.Tests;

public sealed class DefinitionWorkflowInspectionTests
{
    private static string Graph(int count = 2)
    {
        JsonObject source = JsonNode.Parse(CliFixture.Definition)!.AsObject();
        source["configuration"] = JsonNode.Parse("""
            {"type":"object","additionalProperties":false,"required":["task/~"],
             "x-configurator":{"sensitivity":"public"},"properties":{
               "task/~":{"type":"string","enum":["settings","identity"],
                 "x-configurator":{"sensitivity":"public","initialValue":"settings"}}}}
            """);
        source["presentation"] = JsonNode.Parse("""
            {"defaultJourney":"configure","sections":[{"id":"output","title":"Output","pointers":["/task~1~0"]}],
             "journeys":[{"id":"configure","title":"Configure","sections":["output"]}],
             "outputSelector":{"pointer":"/task~1~0","options":[
               {"value":"settings","title":"label-sentinel","artifactIds":["settings"],
                "nextActions":[{"targetOption":"identity","title":"action-sentinel","priority":"recommended",
                  "proposedWhen":{"op":"equals","reference":{"scope":"root","path":"/task~1~0"},"value":"settings"}}]},
               {"value":"identity","title":"identity-sentinel","artifactIds":["identity"]}]}}
            """);
        var outputs = new JsonObject();
        foreach (string option in new[] { "settings", "identity" })
            outputs[option + ".json"] = new JsonObject
            {
                ["id"] = option,
                ["format"] = "json",
                ["projection"] = new JsonObject
                {
                    ["op"] = "literal",
                    ["value"] = new JsonObject { ["kind"] = option },
                    ["when"] = new JsonObject
                    {
                        ["op"] = "equals",
                        ["value"] = option,
                        ["reference"] = new JsonObject { ["scope"] = "root", ["path"] = "/task~1~0" }
                    }
                }
            };
        source["outputs"] = outputs;
        for (int index = 2; index < count; index++)
        {
            string option = "option-" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
            source["configuration"]!["properties"]!["task/~"]!["enum"]!.AsArray().Add(option);
            source["presentation"]!["outputSelector"]!["options"]!.AsArray().Add(new JsonObject
            {
                ["value"] = option,
                ["title"] = "large-graph-label",
                ["artifactIds"] = new JsonArray(option)
            });
            JsonObject output = outputs["settings.json"]!.DeepClone().AsObject();
            output["id"] = option;
            output["projection"]!["when"]!["value"] = option;
            outputs[option + ".json"] = output;
        }
        return source.ToJsonString();
    }

    [Test]
    public async Task GraphPagesContainEveryStructuralItemWithoutAuthoredText()
    {
        string source = Graph();
        // Establish admission independently so parser rejection cannot masquerade as paging assurance.
        Definition definition = DefinitionAuthoringCompiler.Compile(
            DefinitionAuthoringCodec.ParseJson(Encoding.UTF8.GetBytes(source)));
        await Assert.That(definition.Presentation!.OutputSelector!.Options.Count).IsEqualTo(2);
        var io = new CliFixture { AuthoringFiles = new InspectionFiles(source) };
        var ids = new List<string>();
        string? digest = null;
        int offset = 0;
        do
        {
            string[] continuation = digest is null ? [] : ["--expected-detail-digest", digest];
            await Assert.That(await io.RunAsync(["definition", "inspect", "--machine", "--limit", "1",
                "--offset", offset.ToString(System.Globalization.CultureInfo.InvariantCulture), .. continuation]))
                .IsEqualTo(CliExitCode.Success);
            using JsonDocument document = JsonDocument.Parse(io.StandardOutput);
            await Assert.That(CliMachineContractVerifier.Validate(io.StandardOutput)).IsEmpty();
            JsonElement page = document.RootElement.GetProperty("workflow");
            digest ??= page.GetProperty("digest").GetString();
            await Assert.That(page.GetProperty("digest").GetString()).IsEqualTo(digest);
            await Assert.That(page.GetProperty("total").GetInt32()).IsEqualTo(3);
            foreach (JsonElement item in page.GetProperty("items").EnumerateArray())
            {
                ids.Add(item.GetProperty("id").GetString()!);
                if (item.GetProperty("kind").GetString() == "next-action")
                {
                    await Assert.That(item.GetProperty("targetOption").GetString()).IsEqualTo("identity");
                    await Assert.That(item.GetProperty("priority").GetString()).IsEqualTo("recommended");
                    await Assert.That(item.GetProperty("conditional").GetBoolean()).IsTrue();
                }
            }
            string text = Encoding.UTF8.GetString(io.StandardOutput);
            foreach (string sentinel in new[] { "label-sentinel", "action-sentinel", "identity-sentinel", "private-answer-marker" })
                await Assert.That(text).DoesNotContain(sentinel);
            await Assert.That(io.StandardOutput.Length <= 65_536).IsTrue();
            if (page.GetProperty("nextOffset").ValueKind == JsonValueKind.Null) break;
            offset = page.GetProperty("nextOffset").GetInt32();
        } while (offset < 4);
        await Assert.That(ids.Count).IsEqualTo(3);
        await Assert.That(ids.Distinct(StringComparer.Ordinal).Count()).IsEqualTo(3);
        await Assert.That(ids.Contains("/presentation/outputSelector/options/0")).IsTrue();
        await Assert.That(ids.Contains("/presentation/outputSelector/options/0/nextActions/0")).IsTrue();
        await Assert.That(ids.Contains("/presentation/outputSelector/options/1")).IsTrue();
        await Assert.That(await io.RunAsync("definition", "inspect", "--machine", "--offset", "1",
            "--expected-detail-digest", new string('0', 64))).IsEqualTo(CliExitCode.Data);
    }

    [Test]
    public async Task LargeInventoryShrinksPagesToEnvelopeBudgetWithoutLosingItems()
    {
        string source = Graph(40);
        Definition definition = DefinitionAuthoringCompiler.Compile(
            DefinitionAuthoringCodec.ParseJson(Encoding.UTF8.GetBytes(source)));
        await Assert.That(definition.Presentation!.OutputSelector!.Options.Count).IsEqualTo(40);
        var files = new InspectionFiles(source);
        var io = new CliFixture();
        int offset = 0;
        string? digest = null;
        var ids = new HashSet<string>(StringComparer.Ordinal);
        while (true)
        {
            string[] continuation = digest is null ? [] : ["--expected-detail-digest", digest];
            CliExitCode exit = await new CliApplication().RunAsync(new(
                ["definition", "inspect", "--machine", "--limit", "32", "--offset",
                    offset.ToString(System.Globalization.CultureInfo.InvariantCulture), .. continuation],
                CliMode.Machine, new(io, io, io, files, 2048, 4 * 1024 * 1024)));
            await Assert.That(exit).IsEqualTo(CliExitCode.Success);
            await Assert.That(io.StandardOutput.Length <= 2048).IsTrue();
            await Assert.That(CliMachineContractVerifier.Validate(io.StandardOutput)).IsEmpty();
            using JsonDocument document = JsonDocument.Parse(io.StandardOutput);
            JsonElement page = document.RootElement.GetProperty("workflow");
            digest ??= page.GetProperty("digest").GetString();
            await Assert.That(page.GetProperty("total").GetInt32()).IsEqualTo(41);
            foreach (JsonElement item in page.GetProperty("items").EnumerateArray())
                await Assert.That(ids.Add(item.GetProperty("id").GetString()!)).IsTrue();
            if (page.GetProperty("nextOffset").ValueKind == JsonValueKind.Null) break;
            int next = page.GetProperty("nextOffset").GetInt32();
            await Assert.That(next > offset).IsTrue();
            offset = next;
        }
        await Assert.That(ids.Count).IsEqualTo(41);
    }

    [Test]
    public async Task OversizedSingleItemFailsExplicitlyRatherThanDroppingIt()
    {
        JsonObject source = JsonNode.Parse(Graph())!.AsObject();
        string large = new('x', 12000);
        source["configuration"]!["properties"]!["task/~"]!["enum"]![0] = large;
        source["configuration"]!["properties"]!["task/~"]!["x-configurator"]!["initialValue"] = large;
        source["presentation"]!["outputSelector"]!["options"]![0]!["value"] = large;
        source["outputs"]!["settings.json"]!["projection"]!["when"]!["value"] = large;
        DefinitionAuthoringCompiler.Compile(DefinitionAuthoringCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString())));
        var io = new CliFixture { AuthoringFiles = new InspectionFiles(source.ToJsonString()) };
        await Assert.That(await io.RunAsync("definition", "inspect", "--machine")).IsEqualTo(CliExitCode.Data);
        using JsonDocument document = JsonDocument.Parse(io.StandardOutput);
        await Assert.That(document.RootElement.GetProperty("diagnostics")[0].GetProperty("code").GetString())
            .IsEqualTo("workflow-item-unencodable");
        await Assert.That(document.RootElement.TryGetProperty("workflow", out _)).IsFalse();
    }

    [Test]
    public async Task SuggestedLinksExposeEscapedSchemaPointersWithoutInitialValues()
    {
        JsonObject source = JsonNode.Parse(Graph())!.AsObject();
        JsonObject properties = source["configuration"]!["properties"]!.AsObject();
        properties["source/~"] = JsonNode.Parse("""
            {"type":"string","x-configurator":{"sensitivity":"public","initialValue":"initial-value-sentinel"}}
            """);
        properties["target"] = JsonNode.Parse("""
            {"type":"string","x-configurator":{"sensitivity":"public","suggestedFrom":{"pointer":"/source~1~0"}}}
            """);
        Definition definition = DefinitionAuthoringCompiler.Compile(
            DefinitionAuthoringCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString())));
        await Assert.That(definition.Configuration.Properties["target"].PresentationMetadata.SuggestedSource is not null).IsTrue();
        var io = new CliFixture { AuthoringFiles = new InspectionFiles(source.ToJsonString()) };
        await Assert.That(await io.RunAsync("definition", "inspect", "--machine")).IsEqualTo(CliExitCode.Success);
        await Assert.That(CliMachineContractVerifier.Validate(io.StandardOutput)).IsEmpty();
        using JsonDocument document = JsonDocument.Parse(io.StandardOutput);
        JsonElement page = document.RootElement.GetProperty("workflow");
        await Assert.That(page.GetProperty("total").GetInt32()).IsEqualTo(4);
        await Assert.That(page.GetProperty("suggestionCount").GetInt32()).IsEqualTo(1);
        JsonElement link = page.GetProperty("items").EnumerateArray().Single(item => item.GetProperty("kind").GetString() == "suggestion");
        await Assert.That(link.GetProperty("sourcePointer").GetString()).IsEqualTo("/configuration/properties/source~1~0");
        await Assert.That(link.GetProperty("targetPointer").GetString()).IsEqualTo("/configuration/properties/target");
        await Assert.That(Encoding.UTF8.GetString(io.StandardOutput)).DoesNotContain("initial-value-sentinel");
    }

    [Test]
    public async Task HumanInspectionShowsTheSameBoundedStructuralInventory()
    {
        var io = new CliFixture { AuthoringFiles = new InspectionFiles(CliFixture.Definition) };
        await Assert.That(await io.RunAsync("definition", "inspect")).IsEqualTo(CliExitCode.Success);
        await Assert.That(Encoding.UTF8.GetString(io.StandardOutput)).Contains("workflow");
        await Assert.That(io.MaximumRead).IsEqualTo(0);
    }

    [Test]
    public async Task SameNamedIndependentFieldsRemainValidWithoutInferredLinks()
    {
        JsonObject source = JsonNode.Parse(CliFixture.Definition)!.AsObject();
        JsonObject properties = source["configuration"]!["properties"]!.AsObject();
        foreach (string group in new[] { "first", "second" })
            properties[group] = JsonNode.Parse("""
                {"type":"object","additionalProperties":false,"x-configurator":{"sensitivity":"public"},
                 "properties":{"name":{"type":"string","x-configurator":{"sensitivity":"public"}}}}
                """);
        using var directory = new DefinitionTestDirectory();
        string path = directory.Path("configurator.json");
        File.WriteAllText(path, source.ToJsonString());
        var io = new CliFixture { AuthoringFiles = new PublicDefinitionFiles() };
        await Assert.That(await io.RunAsync("definition", "lint", "--source", path, "--quality", "--machine"))
            .IsEqualTo(CliExitCode.Success);
        await Assert.That(await io.RunAsync("definition", "inspect", "--source", path, "--machine"))
            .IsEqualTo(CliExitCode.Success);
        using JsonDocument document = JsonDocument.Parse(io.StandardOutput);
        await Assert.That(document.RootElement.GetProperty("workflow").GetProperty("suggestionCount").GetInt32())
            .IsEqualTo(0);
    }

    [Test]
    public async Task NoninitialWorkflowPageRequiresDigestAndActualInventoryRange()
    {
        var io = new CliFixture { AuthoringFiles = new InspectionFiles(CliFixture.Definition) };
        await Assert.That(await io.RunAsync("definition", "inspect", "--machine", "--offset", "1"))
            .IsEqualTo(CliExitCode.Data);
        using JsonDocument document = JsonDocument.Parse(io.StandardOutput);
        await Assert.That(document.RootElement.GetProperty("diagnostics")[0].GetProperty("code").GetString())
            .IsEqualTo("workflow-digest-mismatch");
    }

    [Test]
    public async Task RegisteredInspectionReadsOnlyPublicSourceAndReturnsEmptyInventory()
    {
        var files = new InspectionFiles(CliFixture.Definition);
        var io = new CliFixture { AuthoringFiles = files };

        await Assert.That(await io.RunAsync("definition", "inspect", "--source", "source.json", "--machine"))
            .IsEqualTo(CliExitCode.Success);
        using JsonDocument document = JsonDocument.Parse(io.StandardOutput);
        JsonElement workflow = document.RootElement.GetProperty("workflow");
        await Assert.That(workflow.GetProperty("total").GetInt32()).IsEqualTo(0);
        await Assert.That(workflow.GetProperty("limit").GetInt32()).IsEqualTo(16);
        await Assert.That(workflow.GetProperty("items").GetArrayLength()).IsEqualTo(0);
        await Assert.That(workflow.GetProperty("nextOffset").ValueKind).IsEqualTo(JsonValueKind.Null);
        await Assert.That(io.MaximumRead).IsEqualTo(0);
        await Assert.That(files.Reads).IsEqualTo(1);
        await Assert.That(Encoding.UTF8.GetString(io.StandardOutput)).DoesNotContain("private-answer-marker");
    }

    [Test]
    public async Task InspectionIsDiscoverableThroughRegisteredHelp()
    {
        var io = new CliFixture();
        await Assert.That(await io.RunAsync("definition", "--help", "--machine")).IsEqualTo(CliExitCode.Success);
        using JsonDocument document = JsonDocument.Parse(io.StandardOutput);
        await Assert.That(document.RootElement.GetProperty("help").GetProperty("commands").EnumerateArray()
            .Select(item => item.GetString()).Contains("inspect")).IsTrue();
    }

    [Test]
    [Arguments("--answers", "answers.json")]
    [Arguments("--output", "output.json")]
    [Arguments("--limit", "33")]
    [Arguments("--offset", "-1")]
    public async Task InspectionRejectsPrivateInputsWritesAndInvalidPaging(string option, string value)
    {
        var files = new InspectionFiles(CliFixture.Definition);
        var io = new CliFixture { AuthoringFiles = files };
        await Assert.That(await io.RunAsync("definition", "inspect", "--machine", option, value))
            .IsEqualTo(CliExitCode.Usage);
        await Assert.That(files.Reads).IsEqualTo(0);
    }

    [Test]
    public async Task CancelledInspectionDoesNotReadPublicOrPrivateInput()
    {
        var files = new InspectionFiles(CliFixture.Definition);
        var io = new CliFixture();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        CliExitCode exit = await new CliApplication().RunAsync(new(
            ["definition", "inspect", "--machine"], CliMode.Machine,
            new(io, io, io, files, 65_536, 4 * 1024 * 1024)), cancellation.Token);
        await Assert.That(exit).IsEqualTo(CliExitCode.Cancelled);
        await Assert.That(files.Reads).IsEqualTo(0);
        await Assert.That(io.MaximumRead).IsEqualTo(0);
    }

    private sealed class InspectionFiles(string source) : ICliAuthoringFiles
    {
        internal int Reads { get; private set; }
        public ValueTask<string> DiscoverSourceAsync(string root, string? path, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult("source.json");
        }
        public async ValueTask<CliInputBuffer> ReadAsync(string path, int maximumBytes, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (path != "source.json") throw new InvalidOperationException("unexpected-read");
            Reads++;
            byte[] bytes = await ValueTask.FromResult(Encoding.UTF8.GetBytes(source));
            if (bytes.Length > maximumBytes) throw new IOException("input-bound");
            return new CliInputBuffer(bytes);
        }
        public ValueTask<bool> ExistsAsync(string path, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("unexpected-anchor-access");
        public ICliPublicFilePreparation PrepareCreate(string path, ReadOnlyMemory<byte> bytes,
            int maximumBytes, bool acceptIdentical) => throw new InvalidOperationException("unexpected-write");
    }
}
