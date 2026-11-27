namespace Configurator.Browser.Components.Configuration;

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Configurator.Browser.Profiles;
using Configurator.Browser.Resources;
using Configurator.Core.Profiles;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

/// <summary>
/// Stateless view over <see cref="BrowserProfileVaultController"/>. Keeps typed passphrase drafts
/// view-local and clears them synchronously before awaiting controller work, on credential clearing,
/// and on component disposal.
/// </summary>
public partial class ConfigurationVaultPanel : ComponentBase, IDisposable
{
    [Parameter, EditorRequired] public BrowserProfileVaultController Controller { get; set; } = null!;
    [Parameter] public EventCallback RequestConsent { get; set; }
    [Parameter] public EventCallback CloseRequested { get; set; }

    private BrowserProfileVaultController? _subscribed;
    private IBrowserFile? _vaultFile;
    private IBrowserFile? _recoveryFile;
    private string _passphrase = string.Empty;
    private string _newPassphrase = string.Empty;
    private string _confirmation = string.Empty;
    private string _backupPassphrase = string.Empty;
    private string _backupConfirmation = string.Empty;
    private string _recoveryPassphrase = string.Empty;
    private readonly HashSet<string> _visiblePassphrases = new(StringComparer.Ordinal);
    private ElementReference _heading;
    private long _observedHeadingFocusGeneration;
    private long _observedFileKey;
    private bool _disposed;

    private string Text(string key) => BrowserStrings.Get(key, Controller.Form.Locale);

    private string Format(string key, params object?[] arguments) =>
        string.Format(CultureInfo.GetCultureInfo(Controller.Form.Locale), Text(key), arguments);

    private bool IsPassphraseVisible(string field) =>
        Controller.Consented && !Controller.IsBusy && _visiblePassphrases.Contains(field);

    private void TogglePassphraseVisibility(string field)
    {
        if (!Controller.Consented || Controller.IsBusy) return;
        if (!_visiblePassphrases.Add(field))
            _visiblePassphrases.Remove(field);
    }

