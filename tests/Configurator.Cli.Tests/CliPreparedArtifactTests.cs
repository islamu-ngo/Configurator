using System.Text;
using System.Text.Json;
using Configurator.Core;

namespace Configurator.Cli.Tests;

public sealed class CliPreparedArtifactTests
{
    [Test]
    public async Task PublicStdoutDeliveryDoesNotClaimNativeInstallationOrVerification()
    {
        using var output = new MemoryStream();
        var writer = new CliProgram.SystemWriter(output);
        CliPublicationResult publication = await writer.WriteArtifactAsync("-", ArtifactKind.PublicCatalogue,
            "[]"u8.ToArray(), 65_536, CancellationToken.None);
        await Assert.That(Encoding.UTF8.GetString(output.ToArray())).IsEqualTo("[]");
        await Assert.That(publication.Outcomes[0].Result).IsNull();
        await Assert.That(publication.Outcomes[0].DeliveredToStandardOutput).IsTrue();
        var invocation = new CliInvocation([], CliMode.Machine,
            new(new CliFixture(), writer, writer, CliFixture.RejectingAuthoringFiles, 65_536, 4 * 1024 * 1024));
        var command = new CliCommand("catalogue", "list", true, false, false, null, null, null, "-", null, null);
        CliCommandResult planned = CliResults.Success([CliResults.Artifact("catalogue", "application/json",
            "[]"u8.ToArray(), "public", "stdout", "not-attempted")]);
        var ledger = new CliOutcomeLedger();
        ledger.Plan(invocation, command, planned);
        ledger.BeginPublication();
        byte[] receipt = CliMachineOutput.Serialize(command, ledger.Apply(publication, planned), 65_536);
        await Assert.That(CliMachineContractVerifier.Validate(receipt)).IsEmpty();
        using JsonDocument result = JsonDocument.Parse(receipt);
        JsonElement artifact = result.RootElement.GetProperty("artifacts")[0];
        await Assert.That(artifact.GetProperty("writeStatus").GetString()).IsEqualTo("written");
        await Assert.That(artifact.GetProperty("installation").GetString()).IsEqualTo("not-applicable");
        await Assert.That(artifact.GetProperty("verification").GetString()).IsEqualTo("not-applicable");
    }

    [Test]
    [Arguments("config", "json")]
    [Arguments("yaml", "yaml")]
    [Arguments("environment", "dotenv")]
    [Arguments("markdown", "markdown")]
    public async Task PreparedArtifactReopensValidatesAndFormatsAgainstAnswerProof(string artifact, string format)
    {
        var io = new CliFixture();
        await Assert.That(await io.ArtifactAsync("create", artifact, "--output", "prepared")).IsEqualTo(CliExitCode.Success);
        foreach (string operation in new[] { "open", "validate", "format", "export", "preview", "coverage" })
        {
            string[] options = operation is "format" or "export" or "preview" ? ["--output", operation] : [];
            await Assert.That(await io.RunAsync(["artifact", operation, "--definition", "definition.json", "--artifact", artifact,
                "--input", "prepared", "--answers", "answers.json", "--machine", .. options])).IsEqualTo(CliExitCode.Success);
            await Assert.That(CliMachineContractVerifier.Validate(io.StandardOutput)).IsEmpty();
            using JsonDocument receipt = JsonDocument.Parse(io.StandardOutput);
            await Assert.That(receipt.RootElement.GetProperty("schemaVersion").GetString()).IsEqualTo("islamu-configurator-command/v0.0.1");
            await Assert.That(receipt.RootElement.TryGetProperty("assessment", out _)).IsFalse();
            await Assert.That(Encoding.UTF8.GetString(io.StandardOutput)).DoesNotContain("private-answer-marker");
        }
        await Assert.That(await io.RunAsync("artifact", "format", "--definition", "definition.json", "--artifact", artifact,
            "--input", "prepared", "--answers", "answers.json", "--output", "canonical", "--format", format, "--machine"))
            .IsEqualTo(CliExitCode.Success);
        await Assert.That(io.Files["canonical"]).IsEquivalentTo(io.Files["prepared"]);
        io.Files["prepared"] = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(io.Files["prepared"])
            .Replace("marker", "tampered", StringComparison.Ordinal));
        await Assert.That(await io.RunAsync("artifact", "validate", "--definition", "definition.json", "--artifact", artifact,
            "--input", "prepared", "--answers", "answers.json", "--machine")).IsEqualTo(CliExitCode.Data);
        await Assert.That(Encoding.UTF8.GetString(io.StandardOutput)).DoesNotContain("tampered");
    }

    [Test]
    public async Task NoncanonicalJsonCanBeFormattedAndUnknownPropertiesFail()
    {
        var io = new CliFixture();
        io.Files["prepared"] = """{ "name": "private-answer-marker", "enabled": true }"""u8.ToArray();
        await Assert.That(await io.RunAsync("artifact", "format", "--definition", "definition.json", "--artifact", "config",
            "--input", "prepared", "--answers", "answers.json", "--output", "canonical", "--machine")).IsEqualTo(CliExitCode.Success);
        await Assert.That(Encoding.UTF8.GetString(io.Files["canonical"])).IsEqualTo("""{"enabled":true,"name":"private-answer-marker"}""");
        io.Files["prepared"] = """{"name":"private-answer-marker","enabled":true,"extra":true}"""u8.ToArray();
        await Assert.That(await io.RunAsync("artifact", "validate", "--definition", "definition.json", "--artifact", "config",
            "--input", "prepared", "--answers", "answers.json", "--machine")).IsEqualTo(CliExitCode.Data);
    }

    [Test]
    public async Task JsonListProofWorksAndMalformedDiffBaselineFails()
    {
        var io = new CliFixture();
        io.Files["definition.json"] = Encoding.UTF8.GetBytes(CliFixture.Definition.Replace(
            "\"id\":\"markdown\",\"format\":\"markdown\"", "\"id\":\"markdown\",\"format\":\"json\"", StringComparison.Ordinal));
        await Assert.That(await io.ArtifactAsync("create", "markdown", "--output", "prepared")).IsEqualTo(CliExitCode.Success);
        await Assert.That(await io.RunAsync("artifact", "open", "--definition", "definition.json", "--artifact", "markdown",
            "--input", "prepared", "--answers", "answers.json", "--machine")).IsEqualTo(CliExitCode.Success);
        io.Files["baseline"] = "{bad}"u8.ToArray();
        await Assert.That(await io.RunAsync("artifact", "diff", "--definition", "definition.json", "--artifact", "markdown",
            "--input", "prepared", "--answers", "answers.json", "--baseline", "baseline", "--machine")).IsEqualTo(CliExitCode.Data);
        io.Files["prepared"] = """[{"a":1,"a":2}]"""u8.ToArray();
        await Assert.That(await io.RunAsync("artifact", "validate", "--definition", "definition.json", "--artifact", "markdown",
            "--input", "prepared", "--answers", "answers.json", "--machine")).IsEqualTo(CliExitCode.Data);
    }
}
