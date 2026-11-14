namespace Configurator.Browser.Services;

using MudBlazor;

/// <summary>Standard MudBlazor dialog configuration presets for workspace modals and palettes.</summary>
public static class DialogOptionsFactory
{
    public static DialogOptions Standard(bool closeOnEscape = true) => new()
    {
        CloseOnEscapeKey = closeOnEscape,
        MaxWidth = MaxWidth.Small,
        FullWidth = true
    };

    public static DialogOptions Large(bool closeOnEscape = true) => new()
    {
        CloseOnEscapeKey = closeOnEscape,
        MaxWidth = MaxWidth.Medium,
        FullWidth = true
    };
}
