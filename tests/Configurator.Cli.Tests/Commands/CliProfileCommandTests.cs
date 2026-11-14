using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Security.Cryptography;
using Configurator.Artifacts.Profiles;
using Configurator.Core;
using Configurator.Core.Definitions;
using Configurator.Core.Profiles;
using System.Diagnostics;

namespace Configurator.Cli.Tests;

[NotInParallel("profile-container")]
public sealed class CliProfileCommandTests
{
    private static readonly string[] ExpectedOperations =
        ["adopt", "diff", "inspect", "list", "recovery-check", "save", "slots", "unlock", "upgrade-plan"];

    [Test]
    public async Task ProfileHelpExposesTheClosedOperationalVocabularyWithoutReadingInputs()
    {
        var io = new CliFixture();

        CliExitCode exit = await io.RunAsync("profile", "--help", "--machine");

        await Assert.That(exit).IsEqualTo(CliExitCode.Success);
        using JsonDocument document = JsonDocument.Parse(io.StandardOutput);
        string[] operations = document.RootElement.GetProperty("help").GetProperty("commands")
            .EnumerateArray().Select(item => item.GetString()!).Order(StringComparer.Ordinal).ToArray();
        await Assert.That(operations).IsEquivalentTo(ExpectedOperations);
        await Assert.That(io.MaximumRead).IsEqualTo(0);
        await Assert.That(CliMachineContractVerifier.Validate(io.StandardOutput)).IsEmpty();
    }

    [Test]
    public async Task CompetingAnswerAndPassphraseStdinIsRejectedBeforeReading()
    {
        var io = new CliFixture();

        CliExitCode exit = await io.RunAsync("profile", "save", "--store", "/synthetic/profile-store",
            "--definition", "definition.json", "--input", "-", "--mode", "import-upgrade-only",
            "--environment-context", "synthetic-environment", "--passphrase-stdin", "--machine");

        await Assert.That(exit).IsEqualTo(CliExitCode.Usage);
        await Assert.That(Code(io)).IsEqualTo("stdin-conflict");
        await Assert.That(io.MaximumRead).IsEqualTo(0);
    }

    [Test]
    public async Task DeviceOnlySaveRequiresExplicitAcknowledgementBeforeAnyNativeOperation()
    {
        var io = new CliFixture();

        CliExitCode exit = await io.RunAsync("profile", "save", "--store", "/synthetic/profile-store",
            "--definition", "definition.json", "--input", "answers.json",
            "--mode", "import-upgrade-only", "--environment-context", "synthetic-environment", "--device", "--machine");

        await Assert.That(exit).IsEqualTo(CliExitCode.Usage);
        await Assert.That(Code(io)).IsEqualTo("device-only-acknowledgement-required");
        await Assert.That(io.MaximumRead).IsEqualTo(0);
    }

