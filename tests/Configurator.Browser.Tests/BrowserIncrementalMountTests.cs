namespace Configurator.Browser.Tests;

using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using Bunit;
using Configurator.Browser.Layout;
using Configurator.Core.Definitions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

public sealed class BrowserIncrementalMountTests
{
    [Test]
    public async Task CompleteContentRemainsBusyUntilItsFinalBatchAndDensityPreservesMountedControls()
    {
        await using var context = BrowserFixture.Context();
        using var frames = new FrameBoundary(context.JSInterop.JSRuntime);
        context.Services.AddSingleton<IJSRuntime>(frames);
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, Definition()));
        Frame firstFrame = await frames.NextAsync();
        var first = component.FindComponents<ConfigurationField>().Single(field => field.Instance.Path.SequenceEqual(["setting0"])).Instance;
        var form = first.Form;
        long revision = form.InputRevision;
        await Assert.That(Pending(component) > 0).IsTrue();
        await Assert.That(component.FindAll("[data-field='setting35'] input")).IsEmpty();
        await Assert.That(Busy(component)).IsTrue();

        await component.Find("#configuration-density").ChangeAsync(new ChangeEventArgs { Value = "compact" });
        await Assert.That(component.FindComponents<ConfigurationField>()
            .Single(field => field.Instance.Path.SequenceEqual(["setting0"])).Instance).IsSameReferenceAs(first);
        await Assert.That(form.InputRevision).IsEqualTo(revision);
        await component.InvokeAsync(firstFrame.Release);
        while (Pending(component) > 0)
        {
            Frame frame = await frames.NextAsync();
            await component.InvokeAsync(frame.Release);
        }

        await Assert.That(component.FindAll("[data-configuration-mount] input")).Count().IsEqualTo(36);
        await AssertCompleteAsync(component);
        await Assert.That(component.FindComponents<ConfigurationField>().All(field => ReferenceEquals(field.Instance.Form, form))).IsTrue();
        await Assert.That(component.FindComponents<ConfigurationField>()
            .Single(field => field.Instance.Path.SequenceEqual(["setting0"])).Instance).IsSameReferenceAs(first);
    }

    [Test]
    public async Task DisclosureSupersedesHeldMountWithoutChangingAnswersOrRestoringHiddenControls()
    {
        await using var context = BrowserFixture.Context();
        using var frames = new FrameBoundary(context.JSInterop.JSRuntime);
        context.Services.AddSingleton<IJSRuntime>(frames);
        var component = context.Render<ConfigurationEditor>(parameters =>
            parameters.Add(item => item.Definition, Definition(advancedTail: true)));
        await CompleteAsync(component, frames);
        var first = component.FindComponents<ConfigurationField>().Single(field => field.Instance.Path.SequenceEqual(["setting0"])).Instance;
        long revision = first.Form.InputRevision;
        await component.Find("#configuration-detail").ChangeAsync(new ChangeEventArgs { Value = "advanced" });
        Frame obsolete = await frames.NextAsync();
        await Assert.That(Pending(component) > 0).IsTrue();
        await component.Find("#configuration-detail").ChangeAsync(new ChangeEventArgs { Value = "essential" });
        int focusCalls = frames.FocusCalls;
        await component.InvokeAsync(obsolete.Release);
        await CompleteAsync(component, frames);
        await Assert.That(component.FindAll("[data-field='setting35'] input")).IsEmpty();
        await Assert.That(component.FindAll("[data-field='setting35'] [data-action='expand-field']")).Count().IsEqualTo(1);
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
        await CompleteAsync(component, frames);
        await component.Find("#secret-consent").ChangeAsync(new ChangeEventArgs { Value = true });
        await CompleteAsync(component, frames);
        await component.Find("[data-field='setting0'] input").InputAsync(new ChangeEventArgs { Value = "ordinary-retained" });
        await component.Find("[data-field='setting1'] input").InputAsync(new ChangeEventArgs { Value = "synthetic-retired-secret" });
        var secret = component.FindComponents<BrowserCredentialEntryTests.EmittedInputField>()
            .Single(field => field.Instance.Path.SequenceEqual(["setting1"]));
        var form = secret.Instance.Form;
        object oldInput = secret.Instance.CaptureInput();
        await component.Find("#prepare-complete").ClickAsync(new MouseEventArgs());
        await Assert.That(component.FindAll(".configuration-editor__artifact")).Count().IsEqualTo(1);
        await component.Find("#configuration-detail").ChangeAsync(new ChangeEventArgs { Value = "advanced" });
        Frame obsolete = await frames.NextAsync();

        await component.Find("#secret-consent").ChangeAsync(new ChangeEventArgs { Value = false });
        await Assert.That(form.Snapshot().Values.ContainsKey("setting1")).IsFalse();
        await Assert.That(component.FindAll("[data-field='setting1'] input")).IsEmpty();
        await Assert.That(component.FindAll(".configuration-editor__artifact")).IsEmpty();
        int focusCalls = frames.FocusCalls;
        await component.InvokeAsync(obsolete.Release);
        await component.InvokeAsync(() => InvokeInputAsync(oldInput, "synthetic-late-secret"));
        await CompleteAsync(component, frames);
        await Assert.That(frames.FocusCalls).IsEqualTo(focusCalls);
        await Assert.That(form.Snapshot().Values["setting0"].GetString()).IsEqualTo("ordinary-retained");
        await Assert.That(form.Snapshot().Values.ContainsKey("setting1")).IsFalse();
        await Assert.That(component.FindAll("[data-field='setting1'] input")).IsEmpty();

        await component.Find("#secret-consent").ChangeAsync(new ChangeEventArgs { Value = true });
        await CompleteAsync(component, frames);
        await Assert.That(component.FindAll("[data-configuration-mount] input")).Count().IsEqualTo(36);
        await Assert.That(component.Find("[data-field='setting1'] input").GetAttribute("value")).IsEqualTo("");
        await Assert.That(component.Markup).DoesNotContain("synthetic-retired-secret");
        await Assert.That(component.Markup).DoesNotContain("synthetic-late-secret");
        await Assert.That(component.FindAll(".configuration-editor__artifact")).IsEmpty();
        await AssertCompleteAsync(component);
    }

    [Test]
    public async Task DefinitionReplacementCannotMountOrFocusRetiredFields()
    {
        await using var context = BrowserFixture.Context();
        using var frames = new FrameBoundary(context.JSInterop.JSRuntime);
        context.Services.AddSingleton<IJSRuntime>(frames);
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, Definition()));
        Frame obsolete = await frames.NextAsync();
        var previous = component.FindComponents<ConfigurationField>()[0].Instance.Form;
        await component.Find("[data-field='setting0'] input").InputAsync(new ChangeEventArgs { Value = "retired-answer" });
        await component.InvokeAsync(() => component.Render(parameters =>
            parameters.Add(item => item.Definition, Definition(prefix: "replacement", revision: 2))));
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
        Frame obsolete = await frames.NextAsync();
        var form = component.FindComponents<ConfigurationField>()[0].Instance.Form;
        await component.Find("[data-field='setting0'] input").InputAsync(new ChangeEventArgs { Value = "retained-section-answer" });
        await component.Find("[data-section='second']").ClickAsync(new MouseEventArgs());
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
        await Assert.That(Pending(component) > 0).IsTrue();
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
        Frame initial = await frames.NextAsync();
        await component.Find("#configuration-search").ClickAsync(new MouseEventArgs());
        await component.Find("#configuration-search").InputAsync(new ChangeEventArgs { Value = "setting35" });
        await component.Find("[data-discovery-result='setting35'] button").ClickAsync(new MouseEventArgs());
        Frame correction = await frames.NextAsync();
        await component.Find("[data-workflow-step='review']").ClickAsync(new MouseEventArgs());
        int focusCalls = frames.FocusCalls;
        await component.InvokeAsync(initial.Release);
        await component.InvokeAsync(correction.Release);
        await CompleteAsync(component, frames);

        await Assert.That(frames.FocusCalls).IsEqualTo(focusCalls);
        await Assert.That(component.Find("[data-workflow-step='review']").GetAttribute("aria-current")).IsEqualTo("step");
        await Assert.That(component.FindComponents<ConfigurationField>().All(field => !field.Instance.FocusFirstControl)).IsTrue();
        await Assert.That(component.FindAll("[data-configuration-mount] input")).Count().IsEqualTo(36);
        await AssertCompleteAsync(component);
    }

    private static bool Busy<T>(IRenderedComponent<T> component) where T : class, IComponent =>
        component.Find("[data-configuration-mount]").GetAttribute("aria-busy") == "true";

    private static int Pending<T>(IRenderedComponent<T> component) where T : class, IComponent =>
        int.Parse(component.Find("[data-configuration-mount]").GetAttribute("data-pending-fields")!,
            System.Globalization.CultureInfo.InvariantCulture);

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
        SetupDefinition definition = Definition();
        return context.Render<MainLayout>(parameters => parameters.Add(item => item.Body, builder =>
        {
            builder.OpenComponent<ConfigurationEditor>(0);
            builder.AddAttribute(1, nameof(ConfigurationEditor.Definition), definition);
            builder.CloseComponent();
        }));
    }

    private static SetupDefinition Definition(bool advancedTail = false, bool secret = false, string prefix = "setting",
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
                    ["detail"] = index < 4 ? "basic" : advancedTail ? "advanced" : "standard"
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
        return SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString()));
    }

    public sealed class EditorHost : ComponentBase
    {
        [Parameter] public SetupDefinition Definition { get; set; } = null!;
        [Parameter] public bool Show { get; set; }
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            if (!Show) return;
            builder.OpenComponent<ConfigurationEditor>(0);
            builder.AddAttribute(1, nameof(ConfigurationEditor.Definition), Definition);
            builder.CloseComponent();
        }
    }

    private sealed class Frame
    {
        // Inline continuations are deliberate: releasing on the renderer drains
        // the awaited JS result before assertions, including obsolete results
        // that correctly cause no render and therefore have no render event.
        internal TaskCompletionSource Completion { get; } = new();
        internal void Release() => Completion.TrySetResult();
    }

    private sealed class FrameBoundary : IJSRuntime, IDisposable
    {
        private readonly IJSRuntime _fallback;
        private readonly FrameModule _module;
        private readonly Channel<Frame> _frames = Channel.CreateUnbounded<Frame>();
        private readonly List<Frame> _requested = [];
        private bool _disposed;
        internal int FocusCalls { get; private set; }
        internal TaskCompletionSource ModuleDisposed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal Task<Frame> NextAsync() => _frames.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));

        internal FrameBoundary(IJSRuntime fallback)
        {
            _fallback = fallback;
            _module = new FrameModule(this);
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            if (identifier == "import" && args?.FirstOrDefault()?.ToString() == "./configuration-rendering.js")
                return ValueTask.FromResult((TValue)(object)_module);
            if (identifier.Contains("focus", StringComparison.OrdinalIgnoreCase)) ++FocusCalls;
            return _fallback.InvokeAsync<TValue>(identifier, cancellationToken, args);
        }

        public void Dispose()
        {
            _disposed = true;
            foreach (Frame frame in _requested) frame.Release();
            _module.Dispose();
        }

        private sealed class FrameModule(FrameBoundary owner) : IJSObjectReference, IDisposable
        {
            public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
                InvokeAsync<TValue>(identifier, CancellationToken.None, args);

            public async ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            {
                if (identifier != "nextFrame") throw new InvalidOperationException("unexpected-render-boundary");
                if (args is { Length: > 0 }) throw new InvalidOperationException("render-boundary-must-not-receive-answers");
                if (owner._disposed) return default!;
                var frame = new Frame();
                owner._requested.Add(frame);
                owner._frames.Writer.TryWrite(frame);
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
