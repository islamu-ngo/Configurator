using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Configurator.Core.Definitions;

namespace Configurator.Cli.Tests;

public sealed class SetupCliDefinitionTests
{
    [Test]
    public async Task InitUsesYamlPersistsIdentityAndBuildsImmutableCanonicalRelease()
    {
        using var files = new DefinitionTestDirectory();
        var io = new CliFixture();
        await Assert.That(Initialize(io, files)).IsEqualTo(SetupCliExitCode.Success);
        string source = files.Path("configurator.yaml");
        byte[] initial = File.ReadAllBytes(source);
        await Assert.That(Encoding.UTF8.GetString(initial)).Contains("urn:islamu:configurator:authoring:v1");
        await Assert.That(Initialize(io, files)).IsEqualTo(SetupCliExitCode.Data);
        await Assert.That(File.ReadAllBytes(source)).IsEquivalentTo(initial);
        await Assert.That(io.Run("definition", "check", "--root", files.Root, "--machine")).IsEqualTo(SetupCliExitCode.Success);
        await Assert.That(SetupCliMachineContractVerifier.Validate(io.StandardOutput)).IsEmpty();
        using (JsonDocument receipt = JsonDocument.Parse(io.StandardOutput))
        {
            await Assert.That(receipt.RootElement.GetProperty("authoringContractVersion").GetInt32()).IsEqualTo(1);
            await Assert.That(receipt.RootElement.GetProperty("runtimeSchemaVersion").GetInt32()).IsEqualTo(1);
        }
        await Assert.That(io.Run("definition", "build", "--root", files.Root, "--machine")).IsEqualTo(SetupCliExitCode.Success);
        string release = files.Path("dist/configurator/revisions/1.json");
        SetupDefinition definition = SetupDefinitionCodec.ParseJson(File.ReadAllBytes(release));
        await Assert.That(definition.Revision).IsEqualTo(1);
        await Assert.That(io.Run("definition", "build", "--root", files.Root, "--machine")).IsEqualTo(SetupCliExitCode.Success);
        using JsonDocument repeated = JsonDocument.Parse(io.StandardOutput);
        await Assert.That(repeated.RootElement.GetProperty("artifacts")[0].GetProperty("writeStatus").GetString()).IsEqualTo("none");
    }

    [Test]
    public async Task ChangedContentRequiresRevisionAndPreservesAllOtherSourceText()
    {
        using var files = new DefinitionTestDirectory();
        var io = new CliFixture();
        await Assert.That(Initialize(io, files)).IsEqualTo(SetupCliExitCode.Success);
        await Assert.That(io.Run("definition", "build", "--root", files.Root, "--machine")).IsEqualTo(SetupCliExitCode.Success);
        string source = files.Path("configurator.yaml");
        string original = File.ReadAllText(source);
        string changed = "# café Δ retain this comment\r\n" + original.Replace("Example Service", "Updated Service", StringComparison.Ordinal).Replace("\n", "\r\n", StringComparison.Ordinal);
        File.WriteAllBytes(source, [0xef, 0xbb, 0xbf, .. Encoding.UTF8.GetBytes(changed)]);
        await Assert.That(io.Run("definition", "check", "--root", files.Root, "--machine")).IsEqualTo(SetupCliExitCode.Data);
        await Assert.That(io.Run("definition", "build", "--root", files.Root, "--output", files.Path("escape.json"), "--machine")).IsEqualTo(SetupCliExitCode.Data);
        await Assert.That(File.Exists(files.Path("escape.json"))).IsFalse();
        await Assert.That(io.Run("definition", "revise", "--root", files.Root, "--machine")).IsEqualTo(SetupCliExitCode.Success);
        byte[] expected = [0xef, 0xbb, 0xbf, .. Encoding.UTF8.GetBytes(changed.Replace("\"revision\": 1", "\"revision\": 2", StringComparison.Ordinal))];
        await Assert.That(File.ReadAllBytes(source)).IsEquivalentTo(expected);
        await Assert.That(io.Run("definition", "build", "--root", files.Root, "--machine")).IsEqualTo(SetupCliExitCode.Success);
        await Assert.That(SetupDefinitionCodec.ParseJson(File.ReadAllBytes(files.Path("dist/configurator/revisions/2.json"))).Revision).IsEqualTo(2);
        await Assert.That(io.Run("definition", "revise", "--root", files.Root, "--machine")).IsEqualTo(SetupCliExitCode.Success);
        await Assert.That(File.ReadAllBytes(source)).IsEquivalentTo(expected);
    }

