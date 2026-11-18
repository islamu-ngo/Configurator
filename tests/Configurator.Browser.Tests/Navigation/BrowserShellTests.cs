namespace Configurator.Browser.Tests;

using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;
using Bunit;
using Configurator.Browser.Components.Configuration;
using Configurator.Browser.Layout;
using Configurator.Browser.Pages;
using Configurator.Browser.Services;
using Configurator.Browser.Services.Docking;
using Configurator.Core.Definitions;
using Configurator.Presentation;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

public sealed class BrowserShellTests
{
    [Test]
    public async Task ClearingWorkspaceRetiresOldConsentActionsAndCreatesWorkingRecovery()
    {
        await using var context = BrowserFixture.Context();
        var editor = context.Render<ConfigurationEditor>(parameters =>
            parameters.Add(item => item.Definition, BrowserFixture.Definition));
        var oldConsent = editor.FindComponents<ConfigurationField>().Single(field => field.Instance.Path.SequenceEqual(["credential"])).Instance.RequestSecretConsent;
        await BrowserFixture.SelectPanelAsync(editor, "prepare");
        await editor.Find("#clear-workspace").ClickAsync(new MouseEventArgs());
        await editor.InvokeAsync(editor.Instance.BeginAsync);
        await editor.InvokeAsync(() => oldConsent.InvokeAsync());
        await Assert.That(editor.Find("details[data-secret-consent]").HasAttribute("open")).IsFalse();
        await editor.Find("[data-field='credential'] [data-action='field-secret-consent']").ClickAsync(new MouseEventArgs());
        await Assert.That(editor.Find("details[data-secret-consent]").HasAttribute("open")).IsTrue();
        await Assert.That(editor.Find("#secret-consent").HasAttribute("checked")).IsFalse();
    }

    [Test]
    public async Task SearchResultCapturedBeforeAnAnswerEditCannotNavigate()
    {
        await using var context = BrowserFixture.Context();
        var shell = context.Render<NavMenu>();
        var editor = context.Render<ConfigurationEditor>(parameters =>
            parameters.Add(item => item.Definition, BrowserFixture.Definition));
        var form = editor.FindComponents<ConfigurationField>()[0].Instance.Form;
        await shell.Find("#configuration-search").InputAsync(new ChangeEventArgs { Value = "credential" });
        var navigate = shell.FindComponent<ConfigurationDiscovery>().Instance.Navigate;
        await editor.InvokeAsync(() => form.EditValue(["hostname"], "synthetic-updated.test"));
        int focusCount = context.JSInterop.Invocations.Count(call => call.Identifier == "Blazor._internal.domWrapper.focus");
        await editor.InvokeAsync(() => navigate.InvokeAsync(new Configurator.Core.Definitions.DefinitionDiagnostic("discovery", "credential")));
        await Assert.That(context.JSInterop.Invocations.Count(call => call.Identifier == "Blazor._internal.domWrapper.focus")).IsEqualTo(focusCount);
        await Assert.That(shell.FindAll("[data-discovery-result]").Count).IsEqualTo(1);
        await Assert.That(form.InputRevision).IsEqualTo(1);
    }

    [Test]
    public async Task SecretSearchSelectionReachesConsentWithoutChangingAnswers()
    {
        await using var context = BrowserFixture.Context();
        var shell = context.Render<NavMenu>();
        var editor = context.Render<ConfigurationEditor>(parameters =>
            parameters.Add(item => item.Definition, BrowserFixture.Definition));
        var form = editor.FindComponents<ConfigurationField>()[0].Instance.Form;
        await shell.Find("#configuration-search").InputAsync(new ChangeEventArgs { Value = "credential" });
        await shell.Find("[data-discovery-result='credential'] button").ClickAsync(new MouseEventArgs());
        await Assert.That(shell.FindAll("[data-discovery-result]")).IsEmpty();
        await Assert.That(editor.Find("#secret-consent").HasAttribute("checked")).IsFalse();
        await Assert.That(editor.Find("details[data-secret-consent]").HasAttribute("open")).IsTrue();
        await Assert.That(editor.Find("[data-workflow-step='configure']").GetAttribute("aria-current")).IsEqualTo("step");
        await Assert.That(form.InputRevision).IsEqualTo(0);
        await Assert.That(form.CanEdit(["credential"])).IsFalse();
        await Assert.That(context.JSInterop.Invocations.Last(call => call.Identifier == "Blazor._internal.domWrapper.focus").Arguments[0]).IsNotNull();
        await editor.Find("details[data-secret-consent] > summary").ClickAsync(new MouseEventArgs());
        await Assert.That(editor.Find("details[data-secret-consent]").HasAttribute("open")).IsFalse();
        await shell.Find("button[aria-controls='workspace-search-results']").ClickAsync(new MouseEventArgs());
        await shell.Find("[data-discovery-result='credential'] button").ClickAsync(new MouseEventArgs());
        await Assert.That(editor.Find("details[data-secret-consent]").HasAttribute("open")).IsTrue();
    }

