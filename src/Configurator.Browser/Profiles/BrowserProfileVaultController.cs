namespace Configurator.Browser.Profiles;

using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Configurator.Browser.Services;
using Configurator.Core;
using Configurator.Core.Definitions;
using Configurator.Core.Profiles;
using Configurator.Presentation;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;

/// <summary>
/// Editor-scoped controller owning dedicated profile worker custody, comparison results, and recovery
/// references across transient panel mounts. Never stores or accepts string passphrases.
/// </summary>
public sealed class BrowserProfileVaultController : IAsyncDisposable
{
    private readonly IJSRuntime _javascript;
    private readonly BrowserDefinitionRegistry _registry;
    private readonly Func<ProfileWorkerAuthority> _captureAuthority;
    private readonly Func<bool> _isReady;

    private ProfileWorkerClient? _worker;
    private ProfileWorkerClient? _recoveryWorker;
    private ProfileWorkerField[] _fields = [];
    private ProfileRecoveryReference? _recoveryReference;
    private BrowserArtifactExport? _comparisonExport;
    private CancellationTokenSource? _operation;
    private string _artifactId = string.Empty;
    private string _status = "ProfileLocalOnly";
    private string? _code;
    private string? _replacement;
    private ProfileProtectionMode _mode = ProfileProtectionMode.ImportUpgradeOnly;
    private int _recipientIndex;
    private int _recoveryIndex;
    private bool _addRecovery;
    private bool _inspectConfirmed;
    private bool _adopting;
    private bool _retired;
    private bool _disposed;
    private long _fileKey;
    private long _headingFocusGeneration;

    public BrowserProfileVaultController(
        DefinitionFormModel form,
        IJSRuntime javascript,
        BrowserDefinitionRegistry registry,
        Func<ProfileWorkerAuthority> captureAuthority,
        Func<bool>? isReady = null)
    {
        ArgumentNullException.ThrowIfNull(form);
        ArgumentNullException.ThrowIfNull(javascript);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(captureAuthority);

        Form = form;
        _javascript = javascript;
        _registry = registry;
        _captureAuthority = captureAuthority;
        _isReady = isReady ?? (static () => true);
        SyncEligibleArtifacts();
        Form.InputChanged += OnFormInputChanged;
    }

    public event Action? Changed;

    public event Action? CredentialsCleared;

    public DefinitionFormModel Form { get; }

    public IReadOnlyList<ProfileWorkerField> Fields => _fields;

    public ProfileRecoveryReference? RecoveryReference => _recoveryReference;

    public string Status => _status;

    public string? Code => _code;

    public string? Replacement => _replacement;

    public bool InspectConfirmed => _inspectConfirmed;

    public string ArtifactId => _artifactId;

    public ProfileProtectionMode Mode => _mode;

    public int RecipientIndex => _recipientIndex;

    public int RecoveryIndex => _recoveryIndex;

    public bool AddRecovery => _addRecovery;

    public long FileKey => _fileKey;

    public long HeadingFocusGeneration => _headingFocusGeneration;

    public bool IsBusy => _operation is not null;

    public bool Ready => !_disposed && !_retired && _isReady();

    public bool Consented => Form.SessionPrivacy == DefinitionSessionPrivacy.BrowserSecretsEnabled;

    public ProfileWorkerAuthority CaptureAuthority() => _captureAuthority();

    public bool IsCurrent(ProfileWorkerAuthority captured) =>
        !_disposed && !_retired && _isReady() && captured == _captureAuthority();

    public void SyncEligibleArtifacts()
    {
        if (!Form.EligibleArtifacts.Any(artifact => artifact.Id == _artifactId))
            _artifactId = Form.EligibleArtifacts.Count == 0 ? string.Empty : Form.EligibleArtifacts[0].Id;
    }

    public void RetryProfileTask(ProfileWorkerAuthority captured)
    {
        if (!IsCurrent(captured) || IsBusy) return;
        _status = "ProfileLocalOnly";
        _code = null;
        ++_headingFocusGeneration;
        NotifyChanged();
    }