    [Test]
    [Arguments("--cipher", "unsupported-cipher")]
    [Arguments("--kdf", "unsupported-kdf")]
    public async Task UnsupportedProfileIdentifiersAreRejectedBeforeInput(string option, string value)
    {
        var io = new CliFixture();

        CliExitCode exit = await io.RunAsync("profile", "save", "--store", "/synthetic/profile-store",
            "--definition", "definition.json", "--input", "answers.json",
            "--mode", "import-upgrade-only", "--environment-context", "synthetic-environment",
            "--passphrase-stdin", option, value, "--machine");

        await Assert.That(exit).IsEqualTo(CliExitCode.Usage);
        await Assert.That(Code(io)).IsEqualTo("profile-policy-unsupported");
        await Assert.That(io.MaximumRead).IsEqualTo(0);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ConflictingCredentialChannelsNeverEchoPrivateArguments(bool machine)
    {
        var io = new CliFixture();
        const string privatePath = "/synthetic/private-key-path-sentinel";
        string[] mode = machine ? ["--machine"] : ["--text"];

        CliExitCode exit = await io.RunAsync(["profile", "unlock", "--input", "profile.cfgvault",
            "--passphrase-stdin", "--passphrase-file", privatePath, .. mode]);

        await Assert.That(exit).IsEqualTo(CliExitCode.Usage);
        await Assert.That(io.MaximumRead).IsEqualTo(0);
        await Assert.That(Encoding.UTF8.GetString(io.StandardOutput)).DoesNotContain(privatePath);
        await Assert.That(Encoding.UTF8.GetString(io.StandardError)).DoesNotContain(privatePath);
        if (machine) await Assert.That(Code(io)).IsEqualTo("passphrase-input-conflict");
        else await Assert.That(io.StandardOutput.Length).IsEqualTo(0);
    }

    [Test]
    [Arguments("--passphrase")]
    [Arguments("--passphrase-env")]
    public async Task InlineAndEnvironmentCredentialOptionsAreNotPublicChannels(string option)
    {
        var io = new CliFixture();
        const string sentinel = "synthetic-secret-value-sentinel";

        CliExitCode exit = await io.RunAsync("profile", "unlock", "--input", "profile.cfgvault",
            option, sentinel, "--machine");

        await Assert.That(exit).IsEqualTo(CliExitCode.Usage);
        await Assert.That(Encoding.UTF8.GetString(io.StandardOutput)).DoesNotContain(sentinel);
        await Assert.That(Encoding.UTF8.GetString(io.StandardError)).DoesNotContain(sentinel);
        await Assert.That(io.MaximumRead).IsEqualTo(0);
    }

    [Test]
    public async Task AdoptionRejectsPlaintextStandardOutputBeforeUnlock()
    {
        var io = new CliFixture();

        CliExitCode exit = await io.RunAsync("profile", "adopt", "--input", "profile.cfgvault",
            "--definition", "definition.json", "--artifact", "config", "--output", "-",
            "--passphrase-stdin", "--machine");

        await Assert.That(exit).IsEqualTo(CliExitCode.Usage);
        await Assert.That(Code(io)).IsEqualTo("restricted-stdout");
        await Assert.That(io.MaximumRead).IsEqualTo(0);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task PortableUnlockUsesBoundedSecretInputAndEmitsOnlySafeStatus(bool machine)
    {
        var io = await EncryptedFixture();
        var error = new CliFixture();
        var input = new TrackingInput(io);
        CliExitCode exit = await Run(io, error, input, "profile", "unlock", "--input", "profile.cfgvault",
            "--passphrase-file", "private-passphrase-path-sentinel", machine ? "--machine" : "--text");

        await Assert.That(exit).IsEqualTo(CliExitCode.Success);
        await Assert.That(input.Bounds["private-passphrase-path-sentinel"]).IsEqualTo(1024);
        await Assert.That(input.CredentialCopies.All(bytes => bytes.Span.IndexOfAnyExcept((byte)0) < 0)).IsTrue();
        await SafeSinks(io, error);
        await Assert.That(Encoding.UTF8.GetString(io.StandardOutput)).Contains("profile-unlocked-status-only");
        if (machine) await Assert.That(CliMachineContractVerifier.Validate(io.StandardOutput)).IsEmpty();
    }

    [Test]
    [Arguments("oversized")]
    [Arguments("invalid-utf8")]
    [Arguments("wrong")]
    public async Task CredentialFailuresNeverDiscloseInputsOrPaths(string scenario)
    {
        var io = await EncryptedFixture();
        io.Files["private-passphrase-path-sentinel"] = scenario switch
        {
            "oversized" => new byte[1025],
            "invalid-utf8" => [0xff],
            _ => "wrong-private-passphrase-sentinel"u8.ToArray()
        };
        var error = new CliFixture();
        CliExitCode exit = await Run(io, error, io, "profile", "unlock", "--input", "profile.cfgvault",
            "--passphrase-file", "private-passphrase-path-sentinel", "--machine");
        await Assert.That(exit).IsEqualTo(CliExitCode.Data);
        await SafeSinks(io, error);
        await Assert.That(io.Files.ContainsKey("target.json")).IsFalse();
    }

    [Test]
    public async Task BlindAdoptionWritesRetainedSecretsOnlyThroughRestrictedDestination()
    {
        var io = await EncryptedFixture();
        var error = new CliFixture();
        CliExitCode exit = await Run(io, error, io, "profile", "adopt", "--input", "profile.cfgvault",
            "--definition", "definition.json", "--artifact", "config", "--output", "target.json",
            "--passphrase-file", "private-passphrase-path-sentinel", "--machine");
        await Assert.That(exit).IsEqualTo(CliExitCode.Success);
        using JsonDocument artifact = JsonDocument.Parse(io.Files["target.json"]);
        await Assert.That(artifact.RootElement.GetProperty("name").GetString()).IsEqualTo("private-answer-marker");
        await SafeSinks(io, error);
        await Assert.That(CliMachineContractVerifier.Validate(io.StandardOutput)).IsEmpty();
        JsonObject malformed = JsonNode.Parse(io.StandardOutput)!.AsObject();
        JsonObject malformedArtifact = malformed["artifacts"]![0]!.AsObject();
        malformedArtifact["digest"] = ArtifactDigest.Compute(io.Files["target.json"]).Value;
        byte[] malformedReceipt = Encoding.UTF8.GetBytes(malformed.ToJsonString() + "\n");
        await Assert.That(CliMachineContractVerifier.Validate(malformedReceipt))
            .Contains("machine-profile-artifact-digest");

        // Evaluate the generated conditional's prohibited required-field combination against
        // this actual malformed artifact. This checks the generated digest constraint only.
        JsonObject schema = CliMachineContractVerifier.ParseSchema(CliCommandSchemaMetadata.GenerateSchema());
        JsonObject digestRule = schema["allOf"]!.AsArray().OfType<JsonObject>()
            .Single(rule => rule["then"]?["properties"]?["artifacts"]?["items"]?["not"] is not null);
        JsonObject profileCondition = digestRule["if"]!["anyOf"]!.AsArray().OfType<JsonObject>()
            .Single(condition => condition["properties"]?["invocation"]?["properties"]?["commandFamily"]?["const"]
                ?.GetValue<string>() == "profile");
        await Assert.That(malformed["invocation"]!["commandFamily"]!.GetValue<string>()).IsEqualTo("profile");
        string operation = malformed["invocation"]!["operation"]!.GetValue<string>();
        await Assert.That(profileCondition["properties"]!["invocation"]!["properties"]!["operation"]!["enum"]!
            .AsArray().Select(value => value!.GetValue<string>())).Contains(operation);
        JsonArray forbiddenCombination = digestRule["then"]!["properties"]!["artifacts"]!["items"]!["not"]!["required"]!.AsArray();
        await Assert.That(forbiddenCombination.Select(value => value!.GetValue<string>())).IsEquivalentTo(["digest"]);
        await Assert.That(forbiddenCombination.All(value => malformedArtifact.ContainsKey(value!.GetValue<string>()))).IsTrue();
    }

    [Test]
    public async Task ConflictingHistoricalRevisionCannotAdoptRetainedAnswers()
    {
        var io = await EncryptedFixture();
        io.Files["source.json"] = io.Files["definition.json"].ToArray();
        JsonNode target = JsonNode.Parse(io.Files["definition.json"])!;
        target["project"]!["name"] = "Changed same revision";
        io.Files["definition.json"] = Encoding.UTF8.GetBytes(target.ToJsonString());
        var error = new CliFixture();
        CliExitCode exit = await Run(io, error, io, "profile", "adopt", "--input", "profile.cfgvault",
            "--definition", "definition.json", "--source-definition", "source.json",
            "--artifact", "config", "--output", "target.json",
            "--passphrase-file", "private-passphrase-path-sentinel", "--machine");
        await Assert.That(exit).IsEqualTo(CliExitCode.Data);
        await Assert.That(io.Files.ContainsKey("target.json")).IsFalse();
        await SafeSinks(io, error);
    }

    [Test]
    [Arguments("diff")]
    [Arguments("upgrade-plan")]
    public async Task PlanningReportsRevalidationWithoutExportingRetainedValues(string operation)
    {
        var io = await EncryptedFixture();
        var error = new CliFixture();
        CliExitCode exit = await Run(io, error, io, "profile", operation, "--input", "profile.cfgvault",
            "--definition", "definition.json", "--passphrase-file", "private-passphrase-path-sentinel", "--machine");
        await Assert.That(exit).IsEqualTo(CliExitCode.Success);
        await Assert.That(Encoding.UTF8.GetString(io.StandardOutput)).Contains("profile-revalidation-required");
        await SafeSinks(io, error);
        await Assert.That(CliMachineContractVerifier.Validate(io.StandardOutput)).IsEmpty();
    }

    [Test]
    public async Task InspectionAndSlotsNeverClaimAuthenticationOrConsumeCredentials()
    {
        var io = await EncryptedFixture();
        var error = new CliFixture();
        foreach (string operation in new[] { "inspect", "slots" })
        {
            var input = new TrackingInput(io);
            CliExitCode exit = await Run(io, error, input, "profile", operation, "--input", "profile.cfgvault", "--machine");
            await Assert.That(exit).IsEqualTo(CliExitCode.Success);
            await Assert.That(input.Bounds.ContainsKey("private-passphrase-path-sentinel")).IsFalse();
            string output = Encoding.UTF8.GetString(io.StandardOutput);
            await Assert.That(output).Contains("profile-container-unverified");
            await Assert.That(output).Contains("profile-a256gcm");
            await Assert.That(output).Contains("profile-pbkdf2-hmac-sha512-600000");
            await SafeSinks(io, error);
        }
    }

    [Test]
    public async Task RestrictedTargetModeConsumesAnswersStdinOnce()
    {
        var io = new CliFixture();
        io.Files["-"] = io.Files["answers.json"];
        var error = new CliFixture();
        var input = new TrackingInput(io);
        CliExitCode exit = await Run(io, error, input, "profile", "save", "--mode", "unencrypted-target",
            "--definition", "definition.json", "--input", "-", "--artifact", "config",
            "--output", "target.json", "--machine");
        await Assert.That(exit).IsEqualTo(CliExitCode.Success);
        await Assert.That(input.Reads.Count(path => path == "-")).IsEqualTo(1);
        await Assert.That(io.Files.ContainsKey("target.json")).IsTrue();
        await SafeSinks(io, error);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ExecutablePassphraseStdinKeepsActualOutputAndErrorStreamsValueFree(bool wrongPassphrase)
    {
        var io = await EncryptedFixture();
        string root = Path.Combine(Path.GetTempPath(), "configurator-cli-stream-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string path = Path.Combine(root, "private-profile-path-sentinel.cfgvault");
            await File.WriteAllBytesAsync(path, io.Files["profile.cfgvault"]);
            var start = new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory,
                OperatingSystem.IsWindows() ? "Configurator.Cli.exe" : "Configurator.Cli"))
            {
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            foreach (string argument in new[] { "profile", "unlock", "--input", path, "--passphrase-stdin", "--machine" })
                start.ArgumentList.Add(argument);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            using Process process = Process.Start(start)!;
            try
            {
                Task<string> stdout = process.StandardOutput.ReadToEndAsync(timeout.Token);
                Task<string> stderr = process.StandardError.ReadToEndAsync(timeout.Token);
                await process.StandardInput.BaseStream.WriteAsync(wrongPassphrase
                    ? "wrong-private-passphrase-sentinel"u8.ToArray()
                    : io.Files["private-passphrase-path-sentinel"], timeout.Token);
                process.StandardInput.Close();
                await process.WaitForExitAsync(timeout.Token);
                string output = await stdout;
                string error = await stderr;
                await Assert.That(process.ExitCode).IsEqualTo((int)(wrongPassphrase ? CliExitCode.Data : CliExitCode.Success));
                await Assert.That(error).IsEqualTo("");
                foreach (string sentinel in new[] { "private-answer-marker", "synthetic-portable-passphrase",
                    "wrong-private-passphrase-sentinel", path, root })
                    await Assert.That(output + error).DoesNotContain(sentinel);
                await Assert.That(CliMachineContractVerifier.Validate(Encoding.UTF8.GetBytes(output))).IsEmpty();
            }
            finally { if (!process.HasExited) process.Kill(entireProcessTree: true); }
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Test]
    public async Task NativeImmutableSaveListRecoveryAndResealUseExactSnapshotsWithoutKeyring()
    {
        string root = Path.Combine(Path.GetTempPath(), "configurator-cli-profile-" + Guid.NewGuid().ToString("N"));
        var io = new CliFixture();
        io.Files["-"] = "synthetic-portable-passphrase"u8.ToArray();
        io.Files["confirmation"] = "synthetic-portable-passphrase"u8.ToArray();
        io.Files["new-passphrase"] = "different-portable-passphrase"u8.ToArray();
        var error = new CliFixture();
        try
        {
            CliExitCode saved = await Run(io, error, io, "profile", "save", "--store", root,
                "--definition", "definition.json", "--input", "answers.json", "--mode", "import-upgrade-only",
                "--environment-context", "synthetic-environment", "--passphrase-stdin",
                "--confirm-passphrase-file", "confirmation", "--machine");
            if (!OperatingSystem.IsLinux())
            {
                await Assert.That(saved).IsEqualTo(CliExitCode.Blocked);
                return;
            }
            await Assert.That(saved).IsEqualTo(CliExitCode.Success);
            var store = new LocalProfileVaultStore(root);
            ProfileRevisionDescriptor first = (await store.ListAsync()).Single();
            byte[] original = await store.ReadAsync(first);
            string digest = ArtifactDigest.Compute(original).Value;
            string[] identity = ["--store", root, "--profile-id", first.ProfileId.ToString(), "--snapshot-id", first.SnapshotId.ToString()];
            await Assert.That(await Run(io, error, io, "profile", "list", "--store", root, "--machine"))
                .IsEqualTo(CliExitCode.Success);
            await Assert.That(Encoding.UTF8.GetString(io.StandardOutput)).Contains(first.SnapshotId.ToString("N"));
            byte[] discoveryReceipt = io.StandardOutput.ToArray();
            await Assert.That(await Run(io, error, io, ["profile", "recovery-check", .. identity,
                "--passphrase-stdin", "--expected-digest", digest, "--machine"])).IsEqualTo(CliExitCode.Success);
            await Assert.That(Encoding.UTF8.GetString(io.StandardOutput)).Contains("profile-recovery-verified-for-snapshot");
            await Assert.That(await Run(io, error, io, ["profile", "recovery-check", .. identity,
                "--passphrase-stdin", "--expected-digest", new string('0', 64), "--machine"])).IsEqualTo(CliExitCode.Blocked);
            await Assert.That(Encoding.UTF8.GetString(io.StandardOutput)).DoesNotContain("profile-recovery-verified-for-snapshot");
            await Assert.That(await Run(io, error, io, ["profile", "slots", .. identity,
                "--action", "reseal", "--passphrase-stdin", "--new-passphrase-file", "new-passphrase",
                "--confirm-passphrase-file", "new-passphrase", "--machine"]))
                .IsEqualTo(CliExitCode.Success);
            ProfileRevisionDescriptor[] revisions = (await store.ListAsync()).ToArray();
            await Assert.That(revisions.Length).IsEqualTo(2);
            await Assert.That(await store.ReadAsync(first)).IsEquivalentTo(original);
            ProfileRevisionDescriptor second = revisions.Single(item => item.SnapshotId != first.SnapshotId);
            using var codec = new NativeProfileContainerCodec();
            using UnlockedProfile reopened = await codec.OpenStoredAsync(store, second, 0, io.Files["new-passphrase"]);
            await Assert.That(reopened.ProfileId).IsEqualTo(first.ProfileId);
            await Assert.That(reopened.Mode).IsEqualTo(ProfileProtectionMode.ImportUpgradeOnly);
            await SafeSinks(io, error);
            await Assert.That(Encoding.UTF8.GetString(io.StandardOutput)).DoesNotContain(root);
            await Assert.That(CliMachineContractVerifier.Validate(discoveryReceipt)).IsEmpty();
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }

    [Test]
    [Arguments("compare-only")]
    [Arguments("inspectable-encrypted")]
    public async Task OtherPersistenceModesKeepTheirDistinctRestoreContracts(string mode)
    {
        string root = Path.Combine(Path.GetTempPath(), "configurator-cli-modes-" + Guid.NewGuid().ToString("N"));
        var io = await EncryptedFixture();
        var error = new CliFixture();
        try
        {
            string[] credentials = mode == "compare-only" ? [] : ["--passphrase-file", "private-passphrase-path-sentinel",
                "--confirm-passphrase-file", "private-passphrase-path-sentinel"];
            CliExitCode exit = await Run(io, error, io, ["profile", "save", "--mode", mode, "--store", root,
                "--definition", "definition.json", "--input", "answers.json",
                "--environment-context", "synthetic-environment", .. credentials, "--machine"]);
            if (!OperatingSystem.IsLinux())
            {
                await Assert.That(exit).IsEqualTo(CliExitCode.Blocked);
                return;
            }
            await Assert.That(exit).IsEqualTo(CliExitCode.Success);
            var store = new LocalProfileVaultStore(root);
            ProfileRevisionDescriptor revision = (await store.ListAsync()).Single();
            byte[] saved = await store.ReadAsync(revision);
            if (mode == "compare-only")
            {
                await Assert.That(revision.Kind).IsEqualTo(ProfileFileKind.Comparison);
                await Assert.That(Encoding.UTF8.GetString(saved)).DoesNotContain("private-answer-marker");
                string[] identity = ["--store", root, "--profile-id", revision.ProfileId.ToString(),
                    "--snapshot-id", revision.SnapshotId.ToString()];
                await Assert.That(await Run(io, error, io, ["profile", "diff", .. identity,
                    "--comparison", "--definition", "definition.json", "--source-definition", "definition.json", "--machine"]))
                    .IsEqualTo(CliExitCode.Success);
            }
            else
            {
                using var codec = new NativeProfileContainerCodec();
                using UnlockedProfile profile = await codec.OpenStoredAsync(store, revision, 0,
                    io.Files["private-passphrase-path-sentinel"]);
                await Assert.That(profile.Mode).IsEqualTo(ProfileProtectionMode.InspectableEncrypted);
            }
            await SafeSinks(io, error);
            await Assert.That(CliMachineContractVerifier.Validate(io.StandardOutput)).IsEmpty();
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }

    [Test]
    [Arguments("diff")]
    [Arguments("upgrade-plan")]
    [Arguments("adopt")]
    public async Task EncryptedDayTwoOperationsUseAuthenticatedEmbeddedHistoryWithoutExternalSource(string operation)
    {
        var io = await EncryptedFixture();
        JsonNode targetDocument = JsonNode.Parse(io.Files["definition.json"])!;
        targetDocument["identity"]!["revision"] = 2;
        targetDocument["project"]!["name"] = "Synthetic Day Two";
        io.Files["target-definition.json"] = Encoding.UTF8.GetBytes(targetDocument.ToJsonString());
        Definition target = DefinitionSourceCodec.ParseJson(io.Files["target-definition.json"]);
        using (var codec = new NativeProfileContainerCodec())
        using (UnlockedProfile profile = await codec.OpenAsync(io.Files["profile.cfgvault"], 0,
            io.Files["private-passphrase-path-sentinel"]))
        {
            await Assert.That(profile.DefinitionRevision).IsEqualTo(1L);
            await Assert.That(target.Revision).IsEqualTo(2L);
            await Assert.That(ProfileUpgradePlanner.Plan(profile, target).SourceSchemaAvailable).IsTrue();
        }
        var error = new CliFixture();
        string[] destination = operation == "adopt" ? ["--artifact", "config", "--output", "day-two-target.json"] : [];
        CliExitCode exit = await Run(io, error, io, ["profile", operation, "--input", "profile.cfgvault",
            "--definition", "target-definition.json", "--passphrase-file", "private-passphrase-path-sentinel",
            .. destination, "--machine"]);
        await Assert.That(exit).IsEqualTo(CliExitCode.Success);
        if (operation == "adopt")
        {
            using JsonDocument artifact = JsonDocument.Parse(io.Files["day-two-target.json"]);
            await Assert.That(artifact.RootElement.GetProperty("name").GetString()).IsEqualTo("private-answer-marker");
        }
        else
            await Assert.That(Encoding.UTF8.GetString(io.StandardOutput)).Contains("profile-revalidation-required");
        await SafeSinks(io, error);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task BeforeBaselineEvidenceIsIndependentOfCurrentEncryptedProfileHistory(bool provideBeforeSource)
    {
        var io = await EncryptedFixture(2);
        JsonNode beforeDocument = JsonNode.Parse(io.Files["definition.json"])!;
        beforeDocument["identity"]!["revision"] = 1;
        io.Files["before-definition.json"] = Encoding.UTF8.GetBytes(beforeDocument.ToJsonString());
        Definition beforeDefinition = DefinitionSourceCodec.ParseJson(io.Files["before-definition.json"]);
        Definition target = DefinitionSourceCodec.ParseJson(io.Files["definition.json"]);
        AnswerSnapshot answers = AnswerSnapshotCodec.ParseJson(io.Files["answers.json"], 0);
        CompareBaselineSnapshot before = CompareBaselineCodec.Create(beforeDefinition, answers,
            Guid.NewGuid(), Guid.NewGuid(), "synthetic-environment");
        io.Files["before.cfgcompare.json"] = CompareBaselineCodec.Serialize(before);
        using (var codec = new NativeProfileContainerCodec())
        using (UnlockedProfile profile = await codec.OpenAsync(io.Files["profile.cfgvault"], 0,
            io.Files["private-passphrase-path-sentinel"]))
        {
            await Assert.That(profile.DefinitionRevision).IsEqualTo(2L);
            await Assert.That(before.DefinitionRevision).IsEqualTo(1L);
            ProfileUpgradePlan control = ProfileUpgradePlanner.Compare(before, CompareBaselineCodec.Create(profile),
                provideBeforeSource ? beforeDefinition : null, target, "synthetic-environment");
            await Assert.That(control.SourceSchemaAvailable).IsEqualTo(provideBeforeSource);
        }
        var error = new CliFixture();
        string[] evidence = provideBeforeSource ? ["--source-definition", "before-definition.json"] : [];
        CliExitCode exit = await Run(io, error, io, ["profile", "diff", "--input", "profile.cfgvault",
            "--definition", "definition.json", "--baseline", "before.cfgcompare.json", .. evidence,
            "--passphrase-file", "private-passphrase-path-sentinel", "--machine"]);
        await Assert.That(exit).IsEqualTo(CliExitCode.Success);
        await Assert.That(Encoding.UTF8.GetString(io.StandardOutput)).Contains(provideBeforeSource
            ? "profile-source-schema-available" : "profile-source-schema-unavailable");
        await SafeSinks(io, error);
    }

    [Test]
    [Arguments("missing")]
    [Arguments("newline")]
    [Arguments("unicode-normalization")]
    [Arguments("oversized")]
    public async Task CreatingPortableRecipientRejectsMissingOrMismatchedConfirmationBeforeNativeSideEffects(string scenario)
    {
        bool missing = scenario == "missing";
        var io = new CliFixture();
        io.Files["primary"] = scenario == "unicode-normalization"
            ? Encoding.UTF8.GetBytes("synthetic-portable-\u00e9") : "synthetic-portable-passphrase"u8.ToArray();
        io.Files["confirmation"] = scenario switch
        {
            "unicode-normalization" => Encoding.UTF8.GetBytes("synthetic-portable-e\u0301"),
            "oversized" => new byte[1025],
            _ => "synthetic-portable-passphrase\n"u8.ToArray()
        };
        var input = new TrackingInput(io);
        var error = new CliFixture();
        var store = new CountingStore(Path.Combine(Path.GetTempPath(), "unused-cli-profile-" + Guid.NewGuid().ToString("N")));
        var keyring = new CountingKeyring();
        string[] confirmation = missing ? [] : ["--confirm-passphrase-file", "confirmation"];
        CliExitCode exit = await new CliApplication(_ => store, () => keyring).RunAsync(new(
            ["profile", "save", "--store", "/synthetic/store", "--definition", "definition.json",
                "--input", "answers.json", "--mode", "import-upgrade-only", "--environment-context", "synthetic-environment",
                "--device", "--passphrase-file", "primary", .. confirmation, "--machine"],
            CliMode.Text, new(input, io, error, io.AuthoringFiles, 65_536, io.MaximumBytes)));
        await Assert.That(store.Writes).IsEqualTo(0);
        await Assert.That(keyring.Creations).IsEqualTo(0);
        await Assert.That(exit).IsEqualTo(missing ? CliExitCode.Usage : CliExitCode.Data);
        await Assert.That(Code(io)).IsEqualTo(missing ? "passphrase-confirmation-required"
            : scenario == "oversized" ? "profile-invalid-passphrase" : "passphrase-confirmation-mismatch");
        if (!missing) await Assert.That(input.Bounds["confirmation"]).IsEqualTo(1024);
        await SafeSinks(io, error);
    }

    [Test]
    [Arguments("missing")]
    [Arguments("empty")]
    [Arguments("oversized")]
    public async Task EncryptedSaveRequiresExplicitBoundedEnvironmentBeforeInput(string scenario)
    {
        var io = new CliFixture();
        string[] context = scenario == "missing" ? []
            : ["--environment-context", scenario == "empty" ? "" : new string('x', 257)];
        CliExitCode exit = await io.RunAsync(["profile", "save", "--store", "/synthetic/store",
            "--definition", "definition.json", "--input", "answers.json", "--mode", "import-upgrade-only",
            "--passphrase-file", "primary", "--confirm-passphrase-file", "confirmation", .. context, "--machine"]);
        await Assert.That(exit).IsEqualTo(CliExitCode.Usage);
        await Assert.That(Code(io)).IsEqualTo(scenario == "missing"
            ? "environment-context-required" : "environment-context-invalid");
        await Assert.That(io.MaximumRead).IsEqualTo(0);
    }

    [Test]
    [Arguments("save")]
    [Arguments("slots")]
    public async Task CreatingRecipientCannotShareConfirmationStdinWithAnotherInput(string operation)
    {
        var io = new CliFixture();
        string[] arguments = operation == "save"
            ? ["profile", "save", "--store", "/synthetic/store", "--definition", "definition.json",
                "--input", "-", "--mode", "import-upgrade-only", "--environment-context", "synthetic-environment",
                "--passphrase-file", "primary", "--confirm-passphrase-stdin", "--machine"]
            : ["profile", "slots", "--store", "/synthetic/store",
                "--profile-id", "3b781cb4-a04b-482c-9b14-5c44cbdb4d94",
                "--snapshot-id", "bd940986-793e-465c-9ae8-f68a4792f485",
                "--action", "reseal", "--passphrase-file", "old-credential",
                "--new-passphrase-stdin", "--confirm-passphrase-stdin", "--machine"];
        await Assert.That(await io.RunAsync(arguments)).IsEqualTo(CliExitCode.Usage);
        await Assert.That(Code(io)).IsEqualTo("stdin-conflict");
        await Assert.That(io.MaximumRead).IsEqualTo(0);
    }

    [Test]
    public async Task MatchingPortableConfirmationEnablesLegitimateCreation()
    {
        string root = Path.Combine(Path.GetTempPath(), "configurator-cli-confirmed-" + Guid.NewGuid().ToString("N"));
        var io = new CliFixture();
        io.Files["primary"] = "synthetic-portable-passphrase"u8.ToArray();
        io.Files["confirmation"] = "synthetic-portable-passphrase"u8.ToArray();
        var error = new CliFixture();
        try
        {
            CliExitCode exit = await Run(io, error, io, "profile", "save", "--store", root,
                "--definition", "definition.json", "--input", "answers.json", "--mode", "import-upgrade-only",
                "--environment-context", "synthetic-environment", "--passphrase-file", "primary",
                "--confirm-passphrase-file", "confirmation", "--machine");
            await Assert.That(exit).IsEqualTo(OperatingSystem.IsLinux() ? CliExitCode.Success : CliExitCode.Blocked);
            if (OperatingSystem.IsLinux())
                await Assert.That((await new LocalProfileVaultStore(root).ListAsync()).Count).IsEqualTo(1);
            await SafeSinks(io, error);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }

    [Test]
    public async Task ResealConfirmationMismatchPreservesExistingSnapshotWithoutStoreWriteOrKeyCreation()
    {
        string root = Path.Combine(Path.GetTempPath(), "configurator-cli-confirm-reseal-" + Guid.NewGuid().ToString("N"));
        var io = await EncryptedFixture();
        var error = new CliFixture();
        try
        {
            using var codec = new NativeProfileContainerCodec();
            using UnlockedProfile profile = await codec.OpenAsync(io.Files["profile.cfgvault"], 0,
                io.Files["private-passphrase-path-sentinel"]);
            var native = new LocalProfileVaultStore(root);
            var initial = await native.SaveAsync(profile.ProfileId, profile.SnapshotId, ProfileFileKind.Encrypted,
                io.Files["profile.cfgvault"]);
            await Assert.That(initial.Succeeded).IsEqualTo(OperatingSystem.IsLinux());
            if (!OperatingSystem.IsLinux()) return;
            var store = new CountingStore(root);
            var keyring = new CountingKeyring();
            io.Files["new-primary"] = "different-portable-passphrase"u8.ToArray();
            io.Files["confirmation"] = "different-portable-passphrase\n"u8.ToArray();
            CliExitCode exit = await new CliApplication(_ => store, () => keyring).RunAsync(new(
                ["profile", "slots", "--store", root, "--profile-id", profile.ProfileId.ToString(),
                    "--snapshot-id", profile.SnapshotId.ToString(), "--action", "reseal",
                    "--passphrase-file", "private-passphrase-path-sentinel", "--new-passphrase-file", "new-primary",
                    "--confirm-passphrase-file", "confirmation", "--machine"], CliMode.Text,
                new(io, io, error, io.AuthoringFiles, 65_536, io.MaximumBytes)));
            await Assert.That(store.Writes).IsEqualTo(0);
            await Assert.That(keyring.Creations).IsEqualTo(0);
            await Assert.That(exit).IsEqualTo(CliExitCode.Data);
            await Assert.That(Code(io)).IsEqualTo("passphrase-confirmation-mismatch");
            ProfileRevisionDescriptor revision = (await native.ListAsync()).Single();
            await Assert.That(await native.ReadAsync(revision)).IsEquivalentTo(io.Files["profile.cfgvault"]);
            await SafeSinks(io, error);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }

    private static async Task<CliFixture> EncryptedFixture(int revision = 1)
    {
        var io = new CliFixture();
        JsonNode definition = JsonNode.Parse(io.Files["definition.json"])!;
        definition["identity"]!["revision"] = revision;
        definition["configuration"]!["properties"]!["service.name"]!["x-configurator"]!["secret"] =
            JsonNode.Parse("""{"kind":"password","acquisition":"existing"}""");
        definition["configuration"]!["properties"]!["service.name"]!["x-configurator"]!["sensitivity"] = "secret";
        io.Files["definition.json"] = Encoding.UTF8.GetBytes(definition.ToJsonString());
        Definition admitted = DefinitionSourceCodec.ParseJson(io.Files["definition.json"]);
        AnswerSnapshot answers = AnswerSnapshotCodec.ParseJson(io.Files["answers.json"], 0);
        // Admit the fixture independently so parser rejection cannot stand in for custody evidence.
        await Assert.That(DefinitionValidator.Validate(admitted, answers).IsReady).IsTrue();
        byte[] payload = ProfilePayloadCodec.Serialize(admitted, answers, ProfileProtectionMode.ImportUpgradeOnly,
            Guid.NewGuid(), Guid.NewGuid(), "synthetic", "synthetic-environment");
        try
        {
            using UnlockedProfile profile = ProfilePayloadCodec.Admit(payload);
            using var codec = new NativeProfileContainerCodec();
            using ProfileSealedSnapshot sealedProfile = await ProfileRecipientLifecycle.SealAsync(profile, codec,
                new ReadOnlyMemory<byte>[] { "synthetic-portable-passphrase"u8.ToArray() });
            io.Files["profile.cfgvault"] = sealedProfile.CopyContainer();
            io.Files["private-passphrase-path-sentinel"] = "synthetic-portable-passphrase"u8.ToArray();
            return io;
        }
        finally { CryptographicOperations.ZeroMemory(payload); }
    }

    private static Task<CliExitCode> Run(CliFixture output, CliFixture error, ICliInput input, params string[] arguments) =>
        new CliApplication(root => new LocalProfileVaultStore(root), () => new ForbiddenKeyring()).RunAsync(
            new(arguments, CliMode.Text, new(input, output, error, output.AuthoringFiles, 65_536, output.MaximumBytes)));

    private static async Task SafeSinks(CliFixture output, CliFixture error)
    {
        foreach (byte[] sink in new[] { output.StandardOutput, error.StandardOutput })
        {
            string text = Encoding.UTF8.GetString(sink);
            await Assert.That(text).DoesNotContain("private-answer-marker");
            await Assert.That(text).DoesNotContain("synthetic-portable-passphrase");
            await Assert.That(text).DoesNotContain("different-portable-passphrase");
            await Assert.That(text).DoesNotContain("private-passphrase-path-sentinel");
            await Assert.That(text).DoesNotContain("wrong-private-passphrase-sentinel");
        }
    }

    private sealed class TrackingInput(ICliInput input) : ICliInput
    {
        internal Dictionary<string, int> Bounds { get; } = [];
        internal List<string> Reads { get; } = [];
        internal List<ReadOnlyMemory<byte>> CredentialCopies { get; } = [];
        public async ValueTask<CliInputBuffer> ReadAsync(string path, int maximumBytes, CancellationToken cancellationToken)
        {
            Bounds[path] = maximumBytes;
            Reads.Add(path);
            CliInputBuffer buffer = await input.ReadAsync(path, maximumBytes, cancellationToken);
            if (path == "private-passphrase-path-sentinel") CredentialCopies.Add(buffer.Bytes);
            return buffer;
        }
    }

    private sealed class ForbiddenKeyring : IOsKeyringProvider
    {
        public ValueTask<OsKeyringResult> LookupAsync(Guid keyId, OsKeyringPurpose purpose, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("keyring-must-not-be-used");
        public ValueTask<OsKeyringResult> CreateAsync(OsKeyringPurpose purpose, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("keyring-must-not-be-used");
    }

    private sealed class CountingStore(string root) : IProfileVaultStore
    {
        private readonly LocalProfileVaultStore _store = new(root);
        internal int Writes { get; private set; }
        public ValueTask<IReadOnlyList<ProfileRevisionDescriptor>> ListAsync(CancellationToken cancellationToken = default) =>
            _store.ListAsync(cancellationToken);
        public ValueTask<byte[]> ReadAsync(ProfileRevisionDescriptor revision, CancellationToken cancellationToken = default) =>
            _store.ReadAsync(revision, cancellationToken);
        public Task<Configurator.Artifacts.ProtectedArtifactBundleResult> SaveAsync(Guid profileId, Guid snapshotId,
            ProfileFileKind kind, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken = default)
        {
            Writes++;
            return _store.SaveAsync(profileId, snapshotId, kind, bytes, cancellationToken);
        }
    }

    private sealed class CountingKeyring : IOsKeyringProvider
    {
        internal int Creations { get; private set; }
        public ValueTask<OsKeyringResult> LookupAsync(Guid keyId, OsKeyringPurpose purpose, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("keyring-must-not-be-used");
        public ValueTask<OsKeyringResult> CreateAsync(OsKeyringPurpose purpose, CancellationToken cancellationToken = default)
        {
            Creations++;
            throw new InvalidOperationException("keyring-must-not-be-used");
        }
    }

    private static string? Code(CliFixture io)
    {
        using JsonDocument document = JsonDocument.Parse(io.StandardOutput);
        return document.RootElement.GetProperty("diagnostics")[0].GetProperty("code").GetString();
    }
}
