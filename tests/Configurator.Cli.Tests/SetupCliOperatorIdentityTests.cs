using System.Text;
using System.Text.Json;

namespace Configurator.Cli.Tests;

public sealed class SetupCliOperatorIdentityTests
{
    [Test]
    public async Task ImportPreservesExplicitRevisionThroughDeclaredProjection()
    {
        var io = new CliFixture();
        string revision = new('a', 64);
        await Assert.That(await io.ArtifactAsync("import", "request", "--output", "request.json",
            "--expected-revision", revision, "--revision-field", "expectedRevision")).IsEqualTo(SetupCliExitCode.Success);
        using JsonDocument request = JsonDocument.Parse(io.Files["request.json"]);
        await Assert.That(request.RootElement.GetProperty("expectedRevision").GetString()).IsEqualTo(revision);
        await Assert.That(request.RootElement.GetProperty("name").GetString()).IsEqualTo("private-answer-marker");
        await Assert.That(Encoding.UTF8.GetString(io.StandardOutput)).DoesNotContain(revision);
        await Assert.That(Encoding.UTF8.GetString(io.StandardOutput)).DoesNotContain("private-answer-marker");
        await Assert.That(SetupCliMachineContractVerifier.Validate(io.StandardOutput)).IsEmpty();
    }

    [Test]
    public async Task MissingConflictingUnsupportedAndInactiveRevisionFailClosed()
    {
        var io = new CliFixture();
        await Assert.That(await io.ArtifactAsync("import", "request", "--dry-run")).IsEqualTo(SetupCliExitCode.Usage);
        foreach (string operation in new[] { "create", "format", "export", "preview" })
            await Assert.That(await io.ArtifactAsync(operation, "request", "--dry-run")).IsEqualTo(SetupCliExitCode.Blocked);
        await Assert.That(await io.ArtifactAsync("import", "config", "--dry-run", "--expected-revision", "absent",
            "--revision-field", "expectedRevision")).IsEqualTo(SetupCliExitCode.Blocked);
        io.Files["answers.json"] = """{"service.name":"private-answer-marker","expectedRevision":"different"}"""u8.ToArray();
        await Assert.That(await io.ArtifactAsync("import", "request", "--dry-run", "--expected-revision", "absent",
            "--revision-field", "expectedRevision")).IsEqualTo(SetupCliExitCode.Validation);
        io.Files["answers.json"] = Encoding.UTF8.GetBytes(CliFixture.Answers);
        io.Files["definition.json"] = Encoding.UTF8.GetBytes(CliFixture.Definition.Replace(
            "\"id\":\"expectedRevision\",\"type\":\"string\"", "\"id\":\"expectedRevision\",\"activeWhen\":{\"op\":\"equals\",\"field\":\"enabled\",\"value\":false},\"type\":\"string\"", StringComparison.Ordinal));
        await Assert.That(await io.ArtifactAsync("import", "request", "--dry-run", "--expected-revision", "absent",
            "--revision-field", "expectedRevision")).IsEqualTo(SetupCliExitCode.Blocked);
        await Assert.That(io.Files.Keys).IsEquivalentTo(["definition.json", "answers.json", "baseline.json"]);
    }

    [Test]
    public async Task ImportDryRunValidatesWithoutWritingAndInvalidRevisionIsUsage()
    {
        var io = new CliFixture();
        await Assert.That(await io.ArtifactAsync("import", "request", "--dry-run", "--expected-revision", "absent",
            "--revision-field", "expectedRevision")).IsEqualTo(SetupCliExitCode.Success);
        await Assert.That(io.Files.Keys).IsEquivalentTo(["definition.json", "answers.json", "baseline.json"]);
        await Assert.That(await io.ArtifactAsync("import", "request", "--dry-run", "--expected-revision", "BAD",
            "--revision-field", "expectedRevision")).IsEqualTo(SetupCliExitCode.Usage);
    }
}
