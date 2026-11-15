namespace Configurator.Browser.Tests;

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using Bunit;
using Configurator.Browser.Components.Configuration;
using Configurator.Browser.Pages;
using Configurator.Browser.Resources;
using Configurator.Browser.Services;
using Configurator.Core.Definitions;
using Configurator.Presentation;
using Configurator.Presentation.Store;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

public sealed class BrowserExperienceRecoveryTests
{
    private static Definition CreateRecoveryDefinition(bool multiArtifact = false, bool withGeneratedSecret = false)
    {
        var properties = new JsonObject
        {
            ["hostname"] = JsonNode.Parse("""{"type":"string","title":"Hostname","x-configurator":{"sensitivity":"public","detail":"basic"}}""")!,
            ["port"] = JsonNode.Parse("""{"type":"integer","title":"Port","minimum":1,"maximum":65535,"x-configurator":{"sensitivity":"public","detail":"basic"}}""")!,
            ["token"] = JsonNode.Parse(withGeneratedSecret
                ? """{"type":"string","title":"API token","minLength":16,"x-configurator":{"sensitivity":"secret","detail":"basic","secret":{"kind":"password","acquisition":"local","generation":{"length":24}}}}"""
                : """{"type":"string","title":"API token","x-configurator":{"sensitivity":"secret","detail":"basic"}}""")!
        };

        var sectionPointers = new JsonArray("/hostname", "/port", "/token");
        for (int index = 0; index < 8; index++)
        {
            string name = $"setting{index}";
            properties[name] = new JsonObject
            {
                ["type"] = "string",
                ["title"] = $"Setting {index}",
                ["x-configurator"] = new JsonObject { ["sensitivity"] = "public", ["detail"] = "basic" }
            };
            sectionPointers.Add("/" + name);
        }

        var outputs = new JsonObject
        {
            ["config.json"] = new JsonObject { ["format"] = "json" }
        };
        if (multiArtifact)
        {
            outputs["env.dotenv"] = new JsonObject
            {
                ["format"] = "dotenv",
                ["bindings"] = new JsonObject
                {
                    ["HOST"] = "/hostname",
                    ["PORT"] = "/port"
                }
            };
        }

        var root = new JsonObject
        {
            ["$schema"] = "urn:islamu:configurator:authoring:v0.0.1",
            ["identity"] = new JsonObject
            {
                ["familyId"] = Guid.NewGuid().ToString("D"),
                ["revision"] = 1
            },
            ["project"] = new JsonObject
            {
                ["id"] = "recovery.test",
                ["name"] = "Recovery Test App"
            },
            ["applicationVersions"] = new JsonArray("1.0"),
            ["configuration"] = new JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = false,
                ["required"] = new JsonArray("hostname", "port"),
                ["x-configurator"] = new JsonObject { ["sensitivity"] = "public" },
                ["properties"] = properties
            },
            ["presentation"] = new JsonObject
            {
                ["defaultJourney"] = "core",
                ["sections"] = new JsonArray
                {
                    new JsonObject { ["id"] = "general", ["title"] = "General", ["pointers"] = sectionPointers }
                },
                ["journeys"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["id"] = "core",
                        ["title"] = "Standard setup",
                        ["description"] = "Essential host settings",
                        ["sections"] = new JsonArray("general")
                    }
                }
            },
            ["outputs"] = outputs
        };

        return DefinitionSourceCodec.ParseJson(Encoding.UTF8.GetBytes(root.ToJsonString()));
    }

    [Test]
    public async Task DistinctPhasesDistinguishAdmissionStartupMountingAndPrivateOperations()
    {
        Definition definition = CreateRecoveryDefinition();
        await using var context = BrowserFixture.Context(new BrowserDefinitionRegistry([]));
        var pwa = context.JSInterop.SetupModule("./pwa-lifecycle.js");
        pwa.Setup<BrowserOfflineState>("initialize").SetResult(new(true, true, true));
        pwa.Setup<string[]>("retainedDigests").SetResult([]);
        var sourceModule = context.JSInterop.SetupModule("./js/browser-definition-source.js");
        var pendingFetch = sourceModule.Setup<byte[]>("fetchDefinition", _ => true);

        context.Services.GetRequiredService<NavigationManager>()
            .NavigateTo("open#src=https%3A%2F%2Fpublisher.example%2Fapp.json");

        // 1. Definition admission in-flight: distinct phase + cancel button keeping source URL intact.
        var openPage = context.Render<Open>();
        await Assert.That(openPage.Find("[data-workspace-phase='definition-admission']").GetAttribute("data-admission-state"))
            .IsEqualTo("loading");
        await Assert.That(openPage.FindAll("[data-startup-state], [data-configuration-mount]")).IsEmpty();
        await openPage.Find("[data-action='cancel-source-load']").ClickAsync(new MouseEventArgs());
        await Assert.That(openPage.Find("[data-workspace-phase='definition-admission']").GetAttribute("data-admission-state"))
            .IsEqualTo("cancelled");
        await Assert.That(openPage.Find("#source-url").GetAttribute("value"))
            .IsEqualTo("https://publisher.example/app.json");
        await Assert.That(openPage.FindAll("[data-action='retry-source'], [data-action='choose-local-source']")).Count().IsEqualTo(2);

        // 2. Public app startup & custody gate: distinct from admission and mounting; cancel returns to BeforeEntry.
        await using var startupContext = BrowserFixture.Context();
        using var startupAndMount = new RecoveryJsBoundary(startupContext.JSInterop.JSRuntime);
        startupAndMount.HoldBeginWorkspace = true;
        startupContext.Services.AddSingleton<IJSRuntime>(startupAndMount);
        await using var policy = new BrowserOfflinePolicy(startupAndMount);
        startupContext.Services.AddSingleton(policy);

        var editor = startupContext.Render<ConfigurationEditor>(parameters => parameters
            .Add(item => item.Definition, definition));

        await startupAndMount.StartupRenderEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(editor.Find("[data-workspace-phase='app-startup']").GetAttribute("data-startup-state"))
            .IsEqualTo("preparing");
        await Assert.That(editor.FindAll("[data-configuration-mount], [data-field]")).IsEmpty();
        await editor.Find("[data-action='cancel-startup']").ClickAsync(new MouseEventArgs());
        await startupAndMount.ReleaseStartupFrameAsync(editor);
        await Assert.That(editor.Find("[data-workspace-phase='app-startup']").GetAttribute("data-startup-state"))
            .IsEqualTo("before-entry");
        await Assert.That(policy.IsWorkspaceActive).IsFalse();

        // 3. Ready workspace while incremental mounting is still pending: mount status distinct from private operations.
        startupAndMount.HoldBeginWorkspace = false;
        await editor.Find("[data-action='start-workspace']").ClickAsync(new MouseEventArgs());
        await editor.InvokeAsync(() => startupAndMount.ReleaseMountStep());

        await Assert.That(editor.Find(".configuration-editor").GetAttribute("data-workspace-phase")).IsEqualTo("ready");
        await Assert.That(editor.Find("[data-configuration-mount]").GetAttribute("data-mount-state")).IsEqualTo("pending");
        await Assert.That(editor.Find("[role='status'][data-mount-status='pending']").TextContent).IsNotEmpty();
        await Assert.That(editor.Find("[data-workspace-phase='private-workspace']")).IsNotNull();

        await startupAndMount.DrainAllMountsAsync(editor);
        await Assert.That(editor.Find("[data-configuration-mount]").GetAttribute("data-mount-state")).IsEqualTo("complete");
        await Assert.That(editor.FindAll("[data-mount-status='pending']")).IsEmpty();
    }

    [Test]
    public async Task OfflineFailedStartupExplainsPreservedStateWithoutRawExceptionsAndRecoversOnRetry()
    {
        Definition definition = CreateRecoveryDefinition();
        await using var context = BrowserFixture.Context();
        using var js = new RecoveryJsBoundary(context.JSInterop.JSRuntime)
        {
            OfflineState = new(false, false, false),
            AcceptWorkspace = false
        };
        context.Services.AddSingleton<IJSRuntime>(js);
        await using var policy = new BrowserOfflinePolicy(js);
        context.Services.AddSingleton(policy);

        var editor = context.Render<ConfigurationEditor>(parameters => parameters
            .Add(item => item.Definition, definition));

        await js.ReleaseStartupFrameAsync(editor);

        var gate = editor.Find("[data-workspace-phase='app-startup']");
        await Assert.That(gate.GetAttribute("data-startup-state")).IsEqualTo("failed");
        await Assert.That(gate.GetAttribute("data-offline-state")).IsEqualTo("offline-incomplete");
        await Assert.That(gate.TextContent).Contains("No");
        await Assert.That(gate.TextContent).DoesNotContain("Exception");
        await Assert.That(editor.Find("[data-action='start-workspace']")).IsNotNull();
        await Assert.That(editor.Find("a[href='']")).IsNotNull();

        // Recovery after reconnection/preparation: retry enters ready workspace cleanly.
        js.OfflineState = new(true, true, true);
        js.AcceptWorkspace = true;
        policy.ConnectivityChanged(true);
        await editor.Find("[data-action='start-workspace']").ClickAsync(new MouseEventArgs());
        await js.DrainAllMountsAsync(editor);

        await Assert.That(editor.FindAll("[data-startup-state]")).IsEmpty();
        await Assert.That(editor.Find(".configuration-editor").GetAttribute("data-workspace-phase")).IsEqualTo("ready");
    }

    [Test]
    [Arguments(WorkspaceLayoutMode.Guided)]
    [Arguments(WorkspaceLayoutMode.Compact)]
    public async Task FailedAndCanceledPrivateOperationsPreserveInputAndOfferReachableRecovery(WorkspaceLayoutMode mode)
    {
        Definition definition = CreateRecoveryDefinition(multiArtifact: true, withGeneratedSecret: true);
        await using var context = BrowserFixture.Context();
        var artifactsModule = context.JSInterop.SetupModule("./js/browser-artifacts.js");
        artifactsModule.Setup<bool>("copy", _ => true).SetResult(false);

        var editor = context.Render<ConfigurationEditor>(parameters => parameters
            .Add(item => item.Definition, definition));
        if (mode == WorkspaceLayoutMode.Compact)
            await editor.Find("[data-layout-mode='compact']").ClickAsync(new MouseEventArgs());

        await editor.Find("[data-field='hostname'] input").InputAsync(new ChangeEventArgs { Value = "preserved.example.test" });
        await editor.Find("[data-field='port'] input").InputAsync(new ChangeEventArgs { Value = "not-a-number" });

        // 1. Preparation failure (`input-type-invalid`): preserves input and offers direct correction recovery.
        await BrowserFixture.SelectPanelAsync(editor, "prepare");
        await editor.Find("#prepare-complete").ClickAsync(new MouseEventArgs());
        await Assert.That(editor.Find("[role='status'][data-code='input-type-invalid']")).IsNotNull();
        var correctionRecovery = editor.Find("[data-action='recover-status']");
        await Assert.That(correctionRecovery.GetAttribute("data-recovery-target")).IsEqualTo("correction");
        await correctionRecovery.ClickAsync(new MouseEventArgs());
        await Assert.That(editor.FindAll("[data-compact-workspace]")).Count().IsEqualTo(mode == WorkspaceLayoutMode.Compact ? 1 : 0);
        var form = editor.FindComponents<ConfigurationField>()[0].Instance.Form;
        await Assert.That(form.Snapshot().Values["hostname"].GetString()).IsEqualTo("preserved.example.test");

        // Fix port so valid preparation succeeds.
        await editor.Find("[data-field='port'] input").InputAsync(new ChangeEventArgs { Value = "443" });

        // 2. Import consent refusal then oversized import file: both preserve hostname and offer reachable recovery.
        var importFile = editor.FindComponent<InputFile>();
        await editor.InvokeAsync(() => importFile.UploadFiles(
            InputFileContent.CreateFromBinary(Encoding.UTF8.GetBytes("{}"), "small.json")));
        await Assert.That(editor.Find("[role='status'][data-code='import-secret-consent-required']")).IsNotNull();
        var consentRecovery = editor.Find("[data-action='recover-status']");
        await Assert.That(consentRecovery.GetAttribute("data-recovery-target")).IsEqualTo("consent");
        await consentRecovery.ClickAsync(new MouseEventArgs());
        await editor.Find("#secret-consent").ChangeAsync(new ChangeEventArgs { Value = true });

        await editor.Find("#import-artifact").ChangeAsync(new ChangeEventArgs { Value = "out-config.json" });
        importFile = editor.FindComponent<InputFile>();
        await editor.InvokeAsync(() => importFile.UploadFiles(
            InputFileContent.CreateFromBinary(new byte[1024 * 1024 + 16], "too-large.json")));
        await Assert.That(editor.Find("[role='status'][data-code='import-too-large']")).IsNotNull();
        await Assert.That(editor.Find("[data-action='recover-status']").GetAttribute("data-recovery-target")).IsEqualTo("import");
        await Assert.That(form.Snapshot().Values["hostname"].GetString()).IsEqualTo("preserved.example.test");

        // 3. Aggregate export failure: preserves prepared artifacts and offers direct retry action without losing answers.
        await BrowserFixture.SelectPanelAsync(editor, "prepare");
        await editor.Find("#prepare-draft").ClickAsync(new MouseEventArgs());
        await editor.Find("[data-action='copy-all-files']").ClickAsync(new MouseEventArgs());
        await Assert.That(editor.Find("[role='status'][data-code='aggregate-export-unavailable']")).IsNotNull();
        var retryExport = editor.Find("[data-action='recover-status']");
        await Assert.That(retryExport.GetAttribute("data-recovery-target")).IsEqualTo("retry-aggregate-export");
        artifactsModule.Setup<bool>("copy", _ => true).SetResult(true);
        await retryExport.ClickAsync(new MouseEventArgs());
        await Assert.That(editor.Find("[role='status']").GetAttribute("data-code")).IsEqualTo("aggregate-copied");

        // 4. Secret value preservation across recovery.
        await BrowserFixture.SelectPanelAsync(editor, "configure");
        await editor.Find("[data-field='token'] input").InputAsync(new ChangeEventArgs { Value = "synthetic-existing-secret" });
        await Assert.That(form.Snapshot().Values["hostname"].GetString()).IsEqualTo("preserved.example.test");
        await Assert.That(form.Snapshot().Values["token"].GetString()).IsEqualTo("synthetic-existing-secret");
    }

    [Test]
    public async Task CanceledSecretGenerationPreservesExistingSecretAndOffersRetryWithoutFalseFailure()
    {
        Definition definition = CreateRecoveryDefinition(withGeneratedSecret: true);
        await using var context = BrowserFixture.Context();
        using var boundary = new RecoveryJsBoundary(context.JSInterop.JSRuntime);
        context.Services.AddSingleton<IJSRuntime>(boundary);

        using var form = new DefinitionFormModel(definition, DefinitionSessionPrivacy.BrowserSecretsEnabled);
        form.EditValue(["hostname"], "secret-test.example");
        form.EditValue(["token"], "synthetic-existing-secret");

        var component = context.Render<ConfigurationSecretActions>(parameters => parameters
            .Add(item => item.Form, form)
            .Add(item => item.Schema, definition.Configuration.Properties["token"])
            .Add(item => item.Location, DefinitionPath.FromSegments(["token"]))
            .Add(item => item.ControlId, form.GetEditorIdentity(["token"]))
            .Add(item => item.Title, "API token")
            .Add(item => item.Value, "synthetic-existing-secret"));

        boundary.HoldMountSteps = true;
        Task generating = component.Find("[data-secret-action='generate']").ClickAsync(new MouseEventArgs());
        await boundary.MountStepEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await component.Find("[data-secret-action='cancel']").ClickAsync(new MouseEventArgs());
        boundary.ReleaseAllHeldSteps();
        await generating.WaitAsync(TimeSpan.FromSeconds(5));

        var status = component.Find("[data-secret-status='secret-cancelled']");
        await Assert.That(status.TextContent).IsEqualTo(BrowserStrings.Get("SecretCancelled", "en"));
        await Assert.That(form.Snapshot().Values["token"].GetString()).IsEqualTo("synthetic-existing-secret");
        await Assert.That(component.Find("[data-secret-action='retry']")).IsNotNull();
        await Assert.That(component.Find("[data-operation-phase='private-secret']")).IsNotNull();
    }

    [Test]
    public async Task HeldStartupImportAndExportCannotPublishAfterCancellationOrReplacement()
    {
        Definition first = CreateRecoveryDefinition();
        Definition second = CreateRecoveryDefinition();
        await using var context = BrowserFixture.Context();
        using var boundary = new RecoveryJsBoundary(context.JSInterop.JSRuntime);
        context.Services.AddSingleton<IJSRuntime>(boundary);

        // Held startup before replacement cannot mutate or leave old form active.
        var component = context.Render<ConfigurationEditor>(parameters => parameters
            .Add(item => item.Definition, first));
        await boundary.StartupRenderEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        component.Render(parameters => parameters.Add(item => item.Definition, second));
        await boundary.ReleaseStartupFrameAsync(component);
        await boundary.DrainAllMountsAsync(component);
        DefinitionFormModel currentForm = component.FindComponents<ConfigurationField>()[0].Instance.Form;
        await Assert.That(currentForm.Definition.FamilyId).IsEqualTo(second.FamilyId);

        // Held import stream cancelled by leaving Configure cannot alter typed answers.
        await component.Find("[data-field='hostname'] input").InputAsync(new ChangeEventArgs { Value = "kept.example.test" });
        await component.Find("#secret-consent").ChangeAsync(new ChangeEventArgs { Value = true });
        long revision = currentForm.InputRevision;
        using var heldStream = new HeldReadStream();
        Task importing = component.InvokeAsync(() => component.Instance.LoadAsync(heldStream));
        await heldStream.ReadEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await BrowserFixture.SelectPanelAsync(component, "prepare");
        heldStream.Release.TrySetResult();
        await importing.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(currentForm.Snapshot().Values["hostname"].GetString()).IsEqualTo("kept.example.test");
        await Assert.That(currentForm.InputRevision).IsEqualTo(revision);
        await BrowserFixture.SelectPanelAsync(component, "configure");

        // Held export after edit invalidation cannot publish stale copied state.
        await component.Find("[data-field='port'] input").InputAsync(new ChangeEventArgs { Value = "8080" });
        await BrowserFixture.SelectPanelAsync(component, "prepare");
        await component.Find("#prepare-draft").ClickAsync(new MouseEventArgs());
        var copyStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var copyRelease = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        boundary.OnCopy = () =>
        {
            copyStarted.TrySetResult();
            return copyRelease.Task;
        };
        Task copying = component.Find("[data-action='copy-file']").ClickAsync(new MouseEventArgs());
        await copyStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await BrowserFixture.SelectPanelAsync(component, "configure");
        await component.Find("[data-field='hostname'] input").InputAsync(new ChangeEventArgs { Value = "changed.example.test" });
        copyRelease.SetResult(true);
        await copying.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.That(component.Find("[role='status']").GetAttribute("data-code")).IsEqualTo("answers-changed");
        await Assert.That(currentForm.OutputWorkflow.Tasks.SelectMany(task => task.Deliveries)).IsEmpty();
    }

    [Test]
    public async Task WaitingServiceWorkerUpdateNeverInterruptsOrReloadsActiveWorkspace()
    {
        using var context = new BunitContext();
        var module = context.JSInterop.SetupModule("./pwa-lifecycle.js");
        module.Setup<BrowserOfflineState>("initialize").SetResult(new(true, true, true, UpdateWaiting: true));
        module.Setup<long>("subscribeConnectivity", _ => true).SetResult(1L);
        module.SetupVoid("unsubscribeConnectivity", 1L).SetVoidResult();
        module.Setup<bool>("beginWorkspace", 1L).SetResult(true);
        module.SetupVoid("endWorkspace", 2L).SetVoidResult();

        await using var policy = new BrowserOfflinePolicy(context.JSInterop.JSRuntime);
        await policy.InitializeAsync();
        await Assert.That(policy.UpdateWaiting).IsTrue();
        await Assert.That(await policy.BeginWorkspaceAsync()).IsTrue();
        await Assert.That(policy.IsWorkspaceActive).IsTrue();
        await Assert.That(context.JSInterop.Invocations.Any(call =>
            call.Identifier.Contains("skipWaiting", StringComparison.OrdinalIgnoreCase) ||
            call.Identifier.Contains("reload", StringComparison.OrdinalIgnoreCase))).IsFalse();
        await policy.EndWorkspaceAsync();
    }

    [Test]
    public async Task DirectoryUnavailableOffersKeyboardRetryAndRecoversWithoutLosingBundledDefinitions()
    {
        await using var context = BrowserFixture.Context();
        bool shouldFail = true;
        Definition recoveryProject = CreateRecoveryDefinition();
        var storeProject = new DefinitionStoreProject
        {
            ProjectId = "recovery.app",
            Name = "Recovery Store Project",
            Description = "Public metadata",
            SourceRepository = "https://publisher.example/recovery.app",
            License = "MIT",
            Tags = ["directory"],
            ApplicationId = recoveryProject.ApplicationId,
            FamilyId = recoveryProject.FamilyId,
            Revisions =
            [
                new DefinitionStoreRevision
                {
                    Revision = recoveryProject.Revision,
                    Digest = recoveryProject.Digest.Value,
                    Publisher = recoveryProject.Publisher,
                    Title = recoveryProject.Title,
                    ApplicationVersions = recoveryProject.ApplicationVersions,
                    EntryPoints = recoveryProject.EntryPoints,
                    Locales = recoveryProject.Locales,
                    OutputNames = recoveryProject.Artifacts.Select(artifact => artifact.FileName).ToArray(),
                    SnapshotPath = $"definitions/{recoveryProject.ApplicationId}/{recoveryProject.Revision}.json"
                }
            ]
        };

        context.Services.AddSingleton<IDefinitionStoreProvider>(new TestDefinitionStoreProvider(
            load: _ => shouldFail
                ? Task.FromException<IReadOnlyList<DefinitionStoreProject>>(new DefinitionStoreException("store-unavailable"))
                : Task.FromResult<IReadOnlyList<DefinitionStoreProject>>([storeProject])));

        var page = context.Render<Catalogue>();
        await Assert.That(page.Find("[data-directory-state]").GetAttribute("data-directory-state")).IsEqualTo("unavailable");
        await Assert.That(page.FindAll("[data-catalogue-entry]")).Count().IsGreaterThan(0);

        shouldFail = false;
        await page.Find("[data-action='retry-directory']").ClickAsync(new MouseEventArgs());
        await Assert.That(page.Find("[data-directory-state]").GetAttribute("data-directory-state")).IsEqualTo("ready");
        await Assert.That(page.Find("[data-project='recovery.app']")).IsNotNull();
    }

    private sealed class RecoveryJsBoundary : IJSRuntime, IDisposable
    {
        private readonly IJSRuntime _fallback;
        private readonly RenderingModule _rendering;
        private readonly OfflineModule _offline;
        private readonly ArtifactsModule _artifacts;
        private readonly Channel<TaskCompletionSource> _heldStartupSteps = Channel.CreateUnbounded<TaskCompletionSource>();
        private readonly Channel<TaskCompletionSource> _heldMountSteps = Channel.CreateUnbounded<TaskCompletionSource>();
        private readonly List<TaskCompletionSource> _allHeld = [];

        internal BrowserOfflineState OfflineState { get; set; } = new(true, true, true);
        internal bool AcceptWorkspace { get; set; } = true;
        internal bool HoldBeginWorkspace { get; set; }
        internal bool HoldMountSteps { get; set; } = true;
        internal Func<Task<bool>>? OnCopy { get; set; }
        internal TaskCompletionSource StartupRenderEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource MountStepEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal RecoveryJsBoundary(IJSRuntime fallback)
        {
            _fallback = fallback;
            _rendering = new RenderingModule(this);
            _offline = new OfflineModule(this);
            _artifacts = new ArtifactsModule(this);
        }

        internal void ReleaseStartupFrame()
        {
            if (_heldStartupSteps.Reader.TryRead(out var step))
                step.TrySetResult();
        }

        internal async Task ReleaseStartupFrameAsync<T>(IRenderedComponent<T> component) where T : class, IComponent
        {
            await StartupRenderEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            while (_heldStartupSteps.Reader.TryRead(out var step))
                await component.InvokeAsync(() => step.TrySetResult());
        }

        internal Task ReleaseMountStep()
        {
            if (_heldMountSteps.Reader.TryRead(out var step)) step.TrySetResult();
            return Task.CompletedTask;
        }

        internal void ReleaseAllHeldSteps()
        {
            HoldMountSteps = false;
            while (_heldStartupSteps.Reader.TryRead(out var startup)) startup.TrySetResult();
            while (_heldMountSteps.Reader.TryRead(out var step)) step.TrySetResult();
            foreach (var step in _allHeld) step.TrySetResult();
        }

        internal async Task DrainAllMountsAsync<T>(IRenderedComponent<T> component) where T : class, IComponent
        {
            HoldMountSteps = false;
            while (_heldStartupSteps.Reader.TryRead(out var startup))
                await component.InvokeAsync(() => startup.TrySetResult());
            while (_heldMountSteps.Reader.TryRead(out var step))
                await component.InvokeAsync(() => step.TrySetResult());
            await component.InvokeAsync(() =>
            {
                ReleaseAllHeldSteps();
                return Task.CompletedTask;
            });
        }

        internal Task DrainAllMounts()
        {
            ReleaseAllHeldSteps();
            return Task.CompletedTask;
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public async ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            if (identifier == "import" && args?.Length > 0)
            {
                string? path = args[0]?.ToString();
                if (path == "./js/configuration-rendering.js") return (TValue)(object)_rendering;
                if (path == "./pwa-lifecycle.js") return (TValue)(object)_offline;
                if (path == "./js/browser-artifacts.js") return (TValue)(object)_artifacts;
            }
            return await _fallback.InvokeAsync<TValue>(identifier, cancellationToken, args);
        }

        public void Dispose() => ReleaseAllHeldSteps();

        private async Task WaitStepAsync(bool isStartup, CancellationToken token)
        {
            if (!isStartup && !HoldMountSteps) return;
            var tcs = new TaskCompletionSource();
            _allHeld.Add(tcs);
            (isStartup ? _heldStartupSteps : _heldMountSteps).Writer.TryWrite(tcs);
            if (isStartup) StartupRenderEntered.TrySetResult();
            else MountStepEntered.TrySetResult();
            using CancellationTokenRegistration registration = token.Register(() => tcs.TrySetCanceled(token));
            await tcs.Task;
        }

        private sealed class RenderingModule(RecoveryJsBoundary owner) : IJSObjectReference
        {
            public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
                InvokeAsync<TValue>(identifier, CancellationToken.None, args);

            public async ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            {
                if (identifier == "paintWorkspace")
                    await owner.WaitStepAsync(isStartup: true, cancellationToken);
                else if (identifier is "nextFrame" or "nextTask")
                    await owner.WaitStepAsync(isStartup: false, cancellationToken);
                return default!;
            }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }

        private sealed class OfflineModule(RecoveryJsBoundary owner) : IJSObjectReference
        {
            public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
                InvokeAsync<TValue>(identifier, CancellationToken.None, args);

            public async ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            {
                if (identifier == "initialize")
                    return (TValue)(object)owner.OfflineState;
                if (identifier == "beginWorkspace")
                {
                    if (owner.HoldBeginWorkspace)
                    {
                        var wait = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                        using var _ = cancellationToken.Register(() => wait.TrySetCanceled(cancellationToken));
                        await wait.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
                    }
                    return (TValue)(object)owner.AcceptWorkspace;
                }
                return default!;
            }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }

        private sealed class ArtifactsModule(RecoveryJsBoundary owner) : IJSObjectReference
        {
            public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
                InvokeAsync<TValue>(identifier, CancellationToken.None, args);

            public async ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            {
                if (identifier == "copy" && owner.OnCopy is not null)
                    return (TValue)(object)await owner.OnCopy();
                if (identifier is "copy" or "download" or "reveal")
                    return (TValue)(object)true;
                return default!;
            }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    private sealed class HeldReadStream : Stream
    {
        internal TaskCompletionSource ReadEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _reads;
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => 128;
        public override long Position { get; set; }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => 0;
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _reads) == 1)
            {
                ReadEntered.TrySetResult();
                await Release.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
            }
            byte[] payload = "{\"hostname\":\"stale-import\"}"u8.ToArray();
            if (Position >= payload.Length) return 0;
            int slice = (int)Math.Min(buffer.Length, payload.Length - Position);
            payload.AsSpan((int)Position, slice).CopyTo(buffer.Span);
            Position += slice;
            return slice;
        }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
