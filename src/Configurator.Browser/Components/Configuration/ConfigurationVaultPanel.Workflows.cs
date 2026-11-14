namespace Configurator.Browser.Components.Configuration;

using System.Security.Cryptography;
using Configurator.Browser.Profiles;
using Configurator.Browser.Services;
using Configurator.Core;
using Configurator.Core.Definitions;
using Configurator.Core.Profiles;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;

public partial class ConfigurationVaultPanel
{
    private Task OpenAsync(ProfileWorkerAuthority captured) => RunAsync(captured, true, async token =>
    {
        if (_vaultFile is null || Form.BrowserProfile is not null) return;
        byte[] credential = Credential(_passphrase);
        byte[] container = [];
        try
        {
            container = await ReadFileAsync(_vaultFile, token);
            ProfileWorkerReply opened = await Worker.OpenAsync(container, _recipientIndex, credential,
                candidate =>
                {
                    if (!Current(captured) || candidate.FamilyId != Form.Definition.FamilyId ||
                        !ArtifactDigest.TryParse(candidate.DefinitionDigest, out ArtifactDigest digest)) return false;
                    Registry.AdmitHistoricalIdentity(candidate.FamilyId, candidate.DefinitionRevision, digest);
                    return true;
                }, token);
            if (opened.Status != "ok" || opened.PublicValues is not { } publicValues ||
                !Enum.TryParse(opened.Mode, out ProfileProtectionMode mode))
                throw new ProfileException(ProfileFailureCode.InvalidPayload);
            ProfileWorkerReply plan = await Worker.PlanAsync(opened.ProfileReference, Form.Definition, token);
            if (!Current(captured) || token.IsCancellationRequested) return;
            _adopting = true;
            try
            {
                if (!Form.AdoptBrowserProfile(captured.AnswerRevision, opened.ProfileReference, mode,
                    publicValues, opened.RetainedPaths.Select(DefinitionPath.Parse).ToArray(),
                    RetireWorker, DiscardWorkerOutput))
                    throw new ProfileException(ProfileFailureCode.ModeRestricted);
            }
            finally { _adopting = false; }
            _fields = plan.Fields;
            _vaultFile = null;
            ++_fileKey;
            _status = "ProfileOpened";
            _focusHeading = true;
        }
        catch
        {
            RetireWorker();
            throw;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(credential);
            CryptographicOperations.ZeroMemory(container);
        }
    });

    private Task RefreshPlanAsync(ProfileWorkerAuthority captured) => RunAsync(captured, true, async token =>
    {
        if (Form.BrowserProfile is not { } profile) return;
        ProfileWorkerReply plan = await Worker.PlanAsync(profile.Reference, Form.Definition, token);
        if (!Current(captured)) return;
        _fields = plan.Fields;
        _status = "ProfilePlanReady";
    });

    private Task ReplaceAsync(ProfileWorkerAuthority captured, string path) => RunAsync(captured, true, async token =>
    {
        if (_replacement != path || Form.BrowserProfile is not { } profile) return;
        try
        {
            bool replaced = await Form.ReplaceBrowserRetainedValueAsync(DefinitionPath.Parse(path), null, true,
                async (requested, cancellation) =>
                {
                    ProfileWorkerReply reply = await Worker.RetireAsync(profile.Reference, requested, cancellation);
                    if (reply.Status != "ok") throw new ProfileException(ProfileFailureCode.ModeRestricted);
                    if (Current(captured)) _adopting = true;
                    return reply.RetainedPaths.Select(DefinitionPath.Parse).ToArray();
                }, token);
            _replacement = null;
            _fields = [];
            _status = replaced ? "ProfileReplacementReady" : "ProfileRejected";
        }
        catch
        {
            Form.CloseBrowserProfile();
            RetireWorker();
            throw;
        }
        finally { _adopting = false; }
    });

    private Task InspectAsync(ProfileWorkerAuthority captured) => RunAsync(captured, true, async token =>
    {
        if (!_inspectConfirmed || Form.BrowserProfile is not
            { Mode: ProfileProtectionMode.InspectableEncrypted } profile) return;
        AnswerSnapshot disclosed = await Worker.InspectAsync(profile.Reference, token);
        if (!Current(captured) || token.IsCancellationRequested) return;
        _adopting = true;
        try
        {
            if (!Form.AdoptBrowserProfileDisclosure(profile.Reference, captured.AnswerRevision, disclosed))
                throw new ProfileException(ProfileFailureCode.ModeRestricted);
        }
        finally { _adopting = false; }
        _status = "ProfileInspected";
        _focusHeading = true;
    });

    private Task DownloadPrivateAsync(ProfileWorkerAuthority captured) => RunAsync(captured, true, async token =>
    {
        if (Form.BrowserProfile is not { } profile ||
            !Form.EligibleArtifacts.Any(artifact => artifact.Id == _artifactId) ||
            Form.HasInputErrors || Form.HasAnswerBudgetError) return;
        ProfileWorkerReply reply = await Worker.DownloadArtifactAsync(profile.Reference, Form.Definition,
            Form.Snapshot(), _artifactId, cancellationToken: token);
        if (!Current(captured)) return;
        _status = reply.Status == "ok" ? "ProfileDownloaded" : "ProfileNotReady";
        _code = reply.Status == "ok" ? null : "profile-composition-incomplete";
    });

