namespace Configurator.Browser.Tests;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AngleSharp.Dom;
using Bunit;
using Configurator.Browser.Services;
using Configurator.Core.Definitions;
using Configurator.Presentation;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

public sealed class BrowserEventManifestWorkflowTests
{
    private static readonly Guid EventFamilyId = Guid.Parse("f79259e8-af23-49b0-8d8d-6e371cc96080");
    private static readonly Guid GrafanaFamilyId = Guid.Parse("c9091324-8fc2-42ed-a659-95b708d4c0a4");
    private static readonly Guid SecretGenerationFamilyId = Guid.Parse("b2bed5a4-def5-4818-8cfa-1a5bd0ffc43c");
    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(2)]
    [Arguments(3)]
    public async Task CompleteEventManifestContainsExactlyTypedTenantRowsAndNoInactiveArtifacts(int count)
    {
        var definition = BrowserDefinitionRegistry.LoadBundled().Current.Resolve(Guid.Parse("f79259e8-af23-49b0-8d8d-6e371cc96080"));
        await using var context = BrowserFixture.Context();
        var module = context.JSInterop.SetupModule("./js/browser-artifacts.js");
        module.Setup<bool>("copy", _ => true).SetResult(true);
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, definition));
        await BrowserFixture.NotifyChoiceAsync(component, "operation", "configuration-manifest");
        await component.Find("#configuration-detail").ChangeAsync(new ChangeEventArgs { Value = "guided" });
        await component.Find("[data-field='configurationManifest.metadata.name'] input").InputAsync(new ChangeEventArgs { Value = "qa-config" });
        await component.Find("[data-field='configurationManifest.spec.instance.settings'] > fieldset > button").ClickAsync(new MouseEventArgs());
        await Assert.That(component.Find("[data-field='configurationManifest.spec.instance.settings.modules.tech_enabled'] input").GetAttribute("type")).IsEqualTo("password");
        await component.Find("[data-field='configurationManifest.spec.instance.settings.modules.tech_enabled'] input").InputAsync(new ChangeEventArgs { Value = "true" });
        var tenants = new JsonArray();
        if (count == 0) await component.Find("[data-field='configurationManifest.tenants'] > fieldset > button[data-action='supply-list']").ClickAsync(new MouseEventArgs());
        for (int index = 0; index < count; index++)
        {
            await component.Find("[data-field='configurationManifest.tenants'] > fieldset > button[data-action='add-item']").ClickAsync(new MouseEventArgs());
            await component.Find($"[data-field='configurationManifest.tenants.{index}.metadata.name'] input").InputAsync(new ChangeEventArgs { Value = "tenant-" + index });
            await component.Find($"[data-field='configurationManifest.tenants.{index}.spec.displayName'] input").InputAsync(new ChangeEventArgs { Value = "Tenant " + index });
            foreach (string name in new[] { "settings", "documents", "legalDocuments" })
                await component.Find($"[data-field='configurationManifest.tenants.{index}.spec.{name}'] > fieldset > button[data-action='supply-object']").ClickAsync(new MouseEventArgs());
            tenants.Add(new JsonObject
            {
                ["metadata"] = new JsonObject { ["name"] = "tenant-" + index },
                ["spec"] = new JsonObject
                {
                    ["displayName"] = "Tenant " + index,
                    ["settings"] = new JsonObject(),
                    ["documents"] = new JsonObject(),
                    ["legalDocuments"] = new JsonObject()
                }
            });
        }
        await BrowserFixture.PrepareAsync(component, draft: false);
        await Assert.That(component.FindAll(".configuration-editor__artifact")).Count().IsEqualTo(1);
        await Assert.That(component.Find(".configuration-editor__artifact .configuration-files__heading").TextContent).IsEqualTo("configuration-manifest.json");
        await component.Find(".configuration-editor__artifact [data-action='copy-file']").ClickAsync(new MouseEventArgs());
        byte[] bytes = (byte[])module.Invocations.Single(call => call.Identifier == "open").Arguments[2]!;
        using JsonDocument actual = JsonDocument.Parse(bytes);
        JsonNode expected = JsonNode.Parse("""
            {"$schema":"https://schemas.islamu.org/event/configuration-manifest/v1alpha2/schema.json",
             "apiVersion":"configuration.islamu.org/v1alpha2","kind":"ConfigurationManifest","metadata":{"name":"qa-config"},
             "spec":{"instance":{"settings":{"modules.tech_enabled":true},"documents":{},"legalDocuments":{}},"tenants":[]}}
            """)!;
        expected["spec"]!["tenants"] = tenants;
        await Assert.That(JsonElement.DeepEquals(actual.RootElement, JsonSerializer.SerializeToElement(expected))).IsTrue();
    }

    [Test]
    [Arguments("en")]
    [Arguments("ar")]
    public async Task EventRevision4BilingualWorkbenchExercisesGuidedCompactUndoDocumentReviewAndFiles(string locale)
    {
        Definition definition = LoadRepositoryDefinition("store/islamu-event/releases/4.json");
        await Assert.That(definition.Revision).IsEqualTo(4);
        await Assert.That(definition.FamilyId).IsEqualTo(EventFamilyId);

        var clock = new ManualClock();
        await using var context = BrowserFixture.Context(new BrowserDefinitionRegistry([definition]));
        context.Services.AddSingleton<TimeProvider>(clock);
        await using var developerBridge = new DeveloperBridge(context.JSInterop.JSRuntime);
        context.Services.AddSingleton<IJSRuntime>(developerBridge);
        var artifacts = context.JSInterop.SetupModule("./js/browser-artifacts.js");
        artifacts.Setup<bool>("copy", _ => true).SetResult(true);

        var layout = BrowserFixture.RenderWorkspace(context, definition);
        var editor = layout.FindComponent<ConfigurationEditor>();
        var form = layout.FindComponents<ConfigurationField>()[0].Instance.Form;

        await layout.InvokeAsync(() => form.SelectLocale(locale));
        await Assert.That(form.Locale).IsEqualTo(locale);
        await Assert.That(layout.Find(".configuration-editor").GetAttribute("dir"))
            .IsEqualTo(locale == "ar" ? "rtl" : "ltr");

        // Verify canonical section and pointer-prefix ownership without Event-specific branching.
        IElement basicsTab = BrowserFixture.SectionNavigation(context, layout).Find("[data-section='basics']");
        await Assert.That(basicsTab.TextContent)
            .Contains(locale == "ar" ? "الإعدادات الأساسية" : "Essentials");
        DefinitionFormSection basicsSection = form.Projection.Sections.Single(section => section.Id == "basics");
        DefinitionFormSection manifestSection = form.Projection.Sections.Single(section => section.Id == "manifest");
        await Assert.That(basicsSection.Fields).Contains("apiEndpoint");
        await Assert.That(basicsSection.Fields).Contains("platform");
        await Assert.That(manifestSection.Fields).Contains("configurationManifest");

        // Guided editing + Undo / Redo across an active deployment context.
        await layout.Find("[data-context-pair='separate-images:all']").ClickAsync(new MouseEventArgs());
        await BrowserFixture.SectionNavigation(context, layout).Find("[data-section='basics']").ClickAsync(new MouseEventArgs());
        await layout.Find("[data-field='apiEndpoint'] input").InputAsync(new ChangeEventArgs { Value = "https://api-one.example.org" });
        await layout.Find("[data-field='apiEndpoint'] input").InputAsync(new ChangeEventArgs { Value = "https://api-two.example.org" });
        await Assert.That(form.Snapshot().Values["apiEndpoint"].GetString()).IsEqualTo("https://api-two.example.org");

        await layout.Find("[data-action='undo']").ClickAsync(new MouseEventArgs());
        await Assert.That(form.Snapshot().Values["apiEndpoint"].GetString()).IsEqualTo("https://api-one.example.org");
        await layout.Find("[data-action='redo']").ClickAsync(new MouseEventArgs());
        await Assert.That(form.Snapshot().Values["apiEndpoint"].GetString()).IsEqualTo("https://api-two.example.org");

        // Compact table round-trip without prematurely unlocking secrets.
        await layout.Find("[data-layout-mode='compact']").ClickAsync(new MouseEventArgs());
        await Assert.That(layout.FindAll("[data-compact-table]").Count).IsEqualTo(1);
        await Assert.That(form.SessionPrivacy).IsEqualTo(DefinitionSessionPrivacy.BrowserNoSecrets);
        await layout.Find("[data-compact-search]").InputAsync(new ChangeEventArgs { Value = "API" });
        await clock.AdvanceAsync(TimeSpan.FromMilliseconds(150), editor);
        await Assert.That(layout.FindAll("[data-compact-row='apiEndpoint']").Count).IsEqualTo(1);

        await layout.Find("[data-layout-mode='guided']").ClickAsync(new MouseEventArgs());
        await Assert.That(layout.Find("[data-field='apiEndpoint'] input").GetAttribute("value"))
            .IsEqualTo("https://api-two.example.org");

        // Document (JSON) preview & apply inside developer mode.
        await layout.Find("#secret-consent").ChangeAsync(new ChangeEventArgs { Value = true });
        await layout.Find("[data-editing-mode='developer']").ClickAsync(new MouseEventArgs());
        var developer = layout.FindComponent<ConfigurationDeveloper>();
        await developerBridge.Mounted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        JsonNode candidate = JsonNode.Parse(developerBridge.CurrentText)!;
        candidate["values"]!["apiEndpoint"] = "https://api-doc.example.org";
        await developer.InvokeAsync(() => developerBridge.ChangeAsync(candidate.ToJsonString()));
        await developer.Find("[data-action='developer-preview']").ClickAsync(new MouseEventArgs());
        await developer.Find("[data-action='developer-apply']").ClickAsync(new MouseEventArgs());
        await layout.Find("[data-editing-mode='form']").ClickAsync(new MouseEventArgs());
        await Assert.That(form.Snapshot().Values["apiEndpoint"].GetString()).IsEqualTo("https://api-doc.example.org");

        // Switch to configuration-manifest, complete required manifest entries, review and prepare file.
        await BrowserFixture.NotifyChoiceAsync(layout, "operation", "configuration-manifest");
        await layout.Find("#configuration-detail").ChangeAsync(new ChangeEventArgs { Value = "guided" });
        await BrowserFixture.SectionNavigation(context, layout).Find("[data-section='manifest']").ClickAsync(new MouseEventArgs());
        await layout.Find("[data-field='configurationManifest.metadata.name'] input")
            .InputAsync(new ChangeEventArgs { Value = "qa-rev4-" + locale });
        await layout.Find("[data-field='configurationManifest.spec.instance.settings'] > fieldset > button")
            .ClickAsync(new MouseEventArgs());
        await layout.Find("[data-field='configurationManifest.spec.instance.settings.modules.tech_enabled'] input")
            .InputAsync(new ChangeEventArgs { Value = "true" });
        await layout.Find("[data-field='configurationManifest.tenants'] > fieldset > button[data-action='supply-list']")
            .ClickAsync(new MouseEventArgs());
        await BrowserFixture.SelectPanelAsync(layout, "review");
        await Assert.That(layout.FindAll("[data-workflow-panel='review']").Count).IsEqualTo(1);

        await BrowserFixture.PrepareAsync(layout, draft: false);
        await Assert.That(layout.FindAll(".configuration-editor__artifact").Count).IsEqualTo(1);
        await Assert.That(layout.Find(".configuration-editor__artifact .configuration-files__heading").TextContent)
            .IsEqualTo("configuration-manifest.json");
    }

    [Test]
    public async Task NonEventGrafanaAndSecretGenerationExamplesRunWithZeroEventBranches()
    {
        Definition grafana = LoadRepositoryDefinition("examples/grafana-13.2.2/setup-definition.json");
        await Assert.That(grafana.FamilyId).IsEqualTo(GrafanaFamilyId);
        await Assert.That(grafana.Deployment).IsNull();

        var clock = new ManualClock();
        await using var grafanaContext = BrowserFixture.Context(new BrowserDefinitionRegistry([grafana]));
        grafanaContext.Services.AddSingleton<TimeProvider>(clock);
        var grafanaLayout = BrowserFixture.RenderWorkspace(grafanaContext, grafana);
        var grafanaEditor = grafanaLayout.FindComponent<ConfigurationEditor>();
        var grafanaForm = grafanaLayout.FindComponents<ConfigurationField>()[0].Instance.Form;

        var nav = BrowserFixture.SectionNavigation(grafanaContext, grafanaLayout);
        await Assert.That(nav.FindAll("[data-section='server']").Count).IsEqualTo(1);
        await Assert.That(nav.FindAll("[data-section='datasource']").Count).IsEqualTo(1);
        await Assert.That(nav.FindAll("[data-section='basics']")).IsEmpty();
        await Assert.That(grafanaLayout.FindAll("#configuration-output")).IsEmpty();
        await Assert.That(grafanaLayout.FindAll("[data-workspace-scope]")).IsEmpty();

        await grafanaLayout.Find("[data-field='server.domain'] input").InputAsync(new ChangeEventArgs { Value = "grafana-1.example.org" });
        await grafanaLayout.Find("[data-field='server.domain'] input").InputAsync(new ChangeEventArgs { Value = "grafana-2.example.org" });
        await grafanaLayout.Find("[data-action='undo']").ClickAsync(new MouseEventArgs());
        await Assert.That(grafanaForm.Snapshot().Values["server"].GetProperty("domain").GetString()).IsEqualTo("grafana-1.example.org");
        await grafanaLayout.Find("[data-action='redo']").ClickAsync(new MouseEventArgs());

        await grafanaLayout.Find("[data-layout-mode='compact']").ClickAsync(new MouseEventArgs());
        await grafanaLayout.Find("[data-compact-search]").InputAsync(new ChangeEventArgs { Value = "domain" });
        await clock.AdvanceAsync(TimeSpan.FromMilliseconds(150), grafanaEditor);
        await Assert.That(grafanaLayout.FindAll("[data-compact-row='server.domain']").Count).IsEqualTo(1);
        await grafanaLayout.Find("[data-layout-mode='guided']").ClickAsync(new MouseEventArgs());

        await grafanaLayout.Find("#configuration-detail").ChangeAsync(new ChangeEventArgs { Value = "guided" });
        await BrowserFixture.SectionNavigation(grafanaContext, grafanaLayout).Find("[data-section='datasource']").ClickAsync(new MouseEventArgs());
        await grafanaLayout.Find("[data-field='datasource.name'] input").InputAsync(new ChangeEventArgs { Value = "Graphite" });
        await grafanaLayout.Find("[data-field='datasource.url'] input").InputAsync(new ChangeEventArgs { Value = "https://graphite.example.org" });
        await BrowserFixture.PrepareAsync(grafanaLayout, draft: false);
        await Assert.That(grafanaLayout.FindAll("[data-prepared-file='environment']").Count).IsEqualTo(1);
        await Assert.That(grafanaLayout.FindAll("[data-prepared-file='datasource']").Count).IsEqualTo(1);

        // Modal secret generator runs generically for any definition declaring local generation policy.
        Definition secretGen = LoadRepositoryDefinition("examples/secret-generation/setup-definition.json");
        await Assert.That(secretGen.FamilyId).IsEqualTo(SecretGenerationFamilyId);
        await using var secretContext = BrowserFixture.Context(new BrowserDefinitionRegistry([secretGen]));
        var secretLayout = BrowserFixture.RenderWorkspace(secretContext, secretGen);
        var secretForm = secretLayout.FindComponents<ConfigurationField>()[0].Instance.Form;
        await secretLayout.Find("#secret-consent").ChangeAsync(new ChangeEventArgs { Value = true });
        await secretLayout.Find("[data-field='password'] [data-secret-action='open-generator']").ClickAsync(new MouseEventArgs());
        await Assert.That(secretLayout.FindAll("[data-secret-generator-dialog]").Count).IsEqualTo(1);
        await secretLayout.Find("[data-secret-generator-generate]").ClickAsync(new MouseEventArgs());
        string preview = secretLayout.Find("[data-secret-generator-preview]").GetAttribute("value")!;
        await Assert.That(preview.Length).IsEqualTo(32);
        await Assert.That(secretForm.Snapshot().Values.ContainsKey("password")).IsFalse();
        await secretLayout.Find("[data-secret-generator-apply]").ClickAsync(new MouseEventArgs());
        await Assert.That(secretForm.Snapshot().Values["password"].GetString()).IsEqualTo(preview);
        await Assert.That(secretForm.AnswerOrigin(["password"])).IsEqualTo(DefinitionAnswerOrigin.Generated);
    }

    private static Definition LoadRepositoryDefinition(string relativePath)
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Configurator.slnx")))
            current = current.Parent;
        if (current is null) throw new InvalidOperationException("Repository root not found.");
        string fullPath = Path.Combine(current.FullName, relativePath);
        return DefinitionCodec.ParseJson(File.ReadAllBytes(fullPath));
    }

    private sealed class ManualClock : TimeProvider
    {
        private DateTimeOffset _utcNow = new(2026, 11, 13, 10, 0, 0, TimeSpan.Zero);
        private readonly List<ManualTimer> _timers = [];

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new ManualTimer(callback, state, dueTime, _utcNow);
            lock (_timers) _timers.Add(timer);
            return timer;
        }

        public async Task AdvanceAsync(TimeSpan delta, IRenderedComponent<ConfigurationEditor> host)
        {
            _utcNow += delta;
            List<ManualTimer> due = [];
            lock (_timers)
            {
                foreach (ManualTimer timer in _timers)
                    if (timer.IsDue(_utcNow)) due.Add(timer);
            }
            if (due.Count == 0) return;
            await host.InvokeAsync(() =>
            {
                foreach (ManualTimer timer in due) timer.Fire();
            });
        }

        private sealed class ManualTimer(TimerCallback callback, object? state, TimeSpan dueTime, DateTimeOffset createdAt) : ITimer
        {
            private DateTimeOffset? _dueAt = dueTime == Timeout.InfiniteTimeSpan ? null : createdAt + dueTime;
            private bool _disposed;
            public bool Change(TimeSpan dueTime, TimeSpan period)
            {
                if (_disposed) return false;
                _dueAt = dueTime == Timeout.InfiniteTimeSpan ? null : DateTimeOffset.UtcNow;
                return true;
            }
            public bool IsDue(DateTimeOffset now) => !_disposed && _dueAt is { } at && now >= at;
            public void Fire()
            {
                if (_disposed) return;
                _dueAt = null;
                callback(state);
            }
            public void Dispose() => _disposed = true;
            public ValueTask DisposeAsync()
            {
                _disposed = true;
                return ValueTask.CompletedTask;
            }
        }
    }

    private sealed class DeveloperBridge(IJSRuntime fallback) : IJSRuntime, IJSInProcessObjectReference
    {
        private byte[] _source = [];
        private ConfigurationDeveloper? _owner;
        private long _generation;
        private long _version;
        private bool _active;

        internal TaskCompletionSource Mounted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal string CurrentText => Encoding.UTF8.GetString(_source);

        internal Task ChangeAsync(string text)
        {
            if (!_active || _owner is null) throw new InvalidOperationException("editor-retired");
            CryptographicOperations.ZeroMemory(_source);
            _source = Encoding.UTF8.GetBytes(text);
            _version++;
            return _owner.OnEditorChangedAsync(_generation, _version, true, false);
        }

        public TValue Invoke<TValue>(string identifier, params object?[]? args)
        {
            if (identifier == "retire" && args is not null && (long)args[0]! >= _generation) Retire();
            return default!;
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public async ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            if (identifier == "import" && args?[0]?.ToString() is not ("./js/configuration-developer.js" or "./js/configuration-rendering.js"))
                return await fallback.InvokeAsync<TValue>(identifier, cancellationToken, args);

            object? result = null;
            if (identifier is "import" or "createBridge") result = this;
            else if (identifier == "mount")
            {
                _generation = (long)args![0]!;
                _owner = ((DotNetObjectReference<ConfigurationDeveloper>)args[2]!).Value;
                var options = (ConfigurationDeveloper.EditorOptions)args[3]!;
                CryptographicOperations.ZeroMemory(_source);
                _source = options.Source.ToArray();
                _version = 1;
                _active = true;
                result = _version;
                Mounted.TrySetResult();
            }
            else if (identifier == "read") result = _source.ToArray();
            else if (identifier == "markApplied" || identifier == "freeze" && args?[2] is true) result = true;
            else if (identifier == "replace")
            {
                byte[] replacement = (byte[])args![2]!;
                CryptographicOperations.ZeroMemory(_source);
                _source = replacement.ToArray();
                _version++;
                result = new ConfigurationDeveloper.EditorState(_version, true, false);
            }
            else if (identifier == "retire" && args is not null && (long)args[0]! >= _generation) Retire();
            return result is null ? default! : (TValue)result;
        }

        private void Retire()
        {
            _active = false;
            _owner = null;
            CryptographicOperations.ZeroMemory(_source);
            _source = [];
        }

        public void Dispose() => Retire();
        public ValueTask DisposeAsync()
        {
            Retire();
            return ValueTask.CompletedTask;
        }
    }
}
