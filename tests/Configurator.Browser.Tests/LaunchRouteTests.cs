namespace Configurator.Browser.Tests;

using System.Text;
using System.Text.Json;
using Bunit;
using Configurator.Browser.Pages;
using Configurator.Core.Definitions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

public sealed class LaunchRouteTests
{
    [Test]
    [Arguments("not-a-guid", "", "family-invalid")]
    [Arguments("69CDB122-41D2-4FAB-9B82-7929A889C726", "", "family-invalid")]
    [Arguments("00000000-0000-0000-0000-000000000000", "", "family-invalid")]
    [Arguments("11111111-1111-1111-1111-111111111111", "", "family-unknown")]
    [Arguments(BrowserFixture.Family, "?appVersion=2.0", "version-incompatible")]
    [Arguments(BrowserFixture.Family, "?entry=missing", "entry-unknown")]
    [Arguments(BrowserFixture.Family, "?locale=ar", "locale-unsupported")]
    [Arguments(BrowserFixture.Family, "?digest=invalid", "digest-invalid")]
    [Arguments(BrowserFixture.Family, "?digest=0000000000000000000000000000000000000000000000000000000000000000", "digest-mismatch")]
    [Arguments(BrowserFixture.Family, "?appVersion=1.0&appVersion=1.0", "query-invalid")]
    [Arguments(BrowserFixture.Family, "?entry=setup&%65ntry=setup", "query-invalid")]
    [Arguments(BrowserFixture.Family, "?source=https%3A%2F%2Fexample.test", "query-invalid")]
    [Arguments(BrowserFixture.Family, "?returnUrl=https%3A%2F%2Fexample.test", "query-invalid")]
    [Arguments(BrowserFixture.Family, "?token=private", "query-invalid")]
    [Arguments(BrowserFixture.Family, "?locale=en%", "query-invalid")]
    [Arguments(BrowserFixture.Family, "?appVersion=", "query-invalid")]
    [Arguments(BrowserFixture.Family, "?appVersion=1.0#secret", "query-invalid")]
    public async Task RealRouterRendersValueFreeRejection(string family, string query, string code)
    {
        using var context = BrowserFixture.Context();
        context.Services.GetRequiredService<NavigationManager>().NavigateTo($"/launch/{family}{query}");
        var component = context.Render<App>();
        await Assert.That(component.Find("[role=alert]").GetAttribute("data-code")).IsEqualTo(code);
        await Assert.That(component.FindAll("select")).IsEmpty();
        await Assert.That(component.FindAll("a[download]")).IsEmpty();
    }

    [Test]
    public async Task BoundedSelectorsRejectOversizedOrInvalidValues()
    {
        string[] queries =
        [
            "?appVersion=" + new string('a', 65), "?entry=" + new string('a', 129),
            "?locale=" + new string('a', 36), "?digest=" + new string('a', 65),
            "?locale=en%0A", "?entry=setup&", "?appVersion=" + new string('a', 1024),
            "?entry=https%3A%2F%2Fexample.test", "?appVersion=1.0&locale=en&locale=en"
        ];
        foreach (string query in queries)
        {
            BrowserLaunchException? failure = null;
            try { _ = BrowserLaunchSelection.Resolve(new SetupDefinitionRegistry([BrowserFixture.Definition]), BrowserFixture.Family, query); }
            catch (BrowserLaunchException exception) { failure = exception; }
            await Assert.That(failure?.Code).IsEqualTo("query-invalid");
        }
    }

    [Test]
    public async Task ExplicitCompatibilitySelectsHighestRevisionAndDigestCanSelectOlderRevision()
    {
        var first = BrowserFixture.Definition;
        var second = BrowserFixture.Revision(2);
        var incompatible = BrowserFixture.Revision(3, "2.0");
        var registry = new SetupDefinitionRegistry([first, second, incompatible]);
        var newest = BrowserLaunchSelection.Resolve(registry, BrowserFixture.Family, "?appVersion=1.0&entry=setup&locale=en");
        var pinned = BrowserLaunchSelection.Resolve(registry, BrowserFixture.Family, $"?appVersion=1.0&digest={first.Digest.Value}");
        await Assert.That(newest.Definition.Revision).IsEqualTo(2L);
        await Assert.That(pinned.Definition.Digest).IsEqualTo(first.Digest);
        await Assert.That(pinned.Entry).IsEqualTo("setup");
        await Assert.That(pinned.Locale).IsEqualTo("en");
    }

