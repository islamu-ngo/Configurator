using System.Text;

namespace Configurator.Cli.Tests;

public sealed class SetupCliParsingTests
{
    [Test]
    public async Task PublicSecretLikePathAndUppercaseFieldIdAreAccepted()
    {
        var io = new CliFixture();
        io.Files["token-fixture.yaml"] = Encoding.UTF8.GetBytes(CliFixture.Definition.Replace(
            "service.name", "SERVICE_TOKEN", StringComparison.Ordinal));

        await Assert.That(await io.RunAsync("catalogue", "show", "--definition", "token-fixture.yaml",
            "--key", "SERVICE_TOKEN", "--dry-run", "--machine")).IsEqualTo(SetupCliExitCode.Success);
        await Assert.That(io.MaximumRead).IsGreaterThan(0);
        await Assert.That(SetupCliMachineContractVerifier.Validate(io.StandardOutput)).IsEmpty();
        await Assert.That(io.StandardError).IsEmpty();
    }

    [Test]
    public async Task EqualsOptionsBindRepeatedBundleSelections()
    {
        var io = new CliFixture();

        await Assert.That(await io.RunAsync("artifact", "bundle-check", "--definition=definition.json",
            "--artifact=config", "--artifact=yaml", "--input=answers.json", "--machine"))
            .IsEqualTo(SetupCliExitCode.Success);
        await Assert.That(SetupCliMachineContractVerifier.Validate(io.StandardOutput)).IsEmpty();
        await Assert.That(Encoding.UTF8.GetString(io.StandardOutput)).DoesNotContain("private-answer-marker");
        await Assert.That(io.StandardError).IsEmpty();
    }

    [Test]
    [Arguments("-")]
    [Arguments("-answers.json")]
    public async Task StdinAndDashPrefixedPathsReachTheInputBoundary(string path)
    {
        var io = new CliFixture();
        io.Files[path] = Encoding.UTF8.GetBytes(CliFixture.Answers);

        await Assert.That(await io.RunAsync("artifact", "validate", "--definition", "definition.json",
            "--artifact", "config", "--input=" + path, "--machine")).IsEqualTo(SetupCliExitCode.Success);
        await Assert.That(io.MaximumRead).IsGreaterThan(0);
        await Assert.That(SetupCliMachineContractVerifier.Validate(io.StandardOutput)).IsEmpty();
        await Assert.That(Encoding.UTF8.GetString(io.StandardOutput)).DoesNotContain("private-answer-marker");
    }

    [Test]
    public async Task SeparateStdinDashReachesTheInputBoundary()
    {
        var io = new CliFixture();
        io.Files["-"] = Encoding.UTF8.GetBytes(CliFixture.Answers);

        await Assert.That(await io.RunAsync("artifact", "validate", "--definition", "definition.json",
            "--artifact", "config", "--input", "-", "--machine")).IsEqualTo(SetupCliExitCode.Success);
        await Assert.That(io.MaximumRead).IsGreaterThan(0);
        await Assert.That(SetupCliMachineContractVerifier.Validate(io.StandardOutput)).IsEmpty();
    }

    [Test]
    public async Task MachineOptionSpellingInsideAnEqualsValueDoesNotSelectMachineMode()
    {
        var io = new CliFixture();
        io.Files["--machine"] = Encoding.UTF8.GetBytes(CliFixture.Answers);

        await Assert.That(await io.RunAsync("artifact", "validate", "--definition=definition.json",
            "--artifact=config", "--input=--machine", "--text")).IsEqualTo(SetupCliExitCode.Success);
        await Assert.That(io.MaximumRead).IsGreaterThan(0);
        await Assert.That(Encoding.UTF8.GetString(io.StandardOutput))
            .DoesNotContain("islamu-configurator-command/v3");
        await Assert.That(io.StandardError).IsEmpty();
    }

    [Test]
    public async Task EndOfOptionsDoesNotTurnFollowingOperandsIntoModeFlags()
    {
        var io = new CliFixture();

        await Assert.That(await io.RunAsync("doctor", "--", "--machine")).IsEqualTo(SetupCliExitCode.Usage);
        await Assert.That(io.MaximumRead).IsEqualTo(0);
        await Assert.That(Encoding.UTF8.GetString(io.StandardOutput))
            .DoesNotContain("islamu-configurator-command/v3");
        await Assert.That(Encoding.UTF8.GetString(io.StandardError))
            .DoesNotContain("islamu-configurator-command/v3");
    }