    public void Cancel(ProfileWorkerAuthority captured)
    {
        if (!IsCurrent(captured)) return;
        _operation?.Cancel();
        Form.CloseBrowserProfile();
        RetireWorkers();
        if (_comparisonExport is not null) _ = _comparisonExport.InvalidateAsync().AsTask();
        _status = "ProfileCancelled";
        _code = null;
        NotifyChanged();
    }

    public void ReportRejected(ProfileWorkerAuthority captured, ProfileFailureCode code)
    {
        if (!IsCurrent(captured) || IsBusy) return;
        _status = "ProfileRejected";
        _code = code.ToString();
        NotifyCredentialsCleared();
        NotifyChanged();
    }

    public void SetInspectConfirmed(ProfileWorkerAuthority captured, bool confirmed)
    {
        if (!IsCurrent(captured) || IsBusy) return;
        _inspectConfirmed = confirmed;
        NotifyChanged();
    }

    public void SetArtifactId(ProfileWorkerAuthority captured, string artifactId)
    {
        if (!IsCurrent(captured) || IsBusy) return;
        _artifactId = artifactId ?? string.Empty;
        NotifyChanged();
    }

    public void SetMode(ProfileWorkerAuthority captured, ProfileProtectionMode mode)
    {
        if (!IsCurrent(captured) || IsBusy || !Consented) return;
        _mode = mode;
        NotifyChanged();
    }

    public void SetRecipientIndex(ProfileWorkerAuthority captured, string? rawIndex, bool recovery)
    {
        if (!IsCurrent(captured) || IsBusy || !Consented ||
            !int.TryParse(rawIndex, NumberStyles.None, CultureInfo.InvariantCulture, out int index) || index is < 0 or > 7)
            return;
        if (recovery) _recoveryIndex = index;
        else _recipientIndex = index;
        NotifyChanged();
    }

    public void SetAddRecovery(ProfileWorkerAuthority captured, bool addRecovery)
    {
        if (!IsCurrent(captured) || IsBusy) return;
        _addRecovery = addRecovery;
        NotifyChanged();
    }

    public void BeginReplacement(ProfileWorkerAuthority captured, string path)
    {
        if (!IsCurrent(captured) || IsBusy) return;
        _replacement = path;
        NotifyChanged();
    }

    public void CancelReplacement(ProfileWorkerAuthority captured)
    {
        if (!IsCurrent(captured)) return;
        _replacement = null;
        NotifyChanged();
    }

    public async Task OpenAsync(
        ProfileWorkerAuthority captured,
        IBrowserFile vaultFile,
        ReadOnlyMemory<byte> passphraseUtf8)
    {
        ArgumentNullException.ThrowIfNull(vaultFile);
        byte[] credential = passphraseUtf8.ToArray();
        try
        {
            await RunAsync(captured, needsConsent: true, async token =>
            {
                if (Form.BrowserProfile is not null) return;
                byte[] container = [];
                try
                {
                    container = await ReadFileAsync(vaultFile, token);
                    ProfileWorkerReply opened = await Worker.OpenAsync(container, _recipientIndex, credential,
                        candidate =>
                        {
                            if (!IsCurrent(captured) || candidate.FamilyId != Form.Definition.FamilyId ||
                                !ArtifactDigest.TryParse(candidate.DefinitionDigest, out ArtifactDigest digest))
                                return false;
                            _registry.AdmitHistoricalIdentity(candidate.FamilyId, candidate.DefinitionRevision, digest);
                            return true;
                        }, token);
                    if (opened.Status != "ok" || opened.PublicValues is not { } publicValues ||
                        !Enum.TryParse(opened.Mode, out ProfileProtectionMode mode))
                        throw new ProfileException(ProfileFailureCode.InvalidPayload);
                    ProfileWorkerReply plan = await Worker.PlanAsync(opened.ProfileReference, Form.Definition, token);
                    if (!IsCurrent(captured) || token.IsCancellationRequested) return;
                    _adopting = true;
                    try
                    {
                        if (!Form.AdoptBrowserProfile(captured.AnswerRevision, opened.ProfileReference, mode,
                            publicValues, opened.RetainedPaths.Select(DefinitionPath.Parse).ToArray(),
                            RetireWorkers, DiscardWorkerOutput))
                            throw new ProfileException(ProfileFailureCode.ModeRestricted);
                    }
                    finally { _adopting = false; }
                    _fields = plan.Fields;
                    ++_fileKey;
                    _status = "ProfileOpened";
                    ++_headingFocusGeneration;
                }
                catch
                {
                    RetireWorkers();
                    throw;
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(container);
                }
            });
        }
        finally
        {
            CryptographicOperations.ZeroMemory(credential);
        }
    }

