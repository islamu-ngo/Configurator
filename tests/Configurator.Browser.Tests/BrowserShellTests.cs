namespace Configurator.Browser.Tests;

using Bunit;
using Configurator.Browser.Layout;
using Configurator.Browser.Pages;
using Configurator.Browser.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

public sealed class BrowserShellTests
{
    [Test]
    public async Task ShellProvidesNamedRoutesMainSkipAndLocalSourceWithoutStore()
    {
        await using var context = BrowserFixture.Context();
        var component = context.Render<App>();
        await Assert.That(component.FindAll("main")).Count().IsEqualTo(1);
        await Assert.That(component.Find("a.skip-link").GetAttribute("href")).IsEqualTo("#main-content");
        await Assert.That(component.Find("main").GetAttribute("id")).IsEqualTo("main-content");
        await Assert.That(component.FindAll("h1")).Count().IsEqualTo(1);
        await Assert.That(component.Find("nav[aria-label='Application navigation'] a[href='']").TextContent).IsEqualTo("Home");
        await Assert.That(component.Find("nav[aria-label='Application navigation'] a[href='open']").TextContent).IsEqualTo("Open configuration");
        await Assert.That(component.Find("nav[aria-label='Application navigation'] a[href='help']").TextContent).IsEqualTo("Help");
        await Assert.That(context.JSInterop.Invocations.Any(call => call.Identifier.Contains("readAsset", StringComparison.Ordinal))).IsFalse();
        var help = context.Render<Help>();
        await Assert.That(help.FindAll("h1")).Count().IsEqualTo(1);
        await Assert.That(help.Find("a[href='open']").TextContent).IsEqualTo("Open configuration");
    }

    [Test]
    public async Task NavigationDisclosureSupportsEscapeAndReturnsFocusToItsTrigger()
    {
        await using var context = BrowserFixture.Context();
        var component = context.Render<NavMenu>();
        var toggle = component.Find("button[aria-controls='application-navigation']");
        await Assert.That(toggle.GetAttribute("aria-expanded")).IsEqualTo("false");
        toggle.Click();
        await Assert.That(component.Find("button").GetAttribute("aria-expanded")).IsEqualTo("true");
        component.Find("nav").KeyDown("Escape");
        await Assert.That(component.Find("button").GetAttribute("aria-expanded")).IsEqualTo("false");
        await Assert.That(context.JSInterop.Invocations.Any(call => call.Identifier == "Blazor._internal.domWrapper.focus")).IsTrue();
    }

    [Test]
    public async Task NamedAppearanceControlsPreserveCurrentRouteAndMainContent()
    {
        await using var context = BrowserFixture.Context();
        var component = context.Render<App>();
        string heading = component.Find("h1").TextContent;
        await Assert.That(component.Find("label[for='appearance-mode']").TextContent).IsEqualTo("Appearance");
        await Assert.That(component.Find("label[for='appearance-direction']").TextContent).IsEqualTo("Direction");
        component.Find("#appearance-mode").Change("darkhighcontrast");
        component.Find("#appearance-direction").Change("rtl");
        await Assert.That(component.Find("h1").TextContent).IsEqualTo(heading);
        await Assert.That(component.Find(".main-layout").GetAttribute("dir")).IsEqualTo("rtl");
        await Assert.That(component.Find("#appearance-mode").GetAttribute("value")).IsEqualTo("darkhighcontrast");
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
        component.Find("#choice-mode").Change("single");
        component.Find("#choice-hostname").Input("synthetic.example.test");
        component.Find("#prepare-draft").Click();
        component.Find(".configuration-editor__artifact button").Click();
        byte[] before = (byte[])exports.Invocations.Single(call => call.Identifier == "open").Arguments[2]!;
        component.Find("#appearance-mode").Change("dark");
        component.Find("#appearance-direction").Change("rtl");
        component.Find("a.skip-link").Click();
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
