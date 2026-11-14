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

/// <summary>Active workspace primary canvas layout within the connected shell.</summary>
public enum WorkspaceLayoutMode
{
    Guided,
    Compact,
    Review,
    Files
}

/// <summary>View-only presentation preferences; updates never mutate accepted answers or advance InputRevision.</summary>
public sealed record WorkspacePresentationOptions(
    WorkspaceDensity Density = WorkspaceDensity.Comfortable,
    WorkspaceNamingMode NamingMode = WorkspaceNamingMode.Humanized,
    WorkspaceLayoutMode LayoutMode = WorkspaceLayoutMode.Guided);
