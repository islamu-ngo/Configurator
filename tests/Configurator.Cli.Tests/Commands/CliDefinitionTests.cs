using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Configurator.Core.Definitions;

namespace Configurator.Cli.Tests;

public sealed class CliDefinitionTests
{
    [Test]
    public async Task CompilerFailuresRetainPublicSourceLocationsWithoutUnknownKeysOrValues()
    {
        using var files = new DefinitionTestDirectory();
        string path = files.Path("configurator.json");
        File.WriteAllText(path, CurrentSource.Replace("\"type\":\"string\"",
            "\"type\":\"string\",\"synthetic-private-key\":\"synthetic-private-value\"", StringComparison.Ordinal));
        var io = new CliFixture { AuthoringFiles = new PublicDefinitionFiles() };
        await Assert.That(await io.RunAsync("definition", "check", "--source", path, "--machine")).IsEqualTo(CliExitCode.Data);
        using JsonDocument output = JsonDocument.Parse(io.StandardOutput);
        JsonElement diagnostic = output.RootElement.GetProperty("diagnostics")[0];
        await Assert.That(diagnostic.GetProperty("code").GetString()).IsEqualTo("member-unsupported");
        await Assert.That(diagnostic.GetProperty("source").GetProperty("schemaPath").GetString())
            .IsEqualTo("/configuration/properties/value");
        await Assert.That(diagnostic.GetProperty("source").GetProperty("startLine").GetInt32() > 0).IsTrue();
        await Assert.That(Encoding.UTF8.GetString(io.StandardOutput)).DoesNotContain("synthetic-private");
        await Assert.That(Encoding.UTF8.GetString(io.StandardOutput)).DoesNotContain(files.Root);
    }

    [Test]
    [Arguments("authoring", "urn:islamu:configurator:authoring:v0.0.1")]
    [Arguments("runtime", "urn:islamu:configurator:setup-definition:v0.0.1")]
    [Arguments("answers", "urn:islamu:configurator:answers:v0.0.1")]
    [Arguments("command", "urn:islamu:configurator:cli:v0.0.1")]
    [Arguments("fixtures", "urn:islamu:configurator:fixtures:v0.0.1")]
    [Arguments("store-curation", "urn:islamu:configurator:store-curation:v0.0.1")]
    public async Task SchemaDiscoveryUsesOnlyBuiltInPublicMetadataWithoutFileAuthority(string kind, string id)
    {
        var io = new CliFixture();
        await Assert.That(await io.RunAsync("schema", "export", "--kind", kind, "--machine"))
            .IsEqualTo(CliExitCode.Success);
        using JsonDocument output = JsonDocument.Parse(io.StandardOutput);
        await Assert.That(output.RootElement.GetProperty("schemaVersion").GetString()).IsEqualTo("islamu-configurator-command/v0.0.1");
        JsonElement schema = output.RootElement.GetProperty("schema");
        await Assert.That(schema.GetProperty("kind").GetString()).IsEqualTo(kind);
        await Assert.That(schema.GetProperty("id").GetString()).IsEqualTo(id);
        if (schema.GetProperty("document").TryGetProperty("$id", out JsonElement documentId))
            await Assert.That(documentId.GetString()).IsEqualTo(id);
        await Assert.That(schema.GetProperty("authoringContractVersion").GetString()).IsEqualTo("0.0.1");
        await Assert.That(schema.GetProperty("runtimeSchemaVersion").GetString()).IsEqualTo("0.0.1");
        await Assert.That(schema.GetProperty("answerContractVersion").GetString()).IsEqualTo("0.0.1");
        await Assert.That(schema.GetProperty("capabilities").EnumerateArray().Any(item => item.GetString() == "predicate.item")).IsTrue();
    }

    [Test]
    public async Task LintWarningsRemainDistinctFromAdmissionAndUseBoundedDiagnosticContinuation()
    {
        using var files = new DefinitionTestDirectory();
        string path = files.Path("configurator.json");
        File.WriteAllText(path, CurrentSource);
        var io = new CliFixture { AuthoringFiles = new PublicDefinitionFiles() };
        await Assert.That(await io.RunAsync("definition", "lint", "--source", path, "--machine",
            "--detail", "diagnostics", "--limit", "1")).IsEqualTo(CliExitCode.Success);
        using JsonDocument output = JsonDocument.Parse(io.StandardOutput);
        await Assert.That(output.RootElement.GetProperty("diagnostics").EnumerateArray().Any(item =>
            item.GetProperty("code").GetString() == "default-is-descriptive" &&
            item.GetProperty("severity").GetString() == "warning")).IsTrue();
        await Assert.That(output.RootElement.GetProperty("detail").GetProperty("diagnostics").GetArrayLength()).IsEqualTo(1);
        await Assert.That(output.RootElement.GetProperty("diagnostics").EnumerateArray().Any(item =>
            item.GetProperty("severity").GetString() == "error")).IsFalse();
        await Assert.That(Encoding.UTF8.GetString(io.StandardOutput)).DoesNotContain("synthetic-public-default");
    }

    private const string CurrentSource = """
        {"$schema":"urn:islamu:configurator:authoring:v0.0.1",
         "identity":{"familyId":"d6788869-273c-4d44-b969-d8a54f6a9caf","revision":1},
         "project":{"id":"example","name":"Authoring"},"applicationVersions":["1"],
         "configuration":{"type":"object","additionalProperties":false,
          "x-configurator":{"sensitivity":"public"},"properties":{
           "value":{"type":"string","default":"synthetic-public-default","x-configurator":{"sensitivity":"public"}}}},
         "outputs":{"config.json":{"format":"json"}}}
        """;

    [Test]
    public async Task ChangedRevisionWithoutOutputNeverMutatesSource()
    {
        using var files = new DefinitionTestDirectory();
        var io = new CliFixture { AuthoringFiles = new PublicDefinitionFiles() };
        await Assert.That(await InitializeAsync(io, files)).IsEqualTo(CliExitCode.Success);
        await Assert.That(await io.RunAsync("definition", "build", "--root", files.Root, "--machine")).IsEqualTo(CliExitCode.Success);
        string source = files.Path("configurator.yaml");
        byte[] changed = Encoding.UTF8.GetBytes(File.ReadAllText(source).Replace("Example Service", "Changed Service", StringComparison.Ordinal));
        File.WriteAllBytes(source, changed);

        CliExitCode exit = await io.RunAsync("definition", "revise", "--root", files.Root, "--machine");

        await Assert.That(File.ReadAllBytes(source).AsSpan().SequenceEqual(changed)).IsTrue();
        await Assert.That(exit).IsEqualTo(CliExitCode.Usage);
        await Assert.That(Encoding.UTF8.GetString(io.StandardOutput)).Contains("output-required");
    }