    public Task RefreshPlanAsync(ProfileWorkerAuthority captured) => RunAsync(captured, needsConsent: true, async token =>
    {
        if (Form.BrowserProfile is not { } profile) return;
        ProfileWorkerReply plan = await Worker.PlanAsync(profile.Reference, Form.Definition, token);
        if (!IsCurrent(captured)) return;
        _fields = plan.Fields;
        _status = "ProfilePlanReady";
    });

    public Task ReplaceAsync(ProfileWorkerAuthority captured, string path) => RunAsync(captured, needsConsent: true, async token =>
    {
        if (_replacement != path || Form.BrowserProfile is not { } profile) return;
        try
        {
            bool replaced = await Form.ReplaceBrowserRetainedValueAsync(DefinitionPath.Parse(path), null, true,
                async (requested, cancellation) =>
                {
                    ProfileWorkerReply reply = await Worker.RetireAsync(profile.Reference, requested, cancellation);
                    if (reply.Status != "ok") throw new ProfileException(ProfileFailureCode.ModeRestricted);
                    if (IsCurrent(captured)) _adopting = true;
                    return reply.RetainedPaths.Select(DefinitionPath.Parse).ToArray();
                }, token);
            _replacement = null;
            _fields = [];
            _status = replaced ? "ProfileReplacementReady" : "ProfileRejected";
        }
        catch
        {
            Form.CloseBrowserProfile();
            RetireWorkers();
            throw;
        }
        finally { _adopting = false; }
    });

    public Task InspectAsync(ProfileWorkerAuthority captured) => RunAsync(captured, needsConsent: true, async token =>
    {
        if (!_inspectConfirmed || Form.BrowserProfile is not
            { Mode: ProfileProtectionMode.InspectableEncrypted } profile) return;
        AnswerSnapshot disclosed = await Worker.InspectAsync(profile.Reference, token);
        if (!IsCurrent(captured) || token.IsCancellationRequested) return;
        _adopting = true;
        try
        {
            if (!Form.AdoptBrowserProfileDisclosure(profile.Reference, captured.AnswerRevision, disclosed))
                throw new ProfileException(ProfileFailureCode.ModeRestricted);
        }
        finally { _adopting = false; }
        _status = "ProfileInspected";
        ++_headingFocusGeneration;
    });

    public Task DownloadPrivateAsync(ProfileWorkerAuthority captured) => RunAsync(captured, needsConsent: true, async token =>
    {
        if (Form.BrowserProfile is not { } profile ||
            !Form.EligibleArtifacts.Any(artifact => artifact.Id == _artifactId) ||
            Form.HasInputErrors || Form.HasAnswerBudgetError) return;
        ProfileWorkerReply reply = await Worker.DownloadArtifactAsync(profile.Reference, Form.Definition,
            Form.Snapshot(), _artifactId, cancellationToken: token);
        if (!IsCurrent(captured)) return;
        _status = reply.Status == "ok" ? "ProfileDownloaded" : "ProfileNotReady";
        _code = reply.Status == "ok" ? null : "profile-composition-incomplete";
    });

