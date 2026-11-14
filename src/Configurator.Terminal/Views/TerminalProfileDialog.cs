namespace Configurator.Terminal;

using System.Globalization;
using System.Security.Cryptography;
using Configurator.Artifacts;
using Configurator.Artifacts.Profiles;
using Configurator.Core;
using Configurator.Core.Definitions;
using Configurator.Core.Profiles;
using Configurator.Presentation;
using global::Terminal.Gui.App;
using global::Terminal.Gui.ViewBase;
using global::Terminal.Gui.Views;

/// <summary>Native commands over form-owned custody; no decrypted answer or artifact preview channel.</summary>
internal sealed class TerminalProfileDialog : Dialog
{
    private readonly DefinitionFormModel _form;
    private readonly Action<Action> _dispatch;
    private readonly Func<string, IProfileVaultStore> _stores;
    private readonly IOsKeyringProvider _keyring;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly TerminalSecretBuffer _passphrase = new(ProfileContainerPolicy.MaximumPassphraseUtf8Bytes, false);
    private readonly TerminalSecretBuffer _confirmation = new(ProfileContainerPolicy.MaximumPassphraseUtf8Bytes, false);
    private readonly Label _status;
    private readonly Button _close;
    private readonly List<View> _inputs = [];
    private readonly List<Label> _labels = [];
    private IReadOnlyList<ProfileRevisionDescriptor> _revisions = [];
    private bool _device;
    private bool _acknowledged;
    private bool _closed;

