namespace Configurator.Browser.Tests;

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bunit;
using Configurator.Browser.Services;
using Configurator.Core.Definitions;
using Configurator.Presentation;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

public sealed class BrowserConfigurationWorkflowTests
{
    [Test]
    public async Task LateClearConfirmationCannotDiscardReplacementWorkspace()
    {
        await using var context = BrowserFixture.Context();
        var prompted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var confirmation = context.JSInterop.Setup<bool>("confirm", _ =>
        {
            prompted.TrySetResult();
            return true;
        });
        var component = context.Render<ConfigurationEditor>(parameters =>
            parameters.Add(item => item.Definition, BrowserFixture.Definition));
        await BrowserFixture.NotifyChoiceAsync(component, "mode", "single");
        await component.Find("[data-field='hostname'] input").InputAsync(new ChangeEventArgs { Value = "original.test" });
        await BrowserFixture.SelectPanelAsync(component, "prepare");
        Task clearing = component.Find("#clear-workspace").ClickAsync(new MouseEventArgs());
        DefinitionFormModel? replacement = null;
        long revision = -1;
        try
        {
            await prompted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            component.Render(parameters => parameters.Add(item => item.Definition, BrowserFixture.Revision(2)));
            await BrowserFixture.NotifyChoiceAsync(component, "mode", "single");
            await component.Find("[data-field='hostname'] input").InputAsync(new ChangeEventArgs { Value = "replacement.test" });
            replacement = component.FindComponents<ConfigurationField>()[0].Instance.Form;
            revision = replacement.InputRevision;
        }
        finally
        {
            confirmation.SetResult(true);
            await clearing.WaitAsync(TimeSpan.FromSeconds(5));
        }

        await Assert.That(replacement!.InputRevision).IsEqualTo(revision);
        await Assert.That(replacement.Snapshot().Values["hostname"].GetString()).IsEqualTo("replacement.test");
        await Assert.That(component.Find(".configuration-editor__controls").HasAttribute("disabled")).IsFalse();
    }

