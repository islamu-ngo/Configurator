using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Configurator.ReleaseEngineering;

namespace Configurator.ReleaseEngineering.Tests;

[NotInParallel("RuntimePromotionTrustRoot")]
public sealed class ReleaseCandidateVerificationTests
{
    [Test]
    public async Task VerifyCandidateRejectsMissingDetachedPackageEvidence()
    {
        if (OperatingSystem.IsWindows()) return;
        using var fixture = CandidateFixture.Create();

        (int code, _) = fixture.Verify(fixture.B, includePackageEvidence: false);

        await Assert.That(code).IsEqualTo(Program.ToolchainRejected);
        await Assert.That(File.Exists(fixture.CandidateManifestPath)).IsFalse();
    }

    [Test]
    [Arguments("payload")]
    [Arguments("private-session")]
    [Arguments("expected-digest")]
    [Arguments("claimed-profile")]
    [Arguments("caller-profile")]
    [Arguments("evidence")]
    public async Task CandidateRejectsUnboundOrAlteredPackageEvidence(string attack)
    {
        if (OperatingSystem.IsWindows()) return;
        using var fixture = CandidateFixture.Create();
        PackageEvidenceFixture evidence = fixture.PackageEvidence;
        string[] arguments = evidence.Arguments;
        switch (attack)
        {
            case "payload": File.WriteAllText(Path.Combine(evidence.Package, "app.txt"), "altered"); break;
            case "private-session": Directory.Delete(Path.Combine(evidence.Session, "private"), true); break;
            case "expected-digest": arguments[^1] = new string('0', 64); break;
            case "claimed-profile":
                var manifest = JsonNode.Parse(File.ReadAllBytes(evidence.Manifest))!;
                manifest["requiredChecks"] = new JsonArray();
                byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(manifest);
                File.WriteAllBytes(evidence.Manifest, bytes);
                arguments[^1] = Sha256(bytes);
                break;
            case "caller-profile": arguments = [.. arguments, "--required-checks", "none"]; break;
            case "evidence": File.AppendAllText(Path.Combine(evidence.Evidence, "Configurator.Core.report.json"), "changed"); break;
        }

        (int code, _) = fixture.Verify(fixture.B, packageArguments: arguments);

        await Assert.That(code).IsEqualTo(Program.ToolchainRejected).Because(attack);
        await Assert.That(File.Exists(fixture.CandidateManifestPath)).IsFalse();
    }

    [Test]
    public async Task CandidateRequiresEveryCompiledOwnerRegardlessOfManifestClaims()
    {
        if (OperatingSystem.IsWindows()) return;
        using var fixture = CandidateFixture.Create();
        byte[] original = File.ReadAllBytes(fixture.PackageEvidence.Checks);
        var checks = JsonNode.Parse(original)!.AsArray();
        foreach (JsonNode? omitted in checks)
        {
            string owner = omitted!["owner"]!.GetValue<string>();
            var remaining = new JsonArray(checks.Where(check => check!["owner"]!.GetValue<string>() != owner)
                .Select(check => check!.DeepClone()).ToArray());
            File.WriteAllBytes(fixture.PackageEvidence.Checks, JsonSerializer.SerializeToUtf8Bytes(remaining));

            (int code, _) = fixture.Verify(fixture.B);

            await Assert.That(code).IsEqualTo(Program.ToolchainRejected).Because(owner);
            await Assert.That(File.Exists(fixture.CandidateManifestPath)).IsFalse();
        }
    }

    [Test]
    public async Task CandidateRejectsAuthenticatedIncompleteVerifierProfileWithoutThrowing()
    {
        if (OperatingSystem.IsWindows()) return;
        using var fixture = CandidateFixture.Create();
        fixture.RemoveVerifierAssetAndReadmit();

        (int code, string output) = fixture.Verify(fixture.B);

        await Assert.That(code).IsEqualTo(Program.ToolchainRejected);
        await Assert.That(output).IsEqualTo("verify_candidate_failed: candidate_input_invalid\n");
        await Assert.That(File.Exists(fixture.CandidateManifestPath)).IsFalse();
    }

