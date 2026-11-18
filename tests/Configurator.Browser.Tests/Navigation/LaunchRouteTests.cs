namespace Configurator.Browser.Tests;

using System.Text;
using System.Text.Json;
using Bunit;
using Configurator.Browser.Models;
using Configurator.Browser.Pages;
using Configurator.Browser.Services;
using Configurator.Core.Definitions;
using Configurator.Presentation.Store;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

public sealed class LaunchRouteTests
{
    [Test]
    public async Task NeutralLaunchOffersConditionalFieldsAndSecretDeferredDraft()
    {
        var registry = BrowserDefinitionRegistry.LoadBundled();
        Definition definition = registry.Current.Resolve(Guid.Parse("4d8950ad-972c-4357-8605-7a7c89c05a17"));
        await using var context = BrowserFixture.Context(registry);
        context.Services.GetRequiredService<NavigationManager>()
            .NavigateTo($"/launch/{definition.FamilyId:D}?digest={definition.Digest.Value}");
        var component = context.Render<App>();

        await Assert.That(component.FindAll("[data-field='metrics.enabled'] .configuration-field__choice")).IsEmpty();
        await BrowserFixture.NotifyChoiceAsync(component, "service.mode", "extended");
        await Assert.That(component.FindAll("[data-field='metrics.enabled'] .configuration-field__choice")).IsEmpty();
        component.Find("#configuration-detail").Change("advanced");
        BrowserFixture.SectionNavigation(context, component).Find("[data-section='operations']").Click();
        await BrowserFixture.NotifyChoiceAsync(component, "metrics.enabled", "true");
        await Assert.That(component.FindAll("[data-field='service.credential'] input")).IsEmpty();
        await Assert.That(component.FindAll("input[type=text]")).IsNotEmpty();
        await Assert.That(component.FindAll("a[download]")).IsEmpty();
        await BrowserFixture.PrepareAsync(component, draft: true);
        await Assert.That(component.FindAll(".configuration-editor__artifact").Count).IsEqualTo(3);
        await Assert.That(component.FindAll(".configuration-editor__artifact").All(artifact =>
            artifact.QuerySelector("[data-prepared-file]")?.GetAttribute("data-readiness") == "draft")).IsTrue();
    }

    [Test]
    public async Task BundledNeutralDefinitionHasNativeCanonicalIdentityAndPublicSafeRawBytes()
    {
        using Stream nativeInput = typeof(LaunchRouteTests).Assembly.GetManifestResourceStream("NativeNeutralDefinition")!;
        using var nativeBytes = new MemoryStream();
        nativeInput.CopyTo(nativeBytes);
        Definition native = DefinitionCodec.ParseJson(nativeBytes.ToArray());
        Definition browser = BrowserDefinitionRegistry.LoadBundled().Current.Resolve(native.FamilyId);
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
        await Assert.That(raw.RootElement.GetProperty("configuration").GetProperty("properties").EnumerateObject()
            .Select(property => property.Value)
            .Where(field => field.GetProperty("x-configurator").GetProperty("sensitivity").GetString() != "public")
            .All(field => !field.TryGetProperty("default", out _) &&
                !field.GetProperty("x-configurator").TryGetProperty("initialValue", out _) &&
                !field.GetProperty("x-configurator").TryGetProperty("suggestedValue", out _))).IsTrue();
        await Assert.That(raw.RootElement.GetProperty("artifacts").EnumerateArray()
            .All(artifact =>
            {
                string name = artifact.GetProperty("fileName").GetString()!;
                return !Path.IsPathRooted(name) && !name.Contains('/') && !name.Contains('\\')
                    && !name.Contains(':') && name is not ("." or "..");
            })).IsTrue();
        await Assert.That(Encoding.UTF8.GetString(publicBytes.ToArray())).DoesNotContain("private-canary");
        await Assert.That(Encoding.UTF8.GetString(publicBytes.ToArray())).DoesNotContain("synthetic-placeholder");

        await using var context = BrowserFixture.Context(BrowserDefinitionRegistry.LoadBundled());
        var catalogue = context.Render<Catalogue>();
        await Assert.That(catalogue.Find($"a[href='launch/{native.FamilyId:D}?digest={native.Digest.Value}']").GetAttribute("href"))
            .IsEqualTo($"launch/{native.FamilyId:D}?digest={native.Digest.Value}");
    }