    internal TerminalProfileDialog(DefinitionFormModel form, IApplication? application = null,
        Func<string, IProfileVaultStore>? stores = null, IOsKeyringProvider? keyring = null,
        Action<Action>? dispatch = null)
    {
        _form = form;
        _dispatch = dispatch ?? (application is null ? action => action() : application.Invoke);
        _stores = stores ?? (root => new LocalProfileVaultStore(root));
        _keyring = keyring ?? new OsKeyringProvider();
        Title = TextFor("Profiles");
        X = Pos.Center(); Y = Pos.Center(); Width = Dim.Fill(2); Height = Dim.Fill(2);
        Body = new View { Width = Dim.Fill(), Height = Dim.Fill(), CanFocus = true };
        Body.ViewportSettings |= ViewportSettingsFlags.HasVerticalScrollBar;
        Add(Body);
        Label policy = AddLabel("ProfilePolicy", 0, fullWidth: true);
        StoreRoot = Input("ProfileStore", Pos.Bottom(policy) + 1);
        Revision = Input("ProfileRevision", Pos.Bottom(StoreRoot), "0");
        Recipient = Input("ProfileRecipient", Pos.Bottom(Revision), "0");
        TrustedDigest = Input("ProfileDigest", Pos.Bottom(Recipient));
        DeviceId = Input("ProfileDeviceId", Pos.Bottom(TrustedDigest));
        Passphrase = new SecretTextField(_passphrase) { X = 33, Y = Pos.Bottom(DeviceId), Width = Dim.Fill(), Height = 1 };
        Confirmation = new SecretTextField(_confirmation) { X = 33, Y = Pos.Bottom(Passphrase), Width = Dim.Fill(), Height = 1 };
        AddLabel("ProfilePassphrase", Passphrase.Y); AddLabel("ProfileConfirmation", Confirmation.Y);
        Body.Add(Passphrase, Confirmation);
        _inputs.AddRange([Passphrase, Confirmation]);
        ArtifactId = Input("ProfileArtifact", Pos.Bottom(Confirmation), form.EligibleArtifacts.Count == 0 ? "" : form.EligibleArtifacts[0].Id);
        TargetPath = Input("ProfileTargetPath", Pos.Bottom(ArtifactId));
        RetirePath = Input("ProfileRetirePath", Pos.Bottom(TargetPath));
        Label recovery = AddLabel("ProfileRecoveryWarning", Pos.Bottom(RetirePath) + 1, fullWidth: true);
        var device = new Button { X = 0, Y = Pos.Bottom(recovery) + 1, Text = TextFor("ProfilePortable") };
        device.Accepting += (_, args) =>
        {
            args.Handled = true;
            _device = !_device;
            device.Text = TextFor(_device ? "ProfileDevice" : "ProfilePortable");
        };
        var acknowledge = new Button { X = 0, Y = Pos.Bottom(device) + 1, Text = TextFor("ProfileAcknowledge") };
        acknowledge.Accepting += (_, args) =>
        {
            args.Handled = true;
            _acknowledged = !_acknowledged;
            acknowledge.Text = TextFor("ProfileAcknowledge") + (_acknowledged ? " [X]" : " [ ]");
        };
        Body.Add(device, acknowledge); _inputs.AddRange([device, acknowledge]);
        _status = new Label { X = 0, Y = Pos.Bottom(acknowledge) + 1, Width = Dim.Fill(), Height = Dim.Auto(DimAutoStyle.Text, 1) };
        Body.Add(_status);
        Pos commandRow = Pos.Bottom(_status) + 1;
        foreach ((string key, Func<CancellationToken, Task<string>> command) in
            new (string, Func<CancellationToken, Task<string>>)[]
        {
            ("ProfileList", (Func<CancellationToken, Task<string>>)ListAsync),
            ("ProfileOpen", OpenAsync),
            ("ProfileCompare", CompareAsync),
            ("ProfileSaveBlind", token => SaveAsync(ProfileProtectionMode.ImportUpgradeOnly, false, token)),
            ("ProfileSaveInspectable", token => SaveAsync(ProfileProtectionMode.InspectableEncrypted, false, token)),
            ("ProfileSaveComparison", SaveComparisonAsync),
            ("ProfileUpgrade", token => SaveAsync(ProfileProtectionMode.ImportUpgradeOnly, true, token)),
            ("ProfileCheckRecovery", CheckRecoveryAsync)
        })
        {
            var button = new Button { X = 0, Y = commandRow, Text = TextFor(key) };
            button.Accepting += (_, args) => { args.Handled = true; Start(command); };
            Body.Add(button); _inputs.Add(button);
            commandRow = Pos.Bottom(button) + 1;
        }
        var replace = new Button { X = 0, Y = commandRow, Text = TextFor("ProfileRetire") };
        DefinitionPath? pendingRetirement = null;
        replace.Accepting += (_, args) =>
        {
            args.Handled = true;
            if (!Completion.IsCompleted) return;
            DefinitionPath path;
            try { path = DefinitionPath.Parse(RetirePath.Text); }
            catch (DefinitionException) { _status.Text = TextFor("ProfileRetirePath"); return; }
            if (pendingRetirement is null || !pendingRetirement.Equals(path))
            {
                pendingRetirement = path;
                _status.Text = TextFor("ProfileRetireConfirm");
                return;
            }
            pendingRetirement = null;
            _status.Text = TextFor("ProfileResult") + " " +
                _form.ReplaceRetainedProfileValue(path, null, confirmed: true);
        };
        Body.Add(replace); _inputs.Add(replace);
        var target = new Button { X = 0, Y = Pos.Bottom(replace) + 1, Text = TextFor("ProfileTarget") };
        target.Accepting += (_, args) =>
        {
            args.Handled = true;
            if (!Completion.IsCompleted) return;
            if (_form.Profile is not null) { Start(SaveHeldTargetAsync); return; }
            TargetOutputRequested = true;
            Retire();
            application?.RequestStop(this);
        };
        Body.Add(target); _inputs.Add(target);
        _close = new Button { X = 0, Y = Pos.Bottom(target) + 1, Text = TextFor("DialogCancel") };
        _close.Accepting += (_, args) => { args.Handled = true; Retire(); application?.RequestStop(this); };
        Body.Add(_close);
        Body.SubViewsLaidOut += (_, _) =>
        {
            int height = _close.Frame.Bottom + 1;
            if (Body.GetContentHeight() != height) Body.SetContentHeight(height);
            RevealFocusedControl();
        };
        StoreRoot.SetFocus();
        Body.FocusedChanged += (_, _) => RevealFocusedControl();
    }

    private void RevealFocusedControl()
    {
        if (Body.Focused is not { } focused || focused.Frame.Height == 0) return;
        int top = Body.Viewport.Y;
        if (focused.Frame.Top < top) top = focused.Frame.Top;
        else if (focused.Frame.Bottom > Body.Viewport.Bottom) top = focused.Frame.Bottom - Body.Viewport.Height;
        if (top != Body.Viewport.Y) Body.ScrollVertical(top - Body.Viewport.Y);
    }