    [Test]
    public async Task InitUsesYamlPersistsIdentityAndBuildsImmutableCanonicalRelease()
    {
        using var files = new DefinitionTestDirectory();
        var io = new CliFixture { AuthoringFiles = new PublicDefinitionFiles() };
        await Assert.That(await InitializeAsync(io, files)).IsEqualTo(CliExitCode.Success);
        string source = files.Path("configurator.yaml");
        byte[] initial = File.ReadAllBytes(source);
        await Assert.That(Encoding.UTF8.GetString(initial)).Contains("urn:islamu:configurator:authoring:v0.0.1");
        await Assert.That(await InitializeAsync(io, files)).IsEqualTo(CliExitCode.Data);
        await Assert.That(File.ReadAllBytes(source).AsSpan().SequenceEqual(initial)).IsTrue();
        await Assert.That(await io.RunAsync("definition", "check", "--root", files.Root, "--machine")).IsEqualTo(CliExitCode.Success);
        await Assert.That(CliMachineContractVerifier.Validate(io.StandardOutput)).IsEmpty();
        using (JsonDocument receipt = JsonDocument.Parse(io.StandardOutput))
        {
            await Assert.That(receipt.RootElement.GetProperty("authoringContractVersion").GetString()).IsEqualTo("0.0.1");
            await Assert.That(receipt.RootElement.GetProperty("runtimeSchemaVersion").GetString()).IsEqualTo("0.0.1");
        }
        await Assert.That(await io.RunAsync("definition", "build", "--root", files.Root, "--machine")).IsEqualTo(CliExitCode.Success);
        string release = files.Path("dist/configurator/revisions/1.json");
        Definition definition = DefinitionCodec.ParseJson(File.ReadAllBytes(release));
        await Assert.That(definition.Revision).IsEqualTo(1);
        await Assert.That(await io.RunAsync("definition", "build", "--root", files.Root, "--machine")).IsEqualTo(CliExitCode.Success);
        using JsonDocument repeated = JsonDocument.Parse(io.StandardOutput);
        await Assert.That(repeated.RootElement.GetProperty("artifacts")[0].GetProperty("writeStatus").GetString()).IsEqualTo("none");
    }

    [Test]
    public async Task ChangedContentRequiresRevisionAndPreservesAllOtherSourceText()
    {
        using var files = new DefinitionTestDirectory();
        var io = new CliFixture { AuthoringFiles = new PublicDefinitionFiles() };
        await Assert.That(await InitializeAsync(io, files)).IsEqualTo(CliExitCode.Success);
        await Assert.That(await io.RunAsync("definition", "build", "--root", files.Root, "--machine")).IsEqualTo(CliExitCode.Success);
        string source = files.Path("configurator.yaml");
        string original = File.ReadAllText(source);
        string changed = "# café Δ retain this comment\r\n" + original.Replace("Example Service", "Updated Service", StringComparison.Ordinal).Replace("\n", "\r\n", StringComparison.Ordinal);
        File.WriteAllBytes(source, [0xef, 0xbb, 0xbf, .. Encoding.UTF8.GetBytes(changed)]);
        await Assert.That(await io.RunAsync("definition", "check", "--root", files.Root, "--machine")).IsEqualTo(CliExitCode.Data);
        await Assert.That(await io.RunAsync("definition", "build", "--root", files.Root, "--output", files.Path("escape.json"), "--machine")).IsEqualTo(CliExitCode.Data);
        await Assert.That(File.Exists(files.Path("escape.json"))).IsFalse();
        byte[] captured = File.ReadAllBytes(source);
        string candidate = files.Path("candidate.yaml");
        await Assert.That(await io.RunAsync("definition", "revise", "--root", files.Root, "--output", candidate, "--machine")).IsEqualTo(CliExitCode.Success);
        byte[] expected = [0xef, 0xbb, 0xbf, .. Encoding.UTF8.GetBytes(changed.Replace("\"revision\": 1", "\"revision\": 2", StringComparison.Ordinal))];
        await Assert.That(File.ReadAllBytes(source).AsSpan().SequenceEqual(captured)).IsTrue();
        await Assert.That(File.ReadAllBytes(candidate).AsSpan().SequenceEqual(expected)).IsTrue();
        await Assert.That(await io.RunAsync("definition", "build", "--source", candidate, "--machine")).IsEqualTo(CliExitCode.Success);
        await Assert.That(DefinitionCodec.ParseJson(File.ReadAllBytes(files.Path("dist/configurator/revisions/2.json"))).Revision).IsEqualTo(2);
        await Assert.That(await io.RunAsync("definition", "revise", "--source", candidate, "--machine")).IsEqualTo(CliExitCode.Success);
        await Assert.That(File.ReadAllBytes(candidate).AsSpan().SequenceEqual(expected)).IsTrue();
        await Assert.That(File.ReadAllBytes(source).AsSpan().SequenceEqual(captured)).IsTrue();
    }

    [Test]
    public async Task EnvScaffoldingCopiesOnlyKeysAndDiagnosticsNeverExposeValuesOrPaths()
    {
        using var files = new DefinitionTestDirectory();
        string example = files.Path(".env.example");
        File.WriteAllText(example, "SERVICE_TOKEN=synthetic-private-value-sentinel\nSERVICE_NAME=synthetic-other-value\n");
        var io = new CliFixture { AuthoringFiles = new PublicDefinitionFiles() };
        await Assert.That(await InitializeAsync(io, files, "--from-env-example", example)).IsEqualTo(CliExitCode.Success);
        string source = File.ReadAllText(files.Path("configurator.yaml"));
        await Assert.That(source).Contains("SERVICE_TOKEN");
        await Assert.That(source.Contains("synthetic-private-value-sentinel", StringComparison.Ordinal)).IsFalse();
        await Assert.That(source.Contains("synthetic-other-value", StringComparison.Ordinal)).IsFalse();
        await Assert.That(source.Contains("default", StringComparison.Ordinal)).IsFalse();
        await Assert.That(CliMachineContractVerifier.Validate(io.StandardOutput)).IsEmpty();
        await Assert.That(await InitializeAsync(io, files, "--from-env-example", example)).IsEqualTo(CliExitCode.Data);
        string diagnostics = Encoding.UTF8.GetString(io.StandardOutput);
        await Assert.That(diagnostics.Contains(files.Root, StringComparison.Ordinal)).IsFalse();
        await Assert.That(diagnostics.Contains("synthetic-private-value-sentinel", StringComparison.Ordinal)).IsFalse();
    }

    [Test]
    public async Task ForkHasNewFamilyAndDoesNotChangeOriginal()
    {
        using var files = new DefinitionTestDirectory();
        var io = new CliFixture { AuthoringFiles = new PublicDefinitionFiles() };
        await Assert.That(await InitializeAsync(io, files, "--format", "json")).IsEqualTo(CliExitCode.Success);
        string original = files.Path("configurator.json");
        byte[] bytes = File.ReadAllBytes(original);
        string fork = files.Path("fork/configurator.json");
        await Assert.That(await io.RunAsync("definition", "init", "--fork-from", original, "--output", fork, "--machine")).IsEqualTo(CliExitCode.Success);
        await Assert.That(File.ReadAllBytes(original).AsSpan().SequenceEqual(bytes)).IsTrue();
        await Assert.That(DefinitionSourceCodec.ParseJson(File.ReadAllBytes(fork)).FamilyId)
            .IsNotEqualTo(DefinitionSourceCodec.ParseJson(bytes).FamilyId);
    }

    [Test]
    public async Task SchemaImportIsStrictAndAcknowledgmentReportsBoundedDifferences()
    {
        using var files = new DefinitionTestDirectory();
        string schema = files.Path("schema.json");
        File.WriteAllText(schema, """{"type":"object","additionalProperties":false,"properties":{"port":{"type":"integer","minimum":1,"maximum":65535}}}""");
        var io = new CliFixture { AuthoringFiles = new PublicDefinitionFiles() };
        await Assert.That(await InitializeAsync(io, files, "--from-schema", schema)).IsEqualTo(CliExitCode.Data);
        await Assert.That(File.Exists(files.Path("configurator.yaml"))).IsFalse();
        await Assert.That(Encoding.UTF8.GetString(io.StandardOutput)).Contains("schema-numeric-bounded-domain");
        await Assert.That(await InitializeAsync(io, files, "--from-schema", schema, "--accept-bounded-semantics")).IsEqualTo(CliExitCode.Success);
        await Assert.That(Encoding.UTF8.GetString(io.StandardOutput)).Contains("schema-numeric-bounded-domain");
        await Assert.That(CliMachineContractVerifier.Validate(io.StandardOutput)).IsEmpty();
    }

