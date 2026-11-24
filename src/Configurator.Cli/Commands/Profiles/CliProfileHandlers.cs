using System.Security.Cryptography;
using Configurator.Artifacts;
using Configurator.Artifacts.Profiles;
using Configurator.Core;
using Configurator.Core.Definitions;
using Configurator.Core.Profiles;

namespace Configurator.Cli;

internal static class CliProfileHandlers
{
    internal static async Task<CliCommandResult> ExecuteAsync(CliCommand command, CliInvocation invocation,
        CliOutcomeLedger ledger, Func<string, IProfileVaultStore> stores, Func<IOsKeyringProvider> keyrings,
        CancellationToken token)
    {
        try
        {
            if (command.Operation == "save")
                return await SaveAsync(command, invocation, ledger, stores, keyrings, token);
            if (command.Operation == "list")
            {
                IReadOnlyList<ProfileRevisionDescriptor> revisions = await stores(command.Store!).ListAsync(token);
                ProfileRevisionDescriptor[] ordered = revisions.OrderBy(item => item.ProfileId)
                    .ThenBy(item => item.SnapshotId).ThenBy(item => item.Kind).ToArray();
                ProfileRevisionDescriptor[] page = ordered.Skip(command.ProfileListOffset).Take(command.ProfileListLimit).ToArray();
                CliCommandResult result = CliResults.Success() with
                {
                    Artifacts = page.Select(item => Receipt(item.ProfileId, item.SnapshotId, item.Kind, "input", "none")).ToArray(),
                    Diagnostics = page.Select(item => Info(item.Kind == ProfileFileKind.Encrypted
                        ? "profile-encrypted-discovery-unverified" : "profile-comparison-discovery-unverified",
                        $"/profiles/{item.ProfileId:N}/snapshots/{item.SnapshotId:N}")).ToArray()
                };
                return ordered.Length > command.ProfileListOffset + command.ProfileListLimit
                    ? result with { Diagnostics = [.. result.Diagnostics, Info("profile-list-more")] } : result;
            }
            IProfileVaultStore? store = command.Store is null ? null : stores(command.Store);
            ProfileRevisionDescriptor? revision = store is null ? null : await ResolveAsync(store, command, token);
            if (command.Operation == "recovery-check")
            {
                using CliInputBuffer credential = await CredentialAsync(command.PassphraseInput!, invocation, false, token);
                var reference = new ProfileRecoveryReference(revision!.ProfileId, revision.SnapshotId,
                    ArtifactDigest.Parse(command.RecoveryDigest!), command.DeviceRecipientId, portableRecoveryConfigured: true);
                ProfileRecoveryResult verified = await ProfileRecoveryVerifier.VerifyAsync(store!, revision,
                    reference, command.RecipientIndex, credential.Bytes, token);
                return verified.State == ProfileRecoveryState.VerifiedForSnapshot && verified.Failure == ProfileRecoveryFailure.None
                    ? Status("profile-recovery-verified-for-snapshot")
                    : CliResults.Failure(CliExitCode.Blocked, "profile-recovery-" + Token(verified.Failure));
            }
            if (command.Operation == "inspect" || command.Operation == "slots" && command.ProfileAction == "inspect")
            {
                if (command.PassphraseInput is not null || command.Device)
                    return CliResults.Failure(CliExitCode.Usage, "profile-credentials-inapplicable");
                using CliInputBuffer bytes = await ReadProfileAsync(command, invocation, store, revision, token);
                int recipients = ProfileContainerPolicy.Admit(bytes.Bytes);
                return Status("profile-container-unverified") with
                {
                    Diagnostics = [Info("profile-container-unverified"), .. Policy(),
                        .. Enumerable.Range(0, recipients).Select(index =>
                            Info("profile-recipient-unverified", "/recipients/" + index.ToString(System.Globalization.CultureInfo.InvariantCulture)))]
                };
            }
            Definition? target = command.Definition is null ? null
                : await CliIoOperations.DefinitionAsync(invocation, command.Definition, token);
            Definition? source = command.SourceDefinition is null ? null
                : await CliIoOperations.DefinitionAsync(invocation, command.SourceDefinition, token);
            if (command.ProfileMode == "compare-only")
            {
                using CliInputBuffer bytes = await ReadProfileAsync(command, invocation, store, revision, token);
                CompareBaselineSnapshot baseline = CompareBaselineCodec.ParseJson(bytes.Bytes);
                if (command.Baseline is not null)
                {
                    using CliInputBuffer before = await CliIoOperations.ReadAsync(invocation, command.Baseline, token);
                    return PlanResult(ProfileUpgradePlanner.Compare(CompareBaselineCodec.ParseJson(before.Bytes),
                        baseline, source, target!, ""));
                }
                return PlanResult(ProfileUpgradePlanner.Plan(baseline, target!, source));
            }
            using var codec = new NativeProfileContainerCodec();
            using UnlockedProfile profile = await OpenAsync(command, invocation, codec, store, revision, keyrings, token);
            if (command.Operation == "unlock")
                return Status("profile-unlocked-status-only") with { Diagnostics = [Info("profile-unlocked-status-only"), .. Policy()] };
            if (command.Operation == "slots")
            {
                using CliInputBuffer newCredential = await CredentialAsync(command.NewPassphraseInput!, invocation, true, token);
                using CliInputBuffer confirmation = await CredentialAsync(command.PassphraseConfirmationInput!, invocation, false, token);
                if (!CryptographicOperations.FixedTimeEquals(newCredential.Bytes.Span, confirmation.Bytes.Span))
                    return CliResults.Failure(CliExitCode.Data, "passphrase-confirmation-mismatch");
                using ProfileSealedSnapshot resealed = await ProfileRecipientLifecycle.ResealAsync(profile, Guid.NewGuid(),
                    codec, [newCredential.Bytes], cancellationToken: token);
                return await PublishSnapshotAsync(command, invocation, ledger, store!, resealed, token);
            }
            // A supplied baseline's source evidence belongs to that baseline, independently of this profile.
            if (command.Operation is "diff" or "upgrade-plan" && command.Baseline is not null)
            {
                using CliInputBuffer before = await CliIoOperations.ReadAsync(invocation, command.Baseline, token);
                return PlanResult(ProfileUpgradePlanner.Compare(CompareBaselineCodec.ParseJson(before.Bytes),
                    CompareBaselineCodec.Create(profile), source, target!, ""));
            }
            // Core owns embedded historical admission and its joint binding with the target.
            // Independently supplied evidence is optional, but must match when present.
            if (source is not null)
            {
                if (source.FamilyId != profile.FamilyId || source.Revision != profile.DefinitionRevision
                    || source.Digest != profile.DefinitionDigest)
                    return CliResults.Failure(CliExitCode.Blocked, "profile-source-definition-mismatch");
                _ = new DefinitionRegistry([source, target!]);
            }
            if (command.Operation is "diff" or "upgrade-plan")
                return PlanResult(ProfileUpgradePlanner.Plan(profile, target!));
            if (command.Operation == "adopt")
            {
                if (await invocation.Io.Output.CheckArtifactDestinationAsync(command.Output!, token) is not null)
                    return CliResults.Failure(CliExitCode.Blocked, "profile-output-unavailable", "/output");
                AnswerSnapshot replacements = command.Answers is null
                    ? AnswerSnapshotCodec.ParseJson("""{"answerContractVersion":"0.0.1","values":{},"omitted":[]}"""u8.ToArray(), 0)
                    : await CliIoOperations.AnswersAsync(invocation, command.Answers, "0", token);
                using var input = new ProfileCompositionInput(profile, target!, replacements);
                ProfileArtifactResult composed = ProfileArtifactComposer.Compose(input, command.Artifact!, token);
                if (!composed.Succeeded)
                    return CliResults.Failure(CliExitCode.Validation, "profile-adoption-revalidation-required", "/input");
                using ProfileArtifact artifact = composed.Artifact!;
                var receipt = new CliMachineArtifact("definition-artifact", artifact.MediaType, null, "restricted",
                    CliResults.EmptyCoverage(), CliResults.Ready(), "output", "not-attempted")
                { Id = artifact.ArtifactId };
                CliCommandResult planned = CliResults.Success([receipt]) with { DefinitionDigest = target!.Digest.Value };
                ledger.Plan(invocation, command, planned);
                CliPublicationResult? publication = null;
                await artifact.WriteToNativeSaveAsync(async (bytes, cancellation) =>
                {
                    ledger.BeginPublication();
                    publication = await invocation.Io.Output.WriteArtifactAsync(command.Output!, ArtifactKind.Configuration,
                        bytes, invocation.Io.MaximumBytes, cancellation);
                    foreach (CliPublicationOutcome outcome in publication.Outcomes) ledger.Record(artifact.ArtifactId, outcome);
                }, token);
                return ledger.Apply(publication!, planned, blockedRejections: true);
            }
            return CliResults.Failure(CliExitCode.Usage, "operation-unknown");
        }
        catch (ProfileException exception)
        {
            return CliResults.Failure(CliExitCode.Data, "profile-" + Token(exception.Code), "/input");
        }
        catch (ProfileRecoveryException exception)
        {
            return CliResults.Failure(CliExitCode.Blocked, "profile-recovery-" + Token(exception.Failure));
        }
        catch (ProfileVaultStoreException)
        {
            return CliResults.Failure(CliExitCode.Blocked, "profile-store-unavailable");
        }
        catch (OsKeyringException exception)
        {
            return CliResults.Failure(CliExitCode.Blocked, "profile-keyring-" + Token(exception.Status));
        }
    }

