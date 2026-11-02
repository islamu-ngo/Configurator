namespace Configurator.Browser.Tests;

using System.Text;
using System.Text.Json.Nodes;
using Bunit;
using Configurator.Core.Definitions;
using Configurator.Presentation;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

public sealed class BrowserSecretGenerationTests
{
    [Test]
    public async Task PrivatePemEditorPreservesMultilineDraftExactly()
    {
        JsonNode source = JsonNode.Parse(BrowserFixture.Definition.CanonicalBytes.Span)!;
        BrowserFixture.WithField(source, "credential", """
            {"type":"string","title":"Private key","x-configurator":{"secret":{
              "kind":"private-key","acquisition":"existing","algorithm":"ES256","encoding":"pkcs8-pem"}}}
            """, "schema.secret");
        Definition definition = DefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString()));
        await using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, definition));
        await component.Find("#secret-consent").ChangeAsync(new ChangeEventArgs { Value = true });
        await Assert.That(component.Find("[data-field='credential'] input").HasAttribute("readonly")).IsTrue();
        await component.Find("[data-secret-action='reveal']").ClickAsync(new());
        const string draft = "-----BEGIN PRIVATE KEY-----\nsynthetic-invalid-draft\n-----END PRIVATE KEY-----\n";
        await component.Find("[data-field='credential'] textarea").InputAsync(new ChangeEventArgs { Value = draft });
        var form = component.FindComponents<ConfigurationField>()[0].Instance.Form;
        await Assert.That(form.Snapshot().Values["credential"].GetString()).IsEqualTo(draft);
        await Assert.That(component.FindAll("[data-field='credential'] textarea").Count).IsEqualTo(1);
        await component.Find("[data-secret-action='reveal']").ClickAsync(new());
        await Assert.That(component.FindAll("[data-field='credential'] textarea")).IsEmpty();
        await Assert.That(form.Snapshot().Values["credential"].GetString()).IsEqualTo(draft);
    }

    [Test]
    public async Task RevocationFencesPendingFieldCopyAndZeroesOwnedBytes()
    {
        await using var context = BrowserFixture.Context();
        var javascript = new BrowserExportLifecycleTests.GatedJavascript(context.JSInterop.JSRuntime);
        await using var module = new BrowserExportLifecycleTests.RecordingModule { HoldOpening = true };
        javascript.Release.TrySetResult(module);
        context.Services.AddSingleton<IJSRuntime>(javascript);
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, BrowserFixture.Definition));
        await component.Find("#secret-consent").ChangeAsync(new ChangeEventArgs { Value = true });
        await component.Find("[data-field='credential'] input").InputAsync(new ChangeEventArgs { Value = "synthetic-held-copy" });
        var form = component.FindComponents<ConfigurationField>()[0].Instance.Form;
        var revoked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void Observe(object? sender, EventArgs args)
        {
            if (form.SessionPrivacy == DefinitionSessionPrivacy.BrowserNoSecrets) revoked.TrySetResult();
        }
        form.InputChanged += Observe;
        Task copying = component.Find("[data-secret-action='copy']").ClickAsync(new());
        Task revoking = Task.CompletedTask;
        try
        {
            await module.OpeningStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await Assert.That(component.Find("[data-secret-action='copy']").HasAttribute("disabled")).IsTrue();
            revoking = component.Find("#secret-consent").ChangeAsync(new ChangeEventArgs { Value = false });
            await revoked.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            form.InputChanged -= Observe;
            module.OpeningRelease.TrySetResult();
            await Task.WhenAll(copying, revoking).WaitAsync(TimeSpan.FromSeconds(5));
        }
        await Assert.That(module.ExposureCount).IsEqualTo(0);
        await Assert.That(module.OpeningBytes!.All(item => item == 0)).IsTrue();
        await Assert.That(component.Markup).DoesNotContain("synthetic-held-copy");
    }

    [Test]
    public async Task ArabicKeyGenerationUsesNamedNativeActionsAndConnectedRules()
    {
        JsonNode source = JsonNode.Parse(BrowserFixture.Definition.CanonicalBytes.Span)!;
        source["locales"] = new JsonArray("ar", "en");
        source["localization"] = JsonNode.Parse("""
            {"defaultLocale":"en","translations":{"ar":{"direction":"rtl","overrides":{}}}}
            """);
        BrowserFixture.WithField(source, "credential", """
            {"type":"string","title":"Encryption key","x-configurator":{"secret":{
              "kind":"symmetric-key","acquisition":"local","algorithm":"AES256","encoding":"hex","generation":{"bytes":32}}}}
            """, "schema.secret");
        Definition definition = DefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString()));
        await using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters => parameters
            .Add(item => item.Definition, definition).Add(item => item.InitialLocale, "ar"));
        await component.Find("#secret-consent").ChangeAsync(new ChangeEventArgs { Value = true });
        await Assert.That(component.FindAll("[dir='rtl']").Count).IsGreaterThan(0);
        var generate = component.Find("[data-secret-action='generate']");
        await Assert.That(generate.TagName).IsEqualTo("BUTTON");
        await Assert.That(generate.GetAttribute("type")).IsEqualTo("button");
        await Assert.That(generate.GetAttribute("aria-label")).Contains(generate.TextContent.Trim());
        await generate.ClickAsync(new());
        var input = component.Find("[data-field='credential'] input");
        await Assert.That(input.GetAttribute("type")).IsEqualTo("password");
        await Assert.That(input.GetAttribute("value")!.Length).IsEqualTo(64);
        foreach (string id in input.GetAttribute("aria-describedby")!.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            await Assert.That(component.FindAll("[id='" + id + "']").Count).IsEqualTo(1);
        var reveal = component.Find("[data-secret-action='reveal']");
        await Assert.That(reveal.GetAttribute("aria-controls")).IsEqualTo(input.Id);
        await reveal.ClickAsync(new());
        await Assert.That(component.Find("[data-secret-action='reveal']").GetAttribute("aria-pressed")).IsEqualTo("true");
    }

    [Test]
    public async Task GeneratedOriginalIsSeparateMaskedAndDismissible()
    {
        JsonNode source = JsonNode.Parse(BrowserFixture.Definition.CanonicalBytes.Span)!;
        BrowserFixture.WithField(source, "credential", """
            {"type":"string","title":"Digest","x-configurator":{"secret":{
              "kind":"digest","acquisition":"local","algorithm":"SHA256","encoding":"hex",
              "generation":{"input":"generated","password":{"kind":"password","acquisition":"local","generation":{"length":32}}}}}}
            """, "schema.secret");
        source["configuration"]!["properties"]!["otherHash"] = source["configuration"]!["properties"]!["credential"]!.DeepClone();
        source["configuration"]!["required"]!.AsArray().Add("otherHash");
        Definition definition = DefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString()));
        await using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, definition));
        await component.Find("#secret-consent").ChangeAsync(new ChangeEventArgs { Value = true });
        await component.Find("[data-field='credential'] [data-secret-action='generate']").ClickAsync(new());
        await Assert.That(component.FindAll("[data-field='otherHash'] [data-secret-action='generate']").Count).IsEqualTo(1);
        var original = component.Find("input[id$='-original']");
        await Assert.That(original.GetAttribute("type")).IsEqualTo("password");
        await Assert.That(component.FindAll("[data-field='otherHash'] input[id$='-original']")).IsEmpty();
        await component.Find("[data-secret-action='reveal-original']").ClickAsync(new());
        original = component.Find("input[id$='-original']");
        await Assert.That(original.GetAttribute("type")).IsEqualTo("text");
        string plaintext = original.GetAttribute("value")!;
        await Assert.That(plaintext.Length).IsEqualTo(32);
        var form = component.FindComponents<ConfigurationField>()[0].Instance.Form;
        await Assert.That(form.Snapshot().Values["credential"].GetString()).IsNotEqualTo(plaintext);
        await component.Find("[data-secret-action='dismiss-original']").ClickAsync(new());
        await Assert.That(component.FindAll("input[id$='-original']")).IsEmpty();
        await Assert.That(form.CopyGeneratedPassword()).IsNull();
        await Assert.That(component.Markup).DoesNotContain(plaintext);
        await component.Find("[data-field='credential'] [data-secret-action='generate']").ClickAsync(new());
        await Assert.That(form.SecretOperation.HasGeneratedPassword).IsTrue();
        await BrowserFixture.SelectPanelAsync(component, "review");
        await Assert.That(form.CopyGeneratedPassword()).IsNull();
    }

    [Test]
    public async Task InvalidSecretStaysEditableAndHasConnectedRuleFeedback()
    {
        JsonNode source = JsonNode.Parse(BrowserFixture.Definition.CanonicalBytes.Span)!;
        BrowserFixture.WithField(source, "credential", """
            {"type":"string","title":"Password","minLength":16,
             "x-configurator":{"secret":{"kind":"password","acquisition":"existing","constraints":{"allowWhitespace":false}}}}
            """, "schema.bounds", "schema.secret");
        Definition definition = DefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString()));
        await using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, definition));
        await component.Find("#secret-consent").ChangeAsync(new ChangeEventArgs { Value = true });
        await component.Find("[data-field='credential'] input").InputAsync(new ChangeEventArgs { Value = "bad value" });
        var field = component.Find("[data-field='credential'] input");
        await Assert.That(field.GetAttribute("value")).IsEqualTo("bad value");
        await Assert.That(field.GetAttribute("aria-invalid")).IsEqualTo("true");
        await Assert.That(component.Find("[data-field='credential'] [data-secret-rule='secret-whitespace-forbidden']").TextContent).DoesNotContain("bad value");
    }

    [Test]
    public async Task GenerationRequiresConsentAndNeverRevealsOrCopiesAutomatically()
    {
        JsonNode source = JsonNode.Parse(BrowserFixture.Definition.CanonicalBytes.Span)!;
        BrowserFixture.WithField(source, "credential", """
            {"type":"string","title":"Password","minLength":16,"maxLength":64,
             "x-configurator":{"secret":{"kind":"password","acquisition":"local","generation":{"length":32}}}}
            """, "schema.bounds", "schema.secret");
        Definition definition = DefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString()));
        await using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, definition));
        await Assert.That(component.FindAll("[data-secret-action='generate']")).IsEmpty();
        await component.Find("#secret-consent").ChangeAsync(new ChangeEventArgs { Value = true });
        await component.Find("[data-secret-action='generate']").ClickAsync(new());
        var field = component.Find("[data-field='credential'] input");
        await Assert.That(field.GetAttribute("type")).IsEqualTo("password");
        await Assert.That(field.GetAttribute("value")!.Length).IsEqualTo(32);
        await Assert.That(context.JSInterop.Invocations.Any(item => item.Identifier == "copy")).IsFalse();
        await component.Find("#secret-consent").ChangeAsync(new ChangeEventArgs { Value = false });
        await Assert.That(component.FindAll("[data-secret-action]")).IsEmpty();
    }

    [Test]
    public async Task RevealIsDeliberateAndRevocationRemovesSecretControls()
    {
        await using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, BrowserFixture.Definition));
        await Assert.That(component.FindAll("[data-secret-action]")).IsEmpty();
        await component.Find("#secret-consent").ChangeAsync(new ChangeEventArgs { Value = true });
        await component.Find("[data-field='credential'] input").InputAsync(new ChangeEventArgs { Value = "synthetic-reveal-value" });
        await Assert.That(component.Find("[data-field='credential'] input").GetAttribute("type")).IsEqualTo("password");
        component.Find("[data-field='credential'] [data-secret-action='reveal']").Click();
        await Assert.That(component.Find("[data-field='credential'] input").GetAttribute("type")).IsEqualTo("text");
        await component.Find("[data-field='credential'] input").InputAsync(new ChangeEventArgs { Value = "synthetic-edited-value" });
        await Assert.That(component.Find("[data-field='credential'] input").GetAttribute("type")).IsEqualTo("password");
        await component.Find("#secret-consent").ChangeAsync(new ChangeEventArgs { Value = false });
        await Assert.That(component.FindAll("[data-secret-action]")).IsEmpty();
        await Assert.That(component.Markup).DoesNotContain("synthetic-reveal-value");
        await component.Find("#secret-consent").ChangeAsync(new ChangeEventArgs { Value = true });
        await Assert.That(component.Find("[data-field='credential'] input").GetAttribute("type")).IsEqualTo("password");
    }

    [Test]
    public async Task ClipboardFailurePreservesMaskedValue()
    {
        await using var context = BrowserFixture.Context();
        var bridge = context.JSInterop.SetupModule("./js/browser-artifacts.js");
        bridge.Setup<bool>("copy", _ => true).SetResult(false);
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, BrowserFixture.Definition));
        await component.Find("#secret-consent").ChangeAsync(new ChangeEventArgs { Value = true });
        await component.Find("[data-field='credential'] input").InputAsync(new ChangeEventArgs { Value = "synthetic-copy-value" });
        component.Find("[data-field='credential'] [data-secret-action='copy']").Click();
        await Assert.That(component.Find("[data-field='credential'] input").GetAttribute("type")).IsEqualTo("password");
        await Assert.That(component.Find("[data-field='credential'] input").GetAttribute("value")).IsEqualTo("synthetic-copy-value");
        await Assert.That(component.Find("[data-field='credential'] [role='status']").GetAttribute("data-secret-status")).IsEqualTo("clipboard-unavailable");
    }
}
