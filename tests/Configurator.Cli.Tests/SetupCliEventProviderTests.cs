using System.Text;

namespace Configurator.Cli.Tests;

public sealed class SetupCliEventProviderTests
{
    [Test]
    public async Task UnknownProviderCannotPublishAndCorrectedProviderPreparesExactWireValue()
    {
        var io = new CliFixture();
        using Stream input = typeof(SetupCliEventProviderTests).Assembly.GetManifestResourceStream("EventDefinition")!;
        using var bytes = new MemoryStream();
        input.CopyTo(bytes);
        io.Files["definition.json"] = bytes.ToArray();
        io.Files["answers.json"] = Answers("unsupported-synthetic-provider");

        await Assert.That(await io.ArtifactAsync("create", "environment", "--output", "prepared.env"))
            .IsEqualTo(SetupCliExitCode.Validation);
        await Assert.That(io.Files.ContainsKey("prepared.env")).IsFalse();
        await Assert.That(Encoding.UTF8.GetString(io.StandardOutput)).DoesNotContain("unsupported-synthetic-provider");

        io.Files["existing.env"] = "AUTHENTICATION_PROVIDER=unsupported-synthetic-provider\nSETUP_SECRET=synthetic-setup-secret\n"u8.ToArray();
        await Assert.That(await io.RunAsync("artifact", "check-existing", "--definition", "definition.json",
            "--artifact", "environment", "--input", "existing.env", "--machine"))
            .IsEqualTo(SetupCliExitCode.Data);
        await Assert.That(Encoding.UTF8.GetString(io.StandardOutput)).Contains("import-field-invalid");
        await Assert.That(Encoding.UTF8.GetString(io.StandardOutput)).DoesNotContain("unsupported-synthetic-provider");

        io.Files["answers.json"] = Answers("local");
        await Assert.That(await io.ArtifactAsync("create", "environment", "--output", "prepared.env"))
            .IsEqualTo(SetupCliExitCode.Success);
        string output = Encoding.UTF8.GetString(io.Files["prepared.env"]);
        await Assert.That(output).Contains("AUTHENTICATION_PROVIDER=local");
        await Assert.That(output).DoesNotContain("KEYCLOAK_");
        await Assert.That(Encoding.UTF8.GetString(io.StandardOutput)).DoesNotContain("synthetic-setup-secret");
    }

    private static byte[] Answers(string provider) => Encoding.UTF8.GetBytes($$"""
        {"answerContractVersion":1,"values":{"operation":"environment",
         "select.feature.platform-config":true,"env.AUTHENTICATION_PROVIDER":"{{provider}}",
         "env.SETUP_SECRET":"synthetic-setup-secret"},"omitted":[]}
        """);
}
