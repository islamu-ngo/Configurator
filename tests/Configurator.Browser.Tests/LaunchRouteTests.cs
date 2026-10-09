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
    public async Task NeutralLaunchKeepsConditionalPublicTemplatePartialForNativeCompletion()
    {
        var registry = BrowserDefinitionRegistry.LoadBundled();
        SetupDefinition definition = registry.Current.Resolve(Guid.Parse("69cdb122-41d2-4fab-9b82-7929a889c726"));
        using var context = BrowserFixture.Context(registry);
        context.Services.GetRequiredService<NavigationManager>()
            .NavigateTo($"/launch/{definition.FamilyId:D}?digest={definition.Digest.Value}");
        var component = context.Render<App>();

        await Assert.That(component.FindAll("#choice-metrics\\.enabled")).IsEmpty();
        component.Find("#choice-service\\.mode").Change("extended");
        component.Find("#choice-metrics\\.enabled").Change("true");
        await Assert.That(component.FindAll("[data-field='service.credential']")).IsEmpty();
        await Assert.That(component.Find("[data-native-required]").GetAttribute("data-native-required")).IsEqualTo("1");
        component.Find("button").Click();
        using JsonDocument template = BrowserFixture.Download(component.Find("a[download]").GetAttribute("href")!);
        await Assert.That(template.RootElement.GetProperty("selections").GetProperty("service.mode").GetString())
            .IsEqualTo("extended");
        await Assert.That(template.RootElement.GetProperty("selections").GetProperty("metrics.enabled").GetBoolean())
            .IsTrue();
        BrowserTemplateResult imported = BrowserPublicTemplate.Parse(definition,
            Encoding.UTF8.GetBytes(template.RootElement.GetRawText()));
        await Assert.That(imported.IsAccepted).IsTrue();
        await Assert.That(DefinitionValidator.Validate(definition, imported.Answers!).IsReady).IsFalse();
        foreach (string rejected in new[]
        {
            """{"service.mode":"minimal","metrics.enabled":true}""",
            """{"service.credential":"synthetic-placeholder"}""",
            """{"service.name":"sample"}"""
        })
            await Assert.That(BrowserPublicTemplate.Parse(definition,
                BrowserFixture.Template(rejected, definition)).IsAccepted).IsFalse();
    }

    [Test]
    public async Task BundledNeutralDefinitionHasNativeCanonicalIdentityAndPublicSafeRawBytes()
    {
        using Stream nativeInput = typeof(LaunchRouteTests).Assembly.GetManifestResourceStream("NativeNeutralDefinition")!;
        using var nativeBytes = new MemoryStream();
        nativeInput.CopyTo(nativeBytes);
        SetupDefinition native = SetupDefinitionCodec.ParseJson(nativeBytes.ToArray());
        SetupDefinition browser = BrowserDefinitionRegistry.LoadBundled().Current.Resolve(native.FamilyId);
        await Assert.That(browser.Revision).IsEqualTo(native.Revision);
        await Assert.That(browser.Digest).IsEqualTo(native.Digest);
        await Assert.That(browser.CanonicalBytes.Span.SequenceEqual(native.CanonicalBytes.Span)).IsTrue();

        string[] resources = typeof(BrowserDefinitionRegistry).Assembly.GetManifestResourceNames();
        await Assert.That(resources.Any(name => name.Contains("answers", StringComparison.OrdinalIgnoreCase)))
            .IsFalse();
        string resourceName = resources
            .Single(name => name.EndsWith("neutral-service.json", StringComparison.Ordinal));
        using Stream publicInput = typeof(BrowserDefinitionRegistry).Assembly.GetManifestResourceStream(resourceName)!;
        using var publicBytes = new MemoryStream();
        publicInput.CopyTo(publicBytes);
        using JsonDocument raw = JsonDocument.Parse(publicBytes.ToArray());
        await Assert.That(raw.RootElement.TryGetProperty("answers", out _)).IsFalse();
        await Assert.That(raw.RootElement.GetProperty("fields").EnumerateArray()
            .Where(field => field.GetProperty("sensitivity").GetString() != "public")
            .All(field => !field.TryGetProperty("default", out _))).IsTrue();
        await Assert.That(raw.RootElement.GetProperty("artifacts").EnumerateArray()
            .All(artifact =>
            {
                string name = artifact.GetProperty("fileName").GetString()!;
                return !Path.IsPathRooted(name) && !name.Contains('/') && !name.Contains('\\')
                    && !name.Contains(':') && name is not ("." or "..");
            })).IsTrue();
        await Assert.That(Encoding.UTF8.GetString(publicBytes.ToArray())).DoesNotContain("private-canary");
        await Assert.That(Encoding.UTF8.GetString(publicBytes.ToArray())).DoesNotContain("synthetic-placeholder");

        using var context = BrowserFixture.Context(BrowserDefinitionRegistry.LoadBundled());
        var catalogue = context.Render<Catalogue>();
        await Assert.That(catalogue.Find(".definition-list a").GetAttribute("href"))
            .IsEqualTo($"launch/{native.FamilyId:D}?digest={native.Digest.Value}");
    }

    [Test]
    public async Task ExternalBundledDefinitionKeepsNativeDigestAndOnlyOffersPublicChoices()
    {
        var registry = BrowserDefinitionRegistry.LoadBundled();
        var definition = registry.Current.Resolve(Guid.Parse("a4bcfaa6-9979-43e5-b1f5-00d552618cdf"));
        using var context = BrowserFixture.Context(registry);
        context.Services.GetRequiredService<NavigationManager>()
            .NavigateTo($"/launch/{definition.FamilyId:D}?digest={definition.Digest.Value}&entry=environment");
        var component = context.Render<App>();

        await Assert.That(component.Find("[data-digest]").GetAttribute("data-digest"))
            .IsEqualTo(definition.Digest.Value);
        await Assert.That(component.FindAll("[data-field]").Count).IsEqualTo(1);
        component.Find("#choice-operation").Change("environment");
        foreach (var field in definition.Fields.Where(field => !BrowserPublicTemplate.IsSelectable(field)))
            await Assert.That(component.FindAll($"[id=\"choice-{field.Id}\"]")).IsEmpty();
        await Assert.That(component.FindAll("input[type=password],textarea,input[type=text]")).IsEmpty();
        component.Find("button").Click();
        using JsonDocument template = BrowserFixture.Download(component.Find("a[download]").GetAttribute("href")!);
        await Assert.That(template.RootElement.GetProperty("definitionDigest").GetString())
            .IsEqualTo(definition.Digest.Value);
        foreach (JsonProperty choice in template.RootElement.GetProperty("selections").EnumerateObject())
            await Assert.That(BrowserPublicTemplate.IsSelectable(definition.Fields.Single(field => field.Id == choice.Name)))
                .IsTrue();
    }

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
    [Arguments(false)]
    [Arguments(true)]
    public async Task RegistryCannotReplaceSameFamilyRevisionWithDifferentContent(bool removeBeforeConflict)
    {
        SetupDefinition first = BrowserFixture.Definition;
        var registry = new BrowserDefinitionRegistry([first]);
        SetupDefinition current = first;
        if (removeBeforeConflict)
        {
            current = BrowserFixture.Revision(2);
            registry.Replace([current]);
        }
        byte[] changedBytes = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(first.CanonicalBytes.Span)
            .Replace("Example service", "Changed service", StringComparison.Ordinal));
        SetupDefinition changed = SetupDefinitionCodec.ParseJson(changedBytes);
        SetupDefinitionException? failure = null;
        try { registry.Replace([changed]); }
        catch (SetupDefinitionException exception) { failure = exception; }
        await Assert.That(failure?.Code).IsEqualTo("revision-conflict");
        await Assert.That(registry.Current.Resolve(first.FamilyId).Digest).IsEqualTo(current.Digest);
    }
}
