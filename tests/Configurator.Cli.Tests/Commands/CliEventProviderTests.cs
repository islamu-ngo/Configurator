using System.Text;
using System.Text.Json.Nodes;

namespace Configurator.Cli.Tests;

public sealed class CliEventProviderTests
{
    [Test]
    public async Task UnknownProviderCannotPublishAndCorrectedProviderPreparesExactWireValue()
    {
        var io = new CliFixture();
        using Stream input = typeof(CliEventProviderTests).Assembly.GetManifestResourceStream("EventDefinition")!;
        using var bytes = new MemoryStream();
        input.CopyTo(bytes);
        io.Files["definition.json"] = bytes.ToArray();
        io.Files["answers.json"] = Answers("unsupported-synthetic-provider");

        await Assert.That(await io.ArtifactAsync("create", "environment-standalone", "--output", "prepared.env"))
            .IsEqualTo(CliExitCode.Validation);
        await Assert.That(io.Files.ContainsKey("prepared.env")).IsFalse();
        await Assert.That(Encoding.UTF8.GetString(io.StandardOutput)).DoesNotContain("unsupported-synthetic-provider");

        io.Files["answers.json"] = Answers("local");
        io.Files["existing.env"] = "AUTHENTICATION_PROVIDER=unsupported-synthetic-provider\nSETUP_SECRET=synthetic-setup-secret\n"u8.ToArray();
        await Assert.That(await io.RunAsync("artifact", "check-existing", "--definition", "definition.json",
            "--artifact", "environment-standalone", "--input", "existing.env", "--answers", "answers.json", "--machine"))
            .IsEqualTo(CliExitCode.Validation);
        await Assert.That(Encoding.UTF8.GetString(io.StandardOutput)).Contains("answer-conflict");
        await Assert.That(Encoding.UTF8.GetString(io.StandardOutput)).DoesNotContain("unsupported-synthetic-provider");

        await Assert.That(await io.ArtifactAsync("create", "environment-standalone", "--output", "prepared.env"))
            .IsEqualTo(CliExitCode.Success);
        string output = Encoding.UTF8.GetString(io.Files["prepared.env"]);
        await Assert.That(output).Contains("AUTHENTICATION_PROVIDER=local");
        await Assert.That(output).DoesNotContain("KEYCLOAK_");
        await Assert.That(Encoding.UTF8.GetString(io.StandardOutput)).DoesNotContain("synthetic-setup-secret");
    }

    private static byte[] Answers(string provider)
    {
        using Stream input = typeof(CliEventProviderTests).Assembly.GetManifestResourceStream("EventStandaloneAnswers")!;
        JsonObject answers = JsonNode.Parse(input)!.AsObject();
        JsonObject values = answers["values"]!.AsObject();
        if (values["platform"] is not JsonObject platform) values["platform"] = platform = new JsonObject();
        platform["authenticationProvider"] = provider;
        return Encoding.UTF8.GetBytes(answers.ToJsonString());
    }
}
