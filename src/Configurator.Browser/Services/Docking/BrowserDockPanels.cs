namespace Configurator.Browser.Services.Docking;

/// <summary>Canonical Configurator dock descriptors aligned with ISLAMU Event rail and right-dock defaults.</summary>
public static class BrowserDockPanels
{
    public static readonly DockPanelId StartNavId = new("shell.start-nav");
    public static readonly DockPanelId InspectorId = WorkspaceUtilities.Inspector.PanelId;
    public static readonly DockPanelId DocumentJsonId = WorkspaceUtilities.DocumentJson.PanelId;
    public static readonly DockPanelId ImportId = WorkspaceUtilities.Import.PanelId;
    public static readonly DockPanelId ProfilesId = WorkspaceUtilities.Profiles.PanelId;
    public static readonly DockPanelId TrustPrivacyId = WorkspaceUtilities.TrustPrivacy.PanelId;

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

    public static DockPanelDescriptor Inspector { get; } = CreateWorkspaceUtility(WorkspaceUtilityKind.Inspector, "Inspector (F2)", "Field inspector");

    public static DockPanelDescriptor DocumentJson { get; } = CreateWorkspaceUtility(WorkspaceUtilityKind.DocumentJson, "Document (JSON)", "Document JSON editor");

    public static DockPanelDescriptor Import { get; } = CreateWorkspaceUtility(WorkspaceUtilityKind.Import, "Import", "Import existing configuration");

    public static DockPanelDescriptor Profiles { get; } = CreateWorkspaceUtility(WorkspaceUtilityKind.Profiles, "Profiles", "Portable saved profiles");

    public static DockPanelDescriptor TrustPrivacy { get; } = CreateWorkspaceUtility(WorkspaceUtilityKind.TrustPrivacy, "Trust & Privacy", "Workspace secret consent and privacy limits");

    public static DockPanelDescriptor CreateWorkspaceUtility(WorkspaceUtilityKind kind, string title, string ariaLabel) =>
        WorkspaceUtilities.Get(kind).ToDockPanelDescriptor(title, ariaLabel);

    public static DockPanelDescriptor CreateWorkspaceUtility(WorkspaceUtilityKind kind, string locale) =>
        WorkspaceUtilities.Get(kind).ToDockPanelDescriptor(locale);

    public static DockPanelDescriptor CreateInspector(string title, string ariaLabel) =>
        CreateWorkspaceUtility(WorkspaceUtilityKind.Inspector, title, ariaLabel);

    public static DockPanelDescriptor CreateDocumentJson(string title, string ariaLabel) =>
        CreateWorkspaceUtility(WorkspaceUtilityKind.DocumentJson, title, ariaLabel);
}
