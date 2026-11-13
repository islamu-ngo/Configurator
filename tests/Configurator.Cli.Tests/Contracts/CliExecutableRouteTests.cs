using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Configurator.Cli.Tests;

public sealed class CliExecutableRouteTests
{
    // Literal public-contract cases. Registry enumeration below detects omissions only.
    private static readonly Route[] Routes =
    [
        new("catalogue list", "--definition definition.json --output result.json", "definition output", "catalogue", "output", "written"),
        new("catalogue show", "--definition definition.json --key service.name --output result.json", "definition key output", "catalogue", "output", "written"),
        new("catalogue describe", "--definition definition.json --key service.name --output result.json", "definition key output", "catalogue", "output", "written"),
        new("env render", "--definition definition.json --artifact environment --input answers.json --output result.json", "definition artifact input output", "definition-artifact", "output", "written"),
        new("env validate", "--definition definition.json --artifact environment --input answers.json", "definition artifact input", "definition-artifact", "input", "none"),
        new("artifact create", "--definition definition.json --artifact config --input answers.json --output result.json", "definition artifact input output", "definition-artifact", "output", "written"),
        new("artifact open", "--definition definition.json --artifact config --input answers.json", "definition artifact input", "definition-artifact", "input", "none"),
        new("artifact validate", "--definition definition.json --artifact config --input answers.json", "definition artifact input", "definition-artifact", "input", "none"),
        new("artifact format", "--definition definition.json --artifact config --input existing.json --answers answers.json --output result.json", "definition artifact input output", "definition-artifact", "output", "written"),
        new("artifact diff", "--definition definition.json --artifact config --input answers.json --baseline baseline.json", "definition artifact input baseline", null, null, null),
        new("artifact coverage", "--definition definition.json --artifact config --input answers.json", "definition artifact input", null, null, null),
        new("artifact export", "--definition definition.json --artifact config --input answers.json --output result.json", "definition artifact input output", "definition-artifact", "output", "written"),
        new("artifact import", "--definition definition.json --artifact request --input answers.json --output result.json --expected-revision absent --revision-field expectedRevision", "definition artifact input output expected-revision revision-field", "definition-artifact", "output", "written"),
        new("artifact check-existing", "--definition definition.json --artifact config --input existing.json", "definition artifact input", null, null, null),
        new("artifact compare", "--definition definition.json --artifact config --input existing.json", "definition artifact input", null, null, null),
        new("artifact prefill", "--definition definition.json --artifact config --input existing.json --output result.json", "definition artifact input output", "answer-snapshot", "output", "written"),
        new("artifact bundle-check", "--definition definition.json --artifact config --artifact yaml --input answers.json", "definition artifact input", "definition-artifact", "none", "none", 2),
        new("artifact bundle-create", "--definition definition.json --artifact config --artifact yaml --input answers.json --output bundle", "definition artifact input output", "definition-artifact", "output", "written", 2),
        new("artifact preview", "--definition definition.json --artifact config --input answers.json --output result.json", "definition artifact input output", "definition-artifact", "output", "written"),
        new("doctor", "", "", "doctor-report", "none", "none"),
        new("definition init", "--root {root} --project-id example.route --name Route --application-version 1", "project-id name application-version", "authoring-source", "output", "written"),
        new("definition check", "--source {source}", "", "compiled-definition", "input", "none"),
        new("definition lint", "--source {source} --detail diagnostics", "", "compiled-definition", "input", "none"),
        new("definition schema", "--kind command", "", null, null, null),
        new("definition build", "--source {source}", "", "compiled-definition", "output", "written"),
        new("definition revise", "--source {source} --output {candidate}", "", "authoring-source", "output", "written"),
        new("secret generate", "--definition definition.json --pointer /password --input answers.json --output result.json", "definition pointer input output", "answer-snapshot", "output", "written"),
        new("secret derive", "--definition definition.json --pointer /password --input answers.json --output result.json --secret-input-file secret.txt", "definition pointer input output", "answer-snapshot", "output", "written"),
        new("definition answer-schema", "--definition definition.json --output result.json", "definition output", "answer-schema", "output", "written")
    ];

