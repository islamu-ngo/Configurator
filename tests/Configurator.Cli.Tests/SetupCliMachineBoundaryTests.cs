using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Configurator.Cli.Tests;

public sealed class SetupCliMachineBoundaryTests
{
    [Test]
    public async Task MaximumCoverageCannotHideAnEstablishedNativeWrite()
    {
        string directory = Path.Combine(Path.GetTempPath(), "configurator-receipt-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            JsonObject definition = JsonNode.Parse(CliFixture.Definition)!.AsObject();
            JsonArray fields = definition["fields"]!.AsArray();
            fields.Clear();
            JsonObject answers = new();
            for (int index = 0; index < 1024; index++)
            {
                string id = "f" + index.ToString("D4", System.Globalization.CultureInfo.InvariantCulture) + new string('x', 123);
                fields.Add(new JsonObject { ["id"] = id, ["type"] = "string", ["required"] = true,
                    ["sensitivity"] = "public", ["disclosure"] = "guided", ["title"] = "Field" });
                answers[id] = "synthetic";
            }
            var artifacts = new JsonArray();
            var ids = new List<string>();
            for (int index = 0; index < 64; index++)
            {
                string id = "a" + index.ToString("D3", System.Globalization.CultureInfo.InvariantCulture) + new string('x', 124);
                ids.Add(id);
                artifacts.Add(new JsonObject
                {
                    ["id"] = id, ["format"] = "json", ["fileName"] = index + ".json",
                    ["applySemantics"] = "create", ["restart"] = "none",
                    ["projection"] = new JsonObject { ["op"] = "literal", ["value"] = new JsonObject { ["ok"] = true } }
                });
            }
            definition["artifacts"] = artifacts;
            string source = Path.Combine(directory, "definition.json");
            string input = Path.Combine(directory, "answers.json");
            string target = Path.Combine(directory, "0.json");
            await File.WriteAllTextAsync(source, definition.ToJsonString());
            await File.WriteAllTextAsync(input, answers.ToJsonString());
            var info = new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, "Configurator.Cli"))
            { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
            // Ambient names are irrelevant to this invariant; explicit-input admission is a later slice.
            foreach (string name in info.Environment.Keys.Where(name => name.Contains("TOKEN", StringComparison.OrdinalIgnoreCase)
                || name.Contains("PASSWORD", StringComparison.OrdinalIgnoreCase) || name.Contains("SECRET", StringComparison.OrdinalIgnoreCase)).ToArray())
                info.Environment.Remove(name);
            foreach (string argument in new[] { "artifact", "bundle-create", "--definition", source,
                "--input", input, "--output", directory, "--machine" }) info.ArgumentList.Add(argument);
            foreach (string id in ids) { info.ArgumentList.Add("--artifact"); info.ArgumentList.Add(id); }
            using Process process = Process.Start(info)!;
            Task<string> output = process.StandardOutput.ReadToEndAsync();
            Task<string> error = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            await process.WaitForExitAsync(timeout.Token);
            await Assert.That(File.Exists(target)).IsTrue();
            using JsonDocument payload = JsonDocument.Parse(await File.ReadAllBytesAsync(target, timeout.Token));
            await Assert.That(payload.RootElement.GetProperty("ok").GetBoolean()).IsTrue();
            if (OperatingSystem.IsLinux())
                await Assert.That(File.GetUnixFileMode(target)).IsEqualTo(UnixFileMode.UserRead | UnixFileMode.UserWrite);
            string receipt = await output;
            await Assert.That(await error).IsEmpty();
            await Assert.That(Encoding.UTF8.GetByteCount(receipt)).IsLessThanOrEqualTo(65_536);
            using JsonDocument result = JsonDocument.Parse(receipt);
            await Assert.That(result.RootElement.GetProperty("artifacts").GetArrayLength()).IsEqualTo(64);
            foreach (JsonElement artifact in result.RootElement.GetProperty("artifacts").EnumerateArray())
                await Assert.That(artifact.GetProperty("writeStatus").GetString()).IsEqualTo("written");
            await Assert.That(result.RootElement.GetProperty("coverage").GetProperty("coveredCount").GetInt32()).IsEqualTo(64);
            await Assert.That(process.ExitCode).IsEqualTo(0);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Test]
    [Arguments(0)]
    [Arguments(32)]
    public async Task FailedDeliveryIsAttemptedOnlyOnce(int prefix)
    {
        var writer = new FailingWriter(prefix);
        var error = new FailingWriter(0);
        SetupCliExitCode exit = new SetupCliApplication().Run(new(["doctor", "--machine"], SetupCliMode.Machine,
            new(new CliFixture(), writer, error, 65_536, 4 * 1024 * 1024), new([])));
        await Assert.That(exit).IsEqualTo(SetupCliExitCode.Io);
        await Assert.That(writer.Attempts).IsEqualTo(1);
        await Assert.That(error.Attempts).IsEqualTo(1);
    }

