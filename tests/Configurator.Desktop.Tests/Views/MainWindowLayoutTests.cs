namespace Configurator.Desktop.Tests.Views;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Configurator.Core.Definitions;
using Configurator.Desktop.ViewModels;
using Configurator.Desktop.Views;
using Configurator.Presentation.Store;

public sealed class MainWindowLayoutTests
{
    public static AppBuilder BuildAvaloniaApp() => DesktopApplicationTests.BuildAvaloniaApp();

    [Test]
    [NotInParallel]
    public async Task HeaderAndSectionRailStayFixedWhileBodyScrollsAndRailScrollsIndependently()
    {
        Definition definition = await LoadBundledEventDefinitionAsync();
        var session = HeadlessUnitTestSession.StartNew(typeof(MainWindowLayoutTests));
        try
        {
            var result = await session.Dispatch(() =>
            {
                using var shell = DesktopTestContext.Root();
                var window = new MainWindow { DataContext = shell, Width = 1200, Height = 560 };
                try
                {
                    window.Show();
                    shell.Loader.LoadDefinition(definition.CanonicalBytes.ToArray());
                    window.UpdateLayout();
                    Dispatcher.UIThread.RunJobs();
                    window.UpdateLayout();

                    var openStage = window.GetNamedControl<Button>("OpenStage");
                    var settingsStage = window.GetNamedControl<Button>("SettingsStage");
                    var appearance = window.GetNamedControl<ComboBox>("Appearance");
                    var locale = window.GetNamedControl<ComboBox>("DefinitionLocale");
                    var direction = window.GetNamedControl<ComboBox>("DirectionMode");
                    bool expanderRemoved = window.FindNamedControl<Expander>("WorkspaceSettings") is null;
                    var rail = window.GetNamedControl<ListBox>("SectionRail");
                    var bodyScroll = window.GetNamedControl<ScrollViewer>("WorkspaceScroll");

                    double headerBeforeY = openStage.TranslatePoint(default, window)!.Value.Y;
                    double settingsButtonBeforeY = settingsStage.TranslatePoint(default, window)!.Value.Y;
                    double appearanceBeforeY = appearance.TranslatePoint(default, window)!.Value.Y;
                    bool headerSingleRow = Math.Abs(headerBeforeY - settingsButtonBeforeY) < 4 &&
                        Math.Abs(headerBeforeY - appearanceBeforeY) < 4 &&
                        appearance.IsEffectivelyVisible && locale.IsEffectivelyVisible && direction.IsEffectivelyVisible;
                    double railBeforeY = rail.TranslatePoint(default, window)!.Value.Y;
                    bool railVisible = rail.IsEffectivelyVisible;
                    bool railFitsViewport = railBeforeY + rail.Bounds.Height <= window.Bounds.Height + 1;
                    bool railSpansToBottomWithoutFooter = railBeforeY + rail.Bounds.Height >= window.Bounds.Height - 24;
                    bool bodySpansToBottomWithoutFooter = bodyScroll.TranslatePoint(default, window)!.Value.Y + bodyScroll.Bounds.Height >= window.Bounds.Height - 24;

                    bodyScroll.Offset = new Vector(0, 220);
                    window.UpdateLayout();
                    Dispatcher.UIThread.RunJobs();
                    window.UpdateLayout();

                    double bodyScrolledY = bodyScroll.Offset.Y;
                    double headerAfterBodyScrollY = openStage.TranslatePoint(default, window)!.Value.Y;
                    double settingsAfterBodyScrollY = settingsStage.TranslatePoint(default, window)!.Value.Y;
                    double appearanceAfterBodyScrollY = appearance.TranslatePoint(default, window)!.Value.Y;
                    double railAfterBodyScrollY = rail.TranslatePoint(default, window)!.Value.Y;

                    ScrollViewer? railScroll = rail.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
                    double railExtentY = railScroll?.Extent.Height ?? 0;
                    double railViewportY = railScroll?.Viewport.Height ?? 0;
                    if (railScroll is not null)
                    {
                        railScroll.Offset = new Vector(0, 120);
                        window.UpdateLayout();
                        Dispatcher.UIThread.RunJobs();
                        window.UpdateLayout();
                    }

                    return (
                        expanderRemoved,
                        headerSingleRow,
                        railVisible,
                        railFitsViewport,
                        railSpansToBottomWithoutFooter,
                        bodySpansToBottomWithoutFooter,
                        bodyScrolled: bodyScrolledY > 0,
                        headerPinned: Math.Abs(headerBeforeY - headerAfterBodyScrollY) < 0.5 &&
                            Math.Abs(settingsButtonBeforeY - settingsAfterBodyScrollY) < 0.5 &&
                            Math.Abs(appearanceBeforeY - appearanceAfterBodyScrollY) < 0.5,
                        railPinned: Math.Abs(railBeforeY - railAfterBodyScrollY) < 0.5,
                        railCanScrollInternally: railExtentY > railViewportY,
                        railScrolled: (railScroll?.Offset.Y ?? 0) > 0,
                        bodyPreserved: Math.Abs(bodyScroll.Offset.Y - bodyScrolledY) < 0.5,
                        headerStillPinnedAfterRailScroll: Math.Abs(openStage.TranslatePoint(default, window)!.Value.Y - headerBeforeY) < 0.5
                    );
                }
                finally { window.Close(); }
            }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

            await Assert.That(result.expanderRemoved).IsTrue();
            await Assert.That(result.headerSingleRow).IsTrue();
            await Assert.That(result.railVisible).IsTrue();
            await Assert.That(result.railFitsViewport).IsTrue();
            await Assert.That(result.railSpansToBottomWithoutFooter).IsTrue();
            await Assert.That(result.bodySpansToBottomWithoutFooter).IsTrue();
            await Assert.That(result.bodyScrolled).IsTrue();
            await Assert.That(result.headerPinned).IsTrue();
            await Assert.That(result.railPinned).IsTrue();
            await Assert.That(result.railCanScrollInternally).IsTrue();
            await Assert.That(result.railScrolled).IsTrue();
            await Assert.That(result.bodyPreserved).IsTrue();
            await Assert.That(result.headerStillPinnedAfterRailScroll).IsTrue();
        }
        finally { await Task.Run(session.Dispose); }
    }

    [Test]
    [NotInParallel]
    public async Task SectionRailCollapsesOutsideConfigureAndBelowWidthBreakpoint()
    {
        Definition definition = await LoadBundledEventDefinitionAsync();
        var session = HeadlessUnitTestSession.StartNew(typeof(MainWindowLayoutTests));
        try
        {
            var result = await session.Dispatch(() =>
            {
                using var shell = DesktopTestContext.Root();
                var window = new MainWindow { DataContext = shell, Width = 1200, Height = 700 };
                try
                {
                    window.Show();
                    shell.Loader.LoadDefinition(definition.CanonicalBytes.ToArray());
                    window.UpdateLayout();
                    Dispatcher.UIThread.RunJobs();
                    window.UpdateLayout();

                    var rail = window.GetNamedControl<ListBox>("SectionRail");
                    var bodyScroll = window.GetNamedControl<ScrollViewer>("WorkspaceScroll");
                    var selector = window.GetNamedControl<ComboBox>("SectionSelector");
                    bool configureWideRail = rail.IsEffectivelyVisible;

                    shell.Navigation.OpenCommand.Execute(null);
                    window.UpdateLayout();
                    Dispatcher.UIThread.RunJobs();
                    window.UpdateLayout();
                    bool openRailHidden = !rail.IsEffectivelyVisible;
                    double openBodyLeftX = bodyScroll.TranslatePoint(default, window)!.Value.X;

                    shell.Navigation.ReviewCommand.Execute(null);
                    window.UpdateLayout();
                    Dispatcher.UIThread.RunJobs();
                    window.UpdateLayout();
                    bool reviewRailHidden = !rail.IsEffectivelyVisible;
                    double reviewBodyLeftX = bodyScroll.TranslatePoint(default, window)!.Value.X;

                    shell.Navigation.ConfigureCommand.Execute(null);
                    window.Width = 600;
                    window.Height = 540;
                    window.UpdateLayout();
                    Dispatcher.UIThread.RunJobs();
                    window.UpdateLayout();

                    return (
                        configureWideRail,
                        openRailHidden,
                        reviewRailHidden,
                        openFullWidth: openBodyLeftX <= 24,
                        reviewFullWidth: reviewBodyLeftX <= 24,
                        narrowRailHidden: !rail.IsEffectivelyVisible,
                        narrowSelectorVisible: selector.IsEffectivelyVisible);
                }
                finally { window.Close(); }
            }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

            await Assert.That(result.configureWideRail).IsTrue();
            await Assert.That(result.openRailHidden).IsTrue();
            await Assert.That(result.reviewRailHidden).IsTrue();
            await Assert.That(result.openFullWidth).IsTrue();
            await Assert.That(result.reviewFullWidth).IsTrue();
            await Assert.That(result.narrowRailHidden).IsTrue();
            await Assert.That(result.narrowSelectorVisible).IsTrue();
        }
        finally { await Task.Run(session.Dispose); }
    }

    [Test]
    [NotInParallel]
    public async Task SettingsButtonOpensSettingsWorkspaceWithLeftSidebarSections()
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(MainWindowLayoutTests));
        try
        {
            var result = await session.Dispatch(() =>
            {
                using var shell = DesktopTestContext.Root();
                var window = new MainWindow { DataContext = shell, Width = 1200, Height = 700 };
                try
                {
                    window.Show();
                    window.UpdateLayout();
                    Dispatcher.UIThread.RunJobs();
                    window.UpdateLayout();

                    var settingsButton = window.GetNamedControl<Button>("SettingsStage");
                    bool settingsInitiallyVisible = settingsButton.IsEffectivelyVisible && settingsButton.IsEnabled;
                    settingsButton.Command!.Execute(null);
                    window.UpdateLayout();
                    Dispatcher.UIThread.RunJobs();
                    window.UpdateLayout();

                    var settingsRail = window.GetNamedControl<ListBox>("SettingsSectionRail");
                    var settingsView = window.GetLogicalDescendants().OfType<SettingsWorkspaceView>().Single();
                    bool railVisibleWide = settingsRail.IsEffectivelyVisible;
                    int sectionCount = settingsRail.ItemCount;

                    settingsRail.SelectedIndex = 2;
                    window.UpdateLayout();
                    Dispatcher.UIThread.RunJobs();
                    bool switchedToKeyboard = shell.Navigation.IsKeyboardSettings;

                    window.Width = 600;
                    window.Height = 540;
                    window.UpdateLayout();
                    Dispatcher.UIThread.RunJobs();
                    window.UpdateLayout();
                    var compactSelector = window.GetNamedControl<ComboBox>("SettingsSectionSelector");

                    return (
                        settingsInitiallyVisible,
                        stageIsSettings: shell.Navigation.Stage == DesktopWorkspaceStage.Settings,
                        viewVisible: settingsView.IsEffectivelyVisible,
                        railVisibleWide,
                        sectionCount,
                        switchedToKeyboard,
                        narrowRailHidden: !settingsRail.IsEffectivelyVisible,
                        narrowSelectorVisible: compactSelector.IsEffectivelyVisible);
                }
                finally { window.Close(); }
            }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

            await Assert.That(result.settingsInitiallyVisible).IsTrue();
            await Assert.That(result.stageIsSettings).IsTrue();
            await Assert.That(result.viewVisible).IsTrue();
            await Assert.That(result.railVisibleWide).IsTrue();
            await Assert.That(result.sectionCount).IsEqualTo(4);
            await Assert.That(result.switchedToKeyboard).IsTrue();
            await Assert.That(result.narrowRailHidden).IsTrue();
            await Assert.That(result.narrowSelectorVisible).IsTrue();
        }
        finally { await Task.Run(session.Dispose); }
    }

    private static async Task<Definition> LoadBundledEventDefinitionAsync()
    {
        using var provider = new EmbeddedDefinitionStoreProvider();
        IReadOnlyList<DefinitionStoreProject> projects = await provider.LoadAsync();
        DefinitionStoreProject project = projects.Single(item => item.ProjectId == "islamu-event");
        DefinitionStoreResult opened = await provider.OpenAsync(project.ProjectId, project.Revisions[0].Revision);
        return ((DefinitionStoreResult.Success)opened).Definition;
    }
}
