namespace Configurator.Browser.Tests;

using System.Text;
using System.Text.Json.Nodes;
using Bunit;
using Configurator.Browser.Layout;
using Configurator.Core.Definitions;
using Configurator.Presentation;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;

public sealed class BrowserExperienceWorkflowTests
{
    [Test]
    public async Task UnchangedChoiceRefreshesCurrentCallbackAndRejectsRetiredInput()
    {
        await using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters =>
            parameters.Add(item => item.Definition, BrowserFixture.Definition));
        await BrowserFixture.NotifyChoiceAsync(component, "mode", "single");
        var form = component.FindComponents<ConfigurationField>()[0].Instance.Form;
        var retired = component.FindComponent<MudSelect<string>>().Instance.ValueChanged;
        await component.Find("[data-field='hostname'] input").InputAsync(new ChangeEventArgs { Value = "current.test" });
        long revision = form.InputRevision;
        await component.InvokeAsync(() => retired.InvokeAsync("cluster"));
        await Assert.That(form.Snapshot().Values["mode"].GetString()).IsEqualTo("single");
        await Assert.That(form.InputRevision).IsEqualTo(revision);
        await BrowserFixture.NotifyChoiceAsync(component, "mode", "cluster");
        await Assert.That(form.Snapshot().Values["mode"].GetString()).IsEqualTo("cluster");
        await Assert.That(component.FindAll("[data-field='replicas'] div[role='combobox']").Count).IsEqualTo(1);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task DetailAndDensityRetainConsentDraftsIdentityAndCurrentPreparation(bool invalidDraft)
    {
        await using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, Definition()));
        var form = component.FindComponents<ConfigurationField>()[0].Instance.Form;
        await component.InvokeAsync(() =>
        {
            form.EditValue(["enabled"], "false");
            form.EditValue(["options", "host"], "example.test");
            form.EditValue(["options", "port"], "0");
            form.SetSecretEntryEnabled(true);
        });
        component.Find("#configuration-detail").Change("advanced");
        component.Find("[data-field='options.api_token'] input").Input("synthetic-density-secret");
        if (invalidDraft) component.Find("[data-field='options.limit'] input").Input("1e");
        else component.Find("#prepare-complete").Click();
        long revision = form.InputRevision;
        string identity = form.GetEditorIdentity(["options", "api_token"]);
        foreach (string density in new[] { "compact", "comfortable" })
        {
            component.Find("#configuration-density").Change(density);
            component.Find("#configuration-detail").Change("essential");
            component.Find("#configuration-detail").Change("advanced");
            await Assert.That(component.Find("[data-density]").GetAttribute("data-density")).IsEqualTo(density);
            await Assert.That(form.InputRevision).IsEqualTo(revision);
            await Assert.That(form.SessionPrivacy).IsEqualTo(DefinitionSessionPrivacy.BrowserSecretsEnabled);
            await Assert.That(form.GetEditorIdentity(["options", "api_token"])).IsEqualTo(identity);
            await Assert.That(form.Snapshot().Values["options"].GetProperty("api_token").GetString()).IsEqualTo("synthetic-density-secret");
            await Assert.That(component.FindAll(".configuration-editor__artifact").Count).IsEqualTo(invalidDraft ? 0 : 1);
            if (invalidDraft)
            {
                await Assert.That(component.Find("[data-field='options.limit'] input").GetAttribute("value")).IsEqualTo("1e");
                await Assert.That(form.HasInputErrors).IsTrue();
            }
        }
    }

    [Test]
    public async Task AuthoredDescriptionsReachTheSectionAndWorkspaceInspector()
    {
        await using var context = BrowserFixture.Context();
        var definition = Definition();
        var component = context.Render<MainLayout>(parameters => parameters.Add(item => item.Body, builder =>
        {
            builder.OpenComponent<ConfigurationEditor>(0);
            builder.AddAttribute(1, nameof(ConfigurationEditor.Definition), definition);
            builder.CloseComponent();
        }));
        string descriptionId = component.Find("[data-section-panel='decisions']").GetAttribute("aria-describedby")!;
        await Assert.That(component.Find("#" + descriptionId).TextContent).IsEqualTo(definition.Presentation!.Sections[0].Description);
        await component.Find("[aria-label='Help with this workspace']").ClickAsync(new MouseEventArgs());
        await Assert.That(component.Find("[data-experience-description]").TextContent).IsEqualTo(definition.Presentation!.Experiences[0].Description);
        await Assert.That(component.Find("[data-section-description]").TextContent).IsEqualTo(definition.Presentation!.Sections[0].Description);
    }

    [Test]
    public async Task CallbackFromUnmountedControlCannotEditItsRemountedField()
    {
        await using var context = BrowserFixture.Context();
        using var form = new DefinitionFormModel(Definition());
        form.SetDisclosureOverride(SetupFieldDisclosure.Advanced);
        var component = context.Render<BrowserCredentialEntryTests.EmittedInputField>(parameters => parameters
            .Add(item => item.Form, form).Add(item => item.Path, ["extra"])
            .Add(item => item.Schema, form.Definition.Fields.Single(field => field.Id == "extra").Node));
        object callback = component.Instance.CaptureInput();
        long revision = form.InputRevision;
        await component.InvokeAsync(() => form.SetDisclosureOverride(SetupFieldDisclosure.Essential));
        component.Render();
        await Assert.That(component.FindAll("input")).IsEmpty();
        await component.InvokeAsync(() => form.SetDisclosureOverride(SetupFieldDisclosure.Advanced));
        component.Render();
        await Assert.That(component.FindAll("input").Count).IsEqualTo(1);
        await component.InvokeAsync(() => callback switch
        {
            EventCallback<ChangeEventArgs> typed => typed.InvokeAsync(new ChangeEventArgs { Value = "retired-control-input" }),
            EventCallback untyped => untyped.InvokeAsync(new ChangeEventArgs { Value = "retired-control-input" }),
            MulticastDelegate handler => new EventCallbackWorkItem(handler).InvokeAsync(new ChangeEventArgs { Value = "retired-control-input" }),
            _ => throw new InvalidOperationException("input-handler-missing")
        });
        await Assert.That(form.Snapshot().Values.ContainsKey("extra")).IsFalse();
        await Assert.That(form.InputRevision).IsEqualTo(revision);
        component.Find("input").Input("current-control-input");
        await Assert.That(form.Snapshot().Values["extra"].GetString()).IsEqualTo("current-control-input");
    }

    [Test]
    public async Task QueuedInputFromOneRenderedEditorRetainsEveryAcceptedCharacter()
    {
        await using var context = BrowserFixture.Context();
        using var form = new DefinitionFormModel(Definition());
        var component = context.Render<BrowserCredentialEntryTests.EmittedInputField>(parameters => parameters
            .Add(item => item.Form, form).Add(item => item.Path, ["options", "host"])
            .Add(item => item.Schema, form.Definition.Fields[1].Properties[0].Node));
        object callback = component.Instance.CaptureInput();
        foreach (string text in new[] { "e", "ex", "example.test" })
            await component.InvokeAsync(() => callback switch
            {
                EventCallback<ChangeEventArgs> typed => typed.InvokeAsync(new ChangeEventArgs { Value = text }),
                EventCallback untyped => untyped.InvokeAsync(new ChangeEventArgs { Value = text }),
                MulticastDelegate handler => new EventCallbackWorkItem(handler).InvokeAsync(new ChangeEventArgs { Value = text }),
                _ => throw new InvalidOperationException("input-handler-missing")
            });
        await Assert.That(form.InputRevision).IsEqualTo(3);
        await Assert.That(form.Snapshot().Values["options"].GetProperty("host").GetString()).IsEqualTo("example.test");
    }

    [Test]
    public async Task RapidInputThenBlurDoesNotDuplicateTheAcceptedEdit()
    {
        await using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, Definition()));
        var form = component.FindComponents<ConfigurationField>()[0].Instance.Form;
        foreach (string text in new[] { "e", "ex", "example.test" })
            await component.Find("[data-field='options.host'] input").InputAsync(new ChangeEventArgs { Value = text });
        long revision = form.InputRevision;
        await component.Find("[data-field='options.host'] .configuration-field__input")
            .ChangeAsync(new ChangeEventArgs { Value = "example.test" });
        await Assert.That(form.InputRevision).IsEqualTo(revision);
        await Assert.That(form.Snapshot().Values["options"].GetProperty("host").GetString()).IsEqualTo("example.test");
        await component.Find("[data-field='options.host'] input").InputAsync(new ChangeEventArgs { Value = "example.test" });
        await Assert.That(form.InputRevision).IsEqualTo(revision + 1);
    }

    [Test]
    public async Task WorkflowSeparatesLocationFromTruthfulRequiredAnswerProgress()
    {
        await using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters =>
            parameters.Add(item => item.Definition, Definition()));
        await Assert.That(component.FindAll("[data-workflow-step]").Count).IsEqualTo(3);
        await Assert.That(component.Find("[data-required-count]").GetAttribute("data-required-count")).IsEqualTo("3");
        await Assert.That(component.Find("[data-ready-count]").GetAttribute("data-ready-count")).IsEqualTo("0");

        await BrowserFixture.NotifyChoiceAsync(component, "enabled", "false");
        await component.Find("[data-field='options.host'] input").InputAsync(new ChangeEventArgs { Value = "example.test" });
        await component.Find("[data-field='options.port'] input").InputAsync(new ChangeEventArgs { Value = "0" });

        await Assert.That(component.Find("[data-required-count]").GetAttribute("data-required-count")).IsEqualTo("3");
        await Assert.That(component.Find("[data-ready-count]").GetAttribute("data-ready-count")).IsEqualTo("3");
        await Assert.That(component.Find("[data-workflow-step='configure']").GetAttribute("aria-current")).IsEqualTo("step");
        await Assert.That(component.FindAll(".configuration-editor__artifact")).IsEmpty();
    }

    [Test]
    public async Task LocalDisclosureDoesNotGrantSecretConsentOrChangeAnswerRevision()
    {
        await using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters =>
            parameters.Add(item => item.Definition, Definition()));
        var form = component.FindComponents<ConfigurationField>()[0].Instance.Form;
        long revision = form.InputRevision;
        await Assert.That(component.FindAll("[data-field='options.limit'] input")).IsEmpty();
        await Assert.That(component.FindAll("[data-field='options.api_token'] input")).IsEmpty();

        await component.Find("[data-field='options'] button[data-action='expand-optional']")
            .ClickAsync(new MouseEventArgs());

        await Assert.That(component.FindAll("[data-field='options.limit'] input").Count).IsEqualTo(1);
        await Assert.That(form.InputRevision).IsEqualTo(revision);
        await Assert.That(form.CanEdit(["options", "api_token"])).IsFalse();
        await Assert.That(component.Find("#secret-consent").HasAttribute("checked")).IsFalse();
        await Assert.That(form.Snapshot().Values.Count).IsEqualTo(0);
    }

    [Test]
    public async Task SectionUnmountPreservesInvalidDraftAndCorrectionsRemainSticky()
    {
        await using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters =>
            parameters.Add(item => item.Definition, Definition()));
        var form = component.FindComponents<ConfigurationField>()[0].Instance.Form;
        await component.Find("[data-field='options'] button[data-action='expand-optional']")
            .ClickAsync(new MouseEventArgs());
        await component.Find("[data-field='options.limit'] input").InputAsync(new ChangeEventArgs { Value = "invalid-number" });
        long revision = form.InputRevision;

        await component.Find("[data-section='tuning']").ClickAsync(new MouseEventArgs());
        await Assert.That(component.FindAll("[data-field='options.limit'] input")).IsEmpty();
        await component.Find("[data-section='decisions']").ClickAsync(new MouseEventArgs());
        await Assert.That(component.Find("[data-field='options.limit'] input").GetAttribute("value")).IsEqualTo("invalid-number");
        await Assert.That(form.InputRevision).IsEqualTo(revision);
        await Assert.That(form.HasInputErrors).IsTrue();

        await component.Find("[data-field='options.limit'] input").InputAsync(new ChangeEventArgs { Value = "4" });
        await Assert.That(component.FindAll("[data-field='options.limit'] input").Count).IsEqualTo(1);
        await Assert.That(form.HasInputErrors).IsFalse();
    }

    [Test]
    public async Task GuidanceRendersInertInstructionsAndDeliberatePublicLinks()
    {
        await using var context = BrowserFixture.Context();
        var component = context.Render<MainLayout>(parameters => parameters.Add(item => item.Body, builder =>
        {
            builder.OpenComponent<ConfigurationEditor>(0);
            builder.AddAttribute(1, nameof(ConfigurationEditor.Definition), Definition());
            builder.CloseComponent();
        }));
        await component.Find("[data-field='enabled'] [data-action='field-help']").ClickAsync(new MouseEventArgs());
        var guidance = component.Find("[role='dialog']");

        await Assert.That(guidance.QuerySelectorAll("script")).IsEmpty();
        await Assert.That(guidance.QuerySelector("[data-help-instructions]")!.ChildElementCount).IsEqualTo(0);
        var link = guidance.QuerySelector("a")!;
        await Assert.That(link.GetAttribute("href")).IsEqualTo("https://docs.example.test/configure");
        await Assert.That(link.GetAttribute("rel")).Contains("noopener");
        await Assert.That(link.GetAttribute("rel")).Contains("noreferrer");
        await Assert.That(link.GetAttribute("referrerpolicy")).IsEqualTo("no-referrer");
    }

    private static SetupDefinition Definition()
    {
        JsonObject source = JsonNode.Parse(BrowserFixture.Definition.CanonicalBytes.Span)!.AsObject();
        source["fields"] = JsonNode.Parse("""
        [{"id":"enabled","type":"boolean","required":true,"title":"Enable service",
          "sensitivity":"public","disclosure":"essential","help":"Choose the service behavior.",
          "guidance":{"instructions":"Acquire this value locally and review target instructions before applying configuration.",
            "links":[{"title":"Configuration guide","url":"https://docs.example.test/configure"}]}},
         {"id":"options","type":"object","required":true,"title":"Connection",
          "sensitivity":"public","disclosure":"guided","properties":[
            {"id":"host","type":"string","required":true,"title":"Host","sensitivity":"public","disclosure":"guided"},
            {"id":"port","type":"integer","required":true,"title":"Port","sensitivity":"public","disclosure":"guided"},
            {"id":"limit","type":"integer","title":"Optional limit","sensitivity":"public","disclosure":"advanced"},
            {"id":"api_token","type":"string","title":"Optional issuer token","sensitivity":"secret","disclosure":"advanced"}]},
         {"id":"extra","type":"string","title":"Extra setting","sensitivity":"public","disclosure":"advanced"}]
        """);
        source["presentation"] = JsonNode.Parse("""
        {"defaultExperience":"connect","sections":[
          {"id":"decisions","title":"Connection","description":"Authored connection context.","fields":["enabled","options"]},
          {"id":"tuning","title":"Optional tuning","disclosure":"advanced","fields":["extra"]}],
         "experiences":[
          {"id":"connect","title":"Connect service","description":"Authored connection experience.","initialDisclosure":"essential","navigation":"guided",
           "sections":["decisions","tuning"]},
          {"id":"inspect","title":"Inspect settings","initialDisclosure":"advanced","navigation":"sections",
           "sections":["tuning","decisions"]}]}
        """);
        source["artifacts"]![0]!["projection"] = JsonNode.Parse("""{"op":"literal","value":{}}""");
        return SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString()));
    }
}