    [Test]
    public async Task ClosedSearchReflectsExternalQueryAndCurrentWorkspaceWhenReopened()
    {
        await using var context = BrowserFixture.Context();
        var shell = context.Render<NavMenu>();
        var editor = context.Render<ConfigurationEditor>(parameters =>
            parameters.Add(item => item.Definition, BrowserFixture.Definition));
        var form = editor.FindComponents<ConfigurationField>()[0].Instance.Form;
        await editor.InvokeAsync(() => form.SetDiscovery("hostname"));
        await Assert.That(shell.Find("#configuration-search").GetAttribute("value")).IsEqualTo("hostname");
        await Assert.That(shell.FindAll("[data-discovery-result]")).IsEmpty();
        await editor.InvokeAsync(() => form.EditValue(["hostname"], "synthetic-local.test"));
        var search = shell.FindComponent<WorkspaceSearch>();
        long generation = context.Services.GetRequiredService<WorkspaceSearchState>().Generation;
        await search.InvokeAsync(() => search.Instance.OpenAsync(generation));
        await Assert.That(shell.FindAll("[data-discovery-result]").Count).IsEqualTo(1);
        await Assert.That(form.DiscoveryQuery).IsEqualTo("hostname");
        await Assert.That(form.Snapshot().Values["hostname"].GetString()).IsEqualTo("synthetic-local.test");
    }

    [Test]
    public async Task RetiredEditorDisposalLeavesCurrentSearchAndEscapeUsable()
    {
        await using var context = BrowserFixture.Context();
        var shell = context.Render<NavMenu>();
        var previous = context.Render<ConfigurationEditor>(parameters =>
            parameters.Add(item => item.Definition, BrowserFixture.Definition));
        var search = shell.FindComponent<WorkspaceSearch>();
        long retiredGeneration = context.Services.GetRequiredService<WorkspaceSearchState>().Generation;
        var current = context.Render<ConfigurationEditor>(parameters =>
            parameters.Add(item => item.Definition, BrowserFixture.Definition));
        var form = current.FindComponents<ConfigurationField>()[0].Instance.Form;
        await previous.InvokeAsync(() => previous.Instance.DisposeAsync().AsTask());
        await search.InvokeAsync(() => search.Instance.OpenAsync(retiredGeneration));
        await Assert.That(shell.FindAll("[data-discovery-result]")).IsEmpty();
        shell.Find("#configuration-search").Input("hostname");
        await Assert.That(shell.FindAll("[data-discovery-result]").Count).IsEqualTo(1);
        shell.Find(".workspace-search").KeyDown("Escape");
        await Assert.That(shell.FindAll("[data-discovery-result]")).IsEmpty();
        await Assert.That(form.DiscoveryQuery).IsEqualTo("hostname");
        await Assert.That(form.InputRevision).IsEqualTo(0);
        await Assert.That(form.CanEdit(["credential"])).IsFalse();
    }

    [Test]
    public async Task RetiredSearchOwnerCannotClearOrRetargetTheCurrentWorkspace()
    {
        using var previousForm = new DefinitionFormModel(BrowserFixture.Definition, DefinitionSessionPrivacy.BrowserNoSecrets);
        using var currentForm = new DefinitionFormModel(BrowserFixture.Definition, DefinitionSessionPrivacy.BrowserNoSecrets);
        currentForm.EditValue(["mode"], "single");
        var search = new WorkspaceSearchState();
        object previous = new();
        object current = new();
        long previousLease = search.Register(previous, () => previousForm.DiscoveryQuery,
            previousForm.SetDiscovery, builder => builder.AddContent(0, "Previous public metadata"));
        long currentLease = search.Register(current, () => currentForm.DiscoveryQuery,
            currentForm.SetDiscovery, builder => builder.AddContent(0, "Current public metadata"));

        search.Release(previous, previousLease);
        await Assert.That(search.IsActive).IsTrue();
        search.Search(currentLease, "hostname");
        search.Search(previousLease, "credential");

        await Assert.That(search.Query).IsEqualTo("hostname");
        await Assert.That(currentForm.DiscoveryQuery).IsEqualTo("hostname");
        await Assert.That(previousForm.DiscoveryQuery).IsEqualTo("");
        await Assert.That(currentForm.InputRevision).IsEqualTo(1);
        await Assert.That(currentForm.Snapshot().Values["mode"].GetString()).IsEqualTo("single");
        await Assert.That(currentForm.CanEdit(["credential"])).IsFalse();
        search.Release(current, currentLease);
        await Assert.That(search.IsActive).IsFalse();
        await Assert.That(search.Content).IsNull();
        await Assert.That(search.Query).IsEqualTo("");
    }

    [Test]
    public async Task RetiredRegistrationCannotReleaseReplacementWithinTheSameEditor()
    {
        using var form = new DefinitionFormModel(BrowserFixture.Definition);
        var search = new WorkspaceSearchState();
        object editor = new();
        long previousLease = search.Register(editor, () => form.DiscoveryQuery,
            form.SetDiscovery, builder => builder.AddContent(0, "Public metadata"));
        long currentLease = search.Register(editor, () => form.DiscoveryQuery,
            form.SetDiscovery, builder => builder.AddContent(0, "Replacement metadata"));

        search.Release(editor, previousLease);
        search.Search(currentLease, "hostname");

        await Assert.That(search.IsActive).IsTrue();
        await Assert.That(form.DiscoveryQuery).IsEqualTo("hostname");
        await Assert.That(form.InputRevision).IsEqualTo(0);
    }