    [Test]
    public async Task EnvScaffoldingCopiesOnlyKeysAndDiagnosticsNeverExposeValuesOrPaths()
    {
        using var files = new DefinitionTestDirectory();
        string example = files.Path(".env.example");
        File.WriteAllText(example, "SERVICE_TOKEN=synthetic-private-value-sentinel\nSERVICE_NAME=synthetic-other-value\n");
        var io = new CliFixture();
        await Assert.That(Initialize(io, files, "--from-env-example", example)).IsEqualTo(SetupCliExitCode.Success);
        string source = File.ReadAllText(files.Path("configurator.yaml"));
        await Assert.That(source).Contains("SERVICE_TOKEN");
        await Assert.That(source.Contains("synthetic-private-value-sentinel", StringComparison.Ordinal)).IsFalse();
        await Assert.That(source.Contains("synthetic-other-value", StringComparison.Ordinal)).IsFalse();
        await Assert.That(source.Contains("default", StringComparison.Ordinal)).IsFalse();
        await Assert.That(SetupCliMachineContractVerifier.Validate(io.StandardOutput)).IsEmpty();
        await Assert.That(Initialize(io, files, "--from-env-example", example)).IsEqualTo(SetupCliExitCode.Data);
        string diagnostics = Encoding.UTF8.GetString(io.StandardOutput);
        await Assert.That(diagnostics.Contains(files.Root, StringComparison.Ordinal)).IsFalse();
        await Assert.That(diagnostics.Contains("synthetic-private-value-sentinel", StringComparison.Ordinal)).IsFalse();
    }

    [Test]
    public async Task ForkHasNewFamilyAndDoesNotChangeOriginal()
    {
        using var files = new DefinitionTestDirectory();
        var io = new CliFixture();
        await Assert.That(Initialize(io, files, "--format", "json")).IsEqualTo(SetupCliExitCode.Success);
        string original = files.Path("configurator.json");
        byte[] bytes = File.ReadAllBytes(original);
        string fork = files.Path("fork/configurator.json");
        await Assert.That(io.Run("definition", "init", "--fork-from", original, "--output", fork, "--machine")).IsEqualTo(SetupCliExitCode.Success);
        await Assert.That(File.ReadAllBytes(original)).IsEquivalentTo(bytes);
        await Assert.That(SetupDefinitionSourceCodec.ParseJson(File.ReadAllBytes(fork)).FamilyId)
            .IsNotEqualTo(SetupDefinitionSourceCodec.ParseJson(bytes).FamilyId);
    }

    [Test]
    public async Task SchemaImportIsStrictAndAcknowledgmentReportsBoundedDifferences()
    {
        using var files = new DefinitionTestDirectory();
        string schema = files.Path("schema.json");
        File.WriteAllText(schema, """{"type":"object","additionalProperties":false,"properties":{"port":{"type":"integer","minimum":1,"maximum":65535}}}""");
        var io = new CliFixture();
        await Assert.That(Initialize(io, files, "--from-schema", schema)).IsEqualTo(SetupCliExitCode.Data);
        await Assert.That(File.Exists(files.Path("configurator.yaml"))).IsFalse();
        await Assert.That(Encoding.UTF8.GetString(io.StandardOutput)).Contains("schema-numeric-bounded-domain");
        await Assert.That(Initialize(io, files, "--from-schema", schema, "--accept-bounded-semantics")).IsEqualTo(SetupCliExitCode.Success);
        await Assert.That(Encoding.UTF8.GetString(io.StandardOutput)).Contains("schema-numeric-bounded-domain");
        await Assert.That(SetupCliMachineContractVerifier.Validate(io.StandardOutput)).IsEmpty();
    }