    private static async Task<CliCommandResult> SaveAsync(CliCommand command, CliInvocation invocation,
        CliOutcomeLedger ledger, Func<string, IProfileVaultStore> stores, Func<IOsKeyringProvider> keyrings,
        CancellationToken token)
    {
        if (command.ProfileMode == "unencrypted-target")
        {
            // The ordinary target mode uses ordinary answers, never a blind retained profile.
            return await CliArtifactHandlers.ExecuteAsync(command with
            { Family = "artifact", Operation = "create" }, invocation, ledger, token);
        }
        Definition definition = await CliIoOperations.DefinitionAsync(invocation, command.Definition!, token);
        AnswerSnapshot answers = await CliIoOperations.AnswersAsync(invocation, command.Input!, "0", token);
        Guid profileId = Guid.NewGuid();
        Guid snapshotId = Guid.NewGuid();
        IProfileVaultStore store = stores(command.Store!);
        if (command.ProfileMode == "compare-only")
        {
            byte[] baseline = CompareBaselineCodec.Serialize(CompareBaselineCodec.Create(definition, answers,
                profileId, snapshotId, command.EnvironmentContext ?? ""));
            try
            {
                return await PublishAsync(command, invocation, ledger, store, profileId, snapshotId,
                    ProfileFileKind.Comparison, baseline, null, token);
            }
            finally { CryptographicOperations.ZeroMemory(baseline); }
        }
        ProfileProtectionMode mode = command.ProfileMode == "import-upgrade-only"
            ? ProfileProtectionMode.ImportUpgradeOnly : ProfileProtectionMode.InspectableEncrypted;
        byte[] payload = ProfilePayloadCodec.Serialize(definition, answers, mode, profileId, snapshotId,
            "local", command.EnvironmentContext!);
        try
        {
            using UnlockedProfile profile = ProfilePayloadCodec.Admit(payload);
            using CliInputBuffer? credential = command.PassphraseInput is null ? null
                : await CredentialAsync(command.PassphraseInput, invocation, true, token);
            using CliInputBuffer? confirmation = command.PassphraseConfirmationInput is null ? null
                : await CredentialAsync(command.PassphraseConfirmationInput, invocation, false, token);
            if (credential is not null && !CryptographicOperations.FixedTimeEquals(credential.Bytes.Span, confirmation!.Bytes.Span))
                return CliResults.Failure(CliExitCode.Data, "passphrase-confirmation-mismatch");
            using OsKeyringResult? device = command.Device
                ? await keyrings().CreateAsync(OsKeyringPurpose.NativeRecipient, token) : null;
            if (device is not null && (device.Status != OsKeyringStatus.Success || device.Lease is null))
                return CliResults.Failure(CliExitCode.Blocked, "profile-keyring-" + Token(device.Status));
            using var codec = new NativeProfileContainerCodec(device?.Lease);
            using ProfileSealedSnapshot sealedProfile = await ProfileRecipientLifecycle.SealAsync(profile, codec,
                credential is null ? [] : [credential.Bytes],
                command.AcknowledgeDeviceOnly ? profileId : null, token);
            return await PublishSnapshotAsync(command, invocation, ledger, store, sealedProfile, token);
        }
        finally { CryptographicOperations.ZeroMemory(payload); }
    }