    [Test]
    public async Task UnversionedAmbiguousRegistryRendersRejection()
    {
        using var context = BrowserFixture.Context(new BrowserDefinitionRegistry([BrowserFixture.Definition, BrowserFixture.Revision(2)]));
        context.Services.GetRequiredService<NavigationManager>().NavigateTo($"/launch/{BrowserFixture.Family}");
        var component = context.Render<App>();
        await Assert.That(component.Find("[role=alert]").GetAttribute("data-code")).IsEqualTo("selection-ambiguous");
    }

    [Test]
    public async Task ActiveWorkspacePinsRevisionAndDigestAcrossRegistryReplacementAndRerender()
    {
        SetupDefinition first = BrowserFixture.Definition;
        var registry = new BrowserDefinitionRegistry([first]);
        using var context = BrowserFixture.Context(registry);
        context.Services.GetRequiredService<NavigationManager>().NavigateTo($"/launch/{BrowserFixture.Family}?appVersion=1.0");
        var component = context.Render<App>();
        registry.Replace([BrowserFixture.Revision(2)]);
        component.FindComponent<Launch>().Render(parameters => parameters.Add(item => item.FamilyId, BrowserFixture.Family));
        component.Find("#choice-mode").Change("single");
        component.Find("button").Click();
        using JsonDocument template = BrowserFixture.Download(component.Find("a[download]").GetAttribute("href")!);
        await Assert.That(component.Find("[data-digest]").GetAttribute("data-digest")).IsEqualTo(first.Digest.Value);
        await Assert.That(template.RootElement.GetProperty("revision").GetInt64()).IsEqualTo(1L);
        await Assert.That(template.RootElement.GetProperty("definitionDigest").GetString()).IsEqualTo(first.Digest.Value);
    }

    [Test]
    public async Task QueryNavigationStartsNewWorkspaceAndClearsPriorDownload()
    {
        var registry = new BrowserDefinitionRegistry([BrowserFixture.Definition]);
        using var context = BrowserFixture.Context(registry);
        var navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo($"/launch/{BrowserFixture.Family}?appVersion=1.0");
        var component = context.Render<App>();
        component.Find("#choice-mode").Change("single");
        component.Find("button").Click();
        registry.Replace([BrowserFixture.Revision(2)]);
        await component.InvokeAsync(() => navigation.NavigateTo($"/launch/{BrowserFixture.Family}?appVersion=1.0&entry=setup"));
        await Assert.That(component.Find("[data-revision]").GetAttribute("data-revision")).IsEqualTo("2");
        await Assert.That(component.FindAll("a[download]")).IsEmpty();
        await component.InvokeAsync(() => navigation.NavigateTo($"/launch/{BrowserFixture.Family}?entry=missing"));
        await Assert.That(component.Find("[role=alert]").GetAttribute("data-code")).IsEqualTo("entry-unknown");
        await Assert.That(component.FindAll("select")).IsEmpty();
    }

    [Test]
    public async Task RegistryCannotReplaceSameFamilyRevisionWithDifferentContent()
    {
        SetupDefinition first = BrowserFixture.Definition;
        var registry = new BrowserDefinitionRegistry([first]);
        byte[] changedBytes = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(first.CanonicalBytes.Span)
            .Replace("Example service", "Changed service", StringComparison.Ordinal));
        SetupDefinition changed = SetupDefinitionCodec.ParseJson(changedBytes);
        SetupDefinitionException? failure = null;
        try { registry.Replace([changed]); }
        catch (SetupDefinitionException exception) { failure = exception; }
        await Assert.That(failure?.Code).IsEqualTo("revision-conflict");
        await Assert.That(registry.Current.Resolve(first.FamilyId).Digest).IsEqualTo(first.Digest);
    }
}
