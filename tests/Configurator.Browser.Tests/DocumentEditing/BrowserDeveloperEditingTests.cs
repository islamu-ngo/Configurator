namespace Configurator.Browser.Tests;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AngleSharp.Html.Dom;
using Bunit;
using Configurator.Browser.Services;
using Configurator.Core.Definitions;
using Configurator.Presentation;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

public sealed class BrowserDeveloperEditingTests
{
    [Test]
    public async Task EventProviderPreviewRejectsUnknownChoiceAndAppliesCorrectedWireValue()
    {
        var definition = BrowserDefinitionRegistry.LoadBundled().Current.Resolve(Guid.Parse("ea55cccf-dce5-4f88-8ea1-6dd211714edc"));
        await using var context = BrowserFixture.Context();
        using var form = new DefinitionFormModel(definition, DefinitionSessionPrivacy.BrowserSecretsEnabled);
        await using var engine = Boundary(context);
        var component = Render(context, form);
        await engine.Mounted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        const string source = """
            {"answerContractVersion":"0.0.1","values":{"operation":"environment",
             "select.feature.platform-config":true,"env.AUTHENTICATION_PROVIDER":"unsupported-synthetic-provider"},"omitted":[]}
            """;
        long revision = form.InputRevision;
        await component.InvokeAsync(() => engine.ChangeAsync(source));
        await Click(component, "developer-preview");
        await Assert.That(component.FindAll("[data-developer-diagnostic]").Count > 0).IsTrue();
        await Assert.That(component.FindAll("[data-action='developer-apply']:not([disabled])")).IsEmpty();
        await Assert.That(form.InputRevision).IsEqualTo(revision);
        await Assert.That(component.Markup).DoesNotContain("unsupported-synthetic-provider");

        await component.InvokeAsync(() => engine.ChangeAsync(source.Replace("unsupported-synthetic-provider", "local", StringComparison.Ordinal)));
        await Click(component, "developer-preview");
        await Click(component, "developer-apply");
        await Assert.That(form.Snapshot().Values["env.AUTHENTICATION_PROVIDER"].GetString()).IsEqualTo("local");
        await Assert.That(form.InputRevision).IsEqualTo(revision + 1);
    }

    private const string Pending = """{ "answerContractVersion":"0.0.1", "values":{"mode":"single","hostname":"pending.test"}, "omitted":[] }""";

    [Test]
    public async Task StayingWithPendingTextRestoresTheVisibleRepresentationChoice()
    {
        await using var context = BrowserFixture.Context();
        using var form = new DefinitionFormModel(EnvironmentDefinition(), DefinitionSessionPrivacy.BrowserSecretsEnabled);
        await using var engine = Boundary(context);
        var component = Render(context, form, "environment");
        await engine.Mounted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await component.InvokeAsync(() => engine.ChangeAsync(Pending));
        Task? transition = null;
        await component.InvokeAsync(() =>
        {
            var choice = (IHtmlSelectElement)component.Find("[data-developer-representation]");
            // Native selection changes before Blazor handles its change event.
            choice.Value = "environment";
            transition = choice.ChangeAsync(new ChangeEventArgs { Value = "environment" });
        });
        await Assert.That(transition!.IsCompleted).IsFalse();
        await Click(component, "developer-resolve-stay");
        await transition.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.That(((IHtmlSelectElement)component.Find("[data-developer-representation]")).Value).IsEqualTo("canonical");
        await Assert.That(component.FindAll("[data-developer-mode]")).IsEmpty();
        await Assert.That(engine.CurrentText).IsEqualTo(Pending);
        await Assert.That(component.Instance.IsDirty).IsTrue();
        await Assert.That(engine.MountCount).IsEqualTo(1);
        await Assert.That(form.Snapshot().Values).IsEmpty();
    }

    [Test]
    public async Task BothDeveloperLanguagesAreAvailableWithoutSatelliteLoading()
    {
        string[] resources = typeof(ConfigurationDeveloper).Assembly.GetManifestResourceNames();
        await Assert.That(resources.Contains("Configurator.Browser.Resources.BrowserStrings.resources", StringComparer.Ordinal)).IsTrue();
        await Assert.That(resources.Contains("Configurator.Browser.Resources.BrowserStrings.ar.resources", StringComparer.Ordinal)).IsTrue();
    }