    [Test]
    public async Task ShellProvidesNamedRoutesMainSkipAndLocalSourceWhenDirectoryUnavailable()
    {
        await using var context = BrowserFixture.Context();
        context.Services.AddSingleton<Configurator.Presentation.Store.IDefinitionStoreProvider>(
            new TestDefinitionStoreProvider(
                load: _ => Task.FromException<IReadOnlyList<Configurator.Presentation.Store.DefinitionStoreProject>>(
                    new Configurator.Presentation.Store.DefinitionStoreException("store-unavailable"))));
        var component = context.Render<App>();
        await Assert.That(component.FindAll("main")).Count().IsEqualTo(1);
        await Assert.That(component.Find("a[href='#main-content']").TextContent).IsEqualTo("Skip to main content");
        await Assert.That(component.Find("main").GetAttribute("id")).IsEqualTo("main-content");
        await Assert.That(component.FindAll("h1")).Count().IsEqualTo(1);
        await Assert.That(component.Find("nav[aria-label='Application navigation'] a[href='']").TextContent).IsNotEmpty();
        await Assert.That(component.FindAll("nav[aria-label='Application navigation'] a[href='store']")).IsEmpty();
        await Assert.That(component.Find("nav[aria-label='Application navigation'] a[href='open']").TextContent).IsNotEmpty();
        await Assert.That(component.Find("nav[aria-label='Application navigation'] a[href='help']").TextContent).IsEqualTo("Help");
        await Assert.That(component.FindAll("header .nav-menu__brand")).Count().IsEqualTo(1);
        await Assert.That(component.FindAll("footer")).Count().IsEqualTo(1);
        await Assert.That(component.FindComponents<WorkspaceSearch>()).Count().IsEqualTo(1);
        await Assert.That(component.FindComponents<WorkspaceNavigationGuard>()).Count().IsEqualTo(1);
        await Assert.That(component.FindAll("[data-code='store-unavailable']")).Count().IsEqualTo(1);
        await Assert.That(component.FindAll("a[href^='launch/']")).Count().IsEqualTo(1);
        var help = context.Render<Help>();
        await Assert.That(help.FindAll("h1")).Count().IsEqualTo(1);
        await Assert.That(help.Find("a[href='open']").TextContent).IsNotEmpty();
    }

    [Test]
    public async Task NavigationDisclosureSupportsEscapeCloseAndReturnsFocusToItsTrigger()
    {
        await using var context = BrowserFixture.Context();
        var component = context.Render<NavMenu>();
        const string trigger = "button[aria-controls='application-navigation']";
        await Assert.That(component.Find(trigger).TextContent).IsEqualTo("Menu");
        await Assert.That(component.Find(trigger).GetAttribute("aria-expanded")).IsEqualTo("false");
        await Assert.That(component.Find("#application-navigation").GetAttribute("aria-label")).IsEqualTo("Application navigation");
        await component.Find(trigger).ClickAsync(new MouseEventArgs());
        await Assert.That(component.Find(trigger).GetAttribute("aria-expanded")).IsEqualTo("true");
        await component.Find("nav").KeyDownAsync(new KeyboardEventArgs { Key = "Escape" });
        await Assert.That(component.Find(trigger).GetAttribute("aria-expanded")).IsEqualTo("false");
        await Assert.That(context.JSInterop.Invocations.Count(call => call.Identifier == "Blazor._internal.domWrapper.focus")).IsEqualTo(1);
        object menuFocusTarget = context.JSInterop.Invocations.Last(call => call.Identifier == "Blazor._internal.domWrapper.focus").Arguments[0]!;

        await component.Find(trigger).ClickAsync(new MouseEventArgs());
        await component.Find(trigger).KeyDownAsync(new KeyboardEventArgs { Key = "Escape" });
        await Assert.That(component.Find(trigger).GetAttribute("aria-expanded")).IsEqualTo("false");
        await component.Find(trigger).ClickAsync(new MouseEventArgs());
        await component.Find("nav button").ClickAsync(new MouseEventArgs());
        await Assert.That(component.Find(trigger).GetAttribute("aria-expanded")).IsEqualTo("false");
        await Assert.That(context.JSInterop.Invocations.Count(call => call.Identifier == "Blazor._internal.domWrapper.focus")).IsEqualTo(3);
        await Assert.That(context.JSInterop.Invocations.Last(call => call.Identifier == "Blazor._internal.domWrapper.focus").Arguments[0]).IsEqualTo(menuFocusTarget);
    }

    [Test]
    public async Task NavigationSelectionClosesTheDisclosureWithoutReplacingSearch()
    {
        await using var context = BrowserFixture.Context();
        var component = context.Render<NavMenu>();
        var search = component.FindComponent<WorkspaceSearch>().Instance;
        await component.Find("button[aria-controls='application-navigation']").ClickAsync(new MouseEventArgs());
        await component.Find("nav a[href='open']").ClickAsync(new MouseEventArgs());
        await Assert.That(component.Find("button[aria-controls='application-navigation']").GetAttribute("aria-expanded")).IsEqualTo("false");
        await Assert.That(ReferenceEquals(component.FindComponent<WorkspaceSearch>().Instance, search)).IsTrue();
    }

    [Test]
    public async Task TopBarProvidesQuickThemeLanguageAndSettingsWithoutSidebarSettingsDisclosure()
    {
        await using BunitContext context = BrowserFixture.Context();
        IRenderedComponent<NavMenu> component = context.Render<NavMenu>();
        IRenderedComponent<Microsoft.AspNetCore.Components.IComponent> surface = BrowserFixture.PopoverSurface(context, component);

        await Assert.That(component.FindAll("nav #navigation-settings")).IsEmpty();
        await Assert.That(component.FindAll("header [data-action='toggle-language-picker']")).Count().IsEqualTo(1);
        await Assert.That(component.FindAll("header [data-action='toggle-theme-switcher']")).Count().IsEqualTo(1);
        await Assert.That(component.FindAll("header [data-action='open-settings']")).Count().IsEqualTo(1);

        await component.Find("[data-action='toggle-theme-switcher']").ClickAsync(new MouseEventArgs());
        await Assert.That(surface.FindAll("[data-theme-preset]")).Count().IsEqualTo(8);
        await Assert.That(surface.FindAll("[data-theme-mode]")).Count().IsEqualTo(5);
        await Assert.That(component.Find("button[aria-controls='application-navigation']").GetAttribute("aria-expanded")).IsEqualTo("false");

        await component.Find("[data-action='toggle-language-picker']").ClickAsync(new MouseEventArgs());
        await Assert.That(surface.FindAll("[data-locale='en']")).Count().IsEqualTo(1);
        await Assert.That(surface.FindAll("[data-locale='ar']")).Count().IsEqualTo(1);
    }