    public async Task SaveAsync(
        ProfileWorkerAuthority captured,
        IReadOnlyList<ReadOnlyMemory<byte>> passphrasesUtf8,
        IReadOnlyList<ReadOnlyMemory<byte>> confirmationsUtf8)
    {
        ArgumentNullException.ThrowIfNull(passphrasesUtf8);
        ArgumentNullException.ThrowIfNull(confirmationsUtf8);

        var materials = new List<byte[]>(passphrasesUtf8.Count);
        var confirmations = new List<byte[]>(confirmationsUtf8.Count);
        try
        {
            foreach (ReadOnlyMemory<byte> item in passphrasesUtf8)
                materials.Add(item.ToArray());
            foreach (ReadOnlyMemory<byte> item in confirmationsUtf8)
                confirmations.Add(item.ToArray());

            await RunAsync(captured, needsConsent: true, async token =>
            {
                if (Form.HasInputErrors || Form.HasAnswerBudgetError ||
                    Form.RequiresBrowserProfileComposition && Form.BrowserProfile is null) return;
                if (materials.Count >= 2 && CryptographicOperations.FixedTimeEquals(materials[0], materials[1]))
                    throw new ProfileException(ProfileFailureCode.InvalidPassphrase);

                ReadOnlyMemory<byte>[] recipients = materials.Select(bytes => (ReadOnlyMemory<byte>)bytes).ToArray();
                ReadOnlyMemory<byte>[] confirmed = confirmations.Select(bytes => (ReadOnlyMemory<byte>)bytes).ToArray();
                ProfileWorkerReply reply = Form.BrowserProfile is { } profile
                    ? await Worker.SealAsync(profile.Reference, Form.Definition, Form.Snapshot(), Guid.NewGuid(),
                        recipients, confirmed, cancellationToken: token)
                    : await Worker.CreateAsync(Form.Definition, Form.Snapshot(), _mode, Guid.NewGuid(), Guid.NewGuid(),
                        "browser-catalogue", "browser-workspace", recipients, confirmed, token);
                if (!IsCurrent(captured) || reply.Status != "ok" ||
                    !ArtifactDigest.TryParse(reply.CiphertextDigest, out ArtifactDigest digest))
                    throw new ProfileException(ProfileFailureCode.InvalidPayload);
                _recoveryReference = new(reply.ProfileId, reply.SnapshotId, digest, null, portableRecoveryConfigured: true);
                _recoveryIndex = materials.Count >= 2 ? 1 : 0;
                _status = "ProfileSavedUnverified";
            });
        }
        finally
        {
            foreach (byte[] bytes in materials) CryptographicOperations.ZeroMemory(bytes);
            foreach (byte[] bytes in confirmations) CryptographicOperations.ZeroMemory(bytes);
        }
    }

    public async Task RecoverAsync(
        ProfileWorkerAuthority captured,
        IBrowserFile recoveryFile,
        ReadOnlyMemory<byte> passphraseUtf8)
    {
        ArgumentNullException.ThrowIfNull(recoveryFile);
        byte[] credential = passphraseUtf8.ToArray();
        try
        {
            await RunAsync(captured, needsConsent: true, async token =>
            {
                if (_recoveryReference is not { } trusted) return;
                byte[] container = [];
                try
                {
                    container = await ReadFileAsync(recoveryFile, token);
                    using var recovery = new ProfileWorkerClient(
                        _javascript as IJSInProcessRuntime ?? throw new ProfileException(ProfileFailureCode.ModeRestricted),
                        _captureAuthority);
                    _recoveryWorker = recovery;
                    ProfileWorkerReply result = await recovery.VerifyRecoveryAsync(trusted, container,
                        _recoveryIndex, credential, token);
                    if (IsCurrent(captured))
                    {
                        _status = result.Status == "verified-for-snapshot"
                            ? "ProfileRecoveryVerified"
                            : "ProfileRejected";
                    }
                }
                finally
                {
                    _recoveryWorker = null;
                    CryptographicOperations.ZeroMemory(container);
                    ++_fileKey;
                }
            });
        }
        finally
        {
            CryptographicOperations.ZeroMemory(credential);
        }
    }