    [Test]
    public async Task SchemaReferencesCannotReadOutsideRootOrThroughSymlinks()
    {
        using var files = new DefinitionTestDirectory();
        string schema = files.Path("schema.json");
        File.WriteAllText(schema, """{"type":"object","additionalProperties":false,"properties":{"x":{"$ref":"../outside.json"}}}""");
        var io = new CliFixture { AuthoringFiles = new PublicDefinitionFiles() };
        await Assert.That(await InitializeAsync(io, files, "--from-schema", schema, "--accept-bounded-semantics")).IsEqualTo(CliExitCode.Data);
        File.WriteAllText(files.Path("target.json"), """{"type":"boolean"}""");
        File.CreateSymbolicLink(files.Path("reference.json"), files.Path("target.json"));
        File.WriteAllText(schema, """{"type":"object","additionalProperties":false,"properties":{"x":{"$ref":"reference.json"}}}""");
        await Assert.That(await InitializeAsync(io, files, "--from-schema", schema)).IsEqualTo(CliExitCode.Data);
        await Assert.That(Encoding.UTF8.GetString(io.StandardOutput).Contains(files.Root, StringComparison.Ordinal)).IsFalse();
    }

    [Test]
    public async Task ExplicitAnchorsBindHistoryAndMissingOrMismatchedAnchorsReject()
    {
        using var files = new DefinitionTestDirectory();
        var io = new CliFixture { AuthoringFiles = new PublicDefinitionFiles() };
        await Assert.That(await InitializeAsync(io, files, "--format", "json")).IsEqualTo(CliExitCode.Success);
        string source = files.Path("configurator.json");
        byte[] original = File.ReadAllBytes(source);
        await Assert.That(await io.RunAsync("definition", "revise", "--source", source, "--machine")).IsEqualTo(CliExitCode.Data);
        await Assert.That(File.ReadAllBytes(source).AsSpan().SequenceEqual(original)).IsTrue();
        await Assert.That(await io.RunAsync("definition", "check", "--source", source, "--against", files.Path("missing.json"), "--machine"))
            .IsEqualTo(CliExitCode.Data);
        string anchor = files.Path("canonical.json");
        await Assert.That(await io.RunAsync("definition", "build", "--source", source, "--output", anchor, "--machine")).IsEqualTo(CliExitCode.Success);
        JsonObject changed = JsonNode.Parse(original)!.AsObject();
        changed["project"]!["name"] = "Changed Service";
        File.WriteAllText(source, changed.ToJsonString());
        await Assert.That(await io.RunAsync("definition", "check", "--source", source, "--against", anchor, "--machine"))
            .IsEqualTo(CliExitCode.Data);
        string candidate = files.Path("candidate.json");
        byte[] changedBytes = File.ReadAllBytes(source);
        await Assert.That(await io.RunAsync("definition", "revise", "--source", source, "--against", anchor, "--output", candidate, "--machine"))
            .IsEqualTo(CliExitCode.Success);
        await Assert.That(await io.RunAsync("definition", "revise", "--source", source, "--against", anchor, "--output", candidate, "--machine"))
            .IsEqualTo(CliExitCode.Data);
        await Assert.That(DefinitionSourceCodec.ParseJson(File.ReadAllBytes(candidate)).Revision).IsEqualTo(2);
        await Assert.That(File.ReadAllBytes(source).AsSpan().SequenceEqual(changedBytes)).IsTrue();
    }

    [Test]
    public async Task RevisionOverflowRejectsWithoutChangingSource()
    {
        using var files = new DefinitionTestDirectory();
        var io = new CliFixture { AuthoringFiles = new PublicDefinitionFiles() };
        await Assert.That(await InitializeAsync(io, files, "--format", "json")).IsEqualTo(CliExitCode.Success);
        string source = files.Path("configurator.json");
        JsonObject document = JsonNode.Parse(File.ReadAllBytes(source))!.AsObject();
        document["identity"]!["revision"] = long.MaxValue;
        byte[] priorBytes = Encoding.UTF8.GetBytes(document.ToJsonString());
        string anchor = files.Path("maximum.json");
        File.WriteAllBytes(anchor, DefinitionSourceCodec.ParseJson(priorBytes).CanonicalBytes.ToArray());
        document["project"]!["name"] = "Changed Service";
        byte[] changed = Encoding.UTF8.GetBytes(document.ToJsonString());
        File.WriteAllBytes(source, changed);
        await Assert.That(await io.RunAsync("definition", "revise", "--source", source, "--against", anchor, "--output", files.Path("candidate.json"), "--machine")).IsEqualTo(CliExitCode.Data);
        await Assert.That(Encoding.UTF8.GetString(io.StandardOutput)).Contains("definition-revision-overflow");
        await Assert.That(File.ReadAllBytes(source).AsSpan().SequenceEqual(changed)).IsTrue();
    }

    [Test]
    public async Task ExplicitAnchorsCannotReplaceKnownRevisionHistory()
    {
        using var files = new DefinitionTestDirectory();
        var io = new CliFixture { AuthoringFiles = new PublicDefinitionFiles() };
        await Assert.That(await InitializeAsync(io, files, "--format", "json")).IsEqualTo(CliExitCode.Success);
        await Assert.That(await io.RunAsync("definition", "build", "--root", files.Root, "--machine")).IsEqualTo(CliExitCode.Success);
        string source = files.Path("configurator.json");
        string recorded = files.Path("dist/configurator/revisions/1.json");
        byte[] originalAnchor = File.ReadAllBytes(recorded);
        JsonObject edited = JsonNode.Parse(File.ReadAllBytes(source))!.AsObject();
        edited["project"]!["name"] = "Changed Service";
        byte[] changed = Encoding.UTF8.GetBytes(edited.ToJsonString());
        File.WriteAllBytes(source, changed);
        string substituted = files.Path("substituted.json");
        File.WriteAllBytes(substituted, DefinitionSourceCodec.ParseJson(changed).CanonicalBytes.ToArray());
        await Assert.That(await io.RunAsync("definition", "check", "--root", files.Root, "--against", substituted, "--machine"))
            .IsEqualTo(CliExitCode.Data);
        await Assert.That(await io.RunAsync("definition", "build", "--root", files.Root, "--against", substituted, "--output", files.Path("alternate.json"), "--machine"))
            .IsEqualTo(CliExitCode.Data);
        await Assert.That(File.Exists(files.Path("alternate.json"))).IsFalse();
        await Assert.That(await io.RunAsync("definition", "revise", "--root", files.Root, "--against", substituted, "--machine"))
            .IsEqualTo(CliExitCode.Data);
        await Assert.That(File.ReadAllBytes(recorded).AsSpan().SequenceEqual(originalAnchor)).IsTrue();
        await Assert.That(File.ReadAllBytes(source).AsSpan().SequenceEqual(changed)).IsTrue();
    }

