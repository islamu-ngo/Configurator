namespace Configurator.Browser.Components.Configuration;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Configurator.Browser.Profiles;
using Configurator.Browser.Resources;
using Configurator.Browser.Services;
using Configurator.Core.Definitions;
using Configurator.Core.Profiles;
using Configurator.Presentation;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;

public partial class ConfigurationVaultPanel : ComponentBase, IAsyncDisposable
{
    [Inject] private IJSRuntime Javascript { get; set; } = null!;
    [Inject] private BrowserDefinitionRegistry Registry { get; set; } = null!;
    [Parameter, EditorRequired] public DefinitionFormModel Form { get; set; } = null!;
    [Parameter, EditorRequired] public Func<ProfileWorkerAuthority> CaptureAuthority { get; set; } = null!;
    [Parameter] public bool Ready { get; set; }
    [Parameter] public EventCallback RequestConsent { get; set; }

    private DefinitionFormModel? _subscribed;
    private ProfileWorkerClient? _worker;
    private ProfileWorkerClient? _recoveryWorker;
    private ProfileWorkerField[] _fields = [];
    private ProfileRecoveryReference? _recoveryReference;
    private BrowserArtifactExport? _comparisonExport;
    private CancellationTokenSource? _operation;
    private IBrowserFile? _vaultFile;
    private IBrowserFile? _recoveryFile;
    private string _passphrase = "";
    private string _newPassphrase = "";
    private string _confirmation = "";
    private string _backupPassphrase = "";
    private string _backupConfirmation = "";
    private string _recoveryPassphrase = "";
    private bool _addRecovery;
    private int _recipientIndex;
    private int _recoveryIndex;
    private ProfileProtectionMode _mode = ProfileProtectionMode.ImportUpgradeOnly;
    private string _artifactId = "";
    private string _status = "ProfileLocalOnly";
    private string? _code;
    private string? _replacement;
    private bool _inspectConfirmed;
    private bool _adopting;
    private bool _disposed;
    private long _fileKey;
    private ElementReference _heading;
    private bool _focusHeading;
    private bool Busy => _operation is not null;
    private bool Consented => Form.SessionPrivacy == DefinitionSessionPrivacy.BrowserSecretsEnabled;
    private string Text(string key) => BrowserStrings.Get(key, Form.Locale);
    private bool Current(ProfileWorkerAuthority captured) =>
        !_disposed && Ready && ReferenceEquals(_subscribed, Form) && captured == CaptureAuthority();

    protected override void OnParametersSet()
    {
        if (!ReferenceEquals(_subscribed, Form))
        {
            if (_subscribed is not null) _subscribed.InputChanged -= FormChanged;
            _subscribed = Form;
            Form.InputChanged += FormChanged;
        }
        if (!Form.EligibleArtifacts.Any(artifact => artifact.Id == _artifactId))
            _artifactId = Form.EligibleArtifacts.Count == 0 ? "" : Form.EligibleArtifacts[0].Id;
    }

    private ProfileWorkerClient Worker => _worker ??= new(
        Javascript as IJSInProcessRuntime ?? throw new ProfileException(ProfileFailureCode.ModeRestricted),
        CaptureAuthority);

    private void FormChanged(object? sender, EventArgs args)
    {
        DiscardWorkerOutput();
        if (_comparisonExport is not null) _ = _comparisonExport.InvalidateAsync().AsTask();
        if (_adopting) return;
        _fields = [];
        _replacement = null;
        _inspectConfirmed = false;
        ClearCredentials();
        if (Busy)
        {
            _operation?.Cancel();
            Form.CloseBrowserProfile();
            RetireWorker();
            _status = "ProfileCancelled";
        }
        _ = InvokeAsync(StateHasChanged);
    }

    private void RetireWorker()
    {
        try { _worker?.Retire(); }
        catch (JSException) { _code = "profile-worker-retirement-failed"; }
        try { _recoveryWorker?.Retire(); }
        catch (JSException) { _code = "profile-worker-retirement-failed"; }
        _fields = [];
        _replacement = null;
        _inspectConfirmed = false;
        ClearCredentials();
        _vaultFile = null;
        _recoveryFile = null;
        ++_fileKey;
    }

