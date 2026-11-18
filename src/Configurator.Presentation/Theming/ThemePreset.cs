namespace Configurator.Presentation.Theming;

/// <summary>Immutable curated theme preset pairing light, dark, and high-contrast palettes.</summary>
public sealed record ThemePreset
{
    public required string ThemeKey { get; init; }
    public required string DisplayName { get; init; }
    public required string Description { get; init; }
    public required ThemePalette LightPalette { get; init; }
    public required ThemePalette DarkPalette { get; init; }
    public required ThemePalette LightHighContrastPalette { get; init; }
    public required ThemePalette DarkHighContrastPalette { get; init; }
}