    [Test]
    [Arguments("apply")]
    [Arguments("discard")]
    [Arguments("stay")]
    public async Task WorkspaceNavigationResolvesTextBeforeConfirmingAcceptedAnswerLoss(string decision)
    {
        await using var context = BrowserFixture.Context();
        await using var engine = Boundary(context);
        var definition = BrowserFixture.Definition;
        var root = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, definition));
        await root.InvokeAsync(() => root.Find("[data-editing-mode='developer']").ClickAsync(new MouseEventArgs()));
        var panel = root.FindComponent<ConfigurationDeveloper>();
        await panel.InvokeAsync(() => panel.Instance.Form.SetSecretEntryEnabled(true));
        await engine.Mounted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await panel.InvokeAsync(() => engine.ChangeAsync(Pending));
        var navigation = context.Services.GetRequiredService<BrowserNavigationState>();
        Task<bool>? leaving = null;
        int confirmations = 0;
        await root.InvokeAsync(() =>
        {
            leaving = navigation.TryLeaveAsync(() =>
            {
                confirmations++;
                return Task.FromResult(true);
            });
        });
        await Assert.That(leaving!.IsCompleted).IsFalse();
        await Assert.That(confirmations).IsEqualTo(0);
        await Click(panel, "developer-resolve-" + decision);
        await Assert.That(await leaving.WaitAsync(TimeSpan.FromSeconds(5))).IsEqualTo(decision != "stay");
        await Assert.That(confirmations).IsEqualTo(decision == "apply" ? 1 : 0);
        await Assert.That(navigation.IsActive).IsEqualTo(decision == "stay");
        await Assert.That(engine.Active).IsEqualTo(decision == "stay");
    }

    [Test]
    [Arguments("form", "apply")]
    [Arguments("form", "discard")]
    [Arguments("form", "stay")]
    [Arguments("review", "apply")]
    [Arguments("review", "discard")]
    [Arguments("review", "stay")]
    public async Task WorkspaceTransitionsResolvePendingTextBeforeUnmounting(string destination, string decision)
    {
        await using var context = BrowserFixture.Context();
        await using var engine = Boundary(context);
        var definition = BrowserFixture.Definition;
        var root = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, definition));
        await root.InvokeAsync(() => root.Find("[data-editing-mode='developer']").ClickAsync(new MouseEventArgs()));
        var panel = root.FindComponent<ConfigurationDeveloper>();
        DefinitionFormModel form = panel.Instance.Form;
        await panel.InvokeAsync(() => form.SetSecretEntryEnabled(true));
        await engine.Mounted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await panel.InvokeAsync(() => engine.ChangeAsync(Pending));

        Task? transition = null;
        await root.InvokeAsync(() =>
        {
            transition = root.Find(destination == "form" ? "[data-editing-mode='form']" : "[data-workflow-step='review']")
                .ClickAsync(new MouseEventArgs());
        });
        await Assert.That(transition!.IsCompleted).IsFalse();
        await Assert.That(root.FindAll("[data-developer-resolution]")).Count().IsEqualTo(1);
        await Click(panel, "developer-resolve-" + decision);
        await transition.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.That(root.FindComponents<ConfigurationDeveloper>().Count).IsEqualTo(decision == "stay" ? 1 : 0);
        await Assert.That(form.Snapshot().Values.ContainsKey("hostname")).IsEqualTo(decision == "apply");
        await Assert.That(engine.Active).IsEqualTo(decision == "stay");
    }

    [Test]
    public async Task DeniedConsentKeepsCoverageWithoutAcquiringAnEditor()
    {
        await using var context = BrowserFixture.Context();
        using var form = Form(false);
        await using var engine = Boundary(context);
        var component = Render(context, form);
        await Assert.That(component.FindAll("[data-developer-coverage]")).Count().IsEqualTo(1);
        await Assert.That(component.FindAll("[data-developer-editor]")).IsEmpty();
        await Assert.That(component.Find("[data-developer-status]").GetAttribute("data-code")).IsEqualTo("developer-consent-required");
        await Assert.That(engine.Mounted.Task.IsCompleted).IsFalse();
        await Assert.That(form.Snapshot().Values).IsEmpty();
    }

    [Test]
    public async Task ConsentRequestDoesNotGrantConsentOrAcquireSource()
    {
        await using var context = BrowserFixture.Context();
        using var form = Form(false);
        await using var engine = Boundary(context);
        bool requested = false;
        var component = context.Render<ConfigurationDeveloper>(parameters => parameters
            .Add(item => item.Form, form).Add(item => item.RequestConsent, () => requested = true));
        await Click(component, "developer-request-consent");
        await Assert.That(requested).IsTrue();
        await Assert.That(form.SessionPrivacy).IsEqualTo(DefinitionSessionPrivacy.BrowserNoSecrets);
        await Assert.That(engine.Active).IsFalse();
        await Assert.That(component.Instance.HasWork).IsFalse();
    }

    [Test]
    public async Task ExplicitCancellationLeavesAFocusableRecoveryNoticeWithoutPendingSource()
    {
        await using var context = BrowserFixture.Context();
        using var form = Form();
        form.EditValue(["hostname"], "accepted.test");
        await using var engine = Boundary(context);
        var component = Render(context, form);
        await engine.Mounted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await component.InvokeAsync(() => engine.ChangeAsync(Pending));

        await Click(component, "developer-cancel");

        var recovery = component.Find("[data-developer-retired]");
        await Assert.That(recovery.GetAttribute("tabindex")).IsEqualTo("-1");
        await Assert.That(recovery.QuerySelector("[data-action='developer-reopen']") is not null).IsTrue();
        await Assert.That(engine.Active).IsFalse();
        await Assert.That(component.Instance.IsDirty).IsFalse();
        await Assert.That(component.FindAll("[data-developer-preview]")).IsEmpty();
        await Assert.That(form.Snapshot().Values["hostname"].GetString()).IsEqualTo("accepted.test");
    }

    [Test]
    public async Task SourceMountFrameSeparatesAcquisitionFromEngineMount()
    {
        await using var context = BrowserFixture.Context();
        using var form = Form();
        form.EditValue(["hostname"], "accepted.test");
        await using var engine = Boundary(context);
        engine.FrameRelease = new();
        var component = Render(context, form);
        await engine.FrameStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try
        {
            await Assert.That(engine.Active).IsFalse();
            await Assert.That(component.Find("[data-developer-editor]").ParentElement!.GetAttribute("aria-busy")).IsEqualTo("true");
        }
        finally
        {
            await component.InvokeAsync(() => { engine.FrameRelease.TrySetResult(); });
        }
        await engine.Mounted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.That(engine.Active).IsTrue();
        await Assert.That(engine.MountSource!.All(value => value == 0)).IsTrue();
        using var mounted = JsonDocument.Parse(engine.CurrentText);
        await Assert.That(mounted.RootElement.GetProperty("values").GetProperty("hostname").GetString()).IsEqualTo("accepted.test");
        await Assert.That(component.Instance.IsDirty).IsFalse();
        await Assert.That(form.Snapshot().Values["hostname"].GetString()).IsEqualTo("accepted.test");
    }

    [Test]
    [Arguments("revoke")]
    [Arguments("edit")]
    [Arguments("replacement")]
    [Arguments("cancel")]
    public async Task SourceMountFrameRejectsChangedAuthority(string transition)
    {
        await using var context = BrowserFixture.Context();
        using var form = Form();
        using var replacement = new DefinitionFormModel(BrowserFixture.Revision(2), DefinitionSessionPrivacy.BrowserNoSecrets);
        await using var engine = Boundary(context);
        engine.FrameRelease = new();
        var component = Render(context, form);
        await engine.FrameStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try
        {
            if (transition == "revoke") await component.InvokeAsync(() => form.SetSecretEntryEnabled(false));
            else if (transition == "edit") await component.InvokeAsync(() => form.EditValue(["hostname"], "new.test"));
            else if (transition == "replacement")
                await component.InvokeAsync(() => component.Render(parameters => parameters.Add(item => item.Form, replacement)));
            else await Click(component, "developer-cancel");
        }
        finally
        {
            await component.InvokeAsync(() => { engine.FrameRelease.TrySetResult(); });
        }

        await Assert.That(engine.Active).IsFalse();
        await Assert.That(engine.MountCount).IsEqualTo(0);
        await Assert.That(component.Instance.HasWork).IsFalse();
        await Assert.That(component.FindAll("[data-developer-preview]")).IsEmpty();
        await Assert.That(replacement.Snapshot().Values).IsEmpty();
    }

    [Test]
    public async Task RetiredCallbackCannotResurrectPendingWork()
    {
        await using var context = BrowserFixture.Context();
        using var form = Form();
        await using var engine = Boundary(context);
        var component = Render(context, form);
        await engine.Mounted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        ConfigurationDeveloper callback = engine.Owner!;
        long generation = engine.Generation;
        await component.InvokeAsync(component.Instance.RetireAsync);
        await component.InvokeAsync(() => callback.OnEditorChangedAsync(generation, 2, true, false));
        await Assert.That(component.Instance.IsDirty).IsFalse();
        await Assert.That(component.Instance.HasWork).IsFalse();
        await Assert.That(form.Snapshot().Values).IsEmpty();
        await Assert.That(engine.Active).IsFalse();
    }

    [Test]
    public async Task PreviewFrameAcknowledgesBeforeSourceAcquisition()
    {
        await using var context = BrowserFixture.Context();
        using var form = Form();
        await using var engine = Boundary(context);
        var component = Render(context, form);
        await component.InvokeAsync(() => engine.ChangeAsync(Pending));
        engine.FrameStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        engine.FrameRelease = new();
        Task pending = Click(component, "developer-preview");
        try
        {
            await engine.FrameStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await component.InvokeAsync(() => { });
            await Assert.That(engine.ReadStarted.Task.IsCompleted).IsFalse();
            await Assert.That(component.Find("[data-developer-editor]").ParentElement!.GetAttribute("aria-busy")).IsEqualTo("true");
            await Assert.That(component.Find("[data-action='developer-cancel']").HasAttribute("disabled")).IsFalse();
            await Assert.That(component.FindAll("[data-developer-preview]")).IsEmpty();
            await Assert.That(form.Snapshot().Values).IsEmpty();
        }
        finally { await component.InvokeAsync(() => engine.FrameRelease.TrySetResult()); }
        await pending.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(component.FindAll("[data-developer-preview]").Count).IsEqualTo(1);
        await Assert.That(engine.LastRead!.All(value => value == 0)).IsTrue();
        await Assert.That(form.Snapshot().Values).IsEmpty();
    }

    [Test]
    public async Task PreviewPublicationServesCancellationBeforePublishing()
    {
        await using var context = BrowserFixture.Context();
        using var form = Form();
        await using var engine = Boundary(context);
        var component = Render(context, form);
        await component.InvokeAsync(() => engine.ChangeAsync(Pending));
        engine.TaskRelease = new();
        Task pending = Click(component, "developer-preview");
        try
        {
            await engine.TaskStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await Assert.That(engine.LastRead!.All(value => value == 0)).IsTrue();
            await Assert.That(component.FindAll("[data-developer-preview]")).IsEmpty();
            await Assert.That(form.Snapshot().Values).IsEmpty();
            await Click(component, "developer-cancel");
        }
        finally { await component.InvokeAsync(() => engine.TaskRelease.TrySetResult()); }
        await pending.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(component.Instance.HasWork).IsFalse();
        await Assert.That(engine.Active).IsFalse();
        await Assert.That(component.FindAll("[data-developer-preview]")).IsEmpty();
        await Assert.That(form.Snapshot().Values).IsEmpty();
    }

    [Test]
    [Arguments("cancel")]
    [Arguments("edit")]
    public async Task ApplyFrameAllowsRetirementBeforeCommit(string transition)
    {
        await using var context = BrowserFixture.Context();
        using var form = Form();
        await using var engine = Boundary(context);
        var component = Render(context, form);
        await component.InvokeAsync(() => engine.ChangeAsync(Pending));
        await Click(component, "developer-preview");
        engine.FrameStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        engine.FrameRelease = new();
        Task pending = Click(component, "developer-apply");
        try
        {
            await engine.FrameStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await Assert.That(engine.FreezeStarted.Task.IsCompleted).IsFalse();
            await Assert.That(form.Snapshot().Values).IsEmpty();
            if (transition == "cancel") await Click(component, "developer-cancel");
            else await component.InvokeAsync(() => engine.ChangeAsync(Pending.Replace("pending.test", "newer.test", StringComparison.Ordinal)));
        }
        finally { await component.InvokeAsync(() => engine.FrameRelease.TrySetResult()); }
        await pending.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(form.Snapshot().Values).IsEmpty();
        await Assert.That(component.FindAll("[data-developer-preview]")).IsEmpty();
        await Assert.That(engine.Active).IsEqualTo(transition == "edit");
        if (transition == "edit") await Assert.That(engine.CurrentText).Contains("newer.test");
    }

    [Test]
    public async Task ApplyFrameCompletionCannotClearNewerPreviewBusyState()
    {
        await using var context = BrowserFixture.Context();
        using var form = Form();
        await using var engine = Boundary(context);
        var component = Render(context, form);
        await component.InvokeAsync(() => engine.ChangeAsync(Pending));
        await Click(component, "developer-preview");
        engine.FrameStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource oldFrame = new();
        engine.FrameRelease = oldFrame;
        Task oldApply = Click(component, "developer-apply");
        Task? newerPreview = null;
        try
        {
            await engine.FrameStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await component.InvokeAsync(() => engine.ChangeAsync(Pending.Replace("pending.test", "newer.test", StringComparison.Ordinal)));
            engine.FrameRelease = null;
            engine.ReadStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
            engine.ReadRelease = new(TaskCreationOptions.RunContinuationsAsynchronously);
            newerPreview = Click(component, "developer-preview");
            await engine.ReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await component.InvokeAsync(() => oldFrame.TrySetResult());
            await oldApply.WaitAsync(TimeSpan.FromSeconds(5));
            await Assert.That(component.Find("[data-developer-editor]").ParentElement!.GetAttribute("aria-busy")).IsEqualTo("true");
            await Assert.That(form.Snapshot().Values).IsEmpty();
        }
        finally
        {
            await component.InvokeAsync(() => oldFrame.TrySetResult());
            if (engine.ReadRelease is not null) engine.ReadRelease.TrySetResult();
        }
        if (newerPreview is not null) await newerPreview.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(form.Snapshot().Values).IsEmpty();
        await Assert.That(engine.CurrentText).Contains("newer.test");
        await Assert.That(component.FindAll("[data-developer-preview]").Count).IsEqualTo(1);
    }

    [Test]
    [Arguments("revoke")]
    [Arguments("clear")]
    [Arguments("replacement")]
    [Arguments("dispose")]
    [Arguments("cancel")]
    public async Task RetirementFencesLateReadAndClearsTransferredBytes(string transition)
    {
        await using var context = BrowserFixture.Context();
        using var form = Form();
        using var replacement = new DefinitionFormModel(BrowserFixture.Revision(2), DefinitionSessionPrivacy.BrowserNoSecrets);
        await using var engine = Boundary(context);
        var component = Render(context, form);
        await component.InvokeAsync(() => engine.ChangeAsync(Pending));
        engine.ReadRelease = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task pending = Click(component, "developer-preview");
        await engine.ReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        ConfigurationDeveloper callback = engine.Owner!;
        long generation = engine.Generation;
        try
        {
            if (transition == "revoke") await component.InvokeAsync(() => form.SetSecretEntryEnabled(false));
            else if (transition == "clear") await component.InvokeAsync(() => form.ClearPrivateState());
            else if (transition == "replacement")
                await component.InvokeAsync(() => component.Render(parameters => parameters.Add(item => item.Form, replacement)));
            else if (transition == "dispose") await component.InvokeAsync(async () => await component.Instance.DisposeAsync());
            else await Click(component, "developer-cancel");
        }
        finally { engine.ReadRelease.TrySetResult(); }
        await pending.WaitAsync(TimeSpan.FromSeconds(5));
        await component.InvokeAsync(() => callback.OnEditorChangedAsync(generation, 99, true, true));
        await Assert.That(component.Instance.HasWork).IsFalse();
        await Assert.That(engine.Active).IsFalse();
        await Assert.That(engine.LastRead!.All(value => value == 0)).IsTrue();
        await Assert.That(form.Snapshot().Values).IsEmpty();
        await Assert.That(replacement.Snapshot().Values).IsEmpty();
    }

    [Test]
    public async Task ConsentRevokedDuringImportCannotMountALateEditor()
    {
        await using var context = BrowserFixture.Context();
        using var form = Form();
        await using var engine = Boundary(context);
        engine.ImportRelease = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var component = Render(context, form);
        await engine.ImportStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await component.InvokeAsync(() => form.SetSecretEntryEnabled(false));
        engine.ImportRelease.TrySetResult();
        await engine.ImportReturned.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await component.InvokeAsync(() => { });
        await Assert.That(engine.Active).IsFalse();
        await Assert.That(engine.MountCount).IsEqualTo(0);
        await Assert.That(component.FindAll("[data-developer-editor]")).IsEmpty();
        await Assert.That(form.Snapshot().Values).IsEmpty();
    }

    [Test]
    public async Task NewTextInvalidatesTheOldParseWithoutLosingTheNewDocument()
    {
        await using var context = BrowserFixture.Context();
        using var form = Form();
        await using var engine = Boundary(context);
        var component = Render(context, form);
        await component.InvokeAsync(() => engine.ChangeAsync(Pending));
        engine.ReadRelease = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task old = Click(component, "developer-preview");
        await engine.ReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await component.InvokeAsync(() => engine.ChangeAsync(Pending.Replace("pending.test", "newer.test", StringComparison.Ordinal)));
        engine.ReadRelease.TrySetResult();
        await old.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(component.FindAll("[data-developer-preview]")).IsEmpty();
        await Assert.That(engine.LastRead!.All(value => value == 0)).IsTrue();
        engine.ReadRelease = null;
        await Click(component, "developer-preview");
        await Click(component, "developer-apply");
        await Assert.That(form.Snapshot().Values["hostname"].GetString()).IsEqualTo("newer.test");
    }

    [Test]
    public async Task ApplySwapsOncePreservesProtectedAnswersAndReusesThePrivateEditor()
    {
        await using var context = BrowserFixture.Context();
        using var form = Form();
        form.EditValue(["credential"], "synthetic-protected-sentinel");
        await using var engine = Boundary(context);
        var component = Render(context, form);
        await Assert.That(engine.CurrentText).DoesNotContain("synthetic-protected-sentinel");
        await Assert.That(engine.Completions.Any(item => item.Apply.Contains("credential", StringComparison.Ordinal))).IsFalse();
        await component.InvokeAsync(() => engine.ChangeAsync(Pending));
        long revision = form.InputRevision;
        int changes = 0;
        form.InputChanged += (_, _) => changes++;
        await Click(component, "developer-preview");
        await Assert.That(form.Snapshot().Values.ContainsKey("hostname")).IsFalse();
        await Assert.That(component.Markup).DoesNotContain("pending.test");
        await Click(component, "developer-apply");
        await Assert.That(form.InputRevision).IsEqualTo(revision + 1);
        await Assert.That(changes).IsEqualTo(1);
        await Assert.That(form.Snapshot().Values["hostname"].GetString()).IsEqualTo("pending.test");
        await Assert.That(form.Snapshot().Values["credential"].GetString()).IsEqualTo("synthetic-protected-sentinel");
        await Assert.That(engine.CurrentText).IsEqualTo(Pending);
        await Assert.That(component.Instance.IsDirty).IsFalse();
        await Assert.That(engine.MountCount).IsEqualTo(1);
        await Assert.That(engine.ReplacementCount).IsEqualTo(0);
        await Assert.That(engine.MountSource!.All(value => value == 0)).IsTrue();
        await Assert.That(engine.LastRead!.All(value => value == 0)).IsTrue();
        await component.InvokeAsync(() => engine.ChangeAsync(Pending.Replace("pending.test", "second.test", StringComparison.Ordinal)));
        await Click(component, "developer-preview");
        await Click(component, "developer-apply");
        await Assert.That(form.Snapshot().Values["hostname"].GetString()).IsEqualTo("second.test");
        await Assert.That(form.InputRevision).IsEqualTo(revision + 2);
        await Assert.That(engine.MountCount).IsEqualTo(1);
    }

    [Test]
    [Arguments("{invalid synthetic-private-sentinel")]
    [Arguments("""{"answerContractVersion":"0.0.1","values":{"credential":"synthetic-private-sentinel"},"omitted":[]}""")]
    [Arguments("""{"answerContractVersion":"0.0.1","values":{"unknown":"synthetic-private-sentinel"},"omitted":[]}""")]
    public async Task RejectedOrInvalidDocumentsRemainPrivateAndUnchangedByFormat(string source)
    {
        await using var context = BrowserFixture.Context();
        using var form = Form();
        await using var engine = Boundary(context);
        var component = Render(context, form);
        await component.InvokeAsync(() => engine.ChangeAsync(source));
        long revision = form.InputRevision;
        await Click(component, "developer-preview");
        await Assert.That(component.FindAll("[data-developer-diagnostic]").Count > 0).IsTrue();
        await Assert.That(component.Markup).DoesNotContain("synthetic-private-sentinel");
        await Click(component, "developer-format");
        await Assert.That(engine.CurrentText).IsEqualTo(source);
        await Assert.That(component.Instance.IsDirty).IsTrue();
        await Assert.That(form.InputRevision).IsEqualTo(revision);
        await Assert.That(form.Snapshot().Values).IsEmpty();
    }

    [Test]
    public async Task CanonicalFormatPreservesTypedMeaningWithoutApplying()
    {
        await using var context = BrowserFixture.Context();
        using var form = Form();
        await using var engine = Boundary(context);
        var component = Render(context, form);
        await component.InvokeAsync(() => engine.ChangeAsync("""{"answerContractVersion":"0.0.1","values":{"mode":"cluster","replicas":false,"hostname":"format.test"},"omitted":[]}"""));
        long revision = form.InputRevision;
        await Click(component, "developer-format");
        await Assert.That(form.InputRevision).IsEqualTo(revision);
        await Assert.That(form.Snapshot().Values).IsEmpty();
        using JsonDocument formatted = JsonDocument.Parse(engine.CurrentText);
        await Assert.That(formatted.RootElement.GetProperty("values").GetProperty("replicas").GetBoolean()).IsFalse();
        await Assert.That(engine.LastReplacement!.All(value => value == 0)).IsTrue();
        await Click(component, "developer-preview");
        await Click(component, "developer-apply");
        await Assert.That(form.Snapshot().Values["replicas"].GetBoolean()).IsFalse();
        await Assert.That(form.Snapshot().Values["hostname"].GetString()).IsEqualTo("format.test");
    }

    [Test]
    [Arguments("apply")]
    [Arguments("discard")]
    [Arguments("stay")]
    public async Task DirtyTransitionsRequireAnExplicitDecision(string decision)
    {
        await using var context = BrowserFixture.Context();
        using var form = Form();
        await using var engine = Boundary(context);
        var component = Render(context, form);
        await component.InvokeAsync(() => engine.ChangeAsync(Pending));
        Task<bool>? pending = null;
        await component.InvokeAsync(() => { pending = component.Instance.ResolvePendingAsync(); });
        await Assert.That(pending!.IsCompleted).IsFalse();
        await Assert.That(component.FindAll("[data-developer-resolution]")).Count().IsEqualTo(1);
        await Click(component, "developer-resolve-" + decision);
        bool allowed = await pending.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(allowed).IsEqualTo(decision != "stay");
        await Assert.That(form.Snapshot().Values.ContainsKey("hostname")).IsEqualTo(decision == "apply");
        await Assert.That(component.Instance.IsDirty).IsEqualTo(decision == "stay");
        await Assert.That(engine.Active).IsEqualTo(decision != "discard");
    }

    [Test]
    public async Task PreferencesPreservePendingTextAndPreviewAuthority()
    {
        await using var context = BrowserFixture.Context();
        JsonNode definition = JsonNode.Parse(BrowserFixture.Definition.CanonicalBytes.Span)!;
        definition["locales"] = new JsonArray("ar", "en");
        definition["localization"] = JsonNode.Parse("""
            {"defaultLocale":"en","translations":{"ar":{"direction":"rtl","overrides":{}}}}
            """);
        using var form = new DefinitionFormModel(
            DefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(definition.ToJsonString())),
            DefinitionSessionPrivacy.BrowserSecretsEnabled);
        form.SelectLocale("en");
        await using var engine = Boundary(context);
        var component = Render(context, form);
        await component.InvokeAsync(() => engine.ChangeAsync(Pending));
        await Click(component, "developer-preview");
        await component.InvokeAsync(() => form.SetAllSectionsExpanded(true));
        await component.InvokeAsync(() => form.SelectLocale("ar"));
        await engine.MetadataChanged.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await component.InvokeAsync(() => component.Render(parameters => parameters.Add(item => item.Form, form)));
        await Assert.That(engine.MetadataLocale).IsEqualTo("ar");
        await Assert.That(engine.MetadataPhrases).IsNotNull();
        foreach (string key in new[] { "Find", "next", "previous", "all", "match case", "regexp", "by word", "replace", "replace all", "close", "Completions" })
            await Assert.That(engine.MetadataPhrases!.ContainsKey(key)).IsTrue();
        await Assert.That(engine.CurrentText).IsEqualTo(Pending);
        await Assert.That(component.Instance.IsDirty).IsTrue();
        await Assert.That(component.FindAll("[data-developer-preview]")).Count().IsEqualTo(1);
        await Click(component, "developer-apply");
        await Assert.That(form.Snapshot().Values["hostname"].GetString()).IsEqualTo("pending.test");
    }

    [Test]
    public async Task SameVersionMetadataDoesNotInvalidateThePreview()
    {
        await using var context = BrowserFixture.Context();
        using var form = Form();
        await using var engine = Boundary(context);
        var component = Render(context, form);
        await component.InvokeAsync(() => engine.ChangeAsync(Pending));
        await Click(component, "developer-preview");
        await component.InvokeAsync(() => component.Instance.OnEditorChangedAsync(engine.Generation, engine.Version, false, true));
        await Assert.That(component.Instance.IsDirty).IsTrue();
        await Assert.That(component.FindAll("[data-developer-history-limited]")).Count().IsEqualTo(1);
        await Click(component, "developer-apply");
        await Assert.That(form.Snapshot().Values["hostname"].GetString()).IsEqualTo("pending.test");
    }

    [Test]
    public async Task AChangedJsVersionCannotApplyAnOlderCandidate()
    {
        await using var context = BrowserFixture.Context();
        using var form = Form();
        await using var engine = Boundary(context);
        var component = Render(context, form);
        await component.InvokeAsync(() => engine.ChangeAsync(Pending));
        await Click(component, "developer-preview");
        engine.AdvanceWithoutNotification(Pending.Replace("pending.test", "racing.test", StringComparison.Ordinal));
        long revision = form.InputRevision;
        await Click(component, "developer-apply");
        await Assert.That(form.InputRevision).IsEqualTo(revision);
        await Assert.That(form.Snapshot().Values).IsEmpty();
        await Assert.That(component.Instance.IsDirty).IsTrue();
        await Assert.That(engine.CurrentText).Contains("racing.test");
    }

    [Test]
    public async Task RevocationWhileApplyIsFrozenCannotCommitOrResurrectTheEditor()
    {
        await using var context = BrowserFixture.Context();
        using var form = Form();
        await using var engine = Boundary(context);
        var component = Render(context, form);
        await component.InvokeAsync(() => engine.ChangeAsync(Pending));
        await Click(component, "developer-preview");
        engine.FreezeRelease = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task applying = Click(component, "developer-apply");
        await engine.FreezeStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await component.InvokeAsync(() => form.SetSecretEntryEnabled(false));
        engine.FreezeRelease.TrySetResult();
        await applying.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(form.Snapshot().Values).IsEmpty();
        await Assert.That(engine.Active).IsFalse();
        await Assert.That(component.Instance.HasWork).IsFalse();
    }

    [Test]
    public async Task ObserverFailureAfterCommitIsSuccessfulAndKeepsTheSameModel()
    {
        await using var context = BrowserFixture.Context();
        using var form = Form();
        await using var engine = Boundary(context);
        var component = Render(context, form);
        await component.InvokeAsync(() => engine.ChangeAsync(Pending));
        await Click(component, "developer-preview");
        form.InputChanged += (_, _) => throw new InvalidOperationException("synthetic-observer-failure");
        await Click(component, "developer-apply");
        await Assert.That(form.Snapshot().Values["hostname"].GetString()).IsEqualTo("pending.test");
        await Assert.That(component.Find("[data-developer-status]").GetAttribute("data-code")).IsEqualTo("developer-applied");
        await Assert.That(component.Instance.IsDirty).IsFalse();
        await Assert.That(engine.CurrentText).IsEqualTo(Pending);
    }

    [Test]
    [Arguments("merge")]
    [Arguments("replace")]
    public async Task EnvironmentMissingKeysHaveExactMergeOrOmissionMeaning(string mode)
    {
        await using var context = BrowserFixture.Context();
        using var form = new DefinitionFormModel(EnvironmentDefinition(), DefinitionSessionPrivacy.BrowserSecretsEnabled);
        form.EditValue(["mode"], "cluster");
        form.EditValue(["replicas"], "true");
        form.EditValue(["hostname"], "retained.test");
        await using var engine = Boundary(context);
        var component = Render(context, form, "environment");
        await component.Find("[data-developer-representation]").ChangeAsync(new ChangeEventArgs { Value = "environment" });
        await component.Find("[data-developer-mode]").ChangeAsync(new ChangeEventArgs { Value = mode });
        await component.InvokeAsync(() => engine.ChangeAsync("HOST=changed.test\n"));
        await Click(component, "developer-preview");
        await Assert.That(component.FindAll("[data-developer-change='Omit']").Count > 0).IsEqualTo(mode == "replace");
        await Click(component, "developer-apply");
        await Assert.That(form.Snapshot().Values["hostname"].GetString()).IsEqualTo("changed.test");
        await Assert.That(form.Snapshot().Values.ContainsKey("replicas")).IsEqualTo(mode == "merge");
        await Assert.That(form.Snapshot().Omitted.Contains(DefinitionPath.Parse("/replicas"))).IsEqualTo(mode == "replace");
        await Assert.That(form.Snapshot().Values["mode"].GetString()).IsEqualTo("cluster");
    }

    [Test]
    public async Task EnvironmentFormatNormalizesWithoutChangingEmptyAndBooleanValues()
    {
        await using var context = BrowserFixture.Context();
        using var form = new DefinitionFormModel(EnvironmentDefinition(), DefinitionSessionPrivacy.BrowserSecretsEnabled);
        form.EditValue(["mode"], "cluster");
        await using var engine = Boundary(context);
        var component = Render(context, form, "environment");
        await component.Find("[data-developer-representation]").ChangeAsync(new ChangeEventArgs { Value = "environment" });
        await component.InvokeAsync(() => engine.ChangeAsync("\uFEFF# source comment\r\nPLAIN=\"\"\r\nREPLICAS=true\r\nHOST=formatted.test\r\n"));
        long revision = form.InputRevision;
        await Click(component, "developer-format");
        await Assert.That(form.InputRevision).IsEqualTo(revision);
        await Assert.That(engine.CurrentText).DoesNotContain("\r");
        await Assert.That(engine.CurrentText).DoesNotContain("\uFEFF");
        await Assert.That(engine.CurrentText).DoesNotContain("# source comment");
        await Click(component, "developer-preview");
        await Click(component, "developer-apply");
        await Assert.That(form.Snapshot().Values["plain"].GetString()).IsEqualTo("");
        await Assert.That(form.Snapshot().Values["replicas"].GetBoolean()).IsTrue();
        await Assert.That(form.Snapshot().Values["hostname"].GetString()).IsEqualTo("formatted.test");
    }

    [Test]
    public async Task DiagnosticsRemainBoundedAndValueFree()
    {
        await using var context = BrowserFixture.Context();
        using var form = new DefinitionFormModel(EnvironmentDefinition(), DefinitionSessionPrivacy.BrowserSecretsEnabled);
        form.EditValue(["mode"], "single");
        await using var engine = Boundary(context);
        var component = Render(context, form, "environment");
        await component.Find("[data-developer-representation]").ChangeAsync(new ChangeEventArgs { Value = "environment" });
        await component.InvokeAsync(() => engine.ChangeAsync(string.Join('\n',
            Enumerable.Range(0, 105).Select(index => $"UNKNOWN{index}=synthetic-private-sentinel"))));
        await Click(component, "developer-preview");
        await Assert.That(component.FindAll("[data-developer-diagnostic]")).Count().IsEqualTo(100);
        await Assert.That(component.FindAll("[data-developer-diagnostics-truncated]")).Count().IsEqualTo(1);
        await Assert.That(component.Markup).DoesNotContain("synthetic-private-sentinel");
        await Assert.That(form.Snapshot().Values.ContainsKey("hostname")).IsFalse();
    }

    [Test]
    public async Task ModeLabelsAreLocalizedWithoutChangingMachineIdentifiers()
    {
        await Assert.That(ConfigurationDeveloper.ModeLabel(false, "en")).IsNotEqualTo(ConfigurationDeveloper.ModeLabel(false, "ar"));
        await Assert.That(ConfigurationDeveloper.ModeLabel(true, "en")).IsNotEqualTo(ConfigurationDeveloper.ModeLabel(true, "ar"));
    }

    private static DefinitionFormModel Form(bool consent = true) => new(BrowserFixture.Definition,
        consent ? DefinitionSessionPrivacy.BrowserSecretsEnabled : DefinitionSessionPrivacy.BrowserNoSecrets);
    private static EditorBoundary Boundary(BunitContext context)
    {
        var boundary = new EditorBoundary(context.JSInterop.JSRuntime);
        context.Services.AddSingleton<IJSRuntime>(boundary);
        return boundary;
    }
    private static IRenderedComponent<ConfigurationDeveloper> Render(BunitContext context, DefinitionFormModel form, string? artifact = null) =>
        context.Render<ConfigurationDeveloper>(parameters => parameters.Add(item => item.Form, form).Add(item => item.EnvironmentArtifactId, artifact));
    private static Task Click(IRenderedComponent<ConfigurationDeveloper> component, string action) =>
        component.Find($"[data-action='{action}']").ClickAsync(new MouseEventArgs());

    private static Definition EnvironmentDefinition()
    {
        JsonNode source = JsonNode.Parse(BrowserFixture.Definition.CanonicalBytes.Span)!;
        BrowserFixture.WithField(source, "plain", """{"type":"string","x-configurator":{"sensitivity":"public","detail":"standard"}}""");
        JsonNode artifact = source["artifacts"]![0]!;
        artifact["id"] = "environment";
        artifact["format"] = "dotenv";
        artifact["fileName"] = ".env";
        artifact["projection"]!["members"] = JsonNode.Parse("""
            {"MODE":{"op":"copy","reference":{"scope":"root","path":"/mode"}},
             "HOST":{"op":"copy","reference":{"scope":"root","path":"/hostname"}},
             "REPLICAS":{"op":"copy","reference":{"scope":"root","path":"/replicas"}},
             "PLAIN":{"op":"copy","reference":{"scope":"root","path":"/plain"}},
             "SECRET":{"op":"copy","reference":{"scope":"root","path":"/credential"}}}
            """);
        return DefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString()));
    }

    // Only the external engine boundary is replaced. Parsing, candidates, consent,
    // accepted answers and revisions are the real Presentation/Core owners.
    private sealed class EditorBoundary(IJSRuntime fallback) : IJSRuntime, IJSObjectReference
    {
        private byte[] _source = [];
        private bool _dirty;
        internal TaskCompletionSource Mounted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource MetadataChanged { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal string? MetadataLocale { get; private set; }
        internal Dictionary<string, string>? MetadataPhrases { get; private set; }
        internal TaskCompletionSource ReadStarted { get; set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource ImportStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource ImportReturned { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource FreezeStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource FrameStarted { get; set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource TaskStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource? ReadRelease { get; set; }
        internal TaskCompletionSource? ImportRelease { get; set; }
        internal TaskCompletionSource? FreezeRelease { get; set; }
        internal TaskCompletionSource? FrameRelease { get; set; }
        internal TaskCompletionSource? TaskRelease { get; set; }
        internal ConfigurationDeveloper? Owner { get; private set; }
        internal long Generation { get; private set; }
        internal long Version { get; private set; }
        internal bool Active { get; private set; }
        internal int MountCount { get; private set; }
        internal int ReplacementCount { get; private set; }
        internal byte[]? LastRead { get; private set; }
        internal byte[]? MountSource { get; private set; }
        internal byte[]? LastReplacement { get; private set; }
        internal IReadOnlyList<ConfigurationDeveloper.CompletionItem> Completions { get; private set; } = [];
        internal string CurrentText => Encoding.UTF8.GetString(_source);

        internal void AdvanceWithoutNotification(string text)
        {
            CryptographicOperations.ZeroMemory(_source);
            _source = Encoding.UTF8.GetBytes(text);
            Version++;
            _dirty = true;
        }
        internal Task ChangeAsync(string text)
        {
            if (!Active || Owner is null) throw new InvalidOperationException("editor-retired");
            AdvanceWithoutNotification(text);
            return Owner.OnEditorChangedAsync(Generation, Version, true, false);
        }
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            InvokeAsync<TValue>(identifier, CancellationToken.None, args);
        public async ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            object? result = null;
            if (identifier == "import" && args?[0]?.ToString() is not ("./js/configuration-developer.js" or "./js/configuration-rendering.js"))
                return await fallback.InvokeAsync<TValue>(identifier, cancellationToken, args);
            if (identifier == "import")
            {
                if (args?[0]?.ToString() != "./js/configuration-rendering.js")
                {
                    ImportStarted.TrySetResult();
                    if (ImportRelease is not null) await ImportRelease.Task.WaitAsync(TimeSpan.FromSeconds(5));
                    ImportReturned.TrySetResult();
                }
                result = this;
            }
            else if (identifier == "createBridge") result = this;
            else if (identifier == "nextFrame")
            {
                FrameStarted.TrySetResult();
                if (FrameRelease is not null) await FrameRelease.Task.WaitAsync(TimeSpan.FromSeconds(5));
            }
            else if (identifier == "nextTask")
            {
                TaskStarted.TrySetResult();
                if (TaskRelease is not null) await TaskRelease.Task.WaitAsync(TimeSpan.FromSeconds(5));
            }
            else if (identifier == "mount")
            {
                Generation = (long)args![0]!;
                Owner = ((DotNetObjectReference<ConfigurationDeveloper>)args[2]!).Value;
                var options = (ConfigurationDeveloper.EditorOptions)args[3]!;
                MountSource = options.Source;
                CryptographicOperations.ZeroMemory(_source);
                _source = options.Source.ToArray();
                Completions = options.CompletionItems;
                Version = 1;
                _dirty = false;
                Active = true;
                MountCount++;
                result = Version;
                Mounted.TrySetResult();
            }
            else if (identifier == "metadata")
            {
                MetadataLocale = (string)args![3]!;
                MetadataPhrases = args.Length > 4 ? (Dictionary<string, string>)args[4]! : null;
                MetadataChanged.TrySetResult();
            }
            else if (identifier == "read")
            {
                RequireVersion(args!);
                byte[] transferred = _source.ToArray();
                LastRead = transferred;
                ReadStarted.TrySetResult();
                if (ReadRelease is not null) await ReadRelease.Task.WaitAsync(TimeSpan.FromSeconds(5));
                result = transferred;
            }
            else if (identifier == "freeze")
            {
                if ((bool)args![2]!)
                {
                    RequireVersion(args);
                    FreezeStarted.TrySetResult();
                    if (FreezeRelease is not null) await FreezeRelease.Task.WaitAsync(TimeSpan.FromSeconds(5));
                    result = true;
                }
            }
            else if (identifier == "markApplied")
            {
                RequireVersion(args!);
                _dirty = false;
                if (Owner is not null) await Owner.OnEditorChangedAsync(Generation, Version, false, false);
                result = true;
            }
            else if (identifier == "replace")
            {
                RequireVersion(args!);
                LastReplacement = (byte[])args![2]!;
                CryptographicOperations.ZeroMemory(_source);
                _source = LastReplacement.ToArray();
                Version++;
                _dirty = true;
                ReplacementCount++;
                result = new ConfigurationDeveloper.EditorState(Version, _dirty, false);
            }
            else if (identifier == "retire" && args is not null && (long)args[0]! >= Generation)
            {
                Active = false;
                Owner = null;
                Completions = [];
                CryptographicOperations.ZeroMemory(_source);
                _source = [];
            }
            return result is null ? default! : (TValue)result;
        }
        private void RequireVersion(object?[] args)
        {
            if (!Active || (long)args[0]! != Generation || (long)args[1]! != Version) throw new JSException("developer-stale");
        }
        public ValueTask DisposeAsync()
        {
            CryptographicOperations.ZeroMemory(_source);
            _source = [];
            Active = false;
            Owner = null;
            return ValueTask.CompletedTask;
        }
    }
}