    [Test]
    public async Task NamedAppearanceControlsPreserveCurrentRouteAndMainContent()
    {
        await using BunitContext context = BrowserFixture.Context();
        IRenderedComponent<App> component = context.Render<App>();
        string heading = component.Find("h1").TextContent;
        string route = context.Services.GetRequiredService<NavigationManager>().Uri;

        await component.Find("[data-action='toggle-theme-switcher']").ClickAsync(new MouseEventArgs());
        await component.Find("[data-theme-preset='emerald-green']").ClickAsync(new MouseEventArgs());
        await component.Find("[data-action='toggle-theme-switcher']").ClickAsync(new MouseEventArgs());
        await component.Find("[data-theme-mode='darkhighcontrast']").ClickAsync(new MouseEventArgs());

        await Assert.That(component.Find("h1").TextContent).IsEqualTo(heading);
        await Assert.That(context.Services.GetRequiredService<NavigationManager>().Uri).IsEqualTo(route);

        await component.Find("[data-action='toggle-language-picker']").ClickAsync(new MouseEventArgs());
        await component.Find("[data-locale='ar']").ClickAsync(new MouseEventArgs());
        await Assert.That(component.Find(".main-layout").GetAttribute("dir")).IsEqualTo("rtl");
        await Assert.That(context.Services.GetRequiredService<NavigationManager>().Uri).IsEqualTo(route);
    }

    [Test]
    public async Task PreferenceChangesRefreshTheSwitcherWithoutRecreatingItsParent()
    {
        await using BunitContext context = BrowserFixture.Context();
        IRenderedComponent<ThemeQuickSwitcher> component = context.Render<ThemeQuickSwitcher>();
        IRenderedComponent<Microsoft.AspNetCore.Components.IComponent> surface = BrowserFixture.PopoverSurface(context, component);
        AppearanceThemeService service = context.Services.GetRequiredService<AppearanceThemeService>();

        await component.InvokeAsync(async () =>
        {
            await service.SetThemePresetAsync("abyssal-dark");
            await service.SetModeAsync("dark");
            await service.SetDirectionAsync("rtl");
        });

        await component.Find("[data-action='toggle-theme-switcher']").ClickAsync(new MouseEventArgs());
        await Assert.That(surface.Find("[data-theme-preset='abyssal-dark']").GetAttribute("aria-pressed")).IsEqualTo("true");
        await Assert.That(surface.Find("[data-theme-mode='dark']").GetAttribute("aria-pressed")).IsEqualTo("true");
    }

    [Test]
    public async Task AppearanceChangesPreserveCurrentAnswersAndPreparedOutput()
    {
        await using BunitContext context = BrowserFixture.Context();
        var exports = context.JSInterop.SetupModule("./js/browser-artifacts.js");
        exports.SetupVoid("open", _ => true).SetVoidResult();
        exports.Setup<bool>("copy", _ => true).SetResult(true);
        exports.SetupVoid("invalidate", _ => true).SetVoidResult();
        context.Services.GetRequiredService<NavigationManager>().NavigateTo($"launch/{BrowserFixture.Family}?digest={BrowserFixture.Definition.Digest.Value}");
        IRenderedComponent<App> component = context.Render<App>();
        await BrowserFixture.NotifyChoiceAsync(component, "mode", "single");
        component.Find("[data-field='hostname'] input").Input("synthetic.example.test");
        await BrowserFixture.PrepareAsync(component, draft: true);
        component.Find(".configuration-editor__artifact button").Click();
        byte[] before = (byte[])exports.Invocations.Single(call => call.Identifier == "open").Arguments[2]!;

        await component.Find("[data-action='toggle-theme-switcher']").ClickAsync(new MouseEventArgs());
        await component.Find("[data-theme-preset='emerald-green']").ClickAsync(new MouseEventArgs());
        await component.Find("[data-action='toggle-theme-switcher']").ClickAsync(new MouseEventArgs());
        await component.Find("[data-theme-mode='dark']").ClickAsync(new MouseEventArgs());
        await component.InvokeAsync(() => context.Services.GetRequiredService<AppearanceThemeService>().SetDirectionAsync("rtl").AsTask());
        component.Find("a[href='#main-content']").Click();

        await Assert.That(component.FindAll(".configuration-editor__artifact")).Count().IsEqualTo(1);
        component.Find(".configuration-editor__artifact button").Click();
        byte[] after = (byte[])exports.Invocations.Last(call => call.Identifier == "open").Arguments[2]!;
        await Assert.That(after).IsEquivalentTo(before);
        await Assert.That(exports.Invocations.Count(call => call.Identifier == "open")).IsEqualTo(1);
        await Assert.That(exports.Invocations.Count(call => call.Identifier == "copy")).IsEqualTo(2);
        await Assert.That(exports.Invocations.Any(call => call.Identifier == "invalidate")).IsFalse();
        await Assert.That(context.JSInterop.Invocations.Any(call => call.Identifier == "Blazor._internal.domWrapper.focus")).IsTrue();
    }

