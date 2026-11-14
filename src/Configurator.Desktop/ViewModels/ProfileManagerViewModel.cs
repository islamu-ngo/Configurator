namespace Configurator.Desktop.ViewModels;

using System.ComponentModel;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Configurator.Artifacts;
using Configurator.Artifacts.Profiles;
using Configurator.Core.Definitions;
using Configurator.Core.Profiles;
using Configurator.Desktop.Messages;
using Configurator.Presentation;

/// <summary>Adapts deliberate native actions; the form remains the sole retained-profile custodian.</summary>
public sealed partial class ProfileManagerViewModel : ObservableObject, IDisposable
{
    private readonly FormWorkspaceViewModel _workspace;
    private readonly IMessenger _messenger;
    private readonly Func<string, IProfileVaultStore> _stores;
    private readonly IOsKeyringProvider _keyring;
    private ProfileRecoveryReference? _reference;
    private IReadOnlyList<BlindSecretSlot> _slots = [];
    private DefinitionFormModel? _slotForm;
    private ProfileComparisonProjection? _slotProfile;
    private long _slotRevision;
    private bool _disposed;

    public ProfileManagerViewModel(FormWorkspaceViewModel workspace, IMessenger messenger,
        Func<string, IProfileVaultStore>? stores = null, IOsKeyringProvider? keyring = null)
    {
        _workspace = workspace;
        _messenger = messenger;
        _stores = stores ?? (root => new LocalProfileVaultStore(root));
        _keyring = keyring ?? new OsKeyringProvider();
        workspace.PropertyChanged += WorkspaceChanged;
    }

    [ObservableProperty] private string _directory = string.Empty;
    [ObservableProperty] private string _environmentContext = "local";
    [ObservableProperty] private string _deviceKeyId = string.Empty;
    [ObservableProperty] private bool _useDevice;
    [ObservableProperty] private bool _acknowledgeDeviceOnly;
    [ObservableProperty] private int _modeIndex = 1;
    [ObservableProperty] private int _recipientIndex;
    [ObservableProperty] private string _status = string.Empty;
    [ObservableProperty] private string _recoveryStatus = string.Empty;
    [ObservableProperty] private IReadOnlyList<ProfileRevisionDescriptor> _revisions = [];
    [ObservableProperty] private ProfileRevisionDescriptor? _selectedRevision;
    public ProfileComparisonProjection? Profile => _workspace.Form?.Profile;
    public IReadOnlyList<BlindSecretSlot> RetainedSlots => _slots;
    public IReadOnlyList<ProfileDeltaSummary> UpgradeFields => Profile?.Upgrade.Fields ?? [];
    public IReadOnlyList<DefinitionSchemaDelta> SchemaChanges => Profile?.Upgrade.SchemaDeltas ?? [];
    public string ReviewedProfile => "\u2066RFC 7516 JSON JWE / A256GCM / PBES2-HS512+A256KW / PBKDF2-HMAC-SHA512 600000\u2069";
    public bool HasProfile => Profile is not null;
    public ProfileRecoveryReference? RecoveryReference => _reference;
    public bool HasRecoveryReference => _reference is not null;