    [Test]
    public async Task CheckBindsSanitizedDetailViewToCompiledDefinitionAndReportsPreparationOnlyAfterCompilation()
    {
        using var files = new DefinitionTestDirectory();
        var io = new CliFixture { AuthoringFiles = new PublicDefinitionFiles() };
        await Assert.That(await InitializeAsync(io, files, "--format", "json")).IsEqualTo(CliExitCode.Success);
        await Assert.That(await io.RunAsync("definition", "check", "--root", files.Root, "--machine")).IsEqualTo(CliExitCode.Success);
        using JsonDocument original = JsonDocument.Parse(io.StandardOutput);
        await Assert.That(original.RootElement.GetProperty("preparation").GetString()).IsEqualTo("ready");
        string source = files.Path("configurator.json");
        JsonObject changed = JsonNode.Parse(File.ReadAllBytes(source))!.AsObject();
        changed["project"]!["name"] = "Changed Service";
        File.WriteAllText(source, changed.ToJsonString());
        await Assert.That(await io.RunAsync("definition", "check", "--root", files.Root, "--machine")).IsEqualTo(CliExitCode.Success);
        using JsonDocument revised = JsonDocument.Parse(io.StandardOutput);
        await Assert.That(revised.RootElement.GetProperty("detailDigest").GetString())
            .IsNotEqualTo(original.RootElement.GetProperty("detailDigest").GetString());
        File.WriteAllText(source, "invalid");
        await Assert.That(await io.RunAsync("definition", "check", "--root", files.Root, "--machine")).IsEqualTo(CliExitCode.Data);
        using JsonDocument invalid = JsonDocument.Parse(io.StandardOutput);
        await Assert.That(invalid.RootElement.GetProperty("preparation").ValueKind).IsEqualTo(JsonValueKind.Null);
        await Assert.That(File.Exists(files.Path("dist/configurator/revisions/1.json"))).IsFalse();
    }

    [Test]
    public async Task IdMapPreparationFailurePublishesNeitherOutput()
    {
        using var files = new DefinitionTestDirectory();
        var io = new CliFixture { AuthoringFiles = new PublicDefinitionFiles() };
        await Assert.That(await InitializeAsync(io, files)).IsEqualTo(CliExitCode.Success);
        File.WriteAllText(files.Path("not-a-directory"), "occupied");
        string definition = files.Path("canonical.json");
        string map = files.Path("not-a-directory/id-map.json");
        await Assert.That(await io.RunAsync("definition", "build", "--root", files.Root, "--output", definition,
            "--id-map", map, "--machine")).IsEqualTo(CliExitCode.Io);
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
        var io = new CliFixture { AuthoringFiles = new PublicDefinitionFiles() };
        await Assert.That(await InitializeAsync(io, files)).IsEqualTo(CliExitCode.Success);
        string definition = files.Path("canonical.json");
        string map = files.Path("id-map.json");
        File.WriteAllText(map, "editor-owned");
        await Assert.That(await io.RunAsync("definition", "build", "--root", files.Root, "--output", definition,
            "--id-map", map, "--machine")).IsEqualTo(CliExitCode.Data);
        await Assert.That(DefinitionCodec.ParseJson(File.ReadAllBytes(definition)).Revision).IsEqualTo(1);
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
        var io = new CliFixture { AuthoringFiles = new PublicDefinitionFiles() };
        await Assert.That(await InitializeAsync(io, files)).IsEqualTo(CliExitCode.Success);
        string definition = files.Path("canonical.json");
        string map = files.Path("id-map.json");
        Directory.CreateDirectory(map);
        await Assert.That(await io.RunAsync("definition", "build", "--root", files.Root, "--output", definition,
            "--id-map", map, "--machine")).IsEqualTo(CliExitCode.Io);
        await Assert.That(DefinitionCodec.ParseJson(File.ReadAllBytes(definition)).Revision).IsEqualTo(1);
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
        var io = new CliFixture { AuthoringFiles = new PublicDefinitionFiles() };
        await Assert.That(await InitializeAsync(io, files)).IsEqualTo(CliExitCode.Success);
        string definition = files.Path("canonical.json");
        string map = files.Path("id-map.json");
        string[] arguments = ["definition", "build", "--root", files.Root, "--output", definition,
            "--id-map", map, "--machine"];
        CliExitCode exit = await new CliApplication().RunAsync(new(arguments, CliMode.Machine,
            new(io, io, io, new PublicDefinitionFiles(), 1, 4 * 1024 * 1024)));
        await Assert.That(exit).IsEqualTo(CliExitCode.Io);
        await Assert.That(File.Exists(definition)).IsFalse();
        await Assert.That(File.Exists(map)).IsFalse();
    }