    internal View Body { get; }
    internal TextField StoreRoot { get; }
    internal TextField Revision { get; }
    internal TextField Recipient { get; }
    internal TextField TrustedDigest { get; }
    internal TextField DeviceId { get; }
    internal TextField ArtifactId { get; }
    internal TextField TargetPath { get; }
    internal TextField RetirePath { get; }
    internal SecretTextField Passphrase { get; }
    internal SecretTextField Confirmation { get; }
    internal Task Completion { get; private set; } = Task.CompletedTask;
    internal string StatusText => _status.Text;
    internal ProfileRecoveryReference? RecoveryReference { get; private set; }
    internal ProtectedArtifactCommitResult? LastTargetReceipt { get; private set; }
    internal bool TargetOutputRequested { get; private set; }

    private string TextFor(string key) => TerminalStrings.Get(key, _form.Locale);
    private Label AddLabel(string key, Pos row, bool fullWidth = false)
    {
        var label = new Label
        {
            X = 0, Y = row, Width = fullWidth ? Dim.Fill() : 32,
            Height = fullWidth ? Dim.Auto(DimAutoStyle.Text) : 1, Text = TextFor(key)
        };
        _labels.Add(label);
        Body.Add(label);
        return label;
    }
    private TextField Input(string key, Pos row, string text = "")
    {
        AddLabel(key, row);
        var input = new TextField { X = 33, Y = row, Width = Dim.Fill(), Height = 1, Text = text };
        Body.Add(input); _inputs.Add(input);
        return input;
    }

    private void Start(Func<CancellationToken, Task<string>> command)
    {
        if (_closed || !Completion.IsCompleted) return;
        foreach (View input in _inputs) input.Enabled = false;
        _status.Text = TextFor("ProfileWorking");
        Completion = ExecuteAsync(command);
    }

