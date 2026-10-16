using System.Text;

namespace Configurator.Cli.Tests;

public sealed class SetupCliPreparedArtifactTests
{
    [Test]
    [Arguments("config", "json")]
    [Arguments("yaml", "yaml")]
    [Arguments("environment", "dotenv")]
    [Arguments("markdown", "markdown")]
    public async Task PreparedArtifactReopensValidatesAndFormatsAgainstAnswerProof(string artifact, string format)
    {
        var io = new CliFixture();
        await Assert.That(await io.ArtifactAsync("create", artifact, "--output", "prepared")).IsEqualTo(SetupCliExitCode.Success);
        foreach (string operation in new[] { "open", "validate", "format", "export", "preview", "coverage" })
        {
            string[] options = operation is "format" or "export" or "preview" ? ["--output", operation] : [];
            await Assert.That(await io.RunAsync(["artifact", operation, "--definition", "definition.json", "--artifact", artifact,
                "--input", "prepared", "--answers", "answers.json", "--machine", .. options])).IsEqualTo(SetupCliExitCode.Success);
            await Assert.That(SetupCliMachineContractVerifier.Validate(io.StandardOutput)).IsEmpty();
            await Assert.That(Encoding.UTF8.GetString(io.StandardOutput)).DoesNotContain("private-answer-marker");
        }
        await Assert.That(await io.RunAsync("artifact", "format", "--definition", "definition.json", "--artifact", artifact,
            "--input", "prepared", "--answers", "answers.json", "--output", "canonical", "--format", format, "--machine"))
            .IsEqualTo(SetupCliExitCode.Success);
        await Assert.That(io.Files["canonical"]).IsEquivalentTo(io.Files["prepared"]);
        io.Files["prepared"] = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(io.Files["prepared"])
            .Replace("marker", "tampered", StringComparison.Ordinal));
        await Assert.That(await io.RunAsync("artifact", "validate", "--definition", "definition.json", "--artifact", artifact,
            "--input", "prepared", "--answers", "answers.json", "--machine")).IsEqualTo(SetupCliExitCode.Data);
        await Assert.That(Encoding.UTF8.GetString(io.StandardOutput)).DoesNotContain("tampered");
    }

    [Test]
    public async Task NoncanonicalJsonCanBeFormattedAndUnknownPropertiesFail()
    {
        var io = new CliFixture();
        io.Files["prepared"] = """{ "name": "private-answer-marker", "enabled": true }"""u8.ToArray();
        await Assert.That(await io.RunAsync("artifact", "format", "--definition", "definition.json", "--artifact", "config",
            "--input", "prepared", "--answers", "answers.json", "--output", "canonical", "--machine")).IsEqualTo(SetupCliExitCode.Success);
        await Assert.That(Encoding.UTF8.GetString(io.Files["canonical"])).IsEqualTo("""{"enabled":true,"name":"private-answer-marker"}""");
        io.Files["prepared"] = """{"name":"private-answer-marker","enabled":true,"extra":true}"""u8.ToArray();
        await Assert.That(await io.RunAsync("artifact", "validate", "--definition", "definition.json", "--artifact", "config",
            "--input", "prepared", "--answers", "answers.json", "--machine")).IsEqualTo(SetupCliExitCode.Data);
    }

    [Test]
    public async Task JsonListProofWorksAndMalformedDiffBaselineFails()
    {
        var io = new CliFixture();
        io.Files["definition.json"] = Encoding.UTF8.GetBytes(CliFixture.Definition.Replace(
            "\"id\":\"markdown\",\"format\":\"markdown\"", "\"id\":\"markdown\",\"format\":\"json\"", StringComparison.Ordinal));
        await Assert.That(await io.ArtifactAsync("create", "markdown", "--output", "prepared")).IsEqualTo(SetupCliExitCode.Success);
        await Assert.That(await io.RunAsync("artifact", "open", "--definition", "definition.json", "--artifact", "markdown",
            "--input", "prepared", "--answers", "answers.json", "--machine")).IsEqualTo(SetupCliExitCode.Success);
        io.Files["baseline"] = "{bad}"u8.ToArray();
        await Assert.That(await io.RunAsync("artifact", "diff", "--definition", "definition.json", "--artifact", "markdown",
            "--input", "prepared", "--answers", "answers.json", "--baseline", "baseline", "--machine")).IsEqualTo(SetupCliExitCode.Data);
        io.Files["prepared"] = """[{"a":1,"a":2}]"""u8.ToArray();
        await Assert.That(await io.RunAsync("artifact", "validate", "--definition", "definition.json", "--artifact", "markdown",
            "--input", "prepared", "--answers", "answers.json", "--machine")).IsEqualTo(SetupCliExitCode.Data);
    }
}