    private static async ValueTask<CliInputBuffer> CredentialAsync(string path, CliInvocation invocation,
        bool creating, CancellationToken token)
    {
        int maximum = Math.Min(invocation.Io.MaximumBytes, ProfileContainerPolicy.MaximumPassphraseUtf8Bytes);
        CliInputBuffer buffer = await invocation.Io.Input.ReadAsync(path, maximum, token);
        try
        {
            token.ThrowIfCancellationRequested();
            if (buffer.Bytes.Length > maximum) throw new ProfileException(ProfileFailureCode.InvalidPassphrase);
            ProfileContainerPolicy.ValidatePassphrase(buffer.Bytes.Span, creating);
            return buffer;
        }
        catch { buffer.Dispose(); throw; }
    }

    private static async ValueTask<ProfileRevisionDescriptor> ResolveAsync(IProfileVaultStore store,
        CliCommand command, CancellationToken token)
    {
        IReadOnlyList<ProfileRevisionDescriptor> revisions = await store.ListAsync(token);
        ProfileFileKind kind = command.ProfileMode == "compare-only" ? ProfileFileKind.Comparison : ProfileFileKind.Encrypted;
        return revisions.SingleOrDefault(item => item.ProfileId == command.ProfileId && item.SnapshotId == command.SnapshotId
            && item.Kind == kind) ?? throw new ProfileVaultStoreException(ProtectedArtifactStatus.InvalidRequest);
    }

