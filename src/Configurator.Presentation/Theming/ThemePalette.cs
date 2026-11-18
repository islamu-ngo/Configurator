namespace Configurator.Presentation.Theming;

/// <summary>Immutable semantic color palette consumed identically by Browser and Desktop theme adapters.</summary>
public sealed record ThemePalette
{
    public required string Primary { get; init; }
    public required string PrimaryContrastText { get; init; }
    public required string Secondary { get; init; }
    public required string SecondaryContrastText { get; init; }
    public required string Background { get; init; }
    public required string Surface { get; init; }
    public required string AppbarBackground { get; init; }
    public required string AppbarText { get; init; }
    public required string DrawerBackground { get; init; }
    public required string DrawerText { get; init; }
    public required string DrawerIcon { get; init; }
    public required string TextPrimary { get; init; }
    public required string TextSecondary { get; init; }
    public required string Info { get; init; }
    public required string Success { get; init; }
    public required string Warning { get; init; }
    public required string Error { get; init; }
    public required string LinesDefault { get; init; }
    public required string Divider { get; init; }
}