    private void WorkspaceChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(FormWorkspaceViewModel.Form) or nameof(FormWorkspaceViewModel.Strings))
        {
            RefreshSlots();
            OnPropertyChanged(nameof(Profile));
            OnPropertyChanged(nameof(HasProfile));
            OnPropertyChanged(nameof(RetainedSlots));
            OnPropertyChanged(nameof(UpgradeFields));
            OnPropertyChanged(nameof(SchemaChanges));
            OnPropertyChanged(nameof(RecoveryReference));
            OnPropertyChanged(nameof(HasRecoveryReference));
        }
    }

    [RelayCommand]
    private Task RefreshAsync() => RunAsync(async token =>
    {
        Revisions = await _stores(Directory).ListAsync(token);
        SelectedRevision = null;
    });

    [RelayCommand]
    private Task CreateDeviceKeyAsync() => RunAsync(async token =>
    {
        using OsKeyringResult result = await _keyring.CreateAsync(OsKeyringPurpose.NativeRecipient, token);
        // Retain an uncertain creation identifier so the operator can investigate rather than create silently.
        if (result.KeyId != Guid.Empty) DeviceKeyId = result.KeyId.ToString("D");
        Status = "profile-keyring-" + result.Status;
    });

    public Task OpenAsync(string passphrase) => RunAsync(async token =>
    {
        DefinitionFormModel form = _workspace.Form ?? throw new ProfileException(ProfileFailureCode.InvalidPayload);
        ProfileRevisionDescriptor revision = SelectedRevision ?? throw new ProfileException(ProfileFailureCode.InvalidPayload);
        byte[] material = Encode(passphrase);
        try
        {
            if (revision.Kind == ProfileFileKind.Comparison)
                throw new ProfileException(ProfileFailureCode.InvalidPayload);
            var store = _stores(Directory);
            using var codec = new NativeProfileContainerCodec();
            int selected = RecipientIndex;
            bool native = UseDevice;
            // Resume on the native owner context before synchronous form adoption.
            // The shell token fences all input/replacement changes during native work.
            UnlockedProfile unlocked = native
                ? await codec.OpenNativeAsync(store, revision, selected, _keyring, token)
                : await codec.OpenStoredAsync(store, revision, selected, material, token);
            bool transferred = false;
            try
            {
                token.ThrowIfCancellationRequested();
                bool opened = await form.OpenProfileAsync(_ =>
                {
                    transferred = true;
                    return Task.FromResult(unlocked);
                }, token);
                if (opened)
                {
                    _reference = null;
                    RecoveryStatus = string.Empty;
                    Status = "profile-opened";
                }
            }
            finally { if (!transferred) unlocked.Dispose(); }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(material);
        }
    });

    public Task SaveAsync(string passphrase, string confirmation) => RunAsync(async token =>
    {
        DefinitionFormModel form = _workspace.Form ?? throw new ProfileException(ProfileFailureCode.InvalidPayload);
        if (ModeIndex is < 0 or > 3) throw new ProfileException(ProfileFailureCode.InvalidPayload);
        if (ModeIndex == 3)
        {
            _messenger.Send(new FocusRequestedMessage("SelectOutput"));
            Status = "profile-target-output";
            return;
        }
        var store = _stores(Directory);
        Guid snapshotId = Guid.NewGuid();
        if (ModeIndex == 0)
        {
            // An ordinary snapshot is incomplete after blind open; never silently turn it into a baseline.
            if (form.Profile is not null) throw new ProfileException(ProfileFailureCode.InvalidPayload);
            Guid profileId = Guid.NewGuid();
            byte[] baseline = CompareBaselineCodec.Serialize(CompareBaselineCodec.Create(form.Definition,
                form.Snapshot(), profileId, snapshotId, EnvironmentContext));
            try { Report(await store.SaveAsync(profileId, snapshotId, ProfileFileKind.Comparison, baseline, token)); }
            finally { CryptographicOperations.ZeroMemory(baseline); }
            return;
        }
        byte[] material = Encode(passphrase);
        byte[]? confirmed = null;
        byte[]? payload = null;
        try
        {
            confirmed = Encode(confirmation);
            if (!CryptographicOperations.FixedTimeEquals(material, confirmed))
                throw new ProfileException(ProfileFailureCode.InvalidPassphrase);
            if (material.Length > 0) ProfileContainerPolicy.ValidatePassphrase(material, creating: true);
            using OsKeyringResult? device = await LookupDeviceAsync(token);
            token.ThrowIfCancellationRequested();
            using var codec = new NativeProfileContainerCodec(device?.Lease);
            IReadOnlyList<ReadOnlyMemory<byte>> recipients = material.Length == 0 ? [] : [material];
            ProfileSealedSnapshot? sealedProfile;
            if (form.Profile is { } held)
                sealedProfile = await form.SealProfileAsync(codec, snapshotId, recipients,
                    AcknowledgeDeviceOnly ? held.ProfileId : null, token);
            else
            {
                Guid profileId = Guid.NewGuid();
                payload = ProfilePayloadCodec.Serialize(form.Definition, form.Snapshot(),
                    ModeIndex == 1 ? ProfileProtectionMode.ImportUpgradeOnly : ProfileProtectionMode.InspectableEncrypted,
                    profileId, snapshotId, "local", EnvironmentContext);
                using UnlockedProfile fresh = ProfilePayloadCodec.Admit(payload);
                sealedProfile = await ProfileRecipientLifecycle.SealAsync(fresh, codec, recipients,
                    AcknowledgeDeviceOnly ? profileId : null, token);
            }
            using (sealedProfile)
            {
                if (sealedProfile is null) { Status = "profile-stale"; return; }
                byte[] encrypted = sealedProfile.CopyContainer();
                try
                {
                    ProfileRecoveryReference reference = sealedProfile.Reference;
                    var result = await store.SaveAsync(reference.ProfileId, reference.SnapshotId,
                        ProfileFileKind.Encrypted, encrypted, token);
                    Report(result);
                    if (result.Outcomes.Count > 0 && result.Outcomes.All(outcome => outcome.Result?.InstallationEstablished == true))
                    {
                        _reference = reference;
                        RecoveryStatus = reference.State.ToString();
                    }
                }
                finally { CryptographicOperations.ZeroMemory(encrypted); }
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(material);
            if (confirmed is not null) CryptographicOperations.ZeroMemory(confirmed);
            if (payload is not null) CryptographicOperations.ZeroMemory(payload);
        }
    });

    public Task CheckRecoveryAsync(string passphrase) => RunAsync(async token =>
    {
        if (_reference is null || SelectedRevision is null)
        {
            Status = "profile-trusted-reference-unavailable";
            return;
        }
        byte[] material = Encode(passphrase);
        try
        {
            ProfileRecoveryResult result = await ProfileRecoveryVerifier.VerifyAsync(_stores(Directory),
                SelectedRevision, _reference, RecipientIndex, material, token);
            RecoveryStatus = result.State.ToString();
            Status = "profile-recovery-" + result.Failure;
        }
        finally { CryptographicOperations.ZeroMemory(material); }
    });

    public Task CompareAsync() => RunAsync(async token =>
    {
        DefinitionFormModel form = _workspace.Form ?? throw new ProfileException(ProfileFailureCode.InvalidPayload);
        if (SelectedRevision?.Kind == ProfileFileKind.Comparison && form.Profile is null)
        {
            byte[] bytes = await _stores(Directory).ReadAsync(SelectedRevision, token);
            try
            {
                CompareBaselineSnapshot baseline = CompareBaselineCodec.ParseJson(bytes);
                CompareBaselineSnapshot current = CompareBaselineCodec.Create(form.Definition, form.Snapshot(),
                    baseline.ProfileId, Guid.NewGuid(), EnvironmentContext);
                Status = baseline.DefinitionDigest != current.DefinitionDigest ? "profile-schema-unavailable" :
                    JsonElement.DeepEquals(baseline.PublicValues, current.PublicValues)
                        ? "profile-public-unchanged-secret-unavailable" : "profile-public-changed-secret-unavailable";
            }
            finally { CryptographicOperations.ZeroMemory(bytes); }
        }
        else if (form.Profile is { } profile)
            Status = profile.Upgrade.SourceSchemaAvailable ? "profile-revalidation-required" : "profile-schema-unavailable";
        else Status = "profile-unavailable";
    });

    /// <summary>Composes held values directly into a create-only sink, without ordinary preview or clipboard.</summary>
    public Task SaveTargetAsync(string artifactId, string outputDirectory, string fileName) => RunAsync(async token =>
    {
        if (_workspace.Form is not { } form || form.Profile is null) return;
        if (!Path.IsPathFullyQualified(outputDirectory) || Path.GetFileName(fileName) != fileName ||
            string.IsNullOrEmpty(fileName))
        {
            Status = "profile-storage-InvalidRequest";
            return;
        }
        IReadOnlyList<string> diagnostics = await form.PrepareProfileOutputAsync(artifactId, token);
        if (diagnostics.Count != 0) { Status = string.Join(",", diagnostics); return; }
        ProtectedArtifactCommitResult? installed = null;
        bool current = await form.WriteProfileOutputToNativeSaveAsync(async (bytes, cancellation) =>
        {
            using ProtectedArtifactPreparation preparation = await new ProtectedArtifactWriter().PrepareAsync(
                Configurator.Core.ArtifactKind.Configuration, Path.Combine(outputDirectory, fileName), bytes, cancellation);
            installed = await preparation.CommitAsync(cancellation);
        }, token);
        Status = "profile-output:" + (installed?.ToString() ?? "unattempted") + ":current=" + current;
    });

    public bool Replace(BlindSecretSlot? slot, bool confirmed)
    {
        if (_disposed || slot is null || !confirmed || _workspace.IsBusy) return false;
        DefinitionFormModel? form = _workspace.Form;
        if (form is null || !ReferenceEquals(form, _slotForm) || form.InputRevision != _slotRevision ||
            form.Profile?.ProfileId != _slotProfile?.ProfileId || form.Profile?.SnapshotId != _slotProfile?.SnapshotId ||
            !_slots.Any(current => ReferenceEquals(current, slot))) return false;
        bool replaced = form.ReplaceRetainedProfileValue(DefinitionPath.Parse(slot.Path), null, true);
        RefreshSlots();
        OnPropertyChanged(nameof(RetainedSlots));
        return replaced;
    }

    private void RefreshSlots()
    {
        _slotForm = _workspace.Form;
        _slotProfile = Profile;
        if (_slotProfile is { } held)
            ModeIndex = held.Mode == ProfileProtectionMode.ImportUpgradeOnly ? 1 : 2;
        _slotRevision = _slotForm?.InputRevision ?? 0;
        _slots = _slotProfile?.Slots.ToArray() ?? [];
    }

    private async ValueTask<OsKeyringResult?> LookupDeviceAsync(CancellationToken token)
    {
        if (!UseDevice) return null;
        if (!Guid.TryParse(DeviceKeyId, out Guid id)) throw new OsKeyringException(OsKeyringStatus.InvalidRequest);
        OsKeyringResult result = await _keyring.LookupAsync(id, OsKeyringPurpose.NativeRecipient, token);
        if (result.Status == OsKeyringStatus.Success && result.Lease is not null) return result;
        OsKeyringStatus status = result.Status;
        result.Dispose();
        throw new OsKeyringException(status);
    }

    private static byte[] Encode(string value)
    {
        if (value.Length > ProfileContainerPolicy.MaximumPassphraseUtf8Bytes)
            throw new ProfileException(ProfileFailureCode.InvalidPassphrase);
        try { return new UTF8Encoding(false, true).GetBytes(value); }
        catch (EncoderFallbackException) { throw new ProfileException(ProfileFailureCode.InvalidPassphrase); }
    }

    private void Report(ProtectedArtifactBundleResult result) =>
        Status = "profile-publication:" + result.CompletionCode + ":cleanup=" + result.CleanupFailed + ":" +
            string.Join(",", result.Outcomes.Select(outcome => outcome.Result?.ToString() ?? "unattempted"));

    private async Task RunAsync(Func<CancellationToken, Task> action)
    {
        if (_disposed || _workspace.IsBusy) return;
        using DesktopOperation? operation = DesktopOperation.Begin(_messenger, "profile");
        if (operation is null) return;
        try { await action(operation.Token); }
        catch (ProfileException exception) { Status = "profile-" + exception.Code; }
        catch (ProfileRecoveryException exception) { Status = "profile-" + exception.Failure; }
        catch (ProfileVaultStoreException exception) { Status = "profile-storage-" + exception.Status; }
        catch (OsKeyringException exception) { Status = "profile-keyring-" + exception.Status; }
        catch (DefinitionException exception) { Status = "profile-definition-" + exception.Code; }
        catch (OperationCanceledException) when (operation.Token.IsCancellationRequested) { Status = "profile-cancelled"; }
        finally
        {
            operation.Finish();
            OnPropertyChanged(nameof(RecoveryReference));
            OnPropertyChanged(nameof(HasRecoveryReference));
            RefreshSlots();
            OnPropertyChanged(nameof(Profile));
            OnPropertyChanged(nameof(HasProfile));
            OnPropertyChanged(nameof(RetainedSlots));
            OnPropertyChanged(nameof(UpgradeFields));
            OnPropertyChanged(nameof(SchemaChanges));
        }
    }

    public void Dispose()
    {
        _disposed = true;
        _workspace.PropertyChanged -= WorkspaceChanged;
        _reference = null;
        _slots = [];
        _slotForm = null;
        _slotProfile = null;
    }
}
