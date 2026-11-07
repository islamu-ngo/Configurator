using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Configurator.Cli.Tests;

public sealed class CliRecursiveConformanceTests
{
    [Test]
    [Arguments("literal-map-keys-and-null", 0)]
    [Arguments("empty-map-requires-entry", 2)]
    [Arguments("map-rejects-unlisted-integer", 2)]
    [Arguments("map-rejects-string-instead-of-integer", 2)]
    [Arguments("map-retains-ordinal-case-distinction", 0)]
    [Arguments("variant-applies-initialization-without-suggestion", 0)]
    [Arguments("variant-omission-suppresses-initialization", 0)]
    [Arguments("variant-null-is-present", 0)]
    [Arguments("variant-missing-secret-is-draft-only", 3)]
    [Arguments("variant-supplied-synthetic-secret-is-complete", 0)]
    [Arguments("variant-missing-ordinary-input-is-not-a-draft", 3)]
    [Arguments("variant-rejects-unselected-branch-payload", 2)]
    [Arguments("library-item-scope-excludes-inactive-invalid-value", 0)]
    [Arguments("library-two-active-branches-retain-distinct-periods", 0)]
    [Arguments("library-inactive-required-period-is-absent", 0)]
    [Arguments("library-active-period-is-required", 3)]
    [Arguments("library-active-period-rejects-string", 2)]
    [Arguments("library-inactive-first-row-does-not-shift-item-scope", 0)]
    [Arguments("library-active-period-rejects-unlisted-integer", 2)]
    public async Task ExecutableValidatesAndPublishesOnlyCompleteCorpusOutput(string identity, int expectedExit)
    {
        using Stream casesStream = Resource("Conformance.Cases");
        using JsonDocument cases = JsonDocument.Parse(casesStream);
        JsonElement item = cases.RootElement.GetProperty("cases").EnumerateArray()
            .Single(candidate => candidate.GetProperty("id").GetString() == identity);
        string? expected = item.GetProperty("expectedJson").GetString();
        bool ready = item.GetProperty("ready").GetBoolean();
        string directory = Path.Combine(Path.GetTempPath(), "configurator-recursive-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string definitionPath = Path.Combine(directory, "definition.json");
            string answersPath = Path.Combine(directory, "answers.json");
            using Stream definitionStream = Resource(item.GetProperty("definition").GetString() switch
            {
                "root-map" => "Conformance.RootMap",
                "root-variant" => "Conformance.RootVariant",
                "synthetic-library" => "Conformance.Library",
                _ => throw new InvalidDataException("Unknown conformance definition.")
            });
            using var definitionBytes = new MemoryStream();
            definitionStream.CopyTo(definitionBytes);
            byte[] definition = definitionBytes.ToArray();
            byte[] answers = Encoding.UTF8.GetBytes(
                "{\"answerContractVersion\":\"0.0.1\",\"values\":" + item.GetProperty("values").GetRawText() +
                ",\"omitted\":" + item.GetProperty("omitted").GetRawText() + "}");
            await File.WriteAllBytesAsync(definitionPath, definition);
            await File.WriteAllBytesAsync(answersPath, answers);
            var expectedFiles = new List<string> { definitionPath, answersPath };

            // Preview is protected complete output, not a secret-deferred draft command.
            foreach (string operation in new[] { "validate", "create", "preview" })
            {
                string outputPath = Path.Combine(directory, operation + ".json");
                bool write = operation != "validate";
                string[] arguments = ["artifact", operation, "--definition", definitionPath,
                    "--artifact", "config", "--input", answersPath, "--machine",
                    .. write ? new[] { "--output", outputPath } : []];
                ProcessResult result = await Execute(arguments);
                int exit = ready && write && !OperatingSystem.IsLinux() ? 74 : expectedExit;
                await Assert.That(result.ExitCode).IsEqualTo(exit).Because(identity + ":" + operation);
                await Assert.That(result.Error).IsEmpty();
                await Assert.That(CliMachineContractVerifier.Validate(result.Output)).IsEmpty();
                await Assert.That(result.Output.Count(value => value == (byte)'\n')).IsEqualTo(1);
                string receiptText = Encoding.UTF8.GetString(result.Output);
                await Assert.That(receiptText.Contains(directory, StringComparison.Ordinal)).IsFalse();
                await Assert.That(receiptText.Contains("synthetic-token", StringComparison.Ordinal)).IsFalse();
                using JsonDocument receipt = JsonDocument.Parse(result.Output);
                JsonElement root = receipt.RootElement;
                await Assert.That(root.GetProperty("exitCode").GetInt32()).IsEqualTo(exit);
                await Assert.That(root.GetProperty("invocation").GetProperty("operation").GetString()).IsEqualTo(operation);
                await Assert.That(root.GetProperty("preparation").GetString()).IsEqualTo(ready ? "ready" : "incomplete");
                await Assert.That(root.GetProperty("targetAcceptance").GetString()).IsEqualTo("unassessed");
                await Assert.That(root.GetProperty("artifacts").GetArrayLength()).IsEqualTo(ready ? 1 : 0);

                bool published = write && expected is not null && OperatingSystem.IsLinux();
                await Assert.That(File.Exists(outputPath)).IsEqualTo(published).Because(identity + ":" + operation);
                if (published)
                {
                    expectedFiles.Add(outputPath);
                    // Compare bytes without putting even synthetic private values into assertion diagnostics.
                    await Assert.That((await File.ReadAllBytesAsync(outputPath)).AsSpan()
                        .SequenceEqual(Encoding.UTF8.GetBytes(expected!))).IsTrue().Because(identity + ":" + operation);
                    if (OperatingSystem.IsLinux())
                        await Assert.That(File.GetUnixFileMode(outputPath))
                            .IsEqualTo(UnixFileMode.UserRead | UnixFileMode.UserWrite);
                }
                if (ready)
                {
                    JsonElement artifact = root.GetProperty("artifacts")[0];
                    await Assert.That(artifact.GetProperty("id").GetString()).IsEqualTo("config");
                    await Assert.That(artifact.GetProperty("sensitivity").GetString()).IsEqualTo("restricted");
                    await Assert.That(artifact.GetProperty("writeStatus").GetString())
                        .IsEqualTo(!write ? "none" : published ? "written" : "blocked");
                    await Assert.That(artifact.GetProperty("installation").GetString())
                        .IsEqualTo(!write ? "not-applicable" : published ? "established" : "not-established");
                }
                await Assert.That((await File.ReadAllBytesAsync(definitionPath)).SequenceEqual(definition)).IsTrue();
                await Assert.That((await File.ReadAllBytesAsync(answersPath)).SequenceEqual(answers)).IsTrue();
                await Assert.That(Directory.GetFiles(directory)).IsEquivalentTo(expectedFiles.ToArray());
            }
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static Stream Resource(string name) =>
        typeof(CliRecursiveConformanceTests).Assembly.GetManifestResourceStream(name)
        ?? throw new InvalidDataException("Missing conformance resource.");

    private static async Task<ProcessResult> Execute(string[] arguments)
    {
        var info = new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory,
            OperatingSystem.IsWindows() ? "Configurator.Cli.exe" : "Configurator.Cli"))
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (string argument in arguments) info.ArgumentList.Add(argument);
        using var process = new Process { StartInfo = info };
        if (!process.Start()) throw new InvalidOperationException("process-start-failed");
        using var output = new MemoryStream();
        using var error = new MemoryStream();
        Task readOutput = process.StandardOutput.BaseStream.CopyToAsync(output);
        Task readError = process.StandardError.BaseStream.CopyToAsync(error);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
            await Task.WhenAll(readOutput, readError).WaitAsync(timeout.Token);
            return new(process.ExitCode, output.ToArray(), error.ToArray());
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            }
        }
    }

    private sealed record ProcessResult(int ExitCode, byte[] Output, byte[] Error);
}
