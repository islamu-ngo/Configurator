namespace Configurator.Browser.Tests;

using Bunit;
using Configurator.Browser.Components.Configuration;
using Configurator.Browser.Layout;
using Configurator.Browser.Pages;
using Configurator.Browser.Services;
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
        await Assert.That(component.FindAll("[data-code='store-size-invalid']")).Count().IsEqualTo(1);
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
    public async Task SettingsEscapeClosesOnlySettingsBeforeClosingNavigation()
    {
        await using var context = BrowserFixture.Context();
        var component = context.Render<NavMenu>();
        const string menu = "button[aria-controls='application-navigation']";
        const string settings = "nav button[aria-controls='navigation-settings']";
        await Assert.That(component.Find(settings).TextContent).IsEqualTo("Settings");
        await Assert.That(component.Find(settings).GetAttribute("aria-expanded")).IsEqualTo("false");
        await Assert.That(component.Find("#navigation-settings").HasAttribute("hidden")).IsTrue();
        await Assert.That(component.FindComponents<ThemeQuickSwitcher>()).IsEmpty();
        await component.Find(menu).ClickAsync(new MouseEventArgs());
        await component.Find(settings).ClickAsync(new MouseEventArgs());
        await Assert.That(component.Find(settings).GetAttribute("aria-expanded")).IsEqualTo("true");
        await Assert.That(component.Find("#navigation-settings").HasAttribute("hidden")).IsFalse();
        await Assert.That(component.FindComponents<ThemeQuickSwitcher>()).Count().IsEqualTo(1);
        await Assert.That(component.FindAll("header #appearance-mode")).IsEmpty();

        await component.Find("#appearance-mode").KeyDownAsync(new KeyboardEventArgs { Key = "Escape" });
        await Assert.That(component.Find(settings).GetAttribute("aria-expanded")).IsEqualTo("false");
        await Assert.That(component.Find(menu).GetAttribute("aria-expanded")).IsEqualTo("true");
        await Assert.That(component.FindComponents<ThemeQuickSwitcher>()).IsEmpty();
        object settingsFocusTarget = context.JSInterop.Invocations.Last(call => call.Identifier == "Blazor._internal.domWrapper.focus").Arguments[0]!;
        await component.Find(settings).KeyDownAsync(new KeyboardEventArgs { Key = "Escape" });
        await Assert.That(component.Find(menu).GetAttribute("aria-expanded")).IsEqualTo("false");
        await Assert.That(context.JSInterop.Invocations.Last(call => call.Identifier == "Blazor._internal.domWrapper.focus").Arguments[0]).IsNotEqualTo(settingsFocusTarget);
    }

    [Test]
    public async Task NamedAppearanceControlsPreserveCurrentRouteAndMainContent()
    {
        await using var context = BrowserFixture.Context();
        var component = context.Render<App>();
        string heading = component.Find("h1").TextContent;
        string route = context.Services.GetRequiredService<NavigationManager>().Uri;
        await component.Find("nav button[aria-controls='navigation-settings']").ClickAsync(new MouseEventArgs());
        await Assert.That(component.Find("label[for='appearance-mode']").TextContent).IsEqualTo("Appearance");
        await Assert.That(component.Find("label[for='appearance-direction']").TextContent).IsEqualTo("Direction");
        component.Find("#appearance-mode").Change("darkhighcontrast");
        component.Find("#appearance-direction").Change("rtl");
        await Assert.That(component.Find("h1").TextContent).IsEqualTo(heading);
        await Assert.That(component.Find(".main-layout").GetAttribute("dir")).IsEqualTo("rtl");
        await Assert.That(component.Find("#appearance-mode").GetAttribute("value")).IsEqualTo("darkhighcontrast");
        await Assert.That(context.Services.GetRequiredService<NavigationManager>().Uri).IsEqualTo(route);
        await component.Find("nav button[aria-controls='navigation-settings']").ClickAsync(new MouseEventArgs());
        await component.Find("nav button[aria-controls='navigation-settings']").ClickAsync(new MouseEventArgs());
        await Assert.That(component.Find("#appearance-mode").GetAttribute("value")).IsEqualTo("darkhighcontrast");
        await Assert.That(component.Find("#appearance-direction").GetAttribute("value")).IsEqualTo("rtl");
    }

    [Test]
    public async Task PreferenceChangesRefreshTheSwitcherWithoutRecreatingItsParent()
    {
        await using var context = BrowserFixture.Context();
        var component = context.Render<ThemeQuickSwitcher>();
        await component.InvokeAsync(async () => await context.Services.GetRequiredService<AppearanceThemeService>().SetAsync("dark", "rtl"));
        await Assert.That(component.Find("#appearance-mode").GetAttribute("value")).IsEqualTo("dark");
        await Assert.That(component.Find("#appearance-direction").GetAttribute("value")).IsEqualTo("rtl");
    }

    [Test]
    public async Task AppearanceChangesPreserveCurrentAnswersAndPreparedOutput()
    {
        await using var context = BrowserFixture.Context();
        var exports = context.JSInterop.SetupModule("./browser-artifacts.js");
        exports.SetupVoid("open", _ => true).SetVoidResult();
        exports.Setup<bool>("copy", _ => true).SetResult(true);
        exports.SetupVoid("invalidate", _ => true).SetVoidResult();
        context.Services.GetRequiredService<NavigationManager>().NavigateTo($"launch/{BrowserFixture.Family}?digest={BrowserFixture.Definition.Digest.Value}");
        var component = context.Render<App>();
        await component.Find("nav button[aria-controls='navigation-settings']").ClickAsync(new MouseEventArgs());
        await BrowserFixture.NotifyChoiceAsync(component, "mode", "single");
        component.Find("[data-field='hostname'] input").Input("synthetic.example.test");
        await BrowserFixture.PrepareAsync(component, draft: true);
        component.Find(".configuration-editor__artifact button").Click();
        byte[] before = (byte[])exports.Invocations.Single(call => call.Identifier == "open").Arguments[2]!;
        component.Find("#appearance-mode").Change("dark");
        component.Find("#appearance-direction").Change("rtl");
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
}