    [Test]
    public async Task SchemaReferencesCannotReadOutsideRootOrThroughSymlinks()
    {
        using var files = new DefinitionTestDirectory();
        string schema = files.Path("schema.json");
        File.WriteAllText(schema, """{"type":"object","additionalProperties":false,"properties":{"x":{"$ref":"../outside.json"}}}""");
        var io = new CliFixture();
        await Assert.That(Initialize(io, files, "--from-schema", schema, "--accept-bounded-semantics")).IsEqualTo(SetupCliExitCode.Data);
        File.WriteAllText(files.Path("target.json"), """{"type":"boolean"}""");
        File.CreateSymbolicLink(files.Path("reference.json"), files.Path("target.json"));
        File.WriteAllText(schema, """{"type":"object","additionalProperties":false,"properties":{"x":{"$ref":"reference.json"}}}""");
        await Assert.That(Initialize(io, files, "--from-schema", schema)).IsEqualTo(SetupCliExitCode.Data);
        await Assert.That(Encoding.UTF8.GetString(io.StandardOutput).Contains(files.Root, StringComparison.Ordinal)).IsFalse();
    }

    [Test]
    public async Task ExplicitAnchorsBindHistoryAndMissingOrMismatchedAnchorsReject()
    {
        using var files = new DefinitionTestDirectory();
        var io = new CliFixture();
        await Assert.That(Initialize(io, files, "--format", "json")).IsEqualTo(SetupCliExitCode.Success);
        string source = files.Path("configurator.json");
        byte[] original = File.ReadAllBytes(source);
        await Assert.That(io.Run("definition", "revise", "--source", source, "--machine")).IsEqualTo(SetupCliExitCode.Data);
        await Assert.That(File.ReadAllBytes(source)).IsEquivalentTo(original);
        await Assert.That(io.Run("definition", "check", "--source", source, "--against", files.Path("missing.json"), "--machine"))
            .IsEqualTo(SetupCliExitCode.Data);
        string anchor = files.Path("canonical.json");
        await Assert.That(io.Run("definition", "build", "--source", source, "--output", anchor, "--machine")).IsEqualTo(SetupCliExitCode.Success);
        JsonObject changed = JsonNode.Parse(original)!.AsObject();
        changed["project"]!["name"] = "Changed Service";
        File.WriteAllText(source, changed.ToJsonString());
        await Assert.That(io.Run("definition", "check", "--source", source, "--against", anchor, "--machine"))
            .IsEqualTo(SetupCliExitCode.Data);
        await Assert.That(io.Run("definition", "revise", "--source", source, "--against", anchor, "--machine"))
            .IsEqualTo(SetupCliExitCode.Success);
        await Assert.That(io.Run("definition", "revise", "--source", source, "--against", anchor, "--machine"))
            .IsEqualTo(SetupCliExitCode.Data);
        await Assert.That(SetupDefinitionSourceCodec.ParseJson(File.ReadAllBytes(source)).Revision).IsEqualTo(2);
    }

    [Test]
    public async Task RevisionOverflowRejectsWithoutChangingSource()
    {
        using var files = new DefinitionTestDirectory();
        var io = new CliFixture();
        await Assert.That(Initialize(io, files, "--format", "json")).IsEqualTo(SetupCliExitCode.Success);
        string source = files.Path("configurator.json");
        JsonObject document = JsonNode.Parse(File.ReadAllBytes(source))!.AsObject();
        document["identity"]!["revision"] = long.MaxValue;
        byte[] priorBytes = Encoding.UTF8.GetBytes(document.ToJsonString());
        string anchor = files.Path("maximum.json");
        File.WriteAllBytes(anchor, SetupDefinitionSourceCodec.ParseJson(priorBytes).CanonicalBytes.ToArray());
        document["project"]!["name"] = "Changed Service";
        byte[] changed = Encoding.UTF8.GetBytes(document.ToJsonString());
        File.WriteAllBytes(source, changed);
        await Assert.That(io.Run("definition", "revise", "--source", source, "--against", anchor, "--machine")).IsEqualTo(SetupCliExitCode.Data);
        await Assert.That(Encoding.UTF8.GetString(io.StandardOutput)).Contains("definition-revision-overflow");
        await Assert.That(File.ReadAllBytes(source)).IsEquivalentTo(changed);
    }