    public Task CompareAsync(ProfileWorkerAuthority captured, IBrowserFile comparisonFile)
    {
        ArgumentNullException.ThrowIfNull(comparisonFile);
        return RunAsync(captured, needsConsent: false, async token =>
        {
            byte[] bytes = await ReadFileAsync(comparisonFile, token);
            try
            {
                CompareBaselineSnapshot baseline = CompareBaselineCodec.ParseJson(bytes);
                _registry.AdmitHistoricalIdentity(baseline.FamilyId, baseline.DefinitionRevision, baseline.DefinitionDigest);
                Definition? source = _registry.Current.Definitions.FirstOrDefault(definition =>
                    definition.FamilyId == baseline.FamilyId && definition.Revision == baseline.DefinitionRevision &&
                    definition.Digest == baseline.DefinitionDigest);
                CompareBaselineSnapshot current = CompareBaselineCodec.Create(Form.Definition, Form.Snapshot(),
                    baseline.ProfileId, Guid.NewGuid(), "browser-workspace");
                ProfileUpgradePlan comparison = ProfileUpgradePlanner.Compare(baseline, current, source,
                    Form.Definition, "browser-workspace");
                if (!IsCurrent(captured)) return;
                _fields = comparison.Fields.Select(field =>
                    new ProfileWorkerField(field.FieldPath, field.Eligibility.ToString(), field.Comparison.ToString())).ToArray();
                _status = "ProfileComparisonReady";
            }
            finally { CryptographicOperations.ZeroMemory(bytes); }
        });
    }

    public Task SaveComparisonAsync(ProfileWorkerAuthority captured) => RunAsync(captured, needsConsent: false, async token =>
    {
        token.ThrowIfCancellationRequested();
        CompareBaselineSnapshot baseline = CompareBaselineCodec.Create(Form.Definition, Form.Snapshot(),
            Guid.NewGuid(), Guid.NewGuid(), "browser-workspace");
        byte[] bytes = CompareBaselineCodec.Serialize(baseline);
        try
        {
            await using var export = new BrowserArtifactExport(_javascript);
            _comparisonExport = export;
            export.SetOutput(bytes);
            bool downloaded = await export.DownloadAsync("configuration.cfgcompare.json", "application/json");
            if (IsCurrent(captured)) _status = downloaded ? "ProfileComparisonDownloaded" : "ProfileRejected";
        }
        finally
        {
            _comparisonExport = null;
            CryptographicOperations.ZeroMemory(bytes);
        }
    });

    /// <summary>
    /// Synchronously unsubscribes form listeners, cancels in-flight work, closes adopted profile
    /// custody while the form is still alive, and retires all owned workers.
    /// </summary>
    public void RetireSynchronously()
    {
        if (_retired) return;
        _retired = true;
        Form.InputChanged -= OnFormInputChanged;
        _operation?.Cancel();
        Form.CloseBrowserProfile();
        _worker?.Dispose();
        _worker = null;
        _recoveryWorker?.Dispose();
        _recoveryWorker = null;
        _fields = [];
        _recoveryReference = null;
        _replacement = null;
        _inspectConfirmed = false;
        ++_fileKey;
        NotifyCredentialsCleared();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        RetireSynchronously();
        if (_comparisonExport is not null)
        {
            await _comparisonExport.DisposeAsync();
            _comparisonExport = null;
        }
        GC.SuppressFinalize(this);
    }

    private ProfileWorkerClient Worker => _worker ??= new(
        _javascript as IJSInProcessRuntime ?? throw new ProfileException(ProfileFailureCode.ModeRestricted),
        _captureAuthority);