    [Test]
    public async Task InactiveInvalidDraftStillRequiresLossConfirmation()
    {
        JsonNode source = JsonNode.Parse(BrowserFixture.Definition.CanonicalBytes.Span)!;
        source["configuration"] = JsonNode.Parse("""
        {"type":"object","additionalProperties":false,"x-configurator":{"sensitivity":"public"},"properties":{
          "enabled":{"type":"boolean","x-configurator":{"initialValue":false,"sensitivity":"public"}},
          "attempts":{"type":"integer","x-configurator":{"sensitivity":"public",
            "applicableWhen":{"op":"equals","reference":{"scope":"root","path":"/enabled"},"value":true}}}}}
        """);
        source.AsObject().Remove("presentation");
        source["artifacts"]![0]!["projection"] = JsonNode.Parse("""{"op":"literal","value":{}}""");
        source["requiredCapabilities"] = new JsonArray("predicate.root", "projection.literal", "schema.boolean", "schema.integer", "schema.object", "values.initial");
        Definition definition = DefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString()));
        await using var context = BrowserFixture.Context();
        var confirmation = context.JSInterop.Setup<bool>("confirm", _ => true);
        confirmation.SetResult(false);
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, definition));
        await BrowserFixture.NotifyChoiceAsync(component, "enabled", "true");
        await component.Find("[data-field='attempts'] input").InputAsync(new ChangeEventArgs { Value = "unfinished-number" });
        var form = component.FindComponents<ConfigurationField>()[0].Instance.Form;
        await component.InvokeAsync(() => form.ResetValue(["enabled"]));
        await Assert.That(form.Snapshot().Values.Count).IsEqualTo(0);
        await Assert.That(form.HasInputErrors).IsFalse();
        await Assert.That(form.ReadDraftText(["attempts"])).IsEqualTo("unfinished-number");
        await BrowserFixture.SelectPanelAsync(component, "prepare");

        await component.Find("#clear-workspace").ClickAsync(new MouseEventArgs());

        await Assert.That(confirmation.Invocations.Count).IsEqualTo(1);
        await Assert.That(form.ReadDraftText(["attempts"])).IsEqualTo("unfinished-number");
    }

    [Test]
    public async Task CleanWorkspaceNavigationDoesNotAskForLossConfirmation()
    {
        await using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters =>
            parameters.Add(item => item.Definition, BrowserFixture.Definition));
        var navigation = context.Services.GetRequiredService<BrowserNavigationState>();
        int confirmations = 0;
        bool left = false;
        await Assert.That(navigation.IsActive).IsTrue();

        await component.InvokeAsync(async () => left = await navigation.TryLeaveAsync(() =>
        {
            confirmations++;
            return Task.FromResult(false);
        }));

        await Assert.That(left).IsTrue();
        await Assert.That(confirmations).IsEqualTo(0);
        await Assert.That(navigation.IsActive).IsFalse();
    }

    [Test]
    public async Task ReviewDistinguishesOrdinaryBlockersFromSecretDeferredReadiness()
    {
        await using var context = BrowserFixture.Context();
        JsonNode source = JsonNode.Parse(BrowserFixture.Definition.CanonicalBytes.Span)!;
        source["configuration"]!["properties"]!["optionalSetting"] = JsonNode.Parse(
            """{"type":"string","x-configurator":{"sensitivity":"public"}}""");
        Definition definition = DefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString()));
        var component = context.Render<ConfigurationEditor>(parameters =>
            parameters.Add(item => item.Definition, definition));
        await BrowserFixture.NotifyChoiceAsync(component, "mode", "single");
        var form = component.FindComponents<ConfigurationField>()[0].Instance.Form;
        long revision = form.InputRevision;

        await BrowserFixture.SelectPanelAsync(component, "review");

        await Assert.That(component.Find("[data-review-readiness]").GetAttribute("data-review-readiness"))
            .IsEqualTo("blocked");
        await Assert.That(component.Find("[data-review-optional-missing]").GetAttribute("data-review-optional-missing"))
            .IsEqualTo("1");
        await Assert.That(component.FindAll("[data-action='prepare-reviewed']")).IsEmpty();
        await Assert.That(component.FindAll("[data-action='prepare-reviewed-draft']")).IsEmpty();
        await Assert.That(component.FindAll("[data-correction='hostname']").Count).IsGreaterThan(0);
        await Assert.That(form.InputRevision).IsEqualTo(revision);

        await component.Find("[data-correction='hostname']").ClickAsync(new MouseEventArgs());
        await Assert.That(component.FindAll("[data-action='return-to-review']").Count).IsEqualTo(1);
        await component.Find("[data-field='hostname'] input").InputAsync(new ChangeEventArgs { Value = "retained.test" });
        revision = form.InputRevision;
        await component.Find("[data-action='return-to-review']").ClickAsync(new MouseEventArgs());

        await Assert.That(component.Find("[data-review-readiness]").GetAttribute("data-review-readiness"))
            .IsEqualTo("deferred");
        await Assert.That(component.FindAll("[data-action='prepare-reviewed']")).IsEmpty();
        await Assert.That(component.Find("[data-action='prepare-reviewed-draft']").HasAttribute("disabled")).IsFalse();
        await Assert.That(form.SessionPrivacy).IsEqualTo(DefinitionSessionPrivacy.BrowserNoSecrets);
        await Assert.That(form.InputRevision).IsEqualTo(revision);

        await component.Find("[data-action='prepare-reviewed-draft']").ClickAsync(new MouseEventArgs());

        await Assert.That(component.FindAll("[data-readiness='draft']").Count).IsEqualTo(1);
        await Assert.That(component.FindAll("[data-completion-required='true']").Count).IsEqualTo(1);
        await Assert.That(component.FindAll("[data-readiness='complete']")).IsEmpty();
        await Assert.That(form.InputRevision).IsEqualTo(revision);
    }

    [Test]
    public async Task ClearConfirmsOnlyWhenItWouldDiscardWorkAndCancellationPreservesIt()
    {
        await using var context = BrowserFixture.Context();
        var confirmation = context.JSInterop.Setup<bool>("confirm", _ => true);
        confirmation.SetResult(false);
        var component = context.Render<ConfigurationEditor>(parameters =>
            parameters.Add(item => item.Definition, BrowserFixture.Definition));
        await BrowserFixture.SelectPanelAsync(component, "prepare");

        await component.Find("#clear-workspace").ClickAsync(new MouseEventArgs());

        await Assert.That(confirmation.Invocations.Count).IsEqualTo(0);
        await component.Find(".configuration-editor > button").ClickAsync(new MouseEventArgs());
        await BrowserFixture.NotifyChoiceAsync(component, "mode", "single");
        await component.Find("[data-field='hostname'] input").InputAsync(new ChangeEventArgs { Value = "retained.test" });
        var form = component.FindComponents<ConfigurationField>()[0].Instance.Form;
        long revision = form.InputRevision;
        await BrowserFixture.SelectPanelAsync(component, "prepare");

        await component.Find("#clear-workspace").ClickAsync(new MouseEventArgs());

        await Assert.That(confirmation.Invocations.Count).IsEqualTo(1);
        await Assert.That(form.InputRevision).IsEqualTo(revision);
        await Assert.That(form.Snapshot().Values["hostname"].GetString()).IsEqualTo("retained.test");
        await Assert.That(component.Find(".configuration-editor__controls").HasAttribute("disabled")).IsFalse();

        confirmation.SetResult(true);
        await component.Find("#clear-workspace").ClickAsync(new MouseEventArgs());

        await Assert.That(confirmation.Invocations.Count).IsEqualTo(2);
        await Assert.That(component.Find(".configuration-editor__controls").HasAttribute("disabled")).IsTrue();
        await Assert.That(component.FindAll(".configuration-editor__artifact")).IsEmpty();
    }

    [Test]
    public async Task SupersededCorrectionCannotFocusRetiredPanel()
    {
        await using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters =>
            parameters.Add(item => item.Definition, BrowserFixture.Definition));
        await BrowserFixture.NotifyChoiceAsync(component, "mode", "single");
        await component.Find("[data-field='hostname'] input").InputAsync(new ChangeEventArgs { Value = "original.test" });
        var original = component.FindComponents<ConfigurationField>()[0].Instance.Form;
        DefinitionDiagnostic correction = original.Validate().Diagnostics.Single(item => item.FieldId == "credential");
        await component.Find("[data-workflow-step='review']").ClickAsync(new MouseEventArgs());
        EventCallback<DefinitionDiagnostic> retiredCorrection = component.FindComponent<ConfigurationReview>().Instance.CorrectField;

        component.Render(parameters => parameters.Add(item => item.Definition, BrowserFixture.Revision(2)));
        await BrowserFixture.NotifyChoiceAsync(component, "mode", "single");
        await component.Find("[data-field='hostname'] input").InputAsync(new ChangeEventArgs { Value = "replacement.test" });
        var replacement = component.FindComponents<ConfigurationField>()[0].Instance.Form;
        await Assert.That(replacement.InputRevision).IsEqualTo(correction.Location!.InputRevision);
        await component.Find("[data-workflow-step='review']").ClickAsync(new MouseEventArgs());
        int focusCalls = context.JSInterop.Invocations.Count(call =>
            call.Identifier.Contains("focus", StringComparison.OrdinalIgnoreCase));

        await component.InvokeAsync(() => retiredCorrection.InvokeAsync(correction));

        await Assert.That(component.Find("[data-workflow-step='review']").GetAttribute("aria-current")).IsEqualTo("step");
        await Assert.That(context.JSInterop.Invocations.Count(call =>
            call.Identifier.Contains("focus", StringComparison.OrdinalIgnoreCase))).IsEqualTo(focusCalls);
        await Assert.That(replacement.Snapshot().Values["hostname"].GetString()).IsEqualTo("replacement.test");
        await Assert.That(replacement.SessionPrivacy).IsEqualTo(DefinitionSessionPrivacy.BrowserNoSecrets);
    }

    [Test]
    public async Task PanelRoundTripPreservesAcceptedAndDraftState()
    {
        JsonNode source = JsonNode.Parse(BrowserFixture.Definition.CanonicalBytes.Span)!;
        BrowserFixture.WithField(source, "attempts",
            """{"type":"integer","x-configurator":{"sensitivity":"public","detail":"basic"}}""",
            "schema.integer");
        source["configuration"]!["required"]!.AsArray().Add("attempts");
        Definition definition = DefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString()));
        await using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, definition));
        await BrowserFixture.NotifyChoiceAsync(component, "mode", "single");
        await component.Find("[data-field='hostname'] input").InputAsync(new ChangeEventArgs { Value = "retained.test" });
        await component.Find("[data-field='attempts'] input").InputAsync(new ChangeEventArgs { Value = "7" });
        await component.Find("[data-field='attempts'] input").InputAsync(new ChangeEventArgs { Value = "unfinished-number" });
        await component.Find("#secret-consent").ChangeAsync(new ChangeEventArgs { Value = true });
        await component.Find("[data-field='credential'] input").InputAsync(new ChangeEventArgs { Value = "synthetic-roundtrip-secret" });
        var form = component.FindComponents<ConfigurationField>()[0].Instance.Form;
        long revision = form.InputRevision;
        string identity = form.GetEditorIdentity(["attempts"]);

        await component.Find("[data-workflow-step='review']").ClickAsync(new MouseEventArgs());
        await Assert.That(component.FindAll("[data-section-panel]")).IsEmpty();
        await Assert.That(component.FindAll("[data-field]")).IsEmpty();
        await Assert.That(component.FindAll("#configuration-review-stage-heading").Count).IsEqualTo(1);
        await Assert.That(form.ReadDraftText(["attempts"])).IsEqualTo("unfinished-number");
        await component.Find("[data-workflow-step='prepare']").ClickAsync(new MouseEventArgs());
        await Assert.That(component.FindAll("#configuration-review-stage-heading")).IsEmpty();
        await Assert.That(component.FindAll("[data-field]")).IsEmpty();
        await Assert.That(component.FindAll("#prepare-complete").Count).IsEqualTo(1);
        await component.Find("[data-workflow-step='configure']").ClickAsync(new MouseEventArgs());
        await Assert.That(component.Find("[data-field='attempts'] input").GetAttribute("value")).IsEqualTo("unfinished-number");
        await Assert.That(form.Snapshot().Values["attempts"].GetInt32()).IsEqualTo(7);
        await Assert.That(form.Snapshot().Values["hostname"].GetString()).IsEqualTo("retained.test");
        await Assert.That(form.Snapshot().Values["credential"].GetString()).IsEqualTo("synthetic-roundtrip-secret");
        await Assert.That(form.InputRevision).IsEqualTo(revision);
        await Assert.That(form.GetEditorIdentity(["attempts"])).IsEqualTo(identity);
        await Assert.That(form.Definition.Digest).IsEqualTo(definition.Digest);
        await Assert.That(component.FindComponents<ConfigurationField>()[0].Instance.Form).IsSameReferenceAs(form);
    }

    [Test]
    public async Task DraftOrdinaryMissingValueHasConnectedErrorWhileSecretDeferralRemainsValid()
    {
        await using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, BrowserFixture.Definition));
        await BrowserFixture.NotifyChoiceAsync(component, "mode", "single");
        await BrowserFixture.PrepareAsync(component, draft: true);
        await BrowserFixture.SelectPanelAsync(component, "configure");
        var hostname = component.Find("[data-field='hostname'] input");
        string errorId = hostname.Id + "-error";
        await Assert.That(hostname.GetAttribute("aria-invalid")).IsEqualTo("true");
        await Assert.That(hostname.GetAttribute("aria-describedby")).Contains(errorId);
        await Assert.That(component.Find($"[id='{errorId}']").TextContent).Contains("value-required");
        await Assert.That(component.FindAll("[data-field='credential'] input")).IsEmpty();
        component.Find("[data-field='hostname'] input").Input("example.test");
        await BrowserFixture.PrepareAsync(component, draft: true);
        await Assert.That(component.FindAll(".configuration-editor__artifact")).Count().IsEqualTo(1);
    }

    [Test]
    public async Task DraftAndCompleteUseSharedProjectionAndOnlyExplicitCopyExposesBytes()
    {
        await using var context = BrowserFixture.Context();
        var module = context.JSInterop.SetupModule("./js/browser-artifacts.js");
        module.SetupVoid("open", _ => true).SetVoidResult();
        module.Setup<bool>("copy", _ => true).SetResult(true);
        module.SetupVoid("invalidate", _ => true).SetVoidResult();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, BrowserFixture.Definition));
        await BrowserFixture.NotifyChoiceAsync(component, "mode", "single");
        component.Find("[data-field='hostname'] input").Input("example.test");
        await BrowserFixture.PrepareAsync(component, draft: false);
        await Assert.That(component.FindAll(".configuration-editor__artifact")).IsEmpty();
        await BrowserFixture.PrepareAsync(component, draft: true);
        await Assert.That(component.FindAll(".configuration-editor__artifact")).Count().IsEqualTo(1);
        await Assert.That(component.Markup).Contains("Draft");
        await Assert.That(module.Invocations.Count).IsEqualTo(0);
        component.Find(".configuration-editor__artifact button").Click();
        byte[] draft = (byte[])module.Invocations.Single(call => call.Identifier == "open").Arguments[2]!;
        using JsonDocument draftDocument = JsonDocument.Parse(draft);
        await Assert.That(draftDocument.RootElement.GetProperty("hostname").GetString()).IsEqualTo("example.test");
        await Assert.That(draftDocument.RootElement.GetProperty("credential").GetString()).IsEqualTo("");
        await BrowserFixture.SelectPanelAsync(component, "configure");
        component.Find("#secret-consent").Change(true);
        component.Find("[data-field='credential'] input").Input("synthetic-secret-canary");
        await BrowserFixture.PrepareAsync(component, draft: false);
        await Assert.That(component.FindAll(".configuration-editor__artifact")).Count().IsEqualTo(1);
        await Assert.That(component.Find("pre").TextContent).IsEqualTo("");
        component.Find(".configuration-editor__artifact button").Click();
        byte[] complete = (byte[])module.Invocations.Last(call => call.Identifier == "open").Arguments[2]!;
        using JsonDocument completeDocument = JsonDocument.Parse(complete);
        await Assert.That(completeDocument.RootElement.GetProperty("credential").GetString()).IsEqualTo("synthetic-secret-canary");
        await BrowserFixture.SelectPanelAsync(component, "configure");
        component.Find("[data-field='hostname'] input").Input("changed.test");
        await BrowserFixture.SelectPanelAsync(component, "prepare");
        await Assert.That(component.FindAll(".configuration-editor__artifact")).IsEmpty();
        await Assert.That(module.Invocations.Any(call => call.Identifier == "invalidate")).IsTrue();
    }

    [Test]
    public async Task ClipboardFailureProvidesValueSafeDownloadAlternative()
    {
        await using var context = BrowserFixture.Context();
        var module = context.JSInterop.SetupModule("./js/browser-artifacts.js");
        module.Setup<bool>("copy", _ => true).SetResult(false);
        module.Setup<bool>("download", _ => true).SetResult(true);
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, BrowserFixture.Definition));
        await BrowserFixture.NotifyChoiceAsync(component, "mode", "single");
        component.Find("[data-field='hostname'] input").Input("example.test");
        await BrowserFixture.PrepareAsync(component, draft: true);
        await component.Find(".configuration-editor__artifact button").ClickAsync(new MouseEventArgs());
        await Assert.That(component.Find(".configuration-editor__artifact [data-file-status]").GetAttribute("data-file-status"))
            .IsEqualTo("clipboard-unavailable");
        await Assert.That(component.Find(".configuration-editor__artifact [data-action='download-file']").HasAttribute("disabled"))
            .IsFalse();
        await Assert.That(component.Markup).DoesNotContain("example.test");

        await component.Find(".configuration-editor__artifact [data-action='download-file']").ClickAsync(new MouseEventArgs());

        await Assert.That(module.Invocations.Count(call => call.Identifier == "download")).IsEqualTo(1);
        await Assert.That(component.Find(".configuration-editor__artifact [data-file-status]").GetAttribute("data-file-status"))
            .IsEqualTo("download-requested");
    }
}
