namespace Configurator.Desktop.Tests;

using System.Globalization;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Messaging;
using Configurator.Artifacts;
using Configurator.Desktop.Resources;
using Configurator.Desktop.Services;
using Configurator.Desktop.ViewModels;
using Configurator.Desktop.Views;

public sealed class DesktopAppearanceTests
{
    private static readonly string[] BackgroundRoles = ["Canvas", "Surface", "Input"];
    private static readonly string[] TextRoles = ["Text", "SecondaryText", "Error", "Success", "Warning"];
    private static readonly string[] BoundaryRoles = ["Border", "Focus"];
    public static Avalonia.AppBuilder BuildAvaloniaApp() => DesktopApplicationTests.BuildAvaloniaApp();

    [Test]
    [NotInParallel]
    [Arguments("en")]
    [Arguments("ar")]
    public async Task IntakeUsesHostProviderLanguageAndDirectionBeforeDefinitionAdmission(string locale)
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(DesktopAppearanceTests));
        try
        {
            var result = await session.Dispatch(() =>
            {
                CultureInfo previous = CultureInfo.CurrentUICulture;
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(locale);
                try
                {
                    using var shell = DesktopTestContext.Root();
                    var window = new MainWindow { DataContext = shell };
                    try
                    {
                        window.Show();
                        window.UpdateLayout();
                        Dispatcher.UIThread.RunJobs();
                        string label = window.GetNamedControl<Button>("OpenStage").Content?.ToString() ?? "";
                        return (arabic: label.Any(character => character is >= '\u0600' and <= '\u06ff'),
                            rtl: window.FlowDirection == FlowDirection.RightToLeft,
                            pathLtr: window.GetNamedControl<TextBox>("DefinitionPath").FlowDirection == FlowDirection.LeftToRight,
                            pinArabic: shell.Loader.DefinitionPin.Any(character => character is >= '\u0600' and <= '\u06ff'),
                            culture: CultureInfo.CurrentUICulture.Name);
                    }
                    finally { window.Close(); }
                }
                finally { CultureInfo.CurrentUICulture = previous; }
            }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));
            await Assert.That(result.arabic).IsEqualTo(locale == "ar");
            await Assert.That(result.rtl).IsEqualTo(locale == "ar");
            await Assert.That(result.pinArabic).IsEqualTo(locale == "ar");
            await Assert.That(result.pathLtr).IsTrue();
            await Assert.That(result.culture).IsEqualTo(locale);
        }
        finally { await Task.Run(session.Dispose); }
    }

    [Test]
    [NotInParallel]
    public async Task AppearanceSelectorOffersThreeSessionChoicesWithoutChangingPreparedWorkspace()
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(DesktopAppearanceTests));
        try
        {
            var result = await session.Dispatch(async () =>
            {
                using var theme = new DesktopTheme(Application.Current!, _ => () => { });
                var messenger = new WeakReferenceMessenger();
                var dialogs = new NoDialogs();
                var loader = new DefinitionLoaderViewModel(messenger, dialogs);
                var workspace = new FormWorkspaceViewModel(messenger);
                var publisher = new ArtifactPublisherViewModel(messenger, dialogs, new ProtectedArtifactWriter());
                var review = new DesktopImportReviewViewModel(messenger, dialogs, publisher);
                using var shell = new DesktopShellViewModel(messenger, loader, workspace, publisher, review, theme: theme);
                shell.Loader.LoadDefinition(ViewModels.DefinitionEditingTests.Definition().CanonicalBytes);
                await shell.Publisher.PrepareSelectionAsync();
                var preparation = shell.Publisher.Preparation;
                long revision = shell.Workspace.Form!.InputRevision;
                var window = new MainWindow { DataContext = shell };
                try
                {
                    window.Show();
                    Dispatcher.UIThread.RunJobs();
                    var selector = window.FindNamedControl<ComboBox>("Appearance");
                    bool startsSystem = selector?.SelectedIndex == 0;
                    bool dark = false;
                    bool localized = false;
                    if (selector is not null)
                    {
                        selector.Focus();
                        selector.SelectedIndex = 2;
                        Dispatcher.UIThread.RunJobs();
                        dark = Application.Current!.RequestedThemeVariant == ThemeVariant.Dark;
                        string? english = ((ComboBoxItem)selector.Items[2]!).Content?.ToString();
                        shell.Workspace.SelectedLocale = "ar";
                        Dispatcher.UIThread.RunJobs();
                        string? arabic = ((ComboBoxItem)selector.Items[2]!).Content?.ToString();
                        localized = english != arabic && arabic?.Any(c => c is >= '\u0600' and <= '\u06ff') == true &&
                            selector.SelectedIndex == 2 && selector.IsFocused;
                    }
                    return (present: selector is { ItemCount: 3 }, startsSystem,
                        selected: selector?.SelectedIndex == 2, dark, localized,
                        preserved: revision == shell.Workspace.Form.InputRevision &&
                            preparation is not null && ReferenceEquals(preparation, shell.Publisher.Preparation));
                }
                finally { window.Close(); }
            }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));
            await Assert.That(result.present).IsTrue();
            await Assert.That(result.startsSystem).IsTrue();
            await Assert.That(result.selected).IsTrue();
            await Assert.That(result.dark).IsTrue();
            await Assert.That(result.localized).IsTrue();
            await Assert.That(result.preserved).IsTrue();
        }
        finally { await Task.Run(session.Dispose); }
    }

    [Test]
    [NotInParallel]
    public async Task ResolvedSemanticTextAndControlBoundariesMeetContrastAcrossVariants()
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(DesktopAppearanceTests));
        try
        {
            bool readable = await session.Dispatch(() =>
            {
                var label = new TextBlock { Text = "Selected section" };
                var item = new ListBoxItem { Content = label };
                var list = new ListBox();
                list.Items.Add(item);
                list.SelectedItem = item;
                var window = new Window { Content = list };
                try
                {
                    window.Show();
                    foreach (var variant in new[] { ThemeVariant.Light, ThemeVariant.Dark, DesktopTheme.HighContrast })
                    {
                        window.RequestedThemeVariant = variant;
                        window.UpdateLayout();
                        Dispatcher.UIThread.RunJobs();
                        Color Resolve(string role)
                        {
                            window.TryFindResource("Configurator" + role + "Brush", variant, out object? value);
                            return ((SolidColorBrush)value!).Color;
                        }
                        foreach (string background in BackgroundRoles)
                        {
                            foreach (string text in TextRoles)
                                if (Contrast(Resolve(text), Resolve(background)) < 4.5) return false;
                            foreach (string boundary in BoundaryRoles)
                                if (Contrast(Resolve(boundary), Resolve(background)) < 3) return false;
                        }
                        if (Contrast(Resolve("SelectedText"), Resolve("Selected")) < 4.5) return false;
                        var presenter = item.GetVisualDescendants().OfType<ContentPresenter>()
                            .Single(control => ReferenceEquals(control.Child, label));
                        if (label.Foreground is not SolidColorBrush foreground ||
                            presenter.Background is not SolidColorBrush backgroundBrush ||
                            Contrast(foreground.Color, backgroundBrush.Color) < 4.5)
                            throw new InvalidOperationException($"Selected contrast: {variant.Key}; selected={item.IsSelected}; foreground={label.Foreground}; background={presenter.Background}");
                    }
                    return true;
                }
                finally { window.Close(); }
            }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));
            await Assert.That(readable).IsTrue();
        }
        finally { await Task.Run(session.Dispose); }
    }

    private static double Contrast(Color first, Color second)
    {
        static double Channel(byte channel)
        {
            double value = channel / 255.0;
            return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
        }
        static double Luminance(Color color) => .2126 * Channel(color.R) + .7152 * Channel(color.G) + .0722 * Channel(color.B);
        double a = Luminance(first), b = Luminance(second);
        return (Math.Max(a, b) + .05) / (Math.Min(a, b) + .05);
    }

    private sealed class NoDialogs : IStorageDialogService
    {
        public Task<StorageSelection<Uri>> SelectAsync(DesktopPathSelectionKind kind, CancellationToken cancellationToken = default) =>
            Task.FromResult(new StorageSelection<Uri>(StorageSelectionStatus.Cancelled));
        public Task<StorageSelection<BoundedFileInput>> ReadImportAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new StorageSelection<BoundedFileInput>(StorageSelectionStatus.Cancelled));
    }
}