    private void OnFormInputChanged(object? sender, EventArgs args)
    {
        if (_disposed || _retired) return;
        bool consentRevoked = !Consented;
        bool hadState = _fields.Length > 0
            || _replacement is not null
            || _inspectConfirmed
            || (_recoveryReference is not null && consentRevoked)
            || _worker is not null
            || _recoveryWorker is not null
            || _comparisonExport is not null
            || IsBusy;

        DiscardWorkerOutput();
        if (_comparisonExport is not null) _ = _comparisonExport.InvalidateAsync().AsTask();
        if (_adopting) return;

        _fields = [];
        _replacement = null;
        _inspectConfirmed = false;
        string? previousArtifact = _artifactId;
        SyncEligibleArtifacts();
        bool artifactChanged = !string.Equals(previousArtifact, _artifactId, StringComparison.Ordinal);
        if (consentRevoked)
            _recoveryReference = null;

        NotifyCredentialsCleared();
        if (IsBusy)
        {
            _operation?.Cancel();
            Form.CloseBrowserProfile();
            RetireWorkers();
            _status = "ProfileCancelled";
        }
        else if (consentRevoked)
        {
            RetireWorkers();
        }

        if (hadState || artifactChanged || consentRevoked)
            NotifyChanged();
    }

    private void RetireWorkers()
    {
        try { _worker?.Retire(); }
        catch (JSException) { _code = "profile-worker-retirement-failed"; }
        try { _recoveryWorker?.Retire(); }
        catch (JSException) { _code = "profile-worker-retirement-failed"; }
        _fields = [];
        _replacement = null;
        _inspectConfirmed = false;
        ++_fileKey;
        NotifyCredentialsCleared();
    }

    private void DiscardWorkerOutput()
    {
        try { _worker?.DiscardOutput(); }
        catch (JSException)
        {
            // The client changes its channel before entering JS. A failed host cleanup must
            // not interrupt the form's synchronous revision or consent retirement.
            RetireWorkers();
            _code = "profile-worker-retirement-failed";
        }
    }

    private async Task RunAsync(
        ProfileWorkerAuthority captured,
        bool needsConsent,
        Func<CancellationToken, Task> action)
    {
        if (!IsCurrent(captured) || IsBusy || needsConsent && !captured.ConsentGranted) return;
        using var operation = new CancellationTokenSource();
        _operation = operation;
        _status = "ProfileWorking";
        _code = null;
        NotifyChanged();
        try
        {
            await action(operation.Token);
        }
        catch (OperationCanceledException) when (operation.IsCancellationRequested)
        {
            if (!_disposed && !_retired) _status = "ProfileCancelled";
        }
        catch (Exception exception) when (exception is ProfileException or ProfileRecoveryException or
            DefinitionException or IOException or JSException or JsonException or ArgumentException)
        {
            if (!_disposed && !_retired)
            {
                _status = "ProfileRejected";
                _code = exception switch
                {
                    ProfileException profile => profile.Code.ToString(),
                    ProfileRecoveryException recovery => recovery.Failure.ToString(),
                    DefinitionException definition => definition.Code,
                    _ => "profile-input-rejected"
                };
            }
        }
        finally
        {
            if (ReferenceEquals(_operation, operation)) _operation = null;
            NotifyCredentialsCleared();
            if (!_disposed && !_retired) NotifyChanged();
        }
    }

    private static async Task<byte[]> ReadFileAsync(IBrowserFile file, CancellationToken token)
    {
        if (file.Size is <= 0 or > ProfileContainerPolicy.MaximumContainerBytes)
            throw new ProfileException(ProfileFailureCode.LimitExceeded);
        byte[] bytes = new byte[checked((int)file.Size)];
        try
        {
            await using Stream stream = file.OpenReadStream(ProfileContainerPolicy.MaximumContainerBytes, token);
            await stream.ReadExactlyAsync(bytes, token);
            return bytes;
        }
        catch
        {
            CryptographicOperations.ZeroMemory(bytes);
            throw;
        }
    }

    private void NotifyChanged() => Changed?.Invoke();

    private void NotifyCredentialsCleared() => CredentialsCleared?.Invoke();
}