    [Test]
    public async Task ExplicitAnchorsCannotReplaceKnownRevisionHistory()
    {
        using var files = new DefinitionTestDirectory();
        var io = new CliFixture();
        await Assert.That(Initialize(io, files, "--format", "json")).IsEqualTo(SetupCliExitCode.Success);
        await Assert.That(io.Run("definition", "build", "--root", files.Root, "--machine")).IsEqualTo(SetupCliExitCode.Success);
        string source = files.Path("configurator.json");
        string recorded = files.Path("dist/configurator/revisions/1.json");
        byte[] originalAnchor = File.ReadAllBytes(recorded);
        JsonObject edited = JsonNode.Parse(File.ReadAllBytes(source))!.AsObject();
        edited["project"]!["name"] = "Changed Service";
        byte[] changed = Encoding.UTF8.GetBytes(edited.ToJsonString());
        File.WriteAllBytes(source, changed);
        string substituted = files.Path("substituted.json");
        File.WriteAllBytes(substituted, SetupDefinitionSourceCodec.ParseJson(changed).CanonicalBytes.ToArray());
        await Assert.That(io.Run("definition", "check", "--root", files.Root, "--against", substituted, "--machine"))
            .IsEqualTo(SetupCliExitCode.Data);
        await Assert.That(io.Run("definition", "build", "--root", files.Root, "--against", substituted, "--output", files.Path("alternate.json"), "--machine"))
            .IsEqualTo(SetupCliExitCode.Data);
        await Assert.That(File.Exists(files.Path("alternate.json"))).IsFalse();
        await Assert.That(io.Run("definition", "revise", "--root", files.Root, "--against", substituted, "--machine"))
            .IsEqualTo(SetupCliExitCode.Data);
        await Assert.That(File.ReadAllBytes(recorded)).IsEquivalentTo(originalAnchor);
        await Assert.That(File.ReadAllBytes(source)).IsEquivalentTo(changed);
    }

    [Test]
    public async Task CheckBindsSanitizedDetailViewToCompiledDefinitionAndReportsPreparationOnlyAfterCompilation()
    {
        using var files = new DefinitionTestDirectory();
        var io = new CliFixture();
        await Assert.That(Initialize(io, files, "--format", "json")).IsEqualTo(SetupCliExitCode.Success);
        await Assert.That(io.Run("definition", "check", "--root", files.Root, "--machine")).IsEqualTo(SetupCliExitCode.Success);
        using JsonDocument original = JsonDocument.Parse(io.StandardOutput);
        await Assert.That(original.RootElement.GetProperty("preparation").GetString()).IsEqualTo("ready");
        string source = files.Path("configurator.json");
        JsonObject changed = JsonNode.Parse(File.ReadAllBytes(source))!.AsObject();
        changed["project"]!["name"] = "Changed Service";
        File.WriteAllText(source, changed.ToJsonString());
        await Assert.That(io.Run("definition", "check", "--root", files.Root, "--machine")).IsEqualTo(SetupCliExitCode.Success);
        using JsonDocument revised = JsonDocument.Parse(io.StandardOutput);
        await Assert.That(revised.RootElement.GetProperty("detailDigest").GetString())
            .IsNotEqualTo(original.RootElement.GetProperty("detailDigest").GetString());
        File.WriteAllText(source, "invalid");
        await Assert.That(io.Run("definition", "check", "--root", files.Root, "--machine")).IsEqualTo(SetupCliExitCode.Data);
        using JsonDocument invalid = JsonDocument.Parse(io.StandardOutput);
        await Assert.That(invalid.RootElement.GetProperty("preparation").ValueKind).IsEqualTo(JsonValueKind.Null);
        await Assert.That(File.Exists(files.Path("dist/configurator/revisions/1.json"))).IsFalse();
    }

    [Test]
    public async Task IdMapPreparationFailurePublishesNeitherOutput()
    {
        using var files = new DefinitionTestDirectory();
        var io = new CliFixture();
        await Assert.That(Initialize(io, files)).IsEqualTo(SetupCliExitCode.Success);
        File.WriteAllText(files.Path("not-a-directory"), "occupied");
        string definition = files.Path("canonical.json");
        string map = files.Path("not-a-directory/id-map.json");
        await Assert.That(io.Run("definition", "build", "--root", files.Root, "--output", definition,
            "--id-map", map, "--machine")).IsEqualTo(SetupCliExitCode.Io);
        await Assert.That(File.Exists(definition)).IsFalse();
        await Assert.That(File.Exists(map)).IsFalse();
        using JsonDocument receipt = JsonDocument.Parse(io.StandardOutput);
        JsonElement artifacts = receipt.RootElement.GetProperty("artifacts");
        await Assert.That(artifacts.GetArrayLength()).IsEqualTo(2);
        await Assert.That(artifacts[0].GetProperty("writeStatus").GetString()).IsEqualTo("not-attempted");
        await Assert.That(artifacts[1].GetProperty("writeStatus").GetString()).IsEqualTo("not-attempted");
    }