    [Test]
    public async Task ActiveWorkspaceOmitsFooterAndRendersConnectedWorkspaceHeader()
    {
        await using var context = BrowserFixture.Context();
        context.Services.GetRequiredService<NavigationManager>()
            .NavigateTo($"launch/{BrowserFixture.Family}?digest={BrowserFixture.Definition.Digest.Value}");
        var component = context.Render<App>();

        await Assert.That(component.FindAll("footer")).IsEmpty();
        await Assert.That(component.FindComponents<WorkspaceHeader>()).Count().IsEqualTo(1);
        await Assert.That(component.Find("[data-workspace-consent-chip]")).IsNotNull();
        await Assert.That(component.Find("[data-action='toggle-naming-mode']")).IsNotNull();
        string[] densities = component.FindAll("#configuration-density option").Select(item => item.GetAttribute("value") ?? "").ToArray();
        await Assert.That(densities).IsEquivalentTo(["spacious", "comfortable", "compact", "dense"]);
        string[] workflowSteps = component.FindAll("[data-workflow-step]").Select(item => item.GetAttribute("data-workflow-step") ?? "").ToArray();
        await Assert.That(workflowSteps).IsEquivalentTo(["configure", "review", "prepare"]);
        string[] layoutModes = component.FindAll("[data-layout-mode]").Select(item => item.GetAttribute("data-layout-mode") ?? "").ToArray();
        await Assert.That(layoutModes).IsEquivalentTo(["section-by-section", "all-sections"]);

        var form = component.FindComponents<ConfigurationField>()[0].Instance.Form;
        long revision = form.InputRevision;
        await component.Find("[data-action='toggle-naming-mode']").ClickAsync(new MouseEventArgs());
        await Assert.That(form.WorkspaceNamingMode).IsEqualTo(WorkspaceNamingMode.Technical);
        await Assert.That(form.InputRevision).IsEqualTo(revision);

        await component.Find("#secret-consent").ChangeAsync(new ChangeEventArgs { Value = true });
        await Assert.That(component.Find("[data-workspace-consent-chip]").GetAttribute("data-consent-active")).IsEqualTo("true");
    }

    [Test]
    public async Task WorkspaceHeaderDisplaysTitleAlongsideScopeAndFencesConsentAndStaleStageActions()
    {
        await using var context = BrowserFixture.Context();
        Definition definition = BrowserDeploymentScopeTests.Deployment();
        var editor = context.Render<ConfigurationEditor>(parameters =>
            parameters.Add(item => item.Definition, definition));
        var form = editor.FindComponents<ConfigurationField>()[0].Instance.Form;

        await Assert.That(editor.Find("[data-workspace-title]").TextContent).Contains(definition.Title);
        await Assert.That(editor.Find("[data-workspace-scope]").TextContent).Contains("Standalone");

        var staleStageSelected = editor.FindComponent<WorkspaceHeader>().Instance.StageSelected;
        await editor.InvokeAsync(() => form.EditValue(["dashboard"], "updated-dashboard"));
        await editor.InvokeAsync(() => staleStageSelected.InvokeAsync(WorkspaceStage.Review));
        await Assert.That(form.ConfigureLayoutMode).IsEqualTo(ConfigureLayoutMode.SectionBySection);
        await Assert.That(editor.Find("[data-workflow-step='configure']").GetAttribute("aria-current")).IsEqualTo("step");

        await BrowserFixture.SelectPanelAsync(editor, "review");
        int focusCountBefore = context.JSInterop.Invocations.Count(call => call.Identifier == "Blazor._internal.domWrapper.focus");
        await editor.Find("[data-workspace-consent-chip]").ClickAsync(new MouseEventArgs());
        await Assert.That(editor.Find("details[data-secret-consent]").HasAttribute("open")).IsTrue();
        await Assert.That(context.JSInterop.Invocations.Count(call => call.Identifier == "Blazor._internal.domWrapper.focus"))
            .IsGreaterThan(focusCountBefore);
    }