    [Test]
    public async Task ActualProgramCreatesCandidateWithoutReplacingPublicSource()
    {
        using var files = new DefinitionTestDirectory();
        await ExecuteAsync(["definition", "init", "--root", files.Root, "--project-id", "example.service",
            "--name", "Example Service", "--application-version", "1.0.0", "--machine"]);
        await ExecuteAsync(["definition", "build", "--root", files.Root, "--machine"]);
        string source = files.Path("configurator.yaml");
        byte[] changed = Encoding.UTF8.GetBytes("# source comment\r\n" + File.ReadAllText(source).Replace("Example Service", "Candidate Service", StringComparison.Ordinal));
        File.WriteAllBytes(source, changed);
        string candidate = files.Path("candidate.yaml");
        byte[] receipt = await ExecuteAsync(["definition", "revise", "--root", files.Root, "--output", candidate, "--machine"]);
        await Assert.That(File.ReadAllBytes(source).AsSpan().SequenceEqual(changed)).IsTrue();
        await Assert.That(DefinitionSourceCodec.ParseYaml(File.ReadAllBytes(candidate)).Revision).IsEqualTo(2);
        await Assert.That(Encoding.UTF8.GetString(receipt).Contains(files.Root, StringComparison.Ordinal)).IsFalse();
        using JsonDocument result = JsonDocument.Parse(receipt);
        await Assert.That(result.RootElement.GetProperty("artifacts")[0].GetProperty("writeStatus").GetString()).IsEqualTo("written");

        static async Task<byte[]> ExecuteAsync(string[] arguments)
        {
            var start = new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, OperatingSystem.IsWindows() ? "Configurator.Cli.exe" : "Configurator.Cli"))
            {
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            foreach (string argument in arguments) start.ArgumentList.Add(argument);
            using var process = new Process { StartInfo = start };
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var output = new MemoryStream();
            using var error = new MemoryStream();
            if (!process.Start()) throw new InvalidOperationException("cli-start-failed");
            try
            {
                process.StandardInput.Close();
                Task stdout = process.StandardOutput.BaseStream.CopyToAsync(output, timeout.Token);
                Task stderr = process.StandardError.BaseStream.CopyToAsync(error, timeout.Token);
                await process.WaitForExitAsync(timeout.Token);
                await Task.WhenAll(stdout, stderr).WaitAsync(timeout.Token);
                await Assert.That(process.ExitCode).IsEqualTo(0);
                await Assert.That(error.Length).IsEqualTo(0);
                byte[] receipt = output.ToArray();
                await Assert.That(CliMachineContractVerifier.Validate(receipt)).IsEmpty();
                return receipt;
            }
            finally
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    await process.WaitForExitAsync(CancellationToken.None);
                }
            }
        }
    }

    [Test]
    public async Task EditorSaveDuringCandidateCommitPreservesBothIndependentSources()
    {
        using var files = new DefinitionTestDirectory();
        var io = new CliFixture { AuthoringFiles = new PublicDefinitionFiles() };
        await Assert.That(await InitializeAsync(io, files)).IsEqualTo(CliExitCode.Success);
        await Assert.That(await io.RunAsync("definition", "build", "--root", files.Root, "--machine")).IsEqualTo(CliExitCode.Success);
        string source = files.Path("configurator.yaml");
        string candidate = files.Path("candidate.yaml");
        string changed = File.ReadAllText(source).Replace("Example Service", "Candidate Service", StringComparison.Ordinal);
        File.WriteAllText(source, changed);
        byte[] expected = Encoding.UTF8.GetBytes(changed.Replace("\"revision\": 1", "\"revision\": 2", StringComparison.Ordinal));
        byte[] editorSave = Encoding.UTF8.GetBytes(changed.Replace("Candidate Service", "New Editor Service", StringComparison.Ordinal));
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        io.AuthoringFiles = new ControlledAuthoringFiles
        {
            BeforeCommit = async token =>
            {
                entered.SetResult();
                await release.Task.WaitAsync(TimeSpan.FromSeconds(10), token);
            }
        };
        Task<CliExitCode> revision = io.RunAsync("definition", "revise", "--root", files.Root, "--output", candidate, "--machine");
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            File.WriteAllBytes(source, editorSave);
        }
        finally { release.TrySetResult(); }
        await Assert.That(await revision.WaitAsync(TimeSpan.FromSeconds(10))).IsEqualTo(CliExitCode.Success);
        await Assert.That(File.ReadAllBytes(source).AsSpan().SequenceEqual(editorSave)).IsTrue();
        await Assert.That(File.ReadAllBytes(candidate).AsSpan().SequenceEqual(expected)).IsTrue();
        await Assert.That(CliMachineContractVerifier.Validate(io.StandardOutput)).IsEmpty();
        using JsonDocument receipt = JsonDocument.Parse(io.StandardOutput);
        JsonElement artifact = receipt.RootElement.GetProperty("artifacts")[0];
        await Assert.That(artifact.GetProperty("writeStatus").GetString()).IsEqualTo("written");
        await Assert.That(artifact.GetProperty("installation").GetString()).IsEqualTo("not-applicable");
        await Assert.That(artifact.GetProperty("verification").GetString()).IsEqualTo("not-applicable");
    }

    [Test]
    [Arguments("source")]
    [Arguments("relative-source")]
    [Arguments("configurator.yaml")]
    [Arguments("configurator.yml")]
    [Arguments("configurator.json")]
    [Arguments("existing")]
    [Arguments("link")]
    public async Task CandidateRefusesSourceDiscoveryExistingAndLinkedDestinations(string destination)
    {
        using var files = new DefinitionTestDirectory();
        var io = new CliFixture { AuthoringFiles = new PublicDefinitionFiles() };
        await Assert.That(await InitializeAsync(io, files)).IsEqualTo(CliExitCode.Success);
        await Assert.That(await io.RunAsync("definition", "build", "--root", files.Root, "--machine")).IsEqualTo(CliExitCode.Success);
        string source = files.Path("configurator.yaml");
        byte[] changed = Encoding.UTF8.GetBytes(File.ReadAllText(source).Replace("Example Service", "Changed Service", StringComparison.Ordinal));
        File.WriteAllBytes(source, changed);
        string output = destination switch
        {
            "source" => source,
            "relative-source" => files.Path("child/../configurator.yaml"),
            "existing" => files.Path("candidate.yaml"),
            "link" => files.Path("linked.yaml"),
            _ => files.Path("other/" + destination)
        };
        if (destination == "existing") File.WriteAllText(output, "editor-owned");
        if (destination == "link") File.CreateSymbolicLink(output, source);
        await Assert.That(await io.RunAsync("definition", "revise", "--root", files.Root, "--output", output, "--machine"))
            .IsEqualTo(CliExitCode.Data);
        await Assert.That(File.ReadAllBytes(source).AsSpan().SequenceEqual(changed)).IsTrue();
        if (destination == "existing") await Assert.That(File.ReadAllText(output)).IsEqualTo("editor-owned");
        if (destination.StartsWith("configurator.", StringComparison.Ordinal)) await Assert.That(File.Exists(output)).IsFalse();
        await Assert.That(CliMachineContractVerifier.Validate(io.StandardOutput)).IsEmpty();
        await Assert.That(Encoding.UTF8.GetString(io.StandardOutput).Contains(files.Root, StringComparison.Ordinal)).IsFalse();
    }

    [Test]
    public async Task CanonicallyUnchangedCommentsDoNotCreateCandidateOrRewriteSource()
    {
        using var files = new DefinitionTestDirectory();
        var io = new CliFixture { AuthoringFiles = new PublicDefinitionFiles() };
        await Assert.That(await InitializeAsync(io, files)).IsEqualTo(CliExitCode.Success);
        await Assert.That(await io.RunAsync("definition", "build", "--root", files.Root, "--machine")).IsEqualTo(CliExitCode.Success);
        string source = files.Path("configurator.yaml");
        byte[] comments = [0xef, 0xbb, 0xbf, .. Encoding.UTF8.GetBytes("# retain comments\r\n" + File.ReadAllText(source).Replace("\n", "\r\n", StringComparison.Ordinal))];
        File.WriteAllBytes(source, comments);
        string candidate = files.Path("candidate.yaml");
        foreach (string[] output in new[] { Array.Empty<string>(), new[] { "--output", candidate } })
        {
            await Assert.That(await io.RunAsync(["definition", "revise", "--root", files.Root, "--machine", .. output]))
                .IsEqualTo(CliExitCode.Success);
            await Assert.That(File.ReadAllBytes(source).AsSpan().SequenceEqual(comments)).IsTrue();
            await Assert.That(File.Exists(candidate)).IsFalse();
            using JsonDocument receipt = JsonDocument.Parse(io.StandardOutput);
            await Assert.That(receipt.RootElement.GetProperty("artifacts")[0].GetProperty("writeStatus").GetString()).IsEqualTo("none");
        }
    }

    [Test]
    [Arguments("discover")]
    [Arguments("source")]
    [Arguments("anchor-exists")]
    [Arguments("anchor-read")]
    [Arguments("fork")]
    [Arguments("environment")]
    [Arguments("schema")]
    [Arguments("schema-reference")]
    [Arguments("initial-exists")]
    [Arguments("staging")]
    public async Task EveryAuthoringFileRouteHonorsInvocationDenial(string route)
    {
        using var files = new DefinitionTestDirectory();
        var io = new CliFixture { AuthoringFiles = new PublicDefinitionFiles() };
        await Assert.That(await InitializeAsync(io, files)).IsEqualTo(CliExitCode.Success);
        await Assert.That(await io.RunAsync("definition", "build", "--root", files.Root, "--machine")).IsEqualTo(CliExitCode.Success);
        string source = files.Path("configurator.yaml");
        string anchor = files.Path("dist/configurator/revisions/1.json");
        byte[] original = File.ReadAllBytes(source);
        string schema = files.Path("schema.json");
        string reference = files.Path("reference.json");
        string example = files.Path(".env.example");
        File.WriteAllText(schema, """{"type":"object","additionalProperties":false,"properties":{"enabled":{"$ref":"reference.json"}}}""");
        File.WriteAllText(reference, """{"type":"boolean"}""");
        File.WriteAllText(example, "SERVICE_NAME=synthetic-value-not-for-diagnostics\n");
        var boundary = new ControlledAuthoringFiles
        {
            DenyDiscovery = route == "discover",
            DenyExistsPath = route switch { "anchor-exists" => anchor, "initial-exists" => source, _ => null },
            DenyReadPath = route switch
            {
                "source" or "fork" => source,
                "anchor-read" => anchor,
                "environment" => example,
                "schema" => schema,
                "schema-reference" => reference,
                _ => null
            },
            DenyPrepare = route == "staging"
        };
        io.AuthoringFiles = boundary;
        CliExitCode exit = route switch
        {
            "fork" => await io.RunAsync("definition", "init", "--fork-from", source, "--output", files.Path("fork.yaml"), "--machine"),
            "environment" => await InitializeAsync(io, files, "--from-env-example", example, "--output", files.Path("scaffold.yaml")),
            "schema" or "schema-reference" => await InitializeAsync(io, files, "--from-schema", schema, "--output", files.Path("scaffold.yaml")),
            "initial-exists" => await InitializeAsync(io, files),
            "staging" => await io.RunAsync("definition", "build", "--source", source, "--output", files.Path("candidate.json"), "--machine"),
            _ => await io.RunAsync("definition", "check", "--root", files.Root, "--machine")
        };
        await Assert.That(exit).IsEqualTo(CliExitCode.Io);
        await Assert.That(boundary.Rejected).IsTrue();
        await Assert.That(File.ReadAllBytes(source).AsSpan().SequenceEqual(original)).IsTrue();
        await Assert.That(File.Exists(files.Path("scaffold.yaml")) || File.Exists(files.Path("fork.yaml")) || File.Exists(files.Path("candidate.json"))).IsFalse();
        await Assert.That(CliMachineContractVerifier.Validate(io.StandardOutput)).IsEmpty();
        string receipt = Encoding.UTF8.GetString(io.StandardOutput);
        await Assert.That(receipt.Contains(files.Root, StringComparison.Ordinal)).IsFalse();
        await Assert.That(receipt.Contains("synthetic-value-not-for-diagnostics", StringComparison.Ordinal)).IsFalse();
        await Assert.That(boundary.Returned.All(bytes => bytes.Span.IndexOfAnyExcept((byte)0) < 0)).IsTrue();
    }

    [Test]
    public async Task NonAuthoringFixtureCannotAcquireAmbientPublicFileAuthority()
    {
        using var files = new DefinitionTestDirectory();
        var io = new CliFixture();
        await Assert.That(await InitializeAsync(io, files)).IsEqualTo(CliExitCode.Io);
        await Assert.That(File.Exists(files.Path("configurator.yaml"))).IsFalse();
        await Assert.That(CliMachineContractVerifier.Validate(io.StandardOutput)).IsEmpty();
    }

    [Test]
    [Arguments("source")]
    [Arguments("anchor")]
    [Arguments("fork")]
    [Arguments("environment")]
    [Arguments("schema")]
    [Arguments("schema-reference")]
    [Arguments("staging")]
    public async Task ReducedInvocationLimitBoundsEveryPublicReadAndWrite(string route)
    {
        using var files = new DefinitionTestDirectory();
        var io = new CliFixture { AuthoringFiles = new PublicDefinitionFiles() };
        await Assert.That(await InitializeAsync(io, files)).IsEqualTo(CliExitCode.Success);
        await Assert.That(await io.RunAsync("definition", "build", "--root", files.Root, "--machine")).IsEqualTo(CliExitCode.Success);
        string source = files.Path("configurator.yaml");
        byte[] original = File.ReadAllBytes(source);
        string anchor = files.Path("dist/configurator/revisions/1.json");
        File.AppendAllText(anchor, new string(' ', original.Length + 1));
        string example = files.Path(".env.example");
        File.WriteAllText(example, "SERVICE_NAME=synthetic-value\n");
        string schema = files.Path("schema.json");
        File.WriteAllText(schema, """{"type":"object","additionalProperties":false,"properties":{"enabled":{"$ref":"reference.json"}}}""");
        File.WriteAllText(files.Path("reference.json"), """{"type":"boolean"}""" + new string(' ', 512));
        io.MaximumBytes = route switch
        {
            "source" or "fork" => original.Length - 1,
            "anchor" => original.Length,
            "environment" => 8,
            "schema" => 16,
            "schema-reference" => 256,
            _ => 64
        };
        io.AuthoringFiles = new ControlledAuthoringFiles();
        CliExitCode exit = route switch
        {
            "fork" => await io.RunAsync("definition", "init", "--fork-from", source, "--output", files.Path("fork.yaml"), "--machine"),
            "environment" or "staging" => await InitializeAsync(io, files, "--from-env-example", example, "--output", files.Path("candidate.yaml")),
            "schema" or "schema-reference" => await InitializeAsync(io, files, "--from-schema", schema, "--output", files.Path("candidate.yaml")),
            _ => await io.RunAsync("definition", "check", "--root", files.Root, "--machine")
        };
        await Assert.That(exit).IsEqualTo(CliExitCode.Data);
        await Assert.That(Encoding.UTF8.GetString(io.StandardOutput)).Contains("definition-source-bound");
        await Assert.That(File.ReadAllBytes(source).AsSpan().SequenceEqual(original)).IsTrue();
        await Assert.That(File.Exists(files.Path("candidate.yaml")) || File.Exists(files.Path("fork.yaml"))).IsFalse();
        await Assert.That(CliMachineContractVerifier.Validate(io.StandardOutput)).IsEmpty();
    }

    [Test]
    public async Task SchemaAggregateAndDocumentCeilingsSurviveCapabilityMigration()
    {
        using var files = new DefinitionTestDirectory();
        string schema = files.Path("schema.json");
        string reference = files.Path("reference.json");
        string root = """{"type":"object","additionalProperties":false,"properties":{"enabled":{"$ref":"reference.json"}}}""";
        string child = """{"type":"boolean"}""" + new string(' ', 80);
        File.WriteAllText(schema, root);
        File.WriteAllText(reference, child);
        var boundary = new ControlledAuthoringFiles();
        var io = new CliFixture { AuthoringFiles = boundary, MaximumBytes = 128 };
        await Assert.That(await InitializeAsync(io, files, "--from-schema", schema)).IsEqualTo(CliExitCode.Data);
        await Assert.That(Encoding.UTF8.GetString(io.StandardOutput)).Contains("schema-reference-bound");
        await Assert.That(boundary.Returned.All(bytes => bytes.Span.IndexOfAnyExcept((byte)0) < 0)).IsTrue();

        var properties = new JsonObject();
        for (int index = 0; index < 64; index++)
        {
            string name = "reference" + index.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".json";
            properties["field" + index.ToString(System.Globalization.CultureInfo.InvariantCulture)] = new JsonObject { ["$ref"] = name };
            File.WriteAllText(files.Path(name), """{"type":"boolean"}""");
        }
        File.WriteAllText(schema, new JsonObject { ["type"] = "object", ["additionalProperties"] = false, ["properties"] = properties }.ToJsonString());
        io.MaximumBytes = 4 * 1024 * 1024;
        await Assert.That(await InitializeAsync(io, files, "--from-schema", schema)).IsEqualTo(CliExitCode.Data);
        await Assert.That(Encoding.UTF8.GetString(io.StandardOutput)).Contains("schema-reference-bound");
        await Assert.That(File.Exists(files.Path("configurator.yaml"))).IsFalse();
    }

    private sealed class ControlledAuthoringFiles : ICliAuthoringFiles
    {
        private readonly PublicDefinitionFiles _native = new();
        internal bool DenyDiscovery { get; init; }
        internal string? DenyReadPath { get; init; }
        internal string? DenyExistsPath { get; init; }
        internal bool DenyPrepare { get; init; }
        internal Func<CancellationToken, Task>? BeforeCommit { get; init; }
        internal bool Rejected { get; private set; }
        internal List<ReadOnlyMemory<byte>> Returned { get; } = [];

        public ValueTask<string> DiscoverSourceAsync(string root, string? source, CancellationToken cancellationToken)
        {
            Reject(DenyDiscovery);
            return _native.DiscoverSourceAsync(root, source, cancellationToken);
        }

        public async ValueTask<CliInputBuffer> ReadAsync(string path, int maximumBytes, CancellationToken cancellationToken)
        {
            Reject(Path.GetFullPath(path) == DenyReadPath);
            CliInputBuffer input = await _native.ReadAsync(path, maximumBytes, cancellationToken);
            Returned.Add(input.Bytes);
            return input;
        }

        public ValueTask<bool> ExistsAsync(string path, CancellationToken cancellationToken)
        {
            Reject(Path.GetFullPath(path) == DenyExistsPath);
            return _native.ExistsAsync(path, cancellationToken);
        }

        public ICliPublicFilePreparation PrepareCreate(string path, ReadOnlyMemory<byte> bytes, int maximumBytes, bool acceptIdentical)
        {
            Reject(DenyPrepare);
            return new ControlledPreparation(_native.PrepareCreate(path, bytes, maximumBytes, acceptIdentical), BeforeCommit);
        }

        private void Reject(bool denied)
        {
            if (!denied) return;
            Rejected = true;
            throw new UnauthorizedAccessException();
        }

        private sealed class ControlledPreparation(ICliPublicFilePreparation native,
            Func<CancellationToken, Task>? beforeCommit) : ICliPublicFilePreparation
        {
            public ValueTask StageAsync(CancellationToken cancellationToken) => native.StageAsync(cancellationToken);
            public async ValueTask<bool> CommitAsync(CancellationToken cancellationToken)
            {
                if (beforeCommit is not null) await beforeCommit(cancellationToken);
                return await native.CommitAsync(cancellationToken);
            }
            public void Dispose() => native.Dispose();
        }
    }

    private static Task<CliExitCode> InitializeAsync(CliFixture io, DefinitionTestDirectory files, params string[] extra) =>
        io.RunAsync(["definition", "init", "--root", files.Root, "--project-id", "example.service", "--name", "Example Service",
            "--application-version", "1.0.0", "--machine", .. extra]);

    [Test]
    public async Task DefinitionDiffSeparatesBehavioralAndDisplayOnlyChanges()
    {
        using var files = new DefinitionTestDirectory();
        var io = new CliFixture { AuthoringFiles = new PublicDefinitionFiles() };
        string baseline = files.Path("base.json");
        string displayCandidate = files.Path("display.json");
        string behavioralCandidate = files.Path("behavioral.json");

        string baseSource = """
        {
          "$schema": "urn:islamu:configurator:authoring:v0.0.1",
          "identity": {
            "familyId": "77777777-7777-4777-8777-777777777777",
            "revision": 1
          },
          "project": {
            "id": "example.diff",
            "name": "Diff Service"
          },
          "applicationVersions": ["1.0.0"],
          "configuration": {
            "type": "object",
            "additionalProperties": false,
            "required": ["endpoint"],
            "properties": {
              "endpoint": {
                "type": "string",
                "title": "Endpoint",
                "description": "Original endpoint help",
                "minLength": 4
              }
            }
          },
          "outputs": {
            "config.json": {
              "id": "config",
              "format": "json",
              "targetUpdateMode": "replace",
              "restartRequirement": "process"
            }
          }
        }
        """;
        File.WriteAllText(baseline, baseSource);
        File.WriteAllText(displayCandidate, baseSource.Replace("Original endpoint help", "Clarified endpoint help", StringComparison.Ordinal));
        File.WriteAllText(behavioralCandidate, baseSource.Replace("\"minLength\": 4", "\"minLength\": 8, \"x-configurator\": { \"sensitivity\": \"secret\" }", StringComparison.Ordinal));

        await Assert.That(await io.RunAsync("definition", "diff", "--against", baseline, "--source", displayCandidate, "--machine"))
            .IsEqualTo(CliExitCode.Success);
        await Assert.That(CliMachineContractVerifier.Validate(io.StandardOutput)).IsEmpty();
        using (JsonDocument displayDoc = JsonDocument.Parse(io.StandardOutput))
        {
            JsonElement diff = displayDoc.RootElement.GetProperty("revisionDiff");
            await Assert.That(diff.GetProperty("sameFamily").GetBoolean()).IsTrue();
            await Assert.That(diff.GetProperty("hasBehavioralChanges").GetBoolean()).IsFalse();
            await Assert.That(diff.GetProperty("hasDisplayOnlyChanges").GetBoolean()).IsTrue();
            await Assert.That(diff.GetProperty("displayOnlyCount").GetInt32()).IsGreaterThan(0);
        }

        await Assert.That(await io.RunAsync("definition", "diff", "--against", baseline, "--source", behavioralCandidate, "--machine"))
            .IsEqualTo(CliExitCode.Success);
        await Assert.That(CliMachineContractVerifier.Validate(io.StandardOutput)).IsEmpty();
        using (JsonDocument behavioralDoc = JsonDocument.Parse(io.StandardOutput))
        {
            JsonElement diff = behavioralDoc.RootElement.GetProperty("revisionDiff");
            await Assert.That(diff.GetProperty("hasBehavioralChanges").GetBoolean()).IsTrue();
            string[] categories = diff.GetProperty("changes").EnumerateArray()
                .Select(c => c.GetProperty("category").GetString()!).ToArray();
            await Assert.That(categories).Contains("sensitivity-changed");
            await Assert.That(categories).Contains("constraint-changed");
        }
    }

    [Test]
    public async Task DefinitionTestExecutesFixturesAndRejectsExecutableOrEmptySuites()
    {
        using var files = new DefinitionTestDirectory();
        var io = new CliFixture { AuthoringFiles = new PublicDefinitionFiles() };
        string source = files.Path("configurator.json");
        string passingFixtures = files.Path("fixtures-pass.json");
        string failingFixtures = files.Path("fixtures-fail.json");
        string executableFixtures = files.Path("fixtures-exec.json");
        string emptyFixtures = files.Path("fixtures-empty.json");

        File.WriteAllText(source, """
        {
          "$schema": "urn:islamu:configurator:authoring:v0.0.1",
          "identity": {
            "familyId": "88888888-8888-4888-8888-888888888888",
            "revision": 1
          },
          "project": {
            "id": "example.fixtures",
            "name": "Fixture Service"
          },
          "applicationVersions": ["1.0.0"],
          "configuration": {
            "type": "object",
            "additionalProperties": false,
            "required": ["service"],
            "properties": {
              "service": { "type": "string", "minLength": 3 }
            }
          },
          "outputs": {
            "config.json": {
              "id": "config",
              "format": "json",
              "targetUpdateMode": "replace",
              "restartRequirement": "process"
            }
          }
        }
        """);

        File.WriteAllText(passingFixtures, """
        {
          "fixtureContractVersion": "0.0.1",
          "cases": [
            {
              "id": "valid-config",
              "kind": "prepare",
              "answers": { "answerContractVersion": "0.0.1", "values": { "service": "alpha" }, "omitted": [] },
              "expect": {
                "ready": true,
                "artifacts": [
                  { "id": "config", "fileName": "config.json", "contentUtf8": "{\"service\":\"alpha\"}" }
                ]
              }
            },
            {
              "id": "missing-required-refusal",
              "kind": "prepare",
              "answers": { "answerContractVersion": "0.0.1", "values": {}, "omitted": ["/service"] },
              "expect": {
                "ready": false,
                "refusal": "value-required"
              }
            }
          ]
        }
        """);

        await Assert.That(await io.RunAsync("definition", "test", "--source", source, "--fixtures", passingFixtures, "--machine"))
            .IsEqualTo(CliExitCode.Success);
        await Assert.That(CliMachineContractVerifier.Validate(io.StandardOutput)).IsEmpty();
        using (JsonDocument passDoc = JsonDocument.Parse(io.StandardOutput))
        {
            JsonElement report = passDoc.RootElement.GetProperty("fixtureReport");
            await Assert.That(report.GetProperty("passed").GetBoolean()).IsTrue();
            await Assert.That(report.GetProperty("scope").GetString()).IsEqualTo("synthetic-data-only-local-conformance");
            await Assert.That(report.GetProperty("engineVersion").GetString()).IsEqualTo("0.0.1");
            await Assert.That(report.GetProperty("totalCases").GetInt32()).IsEqualTo(2);
            await Assert.That(report.GetProperty("expectedRefusalCases").GetInt32()).IsEqualTo(1);
            await Assert.That(report.GetProperty("cases")[1].GetProperty("outcome").GetString()).IsEqualTo("passed-expected-refusal");
        }

        // Mismatched bytes fails with Validation exit code and explicit failureCode.
        File.WriteAllText(failingFixtures, """
        {
          "fixtureContractVersion": "0.0.1",
          "cases": [
            {
              "id": "mismatch",
              "kind": "prepare",
              "answers": { "answerContractVersion": "0.0.1", "values": { "service": "alpha" }, "omitted": [] },
              "expect": {
                "ready": true,
                "artifacts": [
                  { "id": "config", "contentUtf8": "{\"service\":\"beta\"}" }
                ]
              }
            }
          ]
        }
        """);
        await Assert.That(await io.RunAsync("definition", "test", "--source", source, "--fixtures", failingFixtures, "--machine"))
            .IsEqualTo(CliExitCode.Validation);
        await Assert.That(CliMachineContractVerifier.Validate(io.StandardOutput)).IsEmpty();
        using (JsonDocument failDoc = JsonDocument.Parse(io.StandardOutput))
        {
            await Assert.That(failDoc.RootElement.GetProperty("fixtureReport").GetProperty("passed").GetBoolean()).IsFalse();
            await Assert.That(failDoc.RootElement.GetProperty("fixtureReport").GetProperty("failedCases").GetInt32()).IsEqualTo(1);
        }

        // Executable keyword rejected fail-closed with Data exit code.
        File.WriteAllText(executableFixtures, """
        {
          "fixtureContractVersion": "0.0.1",
          "command": "echo forbidden",
          "cases": []
        }
        """);
        await Assert.That(await io.RunAsync("definition", "test", "--source", source, "--fixtures", executableFixtures, "--machine"))
            .IsEqualTo(CliExitCode.Data);
        await Assert.That(Encoding.UTF8.GetString(io.StandardOutput)).Contains("fixture-member");

        // Vacuous 0-case suite rejected fail-closed with Data exit code.
        File.WriteAllText(emptyFixtures, """
        {
          "fixtureContractVersion": "0.0.1",
          "cases": []
        }
        """);
        await Assert.That(await io.RunAsync("definition", "test", "--source", source, "--fixtures", emptyFixtures, "--machine"))
            .IsEqualTo(CliExitCode.Data);
        await Assert.That(Encoding.UTF8.GetString(io.StandardOutput)).Contains("fixture-empty");
    }

    [Test]
    [Arguments("diff-missing-against", "against-required")]
    [Arguments("diff-stdin-against", "stdin-not-supported")]
    [Arguments("diff-stdin-source", "stdin-not-supported")]
    [Arguments("diff-stdin-root", "stdin-not-supported")]
    [Arguments("test-missing-fixtures", "fixtures-required")]
    [Arguments("test-stdin-fixtures", "stdin-not-supported")]
    [Arguments("test-stdin-source", "stdin-not-supported")]
    [Arguments("test-stdin-root", "stdin-not-supported")]
    public async Task DefinitionDiffAndTestRejectMissingRequiredAndStdinArgumentsBeforeIo(string scenario, string expectedCode)
    {
        var io = new CliFixture { AuthoringFiles = new PublicDefinitionFiles() };
        string[] arguments = scenario switch
        {
            "diff-missing-against" => ["definition", "diff", "--machine"],
            "diff-stdin-against" => ["definition", "diff", "--against", "-", "--machine"],
            "diff-stdin-source" => ["definition", "diff", "--against", "base.json", "--source", "-", "--machine"],
            "diff-stdin-root" => ["definition", "diff", "--against", "base.json", "--root", "-", "--machine"],
            "test-missing-fixtures" => ["definition", "test", "--machine"],
            "test-stdin-fixtures" => ["definition", "test", "--fixtures", "-", "--machine"],
            "test-stdin-source" => ["definition", "test", "--fixtures", "fixtures.json", "--source", "-", "--machine"],
            "test-stdin-root" => ["definition", "test", "--fixtures", "fixtures.json", "--root", "-", "--machine"],
            _ => throw new InvalidOperationException("unknown-scenario")
        };

        await Assert.That(await io.RunAsync(arguments)).IsEqualTo(CliExitCode.Usage);
        await Assert.That(CliMachineContractVerifier.Validate(io.StandardOutput)).IsEmpty();
        using JsonDocument receipt = JsonDocument.Parse(io.StandardOutput);
        await Assert.That(receipt.RootElement.GetProperty("diagnostics")[0].GetProperty("code").GetString())
            .IsEqualTo(expectedCode);
    }

    [Test]
    public async Task DefinitionDiffIdentifiesIdenticalCanonicalAndCrossFamilySources()
    {
        using var files = new DefinitionTestDirectory();
        var io = new CliFixture { AuthoringFiles = new PublicDefinitionFiles() };
        string first = files.Path("first.json");
        string second = files.Path("second.json");

        string template = """
        {
          "$schema": "urn:islamu:configurator:authoring:v0.0.1",
          "identity": { "familyId": "{FAMILY}", "revision": 1 },
          "project": { "id": "example.family", "name": "Family Service" },
          "applicationVersions": ["1.0.0"],
          "configuration": {
            "type": "object",
            "additionalProperties": false,
            "properties": { "name": { "type": "string" } }
          },
          "outputs": { "config.json": { "id": "config", "format": "json" } }
        }
        """;
        File.WriteAllText(first, template.Replace("{FAMILY}", "11111111-1111-4111-8111-111111111111", StringComparison.Ordinal));
        File.WriteAllText(second, template.Replace("{FAMILY}", "22222222-2222-4222-8222-222222222222", StringComparison.Ordinal));

        await Assert.That(await io.RunAsync("definition", "diff", "--against", first, "--source", first, "--machine")).IsEqualTo(CliExitCode.Success);
        using (JsonDocument same = JsonDocument.Parse(io.StandardOutput))
        {
            await Assert.That(same.RootElement.GetProperty("revisionDiff").GetProperty("canonicalIdentical").GetBoolean()).IsTrue();
            await Assert.That(same.RootElement.GetProperty("revisionDiff").GetProperty("totalChanges").GetInt32()).IsEqualTo(0);
        }

        await Assert.That(await io.RunAsync("definition", "diff", "--against", first, "--source", second, "--machine")).IsEqualTo(CliExitCode.Success);
        using (JsonDocument cross = JsonDocument.Parse(io.StandardOutput))
        {
            await Assert.That(cross.RootElement.GetProperty("revisionDiff").GetProperty("sameFamily").GetBoolean()).IsFalse();
        }
    }
}