    private Task SaveAsync(ProfileWorkerAuthority captured) => RunAsync(captured, true, async token =>
    {
        if (Form.HasInputErrors || Form.HasAnswerBudgetError ||
            Form.RequiresBrowserProfileComposition && Form.BrowserProfile is null) return;
        var materials = new List<byte[]>();
        var confirmations = new List<byte[]>();
        try
        {
            materials.Add(Credential(_newPassphrase));
            confirmations.Add(Credential(_confirmation));
            if (_addRecovery)
            {
                materials.Add(Credential(_backupPassphrase));
                confirmations.Add(Credential(_backupConfirmation));
                if (CryptographicOperations.FixedTimeEquals(materials[0], materials[1]))
                    throw new ProfileException(ProfileFailureCode.InvalidPassphrase);
            }
            ReadOnlyMemory<byte>[] recipients = materials.Select(bytes => (ReadOnlyMemory<byte>)bytes).ToArray();
            ReadOnlyMemory<byte>[] confirmed = confirmations.Select(bytes => (ReadOnlyMemory<byte>)bytes).ToArray();
            ProfileWorkerReply reply = Form.BrowserProfile is { } profile
                ? await Worker.SealAsync(profile.Reference, Form.Definition, Form.Snapshot(), Guid.NewGuid(),
                    recipients, confirmed, cancellationToken: token)
                : await Worker.CreateAsync(Form.Definition, Form.Snapshot(), _mode, Guid.NewGuid(), Guid.NewGuid(),
                    "browser-catalogue", "browser-workspace", recipients, confirmed, token);
            if (!Current(captured) || reply.Status != "ok" ||
                !ArtifactDigest.TryParse(reply.CiphertextDigest, out ArtifactDigest digest))
                throw new ProfileException(ProfileFailureCode.InvalidPayload);
            _recoveryReference = new(reply.ProfileId, reply.SnapshotId, digest, null, portableRecoveryConfigured: true);
            _recoveryIndex = _addRecovery ? 1 : 0;
            _status = "ProfileSavedUnverified";
        }
        finally
        {
            foreach (byte[] bytes in materials.Concat(confirmations)) CryptographicOperations.ZeroMemory(bytes);
        }
    });

    private Task RecoverAsync(ProfileWorkerAuthority captured) => RunAsync(captured, true, async token =>
    {
        if (_recoveryReference is not { } trusted || _recoveryFile is null) return;
        byte[] credential = Credential(_recoveryPassphrase);
        byte[] container = [];
        try
        {
            container = await ReadFileAsync(_recoveryFile, token);
            using var recovery = new ProfileWorkerClient(
                Javascript as IJSInProcessRuntime ?? throw new ProfileException(ProfileFailureCode.ModeRestricted),
                CaptureAuthority);
            _recoveryWorker = recovery;
            ProfileWorkerReply result = await recovery.VerifyRecoveryAsync(trusted, container,
                _recoveryIndex, credential, token);
            if (Current(captured)) _status = result.Status == "verified-for-snapshot"
                ? "ProfileRecoveryVerified" : "ProfileRejected";
        }
        finally
        {
            _recoveryWorker = null;
            CryptographicOperations.ZeroMemory(credential);
            CryptographicOperations.ZeroMemory(container);
            _recoveryFile = null;
            ++_fileKey;
        }
    });

    private Task CompareAsync(ProfileWorkerAuthority captured, InputFileChangeEventArgs args) =>
        RunAsync(captured, false, async token =>
        {
            byte[] bytes = await ReadFileAsync(args.File, token);
            try
            {
                CompareBaselineSnapshot baseline = CompareBaselineCodec.ParseJson(bytes);
                Registry.AdmitHistoricalIdentity(baseline.FamilyId, baseline.DefinitionRevision, baseline.DefinitionDigest);
                Definition? source = Registry.Current.Definitions.FirstOrDefault(definition =>
                    definition.FamilyId == baseline.FamilyId && definition.Revision == baseline.DefinitionRevision &&
                    definition.Digest == baseline.DefinitionDigest);
                CompareBaselineSnapshot current = CompareBaselineCodec.Create(Form.Definition, Form.Snapshot(),
                    baseline.ProfileId, Guid.NewGuid(), "browser-workspace");
                ProfileUpgradePlan comparison = ProfileUpgradePlanner.Compare(baseline, current, source,
                    Form.Definition, "browser-workspace");
                if (!Current(captured)) return;
                _fields = comparison.Fields.Select(field =>
                    new ProfileWorkerField(field.FieldPath, field.Eligibility.ToString(), field.Comparison.ToString())).ToArray();
                _status = "ProfileComparisonReady";
            }
            finally { CryptographicOperations.ZeroMemory(bytes); }
        });

    private Task SaveComparisonAsync(ProfileWorkerAuthority captured) => RunAsync(captured, false, async token =>
    {
        token.ThrowIfCancellationRequested();
        CompareBaselineSnapshot baseline = CompareBaselineCodec.Create(Form.Definition, Form.Snapshot(),
            Guid.NewGuid(), Guid.NewGuid(), "browser-workspace");
        byte[] bytes = CompareBaselineCodec.Serialize(baseline);
        try
        {
            await using var export = new BrowserArtifactExport(Javascript);
            _comparisonExport = export;
            export.SetOutput(bytes);
            bool downloaded = await export.DownloadAsync("configuration.cfgcompare.json", "application/json");
            if (Current(captured)) _status = downloaded ? "ProfileComparisonDownloaded" : "ProfileRejected";
        }
        finally
        {
            _comparisonExport = null;
            CryptographicOperations.ZeroMemory(bytes);
        }
    });
}