    [Test]
    public async Task ExternalBundledDefinitionKeepsNativeDigestAndOffersFullLocalControls()
    {
        var registry = BrowserDefinitionRegistry.LoadBundled();
        var definition = registry.Current.Resolve(Guid.Parse("f79259e8-af23-49b0-8d8d-6e371cc96080"));
        await using var context = BrowserFixture.Context(registry);
        context.Services.GetRequiredService<NavigationManager>()
            .NavigateTo($"/launch/{definition.FamilyId:D}?digest={definition.Digest.Value}&entry=environment");
        var component = context.Render<App>();

        await Assert.That(component.Find("[data-digest]").GetAttribute("data-digest"))
            .IsEqualTo(definition.Digest.Value);
        await BrowserFixture.NotifyChoiceAsync(component, "operation", "environment");
        component.Find("#configuration-detail").Change("advanced");
        await Assert.That(component.FindAll("[data-field]").Count).IsGreaterThan(1);
        var form = component.FindComponents<ConfigurationField>()[0].Instance.Form;
        string securitySection = form.Sections.Single(section =>
            section.Fields.Contains("security")).Id;
        await BrowserFixture.SectionNavigation(context, component).Find($"[data-section='{securitySection}']").ClickAsync(new MouseEventArgs());
        // Including the optional security group replaces the former inclusion toggle.
        await component.Find("[data-field='security'] > fieldset > button[data-action='supply-object']").ClickAsync(new MouseEventArgs());
        string targetSection = form.Sections.Single(section =>
            section.Fields.Contains("security")).Id;
        if (component.Find("[data-section-panel]").GetAttribute("data-section-panel") != targetSection)
            BrowserFixture.SectionNavigation(context, component).Find($"[data-section='{targetSection}']").Click();
        await Assert.That(component.Find("[data-field='security.admissions.credentialLookup.activeKeyVersion'] input").HasAttribute("disabled")).IsFalse();
        await Assert.That(component.FindAll("input[type=text]")).IsNotEmpty();
        await Assert.That(component.FindAll("a[download]")).IsEmpty();
    }

