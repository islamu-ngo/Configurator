namespace Configurator.Browser.Models;

/// <summary>Reports worker-confirmed complete app preload separately from installed offline capability and waiting updates.</summary>
public sealed record BrowserOfflineState(bool AppPrepared, bool OfflineReady, bool Online, bool UpdateWaiting = false);
