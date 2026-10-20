namespace Configurator.Browser.Tests;

using System.Text;
using System.Text.Json.Nodes;
using Bunit;
using Configurator.Core.Definitions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.AspNetCore.Components.Rendering;
using Configurator.Presentation;

public sealed class BrowserCredentialEntryTests
{
    [Test]
    [Arguments("oninput")]
    [Arguments("onchange")]
    public async Task ConsentedFillNotificationsUpdateTheActualAnswer(string notification)
    {
        using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, Definition()));
        await component.Find("#secret-consent").ChangeAsync(new ChangeEventArgs { Value = true });
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
        using var context = BrowserFixture.Context();
        using var form = new DefinitionFormModel(Definition(), DefinitionSessionPrivacy.BrowserSecretsEnabled);
        using var replacement = new DefinitionFormModel(Definition(), DefinitionSessionPrivacy.BrowserSecretsEnabled);
        var component = context.Render<EmittedInputField>(parameters => parameters
            .Add(item => item.Form, form).Add(item => item.Path, ["created"])
            .Add(item => item.Schema, form.Definition.Fields[0].Constraints));
        object callback = component.Instance.CaptureInput();
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
        else component.Instance.Dispose();
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
    public async Task DeniedCredentialsHaveNoAnswerBearingInputsAndDisclosureCannotGrantConsent()
    {
        using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, Definition()));
        await Assert.That(component.FindAll("[data-field='created'] input")).IsEmpty();
        await Assert.That(component.FindAll("[data-field='existing'] input")).IsEmpty();
        await component.Find("[data-action='expand-all-optional']").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        await Assert.That(component.FindAll("[data-field='issuer'] input")).IsEmpty();
        await Assert.That(component.Find("#secret-consent").HasAttribute("checked")).IsFalse();
    }

    [Test]
    public async Task ConsentedPasswordsHaveStableFamilyIdentityAndHonestPurposeHints()
    {
        using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, Definition()));
        await component.Find("#secret-consent").ChangeAsync(new ChangeEventArgs { Value = true });
        await component.Find("[data-action='expand-all-optional']").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
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
        using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, Definition()));
        await component.Find("#secret-consent").ChangeAsync(new ChangeEventArgs { Value = true });
        await component.Find("[data-field='created'] input").InputAsync(new ChangeEventArgs { Value = "synthetic-manager-value" });
        await component.Find("#secret-consent").ChangeAsync(new ChangeEventArgs { Value = false });
        await Assert.That(component.FindAll("[data-field='created'] input")).IsEmpty();
        var form = component.FindComponents<ConfigurationField>()[0].Instance.Form;
        await Assert.That(form.Snapshot().Values.ContainsKey("created")).IsFalse();
        await component.Find("#secret-consent").ChangeAsync(new ChangeEventArgs { Value = true });
        await Assert.That(component.Find("[data-field='created'] input").GetAttribute("value")).IsEqualTo("");
        await Assert.That(component.Markup).DoesNotContain("synthetic-manager-value");
    }

    private static SetupDefinition Definition()
    {
        JsonObject source = JsonNode.Parse(BrowserFixture.Definition.CanonicalBytes.Span)!.AsObject();
        source["fields"] = JsonNode.Parse("""
        [{"id":"created","type":"string","title":"Create password","required":true,"sensitivity":"secret","disclosure":"essential",
          "credential":{"kind":"password","acquisition":"create"}},
         {"id":"existing","type":"string","title":"Existing password","required":true,"sensitivity":"secret","disclosure":"essential",
          "credential":{"kind":"password","acquisition":"existing"}},
         {"id":"issuer","type":"string","title":"Issuer token","sensitivity":"secret","disclosure":"advanced",
          "credential":{"kind":"opaque-token","acquisition":"issuer"}}]
        """);
        source.Remove("presentation");
        source["artifacts"]![0]!["projection"] = JsonNode.Parse("""{"op":"literal","value":{}}""");
        return SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString()));
    }
}
