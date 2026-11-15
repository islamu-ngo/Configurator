namespace Configurator.Browser.Tests;

using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Bunit;
using Configurator.Core.Definitions;
using Configurator.Presentation;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

public sealed class BrowserContinuousWorkspaceTests
{
    [Test]
    public async Task CompactModeLocksSecretsWithoutConsentAndMasksPrivateValuesInContinuousWorkspace()
    {
        var definition = CreateCompactDefinition();
        await using var context = BrowserFixture.Context();

        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, definition));
        var form = component.FindComponents<ConfigurationField>()[0].Instance.Form;

        await component.Find("[data-layout-mode='compact']").ClickAsync(new MouseEventArgs());

        var workspace = component.Find("[data-compact-workspace]");
        await Assert.That(workspace).IsNotNull();
        await Assert.That(component.FindAll("[data-compact-table]")).IsEmpty();
        await Assert.That(component.FindAll("[data-compact-search]")).IsEmpty();

        // Opening Compact mode never grants secret entry consent.
        await Assert.That(form.SessionPrivacy).IsEqualTo(DefinitionSessionPrivacy.BrowserNoSecrets);
        await Assert.That(component.Find("[data-workspace-consent-chip]").GetAttribute("data-consent-active")).IsEqualTo("false");

        var secretField = component.Find("[data-field='secretToken']");
        await Assert.That(secretField.QuerySelectorAll("[data-secret-denied]").Length).IsEqualTo(1);
        await Assert.That(secretField.QuerySelectorAll("input").Length).IsEqualTo(0);
        await Assert.That(secretField.QuerySelectorAll("[data-action='field-secret-consent']").Length).IsEqualTo(1);

        // Clicking locked secret consent action opens the single consent destination without auto-enabling consent.
        await secretField.QuerySelector("[data-action='field-secret-consent']")!.ClickAsync(new MouseEventArgs());
        await Assert.That(form.SessionPrivacy).IsEqualTo(DefinitionSessionPrivacy.BrowserNoSecrets);
        await Assert.That(component.Find("details[data-secret-consent]").HasAttribute("open")).IsTrue();

        // Enable secrets, supply secret and private map entry, and verify masking + metadata-only search.
        await component.Find("#secret-consent").ChangeAsync(new ChangeEventArgs { Value = true });
        await component.InvokeAsync(() =>
        {
            form.EditValue(["secretToken"], "synthetic-top-secret-value");
            form.AddMapEntry(["privateMap"], "canary-private-map-key");
            form.EditValue(["privateMap", "canary-private-map-key"], "canary-private-map-value");
        });

        await Assert.That(component.Find("[data-field='secretToken'] input").GetAttribute("type")).IsEqualTo("password");
        await Assert.That(component.Find("[data-field='privateMap.canary-private-map-key'] input").GetAttribute("type")).IsEqualTo("password");
        string visibleText = string.Join(" ", component.FindAll("[data-compact-workspace] label, [data-compact-workspace] [data-compact-status], [data-compact-workspace] h3").Select(element => element.TextContent));
        await Assert.That(visibleText).DoesNotContain("synthetic-top-secret-value");
        await Assert.That(visibleText).DoesNotContain("canary-private-map-value");

        await component.InvokeAsync(() => form.SetDiscovery("canary-private"));
        await Assert.That(form.Projection.Matches).IsEmpty();
    }

    [Test]
    public async Task CompactModeRoundTripsWithGuidedWithoutMaterializingOmittedOrDefaultText()
    {
        var definition = CreateCompactDefinition();
        await using var context = BrowserFixture.Context();

        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, definition));
        var form = component.FindComponents<ConfigurationField>()[0].Instance.Form;

        await component.Find("[data-layout-mode='compact']").ClickAsync(new MouseEventArgs());

        await component.Find("[data-field='hostname'] input").InputAsync(new ChangeEventArgs { Value = "compact.example" });
        await Assert.That(form.Snapshot().Values["hostname"].GetString()).IsEqualTo("compact.example");
        await Assert.That(form.Snapshot().Values.ContainsKey("timeout")).IsFalse();
        await Assert.That(form.Snapshot().Values.ContainsKey("optionalFlag")).IsFalse();

        await component.Find("[data-layout-mode='guided']").ClickAsync(new MouseEventArgs());
        await Assert.That(component.Find("[data-field='hostname'] input").GetAttribute("value")).IsEqualTo("compact.example");
        await Assert.That(form.Snapshot().Values.ContainsKey("timeout")).IsFalse();
        await Assert.That(form.Snapshot().Values.ContainsKey("optionalFlag")).IsFalse();
    }

    [Test]
    public async Task CompactRendersAllApplicableSectionsInContinuousFlowWithSingleGlobalAdmissionBudget()
    {
        var definition = CreateMultiSectionContinuousDefinition(fieldsPerSection: 8);
        await using var context = BrowserFixture.Context();
        using var gatedJs = new GatedNextFrameJsRuntime(context.JSInterop.JSRuntime);
        context.Services.AddSingleton<IJSRuntime>(gatedJs);

        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, definition));
        await gatedJs.ReleaseAllFramesAsync(component);

        var form = component.FindComponents<ConfigurationField>()[0].Instance.Form;
        await Assert.That(form.SelectedExperienceId).IsEqualTo("essential");

        // Gate nextFrame before switching to Compact so we can inspect the initial FirstBatchSize = 6 admission across both sections.
        gatedJs.HoldNextFrame = true;
        await component.Find("[data-layout-mode='compact']").ClickAsync(new MouseEventArgs());

        // Single [data-configuration-mount] owner across the entire Compact workspace.
        var mounts = component.FindAll("[data-compact-workspace] [data-configuration-mount]");
        await Assert.That(mounts.Count).IsEqualTo(1);
        await Assert.That(component.FindAll("[data-compact-workspace] [data-compact-section]").Count).IsEqualTo(2);
        await Assert.That(component.FindAll("[data-compact-workspace] [data-compact-section-jump]").Count).IsEqualTo(2);

        // With 16 active root fields across 2 sections, the single initial admission batch admits 6 fields total (not 6 per section).
        int initialAdmitted = component.FindComponents<ConfigurationField>().Count;
        await Assert.That(initialAdmitted).IsEqualTo(6);

        await gatedJs.ReleaseAllFramesAsync(component);
        await Assert.That(component.FindComponents<ConfigurationField>().Count).IsEqualTo(16);
    }

    [Test]
    public async Task CollapsingGroupAndFilteringMatchesInCompactHideViaDomAttributesWithoutDisposingAdmittedControlsOrMutatingExperience()
    {
        var definition = CreateMultiSectionContinuousDefinition(fieldsPerSection: 2);
        await using var context = BrowserFixture.Context();
        var component = BrowserFixture.RenderWorkspace(context, definition);
        var form = component.FindComponents<ConfigurationField>()[0].Instance.Form;

        await Assert.That(form.SelectedExperienceId).IsEqualTo("essential");

        await component.Find("[data-layout-mode='compact']").ClickAsync(new MouseEventArgs());
        await Assert.That(form.EditorVisibility).IsEqualTo(DefinitionEditorVisibilityPolicy.Continuous);
        await Assert.That(form.SelectedExperienceId).IsEqualTo("essential");

        // Both "general" and "security" sections are mounted even though SelectedExperienceId is "essential".
        await Assert.That(component.FindAll("[data-compact-section='general']").Count).IsEqualTo(1);
        await Assert.That(component.FindAll("[data-compact-section='security']").Count).IsEqualTo(1);

        var auditFieldComponent = component.FindComponents<ConfigurationField>()
            .Single(field => field.Instance.Path.SequenceEqual(["securityField1"]));
        string mountGenerationBefore = component.Find("[data-compact-workspace] [data-configuration-mount]")
            .GetAttribute("data-mount-generation")!;

        // Edit a field in the non-essential "security" section.
        await component.Find("[data-field='securityField0'] input").InputAsync(new ChangeEventArgs { Value = "sec-zero" });
        await Assert.That(form.Snapshot().Values["securityField0"].GetString()).IsEqualTo("sec-zero");

        // Re-capture after the input revision update.
        auditFieldComponent = component.FindComponents<ConfigurationField>()
            .Single(field => field.Instance.Path.SequenceEqual(["securityField1"]));
        mountGenerationBefore = component.Find("[data-compact-workspace] [data-configuration-mount]")
            .GetAttribute("data-mount-generation")!;

        // Collapse and expand the group in Compact: uses hidden + inert without disposing ConfigurationField or resetting mount generation.
        var groupSection = component.Find("[data-group='security-advanced']");
        await Assert.That(groupSection.GetAttribute("data-group-expanded")).IsEqualTo("false");
        var groupFieldsContainer = groupSection.QuerySelector(".configuration-section-group__fields")!;
        await Assert.That(groupFieldsContainer.HasAttribute("hidden")).IsTrue();
        await Assert.That(groupFieldsContainer.HasAttribute("inert")).IsTrue();

        await groupSection.QuerySelector("[data-group-toggle]")!.ClickAsync(new MouseEventArgs());
        await Assert.That(component.Find("[data-group='security-advanced']").GetAttribute("data-group-expanded")).IsEqualTo("true");
        await Assert.That(component.Find("[data-group='security-advanced'] .configuration-section-group__fields").HasAttribute("hidden")).IsFalse();

        // Collapse again and verify the exact same ConfigurationField instance remains mounted.
        await component.Find("[data-group='security-advanced'] [data-group-toggle]").ClickAsync(new MouseEventArgs());
        var auditFieldAfterCollapse = component.FindComponents<ConfigurationField>()
            .Single(field => field.Instance.Path.SequenceEqual(["securityField1"]));
        await Assert.That(ReferenceEquals(auditFieldComponent.Instance, auditFieldAfterCollapse.Instance)).IsTrue();
        await Assert.That(component.Find("[data-compact-workspace] [data-configuration-mount]").GetAttribute("data-mount-generation"))
            .IsEqualTo(mountGenerationBefore);

        // Search filter "Show matches only" hides non-matching slots via hidden + inert without disposing admitted controls.
        await component.InvokeAsync(() => form.SetDiscovery("generalField0"));
        await component.Find("[data-compact-matches-only]").ChangeAsync(new ChangeEventArgs { Value = true });

        var nonMatchingSlot = component.Find("[data-compact-slot='securityField0']");
        await Assert.That(nonMatchingSlot.HasAttribute("hidden")).IsTrue();
        await Assert.That(nonMatchingSlot.HasAttribute("inert")).IsTrue();
        var auditFieldAfterFilter = component.FindComponents<ConfigurationField>()
            .Single(field => field.Instance.Path.SequenceEqual(["securityField1"]));
        await Assert.That(ReferenceEquals(auditFieldComponent.Instance, auditFieldAfterFilter.Instance)).IsTrue();

        // Triggering a correction/search locate in Compact stays in Compact, clears obstructing "Show matches only", and expands the group.
        await component.Find(".configuration-editor").KeyDownAsync(new KeyboardEventArgs { Key = "k", CtrlKey = true });
        await component.InvokeAsync(() => form.SetDiscovery("securityField1"));
        await component.Find("[data-discovery-result='securityField1'] button").ClickAsync(new MouseEventArgs());

        await Assert.That(form.WorkspaceLayoutMode).IsEqualTo(WorkspaceLayoutMode.Compact);
        await Assert.That(form.SelectedExperienceId).IsEqualTo("essential");
        await Assert.That(component.Find("[data-group='security-advanced']").GetAttribute("data-group-expanded")).IsEqualTo("true");
        await Assert.That(component.Find("[data-compact-slot='securityField0']").HasAttribute("hidden")).IsFalse();

        // Returning to Guided restores Authored visibility and original "essential" SelectedExperienceId while preserving edited answers.
        await component.Find("[data-layout-mode='guided']").ClickAsync(new MouseEventArgs());
        await Assert.That(form.WorkspaceLayoutMode).IsEqualTo(WorkspaceLayoutMode.Guided);
        await Assert.That(form.EditorVisibility).IsEqualTo(DefinitionEditorVisibilityPolicy.Authored);
        await Assert.That(form.SelectedExperienceId).IsEqualTo("essential");
        await Assert.That(form.Snapshot().Values["securityField0"].GetString()).IsEqualTo("sec-zero");
    }

    [Test]
    public async Task CompactScalarEditSectionJumpAndGroupToggleSkipUnchangedSiblingFieldRendersWhilePropagatingDependentChanges()
    {
        var root = new JsonObject
        {
            ["schemaVersion"] = "0.0.1",
            ["profile"] = "bounded-v0.0.1",
            ["familyId"] = "d66d02af-6a66-4396-9717-452e8eb9541b",
            ["revision"] = 1,
            ["project"] = JsonNode.Parse("""{"id":"compact-render-skip","publisher":"example","name":"Compact Render Skip Test"}"""),
            ["applicationVersions"] = new JsonArray("1.0.0"),
            ["entryPoints"] = new JsonArray("setup"),
            ["locales"] = new JsonArray("en"),
            ["requiredCapabilities"] = new JsonArray("predicate.root", "projection.literal", "schema.bounds", "schema.enum", "schema.integer", "schema.object", "schema.string"),
            ["configuration"] = JsonNode.Parse("""
            {
              "type": "object",
              "additionalProperties": false,
              "x-configurator": { "sensitivity": "public" },
              "required": ["mode"],
              "properties": {
                "mode": {
                  "type": "string",
                  "title": "Mode",
                  "enum": ["local", "remote"],
                  "x-configurator": { "sensitivity": "public", "detail": "basic" }
                },
                "endpoint": {
                  "type": "string",
                  "title": "Endpoint",
                  "x-configurator": {
                    "sensitivity": "public",
                    "detail": "basic",
                    "applicableWhen": { "op": "equals", "reference": { "scope": "root", "path": "/mode" }, "value": "remote" }
                  }
                },
                "host": {
                  "type": "string",
                  "title": "Host",
                  "x-configurator": { "sensitivity": "public", "detail": "basic" }
                },
                "port": {
                  "type": "integer",
                  "title": "Port",
                  "minimum": 1,
                  "maximum": 65535,
                  "x-configurator": { "sensitivity": "public", "detail": "advanced" }
                },
                "workerNote": {
                  "type": "string",
                  "title": "Worker note",
                  "x-configurator": { "sensitivity": "public", "detail": "basic" }
                }
              }
            }
            """),
            ["presentation"] = JsonNode.Parse("""
            {
              "defaultJourney": "essential",
              "navigation": "sections",
              "sections": [
                {
                  "id": "core",
                  "title": "Core",
                  "detail": "basic",
                  "pointers": ["/mode", "/endpoint", "/host", "/port"],
                  "groups": [
                    {
                      "id": "tuning",
                      "title": "Tuning",
                      "detail": "advanced",
                      "collapsed": true,
                      "pointers": ["/port"]
                    }
                  ]
                },
                {
                  "id": "worker",
                  "title": "Worker",
                  "detail": "basic",
                  "pointers": ["/workerNote"]
                }
              ],
              "journeys": [
                {
                  "id": "essential",
                  "title": "Essential",
                  "sections": ["core", "worker"]
                }
              ]
            }
            """),
            ["artifacts"] = JsonNode.Parse("""
            [
              {
                "id": "out",
                "format": "json",
                "fileName": "out.json",
                "targetUpdateMode": "create",
                "restartRequirement": "none",
                "projection": { "op": "literal", "value": {} }
              }
            ]
            """)
        };
        Definition definition = DefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(root.ToJsonString()));

        await using var context = BrowserFixture.Context();
        var component = BrowserFixture.RenderWorkspace(context, definition);
        var form = component.FindComponents<ConfigurationField>()[0].Instance.Form;

        await component.Find("[data-layout-mode='compact']").ClickAsync(new MouseEventArgs());

        var hostField = component.FindComponents<ConfigurationField>().Single(field => field.Instance.Path.SequenceEqual(["host"]));
        var workerField = component.FindComponents<ConfigurationField>().Single(field => field.Instance.Path.SequenceEqual(["workerNote"]));
        string initialMountGeneration = component.Find("[data-compact-workspace] [data-configuration-mount]").GetAttribute("data-mount-generation")!;
        int hostRendersBaseline = hostField.RenderCount;
        int workerRendersBaseline = workerField.RenderCount;

        // Editing host re-renders host only; sibling workerNote skips redundant re-render without bumping mount generation.
        await component.Find("[data-field='host'] input").InputAsync(new ChangeEventArgs { Value = "api.internal" });
        await Assert.That(hostField.RenderCount).IsGreaterThan(hostRendersBaseline);
        await Assert.That(workerField.RenderCount).IsEqualTo(workerRendersBaseline);
        await Assert.That(component.Find("[data-compact-workspace] [data-configuration-mount]").GetAttribute("data-mount-generation"))
            .IsEqualTo(initialMountGeneration);

        int hostAfterOwnEdit = hostField.RenderCount;

        // Section jump, group toggle, and search filter do not re-render unchanged scalar siblings or bump mount generation.
        await component.Find("[data-compact-section-jump='worker']").ClickAsync(new MouseEventArgs());
        await component.Find("[data-group='tuning'] [data-group-toggle]").ClickAsync(new MouseEventArgs());
        await component.InvokeAsync(() => form.SetDiscovery("Host"));
        await component.Find("[data-compact-matches-only]").ChangeAsync(new ChangeEventArgs { Value = true });

        await Assert.That(hostField.RenderCount).IsEqualTo(hostAfterOwnEdit);
        await Assert.That(workerField.RenderCount).IsEqualTo(workerRendersBaseline);
        await Assert.That(component.Find("[data-compact-workspace] [data-configuration-mount]").GetAttribute("data-mount-generation"))
            .IsEqualTo(initialMountGeneration);

        await component.Find("[data-compact-matches-only]").ChangeAsync(new ChangeEventArgs { Value = false });
        await component.Find("[data-group='tuning'] [data-group-toggle]").ClickAsync(new MouseEventArgs());

        // Editing workerNote after it skipped multiple sibling updates still applies cleanly via synchronized revision.
        await component.Find("[data-field='workerNote'] input").InputAsync(new ChangeEventArgs { Value = "worker-ready" });
        await Assert.That(workerField.RenderCount).IsGreaterThan(workerRendersBaseline);
        await Assert.That(hostField.RenderCount).IsEqualTo(hostAfterOwnEdit);
        await Assert.That(component.Find("[data-field='workerNote'] input").GetAttribute("value")).IsEqualTo("worker-ready");

        // Conditional activation mounts the newly applicable field cleanly while keeping settled siblings intact.
        await component.InvokeAsync(() => form.EditValue(["mode"], "remote"));
        await Assert.That(component.FindComponents<ConfigurationField>().Any(field => field.Instance.Path.SequenceEqual(["endpoint"]))).IsTrue();
        await Assert.That(hostField.RenderCount).IsEqualTo(hostAfterOwnEdit);

        // Undoing back across mode, workerNote, and host propagates the restored value to host even after earlier skips.
        await component.InvokeAsync(() =>
        {
            form.Undo();
            form.Undo();
            form.Undo();
        });
        await Assert.That(hostField.RenderCount).IsGreaterThan(hostAfterOwnEdit);
        await Assert.That(component.Find("[data-field='host'] input").GetAttribute("value")).IsEqualTo(string.Empty);
    }

    [Test]
    public async Task ClosingCompactInlineFilesPanelRetiresPreviewAndRejectsLateExportContinuationWithoutUnmountingWorkspace()
    {
        await using var context = BrowserFixture.Context();
        var javascript = new BrowserExportLifecycleTests.GatedJavascript(context.JSInterop.JSRuntime);
        await using var module = new BrowserExportLifecycleTests.RecordingModule
        {
            HoldOpening = true
        };
        javascript.Release.TrySetResult(module);
        context.Services.AddSingleton<IJSRuntime>(javascript);

        var component = BrowserFixture.RenderWorkspace(context, CreateCompactDefinition());
        var form = component.FindComponents<ConfigurationField>()[0].Instance.Form;

        await component.Find("[data-layout-mode='compact']").ClickAsync(new MouseEventArgs());
        await component.Find("#secret-consent").ChangeAsync(new ChangeEventArgs { Value = true });
        await component.Find("[data-field='hostname'] input").InputAsync(new ChangeEventArgs { Value = "compact.example" });
        await component.Find("[data-field='secretToken'] input").InputAsync(new ChangeEventArgs { Value = "synthetic-secret-compact-key" });

        var endpointComponentBefore = component.FindComponents<ConfigurationField>()
            .Single(field => field.Instance.Path.SequenceEqual(["hostname"])).Instance;

        // Open Review in Compact: mounts inline panel while keeping ConfigurationCompactWorkspace mounted.
        await component.Find("[data-workflow-step='review']").ClickAsync(new MouseEventArgs());
        await Assert.That(component.FindAll("[data-compact-inline-panel='review']").Count).IsEqualTo(1);
        await Assert.That(component.FindAll("[data-compact-workspace]").Count).IsEqualTo(1);

        // Prepare complete output from Compact inline Review panel: switches inline panel to Files while keeping Compact workspace mounted.
        await component.Find("[data-compact-inline-panel='review'] [data-action='prepare-reviewed']").ClickAsync(new MouseEventArgs());
        await Assert.That(component.FindAll("[data-compact-inline-panel='files']").Count).IsEqualTo(1);
        await Assert.That(component.FindAll("[data-compact-workspace]").Count).IsEqualTo(1);

        var endpointComponentDuringFiles = component.FindComponents<ConfigurationField>()
            .Single(field => field.Instance.Path.SequenceEqual(["hostname"])).Instance;
        await Assert.That(ReferenceEquals(endpointComponentBefore, endpointComponentDuringFiles)).IsTrue();

        // Start an async copy in the inline Files panel, then close the panel before the JS copy resolves.
        Task copying = component.Find("[data-compact-inline-panel='files'] [data-action='copy-file']").ClickAsync(new MouseEventArgs());
        await module.OpeningStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await component.Find("[data-action='close-compact-panel']").ClickAsync(new MouseEventArgs());
        await Assert.That(component.FindAll("[data-compact-inline-panel]")).IsEmpty();
        await Assert.That(module.OpeningBytes!.All(value => value == 0)).IsTrue();

        module.OpeningRelease.TrySetResult();
        await copying;

        // Late copy continuation was rejected by generation check; workspace remains mounted and no exposure was recorded.
        await Assert.That(component.FindAll("[data-compact-workspace]").Count).IsEqualTo(1);
        await Assert.That(module.ExposureCount).IsEqualTo(0);
        await Assert.That(component.Find("[role=status]").GetAttribute("data-code")).IsNotEqualTo("copied");

        // Re-opening Compact inline Files panel shows the unrevealed prepared file is still retained, and section jump preserves it.
        await component.InvokeAsync(() => component.FindComponent<ConfigurationCompactWorkspace>().Instance.JumpToSectionAsync("configure"));
        await component.Find("[data-workflow-step='prepare']").ClickAsync(new MouseEventArgs());
        await Assert.That(component.FindAll("[data-compact-inline-panel='files'] [data-prepared-file]").Count).IsEqualTo(1);
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

    private static Definition CreateMultiSectionContinuousDefinition(int fieldsPerSection)
    {
        var properties = new JsonObject();
        var generalPointers = new JsonArray();
        var securityPointers = new JsonArray();
        var groupPointers = new JsonArray();

        for (int index = 0; index < fieldsPerSection; index++)
        {
            string generalName = "generalField" + index.ToString(CultureInfo.InvariantCulture);
            properties[generalName] = JsonNode.Parse($$"""
            {
              "type": "string",
              "title": "General Field {{index}}",
              "x-configurator": { "sensitivity": "public", "detail": "basic" }
            }
            """);
            generalPointers.Add("/" + generalName);

            string securityName = "securityField" + index.ToString(CultureInfo.InvariantCulture);
            properties[securityName] = JsonNode.Parse($$"""
            {
              "type": "string",
              "title": "Security Field {{index}}",
              "x-configurator": { "sensitivity": "public", "detail": "advanced" }
            }
            """);
            securityPointers.Add("/" + securityName);
            if (index == fieldsPerSection - 1)
                groupPointers.Add("/" + securityName);
        }

        var root = new JsonObject
        {
            ["schemaVersion"] = "0.0.1",
            ["profile"] = "bounded-v0.0.1",
            ["familyId"] = "8f84708c-227a-42db-a9a2-f5870a1eb303",
            ["revision"] = 1,
            ["project"] = JsonNode.Parse("""{"id":"multi-continuous","publisher":"example","name":"Multi Continuous Test"}"""),
            ["applicationVersions"] = new JsonArray("1.0.0"),
            ["entryPoints"] = new JsonArray("setup"),
            ["locales"] = new JsonArray("en"),
            ["configuration"] = new JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = false,
                ["x-configurator"] = JsonNode.Parse("""{"sensitivity":"public"}"""),
                ["required"] = new JsonArray("generalField0"),
                ["properties"] = properties
            },
            ["presentation"] = new JsonObject
            {
                ["defaultJourney"] = "essential",
                ["navigation"] = "sections",
                ["sections"] = new JsonArray(
                    new JsonObject
                    {
                        ["id"] = "general",
                        ["title"] = "General",
                        ["detail"] = "basic",
                        ["pointers"] = generalPointers
                    },
                    new JsonObject
                    {
                        ["id"] = "security",
                        ["title"] = "Security",
                        ["detail"] = "advanced",
                        ["pointers"] = securityPointers,
                        ["groups"] = new JsonArray(
                            new JsonObject
                            {
                                ["id"] = "security-advanced",
                                ["title"] = "Security Advanced Group",
                                ["detail"] = "advanced",
                                ["collapsed"] = true,
                                ["pointers"] = groupPointers
                            })
                    }),
                ["journeys"] = new JsonArray(
                    new JsonObject
                    {
                        ["id"] = "essential",
                        ["title"] = "Essential",
                        ["sections"] = new JsonArray("general")
                    },
                    new JsonObject
                    {
                        ["id"] = "full",
                        ["title"] = "Full",
                        ["sections"] = new JsonArray("general", "security")
                    })
            },
            ["artifacts"] = JsonNode.Parse("""
            [
              {
                "id": "out",
                "format": "json",
                "fileName": "out.json",
                "targetUpdateMode": "create",
                "restartRequirement": "none",
                "projection": { "op": "literal", "value": {} }
              }
            ]
            """),
            ["requiredCapabilities"] = new JsonArray("projection.literal", "schema.object", "schema.string")
        };

        return DefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(root.ToJsonString()));
    }

    private sealed class GatedNextFrameJsRuntime(IJSRuntime inner) : IJSRuntime, IDisposable
    {
        private readonly List<TaskCompletionSource> _heldFrames = [];

        public bool HoldNextFrame { get; set; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public async ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            if (identifier == "import" && args is ["./js/configuration-rendering.js"])
                return (TValue)(object)new RenderingModule(this);
            return await inner.InvokeAsync<TValue>(identifier, cancellationToken, args);
        }

        public async Task ReleaseAllFramesAsync(IRenderedComponent<ConfigurationEditor> component)
        {
            HoldNextFrame = false;
            while (_heldFrames.Count > 0)
            {
                TaskCompletionSource[] pending = _heldFrames.ToArray();
                _heldFrames.Clear();
                foreach (TaskCompletionSource tcs in pending)
                    await component.InvokeAsync(() => tcs.TrySetResult());
            }
        }

        public void Dispose()
        {
            foreach (TaskCompletionSource tcs in _heldFrames)
                tcs.TrySetCanceled();
            _heldFrames.Clear();
        }

        private sealed class RenderingModule(GatedNextFrameJsRuntime owner) : IJSObjectReference
        {
            public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
                InvokeAsync<TValue>(identifier, CancellationToken.None, args);

            public async ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            {
                if ((identifier == "nextFrame" || identifier == "nextTask") && owner.HoldNextFrame)
                {
                    var tcs = new TaskCompletionSource();
                    owner._heldFrames.Add(tcs);
                    await tcs.Task.WaitAsync(cancellationToken);
                }
                return default!;
            }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
