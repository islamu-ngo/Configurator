namespace Configurator.Browser.Services.Docking;

public static class DockElementIds
{
    public static string PanelBodyId(DockPanelId panelId)
    {
        ArgumentNullException.ThrowIfNull(panelId);
        return $"dock-panel-body-{Sanitize(panelId.Value)}";
    }

    public static string TabId(DockPanelId panelId)
    {
        ArgumentNullException.ThrowIfNull(panelId);
        return $"dock-panel-tab-{Sanitize(panelId.Value)}";
    }

    private static string Sanitize(string value)
    {
        string sanitized = string.Concat(value.Select(character => char.IsLetterOrDigit(character)
            ? char.ToLowerInvariant(character)
            : '-')).Trim('-');

        return string.IsNullOrWhiteSpace(sanitized) ? "panel" : sanitized;
    }
}