    [Test]
    public async Task CandidateNeverExecutesRepositoryGitFromInheritedPath()
    {
        if (!OperatingSystem.IsLinux()) return;
        using var fixture = CandidateFixture.Create();
        string poison = Path.Combine(fixture.Root, "poison");
        Directory.CreateDirectory(poison);
        string executable = Path.Combine(poison, "git");
        string marker = Path.Combine(poison, "executed");
        File.WriteAllText(executable, "#!/bin/sh\n: > \"" + marker + "\"\nexit 1\n");
        File.SetUnixFileMode(executable, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        string? original = Environment.GetEnvironmentVariable("PATH");
        try
        {
            Environment.SetEnvironmentVariable("PATH", poison + ":/usr/bin");

            (int code, string output) = fixture.Verify(fixture.B);

            await Assert.That(File.Exists(marker)).IsFalse().Because("Candidate verification must not execute inherited-path tooling.");
            await Assert.That(code).IsEqualTo(Program.Success).Because(output);
        }
        finally { Environment.SetEnvironmentVariable("PATH", original); }
    }

    [Test]
    [Arguments("dirty")]
    [Arguments("missing")]
    [Arguments("moved")]
    [Arguments("other-commit")]
    [Arguments("symlink")]
    [Arguments("hardlink")]
    public async Task PackageSourceProjectionBindsRawBlobsOfExactCandidate(string state)
    {
        if (OperatingSystem.IsWindows()) return;
        using var fixture = CandidateFixture.Create();
        string source = Path.Combine(fixture.RepositoryPath, "src/Configurator.Core/fixture.txt");
        if (state is "dirty" or "other-commit") File.WriteAllText(source, "different relevant bytes\n");
        if (state == "missing") File.Delete(source);
        if (state == "moved") File.Move(source, source + ".moved");
        if (state == "symlink")
        {
            File.Move(source, source + ".outside");
            File.CreateSymbolicLink(source, source + ".outside");
        }
        if (state == "other-commit") fixture.CommitDifferentSourceAndRecapture();
        if (state == "hardlink") fixture.HardlinkSource(source);

        (int code, _) = fixture.Verify(fixture.B);

        await Assert.That(code).IsEqualTo(Program.ToolchainRejected).Because(state);
        await Assert.That(File.Exists(fixture.CandidateManifestPath)).IsFalse();
    }

    [Test]
    [Arguments("dirty")]
    [Arguments("staged")]
    [Arguments("missing")]
    [Arguments("other-commit")]
    [Arguments("conflicting-capture")]
    public async Task FullCapturedTestInputsBindExactCandidateOutsideProductProjection(string state)
    {
        if (OperatingSystem.IsWindows()) return;
        using var fixture = CandidateFixture.Create();
        string source = Path.Combine(fixture.RepositoryPath, PackageEvidenceFixture.TestSource);
        string publicationSource = fixture.PackageEvidence.Source;
        string publicationBuild = fixture.PackageEvidence.Build;
        string payload = fixture.PackageEvidence.Payload;
        using (JsonDocument check = JsonDocument.Parse(File.ReadAllBytes(
            Path.Combine(fixture.PackageEvidence.CheckSession("Configurator.Core"), "capture.json"))))
        {
            string logical = "repo/" + PackageEvidenceFixture.TestSource;
            await Assert.That(check.RootElement.GetProperty("inputs").EnumerateArray()
                .Any(input => input.GetProperty("path").GetString() == logical)).IsTrue();
            await Assert.That(check.RootElement.GetProperty("productSourceProjection").EnumerateArray()
                .Any(input => input.GetProperty("path").GetString() == logical)).IsFalse();
            await Assert.That(check.RootElement.GetProperty("productSourceInputsDigest").GetString())
                .IsNotEqualTo(publicationSource);
        }
        if (state is "dirty" or "staged" or "other-commit") File.AppendAllText(source, "// Different test bytes.\n");
        if (state == "staged") fixture.StageTestSource();
        if (state == "missing") File.Delete(source);
        if (state == "other-commit") fixture.CommitDifferentSourceAndRecapture();
        if (state == "conflicting-capture") fixture.RecaptureConflictingTestInput();
        await Assert.That(fixture.PackageEvidence.Source).IsEqualTo(publicationSource);
        await Assert.That(fixture.PackageEvidence.Build).IsEqualTo(publicationBuild);
        await Assert.That(fixture.PackageEvidence.Payload).IsEqualTo(payload);

        (int code, string output) = fixture.Verify(fixture.B);

        await Assert.That(code).IsEqualTo(Program.ToolchainRejected).Because(state + ": " + output);
        await Assert.That(File.Exists(fixture.CandidateManifestPath)).IsFalse();
    }

    [Test]
    [Arguments("product-add")]
    [Arguments("test-add")]
    [Arguments("resource-add")]
    [Arguments("import-add")]
    [Arguments("remove")]
    [Arguments("rename")]
    public async Task CandidateRepositoryNamespaceRejectsUnconsumedNameChanges(string change)
    {
        if (OperatingSystem.IsWindows()) return;
        using var fixture = CandidateFixture.Create();
        string candidate = fixture.ChangeCandidateNamespace(change);

        (int code, string output) = fixture.Verify(candidate);

        await Assert.That(code).IsEqualTo(Program.ToolchainRejected).Because(change + ": " + output);
        await Assert.That(File.Exists(fixture.CandidateManifestPath)).IsFalse();
    }

    [Test]
    [Arguments("120000")]
    [Arguments("160000")]
    public async Task CandidateRepositoryNamespaceRejectsEligibleNonregularGitModes(string mode)
    {
        if (OperatingSystem.IsWindows()) return;
        using var fixture = CandidateFixture.Create();
        string candidate = fixture.ChangeCandidateMode(PackageEvidenceFixture.UnconsumedSource, mode);

        (int code, string output) = fixture.Verify(candidate);

        await Assert.That(code).IsEqualTo(Program.ToolchainRejected).Because(mode + ": " + output);
        await Assert.That(File.Exists(fixture.CandidateManifestPath)).IsFalse();
    }

    [Test]
    [Arguments("Configurator.Core")]
    [Arguments("Configurator.Terminal")]
    public async Task CandidateRepositoryNamespaceRejectsOneMismatchingRequiredCheck(string owner)
    {
        if (OperatingSystem.IsWindows()) return;
        using var fixture = CandidateFixture.Create();
        fixture.PackageEvidence.OmitUnconsumedNamespaceFromCheck(owner);

        (int code, string output) = fixture.Verify(fixture.B);

        await Assert.That(code).IsEqualTo(Program.ToolchainRejected).Because(owner + ": " + output);
        await Assert.That(File.Exists(fixture.CandidateManifestPath)).IsFalse();
    }

    [Test]
    public async Task CandidateRepositoryNamespaceRejectsInvalidUtf8GitNames()
    {
        if (OperatingSystem.IsWindows()) return;
        using var fixture = CandidateFixture.Create();
        string candidate = fixture.CandidateWithInvalidUtf8Name();

        (int code, string output) = fixture.Verify(candidate);

        await Assert.That(code).IsEqualTo(Program.ToolchainRejected).Because(output);
        await Assert.That(File.Exists(fixture.CandidateManifestPath)).IsFalse();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task CandidateRepositoryNamespaceUsesExactObjectInsteadOfHeadOrCheckout(bool committed)
    {
        if (OperatingSystem.IsWindows()) return;
        using var fixture = CandidateFixture.Create();
        fixture.AddUnrelatedEligibleWorkingFile(committed);

        (int code, string output) = fixture.Verify(fixture.B);

        await Assert.That(code).IsEqualTo(Program.Success).Because(output);
    }

    [Test]
    public async Task CandidateRepositoryNamespaceAllowsExcludedMetadataLinks()
    {
        if (OperatingSystem.IsWindows()) return;
        using var fixture = CandidateFixture.Create();
        string candidate = fixture.ChangeCandidateMode("docs/unrelated-link", "120000");

        (int code, string output) = fixture.Verify(candidate);

        await Assert.That(code).IsEqualTo(Program.Success).Because(output);
    }

    [Test]
    public async Task DetachedMetadataCommitPreservesExactCandidatePackageBinding()
    {
        if (OperatingSystem.IsWindows()) return;
        using var fixture = CandidateFixture.Create();
        fixture.CommitUnrelatedMetadata();

        (int code, string output) = fixture.Verify(fixture.B);

        await Assert.That(code).IsEqualTo(Program.Success).Because(output);
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllBytes(fixture.CandidateManifestPath));
        await Assert.That(manifest.RootElement.GetProperty("packageProvenanceSha256").GetString()).IsEqualTo(fixture.PackageEvidence.ExpectedDigest);
        await Assert.That(manifest.RootElement.GetProperty("payloadDigest").GetString()).IsEqualTo(fixture.PackageEvidence.Payload);
        await Assert.That(manifest.RootElement.GetProperty("candidateOid").GetString()).IsEqualTo(fixture.B);
    }

    [Test]
    [Arguments("untracked")]
    [Arguments("staged")]
    [Arguments("modified")]
    [Arguments("deleted")]
    public async Task CandidateRejectsUncommittedApplicableGroupCorrections(string state)
    {
        if (OperatingSystem.IsWindows()) return;
        using var fixture = CandidateFixture.CreateWithGroupCorrection();
        if (state == "modified") File.AppendAllText(fixture.CorrectionPath, "# changed\n");
        if (state == "deleted") File.Delete(fixture.CorrectionPath);
        if (state is "untracked" or "staged")
        {
            using var plain = CandidateFixture.Create();
            Directory.CreateDirectory(Path.GetDirectoryName(plain.CorrectionPath)!);
            File.WriteAllText(plain.CorrectionPath, "Schema-Version: fragment-group-correction.v1\n");
            if (state == "staged") plain.StageCorrection();
            (int plainCode, _) = plain.Verify(plain.B);
            await Assert.That(plainCode).IsEqualTo(Program.ToolchainRejected);
            return;
        }
        (int code, _) = fixture.Verify(fixture.B);
        await Assert.That(code).IsEqualTo(Program.ToolchainRejected);
    }

    [Test]
    public async Task CandidateHashesCommittedCorrectionWithoutChangingOriginalContext()
    {
        if (OperatingSystem.IsWindows()) return;
        using var fixture = CandidateFixture.CreateWithGroupCorrection();
        (int code, string output) = fixture.Verify(fixture.B);
        await Assert.That(code).IsEqualTo(Program.Success).Because(output);
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllBytes(fixture.CandidateManifestPath));
        string expected = fixture.NormalizedFragment + "\n---\n" + File.ReadAllText(fixture.CorrectionPath);
        await Assert.That(manifest.RootElement.GetProperty("releaseFragmentsSha256").GetString())
            .IsEqualTo(Sha256(System.Text.Encoding.UTF8.GetBytes(expected)));
        using JsonDocument context = JsonDocument.Parse(File.ReadAllBytes(fixture.ContextPath));
        JsonElement change = context.RootElement.GetProperty("changes").EnumerateArray()
            .Single(value => value.TryGetProperty("changeId", out JsonElement id) && id.GetString() == "CHG-2026-0001");
        await Assert.That(change.GetProperty("scope").GetString()).IsEqualTo("self-hosting");
        await Assert.That(change.GetProperty("oid").GetString()).IsEqualTo(fixture.A);
        await Assert.That(context.RootElement.GetProperty("release").GetProperty("minimumBump").GetString()).IsEqualTo("minor");
    }