    [Test]
    public async Task EmptyEndOfOptionsDoesNotCreateAnExtraOperand()
    {
        var io = new CliFixture();

        await Assert.That(await io.RunAsync("artifact", "validate", "--definition", "definition.json",
            "--artifact", "config", "--input", "answers.json", "--machine", "--"))
            .IsEqualTo(SetupCliExitCode.Success);
        await Assert.That(SetupCliMachineContractVerifier.Validate(io.StandardOutput)).IsEmpty();
    }

    [Test]
    public async Task UnknownPrivateFlagsControlsCaseVariantsAndTailsRejectWithoutInputReads()
    {
        string[][] rejected =
        [
            ["doctor", "--machine", "--password", "synthetic-private-marker"],
            ["doctor", "--machine", "--token=synthetic-private-marker"],
            ["doctor", "--machine", "\u001b]0;synthetic-private-marker\u0007"],
            ["Doctor", "--machine"],
            ["artifact", "Validate", "--machine"],
            ["doctor", "--Machine", "--machine"],
            ["doctor", "--machine", "synthetic-private-marker"],
            ["doctor", "--machine", "--", "synthetic-private-marker"]
        ];

        foreach (string[] arguments in rejected)
            await AssertRejectedBeforeInput(arguments);
    }

    [Test]
    public async Task SingletonDuplicatesRejectAcrossSeparateAndEqualsSpellings()
    {
        string[][] rejected =
        [
            ["doctor", "--machine", "--machine"],
            ["doctor", "--text", "--text", "--machine"],
            ["artifact", "validate", "--definition=definition.json", "--definition", "other.json",
                "--artifact=config", "--input=answers.json", "--machine"],
            ["artifact", "validate", "--definition", "definition.json", "--artifact=config", "--artifact", "yaml",
                "--input=answers.json", "--machine"]
        ];

        foreach (string[] arguments in rejected)
            await AssertRejectedBeforeInput(arguments);
    }

    [Test]
    public async Task ClosedValuesRequiredInputsAndConflictingStreamsRejectBeforeInputReads()
    {
        string[] input = ["artifact", "validate", "--definition", "definition.json", "--artifact", "config",
            "--input", "answers.json", "--machine"];
        string[][] rejected =
        [
            ["doctor", "--machine", "--text"],
            ["catalogue", "show", "--definition", "definition.json", "--dry-run", "--machine"],
            ["artifact", "create", "--definition", "definition.json", "--artifact", "config",
                "--input", "answers.json", "--machine"],
            [.. input, "--detail", "synthetic-private-marker"],
            [.. input, "--offset", "-1"],
            [.. input, "--offset", "2049"],
            [.. input, "--limit", "0"],
            [.. input, "--limit", "33"],
            [.. input, "--detail", "diagnostics", "--offset", "-1"],
            [.. input, "--detail", "diagnostics", "--limit", "0"],
            [.. input, "--input-revision", "-1"],
            ["artifact", "create", "--definition", "definition.json", "--artifact", "config",
                "--input", "answers.json", "--format", "xml", "--dry-run", "--machine"],
            ["artifact", "diff", "--definition", "definition.json", "--artifact", "config",
                "--input", "-", "--baseline", "-", "--machine"],
            ["artifact", "prefill", "--definition", "definition.json", "--artifact", "config",
                "--input", "-", "--answers", "-", "--output", "draft.json", "--machine"],
            ["artifact", "create", "--definition", "definition.json", "--artifact", "config",
                "--input", "answers.json", "--output", "-", "--machine"],
            ["catalogue", "list", "--definition", "definition.json", "--detail", "diagnostics",
                "--dry-run", "--machine"],
            ["artifact", "bundle-check", "--definition", "definition.json", "--artifact", "config",
                "--artifact", "config", "--input", "answers.json", "--machine"]
        ];

        foreach (string[] arguments in rejected)
            await AssertRejectedBeforeInput(arguments);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task IncompatibleForkFormatsRejectBeforeOpeningTheSource(bool explicitFormat)
    {
        string directory = Path.Combine(Path.GetTempPath(), "configurator-fork-format-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string[] arguments = ["definition", "init", "--fork-from", Path.Combine(directory, "missing.yaml"),
                "--output", Path.Combine(directory, "fork.json"), "--machine"];
            if (explicitFormat) arguments = [.. arguments, "--format", "json"];
            await AssertRejectedBeforeInput(arguments);
            await Assert.That(Directory.GetFileSystemEntries(directory)).IsEmpty();
        }
        finally { Directory.Delete(directory); }
    }