    private void DiscardWorkerOutput()
    {
        try { _worker?.DiscardOutput(); }
        catch (JSException)
        {
            // The client changes its channel before entering JS. A failed host cleanup must
            // not interrupt the form's synchronous revision or consent retirement.
            RetireWorker();
            _code = "profile-worker-retirement-failed";
        }
    }

    private void Cancel(ProfileWorkerAuthority captured)
    {
        if (!Current(captured)) return;
        _operation?.Cancel();
        Form.CloseBrowserProfile();
        RetireWorker();
        if (_comparisonExport is not null) _ = _comparisonExport.InvalidateAsync().AsTask();
        _status = "ProfileCancelled";
        _code = null;
    }

    private async Task RunAsync(ProfileWorkerAuthority captured, bool needsConsent,
        Func<CancellationToken, Task> action)
    {
        if (!Current(captured) || Busy || needsConsent && !captured.ConsentGranted) return;
        using var operation = new CancellationTokenSource();
        _operation = operation;
        _status = "ProfileWorking";
        _code = null;
        StateHasChanged();
        try { await action(operation.Token); }
        catch (OperationCanceledException) when (operation.IsCancellationRequested)
        {
            if (!_disposed) _status = "ProfileCancelled";
        }
        catch (Exception exception) when (exception is ProfileException or ProfileRecoveryException or
            DefinitionException or IOException or JSException or JsonException or ArgumentException)
        {
            if (!_disposed)
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
            ClearCredentials();
            if (!_disposed) StateHasChanged();
        }
    }

    private static byte[] Credential(string text)
    {
        var encoding = new UTF8Encoding(false, true);
        if (encoding.GetByteCount(text) > ProfileContainerPolicy.MaximumPassphraseUtf8Bytes)
            throw new ProfileException(ProfileFailureCode.InvalidPassphrase);
        return encoding.GetBytes(text);
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

    private void ClearCredentials()
    {
        _passphrase = _newPassphrase = _confirmation = _backupPassphrase = _backupConfirmation = _recoveryPassphrase = "";
    }

    private void SetCredential(ProfileWorkerAuthority captured, string field, ChangeEventArgs args)
    {
        if (!Current(captured) || Busy || !Consented) return;
        string value = args.Value?.ToString() ?? "";
        if (value.Length > ProfileContainerPolicy.MaximumPassphraseUtf8Bytes) return;
        switch (field)
        {
            case "open": _passphrase = value; break;
            case "new": _newPassphrase = value; break;
            case "confirmation": _confirmation = value; break;
            case "backup": _backupPassphrase = value; break;
            case "backupConfirmation": _backupConfirmation = value; break;
            case "recovery": _recoveryPassphrase = value; break;
        }
    }

    private void SetRecipient(ProfileWorkerAuthority captured, ChangeEventArgs args, bool recovery)
    {
        if (!Current(captured) || Busy || !Consented ||
            !int.TryParse(args.Value?.ToString(), System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out int index) || index is < 0 or > 7) return;
        if (recovery) _recoveryIndex = index;
        else _recipientIndex = index;
    }

    private void SetMode(ProfileWorkerAuthority captured, ChangeEventArgs args)
    {
        if (!Current(captured) || Busy || !Consented) return;
        if (args.Value is nameof(ProfileProtectionMode.ImportUpgradeOnly)) _mode = ProfileProtectionMode.ImportUpgradeOnly;
        else if (args.Value is nameof(ProfileProtectionMode.InspectableEncrypted)) _mode = ProfileProtectionMode.InspectableEncrypted;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_disposed && _focusHeading)
        {
            _focusHeading = false;
            await _heading.FocusAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        if (_subscribed is not null) _subscribed.InputChanged -= FormChanged;
        _operation?.Cancel();
        Form.CloseBrowserProfile();
        _worker?.Dispose();
        _recoveryWorker?.Dispose();
        ClearCredentials();
        _vaultFile = _recoveryFile = null;
        if (_comparisonExport is not null) await _comparisonExport.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