    [Test]
    public async Task CandidateRejectsCorrectionSubstitutionFromDifferentCommittedTree()
    {
        if (OperatingSystem.IsWindows()) return;
        using var fixture = CandidateFixture.CreateWithGroupCorrection();
        fixture.CommitCorrectionSubstitution();

        (int code, _) = fixture.Verify(fixture.B);

        await Assert.That(code).IsEqualTo(Program.ToolchainRejected);
        await Assert.That(File.Exists(fixture.CandidateManifestPath)).IsFalse();
    }

    [Test]
    public async Task CandidateIgnoresUnlinkedGroupCorrectionInputs()
    {
        if (OperatingSystem.IsWindows()) return;
        using var fixture = CandidateFixture.Create();
        string path = Path.Join(fixture.RepositoryPath, "docs/internal/releases/group-corrections/CHG-2026-9999.yaml");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "not a correction\n");

        (int code, string output) = fixture.Verify(fixture.B);

        await Assert.That(code).IsEqualTo(Program.Success).Because(output);
    }

    [Test]
    public async Task VerifyCandidateAcceptsExactBAndWritesDeterministicManifestTwice()
    {
        if (OperatingSystem.IsWindows()) return;

        using var fixture = CandidateFixture.Create();

        (int firstCode, string firstOutput) = fixture.Verify(fixture.B);
        byte[] firstBytes = File.ReadAllBytes(fixture.CandidateManifestPath);
        string firstDigest = Sha256(firstBytes);
        (int secondCode, string secondOutput) = fixture.Verify(fixture.B);
        byte[] secondBytes = File.ReadAllBytes(fixture.CandidateManifestPath);

        using JsonDocument document = JsonDocument.Parse(firstBytes);
        JsonElement root = document.RootElement;
        await Assert.That(firstCode).IsEqualTo(Program.Success);
        await Assert.That(secondCode).IsEqualTo(Program.Success);
        await Assert.That(secondOutput).IsEqualTo(firstOutput);
        await Assert.That(secondBytes).IsEquivalentTo(firstBytes);
        await Assert.That(firstOutput).IsEqualTo("release_candidate_verified: docs/internal/releases/1.1.0/release-candidate.v1.json\n");
        await Assert.That(root.GetProperty("schemaVersion").GetString()).IsEqualTo("release-candidate.v1");
        await Assert.That(root.GetProperty("objectFormat").GetString()).IsEqualTo(fixture.ObjectFormat);
        await Assert.That(root.GetProperty("candidateOid").GetString()).IsEqualTo(fixture.B);
        await Assert.That(root.GetProperty("candidateParentOid").GetString()).IsEqualTo(fixture.A);
        await Assert.That(root.TryGetProperty("releaseBranchRef", out _)).IsFalse();
        await Assert.That(root.TryGetProperty("releaseLineHeadOid", out _)).IsFalse();
        await Assert.That(root.GetProperty("expectedIntegrationOldOid").GetString()).IsEqualTo(fixture.A);
        await Assert.That(root.GetProperty("expectedIntegrationNewOid").GetString()).IsEqualTo(fixture.B);
        await Assert.That(root.GetProperty("baseStableRef").GetString()).IsEqualTo("refs/tags/v1.0.0");
        await Assert.That(root.GetProperty("previousPublishedRef").GetString()).IsEqualTo("refs/tags/v1.0.0");
        await Assert.That(root.GetProperty("trustedBundleTrustSha256").GetString()?.Length).IsEqualTo(64);
        await Assert.That(root.GetProperty("rangeOids").EnumerateArray().Select(item => item.GetString()!).ToArray()).IsEquivalentTo(new[] { fixture.A, fixture.B });
        await Assert.That(root.GetProperty("releaseContextSha256").GetString()).IsEqualTo(Sha256(File.ReadAllBytes(fixture.ContextPath)));
        await Assert.That(root.GetProperty("releaseNotesSha256").GetString()).IsEqualTo(Sha256(File.ReadAllBytes(fixture.NotesPath)));
        await Assert.That(root.TryGetProperty("tagObjectId", out _)).IsFalse();
        await Assert.That(root.ToString()).DoesNotContain("provider");
        await Assert.That(root.ToString()).DoesNotContain("@example");
        await Assert.That(firstDigest).IsEqualTo(Sha256(secondBytes));
    }

    [Test]
    public async Task VerifyCandidateRejectsParentADriftAndStaleManifest()
    {
        if (OperatingSystem.IsWindows()) return;

        using var parent = CandidateFixture.Create();
        (int parentCode, string parentOutput) = parent.Verify(parent.A);

        using var drift = CandidateFixture.Create();
        File.AppendAllText(drift.NotesPath, "manual drift\n");
        (int driftCode, string driftOutput) = drift.Verify(drift.B);

        using var stale = CandidateFixture.Create();
        File.WriteAllText(stale.CandidateManifestPath, "{}\n");
        (int staleCode, string staleOutput) = stale.Verify(stale.B);

        await Assert.That(parentCode).IsEqualTo(Program.ToolchainRejected);
        await Assert.That(parentOutput).IsEqualTo("verify_candidate_failed: candidate_committed_artifact_mismatch\n");
        await Assert.That(driftCode).IsEqualTo(Program.ToolchainRejected);
        await Assert.That(driftOutput).IsEqualTo("verify_candidate_failed: candidate_generated_artifacts_dirty\n");
        await Assert.That(staleCode).IsEqualTo(Program.ToolchainRejected);
        await Assert.That(staleOutput).IsEqualTo("verify_candidate_failed: candidate_manifest_stale\n");
    }

    [Test]
    [Explicit]
    [Category("Runtime")]
    [Arguments(false)]
    [Arguments(true)]
    public async Task VerifyCandidateRejectsCleanUnsignedBreakingNotesTamper(bool omitEntry)
    {
        Skip.Unless(OperatingSystem.IsLinux() && RuntimeInformation.ProcessArchitecture == Architecture.X64,
            "The governed-release fixture requires the pinned Linux x64 renderer.");

        using var fixture = GovernedReleaseFixture.CreateCategorizedFirstGovernedRelease();
        string version = GovernedReleaseFixture.FirstGovernedReleaseVersion;
        string directory = Path.Combine(fixture.RepositoryPath, "docs", "internal", "releases", version);
        string notesPath = Path.Combine(directory, "release-notes.md");
        byte[] contextBytes = await File.ReadAllBytesAsync(Path.Combine(directory, "release-context.v1.json"));
        using JsonDocument context = JsonDocument.Parse(contextBytes);
        JsonElement breaking = context.RootElement.GetProperty("changes").EnumerateArray()
            .Single(change => change.GetProperty("breaking").GetBoolean());
        await Assert.That(breaking.GetProperty("type").GetString()).IsEqualTo("fix");
        string notes = await File.ReadAllTextAsync(notesPath);
        string primaryEntry = notes.Split('\n').Single(line => line.StartsWith("- ", StringComparison.Ordinal) &&
            line.EndsWith($"({breaking.GetProperty("displayId").GetString()})", StringComparison.Ordinal));

        (int validCode, string validOutput) = fixture.VerifyCandidate(version, fixture.B);
        await Assert.That(validCode).IsEqualTo(Program.Success).Because(validOutput);
        string refsBefore = fixture.AllRefs();
        string tampered = notes.Replace(primaryEntry + "\n", omitEntry ? string.Empty :
            $"- self-hosting: No token replacement is required ({breaking.GetProperty("displayId").GetString()})\n", StringComparison.Ordinal);
        string alternativeB = fixture.CreateUnsignedCandidateWithNotes(version, tampered);

        // Exact committed files and footer must pass: only trusted byte recomposition may reject this sibling B.
        await Assert.That(alternativeB == fixture.B).IsFalse();
        await Assert.That(fixture.ResolveRef($"{alternativeB}^")).IsEqualTo(fixture.A);
        await Assert.That(fixture.WorkingTreeStatus()).IsEqualTo(string.Empty);
        await Assert.That(fixture.AllRefs()).IsEqualTo(refsBefore);
        await Assert.That(fixture.ResolveTagObject($"v{version}")).IsEqualTo(fixture.FirstTagObject);
        await Assert.That(await File.ReadAllBytesAsync(Path.Combine(directory, "release-context.v1.json"))).IsEquivalentTo(contextBytes);
        await Assert.That(await File.ReadAllTextAsync(notesPath)).IsEqualTo(tampered);
        await Assert.That(File.Exists(Path.Combine(directory, "release-candidate.v1.json"))).IsFalse();
        (int exitCode, string output) = fixture.VerifyCandidate(version, alternativeB);
        await Assert.That(exitCode).IsEqualTo(Program.ToolchainRejected).Because(output);
        await Assert.That(output).IsEqualTo("verify_candidate_failed: candidate_release_notes_mismatch\n");
        await Assert.That(File.Exists(Path.Combine(directory, "release-candidate.v1.json"))).IsFalse();
    }

    [Test]
    public async Task VerifyCandidateSupportsSha256RepositoriesWhenGitSupportsThem()
    {
        if (OperatingSystem.IsWindows()) return;

        using CandidateFixture? fixture = CandidateFixture.CreateSha256OrNull();
        if (fixture is null) return;

        (int exitCode, _) = fixture.Verify(fixture.B);
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(fixture.CandidateManifestPath));

        await Assert.That(exitCode).IsEqualTo(Program.Success);
        await Assert.That(document.RootElement.GetProperty("objectFormat").GetString()).IsEqualTo("sha256");
        await Assert.That(document.RootElement.GetProperty("candidateOid").GetString()?.Length).IsEqualTo(64);
    }

    [Test]
    public async Task VerifyCandidateFailsClosedForTrustFooterAndReleaseArtifactDrift()
    {
        if (OperatingSystem.IsWindows()) return;

        using var policy = CandidateFixture.Create();
        policy.DriftPolicy();
        (int policyCode, string policyOutput) = policy.Verify(policy.B);

        using var footer = CandidateFixture.Create();
        string replacedB = footer.ReplaceTerminalFooter();
        (int footerCode, string footerOutput) = footer.Verify(replacedB);

        using var summary = CandidateFixture.Create();
        summary.DriftSummary();
        (int summaryCode, string summaryOutput) = summary.Verify(summary.B);

        using var context = CandidateFixture.Create();
        context.DriftContext();
        (int contextCode, string contextOutput) = context.Verify(context.B);

        using var missing = CandidateFixture.Create();
        missing.DeleteNotes();
        (int missingCode, string missingOutput) = missing.Verify(missing.B);

        using var linked = CandidateFixture.Create();
        linked.ReplaceNotesWithSymlink();
        (int linkedCode, string linkedOutput) = linked.Verify(linked.B);

        using var unrelated = CandidateFixture.Create();
        unrelated.DirtyUnrelatedFile();
        (int unrelatedCode, _) = unrelated.Verify(unrelated.B);

        await Assert.That(policyCode).IsEqualTo(Program.ToolchainRejected);
        await Assert.That(policyOutput).IsEqualTo("verify_candidate_failed: candidate_policy_digest_mismatch\n");
        await Assert.That(footerCode).IsEqualTo(Program.ToolchainRejected);
        await Assert.That(footerOutput).IsEqualTo("verify_candidate_failed: candidate_terminal_commit_not_release_metadata_skip\n");
        await Assert.That(summaryCode).IsEqualTo(Program.ToolchainRejected);
        await Assert.That(summaryOutput).IsEqualTo("verify_candidate_failed: candidate_generated_artifacts_dirty\n");
        await Assert.That(contextCode).IsEqualTo(Program.ToolchainRejected);
        await Assert.That(contextOutput).IsEqualTo("verify_candidate_failed: candidate_generated_artifacts_dirty\n");
        await Assert.That(missingCode).IsEqualTo(Program.ToolchainRejected);
        await Assert.That(missingOutput).IsEqualTo("verify_candidate_failed: candidate_input_invalid\n");
        await Assert.That(linkedCode).IsEqualTo(Program.ToolchainRejected);
        await Assert.That(linkedOutput).IsEqualTo("verify_candidate_failed: candidate_input_invalid\n");
        await Assert.That(unrelatedCode).IsEqualTo(Program.Success);
        await Assert.That(Directory.EnumerateFiles(missing.ReleaseDirectory, "*.tmp").Any()).IsFalse();
        await Assert.That(Directory.EnumerateFiles(linked.ReleaseDirectory, "*.tmp").Any()).IsFalse();
    }

    private static string Sha256(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    private sealed class CandidateFixture : IDisposable
    {
        private const string ReleasePolicyYaml = "schemaVersion: 1\nmaximumCommitMessageBytes: 8192\nreleaseVisibleTypes:\n  - feat\n  - fix\n  - perf\n  - revert\n  - docs\ninternalTypes:\n  - test\n  - refactor\n  - style\n  - build\n  - ci\n  - chore\nrequiredBreakingSignals:\n  bang: true\n  footer: BREAKING CHANGE\nskipTrailer: Changelog\nskipValue: skip\nskipReasonTrailer: Changelog-Reason\n";
        private readonly string bundleRoot;
        private readonly string authorityRoot;
        private readonly string privateKeyPath;
        private readonly string receiptPath;
        private readonly string signaturePath;
        private readonly string allowedSignersPath;
        private readonly string configPath;
        private readonly string executablePath;
        internal PackageEvidenceFixture PackageEvidence { get; private set; }

        private CandidateFixture(string objectFormat, bool groupCorrection = false)
        {
            Root = Path.Combine(Path.GetTempPath(), $"islamu-candidate-{Guid.NewGuid():N}");
            RepositoryPath = Path.Combine(Root, "repo");
            bundleRoot = Path.Combine(Root, "bundle");
            authorityRoot = Path.Combine(Root, "authority");
            privateKeyPath = Path.Combine(authorityRoot, "promotion-key");
            receiptPath = Path.Combine(authorityRoot, "promotion-receipt.v1.json");
            signaturePath = receiptPath + ".sig";
            allowedSignersPath = Path.Combine(authorityRoot, "allowed-promoters");
            configPath = Path.Combine(bundleRoot, "config", "cliff.toml");
            executablePath = Path.Combine(bundleRoot, "git-cliff");
            ReleaseDirectory = Path.Combine(RepositoryPath, "docs", "internal", "releases", "1.1.0");
            ContextPath = Path.Combine(ReleaseDirectory, "release-context.v1.json");
            NotesPath = Path.Combine(ReleaseDirectory, "release-notes.md");
            CandidateManifestPath = Path.Combine(ReleaseDirectory, "release-candidate.v1.json");
            ObjectFormat = objectFormat == "sha256" ? "sha256" : "sha1";
            Directory.CreateDirectory(RepositoryPath);
            Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
            Directory.CreateDirectory(authorityRoot);
            Git("init", $"--object-format={objectFormat}", "--initial-branch=main");
            PackageEvidenceFixture.WriteSources(RepositoryPath);
            PackageEvidenceFixture.CopyTools(bundleRoot);
            CreatePromotionAuthority();
            if (groupCorrection)
            {
                WriteFragment();
                File.AppendAllText(Path.Join(RepositoryPath, "docs/internal/releases/changes/CHG-2026-0001.yaml"), "Group: original-group\n");
            }
            string previous = Commit("fix(definitions): preserve published event notes");
            if (groupCorrection)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(CorrectionPath)!);
                File.WriteAllText(CorrectionPath,
                    "Schema-Version: fragment-group-correction.v1\nChange-Id: CHG-2026-0001\n" +
                    $"Fragment-Sha256: {Sha256(System.Text.Encoding.UTF8.GetBytes(NormalizedFragment))}\nGroup: corrected-group\n");
                previous = Commit("chore(release): correct fixture group\n\nChangelog: skip\nChangelog-Reason: Correct fixture grouping.");
            }
            Git("-c", "user.name=Release Test", "-c", "user.email=release@example.invalid", "tag", "-a", "v1.0.0", previous, "-m", "v1.0.0");
            A = Commit("feat(self-hosting): let attendees correct self-hosting details\n\nChange-Id: CHG-2026-0001");
            WriteBundle();
            Prepare(groupCorrection);
            Git("add", "docs/internal/releases/1.1.0");
            B = Commit("docs(release): prepare 1.1.0\n\nChangelog: skip\nChangelog-Reason: release metadata commit");
            PackageEvidence = new PackageEvidenceFixture(Path.Combine(Root, "package-evidence"), RepositoryPath);
        }

        public string Root { get; }
        public string RepositoryPath { get; }
        public string ReleaseDirectory { get; }
        public string ContextPath { get; }
        public string NotesPath { get; }
        public string CandidateManifestPath { get; }
        public string A { get; }
        public string B { get; }
        public string ObjectFormat { get; }
        public string CorrectionPath => Path.Join(RepositoryPath, "docs/internal/releases/group-corrections/CHG-2026-0001.yaml");
        public string NormalizedFragment => File.ReadAllText(Path.Join(RepositoryPath, "docs/internal/releases/changes/CHG-2026-0001.yaml")).TrimEnd() + "\n";

        public static CandidateFixture Create() => new("sha1");
        public static CandidateFixture CreateWithGroupCorrection() => new("sha1", groupCorrection: true);
        public void StageCorrection() => Git("add", "docs/internal/releases/group-corrections");
        public void CommitCorrectionSubstitution()
        {
            File.WriteAllText(CorrectionPath, File.ReadAllText(CorrectionPath).Replace("corrected-group", "substituted-group", StringComparison.Ordinal));
            Commit("chore(release): substitute fixture correction\n\nChangelog: skip\nChangelog-Reason: Test exact tree binding.");
        }
        public static CandidateFixture? CreateSha256OrNull()
        {
            try { return new CandidateFixture("sha256"); }
            catch (InvalidOperationException) { return null; }
        }

        public void DriftPolicy() => File.AppendAllText(Path.Combine(RepositoryPath, "eng", "release", "policy", "release-policy.yaml"), "# drift\n");
        public void DriftSummary() => File.AppendAllText(Path.Combine(ReleaseDirectory, "summary.md"), "drift\n");
        public void DriftContext() => File.AppendAllText(ContextPath, " ");
        public void DeleteNotes() => File.Delete(NotesPath);
        public void DirtyUnrelatedFile() => File.AppendAllText(Path.Combine(RepositoryPath, "file.txt"), "unrelated dirty worktree state\n");
        internal void CommitUnrelatedMetadata()
        {
            File.WriteAllText(Path.Combine(RepositoryPath, "docs/unrelated.md"), "detached metadata\n");
            Commit("docs(release): update unrelated metadata\n\nChangelog: skip\nChangelog-Reason: Fixture metadata only.");
        }
        internal void CommitDifferentSourceAndRecapture()
        {
            Commit("chore(release): replace fixture source\n\nChangelog: skip\nChangelog-Reason: Test exact candidate bytes.");
            PackageEvidence = new PackageEvidenceFixture(Path.Combine(Root, "replacement-evidence"), RepositoryPath);
        }
        internal void StageTestSource() => Git("add", PackageEvidenceFixture.TestSource);
        internal void RecaptureConflictingTestInput() =>
            PackageEvidence = new PackageEvidenceFixture(Path.Combine(Root, "conflicting-evidence"), RepositoryPath, conflictingTestInput: true);
        internal string ChangeCandidateNamespace(string change)
        {
            string unused = Path.Combine(RepositoryPath, PackageEvidenceFixture.UnconsumedSource);
            if (change == "remove") File.Delete(unused);
            else if (change == "rename") File.Move(unused, unused + ".renamed.cs");
            else
            {
                string path = change switch
                {
                    "product-add" => "src/Configurator.Core/NewMember.cs",
                    "test-add" => "tests/NewAssertions.cs",
                    "resource-add" => "src/Configurator.Core/NewResource.resx",
                    "import-add" => "Directory.Build.targets",
                    _ => throw new ArgumentException("Unknown fixture change.", nameof(change))
                };
                File.WriteAllText(Path.Combine(RepositoryPath, path), "// Newly eligible fixture member.\n");
            }
            Git("add", "--all");
            return CandidateFromTree(Git("write-tree").Trim());
        }
        internal string ChangeCandidateMode(string relative, string mode)
        {
            string path = Path.Combine(RepositoryPath, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            if (!File.Exists(path)) File.WriteAllText(path, "inert-link-target");
            Git("add", "--all");
            string oid = mode == "160000" ? A : Git("hash-object", "-w", path).Trim();
            Git("update-index", "--add", "--cacheinfo", mode + "," + oid + "," + relative);
            return CandidateFromTree(Git("write-tree").Trim());
        }
        internal void AddUnrelatedEligibleWorkingFile(bool committed)
        {
            File.WriteAllText(Path.Combine(RepositoryPath, "src/Configurator.Core/LaterMember.cs"), "// Later work.\n");
            if (committed) Commit("chore(release): retain later work\n\nChangelog: skip\nChangelog-Reason: Test exact object selection.");
        }
        internal string CandidateWithInvalidUtf8Name()
        {
            string oid = Git("rev-parse", B + ":file.txt").Trim();
            byte[] entries = [.. Encoding.UTF8.GetBytes(Git("ls-tree", "-z", B)),
                .. Encoding.ASCII.GetBytes("100644 blob " + oid + "\tinvalid-"), 0xff, 0];
            return CandidateFromTree(GitWithInput(entries, "mktree", "-z").Trim());
        }
        private string CandidateFromTree(string tree) =>
            Git("-c", "user.name=Release Test", "-c", "user.email=release@example.invalid",
                "commit-tree", tree, "-p", A, "-m",
                "docs(release): prepare 1.1.0\n\nChangelog: skip\nChangelog-Reason: release metadata commit").Trim();
        private string GitWithInput(byte[] input, params string[] arguments)
        {
            var start = new ProcessStartInfo("/usr/bin/git") { WorkingDirectory = RepositoryPath, UseShellExecute = false,
                RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
                StandardInputEncoding = new UTF8Encoding(false, true) };
            start.Environment["GIT_CONFIG_NOSYSTEM"] = "1";
            start.Environment["GIT_CONFIG_GLOBAL"] = "/dev/null";
            start.ArgumentList.Add("-c");
            start.ArgumentList.Add("core.hooksPath=/dev/null");
            foreach (string argument in arguments) start.ArgumentList.Add(argument);
            using Process process = Process.Start(start)!;
            Task<string> stdout = process.StandardOutput.ReadToEndAsync();
            Task<string> stderr = process.StandardError.ReadToEndAsync();
            process.StandardInput.BaseStream.Write(input);
            process.StandardInput.Close();
            try { Task.WhenAll(process.WaitForExitAsync(), stdout, stderr).WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult(); }
            catch (TimeoutException) { process.Kill(entireProcessTree: true); throw; }
            if (process.ExitCode != 0) throw new InvalidOperationException("fixture_git_input_failed");
            return stdout.Result;
        }
        internal void HardlinkSource(string source) => RunProcess("/usr/bin/ln", null, source, source + ".alias");
        internal void RemoveVerifierAssetAndReadmit()
        {
            File.Delete(Path.Combine(bundleRoot, "bin/package-verification/support/Configurator.SupportQualification.dll"));
            RewriteManifestAndReceipt();
        }

        public void ReplaceNotesWithSymlink()
        {
            File.Delete(NotesPath);
            File.CreateSymbolicLink(NotesPath, Path.Combine(ReleaseDirectory, "summary.md"));
        }

        public string ReplaceTerminalFooter()
        {
            Git("-c", "user.name=Release Test", "-c", "user.email=release@example.invalid", "commit", "--amend", "-m", "docs(release): prepare 1.1.0\n\nChangelog: skip\nChangelog-Reason: altered");
            string oid = Git("rev-parse", "HEAD").Trim();
            Git("branch", "-f", "v1.1", oid);
            return oid;
        }

        public (int ExitCode, string Output) Verify(string candidateOid, bool includePackageEvidence = true, string[]? packageArguments = null)
        {
            var variables = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["CONFIGURATOR_RELEASE_TRUSTED_BUNDLE"] = bundleRoot,
                ["CONFIGURATOR_RELEASE_PROMOTION_RECEIPT"] = receiptPath,
                ["CONFIGURATOR_RELEASE_PROMOTION_SIGNATURE"] = signaturePath,
                ["CONFIGURATOR_RELEASE_PROMOTION_PRINCIPAL"] = "fixture-tooling-promoter",
                ["CONFIGURATOR_RELEASE_MANIFEST_SHA256"] = Digest(Path.Combine(bundleRoot, "trusted-bundle.manifest.json")),
                ["CONFIGURATOR_RELEASE_BUNDLE_ID"] = "islamu-release-engineering",
                ["CONFIGURATOR_RELEASE_BUNDLE_VERSION"] = "1.0.0",
                ["CONFIGURATOR_RELEASE_POLICY_VERSION"] = "policy-v1",
                ["CONFIGURATOR_RELEASE_CONFIG_VERSION"] = "config-v1",
                ["CONFIGURATOR_RELEASE_TRUST_VERSION"] = "trust-v1",
            };
            Dictionary<string, string?> originals = variables.Keys.ToDictionary(name => name, Environment.GetEnvironmentVariable, StringComparer.Ordinal);
            try
            {
                foreach ((string name, string value) in variables) Environment.SetEnvironmentVariable(name, value);
                using RuntimePromotionTrustRootScope trustRoot = RuntimePromotionTrustRootScope.Use(allowedSignersPath);
                using var output = new StringWriter();
                int exitCode = CandidateCommand.Run(["verify-candidate", "docs/internal/releases/1.1.0", candidateOid,
                    .. includePackageEvidence ? packageArguments ?? PackageEvidence.Arguments : []],
                    output, RepositoryPath, "linux-x64", TimeSpan.FromSeconds(2));
                return (exitCode, output.ToString());
            }
            finally
            {
                foreach ((string name, string? value) in originals) Environment.SetEnvironmentVariable(name, value);
            }
        }

        public void Dispose() => Directory.Delete(Root, recursive: true);

        private void Prepare(bool groupCorrection)
        {
            Directory.CreateDirectory(ReleaseDirectory);
            Directory.CreateDirectory(Path.Combine(RepositoryPath, "docs", "internal", "releases", "changes"));
            Directory.CreateDirectory(Path.Combine(RepositoryPath, "eng", "release", "policy"));
            string previous = Git("rev-parse", "refs/tags/v1.0.0^{commit}").Trim();
            File.WriteAllText(Path.Combine(ReleaseDirectory, "release.yaml"),
                $"Version: 1.1.0\nLine: v1.1\nRelease-Date: 2026-08-14\nBase-Stable-Tag: v1.0.0\nPrevious-Published-Tag: v1.0.0\nRelease-Range:\n  Base-Ref: v1.0.0\n  Base-Oid: {previous}\n  Previous-Ref: v1.0.0\n  Previous-Oid: {previous}\nCompatibility:\n  - v1\nImpact-Dispositions:\n  breaking: not-applicable\n  security: not-applicable\n  migration: not-applicable\n  configuration: not-applicable\n  openapi: not-applicable\n  operator: documented\n");
            File.WriteAllText(Path.Combine(ReleaseDirectory, "summary.md"), "Attendees can now correct self-hosting details.\n");
            if (!groupCorrection) WriteFragment();
            File.WriteAllText(Path.Combine(RepositoryPath, "eng", "release", "policy", "release-policy.yaml"),
                ReleasePolicyYaml);
            File.WriteAllText(Path.Combine(RepositoryPath, "eng", "release", "policy", "scope-registry.yaml"),
                "schemaVersion: 1\npublicScopes:\n  - definitions\n  - self-hosting\nengineeringScopes:\n  - release\n");
            RewriteManifestAndReceipt();
            (int exitCode, string output) = RunPrepareCommand();
            if (exitCode != Program.Success) throw new InvalidOperationException("prepare_fixture_failed:" + output);
        }

        private void WriteFragment()
        {
            string path = Path.Join(RepositoryPath, "docs/internal/releases/changes/CHG-2026-0001.yaml");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path,
                "Change-Id: CHG-2026-0001\nTitle: Registration worker restart\nType: feat\nScope: self-hosting\nSummary: Attendees can now correct self-hosting details.\nSupersedes: []\nImpacts:\n  Breaking:\n    Reference: docs/internal/releases/README.md\n    Disposition: not-applicable\n  Security:\n    Reference: docs/SECURITY_OVERVIEW.md\n    Disposition: not-applicable\n  Migration:\n    Reference: docs/RELEASE_RUNBOOK.md\n    Disposition: not-applicable\n  Configuration:\n    Reference: docs/CONFIGURATION.md\n    Disposition: not-applicable\n  OpenAPI:\n    Reference: docs/API_CHANGELOG.md\n    Disposition: not-applicable\n  Operator:\n    Reference: docs/RELEASE_RUNBOOK.md\n    Disposition: documented\n    Detail: Restart self-hosting workers after deployment.\n");
        }

        private (int ExitCode, string Output) RunPrepareCommand()
        {
            var variables = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["CONFIGURATOR_RELEASE_TRUSTED_BUNDLE"] = bundleRoot,
                ["CONFIGURATOR_RELEASE_PROMOTION_RECEIPT"] = receiptPath,
                ["CONFIGURATOR_RELEASE_PROMOTION_SIGNATURE"] = signaturePath,
                ["CONFIGURATOR_RELEASE_PROMOTION_PRINCIPAL"] = "fixture-tooling-promoter",
                ["CONFIGURATOR_RELEASE_MANIFEST_SHA256"] = Digest(Path.Combine(bundleRoot, "trusted-bundle.manifest.json")),
                ["CONFIGURATOR_RELEASE_BUNDLE_ID"] = "islamu-release-engineering",
                ["CONFIGURATOR_RELEASE_BUNDLE_VERSION"] = "1.0.0",
                ["CONFIGURATOR_RELEASE_POLICY_VERSION"] = "policy-v1",
                ["CONFIGURATOR_RELEASE_CONFIG_VERSION"] = "config-v1",
                ["CONFIGURATOR_RELEASE_TRUST_VERSION"] = "trust-v1",
            };
            Dictionary<string, string?> originals = variables.Keys.ToDictionary(name => name, Environment.GetEnvironmentVariable, StringComparer.Ordinal);
            try
            {
                foreach ((string name, string value) in variables) Environment.SetEnvironmentVariable(name, value);
                using RuntimePromotionTrustRootScope trustRoot = RuntimePromotionTrustRootScope.Use(allowedSignersPath);
                using var output = new StringWriter();
                int exitCode = PrepareCommand.Run(["prepare", "docs/internal/releases/1.1.0"], output, RepositoryPath, "linux-x64", TimeSpan.FromSeconds(2));
                return (exitCode, output.ToString());
            }
            finally
            {
                foreach ((string name, string? value) in originals) Environment.SetEnvironmentVariable(name, value);
            }
        }

        private string Commit(string message)
        {
            File.AppendAllText(Path.Combine(RepositoryPath, "file.txt"), message + Environment.NewLine);
            Git("add", ".");
            Git("-c", "user.name=Release Test", "-c", "user.email=release@example.invalid", "commit", "-m", message);
            return Git("rev-parse", "HEAD").Trim();
        }

        private void WriteBundle()
        {
            File.WriteAllText(configPath,
                "[changelog]\nbody = \"\"\"\n# Release {{ version }}\n{% for commit in commits %}\n- {{ commit.group }}: {{ commit.message }} ({{ commit.id }})\n{% endfor %}\n\"\"\"\ntrim = true\nrender_always = true\n");
            File.WriteAllText(executablePath,
                "#!/bin/sh\nif [ \"$1\" = \"--version\" ]; then printf 'git-cliff 2.13.1\\n'; exit 0; fi\nprintf '# Release 1.1.0\\n\\n- self-hosting: let attendees correct self-hosting details (" + APlaceholder + ")\\n'\n");
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(executablePath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }
            File.WriteAllText(Path.Combine(bundleRoot, "toolchain.lock.json"), $$"""
                {
                  "schemaVersion": 1,
                  "tool": "git-cliff",
                  "version": "2.13.1",
                  "platforms": [{ "platform": "linux-x64", "executable": "git-cliff", "executableSha256": "{{Digest(executablePath)}}" }]
                }
                """);
            RewriteManifestAndReceipt();
        }

        private string APlaceholder => A.Length == 64 ? A[..12] : "cccccccccccc";

        private void RewriteManifestAndReceipt()
        {
            if (!File.Exists(executablePath)) return;
            EnsureFile("bin/Configurator.ReleaseEngineering.dll", "release-engine-binary");
            EnsureFile("policy/context-version.txt", "context-v1\n");
            EnsureFile("policy/schema-version.txt", "schema-v1\n");
            EnsureFile("policy/release-policy.yaml", ReleasePolicyYaml);
            EnsureFile("trust/allowed-signers", "# production signers absent\n");
            EnsureFile("trust/release-signing-policy.yaml", "status: inactive-fixture-only\n");
            string manifestJson = JsonSerializer.Serialize(new
            {
                schemaVersion = "trusted-bundle.v1",
                bundleId = "islamu-release-engineering",
                bundleVersion = "1.0.0",
                policyVersion = "policy-v1",
                configVersion = "config-v1",
                trustVersion = "trust-v1",
                policyDigest = Digest(Path.Combine(bundleRoot, "policy", "release-policy.yaml")),
                configDigest = Digest(configPath),
                trustDigest = Digest(Path.Combine(bundleRoot, "trust", "release-signing-policy.yaml")),
                files = Directory.EnumerateFiles(bundleRoot, "*", SearchOption.AllDirectories)
                    .Where(path => Path.GetFileName(path) != "trusted-bundle.manifest.json")
                    .Select(path => new { path = Path.GetRelativePath(bundleRoot, path).Replace(Path.DirectorySeparatorChar, '/'), sha256 = Digest(path) })
                    .OrderBy(item => item.path, StringComparer.Ordinal)
                    .ToArray(),
            });
            File.WriteAllBytes(Path.Combine(bundleRoot, "trusted-bundle.manifest.json"), ReleaseArtifactPolicy.NormalizeJson(manifestJson).Bytes!);
            ResignReceipt();
        }

        private string EnsureFile(string path, string content)
        {
            string fullPath = Path.Combine(bundleRoot, path.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            if (!File.Exists(fullPath)) File.WriteAllText(fullPath, content);
            return fullPath;
        }

        private void CreatePromotionAuthority()
        {
            RunProcess("/usr/bin/ssh-keygen", null, "-q", "-t", "ed25519", "-N", string.Empty, "-C", "synthetic-promotion-fixture", "-f", privateKeyPath);
            string publicKey = string.Join(' ', File.ReadAllText(privateKeyPath + ".pub").Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2));
            File.WriteAllText(allowedSignersPath, $"fixture-tooling-promoter namespaces=\"islamu-release-promotion\" {publicKey}\n");
        }

        private void ResignReceipt()
        {
            string manifestPath = Path.Combine(bundleRoot, "trusted-bundle.manifest.json");
            using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(manifestPath));
            JsonElement root = document.RootElement;
            string receiptJson = JsonSerializer.Serialize(new
            {
                schemaVersion = "trusted-bundle-promotion.v1",
                receiptId = "promotion-fixture-0001",
                bundleManifestSha256 = Digest(manifestPath),
                bundleId = root.GetProperty("bundleId").GetString(),
                bundleVersion = root.GetProperty("bundleVersion").GetString(),
                policyVersion = root.GetProperty("policyVersion").GetString(),
                configVersion = root.GetProperty("configVersion").GetString(),
                trustVersion = root.GetProperty("trustVersion").GetString(),
                policyDigest = root.GetProperty("policyDigest").GetString(),
                configDigest = root.GetProperty("configDigest").GetString(),
                trustDigest = root.GetProperty("trustDigest").GetString(),
                promotionPrincipal = "fixture-tooling-promoter",
            });
            File.WriteAllBytes(receiptPath, ReleaseArtifactPolicy.NormalizeJson(receiptJson).Bytes!);
            if (File.Exists(signaturePath)) File.Delete(signaturePath);
            RunProcess("/usr/bin/ssh-keygen", null, "-Y", "sign", "-f", privateKeyPath, "-n", "islamu-release-promotion", receiptPath);
        }

        private string Git(params string[] args) => RunGit(RepositoryPath, args);
        private static string RunGit(string workingDirectory, params string[] args) => RunProcess("git", workingDirectory, args);
        private static string RunProcess(string executable, string? workingDirectory, params string[] args)
        {
            using var process = new Process { StartInfo = new ProcessStartInfo(executable) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory } };
            string nullDevice = OperatingSystem.IsWindows() ? "NUL" : "/dev/null";
            process.StartInfo.Environment["GIT_CONFIG_NOSYSTEM"] = "1";
            process.StartInfo.Environment["GIT_CONFIG_GLOBAL"] = nullDevice;
            if (executable == "git")
            {
                process.StartInfo.ArgumentList.Add("-c");
                process.StartInfo.ArgumentList.Add($"core.hooksPath={nullDevice}");
            }
            foreach (string arg in args) process.StartInfo.ArgumentList.Add(arg);
            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0) throw new InvalidOperationException($"{executable}_failed");
            return output;
        }

        private static string Digest(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
    }
}
