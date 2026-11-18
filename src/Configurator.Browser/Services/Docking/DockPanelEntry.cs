namespace Configurator.Browser.Services.Docking;

using Microsoft.AspNetCore.Components;

public sealed record DockPanelEntry(
    DockPanelDescriptor Descriptor,
    RenderFragment Content,
    DockPanelState State);