    private static async ValueTask<CliInputBuffer> ReadProfileAsync(CliCommand command, CliInvocation invocation,
        IProfileVaultStore? store, ProfileRevisionDescriptor? revision, CancellationToken token)
    {
        if (store is null) return await CliIoOperations.ReadAsync(invocation, command.Input!, token);
        byte[] bytes = await store.ReadAsync(revision!, token);
        return new CliInputBuffer(bytes);
    }

    private static async ValueTask<UnlockedProfile> OpenAsync(CliCommand command, CliInvocation invocation,
        NativeProfileContainerCodec codec, IProfileVaultStore? store, ProfileRevisionDescriptor? revision,
        Func<IOsKeyringProvider> keyrings, CancellationToken token)
    {
        if (command.Device)
            return await codec.OpenNativeAsync(store!, revision!, command.RecipientIndex, keyrings(), token);
        using CliInputBuffer credential = await CredentialAsync(command.PassphraseInput!, invocation, false, token);
        if (store is not null)
            return await codec.OpenStoredAsync(store, revision!, command.RecipientIndex, credential.Bytes, token);
        using CliInputBuffer bytes = await ReadProfileAsync(command, invocation, null, null, token);
        return await codec.OpenAsync(bytes.Bytes, command.RecipientIndex, credential.Bytes, token);
    }

