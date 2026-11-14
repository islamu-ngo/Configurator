namespace Configurator.Browser.Components.Configuration;

using Configurator.Browser.Profiles;
using Configurator.Presentation;

public partial class ConfigurationEditor
{
    private long _profileConsentEpoch;

    private ProfileWorkerAuthority CaptureProfileAuthority() => new(
        Guid.ParseExact(_workspaceLifetime, "N"), _generation, _form?.InputRevision ?? 0,
        _profileConsentEpoch, !_disposed && _ready &&
            _form?.SessionPrivacy == DefinitionSessionPrivacy.BrowserSecretsEnabled);
}
