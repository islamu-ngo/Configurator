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
        foreach (string operation in new[] { "validate", "create", "coverage" })
        {
            string[] options = operation == "create" ? ["--output", operation] : [];
            await Assert.That(await io.RunAsync(["artifact", operation, "--definition", "definition.json", "--artifact", artifact,
                "--input", "prepared", "--answers", "answers.json", "--machine", .. options])).IsEqualTo(CliExitCode.Success);
            await Assert.That(CliMachineContractVerifier.Validate(io.StandardOutput)).IsEmpty();
            using JsonDocument receipt = JsonDocument.Parse(io.StandardOutput);
            await Assert.That(receipt.RootElement.GetProperty("schemaVersion").GetString()).IsEqualTo("islamu-configurator-command/v0.0.1");
            await Assert.That(receipt.RootElement.TryGetProperty("assessment", out _)).IsFalse();
            await Assert.That(Encoding.UTF8.GetString(io.StandardOutput)).DoesNotContain("private-answer-marker");
        }
        await Assert.That(await io.RunAsync("artifact", "create", "--definition", "definition.json", "--artifact", artifact,
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
        await Assert.That(await io.RunAsync("artifact", "create", "--definition", "definition.json", "--artifact", "config",
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
        await Assert.That(await io.RunAsync("artifact", "validate", "--definition", "definition.json", "--artifact", "markdown",
            "--input", "prepared", "--answers", "answers.json", "--machine")).IsEqualTo(CliExitCode.Success);
        io.Files["baseline"] = "{bad}"u8.ToArray();
        await Assert.That(await io.RunAsync("artifact", "diff", "--definition", "definition.json", "--artifact", "markdown",
            "--input", "prepared", "--answers", "answers.json", "--baseline", "baseline", "--machine")).IsEqualTo(CliExitCode.Data);
        io.Files["prepared"] = """[{"a":1,"a":2}]"""u8.ToArray();
        await Assert.That(await io.RunAsync("artifact", "validate", "--definition", "definition.json", "--artifact", "markdown",
            "--input", "prepared", "--answers", "answers.json", "--machine")).IsEqualTo(CliExitCode.Data);
    }

    [Test]
    public async Task BundleCreateDraftEmitsUsableAndDeferredArtifactsWhileStrictRefuses()
    {
        var io = new CliFixture();
        byte[] authoringBytes = """
        {
          "$schema": "urn:islamu:configurator:authoring:v0.0.1",
          "identity": {
            "familyId": "66666666-6666-4666-8666-666666666666",
            "revision": 1
          },
          "project": {
            "id": "example.draft",
            "name": "Draft Example"
          },
          "applicationVersions": ["1.0.0"],
          "configuration": {
            "type": "object",
            "additionalProperties": false,
            "required": ["service", "apiToken"],
            "properties": {
              "service": { "type": "string", "minLength": 1 },
              "apiToken": {
                "type": "string",
                "minLength": 8,
                "x-configurator": {
                  "sensitivity": "secret",
                  "secret": { "kind": "opaque-token", "acquisition": "existing", "encoding": "base64url" }
                }
              }
            }
          },
          "outputs": {
            "config.json": {
              "id": "public-config",
              "format": "json",
              "targetUpdateMode": "replace",
              "restartRequirement": "process",
              "bindings": {
                "/service": "/service"
              }
            },
            "service.env": {
              "id": "secret-env",
              "format": "dotenv",
              "targetUpdateMode": "replace",
              "restartRequirement": "process",
              "bindings": {
                "SERVICE_NAME": "/service",
                "API_TOKEN": "/apiToken"
              }
            }
          }
        }
        """u8.ToArray();
        Configurator.Core.Definitions.Definition compiled =
            Configurator.Core.Definitions.Authoring.DefinitionAuthoringCompiler.Compile(
                Configurator.Core.Definitions.Authoring.DefinitionAuthoringCodec.ParseJson(authoringBytes));
        io.Files["definition.json"] = compiled.CanonicalBytes.ToArray();
        io.Files["answers.json"] = """{"answerContractVersion":"0.0.1","values":{"service":"demo-service"},"omitted":["/apiToken"]}"""u8.ToArray();

        // Strict bundle-create MUST refuse when a required secret is omitted and write zero files.
        await Assert.That(await io.RunAsync("artifact", "bundle-create", "--definition", "definition.json",
            "--artifact", "public-config", "--artifact", "secret-env",
            "--input", "answers.json", "--output", "strict-out", "--machine")).IsEqualTo(CliExitCode.Validation);
        await Assert.That(io.Files.Keys.Any(path => path.StartsWith("strict-out", StringComparison.Ordinal))).IsFalse();

        // Explicit --draft emits both independently usable and deferred artifacts with honest incomplete receipt metadata.
        await Assert.That(await io.RunAsync("artifact", "bundle-create", "--definition", "definition.json",
            "--artifact", "public-config", "--artifact", "secret-env",
            "--input", "answers.json", "--output", "draft-out", "--draft", "--machine")).IsEqualTo(CliExitCode.Success);
        await Assert.That(CliMachineContractVerifier.Validate(io.StandardOutput)).IsEmpty();

        using JsonDocument receipt = JsonDocument.Parse(io.StandardOutput);
        JsonElement root = receipt.RootElement;
        await Assert.That(root.GetProperty("preparation").GetString()).IsEqualTo("incomplete");
        await Assert.That(root.GetProperty("readiness").GetProperty("state").GetString()).IsEqualTo("incomplete");
        await Assert.That(root.GetProperty("readiness").GetProperty("missingCount").GetInt32()).IsEqualTo(2);
        await Assert.That(root.GetProperty("coverage").GetProperty("coveredCount").GetInt32()).IsEqualTo(1);
        await Assert.That(root.GetProperty("coverage").GetProperty("missingCount").GetInt32()).IsEqualTo(1);
        await Assert.That(root.GetProperty("diagnostics").EnumerateArray()
            .Any(d => d.GetProperty("code").GetString() == "secret-deferred"
                && d.GetProperty("path").GetString() == "/apiToken"
                && d.GetProperty("severity").GetString() == "warning")).IsTrue();

        JsonElement[] artifacts = root.GetProperty("artifacts").EnumerateArray().ToArray();
        await Assert.That(artifacts.Length).IsEqualTo(2);
        JsonElement publicArtifact = artifacts.Single(a => a.GetProperty("id").GetString() == "public-config");
        JsonElement deferredArtifact = artifacts.Single(a => a.GetProperty("id").GetString() == "secret-env");
        await Assert.That(publicArtifact.GetProperty("completeness").GetString()).IsEqualTo("complete");
        await Assert.That(publicArtifact.GetProperty("deferredFieldCount").GetInt32()).IsEqualTo(0);
        await Assert.That(deferredArtifact.GetProperty("completeness").GetString()).IsEqualTo("deferred");
        await Assert.That(deferredArtifact.GetProperty("deferredFieldCount").GetInt32()).IsEqualTo(1);

        await Assert.That(Encoding.UTF8.GetString(io.Files["draft-out/config.json"])).IsEqualTo("""{"service":"demo-service"}""");
        await Assert.That(Encoding.UTF8.GetString(io.Files["draft-out/service.env"])).Contains("SERVICE_NAME=demo-service");
        await Assert.That(Encoding.UTF8.GetString(io.Files["draft-out/service.env"])).Contains("API_TOKEN=");
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task BundleCreateDraftDryRunAndCompleteAnswersReportTruthfulPreparation(bool dryRun)
    {
        var io = new CliFixture();
        byte[] authoringBytes = """
        {
          "$schema": "urn:islamu:configurator:authoring:v0.0.1",
          "identity": {
            "familyId": "66666666-6666-4666-8666-666666666666",
            "revision": 1
          },
          "project": {
            "id": "example.draft",
            "name": "Draft Example"
          },
          "applicationVersions": ["1.0.0"],
          "configuration": {
            "type": "object",
            "additionalProperties": false,
            "required": ["service", "apiToken"],
            "properties": {
              "service": { "type": "string", "minLength": 1 },
              "apiToken": {
                "type": "string",
                "minLength": 8,
                "x-configurator": {
                  "sensitivity": "secret",
                  "secret": { "kind": "opaque-token", "acquisition": "existing", "encoding": "base64url" }
                }
              }
            }
          },
          "outputs": {
            "config.json": {
              "id": "public-config",
              "format": "json",
              "targetUpdateMode": "replace",
              "restartRequirement": "process",
              "bindings": { "/service": "/service" }
            },
            "service.env": {
              "id": "secret-env",
              "format": "dotenv",
              "targetUpdateMode": "replace",
              "restartRequirement": "process",
              "bindings": { "SERVICE_NAME": "/service", "API_TOKEN": "/apiToken" }
            }
          }
        }
        """u8.ToArray();
        io.Files["definition.json"] = Configurator.Core.Definitions.Authoring.DefinitionAuthoringCompiler.Compile(
            Configurator.Core.Definitions.Authoring.DefinitionAuthoringCodec.ParseJson(authoringBytes)).CanonicalBytes.ToArray();
        io.Files["answers.json"] = dryRun
            ? """{"answerContractVersion":"0.0.1","values":{"service":"demo-service"},"omitted":["/apiToken"]}"""u8.ToArray()
            : """{"answerContractVersion":"0.0.1","values":{"service":"demo-service","apiToken":"c3ludGhldGljLXRva2VuLTEyMzQ"},"omitted":[]}"""u8.ToArray();

        string[] args = dryRun
            ? ["artifact", "bundle-create", "--definition", "definition.json", "--artifact", "public-config", "--artifact", "secret-env", "--input", "answers.json", "--draft", "--dry-run", "--machine"]
            : ["artifact", "bundle-create", "--definition", "definition.json", "--artifact", "public-config", "--artifact", "secret-env", "--input", "answers.json", "--output", "complete-out", "--draft", "--machine"];

        await Assert.That(await io.RunAsync(args)).IsEqualTo(CliExitCode.Success);
        await Assert.That(CliMachineContractVerifier.Validate(io.StandardOutput)).IsEmpty();
        using JsonDocument receipt = JsonDocument.Parse(io.StandardOutput);
        if (dryRun)
        {
            await Assert.That(receipt.RootElement.GetProperty("preparation").GetString()).IsEqualTo("incomplete");
            await Assert.That(receipt.RootElement.GetProperty("artifacts")[0].GetProperty("writeStatus").GetString()).IsEqualTo("planned");
            await Assert.That(io.Files.Keys.Any(path => path.StartsWith("complete-out", StringComparison.Ordinal))).IsFalse();
        }
        else
        {
            await Assert.That(receipt.RootElement.GetProperty("preparation").GetString()).IsEqualTo("ready");
            await Assert.That(receipt.RootElement.GetProperty("artifacts").EnumerateArray()
                .All(a => a.GetProperty("completeness").GetString() == "complete" && a.GetProperty("deferredFieldCount").GetInt32() == 0)).IsTrue();
        }
    }
}
