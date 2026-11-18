namespace Configurator.Desktop.Tests;

using System.Text;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Configurator.Desktop.Resources;
using Configurator.Desktop.ViewModels;
using Configurator.Desktop.ViewModels.Fields;
using Configurator.Desktop.Views;
using Configurator.Presentation;
using Configurator.Presentation.Theming;

public sealed class DesktopNavigationTests
{
    public static Avalonia.AppBuilder BuildAvaloniaApp() => DesktopApplicationTests.BuildAvaloniaApp();

    [Test]
    public async Task StageAndSectionSelectionPreserveCurrentPreparation()
    {
        using var shell = DesktopTestContext.Root();
        using var navigation = new DesktopNavigationViewModel(shell.Workspace);
        shell.Loader.LoadDefinition(DesktopApplicationTests.DefinitionWithExperiences());
        await shell.Publisher.PrepareSelectionAsync();
        var preparation = shell.Publisher.Preparation;
        long revision = shell.Workspace.Form!.InputRevision;
        navigation.Stage = DesktopWorkspaceStage.Review;
        navigation.Stage = DesktopWorkspaceStage.Configure;
        shell.Workspace.SelectedExperience = shell.Workspace.Experiences[1];
        navigation.SelectedSection = navigation.Sections.Single();
        await Assert.That(preparation).IsNotNull();
        await Assert.That(shell.Publisher.Preparation).IsSameReferenceAs(preparation);
        await Assert.That(shell.Workspace.Form.InputRevision).IsEqualTo(revision);
        await Assert.That(navigation.SelectedSection!.Id).IsEqualTo("service");
    }

    [Test]
    [Arguments("Conformance.RootMap")]
    [Arguments("Conformance.RootVariant")]
    public async Task RecursiveRootsRemainReachableWithoutOrdinaryFields(string resource)
    {
        using Stream input = typeof(DesktopNavigationTests).Assembly.GetManifestResourceStream(resource)!;
        using var bytes = new MemoryStream();
        input.CopyTo(bytes);
        using var shell = DesktopTestContext.Root();
        using var navigation = new DesktopNavigationViewModel(shell.Workspace);
        shell.Loader.LoadDefinition(bytes.ToArray());
        await Assert.That(navigation.Stage).IsEqualTo(DesktopWorkspaceStage.Configure);
        await Assert.That(navigation.Sections.Count).IsEqualTo(1);
        await Assert.That(navigation.VisibleFields.Single()).IsSameReferenceAs(shell.Workspace.Fields.Single());
        await Assert.That(shell.Workspace.Form!.InputRevision).IsEqualTo(0);
    }

    [Test]
    public async Task RootPagesRetainEditorsAndGlobalMissingCounts()
    {
        JsonObject definition = JsonNode.Parse("""
            {"$schema":"urn:islamu:configurator:authoring:v0.0.1",
             "identity":{"familyId":"7cb18fb4-3795-4b09-ab10-e9286b7c0f01","revision":1},
             "project":{"id":"paged-editor","name":"Paged editor"},"applicationVersions":["1"],
             "configuration":{"type":"object","additionalProperties":false,
               "x-configurator":{"sensitivity":"public"}},
             "outputs":{"config.json":{"id":"config","format":"json"}}}
            """)!.AsObject();
        JsonObject configuration = definition["configuration"]!.AsObject();
        var properties = new JsonObject();
        var required = new JsonArray();
        for (int index = 0; index < 101; index++)
        {
            string name = "setting" + index;
            properties[name] = JsonNode.Parse("""{"type":"string","x-configurator":{"sensitivity":"public","detail":"basic"}}""");
            required.Add(name);
        }
        configuration["properties"] = properties;
        configuration["required"] = required;
        using var shell = DesktopTestContext.Root();
        using var navigation = new DesktopNavigationViewModel(shell.Workspace);
        await Assert.That(shell.Loader.LoadDefinition(Encoding.UTF8.GetBytes(definition.ToJsonString())))
            .IsTrue().Because(shell.Status);
        await Assert.That(navigation.VisibleFields.Count).IsEqualTo(50);
        var first = navigation.VisibleFields[0];
        first.Text = "draft kept";
        long revision = shell.Workspace.Form!.InputRevision;
        navigation.NextPageCommand.Execute(null);
        await Assert.That(navigation.VisibleFields.Count).IsEqualTo(50);
        navigation.NextPageCommand.Execute(null);
        await Assert.That(navigation.VisibleFields.Count).IsEqualTo(1);
        await Assert.That(navigation.Progress!.Missing).IsEqualTo(100);
        navigation.PreviousPageCommand.Execute(null);
        navigation.PreviousPageCommand.Execute(null);
        await Assert.That(navigation.VisibleFields[0]).IsSameReferenceAs(first);
        await Assert.That(first.Text).IsEqualTo("draft kept");
        await Assert.That(shell.Workspace.Form.InputRevision).IsEqualTo(revision);
        // Authored property order keeps setting100 last, on the third page.
        var correction = shell.Workspace.Form.Validate().Diagnostics.Single(item => item.FieldId == "setting100");
        await Assert.That(navigation.FocusCorrection(correction.FieldId, correction.Location!)).IsTrue();
        await Assert.That(navigation.PageNumber).IsEqualTo(3);
        await Assert.That(navigation.VisibleFields.Single().Id).IsEqualTo("setting100");
        first.Text = "revised";
        await Assert.That(navigation.FocusCorrection(correction.FieldId, correction.Location!)).IsFalse();
        await Assert.That(navigation.PageNumber).IsEqualTo(3);
    }

