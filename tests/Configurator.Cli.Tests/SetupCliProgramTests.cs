using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Configurator.Cli.Tests;

public sealed class SetupCliProgramTests
{
    [Test]
    public async Task TerminalStdinRejectsWithoutWaitingForEof()
    {
        if (!OperatingSystem.IsLinux()) return;
        using var files = new NativeFixture();
        string[] command = [Path.Combine(AppContext.BaseDirectory, "Configurator.Cli"),
            "artifact", "validate", "--definition", files.Definition,
            "--artifact", "config", "--input", "-", "--machine"];
        ProcessResult result = await ExecuteHostAsync("/usr/bin/script",
            ["-q", "-e", "-c", "TERM=xterm-256color exec " + string.Join(' ', command.Select(ShellQuote)), "/dev/null"]);

        await Assert.That(result.ExitCode).IsEqualTo(74);
        await Assert.That(result.StandardError).IsEmpty();
        byte[] receipt = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(result.StandardOutput)
            .Replace("\r\n", "\n", StringComparison.Ordinal));
        await Assert.That(SetupCliMachineContractVerifier.Validate(receipt)).IsEmpty();
        await Assert.That(receipt.Count(value => value == (byte)'\n')).IsEqualTo(1);
    }

    [Test]
    public async Task RegularFileStdinAndStdoutPreserveArtifactAndReceipt()
    {
        if (!OperatingSystem.IsLinux()) return;
        using var files = new NativeFixture();
        string artifactPath = Path.Combine(Path.GetDirectoryName(files.Input)!, "service ' config.json");
        string receiptPath = Path.Combine(Path.GetDirectoryName(files.Input)!, "receipt ' result.json");
        string[] command = [Path.Combine(AppContext.BaseDirectory, "Configurator.Cli"),
            "artifact", "create", "--definition", files.Definition,
            "--artifact", "config", "--input", "-", "--output", artifactPath, "--machine"];
        ProcessResult result = await ExecuteHostAsync("/bin/sh",
            ["-c", "exec " + string.Join(' ', command.Select(ShellQuote))
                + " < " + ShellQuote(files.Input) + " > " + ShellQuote(receiptPath)]);

        await Assert.That(result.ExitCode).IsEqualTo(0);
        await Assert.That(result.StandardOutput).IsEmpty();
        await Assert.That(result.StandardError).IsEmpty();
        await Assert.That(await File.ReadAllBytesAsync(artifactPath))
            .IsEquivalentTo("""{"enabled":true,"name":"private-answer-marker"}"""u8.ToArray());
        byte[] receipt = await File.ReadAllBytesAsync(receiptPath);
        await Assert.That(SetupCliMachineContractVerifier.Validate(receipt)).IsEmpty();
        await Assert.That(receipt.Count(value => value == (byte)'\n')).IsEqualTo(1);
        using JsonDocument document = JsonDocument.Parse(receipt);
        await Assert.That(document.RootElement.GetProperty("artifacts")[0].GetProperty("writeStatus").GetString())
            .IsEqualTo("written");
    }

    [Test]
    [Arguments("TERM", false, false)]
    [Arguments("INT", false, false)]
    [Arguments("TERM", true, false)]
    [Arguments("TERM", false, true)]
    public async Task SignalDuringBoundedStdinReadPreservesFinalDelivery(string signalName, bool repeated, bool brokenOutput)
    {
        if (!OperatingSystem.IsLinux()) return;
        using var files = new NativeFixture();
        var info = new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, "Configurator.Cli"))
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (string argument in new[] { "artifact", "validate", "--definition", files.Definition,
            "--artifact", "config", "--input", "-", "--machine" })
            info.ArgumentList.Add(argument);
        using var process = new Process { StartInfo = info, EnableRaisingEvents = true };
        var exited = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        process.Exited += (_, _) => exited.TrySetResult();
        if (!process.Start()) throw new InvalidOperationException("process-start-failed");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        Task<byte[]> output = ReadAllAsync(process.StandardOutput.BaseStream);
        Task<byte[]> error = ReadAllAsync(process.StandardError.BaseStream);
        try
        {
            // This exceeds Linux's maximum pipe capacity: completion proves the child is consuming stdin.
            await process.StandardInput.BaseStream.WriteAsync(new byte[2 * 1024 * 1024], timeout.Token);
            if (brokenOutput) process.StandardOutput.Close();
            var signalInfo = new ProcessStartInfo("/bin/kill")
            {
                UseShellExecute = false
            };
            signalInfo.ArgumentList.Add("-" + signalName);
            signalInfo.ArgumentList.Add(process.Id.ToString(System.Globalization.CultureInfo.InvariantCulture));
            if (repeated) signalInfo.ArgumentList.Add(process.Id.ToString(System.Globalization.CultureInfo.InvariantCulture));
            using var signal = Process.Start(signalInfo) ?? throw new InvalidOperationException("signal-start-failed");
            await signal.WaitForExitAsync(timeout.Token);
            await exited.Task.WaitAsync(timeout.Token);
            await Assert.That(process.ExitCode).IsEqualTo(brokenOutput ? 74 : 130);
            if (brokenOutput)
            {
                try { await output.WaitAsync(timeout.Token); }
                catch (Exception exception) when (exception is IOException or ObjectDisposedException) { }
                await Assert.That(Encoding.UTF8.GetString(await error.WaitAsync(timeout.Token)))
                    .IsEqualTo("configurator: result delivery failed\n");
            }
            else
            {
                byte[] receipt = await output.WaitAsync(timeout.Token);
                await Assert.That(await error.WaitAsync(timeout.Token)).IsEmpty();
                await Assert.That(SetupCliMachineContractVerifier.Validate(receipt)).IsEmpty();
                await Assert.That(receipt.Count(value => value == (byte)'\n')).IsEqualTo(1);
                using JsonDocument result = JsonDocument.Parse(receipt);
                await Assert.That(result.RootElement.GetProperty("completion").GetString()).IsEqualTo("cancelled");
            }
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

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task FrameworkParseFailureCannotDiscloseInputPaths(bool machine)
    {
        string marker = Guid.CreateVersion7().ToString("N");
        ProcessResult result = await ExecuteAsync(
            ["artifact", "diff", "--definition", "definition.json", "--artifact", "config", "--input", $"/synthetic/{marker}.json",
             "--baseline", "-x", machine ? "--machine" : "--text"]);

        await Assert.That(Encoding.UTF8.GetString(result.StandardOutput)).DoesNotContain(marker);
        await Assert.That(Encoding.UTF8.GetString(result.StandardError)).DoesNotContain(marker);
        await Assert.That(result.ExitCode).IsEqualTo(64);
        if (machine)
            await Assert.That(SetupCliMachineContractVerifier.Validate(result.StandardOutput)).IsEmpty();
    }

    [Test]
    public async Task RestrictedEnvironmentCannotReachExecutableStandardOutput()
    {
        using var files = new NativeFixture();
        ProcessResult result = await ExecuteAsync(files.Arguments("env", "render", "environment", "--output", "-"));

        await Assert.That(Encoding.UTF8.GetString(result.StandardOutput)).DoesNotContain("private-answer-marker");
        await Assert.That(result.ExitCode).IsEqualTo(4);
    }

    [Test]
    public async Task PublicCatalogueStillReachesStandardOutput()
    {
        using var files = new NativeFixture();
        ProcessResult result = await ExecuteAsync(["catalogue", "show", "--definition", files.Definition,
            "--key", "service.name", "--output", "-"]);
        await Assert.That(result.ExitCode).IsEqualTo(0);
        using JsonDocument catalogue = JsonDocument.Parse(result.StandardOutput);
        await Assert.That(catalogue.RootElement.GetProperty("id").GetString()).IsEqualTo("service.name");
    }

    [Test]
    public async Task MachineValidationNeverReturnsInputValuesOrPaths()
    {
        string path = Path.Combine(Path.GetTempPath(), "islamu-configurator-input-" + Guid.NewGuid().ToString("N"));
        string value = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        using var files = new NativeFixture();
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new Dictionary<string, string> { ["service.name"] = value }));
        try
        {
            ProcessResult result = await ExecuteAsync(["env", "validate", "--definition", files.Definition,
                "--artifact", "environment", "--input", path, "--machine"]);
            await Assert.That(result.ExitCode).IsEqualTo(0);
            await Assert.That(result.StandardError).IsEmpty();
            await Assert.That(SetupCliMachineContractVerifier.Validate(result.StandardOutput)).IsEmpty();
            string output = Encoding.UTF8.GetString(result.StandardOutput);
            await Assert.That(output).DoesNotContain(value);
            await Assert.That(output).DoesNotContain(path);
            using JsonDocument document = JsonDocument.Parse(result.StandardOutput);
            await Assert.That(document.RootElement.GetProperty("artifacts")[0].GetProperty("sensitivity").GetString())
                .IsEqualTo("restricted");
        }
        finally { File.Delete(path); }
    }

    [Test]
    public async Task RestrictedExecutableFileOutputRequiresProvedHostProtection()
    {
        string directory = Path.Combine(Path.GetTempPath(), "islamu-configurator-output-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string target = Path.Combine(directory, "deployment.env");
        using var files = new NativeFixture();
        try
        {
            ProcessResult result = await ExecuteAsync(files.Arguments("env", "render", "environment", "--output", target, "--machine"));
            await Assert.That(result.StandardError).IsEmpty();
            await Assert.That(SetupCliMachineContractVerifier.Validate(result.StandardOutput)).IsEmpty();
            await Assert.That(Encoding.UTF8.GetString(result.StandardOutput)).DoesNotContain("private-answer-marker");
            await Assert.That(result.ExitCode).IsEqualTo(OperatingSystem.IsLinux() ? 0 : 74);
            if (OperatingSystem.IsLinux())
            {
                await Assert.That(File.GetUnixFileMode(target)).IsEqualTo(UnixFileMode.UserRead | UnixFileMode.UserWrite);
                byte[] original = await File.ReadAllBytesAsync(target);
                ProcessResult overwrite = await ExecuteAsync(files.Arguments("env", "render", "environment", "--output", target, "--machine"));
                await Assert.That(overwrite.ExitCode).IsEqualTo(74);
                await Assert.That(await File.ReadAllBytesAsync(target)).IsEquivalentTo(original);
                await Assert.That(Directory.GetFileSystemEntries(directory)).IsEquivalentTo([target]);
            }
            else
                await Assert.That(Directory.GetFileSystemEntries(directory)).IsEmpty();
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Test]
    public async Task EmptyInvocationAndHelpShowAvailableCommands()
    {
        ProcessResult empty = await ExecuteAsync([]);
        ProcessResult help = await ExecuteAsync(["--help"]);

        await Assert.That(empty.ExitCode).IsEqualTo(0);
        await Assert.That(empty.StandardError).IsEmpty();
        string usage = Encoding.UTF8.GetString(empty.StandardOutput);
        foreach (string family in SetupCliContractSpecification.Operations.Keys)
            await Assert.That(usage).Contains(family);
        await Assert.That(help.ExitCode).IsEqualTo(0);
        await Assert.That(help.StandardOutput).IsEquivalentTo(empty.StandardOutput);
    }

    [Test]
    [Arguments("catalogue", "")]
    [Arguments("artifact", "validate")]
    [Arguments("env", "render")]
    [Arguments("artifact", "import")]
    public async Task HelpDoesNotRequireOperationalInputsOrOutput(string family, string operation)
    {
        string[] arguments = operation.Length == 0
            ? [family, "--help"]
            : [family, operation, "--help"];
        ProcessResult human = await ExecuteAsync(arguments);
        await Assert.That(human.ExitCode).IsEqualTo(0);
        await Assert.That(human.StandardOutput.Length).IsGreaterThan(0);
        await Assert.That(human.StandardError).IsEmpty();

        ProcessResult machine = await ExecuteAsync([.. arguments, "--machine"]);
        await Assert.That(machine.ExitCode).IsEqualTo(0);
        await Assert.That(SetupCliMachineContractVerifier.Validate(machine.StandardOutput)).IsEmpty();
    }

    [Test]
    public async Task IncompleteCommandsRenderHumanDiagnosticsWithoutChangingMachineDiagnostics()
    {
        var cases = new (string[] Arguments, string Code)[]
        {
            (["catalogue", "list"], "definition-required"),
            (["catalogue", "show", "--definition", "definition.json"], "key-required"),
            (["artifact", "create"], "definition-required")
        };

        foreach ((string[] arguments, string code) in cases)
        {
            ProcessResult text = await ExecuteAsync(arguments);
            await Assert.That(text.ExitCode).IsEqualTo(64);
            await Assert.That(text.StandardOutput).IsEmpty();
            string guidance = Encoding.UTF8.GetString(text.StandardError);
            await Assert.That(guidance).Contains(code);

            ProcessResult machine = await ExecuteAsync([.. arguments, "--machine"]);
            await Assert.That(machine.ExitCode).IsEqualTo(64);
            await Assert.That(machine.StandardError).IsEmpty();
            await Assert.That(SetupCliMachineContractVerifier.Validate(machine.StandardOutput)).IsEmpty();
            using JsonDocument result = JsonDocument.Parse(machine.StandardOutput);
            await Assert.That(result.RootElement.GetProperty("diagnostics")[0].GetProperty("code").GetString())
                .IsEqualTo(code);
        }

        ProcessResult rejected = await ExecuteAsync(["catalogue", "show", "--key", "person@example.invalid"]);
        await Assert.That(Encoding.UTF8.GetString(rejected.StandardError))
            .DoesNotContain("person@example.invalid");
    }

    [Test]
    public async Task OversizedMachineArgumentProducesOneUsageObjectWithoutStderr()
    {
        ProcessResult result = await ExecuteAsync(["doctor", "--machine", new string('x', 5_000)]);

        await Assert.That(result.ExitCode).IsEqualTo(64);
        await Assert.That(result.StandardError).IsEmpty();
        await Assert.That(SetupCliMachineContractVerifier.Validate(result.StandardOutput)).IsEmpty();
    }

    [Test]
    public async Task HostileArgumentsAreRejectedBeforeParserOutputCanRevealValues()
    {
        string value = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        string path = Path.Combine(Path.GetTempPath(), "credential-" + Guid.NewGuid().ToString("N"));
        var vectors = new[]
        {
            new[] { "doctor", "--machine", "--password", value },
            new[] { "artifact", "validate", "--machine", "--input", path },
            new[] { "doctor", "--machine", "\u001b]0;" + value + "\u0007" }
        };

        foreach (string[] arguments in vectors)
        {
            ProcessResult result = await ExecuteAsync(arguments);
            await Assert.That(result.ExitCode).IsEqualTo(64);
            await Assert.That(result.StandardError).IsEmpty();
            await Assert.That(SetupCliMachineContractVerifier.Validate(result.StandardOutput)).IsEmpty();
            string output = Encoding.UTF8.GetString(result.StandardOutput);
            await Assert.That(output).DoesNotContain(value);
            await Assert.That(output).DoesNotContain(path);
        }
    }

    [Test]
    public async Task ExecutableIgnoresUnrelatedAmbientCredentials()
    {
        string suffix = Guid.NewGuid().ToString("N");
        string name = "SERVICE_TOKEN_" + suffix;
        string value = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        ProcessResult result = await ExecuteAsync(["doctor", "--machine"], new Dictionary<string, string>
        {
            [name] = value,
            ["GITHUB_TOKEN"] = "synthetic-unrelated-token"
        });

        await Assert.That(result.ExitCode).IsEqualTo(OperatingSystem.IsLinux() ? 0 : 4);
        await Assert.That(result.StandardError).IsEmpty();
        await Assert.That(SetupCliMachineContractVerifier.Validate(result.StandardOutput)).IsEmpty();
        string output = Encoding.UTF8.GetString(result.StandardOutput);
        await Assert.That(output).DoesNotContain(name);
        await Assert.That(output).DoesNotContain(value);
    }

    [Test]
    public async Task OversizedMachineArtifactProducesOneIoObjectWithoutStderr()
    {
        string path = Path.Combine(Path.GetTempPath(), "islamu-configurator-bound-" + Guid.NewGuid().ToString("N"));
        await File.WriteAllBytesAsync(path, new byte[(4 * 1024 * 1024) + 1]);
        using var files = new NativeFixture();
        try
        {
            ProcessResult result = await ExecuteAsync(["artifact", "validate", "--definition", files.Definition,
                "--artifact", "config", "--input", path, "--machine"]);
            await Assert.That(result.ExitCode).IsEqualTo(74);
            await Assert.That(result.StandardError).IsEmpty();
            await Assert.That(SetupCliMachineContractVerifier.Validate(result.StandardOutput)).IsEmpty();
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string ShellQuote(string value) => "'" + value.Replace("'", "'\"'\"'", StringComparison.Ordinal) + "'";

    private static async Task<ProcessResult> ExecuteHostAsync(string executable, IReadOnlyList<string> arguments)
    {
        var info = new ProcessStartInfo(executable)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (string argument in arguments) info.ArgumentList.Add(argument);
        using var process = new Process { StartInfo = info, EnableRaisingEvents = true };
        var exited = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        process.Exited += (_, _) => exited.TrySetResult();
        if (!process.Start()) throw new InvalidOperationException("process-start-failed");
        Task<byte[]> output = ReadAllAsync(process.StandardOutput.BaseStream);
        Task<byte[]> error = ReadAllAsync(process.StandardError.BaseStream);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try
        {
            // Keep the parent's stdin open: a terminal read must reject without EOF.
            await exited.Task.WaitAsync(timeout.Token);
            return new(process.ExitCode, await output.WaitAsync(timeout.Token), await error.WaitAsync(timeout.Token));
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

    private static async Task<ProcessResult> ExecuteAsync(
        IReadOnlyList<string> arguments,
        IReadOnlyDictionary<string, string>? environment = null)
    {
        var info = new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory,
            OperatingSystem.IsWindows() ? "Configurator.Cli.exe" : "Configurator.Cli"))
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (string argument in arguments) info.ArgumentList.Add(argument);
        if (environment is not null)
        {
            foreach ((string name, string value) in environment)
                info.Environment[name] = value;
        }
        using var process = new Process { StartInfo = info, EnableRaisingEvents = true };
        var exited = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        process.Exited += (_, _) => exited.TrySetResult();
        if (!process.Start()) throw new InvalidOperationException("process-start-failed");
        Task<byte[]> output = ReadAllAsync(process.StandardOutput.BaseStream);
        Task<byte[]> error = ReadAllAsync(process.StandardError.BaseStream);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try
        {
            await exited.Task.WaitAsync(timeout.Token);
            return new ProcessResult(process.ExitCode, await output.WaitAsync(timeout.Token), await error.WaitAsync(timeout.Token));
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

    private static async Task<byte[]> ReadAllAsync(Stream stream)
    {
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer);
        return buffer.ToArray();
    }

    private sealed record ProcessResult(int ExitCode, byte[] StandardOutput, byte[] StandardError);

    private sealed class NativeFixture : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "configurator-cli-" + Guid.NewGuid().ToString("N"));
        internal NativeFixture()
        {
            Directory.CreateDirectory(_directory);
            Definition = Path.Combine(_directory, "definition.json");
            Input = Path.Combine(_directory, "answers.json");
            File.WriteAllText(Definition, CliFixture.Definition);
            File.WriteAllText(Input, CliFixture.Answers);
        }
        internal string Definition { get; }
        internal string Input { get; }
        internal string[] Arguments(string family, string operation, string artifact, params string[] options) =>
            [family, operation, "--definition", Definition, "--artifact", artifact, "--input", Input, .. options];
        public void Dispose() => Directory.Delete(_directory, recursive: true);
    }
}
