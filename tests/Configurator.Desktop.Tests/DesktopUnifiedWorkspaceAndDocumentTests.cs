namespace Configurator.Desktop.Tests;

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Configurator.Core.Definitions;
using Configurator.Desktop.Resources;
using Configurator.Desktop.ViewModels;
using Configurator.Desktop.Views;
using Configurator.Presentation;

public sealed class DesktopUnifiedWorkspaceAndDocumentTests
{
    public static AppBuilder BuildAvaloniaApp() => DesktopApplicationTests.BuildAvaloniaApp();

    [Test]
    [NotInParallel]
    public async Task UnifiedAllSectionsVirtualizesBoundedSlotExcludesSecretsAndRoundTripsWithoutOmittedMaterialization()
    {
        byte[] definition = RichCompactDefinitionBytes();
        var session = HeadlessUnitTestSession.StartNew(typeof(DesktopUnifiedWorkspaceAndDocumentTests));
        try
        {
            var result = await session.Dispatch(() =>
            {
                using var shell = DesktopTestContext.Root();
                if (!shell.Loader.LoadDefinition(definition))
                    throw new InvalidOperationException("LoadDefinition failed: " + shell.Status);
                var form = shell.Workspace.Form!;

                shell.Workspace.Fields.Single(f => f.Id == "apiSecret").Text = "synthetic-secret-compact-91";
                form.SetValue(DefinitionPath.Parse("/namedSecrets"), JsonSerializer.SerializeToElement(
                    new Dictionary<string, string> { ["synthetic-private-map-key-91"] = "secret-entry" }));

                var window = new MainWindow { DataContext = shell, Width = 1280, Height = 820 };
                try
                {
                    window.Show();
                    window.UpdateLayout();
                    Dispatcher.UIThread.RunJobs();

                    shell.Navigation.LayoutMode = ConfigureLayoutMode.AllSections;
                    window.UpdateLayout();
                    Dispatcher.UIThread.RunJobs();

                    var listBox = window.GetVisualDescendants().OfType<ListBox>()
                        .Single(control => control.Name == "AllSectionsFieldsList");
                    bool usesVirtualizingPanel = listBox.GetVisualDescendants()
                        .OfType<VirtualizingStackPanel>().Any();
                    var boundedSlot = window.FindNamedControl<Grid>("BoundedConfigureSlot")!;

                    shell.Navigation.SearchText = "synthetic-secret-compact-91";
                    int secretValueMatches = shell.Navigation.DiscoveryTotal;

                    shell.Navigation.SearchText = "synthetic-private-map-key-91";
                    int secretMapKeyMatches = shell.Navigation.DiscoveryTotal;

                    shell.Navigation.SearchText = string.Empty;
                    shell.Navigation.CloseCommandPaletteCommand.Execute(null);
                    var secretField = shell.Navigation.AllSectionsFields.Single(f => f.Id == "apiSecret");
                    bool secretWireSuppressed = secretField.RequiresMasking &&
                        !secretField.HasCopyableWireToken &&
                        secretField.PasswordChar != '\0';

                    var allSectionsHost = shell.Navigation.AllSectionsFields.Single(f => f.Id == "host");
                    allSectionsHost.Text = "compact.example.org";
                    bool edited = form.Snapshot().Values["host"].GetString() == "compact.example.org";

                    shell.Navigation.LayoutMode = ConfigureLayoutMode.SectionBySection;
                    window.UpdateLayout();
                    Dispatcher.UIThread.RunJobs();

                    var hostField = shell.Workspace.Fields.Single(f => f.Id == "host");
                    var snapshot = form.Snapshot();
                    return (
                        usesVirtualizingPanel,
                        finiteHeight: boundedSlot.Bounds.Height > 0 && boundedSlot.Bounds.Height <= 820,
                        secretValueMatches,
                        secretMapKeyMatches,
                        secretWireSuppressed,
                        edited,
                        guidedHostText: hostField.Text,
                        snapshotHost: snapshot.Values.TryGetValue("host", out JsonElement hostVal) ? hostVal.GetString() : null,
                        optionalRemainsOmitted: !snapshot.Values.ContainsKey("optionalTag"));
                }
                finally { window.Close(); }
            }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

            await Assert.That(result.usesVirtualizingPanel).IsTrue();
            await Assert.That(result.finiteHeight).IsTrue();
            await Assert.That(result.secretValueMatches).IsEqualTo(0);
            await Assert.That(result.secretMapKeyMatches).IsEqualTo(0);
            await Assert.That(result.secretWireSuppressed).IsTrue();
            await Assert.That(result.edited).IsTrue();
            await Assert.That(result.guidedHostText).IsEqualTo("compact.example.org");
            await Assert.That(result.snapshotHost).IsEqualTo("compact.example.org");
            await Assert.That(result.optionalRemainsOmitted).IsTrue();
        }
        finally { await Task.Run(session.Dispose); }
    }

    [Test]
    public async Task DocumentEditorPreviewsDiffBeforeApplyAndRejectsStaleWorkspaceGeneration()
    {
        using var shell = DesktopTestContext.Root();
        await Assert.That(shell.Loader.LoadDefinition(RichCompactDefinitionBytes())).IsTrue().Because(shell.Status);
        shell.Navigation.IsDocumentOpen = true;

        var doc = shell.DocumentEditor;
        string initialHost = shell.Workspace.Form!.Snapshot().Values.TryGetValue("host", out JsonElement v)
            ? v.GetString() ?? "" : "";
        JsonObject editedJson = JsonNode.Parse(doc.DocumentText)!.AsObject();
        JsonObject editedValues = editedJson["values"]?.AsObject() ?? (JsonObject)(editedJson["values"] = new JsonObject());
        editedValues["host"] = "doc-preview.example.org";
        editedValues["optionalTag"] = "canary";
        doc.DocumentText = editedJson.ToJsonString();

        bool previewOk = doc.PreviewChanges();
        string? hostBeforeApply = shell.Workspace.Form.Snapshot().Values.TryGetValue("host", out JsonElement beforeEl)
            ? beforeEl.GetString() : null;
        int setCount = doc.SetCount;
        bool hasHostChange = doc.PendingChanges.Any(c => c.Path == DefinitionPath.Parse("/host") &&
                                                         c.Kind == DefinitionDocumentChangeKind.Set);

        await Assert.That(previewOk).IsTrue();
        await Assert.That(doc.HasPreview).IsTrue();
        await Assert.That(setCount).IsGreaterThanOrEqualTo(2);
        await Assert.That(hasHostChange).IsTrue();
        await Assert.That(hostBeforeApply ?? "").IsEqualTo(initialHost);

        bool applied = doc.ApplyPreview();
        await Assert.That(applied).IsTrue();
        await Assert.That(doc.LastErrorCode).IsEqualTo("document-applied");
        await Assert.That(shell.Workspace.Form.Snapshot().Values["host"].GetString()).IsEqualTo("doc-preview.example.org");
        await Assert.That(shell.Workspace.Form.Snapshot().Values["optionalTag"].GetString()).IsEqualTo("canary");

        // Now modify text, preview, then mutate workspace externally to invalidate generation/revision.
        JsonObject secondJson = JsonNode.Parse(doc.DocumentText)!.AsObject();
        JsonObject secondValues = secondJson["values"]?.AsObject() ?? (JsonObject)(secondJson["values"] = new JsonObject());
        secondValues["host"] = "stale-overwrite.example.org";
        doc.DocumentText = secondJson.ToJsonString();
        await Assert.That(doc.PreviewChanges()).IsTrue();

        shell.Workspace.Fields.Single(f => f.Id == "host").Text = "guided-won.example.org";
        bool staleApplied = doc.ApplyPreview();
        await Assert.That(staleApplied).IsFalse();
        await Assert.That(doc.LastErrorCode).IsEqualTo("document-stale");
        await Assert.That(shell.Workspace.Form.Snapshot().Values["host"].GetString()).IsEqualTo("guided-won.example.org");
    }

    [Test]
    [NotInParallel]
    public async Task InspectorDisplaysPublicSchemaMetadataOnlyWithoutAnswersAndPreservesEditorFocus()
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(DesktopUnifiedWorkspaceAndDocumentTests));
        try
        {
            var result = await session.Dispatch(() =>
            {
                using var shell = DesktopTestContext.Root();
                if (!shell.Loader.LoadDefinition(RichCompactDefinitionBytes()))
                    throw new InvalidOperationException("LoadDefinition failed: " + shell.Status);
                shell.Workspace.Fields.Single(f => f.Id == "host").Text = "public-answer-must-not-appear";
                shell.Workspace.Fields.Single(f => f.Id == "apiSecret").Text = "secret-answer-must-not-appear";

                var window = new MainWindow { DataContext = shell, Width = 1360, Height = 820 };
                try
                {
                    window.Show();
                    window.UpdateLayout();
                    Dispatcher.UIThread.RunJobs();

                    var hostEditor = window.GetVisualDescendants().OfType<TextBox>()
                        .First(tb => tb.DataContext is Configurator.Desktop.ViewModels.Fields.FieldViewModel { Id: "host" });
                    hostEditor.Focus();
                    Dispatcher.UIThread.RunJobs();
                    var focusedBefore = window.FocusManager?.GetFocusedElement();

                    shell.Inspector.InspectPath(DefinitionPath.Parse("/host"));
                    window.RaiseEvent(new KeyEventArgs
                    {
                        RoutedEvent = InputElement.KeyDownEvent,
                        Key = Key.F2,
                        KeyModifiers = KeyModifiers.None,
                        Source = window
                    });
                    window.UpdateLayout();
                    Dispatcher.UIThread.RunJobs();

                    var focusedAfter = window.FocusManager?.GetFocusedElement();
                    var inspectorPane = window.FindNamedControl<Control>("InspectorPane");
                    string[] hostInspectorTexts = inspectorPane is null
                        ? []
                        : inspectorPane.GetVisualDescendants().OfType<TextBlock>()
                            .Where(tb => !string.IsNullOrWhiteSpace(tb.Text))
                            .Select(tb => tb.Text!)
                            .ToArray();

                    shell.Inspector.InspectPath(DefinitionPath.Parse("/apiSecret"));
                    window.UpdateLayout();
                    Dispatcher.UIThread.RunJobs();
                    string[] secretInspectorTexts = inspectorPane is null
                        ? []
                        : inspectorPane.GetVisualDescendants().OfType<TextBlock>()
                            .Where(tb => !string.IsNullOrWhiteSpace(tb.Text))
                            .Select(tb => tb.Text!)
                            .ToArray();

                    string combined = string.Join("\n", hostInspectorTexts.Concat(secretInspectorTexts));
                    return (
                        isOpen: shell.Inspector.IsOpen,
                        paneVisible: inspectorPane?.IsEffectivelyVisible == true,
                        paneWidthBounded: inspectorPane is not null && inspectorPane.Bounds.Width is >= 280 and <= 620,
                        focusPreserved: ReferenceEquals(focusedBefore, hostEditor) && ReferenceEquals(focusedAfter, hostEditor),
                        hasHostInstructions: combined.Contains("Public host identifier", StringComparison.Ordinal),
                        hasUnitsOrBounds: combined.Contains("minLength", StringComparison.OrdinalIgnoreCase) ||
                                          combined.Contains('3', StringComparison.Ordinal),
                        hasOutputBinding: combined.Contains("HOST_NAME", StringComparison.Ordinal) ||
                                          combined.Contains("config", StringComparison.Ordinal),
                        leakedPublicAnswer: combined.Contains("public-answer-must-not-appear", StringComparison.Ordinal),
                        leakedSecretAnswer: combined.Contains("secret-answer-must-not-appear", StringComparison.Ordinal));
                }
                finally { window.Close(); }
            }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

            await Assert.That(result.isOpen).IsTrue();
            await Assert.That(result.paneVisible).IsTrue();
            await Assert.That(result.paneWidthBounded).IsTrue();
            await Assert.That(result.focusPreserved).IsTrue();
            await Assert.That(result.hasHostInstructions).IsTrue();
            await Assert.That(result.hasUnitsOrBounds).IsTrue();
            await Assert.That(result.hasOutputBinding).IsTrue();
            await Assert.That(result.leakedPublicAnswer).IsFalse();
            await Assert.That(result.leakedSecretAnswer).IsFalse();
        }
        finally { await Task.Run(session.Dispose); }
    }

