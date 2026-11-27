namespace Configurator.Browser.Services.Docking;

using System.Collections.Immutable;
using Configurator.Browser.Resources;
using MudBlazor;

/// <summary>Canonical non-form utility panels hosted inside the single-active Workspace End Dock.</summary>
public enum WorkspaceUtilityKind
{
    Inspector,
    DocumentJson,
    Import,
    Profiles,
    TrustPrivacy
}

/// <summary>
/// Immutable metadata descriptor defining one Workspace End-Dock utility across docking,
/// the desktop utility rail, the narrow header Tools menu, and editor coordination.
/// </summary>
public sealed record WorkspaceUtilityDescriptor(
    WorkspaceUtilityKind Kind,
    DockPanelId PanelId,
    string ActionToken,
    string TitleResourceKey,
    string AriaLabelResourceKey,
    string Icon,
    int Order,
    int ResponsivePriority,
    bool RequiresImportArtifacts)
{
    public const int DefaultPanelWidth = 360;
    public const int MinimumPanelWidth = 300;
    public const int MaximumPanelWidth = 620;

    public string GetTitle(string locale) => BrowserStrings.Get(TitleResourceKey, locale);

    public string GetAriaLabel(string locale) => BrowserStrings.Get(AriaLabelResourceKey, locale);

    public DockPanelDescriptor ToDockPanelDescriptor(string title, string ariaLabel) =>
        new DockPanelDescriptor(
            PanelId,
            DockScope.Workspace,
            DockSide.End,
            DockMode.Docked,
            Title: title,
            AriaLabel: ariaLabel,
            DefaultWidth: DefaultPanelWidth,
            MinWidth: MinimumPanelWidth,
            MaxWidth: MaximumPanelWidth,
            Order: Order,
            IsResizable: true,
            CanClose: true,
            PersistState: false,
            StackStrategy: DockPanelStackStrategy.Tabbed,
            ResponsivePriority: ResponsivePriority).Validate();

    public DockPanelDescriptor ToDockPanelDescriptor(string locale) =>
        ToDockPanelDescriptor(GetTitle(locale), GetAriaLabel(locale));
}

/// <summary>Single canonical registry of all Browser workspace utilities.</summary>
public static class WorkspaceUtilities
{
    public static WorkspaceUtilityDescriptor Inspector { get; } = new(
        WorkspaceUtilityKind.Inspector,
        new DockPanelId("workspace.inspector"),
        "inspector",
        "DockTabInspector",
        "InspectorPaneLabel",
        Icons.Material.Outlined.Info,
        Order: 10,
        ResponsivePriority: 20,
        RequiresImportArtifacts: false);

    public static WorkspaceUtilityDescriptor DocumentJson { get; } = new(
        WorkspaceUtilityKind.DocumentJson,
        new DockPanelId("workspace.document-json"),
        "document",
        "DockTabDocumentJson",
        "DocumentJsonDockLabel",
        Icons.Material.Outlined.Code,
        Order: 20,
        ResponsivePriority: 30,
        RequiresImportArtifacts: false);

    public static WorkspaceUtilityDescriptor Import { get; } = new(
        WorkspaceUtilityKind.Import,
        new DockPanelId("workspace.import"),
        "import",
        "UtilityImportTitle",
        "UtilityImportAriaLabel",
        Icons.Material.Outlined.UploadFile,
        Order: 30,
        ResponsivePriority: 20,
        RequiresImportArtifacts: true);

    public static WorkspaceUtilityDescriptor Profiles { get; } = new(
        WorkspaceUtilityKind.Profiles,
        new DockPanelId("workspace.profiles"),
        "profiles",
        "UtilityProfilesTitle",
        "UtilityProfilesAriaLabel",
        Icons.Material.Outlined.Key,
        Order: 40,
        ResponsivePriority: 20,
        RequiresImportArtifacts: false);

    public static WorkspaceUtilityDescriptor TrustPrivacy { get; } = new(
        WorkspaceUtilityKind.TrustPrivacy,
        new DockPanelId("workspace.trust-privacy"),
        "trust-privacy",
        "UtilityTrustPrivacyTitle",
        "UtilityTrustPrivacyAriaLabel",
        Icons.Material.Outlined.Shield,
        Order: 50,
        ResponsivePriority: 20,
        RequiresImportArtifacts: false);

    public static ImmutableArray<WorkspaceUtilityDescriptor> All { get; } =
    [
        Inspector,
        DocumentJson,
        Import,
        Profiles,
        TrustPrivacy
    ];

    public static WorkspaceUtilityDescriptor Get(WorkspaceUtilityKind kind) => kind switch
    {
        WorkspaceUtilityKind.Inspector => Inspector,
        WorkspaceUtilityKind.DocumentJson => DocumentJson,
        WorkspaceUtilityKind.Import => Import,
        WorkspaceUtilityKind.Profiles => Profiles,
        WorkspaceUtilityKind.TrustPrivacy => TrustPrivacy,
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    public static bool TryGetByPanelId(DockPanelId panelId, out WorkspaceUtilityDescriptor? descriptor)
    {
        ArgumentNullException.ThrowIfNull(panelId);
        foreach (WorkspaceUtilityDescriptor item in All)
        {
            if (item.PanelId == panelId)
            {
                descriptor = item;
                return true;
            }
        }

        descriptor = null;
        return false;
    }

    public static bool TryGetByActionToken(string? actionToken, out WorkspaceUtilityDescriptor? descriptor)
    {
        if (!string.IsNullOrWhiteSpace(actionToken))
        {
            foreach (WorkspaceUtilityDescriptor item in All)
            {
                if (string.Equals(item.ActionToken, actionToken, StringComparison.Ordinal))
                {
                    descriptor = item;
                    return true;
                }
            }
        }

        descriptor = null;
        return false;
    }
}
