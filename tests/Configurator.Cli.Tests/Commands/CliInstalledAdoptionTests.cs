using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Configurator.Cli.Tests;

public sealed class CliInstalledAdoptionTests
{
    [Test]
    public async Task CompiledCliRunsFullMaintainerAndPreparationWorkflowOutsideRepositoryCheckout()
    {
        string workspace = Path.Combine(Path.GetTempPath(), "islamu-configurator-installed-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspace);
        try
        {
            // 1. info --machine outside repository checkout (zero git invocation, 0.0.1 contracts).
            ProcessResult info = await RunCliAsync(workspace, "info", "--machine");
            await Assert.That(info.ExitCode).IsEqualTo(0);
            await Assert.That(info.StandardError).IsEmpty();
            await Assert.That(CliMachineContractVerifier.Validate(info.StandardOutput)).IsEmpty();
            using (JsonDocument infoDoc = JsonDocument.Parse(info.StandardOutput))
            {
                JsonElement infoObj = infoDoc.RootElement.GetProperty("info");
                await Assert.That(infoObj.GetProperty("authoringContractVersion").GetString()).IsEqualTo("0.0.1");
                await Assert.That(infoObj.GetProperty("runtimeSchemaVersion").GetString()).IsEqualTo("0.0.1");
                await Assert.That(infoObj.GetProperty("fixtureContractVersion").GetString()).IsEqualTo("0.0.1");
                await Assert.That(infoObj.GetProperty("version").GetString()).IsNotEmpty();
            }

            // 2. schema export --kind authoring --text produces valid JSON Schema outside checkout.
            ProcessResult schema = await RunCliAsync(workspace, "schema", "export", "--kind", "authoring", "--text");
            await Assert.That(schema.ExitCode).IsEqualTo(0);
            await Assert.That(schema.StandardError).IsEmpty();
            using (JsonDocument schemaDoc = JsonDocument.Parse(schema.StandardOutput))
            {
                await Assert.That(schemaDoc.RootElement.GetProperty("$id").GetString())
                    .IsEqualTo("urn:islamu:configurator:authoring:v0.0.1");
            }

            // 3. definition init creates a valid standalone authoring source in the isolated workspace.
            ProcessResult init = await RunCliAsync(workspace, "definition", "init",
                "--project-id", "acme.worker",
                "--name", "Acme Worker",
                "--application-version", "1.2.0",
                "--format", "json",
                "--machine");
            await Assert.That(init.ExitCode).IsEqualTo(0);
            await Assert.That(CliMachineContractVerifier.Validate(init.StandardOutput)).IsEmpty();
            string sourcePath = Path.Combine(workspace, "configurator.json");
            await Assert.That(File.Exists(sourcePath)).IsTrue();

            // Enhance scaffolded source with a guided required field and explicit output metadata so lint --quality --strict passes cleanly.
            JsonObject sourceObj = JsonNode.Parse(File.ReadAllText(sourcePath))!.AsObject();
            JsonObject config = sourceObj["configuration"]!.AsObject();
            config["required"] = new JsonArray("applicationName");
            JsonObject properties = config["properties"]!.AsObject();
            properties["applicationName"] = new JsonObject
            {
                ["type"] = "string",
                ["minLength"] = 1,
                ["title"] = "Application name",
                ["description"] = "Display identifier written to the worker configuration file."
            };
            JsonObject outputDef = sourceObj["outputs"]!["config.json"]!.AsObject();
            outputDef["id"] = "config";
            outputDef["targetUpdateMode"] = "replace";
            outputDef["restartRequirement"] = "process";
            File.WriteAllText(sourcePath, sourceObj.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n");

            // 4. definition check + definition lint --quality --strict.
            ProcessResult check = await RunCliAsync(workspace, "definition", "check", "--source", sourcePath, "--machine");
            await Assert.That(check.ExitCode).IsEqualTo(0);
            await Assert.That(CliMachineContractVerifier.Validate(check.StandardOutput)).IsEmpty();

            ProcessResult lint = await RunCliAsync(workspace, "definition", "lint", "--source", sourcePath, "--quality", "--strict", "--machine");
            await Assert.That(lint.ExitCode).IsEqualTo(0);
            await Assert.That(CliMachineContractVerifier.Validate(lint.StandardOutput)).IsEmpty();

            // 5. definition build writes canonical runtime definition.json.
            string builtV1 = Path.Combine(workspace, "dist", "configurator", "revisions", "1.json");
            ProcessResult build = await RunCliAsync(workspace, "definition", "build", "--source", sourcePath, "--machine");
            await Assert.That(build.ExitCode).IsEqualTo(0);
            await Assert.That(File.Exists(builtV1)).IsTrue();

            // 6. definition describe pages declared fields and artifacts without answers.
            ProcessResult describe = await RunCliAsync(workspace, "definition", "describe", "--source", sourcePath, "--machine");
            await Assert.That(describe.ExitCode).IsEqualTo(0);
            await Assert.That(CliMachineContractVerifier.Validate(describe.StandardOutput)).IsEmpty();
            using (JsonDocument descDoc = JsonDocument.Parse(describe.StandardOutput))
            {
                await Assert.That(descDoc.RootElement.GetProperty("definitionDescription").GetProperty("total").GetInt32())
                    .IsGreaterThan(0);
            }

            // 7. Edit source (add an optional port), revise to candidate rev 2, and compare via definition diff.
            JsonObject props = sourceObj["configuration"]!["properties"]!.AsObject();
            props["port"] = new JsonObject
            {
                ["type"] = "integer",
                ["minimum"] = 1024,
                ["maximum"] = 65535,
                ["title"] = "Listen port",
                ["description"] = "TCP port bound by the local worker."
            };
            File.WriteAllText(sourcePath, sourceObj.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n");

            string revisedSource = Path.Combine(workspace, "configurator.rev2.json");
            ProcessResult revise = await RunCliAsync(workspace, "definition", "revise",
                "--source", sourcePath, "--against", builtV1, "--output", revisedSource, "--machine");
            await Assert.That(revise.ExitCode).IsEqualTo(0);
            await Assert.That(File.Exists(revisedSource)).IsTrue();

            ProcessResult diff = await RunCliAsync(workspace, "definition", "diff",
                "--against", builtV1, "--source", revisedSource, "--machine");
            await Assert.That(diff.ExitCode).IsEqualTo(0);
            await Assert.That(CliMachineContractVerifier.Validate(diff.StandardOutput)).IsEmpty();
            using (JsonDocument diffDoc = JsonDocument.Parse(diff.StandardOutput))
            {
                await Assert.That(diffDoc.RootElement.GetProperty("revisionDiff").GetProperty("hasBehavioralChanges").GetBoolean()).IsTrue();
                await Assert.That(diffDoc.RootElement.GetProperty("revisionDiff").GetProperty("changes").EnumerateArray()
                    .Any(c => c.GetProperty("category").GetString() == "field-added")).IsTrue();
            }

            // 8. Build rev 2 and execute synthetic fixtures via definition test.
            string builtV2 = Path.Combine(workspace, "definition.v2.json");
            ProcessResult buildV2 = await RunCliAsync(workspace, "definition", "build",
                "--source", revisedSource, "--output", builtV2, "--machine");
            await Assert.That(buildV2.ExitCode).IsEqualTo(0);

            string fixturesPath = Path.Combine(workspace, "fixtures.json");
            const string artifactId = "config";
            File.WriteAllText(fixturesPath, $$"""
            {
              "fixtureContractVersion": "0.0.1",
              "cases": [
                {
                  "id": "nominal-worker",
                  "kind": "prepare",
                  "answers": {
                    "answerContractVersion": "0.0.1",
                    "values": { "applicationName": "acme-worker-prod", "port": 8080 },
                    "omitted": []
                  },
                  "expect": {
                    "ready": true,
                    "artifacts": [
                      { "id": "{{artifactId}}", "contentUtf8": "{\"applicationName\":\"acme-worker-prod\",\"port\":8080}" }
                    ]
                  }
                }
              ]
            }
            """);

            ProcessResult testRun = await RunCliAsync(workspace, "definition", "test",
                "--source", revisedSource, "--fixtures", fixturesPath, "--machine");
            await Assert.That(testRun.ExitCode).IsEqualTo(0);
            await Assert.That(CliMachineContractVerifier.Validate(testRun.StandardOutput)).IsEmpty();
            using (JsonDocument testDoc = JsonDocument.Parse(testRun.StandardOutput))
            {
                await Assert.That(testDoc.RootElement.GetProperty("fixtureReport").GetProperty("passed").GetBoolean()).IsTrue();
            }

            // 9. artifact create prepares the declared output with exact expected bytes.
            string answersPath = Path.Combine(workspace, "answers.json");
            File.WriteAllText(answersPath, """{"answerContractVersion":"0.0.1","values":{"applicationName":"acme-worker-prod","port":8080},"omitted":[]}""");
            string preparedPath = Path.Combine(workspace, "prepared.json");

            ProcessResult create = await RunCliAsync(workspace, "artifact", "create",
                "--definition", builtV2, "--artifact", artifactId,
                "--input", answersPath, "--output", preparedPath, "--machine");
            if (OperatingSystem.IsLinux())
            {
                await Assert.That(create.ExitCode).IsEqualTo(0);
                await Assert.That(CliMachineContractVerifier.Validate(create.StandardOutput)).IsEmpty();
                await Assert.That(File.ReadAllText(preparedPath)).IsEqualTo("""{"applicationName":"acme-worker-prod","port":8080}""");
            }
            else
            {
                await Assert.That(create.ExitCode).IsEqualTo(4);
            }
        }
        finally
        {
            if (Directory.Exists(workspace)) Directory.Delete(workspace, recursive: true);
        }
    }

    private static async Task<ProcessResult> RunCliAsync(string workingDirectory, params string[] arguments)
    {
        string binary = Path.Combine(AppContext.BaseDirectory, OperatingSystem.IsWindows() ? "Configurator.Cli.exe" : "Configurator.Cli");
        var startInfo = new ProcessStartInfo
        {
            FileName = File.Exists(binary) ? binary : "dotnet",
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        if (!File.Exists(binary))
            startInfo.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "Configurator.Cli.dll"));
        foreach (string arg in arguments)
            startInfo.ArgumentList.Add(arg);

        using Process process = Process.Start(startInfo)!;
        using var stdout = new MemoryStream();
        using var stderr = new MemoryStream();
        Task outCopy = process.StandardOutput.BaseStream.CopyToAsync(stdout);
        Task errCopy = process.StandardError.BaseStream.CopyToAsync(stderr);
        await Task.WhenAll(outCopy, errCopy, process.WaitForExitAsync());
        return new(process.ExitCode, stdout.ToArray(), stderr.ToArray());
    }

    private sealed record ProcessResult(int ExitCode, byte[] StandardOutput, byte[] StandardError);
}