    private async Task ExecuteAsync(Func<CancellationToken, Task<string>> command)
    {
        string result;
        try { result = await command(_lifetime.Token).ConfigureAwait(false); }
        catch (ProfileException exception) { result = exception.Code.ToString(); }
        catch (ProfileRecoveryException exception) { result = exception.Failure.ToString(); }
        catch (ProfileVaultStoreException exception) { result = exception.Status.ToString(); }
        catch (OsKeyringException exception) { result = exception.Status.ToString(); }
        catch (OperationCanceledException) { result = "Cancelled"; }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or FormatException or
            IOException or UnauthorizedAccessException)
        { result = "InvalidRequest"; }
        finally { _passphrase.Clear(); _confirmation.Clear(); }
        if (_closed) return;
        _dispatch(() =>
        {
            if (_closed) return;
            Passphrase.ClearSensitiveState(); Confirmation.ClearSensitiveState();
            foreach (View input in _inputs) input.Enabled = true;
            _status.Text = TextFor("ProfileResult") + " \u2068" + result + "\u2069";
            StoreRoot.SetFocus();
        });
    }

    private IProfileVaultStore Store() => _stores(StoreRoot.Text);
    private int Index(TextField field) => int.TryParse(field.Text, NumberStyles.None,
        CultureInfo.InvariantCulture, out int index) && index >= 0 ? index
        : throw new ProfileException(ProfileFailureCode.InvalidPayload);
    private ProfileRevisionDescriptor Selected() => Index(Revision) < _revisions.Count
        ? _revisions[Index(Revision)] : throw new ProfileException(ProfileFailureCode.InvalidPayload);

    private async Task<string> ListAsync(CancellationToken token)
    {
        _revisions = await Store().ListAsync(token).ConfigureAwait(false);
        return string.Join(" | ", _revisions.Select((revision, index) =>
            $"{index}: {revision.ProfileId:N}/{revision.SnapshotId:N} {revision.Kind}"));
    }

    private async Task<string> OpenAsync(CancellationToken token)
    {
        IProfileVaultStore store = Store();
        ProfileRevisionDescriptor revision = Selected();
        int recipient = Index(Recipient);
        bool device = _device;
        byte[] material = _passphrase.Count == 0 ? [] : _passphrase.CopyUtf8Bytes();
        try
        {
            using var codec = new NativeProfileContainerCodec();
            bool opened = await _form.OpenProfileAsync(async cancellation => device
                ? await codec.OpenNativeAsync(store, revision, recipient, _keyring, cancellation).ConfigureAwait(false)
                : await codec.OpenStoredAsync(store, revision, recipient, material, cancellation).ConfigureAwait(false), token).ConfigureAwait(false);
            return opened ? "Unlocked" : "Stale";
        }
        finally { CryptographicOperations.ZeroMemory(material); }
    }

    private async Task<string> CompareAsync(CancellationToken token)
    {
        IProfileVaultStore store = Store();
        ProfileRevisionDescriptor revision = Selected();
        if (_form.Profile is { } current && current.ProfileId == revision.ProfileId &&
            current.SnapshotId == revision.SnapshotId) return PlanSummary(current.Upgrade);
        if (revision.Kind == ProfileFileKind.Comparison)
        {
            byte[] bytes = await store.ReadAsync(revision, token).ConfigureAwait(false);
            try
            {
                ProfileUpgradePlan plan = ProfileUpgradePlanner.Plan(CompareBaselineCodec.ParseJson(bytes), _form.Definition);
                return PlanSummary(plan);
            }
            finally { CryptographicOperations.ZeroMemory(bytes); }
        }
        string result = await OpenAsync(token).ConfigureAwait(false);
        return result == "Unlocked" && _form.Profile is { } profile
            ? PlanSummary(profile.Upgrade) : result;
    }

    private static string PlanSummary(ProfileUpgradePlan plan) =>
        $"SchemaAvailable={plan.SourceSchemaAvailable}; SchemaChanges={plan.SchemaDeltas.Count}; " +
        string.Join(", ", plan.Fields.GroupBy(field => field.Eligibility).Select(group => $"{group.Key}={group.Count()}"));

    private async Task<string> SaveAsync(ProfileProtectionMode mode, bool upgrade, CancellationToken token)
    {
        IProfileVaultStore store = Store();
        long revision = _form.InputRevision;
        Guid profileId = upgrade ? _form.Profile?.ProfileId ??
            throw new ProfileException(ProfileFailureCode.InvalidPayload) : Guid.NewGuid();
        Guid snapshotId = Guid.NewGuid();
        byte[] material = _passphrase.Count == 0 ? [] : _passphrase.CopyUtf8Bytes();
        byte[] confirmation = _confirmation.Count == 0 ? [] : _confirmation.CopyUtf8Bytes();
        try
        {
            if (!CryptographicOperations.FixedTimeEquals(material, confirmation))
                return "PassphraseConfirmationMismatch";
            if (material.Length != 0) ProfileContainerPolicy.ValidatePassphrase(material, creating: true);
            if (material.Length == 0 && (!_device || !_acknowledged))
                return "DeviceBoundAcknowledgementRequired";
            // Inspectable saves never downgrade an existing blind owner through ordinary answers.
            if (!upgrade && _form.Profile is not null) return "UseUpgradeSnapshot";
            using OsKeyringResult? device = _device
                ? await _keyring.CreateAsync(OsKeyringPurpose.NativeRecipient, token).ConfigureAwait(false) : null;
            if (device is not null && (device.Status != OsKeyringStatus.Success || device.Lease is null))
                return device.Status.ToString();
            using var codec = new NativeProfileContainerCodec(device?.Lease);
            ReadOnlyMemory<byte>[] recipients = material.Length == 0 ? [] : [material];
            using ProfileSealedSnapshot? sealedProfile = upgrade
                ? await _form.SealProfileAsync(codec, snapshotId, recipients,
                    _acknowledged ? profileId : null, token).ConfigureAwait(false)
                : await SealNewAsync(codec, mode, profileId, snapshotId, recipients, token).ConfigureAwait(false);
            if (sealedProfile is null || _form.InputRevision != revision) return "Stale";
            byte[] bytes = sealedProfile.CopyContainer();
            try
            {
                ProtectedArtifactBundleResult saved = await store.SaveAsync(profileId, snapshotId,
                    ProfileFileKind.Encrypted, bytes, token).ConfigureAwait(false);
                bool installed = saved.Outcomes.Any(outcome => outcome.Result?.InstallationEstablished == true);
                if (installed)
                    RecoveryReference = sealedProfile.Reference;
                return Receipt(saved) + $"; Current={_form.InputRevision == revision}" +
                    (installed && RecoveryReference is { } reference
                    ? $" {reference.State}; Digest={reference.Digest.Value}; Device={reference.DeviceRecipientId:N}; Snapshot={snapshotId:N}" : "");
            }
            finally { CryptographicOperations.ZeroMemory(bytes); }
        }
        finally { CryptographicOperations.ZeroMemory(material); CryptographicOperations.ZeroMemory(confirmation); }
    }

    private async ValueTask<ProfileSealedSnapshot> SealNewAsync(NativeProfileContainerCodec codec,
        ProfileProtectionMode mode, Guid profileId, Guid snapshotId,
        IReadOnlyList<ReadOnlyMemory<byte>> recipients, CancellationToken token)
    {
        byte[] payload = ProfilePayloadCodec.Serialize(_form.Definition, _form.Snapshot(), mode,
            profileId, snapshotId, "local", "local");
        try
        {
            using UnlockedProfile profile = ProfilePayloadCodec.Admit(payload);
            return await ProfileRecipientLifecycle.SealAsync(profile, codec, recipients,
                _acknowledged ? profileId : null, token).ConfigureAwait(false);
        }
        finally { CryptographicOperations.ZeroMemory(payload); }
    }

    private async Task<string> SaveComparisonAsync(CancellationToken token)
    {
        if (_form.Profile is not null) return "UseUpgradeSnapshot";
        Guid profileId = Guid.NewGuid(), snapshotId = Guid.NewGuid();
        byte[] bytes = CompareBaselineCodec.Serialize(CompareBaselineCodec.Create(_form.Definition,
            _form.Snapshot(), profileId, snapshotId, "local"));
        try { return Receipt(await Store().SaveAsync(profileId, snapshotId, ProfileFileKind.Comparison, bytes, token).ConfigureAwait(false)); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    private async Task<string> CheckRecoveryAsync(CancellationToken token)
    {
        ProfileRevisionDescriptor revision = Selected();
        ProfileRecoveryReference reference = RecoveryReference is { } saved &&
            saved.ProfileId == revision.ProfileId && saved.SnapshotId == revision.SnapshotId ? saved
            : new(revision.ProfileId, revision.SnapshotId, ArtifactDigest.Parse(TrustedDigest.Text),
                string.IsNullOrWhiteSpace(DeviceId.Text) ? null : Guid.Parse(DeviceId.Text), true);
        byte[] material = _passphrase.Count == 0 ? [] : _passphrase.CopyUtf8Bytes();
        try
        {
            ProfileRecoveryResult result = await ProfileRecoveryVerifier.VerifyAsync(Store(), revision,
                reference, Index(Recipient), material, token).ConfigureAwait(false);
            return $"{result.State}; {result.Failure}";
        }
        finally { CryptographicOperations.ZeroMemory(material); }
    }

    private async Task<string> SaveHeldTargetAsync(CancellationToken token)
    {
        string artifact = ArtifactId.Text;
        string path = TargetPath.Text;
        if (!Path.IsPathFullyQualified(path)) return "InvalidRequest";
        IReadOnlyList<string> diagnostics = await _form.PrepareProfileOutputAsync(artifact, token).ConfigureAwait(false);
        if (diagnostics.Count != 0) return string.Join(", ", diagnostics);
        ProtectedArtifactPreparation? preparation = null;
        ProtectedArtifactCommitResult? receipt = null;
        try
        {
            bool current = await _form.WriteProfileOutputToNativeSaveAsync(async (bytes, cancellation) =>
            {
                preparation = await new ProtectedArtifactWriter().PrepareAsync(ArtifactKind.Configuration,
                    path, bytes, cancellation).ConfigureAwait(false);
                receipt = await preparation.CommitAsync(cancellation).ConfigureAwait(false);
                LastTargetReceipt = receipt;
            }, token).ConfigureAwait(false);
            return $"{receipt}; Current={current}";
        }
        catch (Exception) when (preparation?.EstablishedResult is not null)
        {
            LastTargetReceipt = preparation.EstablishedResult;
            return $"{preparation.EstablishedResult}; CleanupFailed={preparation.CleanupFailed}";
        }
        finally { preparation?.Dispose(); }
    }

    private static string Receipt(ProtectedArtifactBundleResult result) =>
        $"{result}; " + string.Join("; ", result.Outcomes.Select(outcome => outcome.Result is { } receipt
            ? $"Status={receipt.Status}; Installed={receipt.InstallationEstablished}; Verified={receipt.VerificationSucceeded}; CleanupFailed={receipt.CleanupFailed}"
            : "NotAttempted"));

    internal void Retire()
    {
        if (_closed) return;
        _closed = true;
        _lifetime.Cancel();
        Passphrase.ClearSensitiveState(); Confirmation.ClearSensitiveState();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Retire(); _passphrase.Dispose(); _confirmation.Dispose();
            if (Completion.IsCompleted) _lifetime.Dispose();
            else _ = Completion.ContinueWith(_ => _lifetime.Dispose(), CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }
        base.Dispose(disposing);
    }
}