    [Test]
    public async Task HiddenFrameworkRoutesRemainRejectedWhenHelpIsRequested()
    {
        string directory = Path.Combine(Path.GetTempPath(), "configurator-grammar-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string destination = Path.Combine(directory, "synthetic-private-marker.xml");
            string[][] rejected =
            [
                ["cli", "xmldoc", "--output", destination, "--help", "--machine"],
                ["cli", "explain", "--help", "--machine"],
                ["artifact", "cli", "--help", "--machine"],
                ["definition", "cli", "--help", "--machine"]
            ];

            foreach (string[] arguments in rejected)
                await AssertRejectedBeforeInput(arguments);
            await Assert.That(Directory.GetFileSystemEntries(directory)).IsEmpty();
        }
        finally { Directory.Delete(directory); }
    }

    [Test]
    public async Task SchemaDiscoveryUsesClosedKindsWithoutFileAuthorityOrInputReads()
    {
        foreach (string? kind in new string?[] { null, "authoring", "runtime", "answers", "command" })
        {
            var io = new CliFixture();
            string[] arguments = kind is null ? ["definition", "schema", "--machine"]
                : ["definition", "schema", "--kind", kind, "--machine"];
            await Assert.That(await io.RunAsync(arguments)).IsEqualTo(SetupCliExitCode.Success);
            await Assert.That(io.MaximumRead).IsEqualTo(0);
            await Assert.That(SetupCliMachineContractVerifier.Validate(io.StandardOutput)).IsEmpty();
            using System.Text.Json.JsonDocument result = System.Text.Json.JsonDocument.Parse(io.StandardOutput);
            await Assert.That(result.RootElement.GetProperty("schema").GetProperty("kind").GetString())
                .IsEqualTo(kind ?? "authoring");
        }
        string[][] rejected =
        [
            ["definition", "schema", "--kind", "capabilities", "--machine"],
            ["definition", "schema", "--kind", "Authoring", "--machine"],
            ["definition", "schema", "--kind", "--machine"],
            ["definition", "schema", "--kind=runtime", "--kind", "answers", "--machine"],
            ["definition", "schema", "--source", "synthetic-private-marker", "--machine"],
            ["definition", "schema", "--output", "synthetic-private-marker", "--machine"],
            ["definition", "schema", "--definition", "synthetic-private-marker", "--machine"],
            ["definition", "schema", "--root", "synthetic-private-marker", "--machine"],
            ["definition", "schema", "--against", "synthetic-private-marker", "--machine"],
            ["definition", "schema", "--dry-run", "--machine"],
            ["definition", "lint", "--source", "synthetic-private-marker", "--kind", "authoring", "--machine"]
        ];
        foreach (string[] arguments in rejected) await AssertRejectedBeforeInput(arguments);
    }

    private static async Task AssertRejectedBeforeInput(string[] arguments)
    {
        var io = new CliFixture();
        var original = io.Files.ToDictionary(item => item.Key, item => item.Value.ToArray(), StringComparer.Ordinal);

        await Assert.That(await io.RunAsync(arguments)).IsEqualTo(SetupCliExitCode.Usage);
        await Assert.That(io.MaximumRead).IsEqualTo(0);
        await Assert.That(io.Files.Keys).IsEquivalentTo(original.Keys);
        foreach ((string path, byte[] bytes) in original)
            await Assert.That(io.Files[path]).IsEquivalentTo(bytes);
        await Assert.That(SetupCliMachineContractVerifier.Validate(io.StandardOutput)).IsEmpty();
        await Assert.That(io.StandardError).IsEmpty();
        await Assert.That(Encoding.UTF8.GetString(io.StandardOutput)).DoesNotContain("synthetic-private-marker");
    }
}