    private static async Task<CliCommandResult> PublishSnapshotAsync(CliCommand command, CliInvocation invocation,
        CliOutcomeLedger ledger, IProfileVaultStore store, ProfileSealedSnapshot snapshot, CancellationToken token)
    {
        byte[] bytes = snapshot.CopyContainer();
        try
        {
            return await PublishAsync(command, invocation, ledger, store, snapshot.Reference.ProfileId,
                snapshot.Reference.SnapshotId, ProfileFileKind.Encrypted, bytes, snapshot.Reference, token);
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    private static async Task<CliCommandResult> PublishAsync(CliCommand command, CliInvocation invocation,
        CliOutcomeLedger ledger, IProfileVaultStore store, Guid profileId, Guid snapshotId, ProfileFileKind kind,
        ReadOnlyMemory<byte> bytes, ProfileRecoveryReference? reference, CancellationToken token)
    {
        CliMachineArtifact receipt = Receipt(profileId, snapshotId, kind, "output", "not-attempted") with
        { Digest = ArtifactDigest.Compute(bytes.Span).Value };
        CliCommandResult planned = CliResults.Success([receipt]) with
        {
            Readiness = new("unassessed", [], []),
            Diagnostics = [Info("profile-snapshot", $"/profiles/{profileId:N}/snapshots/{snapshotId:N}"),
                Info("profile-ciphertext-digest", $"/digest/{receipt.Digest}"),
                .. reference is null ? new[] { Info("profile-comparison-not-restorable") }
                : [Info(reference.State == ProfileRecoveryState.DeviceBoundOnly
                    ? "profile-device-bound-only" : "profile-recovery-configured-unverified"), .. Policy(),
                    .. reference.DeviceRecipientId is { } deviceId
                        ? new[] { Info("profile-device-recipient", $"/deviceRecipient/{deviceId:N}") } : []]]
        };
        ledger.Plan(invocation, command, planned);
        ledger.BeginPublication();
        ProtectedArtifactBundleResult saved = await store.SaveAsync(profileId, snapshotId, kind, bytes, token);
        var publication = new CliPublicationResult(saved.Outcomes.Select(outcome => new CliPublicationOutcome(outcome.Result)).ToArray(),
            saved.CleanupFailed, saved.CompletionCode == ProtectedArtifactBundleCompletionCode.Cancelled);
        return ledger.Apply(publication, planned, blockedRejections: true);
    }

    private static CliCommandResult PlanResult(ProfileUpgradePlan plan) => CliResults.Success() with
    {
        Diagnostics = [Info("profile-revalidation-required"),
            Info(plan.SourceSchemaAvailable ? "profile-source-schema-available" : "profile-source-schema-unavailable"),
            .. plan.SchemaDeltas.Select(delta => Info("profile-schema-" + Token(delta.Kind), delta.SchemaPath)),
            .. plan.Fields.SelectMany(field => new[]
            {
                Info("profile-" + Token(field.Eligibility), field.FieldPath),
                Info("profile-comparison-" + Token(field.Comparison), field.FieldPath)
            })]
    };

    private static CliCommandResult Status(string code) => CliResults.Success() with { Diagnostics = [Info(code)] };
    private static CliMachineDiagnostic Info(string code, string path = "") => new(code, path, "info");
    private static CliMachineDiagnostic[] Policy() =>
        [Info("profile-a256gcm"), Info("profile-pbes2-hs512-a256kw"), Info("profile-pbkdf2-hmac-sha512-600000")];
    private static CliMachineArtifact Receipt(Guid profileId, Guid snapshotId, ProfileFileKind kind,
        string intent, string status) => new(kind == ProfileFileKind.Encrypted ? "encrypted-profile" : "comparison-profile",
            "application/json", null, "restricted", CliResults.EmptyCoverage(), CliResults.Ready(), intent, status)
        { Id = $"p{profileId:N}.s{snapshotId:N}.{(kind == ProfileFileKind.Encrypted ? "encrypted" : "comparison")}" };
    private static string Token<T>(T value) => string.Concat(value!.ToString()!.Select((character, index) =>
        char.IsUpper(character) && index > 0 ? "-" + char.ToLowerInvariant(character) : char.ToLowerInvariant(character).ToString()));
}
