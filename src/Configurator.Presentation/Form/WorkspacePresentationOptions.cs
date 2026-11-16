namespace Configurator.Presentation;

/// <summary>Grid and row spacing presets targeting WCAG 2.2 AA target minimums (&gt;= 24px Dense, &gt;= 44px Comfortable/Spacious).</summary>
public enum WorkspaceDensity
{
    Spacious,
    Comfortable,
    Compact,
    Dense
}

/// <summary>Controls whether field and option labels prioritize humanized guidance titles or exact schema wire tokens.</summary>
public enum WorkspaceNamingMode
{
    Humanized,
    Technical
}

/// <summary>Primary top-level workflow stage inside the workspace.</summary>
public enum WorkspaceStage
{
    Configure,
    Review,
    Files
}

/// <summary>Viewing layout used while configuring settings.</summary>
public enum ConfigureLayoutMode
{
    SectionBySection,
    AllSections
}

/// <summary>View-only presentation preferences; updates never mutate accepted answers or advance InputRevision.</summary>
public sealed record WorkspacePresentationOptions(
    WorkspaceDensity Density = WorkspaceDensity.Comfortable,
    WorkspaceNamingMode NamingMode = WorkspaceNamingMode.Humanized,
    ConfigureLayoutMode LayoutMode = ConfigureLayoutMode.SectionBySection,
    WorkspaceStage Stage = WorkspaceStage.Configure);