    [Test]
    [Arguments("not-a-guid", "", "family-invalid")]
    [Arguments("9B6A7B3E-45BF-4485-8FA1-D7888938C09B", "", "family-invalid")]
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
        await using var context = BrowserFixture.Context();
        context.Services.GetRequiredService<NavigationManager>().NavigateTo($"/launch/{family}{query}");
        var component = context.Render<App>();
        await Assert.That(component.Find("[role=alert]").GetAttribute("data-code")).IsEqualTo(code);
        await Assert.That(component.FindAll("main select")).IsEmpty();
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
            try { _ = BrowserLaunchSelection.Resolve(new DefinitionRegistry([BrowserFixture.Definition]), BrowserFixture.Family, query); }
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
        var registry = new DefinitionRegistry([first, second, incompatible]);
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
        await using var context = BrowserFixture.Context(new BrowserDefinitionRegistry([BrowserFixture.Definition, BrowserFixture.Revision(2)]));
        context.Services.GetRequiredService<NavigationManager>().NavigateTo($"/launch/{BrowserFixture.Family}");
        var component = context.Render<App>();
        await Assert.That(component.Find("[role=alert]").GetAttribute("data-code")).IsEqualTo("selection-ambiguous");
    }

    [Test]
    public async Task ActiveWorkspacePinsRevisionAndDigestAcrossRegistryReplacementAndRerender()
    {
        Definition first = BrowserFixture.Definition;
        var registry = new BrowserDefinitionRegistry([first]);
        await using var context = BrowserFixture.Context(registry);
        context.Services.GetRequiredService<NavigationManager>().NavigateTo($"/launch/{BrowserFixture.Family}?appVersion=1.0");
        var component = context.Render<App>();
        registry.Replace([BrowserFixture.Revision(2)]);
        component.FindComponent<Launch>().Render(parameters => parameters.Add(item => item.FamilyId, BrowserFixture.Family));
        await BrowserFixture.NotifyChoiceAsync(component, "mode", "single");
        component.Find("[data-field='hostname'] input").Input("example.test");
        await BrowserFixture.PrepareAsync(component, draft: true);
        await Assert.That(component.Find("[data-digest]").GetAttribute("data-digest")).IsEqualTo(first.Digest.Value);
        await Assert.That(component.Find("[data-revision]").GetAttribute("data-revision")).IsEqualTo("1");
        await Assert.That(component.FindAll(".configuration-editor__artifact")).IsNotEmpty();
    }

    [Test]
    public async Task QueryNavigationStartsNewWorkspaceAndClearsPriorDownload()
    {
        var registry = new BrowserDefinitionRegistry([BrowserFixture.Definition]);
        await using var context = BrowserFixture.Context(registry);
        var navigation = new RendererNavigationManager(context);
        context.Services.AddSingleton<NavigationManager>(navigation);
        navigation.NavigateTo($"/launch/{BrowserFixture.Family}?appVersion=1.0");
        await navigation.Completion.WaitAsync(TimeSpan.FromSeconds(5));
        var component = context.Render<App>();
        await BrowserFixture.NotifyChoiceAsync(component, "mode", "single");
        component.Find("[data-field='hostname'] input").Input("example.test");
        await BrowserFixture.PrepareAsync(component, draft: true);
        registry.Replace([BrowserFixture.Revision(2)]);
        await component.InvokeAsync(() => navigation.NavigateTo($"/launch/{BrowserFixture.Family}?appVersion=1.0&entry=setup"));
        await component.Find("[data-action='keep-workspace']").ClickAsync(new MouseEventArgs());
        await navigation.Completion.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(component.Find("[data-revision]").GetAttribute("data-revision")).IsEqualTo("1");
        await Assert.That(component.FindAll(".configuration-editor__artifact")).IsNotEmpty();
        await component.InvokeAsync(() => navigation.NavigateTo($"/launch/{BrowserFixture.Family}?appVersion=1.0&entry=setup"));
        await component.Find("[data-action='confirm-discard']").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        await navigation.Completion.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(component.Find("[data-revision]").GetAttribute("data-revision")).IsEqualTo("2");
        await Assert.That(component.FindAll("a[download]")).IsEmpty();
        await component.InvokeAsync(() => navigation.NavigateTo($"/launch/{BrowserFixture.Family}?entry=missing"));
        await navigation.Completion.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(component.Find("[role=alert]").GetAttribute("data-code")).IsEqualTo("entry-unknown");
        await Assert.That(component.FindAll("main select")).IsEmpty();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task RegistryCannotReplaceSameFamilyRevisionWithDifferentContent(bool removeBeforeConflict)
    {
        Definition first = BrowserFixture.Definition;
        var registry = new BrowserDefinitionRegistry([first]);
        Definition current = first;
        if (removeBeforeConflict)
        {
            current = BrowserFixture.Revision(2);
            registry.Replace([current]);
        }
        byte[] changedBytes = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(first.CanonicalBytes.Span)
            .Replace("Example service", "Changed service", StringComparison.Ordinal));
        Definition changed = DefinitionCodec.ParseJson(changedBytes);
        DefinitionException? failure = null;
        try { registry.Replace([changed]); }
        catch (DefinitionException exception) { failure = exception; }
        await Assert.That(failure?.Code).IsEqualTo("revision-conflict");
        await Assert.That(registry.Current.Resolve(first.FamilyId).Digest).IsEqualTo(current.Digest);
    }

    [Test]
    [Arguments("fresh")]
    [Arguments("import")]
    [Arguments("profile")]
    public async Task PublicEntryModeIntentOpensRequestedTaskAfterAdmissionWithoutAutoConsentOrFileReads(string mode)
    {
        var registry = new BrowserDefinitionRegistry([BrowserFixture.Definition]);
        var selection = BrowserLaunchSelection.Resolve(registry.Current, BrowserFixture.Family, $"?appVersion=1.0&mode={mode}");
        await Assert.That(selection.Mode).IsEqualTo(mode);

        await using var context = BrowserFixture.Context(registry);
        context.Services.GetRequiredService<NavigationManager>().NavigateTo($"/launch/{BrowserFixture.Family}?appVersion=1.0&mode={mode}");
        var component = context.Render<App>();

        await Assert.That(component.Find("#secret-consent").HasAttribute("checked")).IsFalse();
        await Assert.That(component.FindAll("[data-returning-entry] [data-entry-choice]").Count).IsEqualTo(3);
        var importPanel = component.Find("details[data-import-panel]");
        var profilePanel = component.Find("details[data-profile-panel]");
        await Assert.That(importPanel.HasAttribute("open")).IsEqualTo(mode == "import");
        await Assert.That(profilePanel.HasAttribute("open")).IsEqualTo(mode == "profile");

        if (mode == "fresh")
        {
            await component.Find("[data-entry-choice='import']").ClickAsync(new MouseEventArgs());
            await Assert.That(component.Find("details[data-import-panel]").HasAttribute("open")).IsTrue();
            await Assert.That(component.Find("details[data-profile-panel]").HasAttribute("open")).IsFalse();
            await Assert.That(component.Find("#secret-consent").HasAttribute("checked")).IsFalse();

            await component.Find("[data-entry-choice='profile']").ClickAsync(new MouseEventArgs());
            await Assert.That(component.Find("details[data-profile-panel]").HasAttribute("open")).IsTrue();
            await Assert.That(component.Find("details[data-import-panel]").HasAttribute("open")).IsFalse();
            await Assert.That(component.Find("#secret-consent").HasAttribute("checked")).IsFalse();

            await component.Find("[data-entry-choice='fresh']").ClickAsync(new MouseEventArgs());
            await Assert.That(component.Find("details[data-import-panel]").HasAttribute("open")).IsFalse();
            await Assert.That(component.Find("details[data-profile-panel]").HasAttribute("open")).IsFalse();
        }
    }

    [Test]
    public async Task CatalogueHidesFilterBarBelowFourProjectsAndKeepsDigestDetailsCollapsed()
    {
        var bundled = BrowserDefinitionRegistry.LoadBundled();
        await using var fewContext = BrowserFixture.Context(bundled);
        var fewProvider = TestDefinitionStoreProvider.FromIndex([]);
        fewContext.Services.AddSingleton<IDefinitionStoreProvider>(fewProvider);
        var fewCatalogue = fewContext.Render<Catalogue>();

        await Assert.That(fewCatalogue.FindAll("article").Count).IsLessThan(4);
        await Assert.That(fewCatalogue.FindAll("#catalogue-search")).IsEmpty();
        await Assert.That(fewCatalogue.FindAll("[data-digest-disclosure]")).IsEmpty();
        await Assert.That(fewCatalogue.FindAll("[data-action='open-project-details']").Count).IsGreaterThan(0);
        await Assert.That(fewCatalogue.FindAll("[data-project-origin='built-in']").Count).IsGreaterThan(0);

        DefinitionStoreProject storeWithDigest = BrowserDefinitionStoreTests.CreateProject("minimal-self-hosted", "Minimal self-hosted", BrowserFixture.Definition);
        await using BunitContext storeContext = BrowserFixture.Context(bundled);
        storeContext.Services.AddSingleton<IDefinitionStoreProvider>(TestDefinitionStoreProvider.FromIndex([storeWithDigest]));
        IRenderedComponent<Configurator.Browser.Pages.StoreProject> storePage =
            storeContext.Render<Configurator.Browser.Pages.StoreProject>(parameters => parameters.Add(page => page.ProjectId, "minimal-self-hosted"));
        await Assert.That(storePage.FindAll("[data-digest-disclosure]").All(details => !details.HasAttribute("open"))).IsTrue();

        Definition[] many =
        [
            BrowserFixture.Definition,
            BrowserDefinitionStoreTests.WithIdentity(BrowserFixture.Definition, Guid.Parse("11111111-1111-4111-8111-111111111101"), "publisher.example.one", 1),
            BrowserDefinitionStoreTests.WithIdentity(BrowserFixture.Definition, Guid.Parse("11111111-1111-4111-8111-111111111102"), "publisher.example.two", 1),
            BrowserDefinitionStoreTests.WithIdentity(BrowserFixture.Definition, Guid.Parse("11111111-1111-4111-8111-111111111103"), "publisher.example.three", 1)
        ];
        DefinitionStoreProject[] projects =
        [
            BrowserDefinitionStoreTests.CreateProject("one", "Project One", many[1]),
            BrowserDefinitionStoreTests.CreateProject("two", "Project Two", many[2]),
            BrowserDefinitionStoreTests.CreateProject("three", "Project Three", many[3])
        ];
        await using var manyContext = BrowserFixture.Context(new BrowserDefinitionRegistry([many[0]]));
        manyContext.Services.AddSingleton<IDefinitionStoreProvider>(
            TestDefinitionStoreProvider.FromIndex(projects, ("one", 1, many[1]), ("two", 1, many[2]), ("three", 1, many[3])));
        var manyCatalogue = manyContext.Render<Catalogue>();
        await Assert.That(manyCatalogue.FindAll("article").Count).IsEqualTo(4);
        await Assert.That(manyCatalogue.FindAll("#catalogue-search").Count).IsEqualTo(1);
    }

    [Test]
    public async Task OpenAdmissionShowsPlainLanguageTaskCardsAndCollapsedDigestDisclosure()
    {
        await using var context = BrowserFixture.Context(new BrowserDefinitionRegistry([]));
        await using var policy = new BrowserOfflinePolicy(context.JSInterop.JSRuntime);
        context.Services.AddSingleton(policy);
        var page = context.Render<Configurator.Browser.Pages.Open>();
        var input = page.FindComponent<InputFile>();
        await page.InvokeAsync(() => input.UploadFiles(
            InputFileContent.CreateFromBinary(BrowserFixture.Definition.CanonicalBytes.ToArray(), "synthetic-open.json")));

        var digestDetails = page.Find("[data-digest-disclosure]");
        await Assert.That(digestDetails.HasAttribute("open")).IsFalse();
        await Assert.That(digestDetails.TextContent).Contains(BrowserFixture.Definition.Digest.Value);
        await Assert.That(page.FindAll("[data-task-card='fresh']").Count).IsEqualTo(1);
        await Assert.That(page.FindAll("[data-task-card='import']").Count).IsEqualTo(1);
        await Assert.That(page.FindAll("[data-task-card='profile']").Count).IsEqualTo(1);
        await Assert.That(page.Find("[data-action='open-workspace']")).IsNotNull();
    }

    [Test]
    public async Task NavigatingAwayFromLaunchToSettingsStillTriggersGuardAndInvalidatesEditor()
    {
        var registry = new BrowserDefinitionRegistry([BrowserFixture.Definition]);
        await using BunitContext context = BrowserFixture.Context(registry);
        var navigation = new RendererNavigationManager(context);
        context.Services.AddSingleton<NavigationManager>(navigation);
        navigation.NavigateTo($"/launch/{BrowserFixture.Family}?appVersion=1.0");
        await navigation.Completion.WaitAsync(TimeSpan.FromSeconds(5));
        IRenderedComponent<App> component = context.Render<App>();

        await BrowserFixture.NotifyChoiceAsync(component, "mode", "single");
        await component.Find("[data-field='hostname'] input").InputAsync(new ChangeEventArgs { Value = "discarded-on-settings-nav.example.test" });

        await component.InvokeAsync(() => navigation.NavigateTo("/settings"));
        await component.Find("[data-action='confirm-discard']").ClickAsync(new MouseEventArgs());
        await navigation.Completion.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.That(navigation.ToBaseRelativePath(navigation.Uri)).IsEqualTo("settings");
        await Assert.That(component.FindAll("[data-settings-section]")).Count().IsEqualTo(4);
        await Assert.That(component.FindAll("[data-action='return-to-configuration']")).IsEmpty();

        await component.InvokeAsync(() => navigation.NavigateTo($"/launch/{BrowserFixture.Family}?appVersion=1.0"));
        await navigation.Completion.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(component.Find("[data-field='hostname'] input").GetAttribute("value")).IsNotEqualTo("discarded-on-settings-nav.example.test");
    }
}
