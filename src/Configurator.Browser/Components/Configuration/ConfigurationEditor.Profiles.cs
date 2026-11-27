namespace Configurator.Browser.Components.Configuration;

using Configurator.Browser.Profiles;
using Configurator.Presentation;

public partial class ConfigurationEditor
{
    private long _profileConsentEpoch;
    private ConfigurationVaultPanel? _vaultPanel;
    private BrowserProfileVaultController? _vaultController;
    private bool _profileRenderPending;

    private ProfileWorkerAuthority CaptureProfileAuthority() => new(
        Guid.ParseExact(_workspaceLifetime, "N"), _generation, _form?.InputRevision ?? 0,
        _profileConsentEpoch, !_disposed && _ready &&
            _form?.SessionPrivacy == DefinitionSessionPrivacy.BrowserSecretsEnabled);

    private BrowserProfileVaultController CreateProfileVaultController(DefinitionFormModel form)
    {
        var controller = new BrowserProfileVaultController(
            form,
            Javascript,
            Registry,
            CaptureProfileAuthority,
            () => !_disposed && _ready && ReferenceEquals(_form, form));
        controller.Changed += OnProfileVaultChanged;
        return controller;
    }

    private void RetireProfileVault()
    {
        BrowserProfileVaultController? controller = _vaultController;
        _vaultController = null;
        if (controller is null) return;
        controller.Changed -= OnProfileVaultChanged;
        controller.RetireSynchronously();
        _outputRetirement = Task.WhenAll(_outputRetirement, ObserveRetirementAsync([controller.DisposeAsync().AsTask()]));
        _navigation?.RefreshWork(this, _navigationLease);
    }

    private void OnProfileVaultChanged()
    {
        if (_disposed) return;
        _navigation?.RefreshWork(this, _navigationLease);
        if (_profileRenderPending) return;
        _profileRenderPending = true;
        _ = InvokeAsync(() =>
        {
            _profileRenderPending = false;
            if (!_disposed) StateHasChanged();
        });
    }
}