    protected override void OnParametersSet()
    {
        if (!ReferenceEquals(_subscribed, Controller))
        {
            Unsubscribe();
            _subscribed = Controller;
            Controller.Changed += OnControllerChanged;
            Controller.CredentialsCleared += OnCredentialsCleared;
            _observedHeadingFocusGeneration = Controller.HeadingFocusGeneration;
            _observedFileKey = Controller.FileKey;
            ClearCredentials();
            _vaultFile = null;
            _recoveryFile = null;
        }

        Controller.SyncEligibleArtifacts();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_disposed && Controller.HeadingFocusGeneration != _observedHeadingFocusGeneration)
        {
            _observedHeadingFocusGeneration = Controller.HeadingFocusGeneration;
            if (!string.IsNullOrEmpty(_heading.Id))
                await _heading.FocusAsync(preventScroll: true);
        }
    }

    private void OnControllerChanged()
    {
        if (_disposed) return;
        SyncFileKey();
        _ = InvokeAsync(StateHasChanged);
    }

    private void OnCredentialsCleared()
    {
        if (_disposed) return;
        bool hadState = HasCredentialOrVisibilityState() || Controller.FileKey != _observedFileKey;
        ClearCredentials();
        SyncFileKey();
        if (hadState)
            _ = InvokeAsync(StateHasChanged);
    }

    private bool HasCredentialOrVisibilityState() =>
        _visiblePassphrases.Count > 0
        || _passphrase.Length > 0
        || _newPassphrase.Length > 0
        || _confirmation.Length > 0
        || _backupPassphrase.Length > 0
        || _backupConfirmation.Length > 0
        || _recoveryPassphrase.Length > 0;

    private void SyncFileKey()
    {
        if (Controller.FileKey == _observedFileKey) return;
        _observedFileKey = Controller.FileKey;
        _vaultFile = null;
        _recoveryFile = null;
    }

    private async Task OpenAsync(ProfileWorkerAuthority captured)
    {
        if (!Controller.IsCurrent(captured) || Controller.IsBusy || !Controller.Consented ||
            _vaultFile is not { } vaultFile || Controller.Form.BrowserProfile is not null)
            return;

        byte[] credential;
        try
        {
            credential = EncodeCredential(_passphrase);
        }
        catch (ProfileException exception)
        {
            ClearCredentials();
            Controller.ReportRejected(captured, exception.Code);
            return;
        }

        ClearCredentials();
        StateHasChanged();
        try
        {
            await Controller.OpenAsync(captured, vaultFile, credential);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(credential);
        }
    }

    private async Task SaveAsync(ProfileWorkerAuthority captured)
    {
        if (!Controller.IsCurrent(captured) || Controller.IsBusy || !Controller.Consented ||
            Controller.Form.HasInputErrors || Controller.Form.HasAnswerBudgetError ||
            Controller.Form.RequiresBrowserProfileComposition && Controller.Form.BrowserProfile is null)
            return;

        var materials = new List<byte[]>(Controller.AddRecovery ? 2 : 1);
        var confirmations = new List<byte[]>(Controller.AddRecovery ? 2 : 1);
        try
        {
            try
            {
                materials.Add(EncodeCredential(_newPassphrase));
                confirmations.Add(EncodeCredential(_confirmation));
                if (Controller.AddRecovery)
                {
                    materials.Add(EncodeCredential(_backupPassphrase));
                    confirmations.Add(EncodeCredential(_backupConfirmation));
                }
            }
            catch (ProfileException exception)
            {
                ClearCredentials();
                Controller.ReportRejected(captured, exception.Code);
                return;
            }

            ClearCredentials();
            StateHasChanged();

            ReadOnlyMemory<byte>[] recipients = materials.Select(bytes => (ReadOnlyMemory<byte>)bytes).ToArray();
            ReadOnlyMemory<byte>[] confirmed = confirmations.Select(bytes => (ReadOnlyMemory<byte>)bytes).ToArray();
            await Controller.SaveAsync(captured, recipients, confirmed);
        }
        finally
        {
            foreach (byte[] bytes in materials) CryptographicOperations.ZeroMemory(bytes);
            foreach (byte[] bytes in confirmations) CryptographicOperations.ZeroMemory(bytes);
        }
    }

    private async Task RecoverAsync(ProfileWorkerAuthority captured)
    {
        if (!Controller.IsCurrent(captured) || Controller.IsBusy || !Controller.Consented ||
            Controller.RecoveryReference is null || _recoveryFile is not { } recoveryFile)
            return;

        byte[] credential;
        try
        {
            credential = EncodeCredential(_recoveryPassphrase);
        }
        catch (ProfileException exception)
        {
            ClearCredentials();
            Controller.ReportRejected(captured, exception.Code);
            return;
        }

        ClearCredentials();
        StateHasChanged();
        try
        {
            await Controller.RecoverAsync(captured, recoveryFile, credential);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(credential);
        }
    }

    private static byte[] EncodeCredential(string text)
    {
        var encoding = new UTF8Encoding(false, true);
        if (encoding.GetByteCount(text) > ProfileContainerPolicy.MaximumPassphraseUtf8Bytes)
            throw new ProfileException(ProfileFailureCode.InvalidPassphrase);
        return encoding.GetBytes(text);
    }

    private void ClearCredentials()
    {
        _passphrase = _newPassphrase = _confirmation = _backupPassphrase = _backupConfirmation = _recoveryPassphrase = string.Empty;
        _visiblePassphrases.Clear();
    }

    private void SetCredential(ProfileWorkerAuthority captured, string field, ChangeEventArgs args)
    {
        if (!Controller.IsCurrent(captured) || Controller.IsBusy || !Controller.Consented) return;
        string value = args.Value?.ToString() ?? string.Empty;
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

    private void SetMode(ProfileWorkerAuthority captured, ChangeEventArgs args)
    {
        if (args.Value is nameof(ProfileProtectionMode.ImportUpgradeOnly))
            Controller.SetMode(captured, ProfileProtectionMode.ImportUpgradeOnly);
        else if (args.Value is nameof(ProfileProtectionMode.InspectableEncrypted))
            Controller.SetMode(captured, ProfileProtectionMode.InspectableEncrypted);
    }

    private void Unsubscribe()
    {
        if (_subscribed is null) return;
        _subscribed.Changed -= OnControllerChanged;
        _subscribed.CredentialsCleared -= OnCredentialsCleared;
        _subscribed = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Unsubscribe();
        ClearCredentials();
        _vaultFile = null;
        _recoveryFile = null;
        GC.SuppressFinalize(this);
    }
}