    [Test]
    [NotInParallel]
    public async Task DiscoveryFilterChipsAndCommandPaletteFilterAndExecuteCommands()
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(DesktopUnifiedWorkspaceAndDocumentTests));
        try
        {
            var result = await session.Dispatch(() =>
            {
                using var shell = DesktopTestContext.Root();
                if (!shell.Loader.LoadDefinition(RichCompactDefinitionBytes()))
                    throw new InvalidOperationException("LoadDefinition failed: " + shell.Status);
                shell.Workspace.Fields.Single(f => f.Id == "host").Text = "invalid-short-x";
                shell.Workspace.Validate();

                var window = new MainWindow { DataContext = shell, Width = 1280, Height = 820 };
                try
                {
                    window.Show();
                    window.UpdateLayout();
                    Dispatcher.UIThread.RunJobs();

                    shell.Navigation.SetDiscoveryFilterCommand.Execute(DefinitionDiscoveryFilter.MissingRequired);
                    bool hasMissing = shell.Navigation.DiscoveryResults.Any(m => m.Entry.FieldId == "requiredEndpoint");

                    shell.Navigation.SetDiscoveryFilterCommand.Execute(DefinitionDiscoveryFilter.Modified);
                    bool hasModifiedHost = shell.Navigation.DiscoveryResults.Any(m => m.Entry.FieldId == "host");

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

                    bool paletteOpened = shell.Navigation.IsCommandPaletteOpen;
                    var namingCmd = shell.Navigation.DiscoveryCommands.FirstOrDefault(c => c.Id == WorkspaceCommandId.ToggleNamingMode);
                    if (namingCmd is not null)
                        shell.ExecuteWorkspaceCommandCommand.Execute(namingCmd);

                    bool technicalAfterCommand = shell.Workspace.IsTechnicalNaming;

                    var inspectorCmd = shell.Navigation.DiscoveryCommands.FirstOrDefault(c => c.Id == WorkspaceCommandId.ToggleInspector);
                    if (inspectorCmd is not null)
                        shell.ExecuteWorkspaceCommandCommand.Execute(inspectorCmd);

                    return (hasMissing, hasModifiedHost, paletteOpened, technicalAfterCommand, inspectorOpened: shell.Inspector.IsOpen);
                }
                finally { window.Close(); }
            }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

            await Assert.That(result.hasMissing).IsTrue();
            await Assert.That(result.hasModifiedHost).IsTrue();
            await Assert.That(result.paletteOpened).IsTrue();
            await Assert.That(result.technicalAfterCommand).IsTrue();
            await Assert.That(result.inspectorOpened).IsTrue();
        }
        finally { await Task.Run(session.Dispose); }
    }

    [Test]
    [NotInParallel]
    public async Task ArtifactPublicationViewSeparatesReviewPreparationFromCreateOnlySave()
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(DesktopUnifiedWorkspaceAndDocumentTests));
        try
        {
            var result = await session.Dispatch(() =>
            {
                using var shell = DesktopTestContext.Root();
                shell.Loader.LoadDefinition(RichCompactDefinitionBytes());
                shell.Navigation.ReviewCommand.Execute(null);
                var window = new MainWindow { DataContext = shell, Width = 1280, Height = 820 };
                try
                {
                    window.Show();
                    window.UpdateLayout();
                    Dispatcher.UIThread.RunJobs();

                    var reviewCard = window.FindNamedControl<Border>("ReviewPrepareCard");
                    var saveCard = window.FindNamedControl<Border>("CreateOnlySaveCard");
                    var prepareButton = window.GetNamedControl<Button>("PrepareSelection");
                    var saveButton = window.GetNamedControl<Button>("SaveSelection");

                    bool prepareInsideReview = reviewCard is not null &&
                        prepareButton.GetVisualAncestors().Contains(reviewCard);
                    bool saveInsideCreateOnly = saveCard is not null &&
                        saveButton.GetVisualAncestors().Contains(saveCard);
                    bool separateCards = reviewCard is not null && saveCard is not null &&
                        !ReferenceEquals(reviewCard, saveCard);

                    return (prepareInsideReview, saveInsideCreateOnly, separateCards, saveDisabledBeforePrepare: !saveButton.IsEffectivelyEnabled);
                }
                finally { window.Close(); }
            }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

            await Assert.That(result.prepareInsideReview).IsTrue();
            await Assert.That(result.saveInsideCreateOnly).IsTrue();
            await Assert.That(result.separateCards).IsTrue();
            await Assert.That(result.saveDisabledBeforePrepare).IsTrue();
        }
        finally { await Task.Run(session.Dispose); }
    }

    private static byte[] RichCompactDefinitionBytes()
    {
        JsonObject root = new()
        {
            ["$schema"] = "urn:islamu:configurator:authoring:v0.0.1",
            ["identity"] = new JsonObject
            {
                ["familyId"] = "f8d0e542-5112-4090-a7d0-156f3cf5c888",
                ["revision"] = 1
            },
            ["project"] = new JsonObject
            {
                ["id"] = "compact-workbench",
                ["name"] = "Compact Workbench"
            },
            ["applicationVersions"] = new JsonArray("1.0"),
            ["configuration"] = new JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = false,
                ["x-configurator"] = new JsonObject
                {
                    ["sensitivity"] = "public"
                },
                ["properties"] = new JsonObject
                {
                    ["host"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["title"] = "Host name",
                        ["minLength"] = 3,
                        ["default"] = "localhost",
                        ["description"] = "Primary service hostname.",
                        ["x-configurator"] = new JsonObject
                        {
                            ["sensitivity"] = "public",
                            ["detail"] = "basic",
                            ["guidance"] = new JsonObject
                            {
                                ["instructions"] = "Public host identifier for generated endpoints."
                            }
                        }
                    },
                    ["requiredEndpoint"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["title"] = "Required endpoint",
                        ["x-configurator"] = new JsonObject { ["sensitivity"] = "public", ["detail"] = "basic" }
                    },
                    ["optionalTag"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["title"] = "Optional release tag",
                        ["x-configurator"] = new JsonObject { ["sensitivity"] = "public", ["detail"] = "basic" }
                    },
                    ["mode"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["title"] = "Runtime mode",
                        ["default"] = "embedded",
                        ["enum"] = new JsonArray("embedded", "cluster"),
                        ["x-configurator"] = new JsonObject { ["sensitivity"] = "public", ["detail"] = "basic" }
                    },
                    ["apiSecret"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["title"] = "API signing key",
                        ["x-configurator"] = new JsonObject { ["sensitivity"] = "secret", ["detail"] = "basic" }
                    },
                    ["namedSecrets"] = new JsonObject
                    {
                        ["type"] = "object",
                        ["title"] = "Per-tenant secret map",
                        ["additionalProperties"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["x-configurator"] = new JsonObject { ["sensitivity"] = "secret" }
                        },
                        ["x-configurator"] = new JsonObject { ["sensitivity"] = "secret", ["detail"] = "basic" }
                    }
                },
                ["required"] = new JsonArray("requiredEndpoint")
            },
            ["presentation"] = new JsonObject
            {
                ["defaultJourney"] = "default",
                ["navigation"] = "sections",
                ["sections"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["id"] = "general",
                        ["title"] = "General Settings",
                        ["pointers"] = new JsonArray("/host", "/requiredEndpoint", "/optionalTag", "/mode", "/apiSecret", "/namedSecrets")
                    }
                },
                ["journeys"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["id"] = "default",
                        ["title"] = "Standard",
                        ["detail"] = "basic",
                        ["sections"] = new JsonArray("general")
                    }
                }
            },
            ["outputs"] = new JsonObject
            {
                ["config.json"] = new JsonObject
                {
                    ["id"] = "config",
                    ["format"] = "json",
                    ["bindings"] = new JsonObject
                    {
                        ["/HOST_NAME"] = new JsonObject { ["scope"] = "root", ["path"] = "/host" }
                    }
                }
            }
        };
        return Encoding.UTF8.GetBytes(root.ToJsonString());
    }

    [Test]
    [NotInParallel]
    public async Task LocalizedStringsFixWireKeyCopiedStatusRemovesTextBlockMnemonicsAndMapsInputsChanged()
    {
        var enStrings = new DesktopStrings("en");
        var arStrings = new DesktopStrings("ar");
        string enWireCopied = enStrings.Read("WireKeyCopiedStatus");
        string arWireCopied = arStrings.Read("WireKeyCopiedStatus");
        string enOptional = enStrings.Read("OptionalEditors");
        string arOptional = arStrings.Read("OptionalEditors");

        await Assert.That(enWireCopied).IsNotEqualTo("WireKeyCopiedStatus");
        await Assert.That(string.IsNullOrWhiteSpace(enWireCopied)).IsFalse();
        await Assert.That(arWireCopied).IsNotEqualTo("WireKeyCopiedStatus");
        await Assert.That(arWireCopied.Any(c => c is >= '\u0600' and <= '\u06ff')).IsTrue();
        await Assert.That(enOptional.Contains('_', StringComparison.Ordinal)).IsFalse();
        await Assert.That(arOptional.Contains('_', StringComparison.Ordinal)).IsFalse();

        var session = HeadlessUnitTestSession.StartNew(typeof(DesktopUnifiedWorkspaceAndDocumentTests));
        try
        {
            var observed = await session.Dispatch(() =>
            {
                using var shell = DesktopTestContext.Root();
                shell.Loader.LoadDefinition(DesktopApplicationTests.DefinitionWithExperiences());
                var window = new MainWindow { DataContext = shell, Width = 1280, Height = 820 };
                try
                {
                    window.Show();
                    window.UpdateLayout();
                    Dispatcher.UIThread.RunJobs();

                    shell.Workspace.Fields.Single(f => f.Id == "name").Text = "trigger-inputs-changed";
                    shell.Navigation.OpenCommand.Execute(null);
                    window.UpdateLayout();
                    Dispatcher.UIThread.RunJobs();

                    TextBlock statusTextBlock = window.GetNamedControl<TextBlock>("Status");
                    string openStatusText = statusTextBlock.Text ?? string.Empty;
                    bool politeLive = AutomationProperties.GetLiveSetting(statusTextBlock) == AutomationLiveSetting.Polite;

                    shell.Navigation.ConfigureCommand.Execute(null);
                    shell.Navigation.LayoutMode = ConfigureLayoutMode.AllSections;
                    window.UpdateLayout();
                    Dispatcher.UIThread.RunJobs();

                    string[] visibleTexts = window.GetVisualDescendants().OfType<TextBlock>()
                        .Where(tb => tb.IsEffectivelyVisible &&
                            tb is not AccessText &&
                            !tb.Classes.Contains("code-font") &&
                            !string.IsNullOrEmpty(tb.Text))
                        .Select(tb => tb.Text!)
                        .ToArray();

                    return (
                        machineStatus: shell.Status,
                        openStatusText,
                        politeLive,
                        showsRawInputsChanged: visibleTexts.Any(t => string.Equals(t, "inputs-changed", StringComparison.Ordinal)),
                        showsUnderscoreMnemonic: visibleTexts.Any(t => t.StartsWith('_') || t.Contains(" _", StringComparison.Ordinal)));
                }
                finally { window.Close(); }
            }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

            await Assert.That(observed.machineStatus).IsEqualTo("inputs-changed");
            await Assert.That(string.IsNullOrWhiteSpace(observed.openStatusText)).IsFalse();
            await Assert.That(observed.openStatusText).IsNotEqualTo("inputs-changed");
            await Assert.That(observed.politeLive).IsTrue();
            await Assert.That(observed.showsRawInputsChanged).IsFalse();
            await Assert.That(observed.showsUnderscoreMnemonic).IsFalse();
        }
        finally { await Task.Run(session.Dispose); }
    }

    [Test]
    [NotInParallel]
    public async Task SectionRailDeduplicatesReviewButtonsAndTabbedEndDockSwitchesBetweenInspectorAndDocument()
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(DesktopUnifiedWorkspaceAndDocumentTests));
        try
        {
            var observed = await session.Dispatch(() =>
            {
                using var shell = DesktopTestContext.Root();
                shell.Loader.LoadDefinition(RichCompactDefinitionBytes());
                var window = new MainWindow { DataContext = shell, Width = 1360, Height = 820 };
                try
                {
                    window.Show();
                    window.UpdateLayout();
                    Dispatcher.UIThread.RunJobs();

                    var rail = window.GetNamedControl<ListBox>("SectionRail");
                    int railButtonCount = rail.GetVisualDescendants().OfType<Button>().Count();
                    var headerReviewButton = window.FindNamedControl<Button>("SectionReviewButton");

                    shell.ToggleInspectorCommand.Execute(null);
                    window.UpdateLayout();
                    Dispatcher.UIThread.RunJobs();

                    var endDock = window.FindNamedControl<Control>("EndDockPane");
                    var inspectorTab = window.FindNamedControl<Button>("InspectorDockTab");
                    var documentTab = window.FindNamedControl<Button>("DocumentDockTab");
                    var inspectorPane = window.FindNamedControl<Control>("InspectorPane");

                    double defaultEndWidth = endDock?.Bounds.Width ?? 0d;

                    documentTab?.Command?.Execute(documentTab.CommandParameter);
                    window.UpdateLayout();
                    Dispatcher.UIThread.RunJobs();
                    var documentDockPanel = window.FindNamedControl<Control>("DocumentDockPanel");
                    bool documentDockedBesideForm = documentDockPanel?.IsEffectivelyVisible == true &&
                        window.GetVisualDescendants().OfType<Control>().Any(c => c.Name == "Fields" && c.IsEffectivelyVisible);

                    inspectorTab?.Command?.Execute(inspectorTab.CommandParameter);
                    window.UpdateLayout();
                    Dispatcher.UIThread.RunJobs();
                    bool inspectorRestored = inspectorPane?.IsEffectivelyVisible == true;

                    return (
                        railButtonCount,
                        hasSingleHeaderReview: headerReviewButton is not null,
                        hasEndDock: endDock is not null,
                        hasTabs: inspectorTab is not null && documentTab is not null,
                        defaultEndWidth,
                        documentDockedBesideForm,
                        inspectorRestored);
                }
                finally { window.Close(); }
            }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

            await Assert.That(observed.railButtonCount).IsEqualTo(0);
            await Assert.That(observed.hasSingleHeaderReview).IsTrue();
            await Assert.That(observed.hasEndDock).IsTrue();
            await Assert.That(observed.hasTabs).IsTrue();
            await Assert.That(observed.defaultEndWidth).IsBetween(300d, 620d);
            await Assert.That(observed.documentDockedBesideForm).IsTrue();
            await Assert.That(observed.inspectorRestored).IsTrue();
        }
        finally { await Task.Run(session.Dispose); }
    }
}
