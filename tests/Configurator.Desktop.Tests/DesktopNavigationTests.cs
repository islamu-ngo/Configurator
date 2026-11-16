namespace Configurator.Desktop.Tests;

using System.Text;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Configurator.Desktop.ViewModels;
using Configurator.Desktop.ViewModels.Fields;
using Configurator.Desktop.Views;
using Configurator.Presentation;

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
                    double railWidth = rail.Width;

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

            await Assert.That(result.railWidth).IsEqualTo(300d);
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

                    // Ctrl+Shift+E opens DocumentDrawerOverlay without losing AllSections.
                    window.RaiseEvent(new KeyEventArgs
                    {
                        RoutedEvent = InputElement.KeyDownEvent,
                        Key = Key.E,
                        KeyModifiers = cmdMod | KeyModifiers.Shift,
                        Source = window
                    });
                    window.UpdateLayout();
                    Dispatcher.UIThread.RunJobs();
                    var docDrawer = window.FindNamedControl<Control>("DocumentDrawerOverlay");
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
}
