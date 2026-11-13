namespace Configurator.Browser.Tests;

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using Bunit;
using Configurator.Browser.Layout;
using Configurator.Browser.Resources;
using Configurator.Browser.Services;
using Configurator.Core.Definitions;
using Configurator.Presentation;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

public sealed class BrowserIncrementalMountTests
{
    [Test]
    [Arguments(null, "ar", "rtl")]
    [Arguments("en", "en", "ltr")]
    public async Task StartupShowsLocalizedPendingBeforeControls(string? initialLocale, string locale, string direction)
    {
        await using var context = BrowserFixture.Context();
        using var frames = new FrameBoundary(context.JSInterop.JSRuntime);
        context.Services.AddSingleton<IJSRuntime>(frames);
        JsonObject source = JsonNode.Parse(Definition().CanonicalBytes.Span)!.AsObject();
        source["locales"] = new JsonArray("ar", "en");
        source["localization"] = JsonNode.Parse("""
            {"defaultLocale":"ar","translations":{"ar":{"direction":"rtl","overrides":{}},"en":{"direction":"ltr","overrides":{}}}}
            """);
        Definition definition = DefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString()));
        var component = context.Render<ConfigurationEditor>(parameters => parameters
            .Add(item => item.Definition, definition).Add(item => item.InitialLocale, initialLocale));

        await AssertStartupPendingAsync(component);
        var workspace = component.Find(".workspace");
        await Assert.That(workspace.GetAttribute("lang")).IsEqualTo(locale);
        await Assert.That(workspace.GetAttribute("dir")).IsEqualTo(direction);
        await Assert.That(component.Find("#" + workspace.GetAttribute("aria-labelledby")).TextContent)
            .IsEqualTo(BrowserStrings.Get("WorkspaceTitle", locale));
        await Assert.That(component.Find("[role='status']").TextContent).IsEqualTo(BrowserStrings.Get("Preparing", locale));
        await Assert.That(context.Services.GetRequiredService<BrowserOfflinePolicy>().IsWorkspaceActive).IsFalse();
        await Assert.That(context.Services.GetRequiredService<BrowserNavigationState>().Culture).IsEqualTo((locale, direction));

        await component.InvokeAsync(() => component.Render(parameters => parameters
            .Add(item => item.InitialLocale, locale == "ar" ? "en" : "ar")));
        await StartAsync(component, frames);
        await CompleteAsync(component, frames);
        await Assert.That(component.FindComponents<ConfigurationField>()[0].Instance.Form.Locale).IsEqualTo(locale);
    }

    [Test]
    public async Task StartupRepeatedBeginAndRenderCannotBypassPaintOrReadImport()
    {
        await using var context = BrowserFixture.Context();
        using var frames = new FrameBoundary(context.JSInterop.JSRuntime);
        context.Services.AddSingleton<IJSRuntime>(frames);
        Definition definition = Definition();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, definition));
        using var input = new ObservedStream();
        await component.InvokeAsync(component.Instance.BeginAsync);
        await component.InvokeAsync(() => component.Render(parameters => parameters.Add(item => item.Definition, definition)));
        await component.InvokeAsync(component.Instance.BeginAsync);
        await component.InvokeAsync(() => component.Instance.LoadAsync(input));

        await AssertStartupPendingAsync(component);
        await Assert.That(input.Reads).IsEqualTo(0);
        await Assert.That(frames.StartupRequests).IsEqualTo(1);
        await Assert.That(context.Services.GetRequiredService<BrowserOfflinePolicy>().IsWorkspaceActive).IsFalse();
        await StartAsync(component, frames);
        await CompleteAsync(component, frames);
        DefinitionFormModel form = component.FindComponents<ConfigurationField>()[0].Instance.Form;
        await component.InvokeAsync(component.Instance.BeginAsync);
        await Assert.That(component.FindComponents<ConfigurationField>()[0].Instance.Form).IsSameReferenceAs(form);
        await Assert.That(frames.StartupRequests).IsEqualTo(1);
    }

    [Test]
    public async Task StartupNavigationRetiresPendingWorkWithoutLossConfirmation()
    {
        await using var context = BrowserFixture.Context();
        using var frames = new FrameBoundary(context.JSInterop.JSRuntime);
        context.Services.AddSingleton<IJSRuntime>(frames);
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, Definition()));
        await AssertStartupPendingAsync(component);
        Frame obsolete = await frames.NextStartupAsync();
        var navigation = context.Services.GetRequiredService<BrowserNavigationState>();
        bool confirmed = false;

        bool left = await component.InvokeAsync(() => navigation.TryLeaveAsync(() =>
        {
            confirmed = true;
            return Task.FromResult(false);
        }));
        await component.InvokeAsync(obsolete.Release);

        await Assert.That(left).IsTrue();
        await Assert.That(confirmed).IsFalse();
        await Assert.That(navigation.IsActive).IsFalse();
        await Assert.That(context.Services.GetRequiredService<BrowserOfflinePolicy>().IsWorkspaceActive).IsFalse();
        await AssertStartupPendingAsync(component);
    }

    [Test]
    [Arguments("replacement")]
    [Arguments("removal")]
    [Arguments("discard")]
    public async Task StartupRetirementRejectsHeldPaint(string transition)
    {
        await using var context = BrowserFixture.Context();
        using var frames = new FrameBoundary(context.JSInterop.JSRuntime);
        context.Services.AddSingleton<IJSRuntime>(frames);
        var host = context.Render<EditorHost>(parameters => parameters
            .Add(item => item.Definition, Definition()).Add(item => item.Show, true));
        await AssertStartupPendingAsync(host);
        Frame obsolete = await frames.NextStartupAsync();
        ConfigurationEditor editor = host.FindComponent<ConfigurationEditor>().Instance;

        if (transition == "replacement")
            await host.InvokeAsync(() => host.Render(parameters =>
                parameters.Add(item => item.Definition, Definition(prefix: "replacement", revision: 2))));
        else if (transition == "removal")
            await host.InvokeAsync(() => host.Render(parameters => parameters.Add(item => item.Show, false)));
        else
            await host.InvokeAsync(editor.DiscardAsync);
        await host.InvokeAsync(obsolete.Release);

        await Assert.That(host.FindAll("[data-field]")).IsEmpty();
        await Assert.That(context.Services.GetRequiredService<BrowserOfflinePolicy>().IsWorkspaceActive).IsFalse();
        if (transition == "removal")
        {
            await frames.ModuleDisposed.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await Assert.That(host.FindAll(".workspace")).IsEmpty();
        }
        else
        {
            await AssertStartupPendingAsync(host);
            await StartAsync(host, frames);
            await CompleteAsync(host, frames);
            if (transition == "replacement")
                await Assert.That(host.FindAll("[data-field^='setting']")).IsEmpty();
            else
            {
                await Assert.That(context.Services.GetRequiredService<BrowserOfflinePolicy>().IsWorkspaceActive).IsFalse();
                await Assert.That(host.Find(".configuration-editor__controls").HasAttribute("disabled")).IsTrue();
                await host.InvokeAsync(editor.BeginAsync);
                await Assert.That(context.Services.GetRequiredService<BrowserOfflinePolicy>().IsWorkspaceActive).IsTrue();
            }
        }
    }

    [Test]
    [Arguments("replacement")]
    [Arguments("removal")]
    [Arguments("discard")]
    [Arguments("disposal")]
    public async Task StartupRetirementDuringModuleImportCannotScheduleObsoletePaint(string transition)
    {
        await using var context = BrowserFixture.Context();
        using var frames = new FrameBoundary(context.JSInterop.JSRuntime, holdImport: true);
        context.Services.AddSingleton<IJSRuntime>(frames);
        var host = context.Render<EditorHost>(parameters => parameters
            .Add(item => item.Definition, Definition()).Add(item => item.Show, true));
        await frames.ImportRequested.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await AssertStartupPendingAsync(host);
        ConfigurationEditor editor = host.FindComponent<ConfigurationEditor>().Instance;
        Task disposal = Task.CompletedTask;
        if (transition == "replacement")
            await host.InvokeAsync(() => host.Render(parameters =>
                parameters.Add(item => item.Definition, Definition(prefix: "replacement", revision: 2))));
        else if (transition == "removal")
            await host.InvokeAsync(() => host.Render(parameters => parameters.Add(item => item.Show, false)));
        else if (transition == "discard")
            await host.InvokeAsync(editor.DiscardAsync);
        else
            await host.InvokeAsync(() => { disposal = editor.DisposeAsync().AsTask(); });
        await host.InvokeAsync(frames.ReleaseImport);
        await disposal.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.That(host.FindAll("[data-field]")).IsEmpty();
        await Assert.That(context.Services.GetRequiredService<BrowserOfflinePolicy>().IsWorkspaceActive).IsFalse();
        if (transition is "removal" or "disposal")
        {
            await frames.ModuleDisposed.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await Assert.That(frames.StartupRequests).IsEqualTo(0);
        }
        else
        {
            await StartAsync(host, frames);
            await CompleteAsync(host, frames);
            await Assert.That(frames.StartupRequests).IsEqualTo(1);
            if (transition == "replacement")
                await Assert.That(host.FindAll("[data-field^='setting']")).IsEmpty();
        }
    }

    [Test]
    public async Task StartupDiscardReconstructionRetiresAnswersBeforePainting()
    {
        await using var context = BrowserFixture.Context();
        using var frames = new FrameBoundary(context.JSInterop.JSRuntime);
        context.Services.AddSingleton<IJSRuntime>(frames);
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, Definition()));
        await StartAsync(component, frames);
        await CompleteAsync(component, frames);
        DefinitionFormModel previous = component.FindComponents<ConfigurationField>()[0].Instance.Form;
        await component.Find("[data-field='setting0'] input").InputAsync(new ChangeEventArgs { Value = "retired-answer" });
        await component.InvokeAsync(component.Instance.DiscardAsync);

        await Assert.That(() => previous.Snapshot()).Throws<ObjectDisposedException>();
        await AssertStartupPendingAsync(component);
        await Assert.That(component.Markup).DoesNotContain("retired-answer");
        await StartAsync(component, frames);
        await CompleteAsync(component, frames);
        await Assert.That(component.FindComponents<ConfigurationField>()[0].Instance.Form.Snapshot().Values).IsEmpty();
        await Assert.That(context.Services.GetRequiredService<BrowserOfflinePolicy>().IsWorkspaceActive).IsFalse();
    }

    [Test]
    public async Task InitialDiscoveryReportsUnknownAndBusyUntilAllControlsAreDiscovered()
    {
        await using var context = BrowserFixture.Context();
        using var frames = new FrameBoundary(context.JSInterop.JSRuntime);
        context.Services.AddSingleton<IJSRuntime>(frames);
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, Definition()));
        await StartAsync(component, frames);
        int mounted = component.FindAll("[data-configuration-mount] input").Count;

        await Assert.That(mounted).IsEqualTo(6);
        await Assert.That(Busy(component)).IsTrue();
        await Assert.That(component.Find("[data-configuration-mount]").GetAttribute("data-discovery-state")).IsEqualTo("pending");
        await Assert.That(component.Find("[data-configuration-mount]").GetAttribute("data-pending-fields")).IsEqualTo("unknown");
        bool initialBoundary = true;
        while (Busy(component))
        {
            Frame frame = await frames.NextAsync();
            await Assert.That(frame.Kind).IsEqualTo(initialBoundary ? "nextFrame" : "nextTask");
            initialBoundary = false;
            await component.InvokeAsync(frame.Release);
            int current = component.FindAll("[data-configuration-mount] input").Count;
            await Assert.That(current - mounted).IsLessThanOrEqualTo(10);
            mounted = current;
            if (component.Find("[data-configuration-mount]").GetAttribute("data-discovery-state") == "pending")
            {
                await Assert.That(Busy(component)).IsTrue();
                await Assert.That(component.Find("[data-configuration-mount]").GetAttribute("data-pending-fields")).IsEqualTo("unknown");
            }
        }

        await Assert.That(mounted).IsEqualTo(36);
        await Assert.That(component.Find("[data-configuration-mount]").GetAttribute("data-discovery-state")).IsEqualTo("complete");
        await AssertCompleteAsync(component);
    }

    [Test]
    public async Task RemovingARowWithNoNewNodesWaitsForChildFocusAndVisibilitySettlement()
    {
        await using var context = BrowserFixture.Context();
        using var frames = new FrameBoundary(context.JSInterop.JSRuntime, holdSettlement: true);
        context.Services.AddSingleton<IJSRuntime>(frames);
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, RowDefinition()));
        await StartAsync(component, frames);
        await CompleteAsync(component, frames);
        var form = component.FindComponents<ConfigurationField>()[0].Instance.Form;
        await component.InvokeAsync(() => form.SetValue(DefinitionPath.FromSegments(["items"]),
            JsonSerializer.SerializeToElement(new[] { new { weight = 7 }, new { weight = 42 }, new { weight = 7 } })));
        await CompleteAsync(component, frames);
        var survivor = component.FindComponents<ConfigurationField>()
            .Single(field => field.Instance.Path.SequenceEqual(["items", "1", "weight"])).Instance;
        int focusCalls = frames.FocusCalls;

        await component.FindAll("[data-action='remove-item']")[0].ClickAsync(new MouseEventArgs());

        await Assert.That(Pending(component)).IsEqualTo(0);
        await Assert.That(component.FindComponents<ConfigurationField>()).Count().IsEqualTo(5);
        await Assert.That(component.FindComponents<ConfigurationField>()
            .Single(field => field.Instance.Path.SequenceEqual(["items", "0", "weight"])).Instance).IsSameReferenceAs(survivor);
        await Assert.That(component.Find("[data-field='items.0.weight'] input").GetAttribute("value")).IsEqualTo("42");
        await Assert.That(Busy(component)).IsTrue();
        await Assert.That(frames.FocusCalls).IsEqualTo(focusCalls + 1);
        Frame childRenderTurn = await frames.NextAsync();
        await component.InvokeAsync(childRenderTurn.Release);
        await Assert.That(Busy(component)).IsTrue();
        Frame settlement = await frames.NextAsync();
        await component.InvokeAsync(settlement.Release);
        await AssertCompleteAsync(component);
    }

    [Test]
    [Arguments("edit")]
    [Arguments("replacement")]
    [Arguments("disposal")]
    public async Task RetiredZeroAddSettlementTurnCannotCompleteOrRestoreTheCurrentOwner(string transition)
    {
        await using var context = BrowserFixture.Context();
        using var frames = new FrameBoundary(context.JSInterop.JSRuntime, holdSettlement: true);
        context.Services.AddSingleton<IJSRuntime>(frames);
        var host = context.Render<EditorHost>(parameters => parameters
            .Add(item => item.Definition, RowDefinition()).Add(item => item.Show, true));
        await StartAsync(host, frames);
        await CompleteAsync(host, frames);
        var original = host.FindComponents<ConfigurationField>()[0].Instance.Form;
        await host.InvokeAsync(() => original.SetValue(DefinitionPath.FromSegments(["items"]),
            JsonSerializer.SerializeToElement(new[] { new { weight = 7 }, new { weight = 42 }, new { weight = 7 } })));
        await CompleteAsync(host, frames);
        await host.FindAll("[data-action='remove-item']")[0].ClickAsync(new MouseEventArgs());
        await Assert.That(Pending(host)).IsEqualTo(0);
        await Assert.That(Busy(host)).IsTrue();
        Frame obsolete = await frames.NextAsync();

        if (transition == "edit")
            await host.Find("[data-field='items.0.weight'] input").InputAsync(new ChangeEventArgs { Value = "43" });
        else if (transition == "replacement")
        {
            await host.InvokeAsync(() => host.Render(parameters => parameters.Add(item => item.Definition, Definition(prefix: "replacement", revision: 2))));
            await StartAsync(host, frames);
        }
        else
            await host.InvokeAsync(() => host.Render(parameters => parameters.Add(item => item.Show, false)));
        await host.InvokeAsync(obsolete.Release);

        if (transition == "disposal")
        {
            await Assert.That(host.FindAll("[data-configuration-mount]")).IsEmpty();
            await Assert.That(() => original.Snapshot()).Throws<ObjectDisposedException>();
        }
        else
        {
            await Assert.That(Busy(host)).IsTrue();
            await CompleteAsync(host, frames);
            await AssertCompleteAsync(host);
            if (transition == "edit")
                await Assert.That(host.Find("[data-field='items.0.weight'] input").GetAttribute("value")).IsEqualTo("43");
            else
            {
                await Assert.That(host.FindAll("[data-field^='items']")).IsEmpty();
                await Assert.That(() => original.Snapshot()).Throws<ObjectDisposedException>();
            }
        }
    }

    [Test]
    public async Task CompleteReadinessWaitsForVisibilitySettlementAfterTheFinalAdmissionRender()
    {
        await using var context = BrowserFixture.Context();
        using var frames = new FrameBoundary(context.JSInterop.JSRuntime, holdSettlement: true);
        context.Services.AddSingleton<IJSRuntime>(frames);
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, Definition()));
        await StartAsync(component, frames);
        var first = component.FindComponents<ConfigurationField>()[0].Instance;
        long revision = first.Form.InputRevision;
        while (HasPending(component))
        {
            Frame frame = await frames.NextAsync();
            await component.InvokeAsync(frame.Release);
        }

        await Assert.That(component.FindAll("[data-configuration-mount] input")).Count().IsEqualTo(36);
        await Assert.That(Busy(component)).IsTrue();
        Frame childRenderTurn = await frames.NextAsync();
        await component.InvokeAsync(childRenderTurn.Release);
        await Assert.That(Busy(component)).IsTrue();
        Frame settlement = await frames.NextAsync();
        await component.InvokeAsync(settlement.Release);

        await AssertCompleteAsync(component);
        await Assert.That(first.Form.InputRevision).IsEqualTo(revision);
        await Assert.That(component.FindComponents<ConfigurationField>()
            .Single(field => field.Instance.Path.SequenceEqual(first.Path)).Instance).IsSameReferenceAs(first);
    }

    [Test]
    [Arguments("edit")]
    [Arguments("replacement")]
    [Arguments("disposal")]
    public async Task RetiredVisibilitySettlementCannotCompleteOrRestoreTheCurrentOwner(string transition)
    {
        await using var context = BrowserFixture.Context();
        using var frames = new FrameBoundary(context.JSInterop.JSRuntime, holdSettlement: true);
        context.Services.AddSingleton<IJSRuntime>(frames);
        var host = context.Render<EditorHost>(parameters => parameters
            .Add(item => item.Definition, Definition()).Add(item => item.Show, true));
        await StartAsync(host, frames);
        var original = host.FindComponents<ConfigurationField>()[0].Instance.Form;
        while (HasPending(host))
        {
            Frame frame = await frames.NextAsync();
            await host.InvokeAsync(frame.Release);
        }
        await Assert.That(Busy(host)).IsTrue();
        Frame childRenderTurn = await frames.NextAsync();
        await host.InvokeAsync(childRenderTurn.Release);
        Frame obsolete = await frames.NextAsync();

        if (transition == "edit")
            await host.Find("[data-field='setting0'] input").InputAsync(new ChangeEventArgs { Value = "synthetic-settlement-edit" });
        else if (transition == "replacement")
        {
            await host.InvokeAsync(() => host.Render(parameters => parameters.Add(item => item.Definition, Definition(prefix: "replacement", revision: 2))));
            await StartAsync(host, frames);
        }
        else
            await host.InvokeAsync(() => host.Render(parameters => parameters.Add(item => item.Show, false)));
        await host.InvokeAsync(obsolete.Release);

        if (transition == "disposal")
        {
            await Assert.That(host.FindAll("[data-configuration-mount]")).IsEmpty();
            await Assert.That(() => original.Snapshot()).Throws<ObjectDisposedException>();
        }
        else
        {
            await Assert.That(Busy(host)).IsTrue();
            await CompleteAsync(host, frames);
            await AssertCompleteAsync(host);
            if (transition == "edit")
                await Assert.That(original.Snapshot().Values["setting0"].GetString()).IsEqualTo("synthetic-settlement-edit");
            else
            {
                await Assert.That(host.FindAll("[data-field^='setting']")).IsEmpty();
                await Assert.That(() => original.Snapshot()).Throws<ObjectDisposedException>();
            }
        }
    }

    [Test]
    public async Task ReplacingRowsWithNoPendingNodesWaitsForVisibilitySettlement()
    {
        await using var context = BrowserFixture.Context();
        using var frames = new FrameBoundary(context.JSInterop.JSRuntime, holdSettlement: true);
        context.Services.AddSingleton<IJSRuntime>(frames);
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, RowDefinition()));
        await StartAsync(component, frames);
        await CompleteAsync(component, frames);
        var form = component.FindComponents<ConfigurationField>()[0].Instance.Form;
        await component.InvokeAsync(() => form.SetValue(DefinitionPath.FromSegments(["items"]),
            JsonSerializer.SerializeToElement(new[] { new { weight = 7 }, new { weight = 42 }, new { weight = 7 } })));
        await CompleteAsync(component, frames);

        await component.InvokeAsync(() => form.SetValue(DefinitionPath.FromSegments(["items"]),
            JsonSerializer.SerializeToElement(new[] { new { weight = 42 }, new { weight = 7 } })));

        await Assert.That(Pending(component)).IsEqualTo(0);
        await Assert.That(component.FindComponents<ConfigurationField>()).Count().IsEqualTo(5);
        await Assert.That(Busy(component)).IsTrue();
        Frame childRenderTurn = await frames.NextAsync();
        await component.InvokeAsync(childRenderTurn.Release);
        await Assert.That(Busy(component)).IsTrue();
        Frame settlement = await frames.NextAsync();
        await component.InvokeAsync(settlement.Release);
        await AssertCompleteAsync(component);
        await Assert.That(component.Find("[data-field='items.0.weight'] input").GetAttribute("value")).IsEqualTo("42");
    }

    [Test]
    public async Task SettledScalarEditsDoNotStartAnotherVisibilitySettlement()
    {
        await using var context = BrowserFixture.Context();
        using var frames = new FrameBoundary(context.JSInterop.JSRuntime, holdSettlement: true);
        context.Services.AddSingleton<IJSRuntime>(frames);
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, Definition()));
        await StartAsync(component, frames);
        await CompleteAsync(component, frames);
        var first = component.FindComponents<ConfigurationField>()
            .Single(field => field.Instance.Path.SequenceEqual(["setting0"])).Instance;

        await component.Find("[data-field='setting0'] input").InputAsync(new ChangeEventArgs { Value = "synthetic-scalar-edit" });

        await AssertCompleteAsync(component);
        await Assert.That(component.FindComponents<ConfigurationField>()
            .Single(field => field.Instance.Path.SequenceEqual(["setting0"])).Instance).IsSameReferenceAs(first);
        await Assert.That(component.Find("[data-field='setting0'] input").GetAttribute("value")).IsEqualTo("synthetic-scalar-edit");
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(true, false)]
    [Arguments(false, true)]
    [Arguments(true, true)]
    public async Task EveryBranchSharesTheYieldAllowanceIncludingReadonlyAndLiteralRoots(bool readOnly, bool literalRoot)
    {
        await using var context = BrowserFixture.Context();
        using var frames = new FrameBoundary(context.JSInterop.JSRuntime);
        context.Services.AddSingleton<IJSRuntime>(frames);
        JsonObject configuration = Group(new JsonObject { ["first"] = StringGroup(18), ["second"] = StringGroup(18) });
        configuration["readOnly"] = readOnly;
        if (literalRoot) configuration["additionalProperties"] = new JsonObject
        {
            ["type"] = "string", ["x-configurator"] = new JsonObject { ["sensitivity"] = "public" }
        };
        var component = context.Render<ConfigurationEditor>(parameters =>
            parameters.Add(item => item.Definition, WithConfiguration(configuration)));
        await StartAsync(component, frames);
        int expectedFields = literalRoot ? 39 : 38;
        int mounted = component.FindComponents<ConfigurationField>().Count;

        await Assert.That(mounted).IsEqualTo(6);
        await Assert.That(Pending(component)).IsNull();
        bool initialBoundary = true;
        while (Busy(component))
        {
            Frame frame = await frames.NextAsync();
            await Assert.That(frame.Kind).IsEqualTo(initialBoundary ? "nextFrame" : "nextTask");
            initialBoundary = false;
            await component.InvokeAsync(frame.Release);
            int current = component.FindComponents<ConfigurationField>().Count;
            await Assert.That(current - mounted).IsLessThanOrEqualTo(10);
            mounted = current;
        }

        await Assert.That(mounted).IsEqualTo(expectedFields);
        await Assert.That(component.FindAll("[data-field$='.setting17'] input")).Count().IsEqualTo(2);
        await Assert.That(component.FindAll("[data-field$='.setting17'] input")
            .All(input => input.HasAttribute("disabled") == readOnly)).IsTrue();
        await AssertCompleteAsync(component);
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(true, false)]
    [Arguments(false, true)]
    [Arguments(true, true)]
    public async Task CollectionRemovalRetiresHeldDescendantsAndKeepsSurvivingEditors(bool map, bool literalRoot)
    {
        await using var context = BrowserFixture.Context();
        context.ComponentFactories.Add<ConfigurationField, BrowserCredentialEntryTests.EmittedInputField>();
        using var frames = new FrameBoundary(context.JSInterop.JSRuntime);
        context.Services.AddSingleton<IJSRuntime>(frames);
        var collection = new JsonObject
        {
            ["type"] = map ? "object" : "array",
            ["x-configurator"] = new JsonObject { ["sensitivity"] = "public" },
            [map ? "additionalProperties" : "items"] = StringGroup(18)
        };
        JsonObject configuration = Group(new JsonObject { ["items"] = collection });
        if (literalRoot)
            configuration = new JsonObject
            {
                ["type"] = "object", ["additionalProperties"] = configuration,
                ["x-configurator"] = new JsonObject { ["sensitivity"] = "public" }
            };
        string[] collectionPath = literalRoot ? ["group", "items"] : ["items"];
        var component = context.Render<ConfigurationEditor>(parameters =>
            parameters.Add(item => item.Definition, WithConfiguration(configuration)));
        await StartAsync(component, frames);
        await CompleteAsync(component, frames);
        DefinitionFormModel form = component.FindComponents<ConfigurationField>()[0].Instance.Form;
        if (literalRoot)
            await component.InvokeAsync(() => form.AddMapEntry(DefinitionPath.FromSegments([]), "group"));
        await component.InvokeAsync(() => form.SetValue(DefinitionPath.FromSegments(collectionPath),
            JsonSerializer.SerializeToElement(map ? (object)new Dictionary<string, object> { ["first"] = new { }, ["second"] = new { } }
                : new object[] { new { }, new { } })));
        await CompleteAsync(component, frames);
        string survivor = map ? "second" : "1";
        var retained = component.FindComponents<BrowserCredentialEntryTests.EmittedInputField>()
            .Single(field => field.Instance.Path.SequenceEqual([.. collectionPath, survivor, "setting0"])).Instance;
        string retainedIdentity = form.GetEditorIdentity(DefinitionPath.FromSegments([.. collectionPath, survivor, "setting0"]));
        var removed = component.FindComponents<BrowserCredentialEntryTests.EmittedInputField>()
            .Single(field => field.Instance.Path.SequenceEqual([.. collectionPath, map ? "first" : "0", "setting0"])).Instance;
        object retiredInput = removed.CaptureInput();
        await component.InvokeAsync(() =>
        {
            if (map) form.AddMapEntry(DefinitionPath.FromSegments(collectionPath), "third");
            else form.AddListItem(DefinitionPath.FromSegments(collectionPath));
        });
        Frame obsolete = await frames.NextAsync();

        await component.InvokeAsync(() =>
        {
            if (map) form.EditValue(DefinitionPath.FromSegments([.. collectionPath, "first"]), null);
            else form.RemoveListItem(DefinitionPath.FromSegments(collectionPath), 0);
        });
        long revision = form.InputRevision;
        await component.InvokeAsync(obsolete.Release);
        await component.InvokeAsync(() => InvokeInputAsync(retiredInput, "synthetic-retired-row"));
        await CompleteAsync(component, frames);

        string currentSurvivor = map ? "second" : "0";
        await Assert.That(form.GetEditorIdentity(DefinitionPath.FromSegments([.. collectionPath, currentSurvivor, "setting0"])))
            .IsEqualTo(retainedIdentity);
        await Assert.That(component.FindComponents<BrowserCredentialEntryTests.EmittedInputField>()
            .Single(field => field.Instance.Path.SequenceEqual([.. collectionPath, currentSurvivor, "setting0"])).Instance)
            .IsSameReferenceAs(retained);
        await Assert.That(component.FindComponents<ConfigurationField>()).Count().IsEqualTo(literalRoot ? 41 : 39);
        await Assert.That(form.InputRevision).IsEqualTo(revision);
        await Assert.That(component.Markup).DoesNotContain("synthetic-retired-row");
        await AssertCompleteAsync(component);
    }

    [Test]
    public async Task DeepCorrectionKeepsItsFocusIntentUntilTheTargetActuallyMounts()
    {
        await using var context = BrowserFixture.Context();
        using var frames = new FrameBoundary(context.JSInterop.JSRuntime);
        context.Services.AddSingleton<IJSRuntime>(frames);
        JsonObject branch = Group(new JsonObject { ["target"] = new JsonObject { ["type"] = "string" } });
        for (int index = 11; index >= 0; index--)
            branch = Group(new JsonObject { ["level" + index] = branch });
        Definition definition = WithConfiguration(Group(new JsonObject { ["settings"] = branch, ["others"] = StringGroup(36) }));
        using var form = new DefinitionFormModel(definition);
        string[] focusPath = ["settings", .. Enumerable.Range(0, 12).Select(index => "level" + index), "target"];
        var component = context.Render<ConfigurationSectionFields>(parameters => parameters
            .Add(item => item.Form, form).Add(item => item.Fields, form.Validate().Fields)
            .Add(item => item.FocusField, "settings").Add(item => item.FocusPath, focusPath)
            .Add(item => item.FocusGeneration, 1));
        await Assert.That(component.FindAll("[data-field$='.target'] input")).IsEmpty();
        await Assert.That(Pending(component)).IsNull();
        int mounted = component.FindComponents<ConfigurationField>().Count;
        await Assert.That(mounted).IsEqualTo(6);
        int beforeFocus = frames.FocusCalls;

        while (Busy(component))
        {
            Frame frame = await frames.NextAsync();
            await component.InvokeAsync(frame.Release);
            int current = component.FindComponents<ConfigurationField>().Count;
            await Assert.That(current - mounted).IsLessThanOrEqualTo(10);
            mounted = current;
        }

        await Assert.That(mounted).IsEqualTo(51);
        var target = component.FindComponents<ConfigurationField>()
            .Single(field => field.Instance.Path.SequenceEqual(focusPath));
        await Assert.That(target.FindAll("input")).Count().IsEqualTo(1);
        await Assert.That(target.Instance.FocusFirstControl).IsTrue();
        await Assert.That(frames.FocusCalls).IsEqualTo(beforeFocus + 1);
        await AssertCompleteAsync(component);
    }

    [Test]
    public async Task LiteralBranchReplacementCannotResumeItsRetiredDescendantFrameOrInput()
    {
        await using var context = BrowserFixture.Context();
        context.ComponentFactories.Add<ConfigurationField, BrowserCredentialEntryTests.EmittedInputField>();
        using var frames = new FrameBoundary(context.JSInterop.JSRuntime);
        context.Services.AddSingleton<IJSRuntime>(frames);
        JsonObject source = JsonNode.Parse(BrowserRootEditingTests.Definition("Conformance.RootVariant").CanonicalBytes.Span)!.AsObject();
        foreach ((JsonNode? branch, int index) in source["configuration"]!["oneOf"]!.AsArray().Select((branch, index) => (branch, index)))
        {
            var shape = branch!.AsObject();
            shape["properties"] = new JsonObject
            {
                ["mode"] = new JsonObject
                {
                    ["type"] = "string", ["const"] = index == 0 ? "disk" : "queue",
                    ["x-configurator"] = new JsonObject { ["sensitivity"] = "public" }
                },
                ["settings"] = StringGroup(36)
            };
            shape["required"] = new JsonArray("mode", "settings");
            // The compiled member order names the replaced properties; the rewritten branch keeps authored order.
            (shape["x-configurator"] as JsonObject)?.Remove("propertyOrder");
        }
        var component = context.Render<ConfigurationEditor>(parameters => parameters
            .Add(item => item.Definition, WithConfiguration(source["configuration"]!.DeepClone().AsObject())));
        await StartAsync(component, frames);
        await BrowserRootEditingTests.Choose(component, [], "0");
        Frame obsolete = await frames.NextAsync();
        var previous = component.FindComponents<BrowserCredentialEntryTests.EmittedInputField>()
            .Single(field => field.Instance.Path.SequenceEqual(["settings", "setting0"])).Instance;
        object oldInput = previous.CaptureInput();
        string previousIdentity = previous.Form.GetEditorIdentity(DefinitionPath.FromSegments(["settings", "setting0"]));

        await BrowserRootEditingTests.Choose(component, [], "1");
        long revision = previous.Form.InputRevision;
        await component.InvokeAsync(obsolete.Release);
        await component.InvokeAsync(() => InvokeInputAsync(oldInput, "synthetic-retired-branch"));
        await CompleteAsync(component, frames);

        await Assert.That(previous.Form.InputRevision).IsEqualTo(revision);
        await Assert.That(previous.Form.Snapshot().Values["mode"].GetString()).IsEqualTo("queue");
        await Assert.That(previous.Form.GetEditorIdentity(DefinitionPath.FromSegments(["settings", "setting0"])))
            .IsNotEqualTo(previousIdentity);
        await Assert.That(component.FindComponents<ConfigurationField>()).Count().IsEqualTo(39);
        await Assert.That(component.Markup).DoesNotContain("synthetic-retired-branch");
        await AssertCompleteAsync(component);
    }

    [Test]
    public async Task NestedConsentRevocationRejectsHeldMountAndRetiredInputBeforeReconsent()
    {
        await using var context = BrowserFixture.Context();
        context.ComponentFactories.Add<ConfigurationField, BrowserCredentialEntryTests.EmittedInputField>();
        using var frames = new FrameBoundary(context.JSInterop.JSRuntime);
        context.Services.AddSingleton<IJSRuntime>(frames);
        JsonObject secret = StringGroup(36);
        secret["x-configurator"]!["sensitivity"] = "secret";
        var component = context.Render<ConfigurationEditor>(parameters => parameters
            .Add(item => item.Definition, WithConfiguration(Group(new JsonObject { ["settings"] = secret }))));
        await StartAsync(component, frames);
        await component.Find("#secret-consent").ChangeAsync(new ChangeEventArgs { Value = true });
        var field = component.FindComponents<BrowserCredentialEntryTests.EmittedInputField>()
            .Single(item => item.Instance.Path.SequenceEqual(["settings", "setting0"]));
        await field.Find("input").InputAsync(new ChangeEventArgs { Value = "synthetic-nested-secret" });
        var currentField = component.FindComponents<BrowserCredentialEntryTests.EmittedInputField>()
            .Single(item => item.Instance.Path.SequenceEqual(["settings", "setting0"])).Instance;
        object oldInput = currentField.CaptureInput();
        Frame obsolete = await frames.NextAsync();

        await component.Find("#secret-consent").ChangeAsync(new ChangeEventArgs { Value = false });
        await component.InvokeAsync(obsolete.Release);
        await component.InvokeAsync(() => InvokeInputAsync(oldInput, "synthetic-late-nested-secret"));
        await CompleteAsync(component, frames);

        await Assert.That(component.FindAll("[data-configuration-mount] input")).IsEmpty();
        await Assert.That(component.Markup).DoesNotContain("synthetic-nested-secret");
        await Assert.That(component.Markup).DoesNotContain("synthetic-late-nested-secret");
        await component.Find("#secret-consent").ChangeAsync(new ChangeEventArgs { Value = true });
        await CompleteAsync(component, frames);
        await Assert.That(component.FindAll("[data-configuration-mount] input")).Count().IsEqualTo(36);
        await Assert.That(component.Find("[data-field='settings.setting0'] input").GetAttribute("value")).IsEqualTo("");
        await AssertCompleteAsync(component);
    }

    [Test]
    public async Task ScalarEditsKeepMountedDescendantsAndDoNotSpendAnotherInitialAllowance()
    {
        await using var context = BrowserFixture.Context();
        using var frames = new FrameBoundary(context.JSInterop.JSRuntime);
        context.Services.AddSingleton<IJSRuntime>(frames);
        var component = context.Render<ConfigurationEditor>(parameters => parameters
            .Add(item => item.Definition, WithConfiguration(Group(new JsonObject { ["settings"] = StringGroup(36) }))));
        await StartAsync(component, frames);
        Frame obsolete = await frames.NextAsync();
        var field = component.FindComponents<ConfigurationField>()
            .Single(item => item.Instance.Path.SequenceEqual(["settings", "setting0"])).Instance;
        long revision = field.Form.InputRevision;
        int focusCalls = frames.FocusCalls;
        await Assert.That(Pending(component)).IsNull();

        await component.Find("[data-field='settings.setting0'] input").InputAsync(new ChangeEventArgs { Value = "synthetic-current-input" });
        await component.InvokeAsync(obsolete.Release);

        await Assert.That(component.FindComponents<ConfigurationField>()).Count().IsEqualTo(6);
        await Assert.That(Pending(component)).IsEqualTo(31);
        await Assert.That(component.FindComponents<ConfigurationField>()
            .Single(item => item.Instance.Path.SequenceEqual(["settings", "setting0"])).Instance).IsSameReferenceAs(field);
        await Assert.That(field.Form.InputRevision).IsEqualTo(revision + 1);
        await Assert.That(frames.FocusCalls).IsEqualTo(focusCalls);
        await CompleteAsync(component, frames);
        await Assert.That(component.Find("[data-field='settings.setting0'] input").GetAttribute("value")).IsEqualTo("synthetic-current-input");
        await AssertCompleteAsync(component);
    }

    [Test]
    public async Task AddingADeepRowKeepsFocusUntilItsFirstEditableDescendantMounts()
    {
        await using var context = BrowserFixture.Context();
        using var frames = new FrameBoundary(context.JSInterop.JSRuntime);
        context.Services.AddSingleton<IJSRuntime>(frames);
        JsonObject item = Group(new JsonObject { ["target"] = new JsonObject { ["type"] = "string" } });
        for (int index = 0; index < 12; index++) item = Group(new JsonObject { ["nested"] = item });
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition,
            WithConfiguration(Group(new JsonObject { ["items"] = new JsonObject { ["type"] = "array", ["items"] = item } }))));
        await StartAsync(component, frames);
        await CompleteAsync(component, frames);
        var form = component.FindComponents<ConfigurationField>()[0].Instance.Form;
        await component.InvokeAsync(() => form.SupplyList(DefinitionPath.FromSegments(["items"])));
        await CompleteAsync(component, frames);
        int focusCalls = frames.FocusCalls;

        await component.Find("[data-action='add-item']").ClickAsync(new MouseEventArgs());
        await Assert.That(component.FindAll("[data-field$='.target'] input")).IsEmpty();
        await Assert.That(Busy(component)).IsTrue();
        await Assert.That(component.FindComponents<ConfigurationField>()
            .Single(field => field.Instance.Path.SequenceEqual(["items", "0"])).Instance.FocusFirstControl).IsTrue();
        await CompleteAsync(component, frames);

        await Assert.That(component.FindAll("[data-field$='.target'] input")).Count().IsEqualTo(1);
        await Assert.That(component.FindComponents<ConfigurationField>()
            .Single(field => field.Instance.Path.Count > 0 && field.Instance.Path[^1] == "target").Instance.FocusFirstControl).IsTrue();
        await Assert.That(frames.FocusCalls).IsEqualTo(focusCalls + 1);
        await AssertCompleteAsync(component);
    }

    [Test]
    public async Task OneContainerYieldsBeforeMountingAllDescendantInputs()
    {
        await using var context = BrowserFixture.Context();
        using var frames = new FrameBoundary(context.JSInterop.JSRuntime);
        context.Services.AddSingleton<IJSRuntime>(frames);
        JsonObject source = JsonNode.Parse(Definition().CanonicalBytes.Span)!.AsObject();
        JsonObject container = source["configuration"]!.DeepClone().AsObject();
        container["required"] = new JsonArray(Enumerable.Range(0, 36)
            .Select(index => (JsonNode?)JsonValue.Create("setting" + index)).ToArray());
        source["familyId"] = "0a7d90e5-0c84-4cc0-9464-e6be8239cc65";
        source["configuration"] = new JsonObject
        {
            ["type"] = "object",
            ["additionalProperties"] = false,
            ["required"] = new JsonArray("settings"),
            ["x-configurator"] = new JsonObject { ["sensitivity"] = "public" },
            ["properties"] = new JsonObject { ["settings"] = container }
        };
        Definition definition = DefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString()));

        var component = context.Render<ConfigurationEditor>(parameters =>
            parameters.Add(item => item.Definition, definition));
        await StartAsync(component, frames);

        int initialInputs = component.FindAll("[data-configuration-mount] input").Count;
        await Assert.That(initialInputs).IsGreaterThan(0);
        await Assert.That(initialInputs).IsLessThan(36);
        await Assert.That(Busy(component)).IsTrue();
        var first = component.FindComponents<ConfigurationField>()
            .Single(field => field.Instance.Path.SequenceEqual(["settings", "setting0"])).Instance;
        long revision = first.Form.InputRevision;

        await CompleteAsync(component, frames);

        await Assert.That(component.FindAll("[data-configuration-mount] input")).Count().IsEqualTo(36);
        await AssertCompleteAsync(component);
        await Assert.That(first.Form.InputRevision).IsEqualTo(revision);
        await Assert.That(component.FindComponents<ConfigurationField>()
            .Single(field => field.Instance.Path.SequenceEqual(["settings", "setting0"])).Instance).IsSameReferenceAs(first);
    }

    [Test]
    public async Task CompleteContentRemainsBusyUntilItsFinalBatchAndDensityPreservesMountedControls()
    {
        await using var context = BrowserFixture.Context();
        using var frames = new FrameBoundary(context.JSInterop.JSRuntime);
        context.Services.AddSingleton<IJSRuntime>(frames);
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, Definition()));
        await StartAsync(component, frames);
        Frame firstFrame = await frames.NextAsync();
        var first = component.FindComponents<ConfigurationField>().Single(field => field.Instance.Path.SequenceEqual(["setting0"])).Instance;
        var form = first.Form;
        long revision = form.InputRevision;
        await Assert.That(HasPending(component)).IsTrue();
        await Assert.That(component.FindAll("[data-field='setting35'] input")).IsEmpty();
        await Assert.That(Busy(component)).IsTrue();

        await component.Find("#configuration-density").ChangeAsync(new ChangeEventArgs { Value = "compact" });
        await Assert.That(component.FindComponents<ConfigurationField>()
            .Single(field => field.Instance.Path.SequenceEqual(["setting0"])).Instance).IsSameReferenceAs(first);
        await Assert.That(form.InputRevision).IsEqualTo(revision);
        await component.InvokeAsync(firstFrame.Release);
        while (HasPending(component))
        {
            Frame frame = await frames.NextAsync();
            await component.InvokeAsync(frame.Release);
        }

        await Assert.That(component.FindAll("[data-configuration-mount] input")).Count().IsEqualTo(36);
        await Assert.That(Busy(component)).IsTrue();
        await CompleteAsync(component, frames);
        await AssertCompleteAsync(component);
        await Assert.That(component.FindComponents<ConfigurationField>().All(field => ReferenceEquals(field.Instance.Form, form))).IsTrue();
        await Assert.That(component.FindComponents<ConfigurationField>()
            .Single(field => field.Instance.Path.SequenceEqual(["setting0"])).Instance).IsSameReferenceAs(first);
    }

    [Test]
    public async Task DetailChangesSupersedeHeldMountWithoutChangingAnswersOrCollapsingOrdinaryControls()
    {
        await using var context = BrowserFixture.Context();
        using var frames = new FrameBoundary(context.JSInterop.JSRuntime);
        context.Services.AddSingleton<IJSRuntime>(frames);
        var component = context.Render<ConfigurationEditor>(parameters =>
            parameters.Add(item => item.Definition, Definition(advancedTail: true)));
        await StartAsync(component, frames);
        var first = component.FindComponents<ConfigurationField>().Single(field => field.Instance.Path.SequenceEqual(["setting0"])).Instance;
        long revision = first.Form.InputRevision;
        Frame obsolete = await frames.NextAsync();
        await Assert.That(Pending(component)).IsNull();
        string? generation = component.Find("[data-configuration-mount]").GetAttribute("data-mount-generation");
        await component.Find("#configuration-detail").ChangeAsync(new ChangeEventArgs { Value = "advanced" });
        await component.Find("#configuration-detail").ChangeAsync(new ChangeEventArgs { Value = "essential" });
        int focusCalls = frames.FocusCalls;
        await component.InvokeAsync(obsolete.Release);
        await Assert.That(component.Find("[data-configuration-mount]").GetAttribute("data-mount-generation")).IsNotEqualTo(generation);
        await Assert.That(component.FindComponents<ConfigurationField>()).Count().IsEqualTo(6);
        await Assert.That(Pending(component)).IsEqualTo(30);
        await CompleteAsync(component, frames);
        await Assert.That(component.FindAll("[data-field='setting35'] input")).Count().IsEqualTo(1);
        await Assert.That(component.FindAll("[data-action='expand-field']")).IsEmpty();
        await Assert.That(frames.FocusCalls).IsEqualTo(focusCalls);
        await Assert.That(first.Form.InputRevision).IsEqualTo(revision);
        await Assert.That(component.FindComponents<ConfigurationField>()
            .Single(field => field.Instance.Path.SequenceEqual(["setting0"])).Instance).IsSameReferenceAs(first);

        await component.Find("#configuration-detail").ChangeAsync(new ChangeEventArgs { Value = "advanced" });
        await CompleteAsync(component, frames);
        await Assert.That(component.FindAll("[data-configuration-mount] input")).Count().IsEqualTo(36);
        await AssertCompleteAsync(component);
    }

    [Test]
    public async Task RevocationRetiresSecretAndOutputBeforeHeldMountOrOldInputCanResume()
    {
        await using var context = BrowserFixture.Context();
        context.ComponentFactories.Add<ConfigurationField, BrowserCredentialEntryTests.EmittedInputField>();
        using var frames = new FrameBoundary(context.JSInterop.JSRuntime);
        context.Services.AddSingleton<IJSRuntime>(frames);
        var component = context.Render<ConfigurationEditor>(parameters =>
            parameters.Add(item => item.Definition, Definition(advancedTail: true, secret: true)));
        await StartAsync(component, frames);
        await CompleteAsync(component, frames);
        await component.Find("#secret-consent").ChangeAsync(new ChangeEventArgs { Value = true });
        await CompleteAsync(component, frames);
        await component.Find("[data-field='setting0'] input").InputAsync(new ChangeEventArgs { Value = "ordinary-retained" });
        await component.Find("[data-field='setting1'] input").InputAsync(new ChangeEventArgs { Value = "synthetic-retired-secret" });
        var form = component.FindComponents<ConfigurationField>()[0].Instance.Form;
        await BrowserFixture.PrepareAsync(component, draft: false);
        await Assert.That(component.FindAll(".configuration-editor__artifact")).Count().IsEqualTo(1);
        await BrowserFixture.SelectPanelAsync(component, "configure");
        var secret = component.FindComponents<BrowserCredentialEntryTests.EmittedInputField>()
            .Single(field => field.Instance.Path.SequenceEqual(["setting1"]));
        object oldInput = secret.Instance.CaptureInput();
        Frame obsolete = await frames.NextAsync();

        await component.Find("#secret-consent").ChangeAsync(new ChangeEventArgs { Value = false });
        await Assert.That(form.Snapshot().Values.ContainsKey("setting1")).IsFalse();
        await Assert.That(component.FindAll("[data-field='setting1'] input")).IsEmpty();
        int focusCalls = frames.FocusCalls;
        await component.InvokeAsync(obsolete.Release);
        await component.InvokeAsync(() => InvokeInputAsync(oldInput, "synthetic-late-secret"));
        await CompleteAsync(component, frames);
        await Assert.That(frames.FocusCalls).IsEqualTo(focusCalls);
        await Assert.That(form.Snapshot().Values["setting0"].GetString()).IsEqualTo("ordinary-retained");
        await Assert.That(form.Snapshot().Values.ContainsKey("setting1")).IsFalse();
        await Assert.That(component.FindAll("[data-field='setting1'] input")).IsEmpty();
        await BrowserFixture.SelectPanelAsync(component, "prepare");
        await Assert.That(component.FindAll(".configuration-editor__artifact")).IsEmpty();
        await BrowserFixture.SelectPanelAsync(component, "configure");
        await CompleteAsync(component, frames);

        await component.Find("#secret-consent").ChangeAsync(new ChangeEventArgs { Value = true });
        await CompleteAsync(component, frames);
        await Assert.That(component.FindAll("[data-configuration-mount] input")).Count().IsEqualTo(36);
        await Assert.That(component.Find("[data-field='setting1'] input").GetAttribute("value")).IsEqualTo("");
        await Assert.That(component.Markup).DoesNotContain("synthetic-retired-secret");
        await Assert.That(component.Markup).DoesNotContain("synthetic-late-secret");
        await AssertCompleteAsync(component);
        await BrowserFixture.SelectPanelAsync(component, "prepare");
        await Assert.That(component.FindAll(".configuration-editor__artifact")).IsEmpty();
    }

    [Test]
    public async Task DefinitionReplacementCannotMountOrFocusRetiredFields()
    {
        await using var context = BrowserFixture.Context();
        using var frames = new FrameBoundary(context.JSInterop.JSRuntime);
        context.Services.AddSingleton<IJSRuntime>(frames);
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, Definition()));
        await StartAsync(component, frames);
        Frame obsolete = await frames.NextAsync();
        var previous = component.FindComponents<ConfigurationField>()[0].Instance.Form;
        await component.Find("[data-field='setting0'] input").InputAsync(new ChangeEventArgs { Value = "retired-answer" });
        await component.InvokeAsync(() => component.Render(parameters =>
            parameters.Add(item => item.Definition, Definition(prefix: "replacement", revision: 2))));
        await StartAsync(component, frames);
        var current = component.FindComponents<ConfigurationField>()[0].Instance.Form;
        int focusCalls = frames.FocusCalls;
        await component.InvokeAsync(obsolete.Release);
        await CompleteAsync(component, frames);

        await Assert.That(ReferenceEquals(previous, current)).IsFalse();
        await Assert.That(() => previous.Snapshot()).Throws<ObjectDisposedException>();
        await Assert.That(current.Snapshot().Values).IsEmpty();
        await Assert.That(component.FindAll("[data-field^='setting']")).IsEmpty();
        await Assert.That(component.FindAll("[data-configuration-mount] input")).Count().IsEqualTo(36);
        await Assert.That(component.Find("[data-field='replacement35'] input").GetAttribute("value")).IsEqualTo("");
        await Assert.That(component.Markup).DoesNotContain("retired-answer");
        await Assert.That(frames.FocusCalls).IsEqualTo(focusCalls);
        await AssertCompleteAsync(component);
    }

    [Test]
    public async Task RemovingEditorFencesHeldFrameAndRetiresItsForm()
    {
        await using var context = BrowserFixture.Context();
        using var frames = new FrameBoundary(context.JSInterop.JSRuntime);
        context.Services.AddSingleton<IJSRuntime>(frames);
        var host = context.Render<EditorHost>(parameters => parameters
            .Add(item => item.Definition, Definition()).Add(item => item.Show, true));
        await StartAsync(host, frames);
        Frame obsolete = await frames.NextAsync();
        var form = host.FindComponents<ConfigurationField>()[0].Instance.Form;
        await host.InvokeAsync(() => host.Render(parameters => parameters.Add(item => item.Show, false)));
        await frames.ModuleDisposed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        int focusCalls = frames.FocusCalls;
        await host.InvokeAsync(obsolete.Release);

        await Assert.That(host.FindAll("[data-field]")).IsEmpty();
        await Assert.That(host.FindAll("[data-configuration-mount]")).IsEmpty();
        await Assert.That(() => form.Snapshot()).Throws<ObjectDisposedException>();
        await Assert.That(frames.FocusCalls).IsEqualTo(focusCalls);
    }

    [Test]
    public async Task SectionNavigationRetainsTheAuthoritativeFormButCannotReviveOldSectionFields()
    {
        await using var context = BrowserFixture.Context();
        using var frames = new FrameBoundary(context.JSInterop.JSRuntime);
        context.Services.AddSingleton<IJSRuntime>(frames);
        var component = context.Render<ConfigurationEditor>(parameters =>
            parameters.Add(item => item.Definition, Definition(sectioned: true)));
        await StartAsync(component, frames);
        var navigation = BrowserFixture.SectionNavigation(context, component);
        Frame obsolete = await frames.NextAsync();
        var form = component.FindComponents<ConfigurationField>()[0].Instance.Form;
        await component.Find("[data-field='setting0'] input").InputAsync(new ChangeEventArgs { Value = "retained-section-answer" });
        await navigation.Find("[data-section='second']").ClickAsync(new MouseEventArgs());
        int focusCalls = frames.FocusCalls;
        await component.InvokeAsync(obsolete.Release);
        await CompleteAsync(component, frames);

        await Assert.That(component.FindAll("[data-section-panel='first']")).IsEmpty();
        await Assert.That(component.FindAll("[data-field='setting0']")).IsEmpty();
        await Assert.That(component.FindAll("[data-configuration-mount] input")).Count().IsEqualTo(18);
        await Assert.That(component.FindAll("[data-field='setting35'] input")).Count().IsEqualTo(1);
        await Assert.That(component.FindComponents<ConfigurationField>().All(field => ReferenceEquals(field.Instance.Form, form))).IsTrue();
        await Assert.That(form.Snapshot().Values["setting0"].GetString()).IsEqualTo("retained-section-answer");
        await Assert.That(frames.FocusCalls).IsEqualTo(focusCalls);
        await AssertCompleteAsync(component);
    }

    [Test]
    [Arguments("search")]
    [Arguments("review")]
    public async Task SearchAndReviewCanReachAnUnmountedFieldWithoutCompletingTheRestEarly(string route)
    {
        await using var context = BrowserFixture.Context();
        using var frames = new FrameBoundary(context.JSInterop.JSRuntime);
        context.Services.AddSingleton<IJSRuntime>(frames);
        var component = RenderWorkspace(context);
        await StartAsync(component, frames);
        Frame obsolete = await frames.NextAsync();
        var form = component.FindComponents<ConfigurationField>()[0].Instance.Form;
        long revision = form.InputRevision;
        int focusCalls = frames.FocusCalls;
        if (route == "search")
        {
            await component.Find("#configuration-search").ClickAsync(new MouseEventArgs());
            await component.Find("#configuration-search").InputAsync(new ChangeEventArgs { Value = "setting35" });
            await component.Find("[data-discovery-result='setting35'] button").ClickAsync(new MouseEventArgs());
        }
        else
        {
            await component.Find("[data-workflow-step='review']").ClickAsync(new MouseEventArgs());
            await component.Find("#review-include-omitted").ChangeAsync(new ChangeEventArgs { Value = true });
            await component.Find("[data-review-path='setting35'] button").ClickAsync(new MouseEventArgs());
        }
        await component.InvokeAsync(obsolete.Release);
        await Assert.That(component.FindAll("[data-field='setting35'] input")).Count().IsEqualTo(1);
        var target = component.FindComponents<ConfigurationField>().Single(field => field.Instance.Path.SequenceEqual(["setting35"])).Instance;
        await Assert.That(target.FocusFirstControl).IsTrue();
        await Assert.That(target.FocusPath.SequenceEqual(["setting35"])).IsTrue();
        await Assert.That(frames.FocusCalls > focusCalls).IsTrue();
        await Assert.That(Busy(component)).IsTrue();
        await Assert.That(HasPending(component)).IsTrue();
        await CompleteAsync(component, frames);
        await Assert.That(form.InputRevision).IsEqualTo(revision);
        if (route == "search") await Assert.That(form.DiscoveryQuery).IsEqualTo("setting35");
        await Assert.That(component.FindAll("[data-configuration-mount] input")).Count().IsEqualTo(36);
        await AssertCompleteAsync(component);
    }

    [Test]
    public async Task ReviewNavigationSupersedesPendingSearchFocusWithoutDroppingCurrentFields()
    {
        await using var context = BrowserFixture.Context();
        using var frames = new FrameBoundary(context.JSInterop.JSRuntime);
        context.Services.AddSingleton<IJSRuntime>(frames);
        var component = RenderWorkspace(context);
        await StartAsync(component, frames);
        Frame initial = await frames.NextAsync();
        await component.Find("#configuration-search").ClickAsync(new MouseEventArgs());
        await component.Find("#configuration-search").InputAsync(new ChangeEventArgs { Value = "setting35" });
        await component.Find("[data-discovery-result='setting35'] button").ClickAsync(new MouseEventArgs());
        Frame correction = await frames.NextAsync();
        await component.Find("[data-workflow-step='review']").ClickAsync(new MouseEventArgs());
        int focusCalls = frames.FocusCalls;
        await component.InvokeAsync(initial.Release);
        await component.InvokeAsync(correction.Release);

        await Assert.That(frames.FocusCalls).IsEqualTo(focusCalls);
        await Assert.That(component.Find("[data-workflow-step='review']").GetAttribute("aria-current")).IsEqualTo("step");
        await BrowserFixture.SelectPanelAsync(component, "configure");
        await CompleteAsync(component, frames);
        await Assert.That(component.FindComponents<ConfigurationField>().All(field => !field.Instance.FocusFirstControl)).IsTrue();
        await Assert.That(component.FindAll("[data-configuration-mount] input")).Count().IsEqualTo(36);
        await AssertCompleteAsync(component);
    }

    private static bool Busy<T>(IRenderedComponent<T> component) where T : class, IComponent =>
        component.Find("[data-configuration-mount]").GetAttribute("aria-busy") == "true";

    private static int? Pending<T>(IRenderedComponent<T> component) where T : class, IComponent
    {
        var mount = component.Find("[data-configuration-mount]");
        string? count = mount.GetAttribute("data-pending-fields");
        return (mount.GetAttribute("data-discovery-state"), count) switch
        {
            ("pending", "unknown") => null,
            ("complete", _) => int.Parse(count!, System.Globalization.CultureInfo.InvariantCulture),
            _ => throw new InvalidOperationException("invalid-discovery-count-protocol")
        };
    }

    private static bool HasPending<T>(IRenderedComponent<T> component) where T : class, IComponent =>
        Pending(component) is not 0;

    private static async Task CompleteAsync<T>(IRenderedComponent<T> component, FrameBoundary frames) where T : class, IComponent
    {
        // Each iteration awaits a real pending boundary request, not a timer or
        // render-count expectation. Release runs its continuation on the renderer.
        while (Busy(component))
        {
            Frame frame = await frames.NextAsync();
            await component.InvokeAsync(frame.Release);
        }
    }

    private static async Task AssertStartupPendingAsync<T>(IRenderedComponent<T> component) where T : class, IComponent
    {
        await Assert.That(component.FindAll("[data-field], [data-required-count], progress, [data-workflow-step]")).IsEmpty();
        await Assert.That(component.Find(".workspace").GetAttribute("aria-busy")).IsEqualTo("true");
        await Assert.That(component.FindAll("[data-code='workspace-ready']")).IsEmpty();
        await Assert.That(component.Find("[role='status']").GetAttribute("data-code")).IsEqualTo("workspace-starting");
    }

    private static async Task StartAsync<T>(IRenderedComponent<T> component, FrameBoundary frames) where T : class, IComponent
    {
        await AssertStartupPendingAsync(component);
        Frame startup = await frames.NextStartupAsync();
        await component.InvokeAsync(startup.Release);
    }

    private sealed class ObservedStream : MemoryStream
    {
        internal int Reads { get; private set; }
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            ++Reads;
            return base.ReadAsync(buffer, cancellationToken);
        }
    }

    private static async Task AssertCompleteAsync<T>(IRenderedComponent<T> component) where T : class, IComponent
    {
        await Assert.That(Busy(component)).IsFalse();
        await Assert.That(Pending(component)).IsEqualTo(0);
        await Assert.That(component.Find("[data-configuration-mount]").GetAttribute("data-mount-state")).IsEqualTo("complete");
    }

    private static Task InvokeInputAsync(object callback, string value)
    {
        var args = new ChangeEventArgs { Value = value };
        return callback switch
        {
            EventCallback<ChangeEventArgs> typed => typed.InvokeAsync(args),
            EventCallback untyped => untyped.InvokeAsync(args),
            MulticastDelegate handler => new EventCallbackWorkItem(handler).InvokeAsync(args),
            _ => throw new InvalidOperationException("input-handler-missing")
        };
    }

    private static IRenderedComponent<MainLayout> RenderWorkspace(BunitContext context)
    {
        Definition definition = Definition();
        return context.Render<MainLayout>(parameters => parameters.Add(item => item.Body, builder =>
        {
            builder.OpenComponent<ConfigurationEditor>(0);
            builder.AddAttribute(1, nameof(ConfigurationEditor.Definition), definition);
            builder.CloseComponent();
        }));
    }

    private static Definition Definition(bool advancedTail = false, bool secret = false, string prefix = "setting",
        long revision = 1, bool sectioned = false)
    {
        JsonObject source = JsonNode.Parse(BrowserFixture.Definition.CanonicalBytes.Span)!.AsObject();
        var properties = new JsonObject();
        for (int index = 0; index < 36; index++)
            properties[prefix + index] = new JsonObject
            {
                ["type"] = "string",
                ["title"] = "Setting " + index,
                ["x-configurator"] = new JsonObject
                {
                    ["sensitivity"] = secret && index == 1 ? "secret" : "public",
                    ["detail"] = advancedTail && index >= 4 ? "advanced" : "basic"
                }
            };
        source["revision"] = revision;
        source["configuration"] = new JsonObject
        {
            ["type"] = "object",
            ["additionalProperties"] = false,
            ["x-configurator"] = new JsonObject { ["sensitivity"] = "public" },
            ["properties"] = properties
        };
        source.Remove("presentation");
        if (sectioned)
            source["presentation"] = JsonNode.Parse("""
                {"defaultJourney":"all","navigation":"sections","sections":[
                  {"id":"first","title":"First section","pointers":["/setting0","/setting1","/setting2","/setting3","/setting4","/setting5","/setting6","/setting7","/setting8","/setting9","/setting10","/setting11","/setting12","/setting13","/setting14","/setting15","/setting16","/setting17"]},
                  {"id":"second","title":"Second section","pointers":["/setting18","/setting19","/setting20","/setting21","/setting22","/setting23","/setting24","/setting25","/setting26","/setting27","/setting28","/setting29","/setting30","/setting31","/setting32","/setting33","/setting34","/setting35"]}],
                 "journeys":[
                  {"id":"all","title":"All settings","sections":["first","second"]}]}
                """);
        source["artifacts"]![0]!["projection"] = JsonNode.Parse("""{"op":"literal","value":{}}""");
        source["requiredCapabilities"] = new JsonArray("projection.literal", "schema.object", "schema.string");
        return DefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString()));
    }

    private static Definition WithConfiguration(JsonObject configuration)
    {
        var source = new JsonObject
        {
            ["$schema"] = "urn:islamu:configurator:authoring:v0.0.1",
            ["identity"] = new JsonObject { ["familyId"] = "0a7d90e5-0c84-4cc0-9464-e6be8239cc65", ["revision"] = 1 },
            ["project"] = new JsonObject { ["id"] = "synthetic.mount", ["name"] = "Synthetic mount" },
            ["applicationVersions"] = new JsonArray("1"),
            ["configuration"] = configuration,
            ["outputs"] = new JsonObject
            {
                ["configuration.json"] = new JsonObject
                {
                    ["id"] = "config", ["format"] = "json",
                    ["projection"] = new JsonObject { ["op"] = "literal", ["value"] = new JsonObject() }
                }
            }
        };
        return DefinitionSourceCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString()));
    }

    private static Definition RowDefinition() => WithConfiguration(Group(new JsonObject
    {
        ["items"] = new JsonObject
        {
            ["type"] = "array", ["x-configurator"] = new JsonObject { ["sensitivity"] = "public" },
            ["items"] = Group(new JsonObject
            {
                ["weight"] = new JsonObject
                {
                    ["type"] = "integer", ["x-configurator"] = new JsonObject { ["sensitivity"] = "public" }
                }
            })
        }
    }));

    private static JsonObject StringGroup(int count)
    {
        var properties = new JsonObject();
        for (int index = 0; index < count; index++)
            properties["setting" + index] = new JsonObject
            {
                ["type"] = "string", ["x-configurator"] = new JsonObject { ["sensitivity"] = "public" }
            };
        return Group(properties);
    }

    private static JsonObject Group(JsonObject properties) => new()
    {
        ["type"] = "object",
        ["additionalProperties"] = false,
        ["required"] = new JsonArray(properties.Select(property => (JsonNode?)JsonValue.Create(property.Key)).ToArray()),
        ["properties"] = properties,
        ["x-configurator"] = new JsonObject { ["sensitivity"] = "public" }
    };

    public sealed class EditorHost : ComponentBase
    {
        [Parameter] public Definition Definition { get; set; } = null!;
        [Parameter] public bool Show { get; set; }
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            if (!Show) return;
            builder.OpenComponent<ConfigurationEditor>(0);
            builder.AddAttribute(1, nameof(ConfigurationEditor.Definition), Definition);
            builder.CloseComponent();
        }
    }

    private sealed class Frame(string kind)
    {
        // Inline continuations are deliberate: releasing on the renderer drains
        // the awaited JS result before assertions, including obsolete results
        // that correctly cause no render and therefore have no render event.
        internal TaskCompletionSource Completion { get; } = new();
        internal string Kind { get; } = kind;
        internal void Release() => Completion.TrySetResult();
    }

    private sealed class FrameBoundary : IJSRuntime, IDisposable
    {
        private readonly IJSRuntime _fallback;
        private readonly FrameModule _module;
        private readonly Channel<Frame> _frames = Channel.CreateUnbounded<Frame>();
        private readonly Channel<Frame> _startupFrames = Channel.CreateUnbounded<Frame>();
        private readonly List<Frame> _requested = [];
        private readonly TaskCompletionSource<IJSObjectReference> _import = new();
        private readonly bool _holdSettlement;
        private bool _disposed;
        internal int FocusCalls { get; private set; }
        internal int StartupRequests { get; private set; }
        internal TaskCompletionSource ImportRequested { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource ModuleDisposed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal Task<Frame> NextAsync() => _frames.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        internal Task<Frame> NextStartupAsync() => _startupFrames.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        internal void ReleaseImport() => _import.TrySetResult(_module);

        internal FrameBoundary(IJSRuntime fallback, bool holdSettlement = false, bool holdImport = false)
        {
            _fallback = fallback;
            _holdSettlement = holdSettlement;
            _module = new FrameModule(this);
            if (!holdImport) ReleaseImport();
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public async ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            if (identifier == "import" && args?.FirstOrDefault()?.ToString() == "./js/configuration-rendering.js")
            {
                ImportRequested.TrySetResult();
                return (TValue)await _import.Task;
            }
            if (identifier.Contains("focus", StringComparison.OrdinalIgnoreCase)) ++FocusCalls;
            return await _fallback.InvokeAsync<TValue>(identifier, cancellationToken, args);
        }

        public void Dispose()
        {
            _disposed = true;
            ReleaseImport();
            foreach (Frame frame in _requested) frame.Release();
            _module.Dispose();
        }

        private sealed class FrameModule(FrameBoundary owner) : IJSObjectReference, IDisposable
        {
            public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
                InvokeAsync<TValue>(identifier, CancellationToken.None, args);

            public async ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            {
                bool settlement = identifier == "revealFocusedControl";
                bool startup = identifier == "paintWorkspace";
                if (!settlement && !startup && identifier is not ("nextFrame" or "nextTask"))
                    throw new InvalidOperationException("unexpected-render-boundary");
                if (settlement || startup)
                {
                    if (args is not [ElementReference, string])
                        throw new InvalidOperationException("settlement-requires-owner-and-generation-only");
                    if (settlement && !owner._holdSettlement) return default!;
                }
                else if (args is { Length: > 0 }) throw new InvalidOperationException("render-boundary-must-not-receive-answers");
                if (owner._disposed) return default!;
                var frame = new Frame(identifier);
                owner._requested.Add(frame);
                if (startup)
                {
                    ++owner.StartupRequests;
                    owner._startupFrames.Writer.TryWrite(frame);
                }
                else owner._frames.Writer.TryWrite(frame);
                await frame.Completion.Task;
                return default!;
            }

            public ValueTask DisposeAsync()
            {
                Dispose();
                return ValueTask.CompletedTask;
            }

            public void Dispose() => owner.ModuleDisposed.TrySetResult();
        }
    }
}