    [Test]
    public async Task InsufficientReceiptCapacityRejectsBeforePublication()
    {
        var fixture = new CliFixture();
        var writer = new PublicationWriter(fixture, new([new(Configurator.Artifacts.ProtectedArtifactStatus.Written)]));
        SetupCliExitCode exit = RunArtifact(fixture, writer, 512);
        await Assert.That(exit).IsEqualTo(SetupCliExitCode.Io);
        await Assert.That(writer.Publications).IsEqualTo(0);
        await Assert.That(fixture.Files.ContainsKey("prepared.json")).IsFalse();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task CleanupFailureDoesNotEraseWrittenOutcome(bool unknownStatus)
    {
        var fixture = new CliFixture();
        var status = unknownStatus ? (Configurator.Artifacts.ProtectedArtifactStatus)int.MaxValue
            : Configurator.Artifacts.ProtectedArtifactStatus.Written;
        var writer = new PublicationWriter(fixture, new([new(status)], CleanupFailed: !unknownStatus));
        await Assert.That(RunArtifact(fixture, writer)).IsEqualTo(SetupCliExitCode.Io);
        using JsonDocument result = JsonDocument.Parse(fixture.StandardOutput);
        await Assert.That(result.RootElement.GetProperty("artifacts")[0].GetProperty("writeStatus").GetString())
            .IsEqualTo(unknownStatus ? "unknown" : "written");
        await Assert.That(result.RootElement.GetProperty("cleanup").GetString()).IsEqualTo(unknownStatus ? "completed" : "failed");
    }

    [Test]
    public async Task ExceptionAfterPublicationCannotClaimNotAttempted()
    {
        var fixture = new CliFixture();
        var writer = new PublicationWriter(fixture, null);
        await Assert.That(RunArtifact(fixture, writer)).IsEqualTo(SetupCliExitCode.Io);
        await Assert.That(fixture.Files.ContainsKey("prepared.json")).IsTrue();
        using JsonDocument result = JsonDocument.Parse(fixture.StandardOutput);
        await Assert.That(result.RootElement.GetProperty("artifacts")[0].GetProperty("writeStatus").GetString()).IsEqualTo("unknown");
    }

    [Test]
    [Arguments("validate", SetupCliExitCode.Incomplete)]
    [Arguments("bundle-check", SetupCliExitCode.Validation)]
    public async Task DiagnosticPagesAccountExactlyAndRejectChangedViews(string operation, SetupCliExitCode expected)
    {
        var fixture = new CliFixture();
        JsonObject definition = JsonNode.Parse(CliFixture.Definition)!.AsObject();
        var fields = new JsonArray();
        for (int index = 0; index < 40; index++) fields.Add(new JsonObject
        {
            ["id"] = "field" + index, ["type"] = "string", ["required"] = true,
            ["sensitivity"] = "public", ["disclosure"] = "guided", ["title"] = "Field"
        });
        definition["fields"] = fields;
        definition["artifacts"] = new JsonArray(new JsonObject
        {
            ["id"] = "config", ["format"] = "json", ["fileName"] = "output.json",
            ["applySemantics"] = "create", ["restart"] = "none",
            ["projection"] = new JsonObject { ["op"] = "literal", ["value"] = new JsonObject() }
        });
        fixture.Files["definition.json"] = Encoding.UTF8.GetBytes(definition.ToJsonString());
        fixture.Files["answers.json"] = "{}"u8.ToArray();
        string[] arguments = ["artifact", operation, "--definition", "definition.json", "--artifact", "config",
            "--input", "answers.json", "--machine", "--detail", "diagnostics", "--limit", "16"];
        await Assert.That(fixture.Run(arguments)).IsEqualTo(expected);
        using JsonDocument first = JsonDocument.Parse(fixture.StandardOutput);
        await Assert.That(first.RootElement.GetProperty("diagnosticsTotal").GetInt32()).IsEqualTo(40);
        await Assert.That(first.RootElement.GetProperty("diagnosticsOmitted").GetInt32()).IsEqualTo(24);
        await Assert.That(first.RootElement.GetProperty("detail").GetProperty("diagnostics").GetArrayLength()).IsEqualTo(16);
        string digest = first.RootElement.GetProperty("detailDigest").GetString()!;
        string[] continuation = [.. arguments, "--offset", "16", "--expected-detail-digest", digest];
        await Assert.That(fixture.Run(continuation)).IsEqualTo(expected);
        await Assert.That(SetupCliMachineContractVerifier.Validate(fixture.StandardOutput)).IsEmpty();
        fixture.Files["answers.json"] = "{\"field0\":\"synthetic\"}"u8.ToArray();
        await Assert.That(fixture.Run(continuation)).IsEqualTo(SetupCliExitCode.Data);
        using JsonDocument mismatch = JsonDocument.Parse(fixture.StandardOutput);
        await Assert.That(mismatch.RootElement.GetProperty("diagnostics")[0].GetProperty("code").GetString()).IsEqualTo("detail-digest-mismatch");
    }

    private static SetupCliExitCode RunArtifact(CliFixture fixture, PublicationWriter writer, int budget = 65_536) =>
        new SetupCliApplication().Run(new(["artifact", "create", "--definition", "definition.json", "--artifact", "config",
            "--input", "answers.json", "--output", "prepared.json", "--machine"], SetupCliMode.Machine,
            new(fixture, writer, writer, budget, 4 * 1024 * 1024), new([])));

    private sealed class PublicationWriter(CliFixture fixture, SetupCliPublicationResult? result) : ISetupCliWriter
    {
        internal int Publications { get; private set; }
        public void Write(string path, ReadOnlyMemory<byte> bytes, int maximumBytes) => fixture.Write(path, bytes, maximumBytes);
        public SetupCliPublicationResult WriteArtifact(string path, Configurator.Core.SetupArtifactKind kind,
            ReadOnlyMemory<byte> bytes, int maximumBytes)
        {
            Publications++;
            fixture.WriteArtifact(path, kind, bytes, maximumBytes);
            return result ?? throw new IOException("synthetic-post-publication-failure");
        }
        public SetupCliPublicationResult WriteBundle(IReadOnlyList<Configurator.Core.Artifacts.DefinitionArtifact> artifacts,
            string directory, int maximumBytes) => throw new InvalidOperationException();
    }

    private sealed class FailingWriter(int prefix) : ISetupCliWriter
    {
        internal int Attempts { get; private set; }
        public void Write(string path, ReadOnlyMemory<byte> bytes, int maximumBytes)
        {
            Attempts++;
            _ = bytes.Span[..Math.Min(prefix, bytes.Length)];
            throw new IOException("synthetic-delivery-failure");
        }
        public SetupCliPublicationResult WriteArtifact(string path, Configurator.Core.SetupArtifactKind kind, ReadOnlyMemory<byte> bytes, int maximumBytes) => throw new IOException();
        public SetupCliPublicationResult WriteBundle(
            IReadOnlyList<Configurator.Core.Artifacts.DefinitionArtifact> artifacts, string directory, int maximumBytes) => throw new IOException();
    }
}
