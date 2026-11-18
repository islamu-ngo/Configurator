namespace Configurator.Browser.Tests;

using System.Text;
using System.Text.Json.Nodes;
using Bunit;
using Configurator.Browser.Components.Configuration;
using Configurator.Browser.Components.Docking;
using Configurator.Browser.Layout;
using Configurator.Browser.Resources;
using Configurator.Browser.Services;
using Configurator.Browser.Services.Docking;
using Configurator.Core.Definitions;
using Configurator.Presentation;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

public sealed class BrowserConfigurationNavigationTests
{
    [Test]
    public async Task EditorModeFocusTargetsTheMountedPanelWithoutChangingAnswers()
    {
        await using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, GroupedDefinition()));
        var form = component.FindComponent<ConfigurationConfigurePanel>().Instance.Form;
        long revision = form.InputRevision;

        await component.Find("[data-editing-mode='developer']").ClickAsync(new MouseEventArgs());
        await Assert.That(context.JSInterop.Invocations.Last(call => call.Identifier == "Blazor._internal.domWrapper.focus").Arguments[0])
            .IsEqualTo(component.FindComponent<ConfigurationDeveloper>().Instance.Heading!.Value);

        await component.Find("[data-editing-mode='form']").ClickAsync(new MouseEventArgs());
        await Assert.That(context.JSInterop.Invocations.Last(call => call.Identifier == "Blazor._internal.domWrapper.focus").Arguments[0])
            .IsEqualTo(component.FindComponent<ConfigurationConfigurePanel>().Instance.SectionHeading!.Value);
        await Assert.That(form.InputRevision).IsEqualTo(revision);
    }

    [Test]
    [Arguments("standard")]
    [Arguments("advanced")]
    public async Task AuthoredOptionalTuningDisclosurePreservesTheAcceptedRevision(string detail)
    {
        JsonNode document = JsonNode.Parse(GroupedDefinition().CanonicalBytes.Span)!;
        BrowserFixture.WithField(document, "tuning", """
            {"type":"string","title":"Optional tuning","x-configurator":{"sensitivity":"public","detail":"advanced"}}
            """);
        document["presentation"]!["sections"]![0]!["pointers"]!.AsArray().Add("/tuning");
        document["configuration"]!["properties"]!["tuning"]!["x-configurator"]!["detail"] = detail;
        Definition definition = DefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(document.ToJsonString()));
        await using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, definition));
        var form = component.FindComponents<ConfigurationField>()[0].Instance.Form;
        await Assert.That(component.FindAll("[data-field='tuning'] input")).IsEmpty();
        await component.Find("[data-action='show-section-options']").ClickAsync(new MouseEventArgs());
        await Assert.That(component.FindAll("[data-field='tuning'] input")).Count().IsEqualTo(1);
        await Assert.That(form.InputRevision).IsEqualTo(0);
        await Assert.That(form.Definition.Digest).IsEqualTo(definition.Digest);
    }

    [Test]
    public async Task SectionSwitchingPreservesConsentAnswersAndPinnedIdentity()
    {
        Definition definition = GroupedDefinition();
        await using var context = BrowserFixture.Context();
        var component = context.Render<MainLayout>(parameters => parameters.Add(item => item.Body, builder =>
        {
            builder.OpenComponent<ConfigurationEditor>(0);
            builder.AddAttribute(1, nameof(ConfigurationEditor.Definition), definition);
            builder.CloseComponent();
        }));
        await Assert.That(component.FindAll("#application-navigation [data-section]")).IsEmpty();
        await Assert.That(component.FindAll(".configuration-configure-panel .configuration-configure-panel__nav nav.configuration-navigation[aria-label='Sections'] [data-section]").Count).IsEqualTo(2);
        await Assert.That(component.FindAll("nav nav")).IsEmpty();
        await BrowserFixture.NotifyChoiceAsync(component, "mode", "single");
        component.Find("[data-field='hostname'] input").Input("example.test");
        component.Find("#secret-consent").Change(true);
        await Assert.That(component.Find("button[aria-controls='application-navigation']").GetAttribute("aria-expanded")).IsEqualTo("true");
        await component.Find("button[aria-controls='application-navigation']").ClickAsync(new MouseEventArgs());
        await Assert.That(component.Find("button[aria-controls='application-navigation']").GetAttribute("aria-expanded")).IsEqualTo("false");

        // Form section rail remains mounted, interactive, and retains entered answers + secret consent while global Menu is collapsed.
        await Assert.That(component.FindAll(".configuration-configure-panel nav [data-section]").Count).IsEqualTo(2);
        await component.Find(".configuration-configure-panel nav [data-section='credentials']").ClickAsync(new MouseEventArgs());
        await Assert.That(context.JSInterop.Invocations.Last(call => call.Identifier == "Blazor._internal.domWrapper.focus").Arguments[0])
            .IsEqualTo(component.FindComponent<ConfigurationConfigurePanel>().Instance.SectionHeading!.Value);
        component.Find("[data-field='credential'] input").Input("synthetic-navigation-secret");

        await component.Find("button[aria-controls='application-navigation']").ClickAsync(new MouseEventArgs());
        await Assert.That(component.Find("button[aria-controls='application-navigation']").GetAttribute("aria-expanded")).IsEqualTo("true");
        await component.Find(".configuration-configure-panel nav [data-section='connection']").ClickAsync(new MouseEventArgs());
        await Assert.That(component.Find("[data-field='hostname'] input").GetAttribute("value")).IsEqualTo("example.test");
        await Assert.That(component.Find("#secret-consent").HasAttribute("checked")).IsTrue();
        await Assert.That(component.FindAll("[data-section-panel='credentials']")).IsEmpty();
        var form = component.FindComponents<ConfigurationField>()[0].Instance.Form;
        await Assert.That(form.Snapshot().Values["credential"].GetString()).IsEqualTo("synthetic-navigation-secret");
        await Assert.That(form.Definition.Digest).IsEqualTo(definition.Digest);
    }

    [Test]
    public async Task HiddenCorrectionOpensItsSectionAndDeniedSecretOffersConsentRecovery()
    {
        await using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, GroupedDefinition()));
        await BrowserFixture.NotifyChoiceAsync(component, "mode", "single");
        component.Find("[data-field='hostname'] input").Input("example.test");
        await BrowserFixture.PrepareAsync(component, draft: false);
        await Assert.That(component.FindAll(".configuration-editor__artifact")).IsEmpty();
        await BrowserFixture.SelectPanelAsync(component, "review");
        component.Find("[data-correction='credential']").Click();
        await Assert.That(component.Find("[data-section-panel='credentials']").HasAttribute("hidden")).IsFalse();
        await Assert.That(component.FindAll("[data-field='credential'] input")).IsEmpty();
        await Assert.That(component.Find("#secret-consent").HasAttribute("checked")).IsFalse();
        await BrowserFixture.SelectPanelAsync(component, "review");
        await Assert.That(component.FindAll("[data-action='enable-secret-entry']")).Count().IsEqualTo(1);
        component.Find("[data-action='enable-secret-entry']").Click();
        await Assert.That(component.Find("[data-field='credential'] input").HasAttribute("disabled")).IsFalse();
        await Assert.That(component.Find("[data-field='credential'] input").GetAttribute("type")).IsEqualTo("password");
    }

    [Test]
    public async Task MechanicalFallbackPreservesAuthoredTitlesAndExactKeys()
    {
        JsonNode document = JsonNode.Parse(BrowserFixture.Definition.CanonicalBytes.Span)!;
        BrowserFixture.WithField(document, "HTTPServer_URL", """
            {"type":"string","x-configurator":{"sensitivity":"public","detail":"standard"}}
            """);
        BrowserFixture.WithField(document, "authored_key", """
            {"type":"string","title":"Publisher's exact wording","x-configurator":{"sensitivity":"public","detail":"standard"}}
            """);
        BrowserFixture.WithField(document, "optional_note", """
            {"type":"string","title":"Optional note","x-configurator":{"sensitivity":"public","detail":"advanced"}}
            """);
        Definition definition = DefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(document.ToJsonString()));
        await using var context = BrowserFixture.Context();
        var component = context.Render<MainLayout>(parameters => parameters.Add(item => item.Body, builder =>
        {
            builder.OpenComponent<ConfigurationEditor>(0);
            builder.AddAttribute(1, nameof(ConfigurationEditor.Definition), definition);
            builder.CloseComponent();
        }));
        string fallbackInputId = component.Find("[data-field='HTTPServer_URL'] input").Id!;
        await Assert.That(component.Find($"label[for='{fallbackInputId}']").TextContent).Contains("HTTP Server URL");
        await component.Find("[data-field='HTTPServer_URL'] [data-action='field-help']").ClickAsync(new MouseEventArgs());
        await Assert.That(component.Find("[data-definition-path]").TextContent).IsEqualTo("HTTPServer_URL");
        await component.Find("[aria-label='Close field help']").ClickAsync(new MouseEventArgs());
        string authoredInputId = component.Find("[data-field='authored_key'] input").Id!;
        await Assert.That(component.Find($"label[for='{authoredInputId}']").TextContent).Contains("Publisher's exact wording");
        await Assert.That(component.FindAll("#configuration-experience")).IsEmpty();
        await Assert.That(component.Find("[data-experience]").GetAttribute("data-experience")).IsEqualTo("configure");
        await Assert.That(component.Find("[data-field='optional_note'] input").HasAttribute("disabled")).IsFalse();
        var form = component.FindComponents<ConfigurationField>()[0].Instance.Form;
        await component.InvokeAsync(() => form.SetAllSectionsExpanded(true));
        await Assert.That(component.Find("[data-field='optional_note'] input").HasAttribute("disabled")).IsFalse();
        await component.InvokeAsync(() => form.SetAllSectionsExpanded(false));
        await Assert.That(component.Find("[data-field='optional_note'] input").HasAttribute("disabled")).IsFalse();
        await Assert.That(component.FindComponents<ConfigurationField>()[0].Instance.Form.Definition.Digest).IsEqualTo(definition.Digest);
    }

    [Test]
    public async Task AuthoredExperienceAndDisclosureChangesPreservePreparedOutput()
    {
        await using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, GroupedDefinition()));
        var navigation = BrowserFixture.SectionNavigation(context, component);
        await Assert.That(component.Find("#configuration-experience").GetAttribute("value")).IsEqualTo("quick-start");
        await Assert.That(component.FindAll("#configuration-experience option")).Count().IsEqualTo(2);
        await Assert.That(component.Find("#configuration-experience option[value='quick-start']").TextContent).IsEqualTo("Connect service");
        await Assert.That(component.Find("#configuration-experience option[value='inspect-settings']").TextContent).IsEqualTo("Inspect configuration");
        await BrowserFixture.NotifyChoiceAsync(component, "mode", "single");
        component.Find("[data-field='hostname'] input").Input("example.test");
        component.Find("#secret-consent").Change(true);
        navigation.Find("[data-section='credentials']").Click();
        component.Find("[data-field='credential'] input").Input("synthetic-experience-secret");
        var form = component.FindComponents<ConfigurationField>()[0].Instance.Form;
        await BrowserFixture.PrepareAsync(component, draft: false);
        await Assert.That(component.FindAll(".configuration-editor__artifact")).Count().IsEqualTo(1);
        long revision = form.InputRevision;

        await BrowserFixture.SelectPanelAsync(component, "configure");
        component.Find("#configuration-experience").Change("inspect-settings");
        await Assert.That(form.SelectedExperienceId).IsEqualTo("inspect-settings");
        await Assert.That(component.Find("[data-navigation]").GetAttribute("data-navigation")).IsEqualTo("wizard");
        await Assert.That(navigation.FindAll("[data-section]")[0].GetAttribute("data-section")).IsEqualTo("credentials");
        component.Find("#configuration-detail").Change("advanced");
        await Assert.That(form.DisclosureOverride).IsEqualTo(FieldDisclosure.Advanced);
        component.Find("#configuration-detail").Change("essential");
        await Assert.That(form.DisclosureOverride).IsEqualTo(FieldDisclosure.Essential);
        component.Find("#configuration-experience").Change("quick-start");
        await Assert.That(form.DisclosureOverride).IsEqualTo(FieldDisclosure.Essential);

        await Assert.That(component.Find("[data-navigation]").GetAttribute("data-navigation")).IsEqualTo("wizard");
        await BrowserFixture.SelectPanelAsync(component, "prepare");
        await Assert.That(component.FindAll(".configuration-editor__artifact")).Count().IsEqualTo(1);
        await Assert.That(component.Find("[role=status]").GetAttribute("data-code")).IsEqualTo("preparation-ready");
        await Assert.That(form.InputRevision).IsEqualTo(revision);
        await Assert.That(form.Snapshot().Values["credential"].GetString()).IsEqualTo("synthetic-experience-secret");
        await BrowserFixture.SelectPanelAsync(component, "configure");
        await Assert.That(component.Find("#secret-consent").HasAttribute("checked")).IsTrue();
    }

    [Test]
    public async Task InvalidNestedAdvancedInputRemainsReachableAndBlocksPreparationAfterSwitchingViews()
    {
        JsonNode document = JsonNode.Parse(GroupedDefinition().CanonicalBytes.Span)!;
        BrowserFixture.WithField(document, "options", """
            {"type":"object","additionalProperties":false,"title":"Options","x-configurator":{"sensitivity":"public","detail":"advanced"},"properties":{
              "dotted.count":{"type":"integer","title":"Count","x-configurator":{"sensitivity":"public","detail":"advanced"}}}}
            """, "schema.object", "schema.integer");
        document["presentation"]!["sections"]!.AsArray().Add(JsonNode.Parse("""
            {"id":"tuning","title":"Advanced settings","pointers":["/options"]}
            """));
        foreach (JsonNode? experience in document["presentation"]!["journeys"]!.AsArray())
            experience!["sections"]!.AsArray().Add("tuning");
        Definition definition = DefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(document.ToJsonString()));
        await using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, definition));
        var navigation = BrowserFixture.SectionNavigation(context, component);
        await BrowserFixture.NotifyChoiceAsync(component, "mode", "single");
        component.Find("[data-field='hostname'] input").Input("example.test");
        await Assert.That(component.FindAll("[data-field='options.dotted.count'] input")).IsEmpty();
        component.Find("#configuration-experience").Change("inspect-settings");
        component.Find("#configuration-detail").Change("advanced");
        navigation.Find("[data-section='tuning']").Click();
        component.Find("[data-field='options.dotted.count'] input").Input("invalid-number");
        navigation.Find("[data-section='connection']").Click();
        component.Find("#configuration-experience").Change("quick-start");
        var form = component.FindComponents<ConfigurationField>()[0].Instance.Form;
        await BrowserFixture.PrepareAsync(component, draft: true);
        await Assert.That(component.FindAll(".configuration-editor__artifact")).IsEmpty();
        await Assert.That(component.Find("[role=status]").GetAttribute("data-code")).IsEqualTo("input-type-invalid");
        await component.InvokeAsync(() => form.SetDiscovery("hostname"));
        await BrowserFixture.SelectPanelAsync(component, "review");
        component.Find("[data-correction='options']").Click();
        await Assert.That(component.Find("[data-section-panel='tuning']").HasAttribute("hidden")).IsFalse();
        await Assert.That(component.Find("[data-field='options.dotted.count'] input").GetAttribute("aria-invalid")).IsEqualTo("true");
        await Assert.That(form.InputDiagnostics.Single().RelativePath.SequenceEqual(["dotted.count"])).IsTrue();
        await Assert.That(form.IsEditorVisible(["options", "dotted.count"])).IsTrue();
        await Assert.That(form.DiscoveryQuery).IsEqualTo("hostname");
        await Assert.That(component.FindComponents<ConfigurationField>().Single(field => field.Instance.Path.SequenceEqual(["options"]))
            .Instance.FocusPath.SequenceEqual(["options", "dotted.count"])).IsTrue();
        component.Find("[data-field='options.dotted.count'] input").Input("2");
        await BrowserFixture.PrepareAsync(component, draft: true);
        await Assert.That(component.FindAll(".configuration-editor__artifact")).Count().IsEqualTo(1);
    }

    private static Definition GroupedDefinition()
    {
        JsonNode document = JsonNode.Parse(BrowserFixture.Definition.CanonicalBytes.Span)!;
        document["presentation"] = JsonNode.Parse("""
            {"defaultJourney":"quick-start","navigation":"wizard","sections":[
              {"id":"connection","title":"General","pointers":["/mode","/replicas","/hostname"]},
              {"id":"credentials","title":"Security","pointers":["/credential"]}],
             "journeys":[
              {"id":"quick-start","title":"Connect service",
               "sections":["connection","credentials"]},
              {"id":"inspect-settings","title":"Inspect configuration",
               "sections":["credentials","connection"]}]}
            """);
        return DefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(document.ToJsonString()));
    }

    [Test]
    public async Task WizardNavigationSupportsBackNextJumpToRequiredAndOptionalMarkerWithoutMutatingAnswers()
    {
        JsonNode document = JsonNode.Parse(GroupedDefinition().CanonicalBytes.Span)!;
        BrowserFixture.WithField(document, "optional_note", """
            {"type":"string","title":"Optional note","x-configurator":{"sensitivity":"public","detail":"advanced"}}
            """);
        document["presentation"]!["sections"]![0]!["pointers"]!.AsArray().Add("/optional_note");
        Definition definition = DefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(document.ToJsonString()));

        await using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, definition));
        var navigation = BrowserFixture.SectionNavigation(context, component);
        var form = component.FindComponents<ConfigurationField>()[0].Instance.Form;
        long revision = form.InputRevision;

        await Assert.That(navigation.FindAll("[data-navigation-mode='wizard']")).Count().IsEqualTo(1);
        await Assert.That(navigation.FindAll("[data-wizard-optional-available='connection']")).Count().IsEqualTo(1);
        await Assert.That(navigation.Find("[data-action='wizard-back']").HasAttribute("disabled")).IsTrue();

        await navigation.Find("[data-action='wizard-next']").ClickAsync(new MouseEventArgs());
        await Assert.That(component.Find("[data-section-panel='credentials']")).IsNotNull();
        await Assert.That(navigation.Find("[data-action='wizard-back']").HasAttribute("disabled")).IsFalse();

        // Jump to next required section from a section when another required section needs work.
        await navigation.Find("[data-action='jump-next-required']").ClickAsync(new MouseEventArgs());
        await Assert.That(component.Find("[data-section-panel='connection']")).IsNotNull();
        await Assert.That(form.InputRevision).IsEqualTo(revision);

        // Switching to sections overview omits wizard chrome and preserves independent section progress + review status.
        JsonNode sectionsDoc = JsonNode.Parse(definition.CanonicalBytes.Span)!;
        sectionsDoc["presentation"]!["navigation"] = "sections";
        Definition sectionsDefinition = DefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(sectionsDoc.ToJsonString()));
        var sectionsComponent = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, sectionsDefinition));
        var overviewNav = BrowserFixture.SectionNavigation(context, sectionsComponent);
        await Assert.That(overviewNav.FindAll("[data-navigation-mode='sections']")).Count().IsEqualTo(1);
        await Assert.That(overviewNav.Find("[data-section='credentials'] [data-review-state]").GetAttribute("data-review-state")).IsEqualTo("not-visited");
    }

    [Test]
    public async Task PersistentLeftSectionRailStaysPopulatedInAllSectionsWithScrollspyLocationAndSectionReviewedAction()
    {
        Definition definition = GroupedDefinition();
        await using var context = BrowserFixture.Context();
        var component = context.Render<MainLayout>(parameters => parameters.Add(item => item.Body, builder =>
        {
            builder.OpenComponent<ConfigurationEditor>(0);
            builder.AddAttribute(1, nameof(ConfigurationEditor.Definition), definition);
            builder.CloseComponent();
        }));
        var panel = component.FindComponent<ConfigurationConfigurePanel>();

        await component.Find("[data-layout-mode='all-sections']").ClickAsync(new MouseEventArgs());

        // Persistent rail lives inside ConfigurationConfigurePanel __nav while [data-layout-switcher] leads __main above __fields.
        await Assert.That(component.FindAll("#application-navigation [data-section]")).IsEmpty();
        await Assert.That(component.FindAll(".configuration-configure-panel__nav [data-section]").Count).IsEqualTo(2);
        await Assert.That(component.FindAll(".configuration-configure-panel__nav [data-layout-switcher]")).IsEmpty();
        await Assert.That(component.FindAll(".configuration-configure-panel__main > .configuration-configure-panel__toolbar[data-layout-switcher]")).Count().IsEqualTo(1);
        await Assert.That(component.FindAll(".configuration-configure-panel__main > .configuration-configure-panel__fields")).Count().IsEqualTo(1);
        await Assert.That(component.FindAll(".configuration-compact-workspace__jumps, [data-compact-section-jump]")).IsEmpty();
        await Assert.That(component.Find(".configuration-configure-panel__nav [data-section='connection']").GetAttribute("aria-current")).IsEqualTo("location");

        // Scrollspy intersection callback updates active section aria-current="location" cleanly.
        await panel.InvokeAsync(() => panel.Instance.OnScrollspySectionChangedAsync("credentials"));
        await Assert.That(component.Find(".configuration-configure-panel__nav [data-section='credentials']").GetAttribute("aria-current")).IsEqualTo("location");
        await Assert.That(component.Find(".configuration-configure-panel__nav [data-section='connection']").GetAttribute("aria-current")).IsNull();

        // Explicit review toggle marks section reviewed and preserves state across layout switching, while retaining active scrollspy section.
        await component.Find("[data-action='toggle-section-reviewed'][data-section-id='connection']").ClickAsync(new MouseEventArgs());
        await Assert.That(component.Find(".configuration-configure-panel__nav [data-section='connection'] [data-review-state]").GetAttribute("data-review-state")).IsEqualTo("reviewed");

        await component.Find("[data-layout-mode='section-by-section']").ClickAsync(new MouseEventArgs());
        await Assert.That(component.Find(".configuration-configure-panel__nav [data-section='connection'] [data-review-state]").GetAttribute("data-review-state")).IsEqualTo("reviewed");
        await Assert.That(component.FindAll("[data-section-panel='credentials']")).Count().IsEqualTo(1);
        await Assert.That(component.Find(".configuration-configure-panel__nav [data-section='credentials']").GetAttribute("aria-current")).IsEqualTo("step");
    }

    [Test]
    public async Task SingleSectionAndLiteralRootDefinitionsOmitPersistentFormNavAndLowerWorkspaceFloor()
    {
        JsonNode singleSectionDoc = JsonNode.Parse(BrowserFixture.Definition.CanonicalBytes.Span)!;
        Definition singleSectionDef = DefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(singleSectionDoc.ToJsonString()));
        Definition literalRootDef = BrowserRootEditingTests.Definition("Conformance.RootMap");

        await using BunitContext context = BrowserFixture.Context();
        IRenderedComponent<ConfigurationEditor> singleEditor = context.Render<ConfigurationEditor>(p => p.Add(x => x.Definition, singleSectionDef));
        await Assert.That(singleEditor.FindAll(".configuration-configure-panel__nav")).IsEmpty();
        await Assert.That(singleEditor.FindComponent<DockLayoutHost>().Instance.MinimumContentWidth).IsEqualTo(DockLayoutState.DefaultMinimumContentWidth);

        IRenderedComponent<ConfigurationEditor> rootEditor = context.Render<ConfigurationEditor>(p => p.Add(x => x.Definition, literalRootDef));
        await Assert.That(rootEditor.FindAll(".configuration-configure-panel__nav")).IsEmpty();
        await Assert.That(rootEditor.FindComponent<DockLayoutHost>().Instance.MinimumContentWidth).IsEqualTo(DockLayoutState.DefaultMinimumContentWidth);
    }

    [Test]
    public async Task WorkspaceMinimumContentWidthProtects420PxFormCanvasAcrossDensitiesAndResetsOutsideDesktopConfigure()
    {
        Definition definition = GroupedDefinition();
        await using BunitContext context = BrowserFixture.Context();
        DockLayoutState dockLayout = context.Services.GetRequiredService<DockLayoutState>();
        AppearanceThemeService appearance = context.Services.GetRequiredService<AppearanceThemeService>();

        IRenderedComponent<MainLayout> app = context.Render<MainLayout>(parameters => parameters.Add(item => item.Body, builder =>
        {
            builder.OpenComponent<ConfigurationEditor>(0);
            builder.AddAttribute(1, nameof(ConfigurationEditor.Definition), definition);
            builder.CloseComponent();
        }));

        await app.InvokeAsync(() => dockLayout.UpdateViewport(1440, isMobile: false, DockScope.Shell, DockLayoutState.DefaultMinimumContentWidth));
        IRenderedComponent<DockLayoutHost> workspaceDock = app.FindComponents<DockLayoutHost>().Single(h => h.Instance.Scope == DockScope.Workspace);

        // Comfortable: 790px floor -> Inspector (360) stays docked at 1150px, overlays at 1149px while StartNav (256) stays docked.
        await Assert.That(workspaceDock.Instance.MinimumContentWidth).IsEqualTo(790);
        await app.Find("[data-action='toggle-inspector']").ClickAsync(new MouseEventArgs());

        await app.InvokeAsync(() => dockLayout.UpdateViewport(1150, isMobile: false));
        await Assert.That(dockLayout.ShouldRenderDockedPanelAsOverlay(dockLayout.GetPanel(BrowserDockPanels.InspectorId)!)).IsFalse();

        await app.InvokeAsync(() => dockLayout.UpdateViewport(1149, isMobile: false));
        await Assert.That(dockLayout.ShouldRenderDockedPanelAsOverlay(dockLayout.GetPanel(BrowserDockPanels.InspectorId)!)).IsTrue();
        await Assert.That(dockLayout.ShouldRenderDockedPanelAsOverlay(dockLayout.GetPanel(BrowserDockPanels.StartNavId)!)).IsFalse();

        // Spacious (806px), Compact (774px), Dense (766px).
        await app.InvokeAsync(() => appearance.SetDensityAsync("spacious").AsTask());
        await Assert.That(workspaceDock.Instance.MinimumContentWidth).IsEqualTo(806);

        await app.InvokeAsync(() => appearance.SetDensityAsync("compact").AsTask());
        await Assert.That(workspaceDock.Instance.MinimumContentWidth).IsEqualTo(774);

        await app.InvokeAsync(() => appearance.SetDensityAsync("dense").AsTask());
        await Assert.That(workspaceDock.Instance.MinimumContentWidth).IsEqualTo(766);

        await app.InvokeAsync(() => appearance.SetDensityAsync("comfortable").AsTask());
        await Assert.That(workspaceDock.Instance.MinimumContentWidth).IsEqualTo(790);

        // At <= 960px (where form nav collapses above form fields), Workspace floor drops to 420px so StartNav fits if Inspector is closed.
        await app.Find("[data-action='toggle-inspector']").ClickAsync(new MouseEventArgs());
        await app.InvokeAsync(() => dockLayout.UpdateViewport(1000, isMobile: false));
        await Assert.That(dockLayout.ShouldRenderDockedPanelAsOverlay(dockLayout.GetPanel(BrowserDockPanels.StartNavId)!)).IsTrue();

        await app.InvokeAsync(() => dockLayout.UpdateViewport(960, isMobile: false));
        await Assert.That(workspaceDock.Instance.MinimumContentWidth).IsEqualTo(DockLayoutState.DefaultMinimumContentWidth);
        await Assert.That(dockLayout.ShouldRenderDockedPanelAsOverlay(dockLayout.GetPanel(BrowserDockPanels.StartNavId)!)).IsFalse();

        // At 1000px desktop Configure, opening in-place Settings or switching to Review resets floor to 420px so StartNav fits docked.
        await app.InvokeAsync(() => dockLayout.UpdateViewport(1000, isMobile: false));
        await Assert.That(dockLayout.ShouldRenderDockedPanelAsOverlay(dockLayout.GetPanel(BrowserDockPanels.StartNavId)!)).IsTrue();

        await app.Find("header [data-action='open-settings']").ClickAsync(new MouseEventArgs());
        await Assert.That(workspaceDock.Instance.MinimumContentWidth).IsEqualTo(DockLayoutState.DefaultMinimumContentWidth);
        await Assert.That(dockLayout.ShouldRenderDockedPanelAsOverlay(dockLayout.GetPanel(BrowserDockPanels.StartNavId)!)).IsFalse();

        await app.Find("[data-action='return-to-configuration']").ClickAsync(new MouseEventArgs());
        await Assert.That(workspaceDock.Instance.MinimumContentWidth).IsEqualTo(790);
        await Assert.That(dockLayout.ShouldRenderDockedPanelAsOverlay(dockLayout.GetPanel(BrowserDockPanels.StartNavId)!)).IsTrue();

        await BrowserFixture.SelectPanelAsync(app, "review");
        await Assert.That(dockLayout.ShouldRenderDockedPanelAsOverlay(dockLayout.GetPanel(BrowserDockPanels.StartNavId)!)).IsFalse();
    }

    [Test]
    public async Task ArabicRtlPreservesSiblingNavLandmarksBidiIsolationAndZeroAppNavSections()
    {
        JsonNode document = JsonNode.Parse(GroupedDefinition().CanonicalBytes.Span)!;
        document["locales"] = new JsonArray("ar", "en");
        document["localization"] = JsonNode.Parse("""
            {"defaultLocale":"en","translations":{"ar":{"direction":"rtl","overrides":{}}}}
            """);
        Definition definition = DefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(document.ToJsonString()));
        await using BunitContext context = BrowserFixture.Context();
        BrowserNavigationState navState = context.Services.GetRequiredService<BrowserNavigationState>();
        navState.SelectLocale("ar");

        IRenderedComponent<MainLayout> component = context.Render<MainLayout>(parameters => parameters.Add(item => item.Body, builder =>
        {
            builder.OpenComponent<ConfigurationEditor>(0);
            builder.AddAttribute(1, nameof(ConfigurationEditor.Definition), definition);
            builder.AddAttribute(2, nameof(ConfigurationEditor.InitialLocale), "ar");
            builder.CloseComponent();
        }));

        await Assert.That(component.Find(".main-layout").GetAttribute("dir")).IsEqualTo("rtl");
        await Assert.That(component.Find(".configuration-editor").GetAttribute("dir")).IsEqualTo("rtl");
        await Assert.That(component.Find("#application-navigation").GetAttribute("aria-label")).IsEqualTo(BrowserStrings.Get("ApplicationNavigation", "ar"));
        await Assert.That(component.Find(".configuration-configure-panel__nav nav.configuration-navigation").GetAttribute("aria-label")).IsEqualTo(BrowserStrings.Get("Sections", "ar"));
        await Assert.That(component.FindAll("#application-navigation [data-section]")).IsEmpty();
        await Assert.That(component.FindAll("nav nav")).IsEmpty();
        await Assert.That(component.FindAll("bdi").Count).IsGreaterThan(0);
    }

    [Test]
    public async Task InPlaceSettingsRoundTripPreservesSeparatedSectionRailAnswersAndConsent()
    {
        Definition definition = GroupedDefinition();
        await using BunitContext context = BrowserFixture.Context();
        IRenderedComponent<MainLayout> component = context.Render<MainLayout>(parameters => parameters.Add(item => item.Body, builder =>
        {
            builder.OpenComponent<ConfigurationEditor>(0);
            builder.AddAttribute(1, nameof(ConfigurationEditor.Definition), definition);
            builder.CloseComponent();
        }));

        await BrowserFixture.NotifyChoiceAsync(component, "mode", "single");
        await component.Find("[data-field='hostname'] input").InputAsync(new ChangeEventArgs { Value = "roundtrip.example.test" });
        await component.Find("#secret-consent").ChangeAsync(new ChangeEventArgs { Value = true });
        await component.Find(".configuration-configure-panel__nav [data-section='credentials']").ClickAsync(new MouseEventArgs());

        await component.Find("header [data-action='open-settings']").ClickAsync(new MouseEventArgs());
        await Assert.That(component.FindAll("#application-navigation [data-section]")).IsEmpty();
        await Assert.That(component.FindAll("#application-navigation .nav-menu__workspace a")).Count().IsEqualTo(3);
        await Assert.That(component.FindAll("#application-navigation .nav-menu__footer .nav-menu__local")).Count().IsEqualTo(1);
        await Assert.That(component.Find("main#main-content").HasAttribute("hidden")).IsTrue();

        await component.Find("[data-action='return-to-configuration']").ClickAsync(new MouseEventArgs());
        await Assert.That(component.Find("main#main-content").HasAttribute("hidden")).IsFalse();
        await Assert.That(component.Find(".configuration-configure-panel__nav [data-section='credentials']").GetAttribute("aria-current")).IsEqualTo("step");
        await Assert.That(component.Find("#secret-consent").HasAttribute("checked")).IsTrue();

        await component.Find(".configuration-configure-panel__nav [data-section='connection']").ClickAsync(new MouseEventArgs());
        await Assert.That(component.Find("[data-field='hostname'] input").GetAttribute("value")).IsEqualTo("roundtrip.example.test");
    }

    [Test]
    public async Task MarkSectionReviewedExposesVisibleReviewedBadgeAlongsideSemanticState()
    {
        Definition definition = GroupedDefinition();
        await using BunitContext context = BrowserFixture.Context();
        IRenderedComponent<ConfigurationEditor> component = context.Render<ConfigurationEditor>(p => p.Add(x => x.Definition, definition));

        AngleSharp.Dom.IElement initialStatus = component.Find(".configuration-configure-panel__nav [data-section='connection'] [data-review-state]");
        await Assert.That(initialStatus.GetAttribute("data-review-state")).IsEqualTo("not-visited");
        await Assert.That(initialStatus.ClassList.Contains("sr-only")).IsTrue();

        await component.Find(".configuration-configure-panel__nav [data-section='credentials']").ClickAsync(new MouseEventArgs());
        AngleSharp.Dom.IElement openedStatus = component.Find(".configuration-configure-panel__nav [data-section='credentials'] [data-review-state]");
        await Assert.That(openedStatus.GetAttribute("data-review-state")).IsEqualTo("opened");
        await Assert.That(openedStatus.ClassList.Contains("sr-only")).IsTrue();

        await component.Find(".configuration-configure-panel__nav [data-section='connection']").ClickAsync(new MouseEventArgs());
        await component.Find("[data-action='toggle-section-reviewed'][data-section-id='connection']").ClickAsync(new MouseEventArgs());

        AngleSharp.Dom.IElement reviewedBadge = component.Find(".configuration-configure-panel__nav [data-section='connection'] [data-review-state='reviewed']");
        await Assert.That(reviewedBadge.ClassList.Contains("sr-only")).IsFalse();
        await Assert.That(reviewedBadge.ClassList.Contains("configuration-navigation__reviewed")).IsTrue();
        await Assert.That(reviewedBadge.TextContent.Trim()).IsEqualTo("Reviewed");
    }
}
