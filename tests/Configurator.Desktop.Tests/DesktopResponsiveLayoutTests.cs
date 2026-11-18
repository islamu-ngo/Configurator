namespace Configurator.Desktop.Tests;

using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Configurator.Desktop.ViewModels.Fields;
using Configurator.Desktop.Views;
using Configurator.Presentation;

public sealed class DesktopResponsiveLayoutTests
{
    public static Avalonia.AppBuilder BuildAvaloniaApp() => DesktopApplicationTests.BuildAvaloniaApp();

    [Test]
    [NotInParallel]
    [Arguments("en", 1)]
    [Arguments("ar", 1)]
    [Arguments("en", 2)]
    [Arguments("ar", 2)]
    public async Task WideReadingWidthIsBoundedAndResizePreservesTheEditingControl(string locale, int textScale)
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(DesktopResponsiveLayoutTests));
        try
        {
            var result = await session.Dispatch(() =>
            {
                using var shell = DesktopTestContext.Root();
                var window = new MainWindow { DataContext = shell, Width = 1440, Height = 1000 };
                window.Resources["ConfiguratorBodyFontSize"] = 14d * textScale;
                window.Resources["ConfiguratorHeadingFontSize"] = 24d * textScale;
                window.Resources["ConfiguratorTitleFontSize"] = 26d * textScale;
                try
                {
                    window.Show();
                    shell.Loader.LoadDefinition(ViewModels.DefinitionEditingTests.Definition().CanonicalBytes);
                    shell.Workspace.SelectedLocale = locale;
                    var field = shell.Workspace.Fields.Single(item => item.Id == "text");
                    field.Text = "Example service";
                    window.UpdateLayout();
                    Dispatcher.UIThread.RunJobs();
                    TextBox editor = window.GetLogicalDescendants().OfType<TextBox>()
                        .Single(control => ReferenceEquals(control.DataContext, field) &&
                            AutomationProperties.GetName(control) == field.Name);
                    editor.Focus();
                    editor.SelectionStart = 1;
                    editor.SelectionEnd = 4;
                    double wideWidth = editor.Bounds.Width;
                    window.Width = 600;
                    window.Height = 540;
                    window.UpdateLayout();
                    Dispatcher.UIThread.RunJobs();
                    TextBox compact = window.GetLogicalDescendants().OfType<TextBox>()
                        .Single(control => ReferenceEquals(control.DataContext, field) &&
                            AutomationProperties.GetName(control) == field.Name);
                    return (wideWidth, minimumHeight: compact.Bounds.Height,
                        fontSize: compact.FontSize,
                        same: ReferenceEquals(editor, compact),
                        selection: (compact.SelectionStart, compact.SelectionEnd),
                        reachable: compact.IsEffectivelyVisible && compact.Bounds.Width > 0);
                }
                finally { window.Close(); }
            }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));
            await Assert.That(result.wideWidth).IsLessThanOrEqualTo(800);
            await Assert.That(result.fontSize).IsEqualTo(14 * textScale);
            await Assert.That(result.minimumHeight).IsGreaterThanOrEqualTo(36);
            await Assert.That(result.same).IsTrue();
            await Assert.That(result.selection).IsEqualTo((1, 4));
            await Assert.That(result.reachable).IsTrue();
        }
        finally { await Task.Run(session.Dispose); }
    }

    [Test]
    [NotInParallel]
    public async Task SplitViewRailCollapsesToOverlayBelowBreakpointAndFieldOneLeadsAboveFoldAt960x800()
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(DesktopResponsiveLayoutTests));
        try
        {
            var result = await session.Dispatch(() =>
            {
                using var shell = DesktopTestContext.Root();
                var window = new MainWindow { DataContext = shell, Width = 960, Height = 800 };
                try
                {
                    window.Show();
                    shell.Loader.LoadDefinition(DesktopApplicationTests.DefinitionWithExperiences());
                    window.UpdateLayout();
                    Dispatcher.UIThread.RunJobs();
                    window.UpdateLayout();

                    var splitView = window.FindNamedControl<SplitView>("WorkspaceSplitView");
                    var wideMode = splitView?.DisplayMode;
                    var discoveryOverlay = window.FindNamedControl<Control>("DiscoveryOverlay");
                    bool discoveryHiddenInitially = discoveryOverlay is not null && !discoveryOverlay.IsEffectivelyVisible;
                    var firstField = shell.Workspace.Fields.First(field => field.IsVisible);
                    var firstInput = window.GetLogicalDescendants().OfType<Control>()
                        .FirstOrDefault(control => ReferenceEquals(control.DataContext, firstField) &&
                            control is TextBox or ComboBox);
                    double firstFieldBottom = firstInput?.TranslatePoint(new Point(0, firstInput.Bounds.Height), window)?.Y ?? 9999;

                    window.Width = 600;
                    window.Height = 640;
                    window.UpdateLayout();
                    Dispatcher.UIThread.RunJobs();
                    window.UpdateLayout();
                    var narrowMode = splitView?.DisplayMode;

                    return (
                        hasSplitView: splitView is not null,
                        wideMode,
                        narrowMode,
                        discoveryHiddenInitially,
                        firstFieldBottom);
                }
                finally { window.Close(); }
            }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

            await Assert.That(result.hasSplitView).IsTrue();
            await Assert.That(result.wideMode is SplitViewDisplayMode.CompactInline or SplitViewDisplayMode.Inline).IsTrue();
            await Assert.That(result.narrowMode).IsEqualTo(SplitViewDisplayMode.Overlay);
            await Assert.That(result.discoveryHiddenInitially).IsTrue();
            await Assert.That(result.firstFieldBottom).IsLessThanOrEqualTo(420d);
        }
        finally { await Task.Run(session.Dispose); }
    }

    [Test]
    [NotInParallel]
    public async Task StoreProjectsRenderCompactLandscapeTwoColumnCardsWithDecodedLogoAndDetailsModal()
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(DesktopResponsiveLayoutTests));
        try
        {
            var result = await session.Dispatch(async () =>
            {
                using var storeProvider = new Configurator.Presentation.Store.EmbeddedDefinitionStoreProvider();
                var messenger = new CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger();
                using var loader = new Configurator.Desktop.ViewModels.DefinitionLoaderViewModel(messenger, new NoDialogs(), storeProvider: storeProvider);
                using var workspace = new Configurator.Desktop.ViewModels.FormWorkspaceViewModel(messenger);
                using var publisher = new Configurator.Desktop.ViewModels.ArtifactPublisherViewModel(messenger, new NoDialogs(), new Configurator.Artifacts.ProtectedArtifactWriter());
                using var importReview = new Configurator.Desktop.ViewModels.DesktopImportReviewViewModel(messenger, new NoDialogs(), publisher);
                using var shell = new Configurator.Desktop.ViewModels.DesktopShellViewModel(messenger, loader, workspace, publisher, importReview);
                await shell.Loader.LoadStoreAsync();

                var window = new MainWindow { DataContext = shell, Width = 1280, Height = 860 };
                try
                {
                    window.Show();
                    window.UpdateLayout();
                    Dispatcher.UIThread.RunJobs();
                    window.UpdateLayout();

                    var cardsControl = window.GetNamedControl<ItemsControl>("StoreProjects");
                    Border[] cards = cardsControl.GetVisualDescendants().OfType<Border>()
                        .Where(border => border.Classes.Contains("store-project-card"))
                        .ToArray();
                    double firstCardTop = cards.Length > 0 ? cards[0].TranslatePoint(default, cardsControl)!.Value.Y : -1;
                    double secondCardTop = cards.Length > 1 ? cards[1].TranslatePoint(default, cardsControl)!.Value.Y : -1;
                    bool twoColumnRow = cards.Length >= 2 && Math.Abs(firstCardTop - secondCardTop) < 4d;
                    bool compactLandscape = cards.All(card => card.Bounds.Height is > 80d and <= 190d);

                    Image? logo = cardsControl.GetVisualDescendants().OfType<Image>()
                        .FirstOrDefault(image => image.Name == "StoreProjectLogo" && image.IsEffectivelyVisible && image.Source is not null);
                    double? logoWidth = logo?.Bounds.Width;

                    Button? detailsButton = cardsControl.GetVisualDescendants().OfType<Button>()
                        .FirstOrDefault(button => button.Name == "StoreProjectDetailsButton");
                    if (detailsButton?.Command is { } detailsCmd && detailsCmd.CanExecute(detailsButton.CommandParameter))
                        detailsCmd.Execute(detailsButton.CommandParameter);
                    window.UpdateLayout();
                    Dispatcher.UIThread.RunJobs();

                    var detailsModal = window.FindNamedControl<Control>("StoreProjectDetailsDialog");
                    bool modalVisible = detailsModal?.IsEffectivelyVisible == true;
                    string modalText = detailsModal is null
                        ? string.Empty
                        : string.Join("\n", detailsModal.GetVisualDescendants().OfType<TextBlock>()
                            .Where(tb => !string.IsNullOrWhiteSpace(tb.Text))
                            .Select(tb => tb.Text!));

                    window.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape, Source = window });
                    window.UpdateLayout();
                    Dispatcher.UIThread.RunJobs();
                    bool modalClosedOnEsc = detailsModal?.IsEffectivelyVisible != true;

                    return (
                        cardCount: cards.Length,
                        twoColumnRow,
                        compactLandscape,
                        hasDecodedLogo: logo is not null && logoWidth is >= 48d and <= 96d,
                        hasDetailsButton: detailsButton is not null,
                        modalVisible,
                        hasLicenseInModal: modalText.Contains("CC-BY-4.0", StringComparison.Ordinal),
                        modalClosedOnEsc);
                }
                finally { window.Close(); }
            }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

            await Assert.That(result.cardCount).IsGreaterThanOrEqualTo(2);
            await Assert.That(result.twoColumnRow).IsTrue();
            await Assert.That(result.compactLandscape).IsTrue();
            await Assert.That(result.hasDecodedLogo).IsTrue();
            await Assert.That(result.hasDetailsButton).IsTrue();
            await Assert.That(result.modalVisible).IsTrue();
            await Assert.That(result.hasLicenseInModal).IsTrue();
            await Assert.That(result.modalClosedOnEsc).IsTrue();
        }
        finally { await Task.Run(session.Dispose); }
    }

    private sealed class NoDialogs : Configurator.Desktop.Services.IStorageDialogService
    {
        public Task<Configurator.Desktop.Services.StorageSelection<Uri>> SelectAsync(Configurator.Desktop.ViewModels.DesktopPathSelectionKind kind, CancellationToken cancellationToken = default) =>
            Task.FromResult(new Configurator.Desktop.Services.StorageSelection<Uri>(Configurator.Desktop.Services.StorageSelectionStatus.Cancelled));
        public Task<Configurator.Desktop.Services.StorageSelection<Configurator.Artifacts.BoundedFileInput>> ReadImportAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new Configurator.Desktop.Services.StorageSelection<Configurator.Artifacts.BoundedFileInput>(Configurator.Desktop.Services.StorageSelectionStatus.Cancelled));
    }
}
