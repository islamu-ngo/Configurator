namespace Configurator.Desktop.Resources;

using Avalonia;
using Configurator.Presentation;

/// <summary>Maps shared <see cref="WorkspaceDensity"/> steps to Avalonia spacing/target resources without altering text scaling.</summary>
public static class DesktopDensity
{
    public sealed record Metrics(
        double ControlMinHeight,
        double SectionSpacing,
        Thickness CardPadding,
        double TableRowHeight);

    public static Metrics For(WorkspaceDensity density) => density switch
    {
        WorkspaceDensity.Spacious => new Metrics(44, 20, new Thickness(16), 48),
        WorkspaceDensity.Comfortable => new Metrics(36, 14, new Thickness(12), 40),
        WorkspaceDensity.Compact => new Metrics(32, 10, new Thickness(8), 34),
        WorkspaceDensity.Dense => new Metrics(26, 6, new Thickness(6), 28),
        _ => throw new ArgumentOutOfRangeException(nameof(density))
    };

    public static void Apply(Application? application, WorkspaceDensity density)
    {
        if (application is null) return;
        Metrics metrics = For(density);
        application.Resources["ConfiguratorControlMinHeight"] = metrics.ControlMinHeight;
        application.Resources["ConfiguratorSpacing"] = metrics.SectionSpacing;
        application.Resources["ConfiguratorCardPadding"] = metrics.CardPadding;
        application.Resources["ConfiguratorTableRowHeight"] = metrics.TableRowHeight;
    }
}