    [Test]
    [NotInParallel]
    public async Task LoadedDefinitionExposesNamedSectionsWithoutChangingDrafts()
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(DesktopNavigationTests));
        try
        {
            var result = await session.Dispatch(() =>
            {
                using var shell = DesktopTestContext.Root();
                var window = new MainWindow { DataContext = shell };
                try
                {
                    window.Show();
                    shell.Loader.LoadDefinition(DesktopApplicationTests.Definition());
                    var form = shell.Workspace.Form!;
                    shell.Workspace.Fields.Single(field => field.Id == "mode").Text = "remote";
                    var port = shell.Workspace.Fields.Single(field => field.Id == "port");
                    port.Text = "unfinished-number";
                    long revision = form.InputRevision;
                    window.UpdateLayout();
                    Dispatcher.UIThread.RunJobs();
                    var sections = window.FindNamedControl<ComboBox>("SectionSelector");
                    return (sectionCount: sections?.ItemCount ?? 0,
                        selected: sections?.SelectedItem is DesktopSectionViewModel selected &&
                            !string.IsNullOrWhiteSpace(selected.Title) && sections.SelectionBoxItem is not null,
                        revision: form.InputRevision == revision,
                        sameEditor: ReferenceEquals(port, shell.Workspace.Fields.Single(field => field.Id == "port")),
                        draft: port.Text,
                        readyRequired: window.FindNamedControl<TextBlock>("ReadyRequiredCount")?.Text,
                        expectedReady: form.Projection.ProgressSummary.ReadyRequired.ToString(System.Globalization.CultureInfo.InvariantCulture));
                }
                finally { window.Close(); }
            }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));
            await Assert.That(result.sectionCount).IsEqualTo(1);
            await Assert.That(result.selected).IsTrue();
            await Assert.That(result.revision).IsTrue();
            await Assert.That(result.sameEditor).IsTrue();
            await Assert.That(result.draft).IsEqualTo("unfinished-number");
            await Assert.That(result.readyRequired).IsEqualTo(result.expectedReady);
        }
        finally { await Task.Run(session.Dispose); }
    }

    [Test]
    public async Task SettingsStageRemainsReachableWithoutWorkspaceAndDirectionModeSyncsBidirectionally()
    {
        using var shell = DesktopTestContext.Root();
        using var navigation = new DesktopNavigationViewModel(shell.Workspace);

        navigation.Stage = DesktopWorkspaceStage.Configure;
        await Assert.That(navigation.Stage).IsEqualTo(DesktopWorkspaceStage.Open);

        navigation.SettingsCommand.Execute(null);
        await Assert.That(navigation.Stage).IsEqualTo(DesktopWorkspaceStage.Settings);
        await Assert.That(navigation.SettingsSections.Count).IsEqualTo(4);
        await Assert.That(navigation.SelectedSettingsSection?.Id).IsEqualTo(DesktopSettingsSectionId.Appearance);

        navigation.SelectedSettingsSection = navigation.SettingsSections[1];
        await Assert.That(navigation.IsLanguageSettings).IsTrue();

        shell.Workspace.DirectionModeIndex = 2;
        await Assert.That(shell.Workspace.FollowLocaleDirection).IsFalse();
        await Assert.That(shell.Workspace.IsRightToLeft).IsTrue();
        await Assert.That(shell.Workspace.FlowDirection).IsEqualTo(Avalonia.Media.FlowDirection.RightToLeft);

        shell.Workspace.DirectionModeIndex = 1;
        await Assert.That(shell.Workspace.FollowLocaleDirection).IsFalse();
        await Assert.That(shell.Workspace.IsRightToLeft).IsFalse();
        await Assert.That(shell.Workspace.FlowDirection).IsEqualTo(Avalonia.Media.FlowDirection.LeftToRight);

        shell.Workspace.DirectionModeIndex = 0;
        await Assert.That(shell.Workspace.FollowLocaleDirection).IsTrue();
    }

    [Test]
    [NotInParallel]
    public async Task ExistingConfigurationEntryWaitsForDefinitionThenOpensImportWithoutReadingAFile()
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(DesktopNavigationTests));
        try
        {
            var result = await session.Dispatch(() =>
            {
                using var shell = DesktopTestContext.Root();
                var window = new MainWindow { DataContext = shell };
                try
                {
                    window.Show();
                    Button? fresh = window.FindNamedControl<Button>("StartFreshTask");
                    Button? profile = window.FindNamedControl<Button>("OpenSavedProfileTask");
                    fresh?.Command?.Execute(null);
                    bool freshFocused = shell.FocusTarget == "DefinitionPath";
                    profile?.Command?.Execute(null);
                    window.UpdateLayout();
                    Dispatcher.UIThread.RunJobs();
                    bool profileFocused = shell.ProfileTaskExpanded && window.GetLogicalDescendants()
                        .OfType<TextBox>().Any(control => control.Name == "ProfileDirectory" && control.IsFocused);
                    Button? entry = window.FindNamedControl<Button>("UseExistingTask");
                    entry?.Command?.Execute(null);
                    bool loaded = shell.Loader.LoadDefinition(DesktopApplicationTests.Definition());
                    window.UpdateLayout();
                    Dispatcher.UIThread.RunJobs();
                    return (hasEntry: entry is not null && fresh is not null && profile is not null,
                        freshFocused, profileFocused, loaded,
                        stage: shell.Navigation.Stage, focus: shell.FocusTarget,
                        focusedName: (window.FocusManager?.GetFocusedElement() as Control)?.Name,
                        importVisible: window.FindNamedControl<Button>("SelectImport")?.IsEffectivelyVisible == true,
                        importEnabled: window.FindNamedControl<Button>("SelectImport")?.IsEffectivelyEnabled == true,
                        hasComparison: shell.Workspace.Form?.ImportComparison is not null);
                }
                finally { window.Close(); }
            }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));
            await Assert.That(result.hasEntry).IsTrue();
            await Assert.That(result.freshFocused).IsTrue();
            await Assert.That(result.profileFocused).IsTrue();
            await Assert.That(result.loaded).IsTrue();
            await Assert.That(result.stage).IsEqualTo(DesktopWorkspaceStage.Review);
            await Assert.That(result.focus).IsEqualTo("SelectImport");
            await Assert.That(result.importVisible).IsTrue();
            await Assert.That(result.importEnabled).IsTrue();
            await Assert.That(result.focusedName).IsEqualTo("SelectImport");
            await Assert.That(result.hasComparison).IsFalse();
        }
        finally { await Task.Run(session.Dispose); }
    }

    [Test]
    [NotInParallel]
    public async Task ConstrainedSlotHostsCompactOrDocumentWithoutUnboundedWorkspaceScrollWhilePreservingRailAndUndo()
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(DesktopNavigationTests));
        try
        {
            var result = await session.Dispatch(() =>
            {
                using var shell = DesktopTestContext.Root();
                shell.Loader.LoadDefinition(DesktopApplicationTests.DefinitionWithExperiences());
                var window = new MainWindow { DataContext = shell, Width = 1280, Height = 820 };
                try
                {
                    window.Show();
                    window.UpdateLayout();
                    Dispatcher.UIThread.RunJobs();

                    var rail = window.GetNamedControl<ListBox>("SectionRail");
                    double railWidth = rail.Bounds.Width;

                    var field = shell.Workspace.Fields.Single(item => item.Id == "name");
                    field.Text = "Updated service";
                    bool canUndoBefore = shell.Workspace.UndoCommand.CanExecute(null);
                    shell.Workspace.UndoCommand.Execute(null);
                    bool revertedByUndo = field.Text != "Updated service";
                    bool canRedoAfter = shell.Workspace.RedoCommand.CanExecute(null);
                    shell.Workspace.RedoCommand.Execute(null);
                    bool restoredByRedo = field.Text == "Updated service";

                    long revBeforeNaming = shell.Workspace.Form!.InputRevision;
                    shell.Workspace.ToggleNamingModeCommand.Execute(null);
                    bool technicalNaming = shell.Workspace.NamingMode == Configurator.Presentation.WorkspaceNamingMode.Technical;
                    bool revisionUnchanged = shell.Workspace.Form.InputRevision == revBeforeNaming;

                    shell.Navigation.LayoutMode = ConfigureLayoutMode.AllSections;
                    shell.Workspace.Fields.Single(f => f.Id == "name").ClearCommand.Execute(null);
                    window.UpdateLayout();
                    Dispatcher.UIThread.RunJobs();

                    var scroll = window.GetNamedControl<ScrollViewer>("WorkspaceScroll");
                    var boundedSlot = window.FindNamedControl<Grid>("BoundedConfigureSlot");
                    bool insideScroll = boundedSlot is not null &&
                        Avalonia.LogicalTree.LogicalExtensions.GetLogicalAncestors(boundedSlot).Contains(scroll);

                    return (railWidth,
                        canUndoBefore,
                        revertedByUndo,
                        canRedoAfter,
                        restoredByRedo,
                        technicalNaming,
                        revisionUnchanged,
                        boundedVisible: boundedSlot?.IsVisible == true,
                        scrollHiddenInBounded: !scroll.IsVisible,
                        outsideScroll: boundedSlot is not null && !insideScroll,
                        finiteHeight: boundedSlot is not null && !double.IsPositiveInfinity(boundedSlot.Bounds.Height) && boundedSlot.Bounds.Height <= 820,
                        hasThreeRows: boundedSlot?.RowDefinitions.Count == 3);
                }
                finally { window.Close(); }
            }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

            await Assert.That(result.railWidth).IsBetween(240d, 460d);
            await Assert.That(result.canUndoBefore).IsTrue();
            await Assert.That(result.revertedByUndo).IsTrue();
            await Assert.That(result.canRedoAfter).IsTrue();
            await Assert.That(result.restoredByRedo).IsTrue();
            await Assert.That(result.technicalNaming).IsTrue();
            await Assert.That(result.revisionUnchanged).IsTrue();
            await Assert.That(result.boundedVisible).IsTrue();
            await Assert.That(result.scrollHiddenInBounded).IsTrue();
            await Assert.That(result.outsideScroll).IsTrue();
            await Assert.That(result.finiteHeight).IsTrue();
            await Assert.That(result.hasThreeRows).IsTrue();
        }
        finally { await Task.Run(session.Dispose); }
    }

    [Test]
    [NotInParallel]
    [Arguments("en")]
    [Arguments("ar")]
    public async Task ErrorSummaryF7F8AndCtrlKFocusTargetsAndCtrlShiftETogglesDocumentDrawerWithoutEjectingAllSections(string locale)
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(DesktopNavigationTests));
        try
        {
            var result = await session.Dispatch(() =>
            {
                using var shell = DesktopTestContext.Root();
                shell.Loader.LoadDefinition(DesktopApplicationTests.DefinitionWithExperiences());
                shell.Workspace.SelectedLocale = locale;
                var window = new MainWindow { DataContext = shell, Width = 1200, Height = 800 };
                try
                {
                    window.Show();
                    window.UpdateLayout();
                    Dispatcher.UIThread.RunJobs();

                    shell.Navigation.LayoutMode = ConfigureLayoutMode.AllSections;
                    shell.Workspace.Fields.Single(f => f.Id == "name").ClearCommand.Execute(null);
                    window.UpdateLayout();
                    Dispatcher.UIThread.RunJobs();

                    // Missing required validation surfaces ErrorSummary and preserves AllSections on F8.
                    bool valid = shell.Workspace.Validate();
                    window.UpdateLayout();
                    Dispatcher.UIThread.RunJobs();

                    var errorBanner = window.FindNamedControl<Control>("ErrorSummaryBanner");
                    window.KeyPress(Key.F8, RawInputModifiers.None, PhysicalKey.F8, null);
                    window.KeyRelease(Key.F8, RawInputModifiers.None, PhysicalKey.F8, null);
                    Dispatcher.UIThread.RunJobs();
                    var layoutAfterF8 = shell.Navigation.LayoutMode;
                    var focusedAfterF8 = window.FocusManager?.GetFocusedElement() as Control;

                    // Ctrl+K opens DiscoveryOverlay and focuses ConfigurationSearch.
                    var cmdMod = Avalonia.Application.Current!.PlatformSettings!.HotkeyConfiguration.CommandModifiers;
                    window.RaiseEvent(new KeyEventArgs
                    {
                        RoutedEvent = InputElement.KeyDownEvent,
                        Key = Key.K,
                        KeyModifiers = cmdMod,
                        Source = window
                    });
                    window.UpdateLayout();
                    Dispatcher.UIThread.RunJobs();
                    var discoveryOverlay = window.FindNamedControl<Control>("DiscoveryOverlay");
                    bool discoveryVisibleOnCtrlK = discoveryOverlay?.IsEffectivelyVisible == true;

                    // Ctrl+Shift+E opens docked Document tab without losing AllSections.
                    window.RaiseEvent(new KeyEventArgs
                    {
                        RoutedEvent = InputElement.KeyDownEvent,
                        Key = Key.E,
                        KeyModifiers = cmdMod | KeyModifiers.Shift,
                        Source = window
                    });
                    window.UpdateLayout();
                    Dispatcher.UIThread.RunJobs();
                    var docDrawer = window.FindNamedControl<Control>("DocumentDockPanel");
                    bool docDrawerOpen = docDrawer?.IsEffectivelyVisible == true;
                    window.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape, Source = window });
                    window.UpdateLayout();
                    Dispatcher.UIThread.RunJobs();
                    bool docDrawerClosedOnEsc = docDrawer?.IsEffectivelyVisible != true;

                    // Toggle SetSectionReviewed via form & section model cleanly.
                    var section = shell.Navigation.SelectedSection!;
                    shell.Workspace.Form!.SetSectionReviewed(section.Id, true);
                    bool reviewedAfterSet = section.Coverage == Configurator.Presentation.SectionReviewCoverage.Reviewed;

                    return (
                        valid,
                        hasErrorBanner: errorBanner?.IsEffectivelyVisible == true,
                        layoutAfterF8,
                        f8FocusedFieldControl: focusedAfterF8?.DataContext is FieldViewModel,
                        discoveryVisibleOnCtrlK,
                        docDrawerOpen,
                        docDrawerClosedOnEsc,
                        layoutAtEnd: shell.Navigation.LayoutMode,
                        reviewedAfterSet);
                }
                finally { window.Close(); }
            }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

            await Assert.That(result.valid).IsFalse();
            await Assert.That(result.hasErrorBanner).IsTrue();
            await Assert.That(result.layoutAfterF8).IsEqualTo(ConfigureLayoutMode.AllSections);
            await Assert.That(result.f8FocusedFieldControl).IsTrue();
            await Assert.That(result.discoveryVisibleOnCtrlK).IsTrue();
            await Assert.That(result.docDrawerOpen).IsTrue();
            await Assert.That(result.docDrawerClosedOnEsc).IsTrue();
            await Assert.That(result.layoutAtEnd).IsEqualTo(ConfigureLayoutMode.AllSections);
            await Assert.That(result.reviewedAfterSet).IsTrue();
        }
        finally { await Task.Run(session.Dispose); }
    }

    [Test]
    [NotInParallel]
    public async Task InPlaceSettingsReturnsToConfigurationWithAnswersIntactAndAppliesEventPresetsAndCustomHsl()
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(DesktopNavigationTests));
        try
        {
            var observed = await session.Dispatch(() =>
            {
                using var theme = new DesktopTheme(Application.Current!, _ => () => { });
                var messenger = new CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger();
                var dialogs = new NoDialogs();
                using var loader = new DefinitionLoaderViewModel(messenger, dialogs);
                using var workspace = new FormWorkspaceViewModel(messenger);
                using var publisher = new ArtifactPublisherViewModel(messenger, dialogs, new Configurator.Artifacts.ProtectedArtifactWriter());
                using var importReview = new DesktopImportReviewViewModel(messenger, dialogs, publisher);
                using var shell = new DesktopShellViewModel(messenger, loader, workspace, publisher, importReview, theme: theme);
                shell.Loader.LoadDefinition(DesktopApplicationTests.DefinitionWithExperiences());

                var window = new MainWindow { DataContext = shell, Width = 1280, Height = 820 };
                try
                {
                    window.Show();
                    window.UpdateLayout();
                    Dispatcher.UIThread.RunJobs();

                    var nameField = shell.Workspace.Fields.Single(f => f.Id == "name");
                    nameField.Text = "preserved-desktop-answer";
                    long revisionBefore = shell.Workspace.Form!.InputRevision;

                    shell.Navigation.SettingsCommand.Execute(null);
                    window.UpdateLayout();
                    Dispatcher.UIThread.RunJobs();

                    var returnButton = window.FindNamedControl<Button>("ReturnToConfigurationButton");
                    bool returnVisible = returnButton?.IsEffectivelyVisible == true;
                    returnButton?.Command?.Execute(returnButton.CommandParameter);
                    window.UpdateLayout();
                    Dispatcher.UIThread.RunJobs();

                    DesktopWorkspaceStage stageAfterReturn = shell.Navigation.Stage;
                    string? answerAfterReturn = shell.Workspace.Form.Snapshot().Values["name"].GetString();
                    long revisionAfterReturn = shell.Workspace.Form.InputRevision;

                    shell.Navigation.SettingsCommand.Execute(null);
                    window.UpdateLayout();
                    Dispatcher.UIThread.RunJobs();

                    var modeSelector = window.FindNamedControl<ComboBox>("SettingsAppearanceModeSelector");
                    var presetGallery = window.FindNamedControl<ItemsControl>("SettingsPresetGallery");
                    var naturalHexBox = window.FindNamedControl<TextBox>("SettingsCustomNaturalHex");
                    var brandHexBox = window.FindNamedControl<TextBox>("SettingsCustomBrandHex");

                    int modeCount = modeSelector?.ItemCount ?? 0;
                    int presetCount = presetGallery?.ItemCount ?? 0;

                    var accentColors = new List<Color>();
                    if (presetGallery is not null)
                    {
                        foreach (Button presetButton in presetGallery.GetVisualDescendants().OfType<Button>().Take(ThemePresetCatalog.All.Count))
                        {
                            presetButton.Command?.Execute(presetButton.CommandParameter);
                            window.UpdateLayout();
                            Dispatcher.UIThread.RunJobs();
                            if (window.TryFindResource("ConfiguratorAccentBrush", window.ActualThemeVariant, out object? brush) &&
                                brush is SolidColorBrush solid)
                            {
                                accentColors.Add(solid.Color);
                            }
                        }
                    }

                    if (naturalHexBox is not null && brandHexBox is not null)
                    {
                        naturalHexBox.Text = "#F4EFE6";
                        brandHexBox.Text = "#0E7490";
                        window.UpdateLayout();
                        Dispatcher.UIThread.RunJobs();
                    }

                    Color? customAccent = window.TryFindResource("ConfiguratorAccentBrush", window.ActualThemeVariant, out object? customBrush) &&
                        customBrush is SolidColorBrush customSolid
                        ? customSolid.Color
                        : null;

                    if (modeSelector is { ItemCount: >= 5 })
                    {
                        modeSelector.SelectedIndex = 3; // Light High Contrast
                        window.UpdateLayout();
                        Dispatcher.UIThread.RunJobs();
                    }
                    ThemeVariant? lightHcVariant = window.ActualThemeVariant;
                    Color? lightHcCanvas = window.TryFindResource("ConfiguratorCanvasBrush", window.ActualThemeVariant, out object? lhcCanvas) &&
                        lhcCanvas is SolidColorBrush lhcCanvasBrush ? lhcCanvasBrush.Color : null;
                    Color? lightHcText = window.TryFindResource("ConfiguratorTextBrush", window.ActualThemeVariant, out object? lhcText) &&
                        lhcText is SolidColorBrush lhcTextBrush ? lhcTextBrush.Color : null;

                    if (modeSelector is { ItemCount: >= 5 })
                    {
                        modeSelector.SelectedIndex = 4; // Dark High Contrast
                        window.UpdateLayout();
                        Dispatcher.UIThread.RunJobs();
                    }
                    ThemeVariant? darkHcVariant = window.ActualThemeVariant;

                    return (
                        returnVisible,
                        stageAfterReturn,
                        answerAfterReturn,
                        revisionPreserved: revisionBefore == revisionAfterReturn,
                        modeCount,
                        presetCount,
                        distinctPresetAccents: accentColors.Distinct().Count(),
                        customApplied: customAccent is not null,
                        lightHcDistinct: lightHcVariant is not null &&
                            lightHcVariant != ThemeVariant.Light &&
                            lightHcVariant != DesktopTheme.HighContrast &&
                            string.Equals(lightHcVariant.Key?.ToString(), "ConfiguratorLightHighContrast", StringComparison.Ordinal),
                        lightHcContrastOk: lightHcCanvas is not null && lightHcText is not null && Contrast(lightHcText.Value, lightHcCanvas.Value) >= 7.0,
                        darkHcActive: darkHcVariant == DesktopTheme.HighContrast);
                }
                finally
                {
                    DesktopDensity.Apply(Application.Current, WorkspaceDensity.Comfortable);
                    window.Close();
                }
            }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

            await Assert.That(observed.returnVisible).IsTrue();
            await Assert.That(observed.stageAfterReturn).IsEqualTo(DesktopWorkspaceStage.Configure);
            await Assert.That(observed.answerAfterReturn).IsEqualTo("preserved-desktop-answer");
            await Assert.That(observed.revisionPreserved).IsTrue();
            await Assert.That(observed.modeCount).IsEqualTo(5);
            await Assert.That(observed.presetCount).IsGreaterThanOrEqualTo(ThemePresetCatalog.All.Count);
            await Assert.That(observed.distinctPresetAccents).IsGreaterThanOrEqualTo(4);
            await Assert.That(observed.customApplied).IsTrue();
            await Assert.That(observed.lightHcDistinct).IsTrue();
            await Assert.That(observed.lightHcContrastOk).IsTrue();
            await Assert.That(observed.darkHcActive).IsTrue();
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
        static double Luminance(Color color) => 0.2126 * Channel(color.R) + 0.7152 * Channel(color.G) + 0.0722 * Channel(color.B);
        double a = Luminance(first), b = Luminance(second);
        return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
    }

    private sealed class NoDialogs : Configurator.Desktop.Services.IStorageDialogService
    {
        public Task<Configurator.Desktop.Services.StorageSelection<Uri>> SelectAsync(DesktopPathSelectionKind kind, CancellationToken cancellationToken = default) =>
            Task.FromResult(new Configurator.Desktop.Services.StorageSelection<Uri>(Configurator.Desktop.Services.StorageSelectionStatus.Cancelled));
        public Task<Configurator.Desktop.Services.StorageSelection<Configurator.Artifacts.BoundedFileInput>> ReadImportAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new Configurator.Desktop.Services.StorageSelection<Configurator.Artifacts.BoundedFileInput>(Configurator.Desktop.Services.StorageSelectionStatus.Cancelled));
    }
}
