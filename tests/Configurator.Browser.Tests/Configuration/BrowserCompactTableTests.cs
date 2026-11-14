namespace Configurator.Browser.Tests;

using System.Text;
using System.Text.Json.Nodes;
using Bunit;
using Configurator.Core.Definitions;
using Configurator.Presentation;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

public sealed class BrowserCompactTableTests
{
    [Test]
    public async Task CompactModeLocksSecretsWithoutConsentAndDebouncesPublicSearch()
    {
        var clock = new ManualTimeProvider();
        var definition = CreateCompactDefinition();
        await using var context = BrowserFixture.Context();
        context.Services.AddSingleton<TimeProvider>(clock);

        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, definition));
        var form = component.FindComponents<ConfigurationField>()[0].Instance.Form;

        await component.Find("[data-layout-mode='compact']").ClickAsync(new MouseEventArgs());

        var table = component.Find("[data-compact-table]");
        await Assert.That(table.GetAttribute("role")).IsEqualTo("table");
        await Assert.That(int.Parse(table.GetAttribute("aria-rowcount")!, System.Globalization.CultureInfo.InvariantCulture))
            .IsGreaterThan(0);

        // Opening Compact mode never grants secret entry consent.
        await Assert.That(form.SessionPrivacy).IsEqualTo(DefinitionSessionPrivacy.BrowserNoSecrets);
        await Assert.That(component.Find("[data-workspace-consent-chip]").GetAttribute("data-consent-active")).IsEqualTo("false");

        var secretRow = component.Find("[data-compact-row='secretToken']");
        await Assert.That(secretRow.GetAttribute("aria-rowindex")).IsNotNull();
        await Assert.That(secretRow.QuerySelectorAll("[data-compact-secret-locked]").Length).IsEqualTo(1);
        await Assert.That(secretRow.QuerySelectorAll("input").Length).IsEqualTo(0);
        await Assert.That(secretRow.QuerySelectorAll("[data-action='compact-secret-consent']").Length).IsEqualTo(1);

        // 150ms TimeProvider debounce: 50ms must not filter yet; +100ms filters public metadata only.
        await component.Find("[data-compact-search]").InputAsync(new ChangeEventArgs { Value = "Host name" });
        await clock.AdvanceAsync(TimeSpan.FromMilliseconds(50), component);
        await Assert.That(component.FindAll("[data-compact-row='secretToken']").Count).IsEqualTo(1);

        await clock.AdvanceAsync(TimeSpan.FromMilliseconds(100), component);
        await Assert.That(component.FindAll("[data-compact-row='hostname']").Count).IsEqualTo(1);
        await Assert.That(component.FindAll("[data-compact-row='secretToken']")).IsEmpty();

        // Clear filter and verify private map entry values/keys are masked and unsearchable.
        await component.Find("[data-compact-search]").InputAsync(new ChangeEventArgs { Value = string.Empty });
        await clock.AdvanceAsync(TimeSpan.FromMilliseconds(150), component);

        await component.Find("#secret-consent").ChangeAsync(new ChangeEventArgs { Value = true });
        await component.InvokeAsync(() =>
        {
            form.EditValue(["secretToken"], "synthetic-top-secret-value");
            form.AddMapEntry(["privateMap"], "canary-private-map-key");
            form.EditValue(["privateMap", "canary-private-map-key"], "canary-private-map-value");
        });

        string tableHtml = component.Find("[data-compact-table]").OuterHtml;
        await Assert.That(tableHtml).DoesNotContain("synthetic-top-secret-value");
        await Assert.That(tableHtml).DoesNotContain("canary-private-map-key");
        await Assert.That(tableHtml).DoesNotContain("canary-private-map-value");

        await component.Find("[data-compact-search]").InputAsync(new ChangeEventArgs { Value = "canary-private" });
        await clock.AdvanceAsync(TimeSpan.FromMilliseconds(150), component);
        await Assert.That(component.FindAll("[data-compact-row]")).IsEmpty();
    }

    [Test]
    public async Task CompactModeRoundTripsWithGuidedWithoutMaterializingOmittedOrDefaultTextAndMaintainsRowKeys()
    {
        var clock = new ManualTimeProvider();
        var definition = CreateCompactDefinition();
        await using var context = BrowserFixture.Context();
        context.Services.AddSingleton<TimeProvider>(clock);

        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, definition));
        var form = component.FindComponents<ConfigurationField>()[0].Instance.Form;

        await component.Find("[data-layout-mode='compact']").ClickAsync(new MouseEventArgs());

        await component.Find("[data-compact-row='hostname'] input").InputAsync(new ChangeEventArgs { Value = "compact.example" });
        await Assert.That(form.Snapshot().Values["hostname"].GetString()).IsEqualTo("compact.example");

        // Sort toggle changes order stably while preserving unique row identities and omitted fields remain absent.
        await component.Find("[data-compact-sort='origin']").ClickAsync(new MouseEventArgs());
        await Assert.That(form.Snapshot().Values.ContainsKey("timeout")).IsFalse();
        await Assert.That(form.Snapshot().Values.ContainsKey("optionalFlag")).IsFalse();

        // Focus tracking keeps editor identity active across re-render/recycle notifications.
        await component.Find("[data-compact-row='hostname'] input").FocusAsync(new FocusEventArgs());
        await Assert.That(component.Find("[data-compact-row='hostname']").HasAttribute("data-compact-active")).IsTrue();

        await component.Find("[data-layout-mode='guided']").ClickAsync(new MouseEventArgs());
        await Assert.That(component.Find("[data-field='hostname'] input").GetAttribute("value")).IsEqualTo("compact.example");
        await Assert.That(form.Snapshot().Values.ContainsKey("timeout")).IsFalse();
        await Assert.That(form.Snapshot().Values.ContainsKey("optionalFlag")).IsFalse();
    }

    private static Definition CreateCompactDefinition()
    {
        JsonObject source = JsonNode.Parse(BrowserFixture.Definition.CanonicalBytes.Span)!.AsObject();
        source["configuration"] = JsonNode.Parse("""
        {
          "type": "object",
          "additionalProperties": false,
          "x-configurator": { "sensitivity": "public" },
          "properties": {
            "hostname": {
              "type": "string",
              "title": "Host name",
              "x-configurator": { "sensitivity": "public", "detail": "basic" }
            },
            "timeout": {
              "type": "integer",
              "title": "Request timeout",
              "minimum": 1,
              "maximum": 300,
              "x-configurator": {
                "sensitivity": "public",
                "detail": "basic",
                "units": "seconds",
                "defaultText": "Server picks 30s if omitted."
              }
            },
            "optionalFlag": {
              "type": "boolean",
              "title": "Optional feature",
              "x-configurator": { "sensitivity": "public", "detail": "basic", "widget": "switch" }
            },
            "secretToken": {
              "type": "string",
              "title": "Secret token",
              "x-configurator": { "sensitivity": "secret", "detail": "basic", "secret": { "acquisition": "local", "kind": "password" } }
            },
            "privateMap": {
              "type": "object",
              "title": "Private map",
              "x-configurator": { "sensitivity": "secret", "detail": "basic" },
              "additionalProperties": {
                "type": "string",
                "title": "Entry value",
                "x-configurator": { "sensitivity": "secret" }
              }
            }
          },
          "required": ["hostname", "secretToken"]
        }
        """);
        source.Remove("presentation");
        source["artifacts"] = JsonNode.Parse("""
        [
          {
            "id": "config",
            "format": "json",
            "fileName": "service.json",
            "targetUpdateMode": "create",
            "restartRequirement": "process",
            "projection": {
              "op": "object",
              "members": {
                "hostname": { "op": "copy", "reference": { "scope": "root", "path": "/hostname" } }
              }
            }
          }
        ]
        """);
        source["requiredCapabilities"] = new JsonArray("projection.copy", "projection.object", "schema.boolean", "schema.bounds", "schema.integer", "schema.map", "schema.object", "schema.secret", "schema.string");
        return DefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString()));
    }

    private sealed class ManualTimeProvider : TimeProvider
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
}
