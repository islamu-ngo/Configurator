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

        await component.Find("[data-layout-mode='all-sections']").ClickAsync(new MouseEventArgs());

        var workspace = component.Find("[data-layout-active='all-sections']");
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
        string visibleText = string.Join(" ", component.FindAll("[data-layout-active='all-sections'] label, [data-layout-active='all-sections'] [data-compact-status], [data-layout-active='all-sections'] h3").Select(element => element.TextContent));
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

        await component.Find("[data-layout-mode='all-sections']").ClickAsync(new MouseEventArgs());

        await component.Find("[data-field='hostname'] input").InputAsync(new ChangeEventArgs { Value = "compact.example" });
        await Assert.That(form.Snapshot().Values["hostname"].GetString()).IsEqualTo("compact.example");
        await Assert.That(form.Snapshot().Values.ContainsKey("timeout")).IsFalse();
        await Assert.That(form.Snapshot().Values.ContainsKey("optionalFlag")).IsFalse();

        await component.Find("[data-layout-mode='section-by-section']").ClickAsync(new MouseEventArgs());
        await Assert.That(component.Find("[data-field='hostname'] input").GetAttribute("value")).IsEqualTo("compact.example");
        await Assert.That(form.Snapshot().Values.ContainsKey("timeout")).IsFalse();
        await Assert.That(form.Snapshot().Values.ContainsKey("optionalFlag")).IsFalse();
    }

    [Test]
    public async Task AllSectionsRendersApplicableSectionsWithLightweightSettingRowsAndWindowedMounts()
    {
        var definition = CreateMultiSectionContinuousDefinition(fieldsPerSection: 8);
        await using var context = BrowserFixture.Context();
        var component = BrowserFixture.RenderWorkspace(context, definition);

        var form = component.FindComponents<ConfigurationField>()[0].Instance.Form;
        await Assert.That(form.SelectedExperienceId).IsEqualTo("essential");

        await component.Find("[data-layout-mode='all-sections']").ClickAsync(new MouseEventArgs());

        await Assert.That(component.FindAll("[data-section-panel]").Count).IsEqualTo(2);
        await Assert.That(component.FindAll("[data-section-panel][data-section-windowed='hydrated']").Count).IsEqualTo(2);
        await Assert.That(component.FindAll("[data-configuration-continuous] [data-configuration-mount]").Count).IsEqualTo(2);
        await Assert.That(BrowserFixture.SectionNavigation(context, component).FindAll("[data-section]").Count).IsEqualTo(2);

        // Collapsed group omits unexpanded setting rows (8 in general + 7 ungrouped in security = 15 rows, 0 heavy ConfigurationField cards).
        await Assert.That(component.FindComponents<ConfigurationField>().Count).IsEqualTo(0);
        await Assert.That(component.FindComponents<ConfigurationSettingRow>().Count).IsEqualTo(15);

        await component.Find("[data-group='security-advanced'] [data-group-toggle]").ClickAsync(new MouseEventArgs());
        await Assert.That(component.FindComponents<ConfigurationSettingRow>().Count).IsEqualTo(16);
    }

    [Test]
    public async Task CollapsingGroupAndFilteringMatchesInAllSectionsOmitCollapsedAndNonMatchingRowsWithoutMutatingExperience()
    {
        var definition = CreateMultiSectionContinuousDefinition(fieldsPerSection: 2);
        await using var context = BrowserFixture.Context();
        var component = BrowserFixture.RenderWorkspace(context, definition);
        var form = component.FindComponents<ConfigurationField>()[0].Instance.Form;

        await Assert.That(form.SelectedExperienceId).IsEqualTo("essential");

        await component.Find("[data-layout-mode='all-sections']").ClickAsync(new MouseEventArgs());
        await Assert.That(form.EditorVisibility).IsEqualTo(DefinitionEditorVisibilityPolicy.Continuous);
        await Assert.That(form.SelectedExperienceId).IsEqualTo("essential");

        // Both "general" and "security" sections are mounted even though SelectedExperienceId is "essential".
        await Assert.That(component.FindAll("[data-section-panel='general']").Count).IsEqualTo(1);
        await Assert.That(component.FindAll("[data-section-panel='security']").Count).IsEqualTo(1);
        await Assert.That(component.FindComponents<ConfigurationSettingRow>().Count).IsEqualTo(3);

        // Edit a field in the non-essential "security" section.
        await component.Find("[data-field='securityField0'] input").InputAsync(new ChangeEventArgs { Value = "sec-zero" });
        await Assert.That(form.Snapshot().Values["securityField0"].GetString()).IsEqualTo("sec-zero");

        // Collapse and expand the group in AllSections: omits collapsed group inputs without resetting saved answers.
        var groupSection = component.Find("[data-group='security-advanced']");
        await Assert.That(groupSection.GetAttribute("data-group-expanded")).IsEqualTo("false");
        await Assert.That(groupSection.QuerySelector(".configuration-section-group__fields")).IsNull();
        await Assert.That(component.FindAll("[data-field='securityField1']").Count).IsEqualTo(0);

        await groupSection.QuerySelector("[data-group-toggle]")!.ClickAsync(new MouseEventArgs());
        await Assert.That(component.Find("[data-group='security-advanced']").GetAttribute("data-group-expanded")).IsEqualTo("true");
        await Assert.That(component.FindAll("[data-field='securityField1'] input").Count).IsEqualTo(1);

        // Collapse again and verify edited answers in the section remain intact while the collapsed row unmounts.
        await component.Find("[data-group='security-advanced'] [data-group-toggle]").ClickAsync(new MouseEventArgs());
        await Assert.That(component.FindAll("[data-field='securityField1']").Count).IsEqualTo(0);
        await Assert.That(form.Snapshot().Values["securityField0"].GetString()).IsEqualTo("sec-zero");

        // Search filter "Show matches only" omits non-matching fields and sections cleanly (zero hidden+inert nodes).
        await component.InvokeAsync(() => form.SetDiscovery("generalField0"));
        await component.Find("[data-compact-matches-only]").ChangeAsync(new ChangeEventArgs { Value = true });

        await Assert.That(component.FindAll("[data-field='securityField0']").Count).IsEqualTo(0);
        await Assert.That(component.FindAll("[data-section-panel='security']").Count).IsEqualTo(0);
        await Assert.That(component.FindAll("[data-layout-active='all-sections'] [hidden][inert]").Count).IsEqualTo(0);

        // Triggering a correction/search locate in AllSections stays in AllSections, clears obstructing "Show matches only", and expands the group.
        await component.Find(".configuration-editor").KeyDownAsync(new KeyboardEventArgs { Key = "k", CtrlKey = true });
        await component.InvokeAsync(() => form.SetDiscovery("securityField1"));
        await component.Find("[data-discovery-result='securityField1'] button").ClickAsync(new MouseEventArgs());

        await Assert.That(form.ConfigureLayoutMode).IsEqualTo(ConfigureLayoutMode.AllSections);
        await Assert.That(form.SelectedExperienceId).IsEqualTo("essential");
        await Assert.That(component.Find("[data-group='security-advanced']").GetAttribute("data-group-expanded")).IsEqualTo("true");
        await Assert.That(component.FindAll("[data-field='securityField0']").Count).IsEqualTo(1);
        await Assert.That(component.FindAll("[data-field='securityField1']").Count).IsEqualTo(1);

        // Returning to SectionBySection restores Authored visibility and original "essential" SelectedExperienceId while preserving edited answers.
        await component.Find("[data-layout-mode='section-by-section']").ClickAsync(new MouseEventArgs());
        await Assert.That(form.ConfigureLayoutMode).IsEqualTo(ConfigureLayoutMode.SectionBySection);
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

        await component.Find("[data-layout-mode='all-sections']").ClickAsync(new MouseEventArgs());

        var hostField = component.FindComponents<ConfigurationSettingRow>().Single(field => field.Instance.Path.SequenceEqual(["host"]));
        var workerField = component.FindComponents<ConfigurationSettingRow>().Single(field => field.Instance.Path.SequenceEqual(["workerNote"]));
        string initialMountGeneration = component.Find("[data-section-panel='worker'] [data-configuration-mount]").GetAttribute("data-mount-generation")!;
        int hostRendersBaseline = hostField.RenderCount;
        int workerRendersBaseline = workerField.RenderCount;

        // Editing host re-renders host only; sibling workerNote skips redundant re-render without bumping mount generation.
        await component.Find("[data-field='host'] input").InputAsync(new ChangeEventArgs { Value = "api.internal" });
        await Assert.That(hostField.RenderCount).IsGreaterThan(hostRendersBaseline);
        await Assert.That(workerField.RenderCount).IsEqualTo(workerRendersBaseline);
        await Assert.That(component.Find("[data-section-panel='worker'] [data-configuration-mount]").GetAttribute("data-mount-generation"))
            .IsEqualTo(initialMountGeneration);

        int hostAfterOwnEdit = hostField.RenderCount;

        // Section jump via left rail and group toggle do not re-render unchanged scalar siblings or bump mount generation.
        await BrowserFixture.SectionNavigation(context, component).Find("[data-section='worker']").ClickAsync(new MouseEventArgs());
        await component.Find("[data-group='tuning'] [data-group-toggle]").ClickAsync(new MouseEventArgs());

        await Assert.That(hostField.RenderCount).IsEqualTo(hostAfterOwnEdit);
        await Assert.That(workerField.RenderCount).IsEqualTo(workerRendersBaseline);
        await Assert.That(component.Find("[data-section-panel='worker'] [data-configuration-mount]").GetAttribute("data-mount-generation"))
            .IsEqualTo(initialMountGeneration);

        // Search filter "Host" omits non-matching worker section cleanly without re-rendering host; unchecking restores worker.
        await component.InvokeAsync(() => form.SetDiscovery("Host"));
        await component.Find("[data-compact-matches-only]").ChangeAsync(new ChangeEventArgs { Value = true });
        await Assert.That(hostField.RenderCount).IsEqualTo(hostAfterOwnEdit);
        await Assert.That(component.FindAll("[data-section-panel='worker']")).IsEmpty();

        await component.Find("[data-compact-matches-only]").ChangeAsync(new ChangeEventArgs { Value = false });
        await component.Find("[data-group='tuning'] [data-group-toggle]").ClickAsync(new MouseEventArgs());

        // Editing workerNote after skipping sibling updates applies cleanly via synchronized revision without re-rendering host.
        workerField = component.FindComponents<ConfigurationSettingRow>().Single(field => field.Instance.Path.SequenceEqual(["workerNote"]));
        int workerBeforeOwnEdit = workerField.RenderCount;
        await component.Find("[data-field='workerNote'] input").InputAsync(new ChangeEventArgs { Value = "worker-ready" });
        await Assert.That(workerField.RenderCount).IsGreaterThan(workerBeforeOwnEdit);
        await Assert.That(hostField.RenderCount).IsEqualTo(hostAfterOwnEdit);
        await Assert.That(component.Find("[data-field='workerNote'] input").GetAttribute("value")).IsEqualTo("worker-ready");

        // Conditional activation mounts the newly applicable field cleanly while keeping settled siblings intact.
        await component.InvokeAsync(() => form.EditValue(["mode"], "remote"));
        await Assert.That(component.FindComponents<ConfigurationSettingRow>().Any(field => field.Instance.Path.SequenceEqual(["endpoint"]))).IsTrue();
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
    public async Task LeavingFilesStageInAllSectionsRetiresPreviewAndRejectsLateExportContinuation()
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

        await component.Find("[data-layout-mode='all-sections']").ClickAsync(new MouseEventArgs());
        await component.Find("#secret-consent").ChangeAsync(new ChangeEventArgs { Value = true });
        await component.Find("[data-field='hostname'] input").InputAsync(new ChangeEventArgs { Value = "compact.example" });
        await component.Find("[data-field='secretToken'] input").InputAsync(new ChangeEventArgs { Value = "synthetic-secret-compact-key" });

        // Open Review stage, then prepare complete output to transition to Files.
        await component.Find("[data-workflow-step='review']").ClickAsync(new MouseEventArgs());
        await Assert.That(component.Find("[data-workflow-step='review']").GetAttribute("aria-current")).IsEqualTo("step");

        await component.Find("[data-action='prepare-reviewed']").ClickAsync(new MouseEventArgs());
        await Assert.That(component.Find("[data-workflow-step='prepare']").GetAttribute("aria-current")).IsEqualTo("step");

        // Start an async copy in Files, then return to Configure before JS copy resolves.
        Task copying = component.Find("[data-action='copy-file']").ClickAsync(new MouseEventArgs());
        await module.OpeningStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await component.Find("[data-workflow-step='configure']").ClickAsync(new MouseEventArgs());
        await Assert.That(module.OpeningBytes!.All(value => value == 0)).IsTrue();

        module.OpeningRelease.TrySetResult();
        await copying;

        // Late copy continuation was rejected by generation check; Configure remains in AllSections and no exposure was recorded.
        await Assert.That(component.FindAll("[data-layout-active='all-sections']").Count).IsEqualTo(1);
        await Assert.That(module.ExposureCount).IsEqualTo(0);
        await Assert.That(component.Find("[role=status]").GetAttribute("data-code")).IsNotEqualTo("copied");
        await Assert.That(form.Snapshot().Values["hostname"].GetString()).IsEqualTo("compact.example");

        // Re-opening Files shows the unrevealed prepared file is still retained.
        await component.Find("[data-workflow-step='prepare']").ClickAsync(new MouseEventArgs());
        await Assert.That(component.FindAll("[data-prepared-file]").Count).IsEqualTo(1);
    }

    [Test]
    public async Task SixHundredSettingAllSectionsMountStaysBoundedWithin1400DomElementsWithWindowingAndVirtualization()
    {
        var definition = CreateLargeAllSectionsDefinition(sectionCount: 6, fieldsPerSection: 100);
        await using var context = BrowserFixture.Context();
        context.JSInterop.SetupVoid("Blazor._internal.Virtualize.init", _ => true);
        var component = BrowserFixture.RenderWorkspace(context, definition);
        var panel = component.FindComponent<ConfigurationConfigurePanel>();

        await component.Find("[data-layout-mode='all-sections']").ClickAsync(new MouseEventArgs());

        // Every section landmark heading remains statically present for outline navigation.
        await Assert.That(component.FindAll("[data-section-panel]").Count).IsEqualTo(6);
        int domNodeCount = component.Find(".configuration-configure-panel__fields").QuerySelectorAll("*").Length;
        await Assert.That(domNodeCount).IsLessThanOrEqualTo(1400);

        // Distant section (sec4) is outside [0 ± 1] window initially, so no active field input is mounted inside it.
        await Assert.That(component.FindAll("[data-section-panel='sec4'] input")).IsEmpty();

        // Scalar rows render lightweight HTML5 ConfigurationSettingRow controls without MudBlazor input wrappers.
        await Assert.That(component.FindAll(".configuration-setting-row").Count).IsGreaterThan(0);
        await Assert.That(component.FindAll("[data-layout-active='all-sections'] .mud-input-control")).IsEmpty();

        // Windowing slides cleanly when scrollspy centers sec4, hydrating sec4 and unloading sec0.
        await panel.InvokeAsync(() => panel.Instance.OnScrollspySectionChangedAsync("sec4"));
        await Assert.That(component.FindAll("[data-section-panel='sec4'] input").Count).IsGreaterThan(0);
        await Assert.That(component.FindAll("[data-section-panel='sec0'] input")).IsEmpty();
        int scrolledDomCount = component.Find(".configuration-configure-panel__fields").QuerySelectorAll("*").Length;
        await Assert.That(scrolledDomCount).IsLessThanOrEqualTo(1400);
    }

    [Test]
    public async Task ZeroMatchFilterRendersZeroHiddenInertNodesShowsPrivacySafeDiagnosisAndResetsFiltersInOneClick()
    {
        var definition = CreateMultiSectionContinuousDefinition(fieldsPerSection: 2);
        await using var context = BrowserFixture.Context();
        var component = BrowserFixture.RenderWorkspace(context, definition);
        var form = component.FindComponents<ConfigurationField>()[0].Instance.Form;

        await component.Find("[data-layout-mode='all-sections']").ClickAsync(new MouseEventArgs());
        await component.InvokeAsync(() => form.EditValue(["generalField0"], "keep-me"));
        long revisionBefore = form.InputRevision;

        await component.InvokeAsync(() => form.SetDiscovery("unmatched-query-token"));
        await component.Find("[data-compact-matches-only]").ChangeAsync(new ChangeEventArgs { Value = true });

        // Filtered-out items consume zero hidden+inert DOM nodes.
        await Assert.That(component.FindAll("[hidden][inert][data-compact-slot], .configuration-section-fields__slot[hidden][inert]")).IsEmpty();
        await Assert.That(component.FindAll("[data-field='generalField0']")).IsEmpty();

        var emptyState = component.Find("[data-empty-matches-state]");
        await Assert.That(emptyState.GetAttribute("role")).IsEqualTo("status");
        await Assert.That(emptyState.TextContent).DoesNotContain("keep-me");

        // One-click Reset filters restores all fields cleanly without altering answers.
        await component.Find("[data-action='reset-discovery-filters']").ClickAsync(new MouseEventArgs());
        await Assert.That(form.DiscoveryQuery).IsEqualTo(string.Empty);
        await Assert.That(component.FindAll("[data-field='generalField0'] input").Count).IsEqualTo(1);
        await Assert.That(form.InputRevision).IsEqualTo(revisionBefore);
        await Assert.That(form.Snapshot().Values["generalField0"].GetString()).IsEqualTo("keep-me");
    }

    [Test]
    public async Task ContinuousOptionalDisclosureLabelsStayConsistentWithExpandedState()
    {
        JsonObject doc = JsonNode.Parse(CreateCompactDefinition().CanonicalBytes.Span)!.AsObject();
        doc["configuration"]!["properties"]!["options"] = JsonNode.Parse("""
            {
              "type": "object",
              "title": "Options",
              "additionalProperties": false,
              "required": ["requiredHost"],
              "x-configurator": { "sensitivity": "public", "detail": "basic" },
              "properties": {
                "requiredHost": { "type": "string", "title": "Required host", "x-configurator": { "sensitivity": "public", "detail": "basic" } },
                "note": { "type": "string", "title": "Optional note", "x-configurator": { "sensitivity": "public", "detail": "advanced" } }
              }
            }
            """);
        Definition definition = DefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(doc.ToJsonString()));
        await using var context = BrowserFixture.Context();
        var component = BrowserFixture.RenderWorkspace(context, definition);

        await component.Find("[data-layout-mode='all-sections']").ClickAsync(new MouseEventArgs());
        await component.Find("[data-field='options'] [data-action='supply-object']").ClickAsync(new MouseEventArgs());
        await Assert.That(component.FindAll("[data-field='options.note'] input").Count).IsEqualTo(1);

        var toggle = component.Find("[data-field='options'] [data-action='expand-optional']");
        await Assert.That(toggle.GetAttribute("aria-expanded")).IsEqualTo("true");
        await Assert.That(toggle.TextContent).DoesNotContain("Show");

        await toggle.ClickAsync(new MouseEventArgs());
        await Assert.That(component.FindAll("[data-field='options.note'] input")).IsEmpty();
        await Assert.That(component.Find("[data-field='options'] [data-action='expand-optional']").GetAttribute("aria-expanded")).IsEqualTo("false");
    }

    private static Definition CreateLargeAllSectionsDefinition(int sectionCount, int fieldsPerSection)
    {
        var properties = new JsonObject();
        var sections = new JsonArray();
        var sectionIds = new JsonArray();
        for (int s = 0; s < sectionCount; s++)
        {
            string secId = $"sec{s}";
            sectionIds.Add(secId);
            var pointers = new JsonArray();
            for (int f = 0; f < fieldsPerSection; f++)
            {
                string fieldId = $"s{s}_f{f}";
                properties[fieldId] = new JsonObject
                {
                    ["type"] = "string",
                    ["title"] = $"Section {s} Field {f}",
                    ["x-configurator"] = new JsonObject { ["sensitivity"] = "public", ["detail"] = "basic" }
                };
                pointers.Add("/" + fieldId);
            }
            sections.Add(new JsonObject
            {
                ["id"] = secId,
                ["title"] = $"Section {s}",
                ["detail"] = "basic",
                ["pointers"] = pointers
            });
        }
        var root = new JsonObject
        {
            ["schemaVersion"] = "0.0.1",
            ["profile"] = "bounded-v0.0.1",
            ["familyId"] = "f16a4e93-6e21-4c9a-a2a0-4810a1a0f101",
            ["revision"] = 1,
            ["project"] = new JsonObject { ["id"] = "scaled-continuous-test", ["publisher"] = "example", ["name"] = "Scaled Continuous Test" },
            ["applicationVersions"] = new JsonArray("1.0.0"),
            ["entryPoints"] = new JsonArray("setup"),
            ["locales"] = new JsonArray("en"),
            ["configuration"] = new JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = false,
                ["x-configurator"] = new JsonObject { ["sensitivity"] = "public" },
                ["properties"] = properties
            },
            ["presentation"] = new JsonObject
            {
                ["defaultJourney"] = "default",
                ["navigation"] = "sections",
                ["sections"] = sections,
                ["journeys"] = new JsonArray(new JsonObject
                {
                    ["id"] = "default",
                    ["title"] = "Default",
                    ["sections"] = sectionIds
                })
            },
            ["artifacts"] = JsonNode.Parse("""[{"id":"out","format":"json","fileName":"out.json","targetUpdateMode":"create","restartRequirement":"none","projection":{"op":"literal","value":{}}}]"""),
            ["requiredCapabilities"] = new JsonArray("projection.literal", "schema.object", "schema.string")
        };
        return DefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(root.ToJsonString()));
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
