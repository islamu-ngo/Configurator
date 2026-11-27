namespace Configurator.Browser.Tests;

using System.Text;
using System.Text.Json.Nodes;
using Bunit;
using Configurator.Core.Definitions;
using Configurator.Presentation;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

public sealed class BrowserCredentialEntryTests
{
    [Test]
    [Arguments("oninput")]
    [Arguments("onchange")]
    public async Task ConsentedFillNotificationsUpdateTheActualAnswer(string notification)
    {
        await using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, Definition()));
        await BrowserFixture.SetSecretConsentAsync(component, true);
        await component.Find("[data-field='created'] .configuration-field__input")
            .TriggerEventAsync(notification, new ChangeEventArgs { Value = "synthetic-manager-fill" });
        var form = component.FindComponents<ConfigurationField>()[0].Instance.Form;
        await Assert.That(form.Snapshot().Values["created"].GetString()).IsEqualTo("synthetic-manager-fill");
        await Assert.That(component.Find("[data-field='created'] input").GetAttribute("type")).IsEqualTo("password");
    }

    [Test]
    [Arguments("reconsent")]
    [Arguments("edit")]
    [Arguments("replacement")]
    [Arguments("disposal")]
    public async Task HeldFillCannotCrossWorkspaceInvalidation(string transition)
    {
        await using var context = BrowserFixture.Context();
        using var form = new DefinitionFormModel(Definition(), DefinitionSessionPrivacy.BrowserSecretsEnabled);
        using var replacement = new DefinitionFormModel(Definition(), DefinitionSessionPrivacy.BrowserSecretsEnabled);
        context.ComponentFactories.Add<ConfigurationField, EmittedInputField>();
        var component = context.Render<ConfigurationSectionFields>(parameters => parameters
            .Add(item => item.Form, form)
            .Add(item => item.Fields, form.Validate().Fields.Where(field => field.Id == "created").ToArray()));
        var input = component.FindComponent<EmittedInputField>().Instance;
        object callback = input.CaptureInput();
        if (transition == "reconsent")
        {
            form.SetSecretEntryEnabled(false);
            form.SetSecretEntryEnabled(true);
            component.Render();
        }
        else if (transition == "edit")
        {
            form.EditValue(["existing"], "synthetic-current-edit");
            component.Render();
        }
        else if (transition == "replacement")
            component.Render(parameters => parameters.Add(item => item.Form, replacement));
        else input.Dispose();
        DefinitionFormModel current = transition == "replacement" ? replacement : form;
        long revision = current.InputRevision;
        var args = new ChangeEventArgs { Value = "synthetic-late-fill" };
        await component.InvokeAsync(() => callback switch
        {
            EventCallback<ChangeEventArgs> typed => typed.InvokeAsync(args),
            EventCallback untyped => untyped.InvokeAsync(args),
            MulticastDelegate handler => new EventCallbackWorkItem(handler).InvokeAsync(args),
            _ => throw new InvalidOperationException("input-handler-missing")
        });
        await Assert.That(current.Snapshot().Values.ContainsKey("created")).IsFalse();
        await Assert.That(form.Snapshot().Values.ContainsKey("created")).IsFalse();
        await Assert.That(current.InputRevision).IsEqualTo(revision);
        if (transition == "edit")
            await Assert.That(form.Snapshot().Values["existing"].GetString()).IsEqualTo("synthetic-current-edit");
        if (transition != "disposal")
            await Assert.That(component.Markup).DoesNotContain("synthetic-late-fill");
    }

    public sealed class EmittedInputField : ConfigurationField
    {
        public object CaptureInput()
        {
            using var builder = new RenderTreeBuilder();
            BuildRenderTree(builder);
            var frames = builder.GetFrames();
            return frames.Array.Take(frames.Count).Single(frame =>
                frame.FrameType == RenderTreeFrameType.Attribute && frame.AttributeName == "oninput").AttributeValue;
        }
    }

    [Test]
    public async Task SiblingFillRefreshesCurrentInputAndRejectsItsPreviouslyEmittedHandler()
    {
        await using var context = BrowserFixture.Context();
        context.ComponentFactories.Add<ConfigurationField, EmittedInputField>();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, Definition()));
        await BrowserFixture.SetSecretConsentAsync(component, true);
        object held = component.FindComponents<EmittedInputField>()
            .Single(item => item.Instance.Path.SequenceEqual(["created"])).Instance.CaptureInput();

        await component.Find("[data-field='existing'] .configuration-field__input")
            .TriggerEventAsync("oninput", new ChangeEventArgs { Value = "synthetic-sibling-fill" });
        await component.Find("[data-field='created'] .configuration-field__input")
            .TriggerEventAsync("oninput", new ChangeEventArgs { Value = "synthetic-current-fill" });
        var form = component.FindComponents<EmittedInputField>()[0].Instance.Form;
        long revision = form.InputRevision;
        var late = new ChangeEventArgs { Value = "synthetic-held-fill" };
        await component.InvokeAsync(() => held switch
        {
            EventCallback<ChangeEventArgs> typed => typed.InvokeAsync(late),
            EventCallback untyped => untyped.InvokeAsync(late),
            MulticastDelegate handler => new EventCallbackWorkItem(handler).InvokeAsync(late),
            _ => throw new InvalidOperationException("input-handler-missing")
        });

        await Assert.That(form.Snapshot().Values["existing"].GetString()).IsEqualTo("synthetic-sibling-fill");
        await Assert.That(form.Snapshot().Values["created"].GetString()).IsEqualTo("synthetic-current-fill");
        await Assert.That(form.InputRevision).IsEqualTo(revision);
        await Assert.That(component.Markup).DoesNotContain("synthetic-held-fill");
    }

    [Test]
    public async Task DeniedCredentialsHaveNoAnswerBearingInputsAndDisclosureCannotGrantConsent()
    {
        await using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, Definition()));
        await Assert.That(component.FindAll("[data-field='created'] input")).IsEmpty();
        await Assert.That(component.FindAll("[data-field='existing'] input")).IsEmpty();
        await component.Find("#configuration-detail").ChangeAsync(new ChangeEventArgs { Value = "advanced" });
        await Assert.That(component.FindAll("[data-field='issuer'] input")).IsEmpty();
        await Assert.That(component.Find("[data-workspace-consent-chip]").GetAttribute("data-consent-active")).IsEqualTo("false");
    }

    [Test]
    public async Task ConsentedPasswordsHaveStableFamilyIdentityAndHonestPurposeHints()
    {
        await using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, Definition()));
        await BrowserFixture.SetSecretConsentAsync(component, true);
        await component.Find("#configuration-detail").ChangeAsync(new ChangeEventArgs { Value = "advanced" });
        var created = component.Find("[data-field='created'] input");
        var existing = component.Find("[data-field='existing'] input");
        var issuer = component.Find("[data-field='issuer'] input");
        var form = component.FindComponents<ConfigurationField>()[0].Instance.Form;
        await Assert.That(created.Id).IsEqualTo(form.GetEditorIdentity(["created"]));
        await Assert.That(created.GetAttribute("name")).IsEqualTo(created.Id);
        await Assert.That(created.GetAttribute("type")).IsEqualTo("password");
        await Assert.That(created.GetAttribute("autocomplete")).IsEqualTo("section-" + created.Id + " new-password");
        await Assert.That(existing.GetAttribute("autocomplete")).IsEqualTo("section-" + existing.Id + " current-password");
        await Assert.That(issuer.GetAttribute("autocomplete")).IsEqualTo("off");
        await Assert.That(component.FindAll("form")).IsEmpty();
        await Assert.That(component.FindAll("input[type='hidden']")).IsEmpty();
    }

    [Test]
    public async Task RevocationRemovesControlsAndReconsentCannotRecoverPreviousPrivateValues()
    {
        await using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, Definition()));
        await BrowserFixture.SetSecretConsentAsync(component, true);
        await component.Find("[data-field='created'] input").InputAsync(new ChangeEventArgs { Value = "synthetic-manager-value" });
        await BrowserFixture.SetSecretConsentAsync(component, false);
        await Assert.That(component.FindAll("[data-field='created'] input")).IsEmpty();
        var form = component.FindComponents<ConfigurationField>()[0].Instance.Form;
        await Assert.That(form.Snapshot().Values.ContainsKey("created")).IsFalse();
        await BrowserFixture.SetSecretConsentAsync(component, true);
        await Assert.That(component.Find("[data-field='created'] input").GetAttribute("value")).IsEqualTo("");
        await Assert.That(component.Markup).DoesNotContain("synthetic-manager-value");
    }

    [Test]
    public async Task SingleConsentDestinationConsolidatesCustodyHelpAndLockedFieldActionOpensWithoutEnablingConsent()
    {
        await using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, Definition()));
        var form = component.FindComponents<ConfigurationField>()[0].Instance.Form;
        string custodyHelp = Configurator.Browser.Resources.BrowserStrings.Get("CredentialCustodyHelp", "en");

        await Assert.That(component.FindAll("[data-trust-privacy-panel]")).IsEmpty();

        await component.Find("[data-field='created'] [data-action='field-secret-consent']").ClickAsync(new MouseEventArgs());
        await Assert.That(component.FindAll("[data-trust-privacy-panel]")).Count().IsEqualTo(1);
        await Assert.That(component.Find("#secret-consent-help").TextContent).Contains(custodyHelp);
        await Assert.That(form.SessionPrivacy).IsEqualTo(DefinitionSessionPrivacy.BrowserNoSecrets);
        await Assert.That(component.Find("#secret-consent").HasAttribute("checked")).IsFalse();

        await component.Find("#secret-consent").ChangeAsync(new ChangeEventArgs { Value = true });
        await Assert.That(component.Find("[data-field='created'] .configuration-field__help").TextContent).DoesNotContain(custodyHelp);
        await Assert.That(component.Find("[data-field='existing'] .configuration-field__help").TextContent).DoesNotContain(custodyHelp);
    }

    [Test]
    public async Task SynchronousConsentRevocationAndVariantReplacementRejectInFlightSecretOperations()
    {
        await using var context = BrowserFixture.Context();
        var javascript = new BrowserExportLifecycleTests.GatedJavascript(context.JSInterop.JSRuntime);
        await using var module = new BrowserExportLifecycleTests.RecordingModule { HoldOpening = true };
        javascript.Release.TrySetResult(module);
        context.Services.AddSingleton<IJSRuntime>(javascript);

        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, Definition()));
        await BrowserFixture.SetSecretConsentAsync(component, true);
        await component.Find("[data-field='created'] input").InputAsync(new ChangeEventArgs { Value = "synthetic-race-secret-value" });
        var form = component.FindComponents<ConfigurationField>()[0].Instance.Form;
        var revoked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void Observe(object? sender, EventArgs args)
        {
            if (form.SessionPrivacy == DefinitionSessionPrivacy.BrowserNoSecrets) revoked.TrySetResult();
        }
        form.InputChanged += Observe;

        Task copyInFlight = component.Find("[data-field='created'] [data-secret-action='copy']").ClickAsync(new MouseEventArgs());
        Task revoking = Task.CompletedTask;
        try
        {
            await module.OpeningStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            revoking = BrowserFixture.SetSecretConsentAsync(component, false);
            await revoked.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await Assert.That(form.Snapshot().Values.ContainsKey("created")).IsFalse();
        }
        finally
        {
            form.InputChanged -= Observe;
            module.OpeningRelease.TrySetResult();
            await Task.WhenAll(copyInFlight, revoking).WaitAsync(TimeSpan.FromSeconds(5));
        }

        await Assert.That(form.SessionPrivacy).IsEqualTo(DefinitionSessionPrivacy.BrowserNoSecrets);
        await Assert.That(form.Snapshot().Values.ContainsKey("created")).IsFalse();
        await Assert.That(module.ExposureCount).IsEqualTo(0);
        await Assert.That(component.FindAll("[data-secret-status='copied']")).IsEmpty();
    }

    private static Definition Definition()
    {
        JsonObject source = JsonNode.Parse(BrowserFixture.Definition.CanonicalBytes.Span)!.AsObject();
        source["configuration"]!["required"] = new JsonArray("created", "existing");
        source["configuration"]!["properties"] = JsonNode.Parse("""
        {"created":{"type":"string","title":"Create password",
          "x-configurator":{"sensitivity":"secret","detail":"basic","secret":{"kind":"password","acquisition":"local"}}},
         "existing":{"type":"string","title":"Existing password",
          "x-configurator":{"sensitivity":"secret","detail":"basic","secret":{"kind":"password","acquisition":"existing"}}},
         "issuer":{"type":"string","title":"Issuer token",
          "x-configurator":{"sensitivity":"secret","detail":"advanced","secret":{"kind":"opaque-token","acquisition":"issuer","encoding":"base64url"}}}}
        """);
        source.Remove("presentation");
        source["artifacts"]![0]!["projection"] = JsonNode.Parse("""{"op":"literal","value":{}}""");
        BrowserFixture.SetCapabilities(source, "projection.literal", "schema.object", "schema.string", "schema.secret");
        return DefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString()));
    }
}