    [Test]
    public async Task ConfigurationDiagnosticTextRetiresAdHocTitleHelper()
    {
        Type? helper = typeof(ConfigurationEditor).Assembly
            .GetType("Configurator.Browser.Components.Configuration.ConfigurationDiagnosticText", throwOnError: false);
        await Assert.That(helper).IsNotNull();
        MethodInfo[] leftoverTitles = helper!
            .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)
            .Where(method => method.Name == "Title")
            .ToArray();
        await Assert.That(leftoverTitles).IsEmpty();
    }

    [Test]
    public async Task ArabicRtlWorkspaceIsolatesWireIdentifiersAndPathsInLtr()
    {
        JsonNode document = JsonNode.Parse(BrowserFixture.Definition.CanonicalBytes.Span)!;
        document["locales"] = new JsonArray("ar", "en");
        document["localization"] = JsonNode.Parse("""
            {"defaultLocale":"en","translations":{"ar":{"direction":"rtl","overrides":{}}}}
            """);
        BrowserFixture.WithField(document, "HTTPServer_URL", """
            {"type":"string","x-configurator":{"sensitivity":"public","detail":"standard"}}
            """);
        Definition definition = DefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(document.ToJsonString()));
        await using var context = BrowserFixture.Context();
        var component = context.Render<MainLayout>(parameters => parameters.Add(item => item.Body, builder =>
        {
            builder.OpenComponent<ConfigurationEditor>(0);
            builder.AddAttribute(1, nameof(ConfigurationEditor.Definition), definition);
            builder.AddAttribute(2, nameof(ConfigurationEditor.InitialLocale), "ar");
            builder.CloseComponent();
        }));

        await Assert.That(component.Find(".configuration-editor").GetAttribute("dir")).IsEqualTo("rtl");
        await Assert.That(component.Find("[data-field='HTTPServer_URL'] .configuration-field__key").GetAttribute("dir")).IsEqualTo("ltr");
        await component.Find("[data-field='HTTPServer_URL'] [data-action='field-help']").ClickAsync(new MouseEventArgs());
        await Assert.That(component.Find("[data-definition-path]").GetAttribute("dir")).IsEqualTo("ltr");
    }

    [Test]
    public async Task UnifiedHeaderSeparatesStagesFromLayoutToggleScopeChipTogglesContextAndProgressBarIsTrimmed()
    {
        await using var context = BrowserFixture.Context();
        Definition definition = BrowserDeploymentScopeTests.Deployment();
        var editor = context.Render<ConfigurationEditor>(parameters =>
            parameters.Add(item => item.Definition, definition));
        var form = editor.FindComponents<ConfigurationField>()[0].Instance.Form;

        // Stage bar strictly contains 3 workflow steps and zero layout toggles.
        var header = editor.Find("header.workspace-header");
        string[] steps = header.QuerySelectorAll("[data-workflow-step]").Select(el => el.GetAttribute("data-workflow-step") ?? "").ToArray();
        await Assert.That(steps).IsEquivalentTo(["configure", "review", "prepare"]);
        await Assert.That(header.QuerySelectorAll("[data-layout-mode]").Length).IsEqualTo(0);

        // Configure panel owns the orthogonal layout switcher.
        var configurePanel = editor.Find(".configuration-configure-panel");
        string[] layoutSwitches = configurePanel.QuerySelectorAll("[data-layout-mode]").Select(el => el.GetAttribute("data-layout-mode") ?? "").ToArray();
        await Assert.That(layoutSwitches).IsEquivalentTo(["section-by-section", "all-sections"]);

        // Progress bar excludes returning-user entry choices, which remain accessible in workspace tools.
        await Assert.That(editor.FindAll(".configuration-editor__progress [data-returning-entry]")).IsEmpty();
        await Assert.That(editor.FindAll("[data-returning-entry] [data-entry-choice]").Count).IsGreaterThan(0);

        // Context disclosure starts hidden/collapsed at rest and toggles open via Scope chip.
        var scopeChip = editor.Find("[data-workspace-scope][data-action='change-scope']");
        await Assert.That(scopeChip.GetAttribute("aria-expanded")).IsEqualTo("false");
        await Assert.That(editor.Find("#configuration-context").HasAttribute("hidden")).IsTrue();
        await scopeChip.ClickAsync(new MouseEventArgs());
        await Assert.That(scopeChip.GetAttribute("aria-expanded")).IsEqualTo("true");
        await Assert.That(editor.Find("#configuration-context").HasAttribute("hidden")).IsFalse();
        await scopeChip.ClickAsync(new MouseEventArgs());
        await Assert.That(scopeChip.GetAttribute("aria-expanded")).IsEqualTo("false");

        // AllSections mode navigates to the single top-level Review stage preserving unparsed drafts without inline panels or CompactPresentation.
        await editor.Find("[data-layout-mode='all-sections']").ClickAsync(new MouseEventArgs());
        await editor.Find("[data-field='dashboard'] input").InputAsync(new ChangeEventArgs { Value = "draft-before-review" });
        await BrowserFixture.SelectPanelAsync(editor, "review");
        await Assert.That(editor.FindAll("[data-compact-inline-panel]")).IsEmpty();
        await Assert.That(editor.FindAll("[data-review-compact-status]")).IsEmpty();
        await Assert.That(editor.Find("[data-workflow-step='review']").GetAttribute("aria-current")).IsEqualTo("step");

        await BrowserFixture.SelectPanelAsync(editor, "configure");
        await Assert.That(form.ConfigureLayoutMode).IsEqualTo(ConfigureLayoutMode.AllSections);
        await Assert.That(editor.Find("[data-field='dashboard'] input").GetAttribute("value")).IsEqualTo("draft-before-review");
    }

    [Test]
    public async Task SettingsRouteOffersAppearancePresetsCustomBuilderLanguagePrivacyAndShortcuts()
    {
        await using BunitContext context = BrowserFixture.Context();
        context.Services.GetRequiredService<NavigationManager>().NavigateTo("settings");
        IRenderedComponent<App> component = context.Render<App>();
        AppearanceThemeService appearance = context.Services.GetRequiredService<AppearanceThemeService>();

        await Assert.That(component.FindAll("[data-action='return-to-configuration']")).IsEmpty();
        await Assert.That(component.FindAll("[data-settings-section]")).Count().IsEqualTo(4);
        await Assert.That(component.FindAll("[data-settings-preset]")).Count().IsEqualTo(8);
        await Assert.That(component.FindAll("[data-settings-theme-mode]")).Count().IsEqualTo(5);
        await Assert.That(component.FindAll("[data-settings-density]")).Count().IsEqualTo(4);
        await Assert.That(component.FindAll("[data-settings-naming]")).Count().IsEqualTo(2);
        await Assert.That(component.FindAll("#settings-custom-theme")).Count().IsEqualTo(1);
        await Assert.That(component.FindAll("[data-settings-custom-preview]")).Count().IsEqualTo(1);
        await Assert.That(component.FindAll("[data-settings-locale]")).Count().IsEqualTo(2);

        await component.Find("[data-settings-preset='abyssal-dark']").ClickAsync(new MouseEventArgs());
        await component.Find("[data-settings-theme-mode='dark']").ClickAsync(new MouseEventArgs());
        await component.Find("[data-settings-density='compact']").ClickAsync(new MouseEventArgs());
        await component.Find("[data-settings-naming='technical']").ClickAsync(new MouseEventArgs());
        await Assert.That(appearance.Current.ThemeKey).IsEqualTo("abyssal-dark");
        await Assert.That(appearance.Current.ThemeMode).IsEqualTo("dark");
        await Assert.That(appearance.Current.Density).IsEqualTo("compact");
        await Assert.That(appearance.Current.NamingMode).IsEqualTo("technical");

        await component.Find("[data-custom-natural-hex] input").ChangeAsync(new ChangeEventArgs { Value = "#5C6BC0" });
        await component.Find("[data-custom-brand-hex] input").ChangeAsync(new ChangeEventArgs { Value = "#0D9488" });
        await component.Find("[data-action='apply-custom-theme']").ClickAsync(new MouseEventArgs());
        await Assert.That(appearance.Current.ThemeKey).IsEqualTo("custom");
        await Assert.That(appearance.Current.CustomNaturalHex).IsEqualTo("#5C6BC0");
        await Assert.That(appearance.Current.CustomBrandHex).IsEqualTo("#0D9488");
    }

    [Test]
    public async Task SettingsOpenedDuringActiveWorkspacePreservesSessionAndReturnsViaTopLeftAction()
    {
        System.Text.Json.Nodes.JsonNode doc = System.Text.Json.Nodes.JsonNode.Parse(BrowserFixture.Definition.CanonicalBytes.Span)!;
        BrowserFixture.WithField(doc, "retries", """
            {"type":"integer","title":"Retry count","x-configurator":{"sensitivity":"public","detail":"standard"}}
            """, "schema.integer");
        Definition definition = DefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(doc.ToJsonString()));
        await using BunitContext context = BrowserFixture.Context(new BrowserDefinitionRegistry([definition]));
        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo($"launch/{BrowserFixture.Family}?digest={definition.Digest.Value}");
        string launchUri = navigation.Uri;
        IRenderedComponent<App> component = context.Render<App>();
        AppearanceThemeService appearance = context.Services.GetRequiredService<AppearanceThemeService>();

        await BrowserFixture.NotifyChoiceAsync(component, "mode", "single");
        await component.Find("[data-field='hostname'] input").InputAsync(new ChangeEventArgs { Value = "preserved-settings.example.test" });
        await BrowserFixture.PrepareAsync(component, draft: true);
        await Assert.That(component.FindAll(".configuration-editor__artifact")).IsNotEmpty();

        await component.Find("header [data-action='open-settings']").ClickAsync(new MouseEventArgs());

        await Assert.That(navigation.Uri).IsEqualTo(launchUri);
        await Assert.That(component.FindAll("[data-action='return-to-configuration']")).Count().IsEqualTo(1);
        await Assert.That(component.Find("main#main-content").HasAttribute("hidden")).IsTrue();

        await component.Find("[data-settings-preset='emerald-green']").ClickAsync(new MouseEventArgs());
        await component.Find("[data-action='return-to-configuration']").ClickAsync(new MouseEventArgs());

        await Assert.That(component.FindAll("[data-action='return-to-configuration']")).IsEmpty();
        await Assert.That(component.Find("main#main-content").HasAttribute("hidden")).IsFalse();
        await Assert.That(appearance.Current.ThemeKey).IsEqualTo("emerald-green");
        await Assert.That(component.FindAll(".configuration-editor__artifact")).IsNotEmpty();

        await BrowserFixture.SelectPanelAsync(component, "configure");
        await Assert.That(component.Find("[data-field='hostname'] input").GetAttribute("value")).IsEqualTo("preserved-settings.example.test");

        await component.Find("[data-field='retries'] input").InputAsync(new ChangeEventArgs { Value = "not-a-number-draft" });
        await component.Find("header [data-action='open-settings']").ClickAsync(new MouseEventArgs());
        await component.Find("[data-settings-density='dense']").ClickAsync(new MouseEventArgs());
        await component.Find("[data-action='return-to-configuration']").ClickAsync(new MouseEventArgs());

        await Assert.That(appearance.Current.Density).IsEqualTo("dense");
        await Assert.That(navigation.Uri).IsEqualTo(launchUri);
        await Assert.That(component.Find("[data-field='retries'] input").GetAttribute("value")).IsEqualTo("not-a-number-draft");
    }

    [Test]
    public async Task GlobalNavigationDrawerSeparatesWorkspaceLinksFromFooterAndShowsCloseInOverlay()
    {
        await using BunitContext context = BrowserFixture.Context();
        IRenderedComponent<App> app = context.Render<App>();
        DockLayoutState dockLayout = context.Services.GetRequiredService<DockLayoutState>();

        await app.InvokeAsync(() => dockLayout.UpdateViewport(1440, isMobile: false, DockScope.Shell, 420));

        // Docked StartNav places global workspace links ahead of bottom custody footer with zero form sections or close button.
        await Assert.That(app.FindAll("#application-navigation .nav-menu__sections")).IsEmpty();
        await Assert.That(app.FindAll("#application-navigation .nav-menu__workspace a[href='']")).Count().IsEqualTo(1);
        await Assert.That(app.FindAll("#application-navigation .nav-menu__workspace a[href='open']")).Count().IsEqualTo(1);
        await Assert.That(app.FindAll("#application-navigation .nav-menu__workspace a[href='help']")).Count().IsEqualTo(1);
        await Assert.That(app.FindAll("#application-navigation .nav-menu__footer .nav-menu__local")).Count().IsEqualTo(1);
        await Assert.That(app.FindAll("#application-navigation .nav-menu__footer a")).IsEmpty();
        await Assert.That(app.FindAll("#application-navigation .nav-menu__close")).IsEmpty();

        // Degrade StartNav into DockOverlayHost (mobile / tight desktop) and re-open Menu -> visible interactive Close restores toggle focus.
        await app.InvokeAsync(() => dockLayout.UpdateViewport(760, isMobile: true, DockScope.Shell, 420));
        await app.Find("#nav-menu-toggle").ClickAsync(new MouseEventArgs());

        IReadOnlyList<AngleSharp.Dom.IElement> overlayCloseButtons = app.FindAll("[data-testid='dock-overlay-host'][data-dock-scope='shell'] nav#application-navigation.nav-menu--overlay .nav-menu__close");
        await Assert.That(overlayCloseButtons).Count().IsEqualTo(1);

        await overlayCloseButtons[0].ClickAsync(new MouseEventArgs());
        await Assert.That(app.Find("#nav-menu-toggle").GetAttribute("aria-expanded")).IsEqualTo("false");
        await Assert.That(app.FindAll("[data-testid='dock-overlay-host'][data-dock-scope='shell'] #application-navigation")).IsEmpty();
        await Assert.That(context.JSInterop.Invocations.Last(call => call.Identifier == "setFocusById").Arguments[0]).IsEqualTo("nav-menu-toggle");

        // Backdrop close inside DockOverlayHost also routes through CloseStartDockAsync and restores toggle focus.
        await app.Find("#nav-menu-toggle").ClickAsync(new MouseEventArgs());
        await app.Find("[data-testid='dock-overlay-host'][data-dock-scope='shell'] [data-testid='dock-overlay-backdrop']").ClickAsync(new MouseEventArgs());
        await Assert.That(app.Find("#nav-menu-toggle").GetAttribute("aria-expanded")).IsEqualTo("false");
        await Assert.That(context.JSInterop.Invocations.Last(call => call.Identifier == "setFocusById").Arguments[0]).IsEqualTo("nav-menu-toggle");
    }

    [Test]
    public async Task LaunchWorkspaceUsesSinglePrimaryHeaderWithCollapsedAdvancedOptionsDisclosure()
    {
        BrowserDefinitionRegistry registry = BrowserDefinitionRegistry.LoadBundled();
        Definition definition = registry.Current.Resolve(Guid.Parse("f79259e8-af23-49b0-8d8d-6e371cc96080"));
        await using BunitContext context = BrowserFixture.Context(registry);
        context.Services.GetRequiredService<NavigationManager>()
            .NavigateTo($"/launch/{definition.FamilyId:D}?digest={definition.Digest.Value}&entry=environment");
        IRenderedComponent<App> app = context.Render<App>();

        // Route keeps a screen-reader h1 without stacking a redundant visual hero box above WorkspaceHeader.
        await Assert.That(app.Find("#launch-heading").ClassList.Contains("sr-only")).IsTrue();
        await Assert.That(app.FindAll(".launch-header")).IsEmpty();

        AngleSharp.Dom.IElement header = app.Find("header.workspace-header");
        AngleSharp.Dom.IElement advancedToggle = header.QuerySelector("[data-action='toggle-advanced-options']")!;
        await Assert.That(advancedToggle.GetAttribute("aria-expanded")).IsEqualTo("false");
        await Assert.That(advancedToggle.GetAttribute("aria-controls")).IsEqualTo("workspace-advanced-options");
        await Assert.That(header.QuerySelectorAll(".workspace-header__advanced-badge").Length).IsEqualTo(0);

        AngleSharp.Dom.IElement advancedPanel = app.Find("#workspace-advanced-options");
        await Assert.That(advancedPanel.HasAttribute("hidden")).IsTrue();
        await Assert.That(advancedPanel.HasAttribute("inert")).IsTrue();

        // Opening Advanced reveals secondary controls and source digest disclosure cleanly.
        await app.Find("[data-action='toggle-advanced-options']").ClickAsync(new MouseEventArgs());
        await Assert.That(app.Find("[data-action='toggle-advanced-options']").GetAttribute("aria-expanded")).IsEqualTo("true");
        advancedPanel = app.Find("#workspace-advanced-options");
        await Assert.That(advancedPanel.HasAttribute("hidden")).IsFalse();
        await Assert.That(advancedPanel.HasAttribute("inert")).IsFalse();
        await Assert.That(advancedPanel.QuerySelectorAll("#configuration-output").Length).IsEqualTo(1);
        await Assert.That(advancedPanel.QuerySelectorAll("#configuration-detail").Length).IsEqualTo(1);
        await Assert.That(advancedPanel.QuerySelectorAll("#configuration-density").Length).IsEqualTo(1);
        await Assert.That(advancedPanel.QuerySelectorAll("[data-workspace-scope]").Length).IsEqualTo(1);
        await Assert.That(advancedPanel.QuerySelectorAll("[data-digest-disclosure] [data-digest]").Length).IsEqualTo(1);

        // Activating Inspector and overriding detail updates the active-options count badge when collapsed again.
        await app.Find("[data-action='toggle-inspector']").ClickAsync(new MouseEventArgs());
        await app.Find("#configuration-detail").ChangeAsync(new ChangeEventArgs { Value = "advanced" });
        await app.Find("[data-action='toggle-advanced-options']").ClickAsync(new MouseEventArgs());
        await Assert.That(app.Find("#workspace-advanced-options").HasAttribute("hidden")).IsTrue();
        await Assert.That(app.Find(".workspace-header__advanced-badge").TextContent.Trim()).IsEqualTo("2");

        // Closing End dock while Advanced is collapsed restores focus safely without throwing or focusing an inert element.
        int focusBefore = context.JSInterop.Invocations.Count(call => call.Identifier == "Blazor._internal.domWrapper.focus");
        await app.Find("[data-action='close-inspector-pane']").ClickAsync(new MouseEventArgs());
        await Assert.That(context.JSInterop.Invocations.Count(call => call.Identifier == "Blazor._internal.domWrapper.focus"))
            .IsGreaterThan(focusBefore);
    }
}
