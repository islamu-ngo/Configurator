namespace Configurator.Browser.Services.Docking;

using Microsoft.AspNetCore.Components;

public interface IDockPanelRegistry
{
    void Register(DockPanelDescriptor descriptor, RenderFragment content);

    void Unregister(DockPanelId id);

    IReadOnlyList<DockPanelEntry> GetPanels(DockScope scope, DockSide side);
}