    [Test]
    public async Task EveryExecutableRegistrationHasAnIndependentBehavioralCase()
    {
        string[] actual = CliCommandRegistry.Families.SelectMany(family => family.Operations.Select(operation =>
            family.Name == "doctor" ? "doctor" : family.Name + " " + operation.Name)).Order(StringComparer.Ordinal).ToArray();
        await Assert.That(actual).IsEquivalentTo(Routes.Select(route => route.Path).Order(StringComparer.Ordinal).ToArray());
    }

    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(2)]
    [Arguments(3)]
    [Arguments(4)]
    [Arguments(5)]
    [Arguments(6)]
    [Arguments(7)]
    [Arguments(8)]
    [Arguments(9)]
    [Arguments(10)]
    [Arguments(11)]
    [Arguments(12)]
    [Arguments(13)]
    [Arguments(14)]
    [Arguments(15)]
    [Arguments(16)]
    [Arguments(17)]
    [Arguments(18)]
    [Arguments(19)]
    [Arguments(20)]
    [Arguments(21)]
    [Arguments(22)]
    [Arguments(23)]
    [Arguments(24)]
    [Arguments(25)]
    [Arguments(26)]
    [Arguments(27)]
    [Arguments(28)]
    public async Task ExecutableRouteHonorsIndependentInputEffectAndReceiptContract(int index)
    {
        Route route = Routes[index];
        string directory = Path.Combine(Path.GetTempPath(), "configurator-routes-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string source = Path.Combine(directory, "source.json");
            string candidate = Path.Combine(directory, "candidate.json");
            File.WriteAllText(source, CliFixture.Definition);
            var fixture = new CliFixture { AuthoringFiles = new PublicDefinitionFiles() };
            fixture.Files["existing.json"] = """{"enabled":true,"name":"private-answer-marker"}"""u8.ToArray();
            fixture.Files["secret.txt"] = "abc"u8.ToArray();
            if (route.Path.StartsWith("secret ", StringComparison.Ordinal))
            {
                JsonObject definition = JsonNode.Parse(CliFixture.Definition)!.AsObject();
                definition["configuration"]!["properties"]!["password"] = JsonNode.Parse(
                    index == 26
                        ? """{"type":"string","x-configurator":{"sensitivity":"secret","secret":{"kind":"password","acquisition":"local","generation":{"length":32}}}}"""
                        : """{"type":"string","x-configurator":{"sensitivity":"secret","secret":{"kind":"digest","acquisition":"local","algorithm":"SHA256","encoding":"hex","generation":{"input":"entered"}}}}""");
                fixture.Files["definition.json"] = Encoding.UTF8.GetBytes(definition.ToJsonString());
            }
            if (route.Path == "definition revise")
            {
                await Assert.That(await fixture.RunAsync("definition", "build", "--source", source, "--machine"))
                    .IsEqualTo(CliExitCode.Success);
                File.WriteAllText(source, CliFixture.Definition.Replace("Example service", "Revised service", StringComparison.Ordinal));
            }
            byte[] originalSource = File.ReadAllBytes(source);
            Dictionary<string, byte[]> before = fixture.Files.ToDictionary(item => item.Key, item => item.Value.ToArray());
            string[] options = route.Options.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(value => value.Replace("{root}", directory, StringComparison.Ordinal)
                    .Replace("{source}", source, StringComparison.Ordinal)
                    .Replace("{candidate}", candidate, StringComparison.Ordinal)).ToArray();
            string[] arguments = [.. route.Path.Split(' '), .. options, "--machine"];

            await Assert.That(await fixture.RunAsync(arguments)).IsEqualTo(CliExitCode.Success);
            await CheckEnvelope(fixture, 0);
            using (JsonDocument receipt = JsonDocument.Parse(fixture.StandardOutput))
            {
                JsonElement root = receipt.RootElement;
                await Assert.That(root.GetProperty("invocation").GetProperty("commandFamily").GetString()).IsEqualTo(route.Path.Split(' ')[0]);
                await Assert.That(root.GetProperty("invocation").GetProperty("operation").GetString()).IsEqualTo(route.Path.Split(' ').Last());
                await Assert.That(root.GetProperty("completion").GetString()).IsEqualTo("completed");
                JsonElement artifacts = root.GetProperty("artifacts");
                await Assert.That(artifacts.GetArrayLength()).IsEqualTo(route.Kind is null ? 0 : route.Count);
                foreach (JsonElement artifact in artifacts.EnumerateArray())
                {
                    await Assert.That(artifact.GetProperty("kind").GetString()).IsEqualTo(route.Kind);
                    await Assert.That(artifact.GetProperty("pathIntent").GetString()).IsEqualTo(route.Intent);
                    await Assert.That(artifact.GetProperty("writeStatus").GetString()).IsEqualTo(route.Status);
                    await Assert.That(artifact.GetProperty("sensitivity").GetString())
                        .IsEqualTo(route.Path.StartsWith("catalogue ", StringComparison.Ordinal)
                            || route.Path.StartsWith("definition ", StringComparison.Ordinal) || route.Path == "doctor" ? "public" : "restricted");
                }
                if (route.Path == "definition schema")
                    await Assert.That(root.GetProperty("schema").GetProperty("id").GetString()).IsEqualTo("urn:islamu:configurator:cli:v0.0.1");
                if (route.Path == "artifact compare")
                {
                    await Assert.That(root.GetProperty("assessment").GetProperty("compatibility").GetString()).IsEqualTo("compatible");
                    await Assert.That(root.GetProperty("assessment").GetProperty("coverage").GetString()).IsEqualTo("complete");
                }
                if (route.Path == "definition lint")
                    await Assert.That(root.GetProperty("detail").GetProperty("kind").GetString()).IsEqualTo("diagnostics");
            }
            foreach ((string path, byte[] bytes) in before)
                await Assert.That(fixture.Files[path].SequenceEqual(bytes)).IsTrue();
            await Assert.That(File.ReadAllBytes(source).SequenceEqual(originalSource)).IsTrue();
            string[] added = fixture.Files.Keys.Except(before.Keys).Order(StringComparer.Ordinal).ToArray();
            await Assert.That(added).IsEquivalentTo(route.Path == "artifact bundle-create"
                ? new[] { Path.Combine("bundle", "service.json"), Path.Combine("bundle", "service.yaml") }
                : route.Status == "written" && (!route.Path.StartsWith("definition ", StringComparison.Ordinal)
                    || route.Path == "definition answer-schema") ? ["result.json"] : []);
            if (route.Path is "artifact create" or "artifact export" or "artifact preview" or "artifact format")
                await Assert.That(Encoding.UTF8.GetString(fixture.Files["result.json"])).IsEqualTo("""{"enabled":true,"name":"private-answer-marker"}""");
            if (route.Path == "artifact import")
                await Assert.That(Encoding.UTF8.GetString(fixture.Files["result.json"])).IsEqualTo("""{"expectedRevision":"absent","name":"private-answer-marker"}""");
            if (route.Path == "secret derive")
            {
                using JsonDocument answers = JsonDocument.Parse(fixture.Files["result.json"]);
                await Assert.That(answers.RootElement.GetProperty("values").GetProperty("password").GetString())
                    .IsEqualTo("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad");
            }
            if (route.Path == "definition init")
                await Assert.That(File.Exists(Path.Combine(directory, "configurator.yaml"))).IsTrue();
            if (route.Path == "definition build")
                await Assert.That(File.Exists(Path.Combine(directory, "dist", "configurator", "revisions", "1.json"))).IsTrue();
            if (route.Path == "definition revise")
                await Assert.That(File.Exists(candidate)).IsTrue();

            // Each required input is removed independently; repeated artifact selection is removed as a group.
            foreach (string required in route.Required.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var missing = new List<string>();
                for (int position = 0; position < options.Length; position++)
                {
                    if (options[position] == "--" + required) { position++; continue; }
                    missing.Add(options[position]);
                }
                await Assert.That(await fixture.RunAsync([.. route.Path.Split(' '), .. missing, "--machine"])).IsEqualTo(CliExitCode.Usage);
                await CheckEnvelope(fixture, 64);
            }
            foreach (string[] invalid in new[] { new[] { "--unknown-route-option", "synthetic-private-option" }, new[] { "--text" }, new[] { "--machine" } })
            {
                await Assert.That(await fixture.RunAsync([.. arguments, .. invalid])).IsEqualTo(CliExitCode.Usage);
                await CheckEnvelope(fixture, 64);
            }
            await Assert.That(fixture.Files.Keys.Order(StringComparer.Ordinal).ToArray())
                .IsEquivalentTo(before.Keys.Concat(added).Order(StringComparer.Ordinal).ToArray());
            if (options.Contains("--definition", StringComparer.Ordinal))
            {
                // Valid grammar with malformed data must reach the data boundary, without publishing.
                fixture.Files["definition.json"] = "{"u8.ToArray();
                await Assert.That(await fixture.RunAsync(arguments)).IsEqualTo(CliExitCode.Data);
                await CheckEnvelope(fixture, 65);
            }
            if (options.Contains("--source", StringComparer.Ordinal))
            {
                File.WriteAllText(source, "{");
                await Assert.That(await fixture.RunAsync(arguments)).IsEqualTo(CliExitCode.Data);
                await CheckEnvelope(fixture, 65);
            }
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static async Task CheckEnvelope(CliFixture fixture, int exit)
    {
        await Assert.That(fixture.StandardError).IsEmpty();
        await Assert.That(CliMachineContractVerifier.Validate(fixture.StandardOutput)).IsEmpty();
        await Assert.That(fixture.StandardOutput.Count(value => value == (byte)'\n')).IsEqualTo(1);
        string output = Encoding.UTF8.GetString(fixture.StandardOutput);
        await Assert.That(output).DoesNotContain("private-answer-marker");
        await Assert.That(output).DoesNotContain("synthetic-private-option");
        await Assert.That(output).DoesNotContain("configurator-routes-");
        using JsonDocument receipt = JsonDocument.Parse(fixture.StandardOutput);
        await Assert.That(receipt.RootElement.GetProperty("exitCode").GetInt32()).IsEqualTo(exit);
        await Assert.That(receipt.RootElement.GetProperty("targetAcceptance").GetString()).IsEqualTo("unassessed");
        await Assert.That(receipt.RootElement.GetProperty("delivery").GetString()).IsEqualTo("attempt");
    }

    private sealed record Route(string Path, string Options, string Required, string? Kind, string? Intent, string? Status, int Count = 1);
}