    [Test]
    public async Task IdMapCommitConflictPreservesWrittenDefinitionAndConflictReceipt()
    {
        using var files = new DefinitionTestDirectory();
        var io = new CliFixture();
        await Assert.That(Initialize(io, files)).IsEqualTo(SetupCliExitCode.Success);
        string definition = files.Path("canonical.json");
        string map = files.Path("id-map.json");
        File.WriteAllText(map, "editor-owned");
        await Assert.That(io.Run("definition", "build", "--root", files.Root, "--output", definition,
            "--id-map", map, "--machine")).IsEqualTo(SetupCliExitCode.Data);
        await Assert.That(SetupDefinitionCodec.ParseJson(File.ReadAllBytes(definition)).Revision).IsEqualTo(1);
        await Assert.That(File.ReadAllText(map)).IsEqualTo("editor-owned");
        using JsonDocument receipt = JsonDocument.Parse(io.StandardOutput);
        JsonElement artifacts = receipt.RootElement.GetProperty("artifacts");
        await Assert.That(artifacts.GetArrayLength()).IsEqualTo(2);
        await Assert.That(artifacts[0].GetProperty("writeStatus").GetString()).IsEqualTo("written");
        await Assert.That(artifacts[1].GetProperty("writeStatus").GetString()).IsEqualTo("conflict");
    }

    [Test]
    public async Task UntypedCommitFailurePreservesWrittenPrefixAndUncertainAttempt()
    {
        using var files = new DefinitionTestDirectory();
        var io = new CliFixture();
        await Assert.That(Initialize(io, files)).IsEqualTo(SetupCliExitCode.Success);
        string definition = files.Path("canonical.json");
        string map = files.Path("id-map.json");
        Directory.CreateDirectory(map);
        await Assert.That(io.Run("definition", "build", "--root", files.Root, "--output", definition,
            "--id-map", map, "--machine")).IsEqualTo(SetupCliExitCode.Io);
        await Assert.That(SetupDefinitionCodec.ParseJson(File.ReadAllBytes(definition)).Revision).IsEqualTo(1);
        await Assert.That(Directory.Exists(map)).IsTrue();
        using JsonDocument receipt = JsonDocument.Parse(io.StandardOutput);
        JsonElement artifacts = receipt.RootElement.GetProperty("artifacts");
        await Assert.That(artifacts.GetArrayLength()).IsEqualTo(2);
        await Assert.That(artifacts[0].GetProperty("writeStatus").GetString()).IsEqualTo("written");
        await Assert.That(artifacts[1].GetProperty("writeStatus").GetString()).IsEqualTo("unknown");
    }

    [Test]
    public async Task ImpossibleReceiptBudgetRejectsBeforePublicOutput()
    {
        using var files = new DefinitionTestDirectory();
        var io = new CliFixture();
        await Assert.That(Initialize(io, files)).IsEqualTo(SetupCliExitCode.Success);
        string definition = files.Path("canonical.json");
        string map = files.Path("id-map.json");
        string[] arguments = ["definition", "build", "--root", files.Root, "--output", definition,
            "--id-map", map, "--machine"];
        SetupCliExitCode exit = new SetupCliApplication().Run(new(arguments, SetupCliMode.Machine,
            new(io, io, io, 1, 4 * 1024 * 1024), new([])));
        await Assert.That(exit).IsEqualTo(SetupCliExitCode.Io);
        await Assert.That(File.Exists(definition)).IsFalse();
        await Assert.That(File.Exists(map)).IsFalse();
    }

    private static SetupCliExitCode Initialize(CliFixture io, DefinitionTestDirectory files, params string[] extra) =>
        io.Run(["definition", "init", "--root", files.Root, "--project-id", "example.service", "--name", "Example Service",
            "--application-version", "1.0.0", "--machine", .. extra]);
}
