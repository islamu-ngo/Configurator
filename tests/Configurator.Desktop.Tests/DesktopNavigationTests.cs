namespace Configurator.Desktop.Tests;

using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using System.Text;
using System.Text.Json.Nodes;
using Configurator.Desktop.ViewModels;
using Configurator.Desktop.Views;

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
                        draft: port.Text);
                }
                finally { window.Close(); }
            }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));
            await Assert.That(result.sectionCount).IsEqualTo(1);
            await Assert.That(result.selected).IsTrue();
            await Assert.That(result.revision).IsTrue();
            await Assert.That(result.sameEditor).IsTrue();
            await Assert.That(result.draft).IsEqualTo("unfinished-number");
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

                    shell.Navigation.ConfigureMode = DesktopConfigureViewMode.Compact;
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
}
