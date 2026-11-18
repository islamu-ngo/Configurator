namespace Configurator.Browser.Services.Docking;

/// <summary>Canonical Configurator dock descriptors aligned with ISLAMU Event rail and right-dock defaults.</summary>
public static class BrowserDockPanels
{
    public static readonly DockPanelId StartNavId = new("shell.start-nav");
    public static readonly DockPanelId InspectorId = new("workspace.inspector");
    public static readonly DockPanelId DocumentJsonId = new("workspace.document-json");

    public static DockPanelDescriptor StartNav { get; } = new DockPanelDescriptor(
        StartNavId,
        DockScope.Shell,
        DockSide.Start,
        DockMode.Docked,
        Title: "Navigation",
        AriaLabel: "Application navigation",
        DefaultWidth: 256,
        MinWidth: 240,
        MaxWidth: 460,
        Order: 10,
        IsResizable: true,
        CanClose: true,
        PersistState: true,
        StackStrategy: DockPanelStackStrategy.Tabbed,
        ResponsivePriority: 10).Validate();

    public static DockPanelDescriptor Inspector { get; } = CreateInspector("Inspector (F2)", "Field inspector");

    public static DockPanelDescriptor DocumentJson { get; } = CreateDocumentJson("Document (JSON)", "Document JSON editor");

    public static DockPanelDescriptor CreateInspector(string title, string ariaLabel) =>
        new DockPanelDescriptor(
            InspectorId,
            DockScope.Workspace,
            DockSide.End,
            DockMode.Docked,
            Title: title,
            AriaLabel: ariaLabel,
            DefaultWidth: 360,
            MinWidth: 300,
            MaxWidth: 620,
            Order: 10,
            IsResizable: true,
            CanClose: true,
            PersistState: true,
            StackStrategy: DockPanelStackStrategy.Tabbed,
            ResponsivePriority: 20).Validate();

    public static DockPanelDescriptor CreateDocumentJson(string title, string ariaLabel) =>
        new DockPanelDescriptor(
            DocumentJsonId,
            DockScope.Workspace,
            DockSide.End,
            DockMode.Docked,
            Title: title,
            AriaLabel: ariaLabel,
            DefaultWidth: 360,
            MinWidth: 300,
            MaxWidth: 620,
            Order: 20,
            IsResizable: true,
            CanClose: true,
            PersistState: true,
            StackStrategy: DockPanelStackStrategy.Tabbed,
            ResponsivePriority: 30).Validate();
}
