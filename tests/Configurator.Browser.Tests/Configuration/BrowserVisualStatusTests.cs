namespace Configurator.Browser.Tests;

using System.Text;
using Bunit;
using Configurator.Core.Definitions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

public sealed class BrowserVisualStatusTests
{
    [Test]
    public async Task CompactPresentationExposesAccessibleVisualStateAndOriginWhileGuidedRetainsUnchangedMarkup()
    {
        var definition = CreateVisualStatusDefinition();
        await using var context = BrowserFixture.Context();
        var component = BrowserFixture.RenderWorkspace(context, definition);

        // In Guided mode, CompactPresentation status indicators are absent.
        await Assert.That(component.FindAll("[data-compact-status]")).IsEmpty();
        await Assert.That(component.FindAll(".configuration-field--compact")).IsEmpty();

        // Switch to All sections layout.
        await component.Find("[data-layout-mode='all-sections']").ClickAsync(new MouseEventArgs());
        await Assert.That(component.FindAll(".configuration-setting-row").Count).IsGreaterThan(0);

        // 1. Missing required state
        var requiredStatus = component.Find("[data-field='requiredHost'] [data-compact-status]");
        await Assert.That(requiredStatus.GetAttribute("data-field-state")).IsEqualTo("missing-required");
        await Assert.That(requiredStatus.GetAttribute("data-field-origin")).IsEqualTo("omitted");
        await Assert.That(string.IsNullOrWhiteSpace(requiredStatus.GetAttribute("aria-label"))).IsFalse();

        // 2. Defaulted / initialized state
        var defaultedStatus = component.Find("[data-field='defaultedPort'] [data-compact-status]");
        await Assert.That(defaultedStatus.GetAttribute("data-field-state")).IsEqualTo("defaulted");
        await Assert.That(defaultedStatus.GetAttribute("data-field-origin")).IsEqualTo("defaulted");
        await Assert.That(string.IsNullOrWhiteSpace(defaultedStatus.GetAttribute("aria-label"))).IsFalse();

        // 3. Omitted optional state
        var omittedStatus = component.Find("[data-field='optionalNote'] [data-compact-status]");
        await Assert.That(omittedStatus.GetAttribute("data-field-state")).IsEqualTo("omitted");
        await Assert.That(omittedStatus.GetAttribute("data-field-origin")).IsEqualTo("omitted");

        // 4. Secret locked state
        var lockedStatus = component.Find("[data-field='secretKey'] [data-compact-status]");
        await Assert.That(lockedStatus.GetAttribute("data-field-state")).IsEqualTo("secret-locked");
        await Assert.That(string.IsNullOrWhiteSpace(lockedStatus.GetAttribute("aria-label"))).IsFalse();

        // 5. Supplied state after editing requiredHost
        await component.Find("[data-field='requiredHost'] input").InputAsync(new ChangeEventArgs { Value = "visual.example" });
        var suppliedStatus = component.Find("[data-field='requiredHost'] [data-compact-status]");
        await Assert.That(suppliedStatus.GetAttribute("data-field-state")).IsEqualTo("supplied");
        await Assert.That(suppliedStatus.GetAttribute("data-field-origin")).IsEqualTo("explicit");

        // 6. Invalid state with readable inline error text
        await component.Find("[data-field='defaultedPort'] input").InputAsync(new ChangeEventArgs { Value = "not-a-number" });
        var invalidStatus = component.Find("[data-field='defaultedPort'] [data-compact-status]");
        await Assert.That(invalidStatus.GetAttribute("data-field-state")).IsEqualTo("invalid");
        await Assert.That(string.IsNullOrWhiteSpace(invalidStatus.GetAttribute("aria-label"))).IsFalse();

        // 7. Section heading concise count badge with accessible label and zero fabricated percentages
        var sectionCount = component.Find("[data-compact-section='general'] [data-compact-section-count]");
        await Assert.That(string.IsNullOrWhiteSpace(sectionCount.GetAttribute("aria-label"))).IsFalse();
        await Assert.That(sectionCount.TextContent).DoesNotContain("%");

        // Switching back to Section by section removes AllSections row markup.
        await component.Find("[data-layout-mode='section-by-section']").ClickAsync(new MouseEventArgs());
        await Assert.That(component.FindAll("[data-compact-status]")).IsEmpty();
        await Assert.That(component.FindAll(".configuration-setting-row")).IsEmpty();
    }

    [Test]
    public async Task OptionalValidationErrorPreventsReadyForExportBadgeAndReservedStatusSlotPreventsLayoutShift()
    {
        var definition = CreateVisualStatusDefinition();
        await using var context = BrowserFixture.Context();
        var component = BrowserFixture.RenderWorkspace(context, definition);
        var form = component.FindComponents<ConfigurationField>()[0].Instance.Form;

        // Reserved status slot renders consistently before first edit to prevent layout shift (CLS = 0).
        var statusSlot = component.Find("[data-status-slot]");
        await Assert.That(statusSlot.GetAttribute("data-status-slot")).IsEqualTo("reserved");
        await Assert.That(statusSlot.QuerySelector("[role='status']")!.ClassList.Contains("sr-only")).IsFalse();

        // Satisfy all required fields, then enter an invalid draft on an optional field (defaultedPort).
        await component.Find("#secret-consent").ChangeAsync(new ChangeEventArgs { Value = true });
        await component.Find("[data-field='requiredHost'] input").InputAsync(new ChangeEventArgs { Value = "ready.example.org" });
        await component.Find("[data-field='secretKey'] input").InputAsync(new ChangeEventArgs { Value = "synthetic-valid-secret" });
        await component.Find("[data-field='defaultedPort'] input").InputAsync(new ChangeEventArgs { Value = "not-a-valid-port" });

        await Assert.That(form.Projection.ProgressSummary.MissingRequired).IsEqualTo(0);
        await Assert.That(form.Projection.ProgressSummary.OptionalInvalidCount).IsGreaterThan(0);
        await Assert.That(form.Projection.ProgressSummary.IsReadyForExport).IsFalse();

        var progress = component.Find(".configuration-editor__progress");
        await Assert.That(progress.GetAttribute("data-ready-state")).IsEqualTo("issues");
        await Assert.That(progress.TextContent).Contains("1");
    }

    [Test]
    public async Task ErrorSummaryAndFunctionKeysFocusDeepVirtualizedRowInHundredRowSection()
    {
        var properties = new System.Text.Json.Nodes.JsonObject();
        var pointers0 = new System.Text.Json.Nodes.JsonArray();
        for (int index = 0; index < 100; index++)
        {
            string id = $"s0_f{index}";
            properties[id] = new System.Text.Json.Nodes.JsonObject
            {
                ["type"] = index == 86 ? "integer" : "string",
                ["title"] = $"Row {index}",
                ["x-configurator"] = new System.Text.Json.Nodes.JsonObject { ["sensitivity"] = "public", ["detail"] = "basic" }
            };
            pointers0.Add("/" + id);
        }
        properties["s1_f0"] = new System.Text.Json.Nodes.JsonObject
        {
            ["type"] = "string",
            ["title"] = "Section 1 Setting",
            ["x-configurator"] = new System.Text.Json.Nodes.JsonObject { ["sensitivity"] = "public", ["detail"] = "basic" }
        };
        var root = new System.Text.Json.Nodes.JsonObject
        {
            ["schemaVersion"] = "0.0.1",
            ["profile"] = "bounded-v0.0.1",
            ["familyId"] = "f26a4e93-6e21-4c9a-a2a0-4810a1a0f102",
            ["revision"] = 1,
            ["project"] = new System.Text.Json.Nodes.JsonObject { ["id"] = "s06-error-summary", ["publisher"] = "example", ["name"] = "S06 Test" },
            ["applicationVersions"] = new System.Text.Json.Nodes.JsonArray("1.0.0"),
            ["entryPoints"] = new System.Text.Json.Nodes.JsonArray("setup"),
            ["locales"] = new System.Text.Json.Nodes.JsonArray("en"),
            ["configuration"] = new System.Text.Json.Nodes.JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = false,
                ["x-configurator"] = new System.Text.Json.Nodes.JsonObject { ["sensitivity"] = "public" },
                ["required"] = new System.Text.Json.Nodes.JsonArray("s0_f85"),
                ["properties"] = properties
            },
            ["presentation"] = new System.Text.Json.Nodes.JsonObject
            {
                ["defaultJourney"] = "default",
                ["navigation"] = "sections",
                ["sections"] = new System.Text.Json.Nodes.JsonArray(
                    new System.Text.Json.Nodes.JsonObject { ["id"] = "sec0", ["title"] = "Section 0", ["detail"] = "basic", ["pointers"] = pointers0 },
                    new System.Text.Json.Nodes.JsonObject { ["id"] = "sec1", ["title"] = "Section 1", ["detail"] = "basic", ["pointers"] = new System.Text.Json.Nodes.JsonArray("/s1_f0") }),
                ["journeys"] = new System.Text.Json.Nodes.JsonArray(
                    new System.Text.Json.Nodes.JsonObject { ["id"] = "default", ["title"] = "Default", ["sections"] = new System.Text.Json.Nodes.JsonArray("sec0", "sec1") })
            },
            ["artifacts"] = System.Text.Json.Nodes.JsonNode.Parse("""[{"id":"out","format":"json","fileName":"out.json","targetUpdateMode":"create","restartRequirement":"none","projection":{"op":"literal","value":{}}}]"""),
            ["requiredCapabilities"] = new System.Text.Json.Nodes.JsonArray("projection.literal", "schema.integer", "schema.object", "schema.string")
        };
        Definition definition = DefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(root.ToJsonString()));
        await using var context = BrowserFixture.Context();
        context.JSInterop.SetupVoid("Blazor._internal.Virtualize.init", _ => true);
        var component = BrowserFixture.RenderWorkspace(context, definition);
        var form = component.FindComponents<ConfigurationField>()[0].Instance.Form;

        await component.Find("[data-layout-mode='all-sections']").ClickAsync(new MouseEventArgs());
        await component.InvokeAsync(() => form.EditValue(["s0_f86"], "not-an-integer"));

        // Validation / preparation exposes the inline ErrorSummary banner above the workspace.
        await BrowserFixture.PrepareAsync(component, draft: false);
        await component.Find("[data-action='recover-status']").ClickAsync(new MouseEventArgs());
        var errorSummary = component.Find("[data-error-summary]");
        await Assert.That(errorSummary.QuerySelectorAll("[data-error-summary-item]").Length).IsGreaterThan(0);

        // Clicking an ErrorSummary item promotes & focuses deep row #86 across virtualization boundary.
        await errorSummary.QuerySelector("[data-error-summary-item='s0_f86']")!.ClickAsync(new MouseEventArgs());
        await Assert.That(component.FindAll("[data-pinned-focus-slot='s0_f86'] [data-field='s0_f86'] input").Count).IsEqualTo(1);

        // F8 jumps to next required row (#85) inside the 100-row virtualized section.
        await component.Find(".configuration-editor").KeyDownAsync(new KeyboardEventArgs { Key = "F8" });
        await Assert.That(component.FindAll("[data-pinned-focus-slot='s0_f85'] [data-field='s0_f85'] input").Count).IsEqualTo(1);

        // F7 jumps to next blocking error row (#86) inside the 100-row virtualized section.
        await component.Find(".configuration-editor").KeyDownAsync(new KeyboardEventArgs { Key = "F7" });
        await Assert.That(component.FindAll("[data-pinned-focus-slot='s0_f86'] [data-field='s0_f86'] input").Count).IsEqualTo(1);

        // F9 advances to the next unvisited section (sec1).
        await component.Find(".configuration-editor").KeyDownAsync(new KeyboardEventArgs { Key = "F9" });
        await Assert.That(form.CurrentSectionId).IsEqualTo("sec1");
    }

    [Test]
    public async Task MinorityRequiredBadgesUniversalSectionHeaderAndStripeChromeRemoveOptionalNoise()
    {
        var definition = CreateVisualStatusDefinition();
        await using var context = BrowserFixture.Context();
        var component = BrowserFixture.RenderWorkspace(context, definition);

        // Phase 4 deletes legacy CompactPresentation parameter from ConfigurationField.
        await Assert.That(typeof(ConfigurationField).GetProperty("CompactPresentation")).IsNull();

        await component.Find("[data-layout-mode='all-sections']").ClickAsync(new MouseEventArgs());

        // Mixed section: Required badge appears ONLY on required rows, zero "Optional" pills or "Not set" noise.
        await Assert.That(component.FindAll("[data-field='requiredHost'] .configuration-field__requirement").Count).IsEqualTo(1);
        await Assert.That(component.FindAll("[data-field='optionalNote'] .configuration-field__requirement")).IsEmpty();
        await Assert.That(component.FindAll("[data-field='defaultedPort'] .configuration-field__requirement")).IsEmpty();
        await Assert.That(component.Find("[data-field='optionalNote'] [data-compact-status]").ClassList.Contains("sr-only")).IsTrue();

        // Universal-required section renders header indicator once and omits per-row Required pills.
        var allReqRoot = System.Text.Json.Nodes.JsonNode.Parse(definition.CanonicalBytes.Span)!.AsObject();
        allReqRoot["configuration"]!["required"] = new System.Text.Json.Nodes.JsonArray("requiredHost", "defaultedPort", "optionalNote", "secretKey");
        Definition allRequiredDef = DefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(allReqRoot.ToJsonString()));
        await using var ctx2 = BrowserFixture.Context();
        var comp2 = BrowserFixture.RenderWorkspace(ctx2, allRequiredDef);
        await comp2.Find("[data-layout-mode='all-sections']").ClickAsync(new MouseEventArgs());
        await Assert.That(comp2.FindAll("[data-section-requirement='all-required']").Count).IsEqualTo(1);
        await Assert.That(comp2.FindAll("[data-field='requiredHost'] .configuration-field__requirement")).IsEmpty();
    }

    private static Definition CreateVisualStatusDefinition() => DefinitionCodec.ParseJson(Encoding.UTF8.GetBytes("""
    {
      "schemaVersion": "0.0.1",
      "profile": "bounded-v0.0.1",
      "familyId": "9f84708c-227a-42db-a9a2-f5870a1eb404",
      "revision": 1,
      "project": { "id": "visual-status-test", "publisher": "example", "name": "Visual Status Test" },
      "applicationVersions": ["1.0.0"],
      "entryPoints": ["setup"],
      "locales": ["en"],
      "configuration": {
        "type": "object",
        "additionalProperties": false,
        "x-configurator": { "sensitivity": "public" },
        "required": ["requiredHost", "secretKey"],
        "properties": {
          "requiredHost": {
            "type": "string",
            "title": "Required Host",
            "x-configurator": { "sensitivity": "public", "detail": "basic" }
          },
          "defaultedPort": {
            "type": "integer",
            "title": "Defaulted Port",
            "minimum": 1,
            "maximum": 65535,
            "x-configurator": { "sensitivity": "public", "detail": "basic", "initialValue": 8080 }
          },
          "optionalNote": {
            "type": "string",
            "title": "Optional Note",
            "x-configurator": { "sensitivity": "public", "detail": "basic" }
          },
          "secretKey": {
            "type": "string",
            "title": "Secret Key",
            "x-configurator": {
              "sensitivity": "secret",
              "detail": "basic",
              "secret": { "kind": "password", "acquisition": "local" }
            }
          }
        }
      },
      "presentation": {
        "defaultJourney": "configure",
        "navigation": "sections",
        "sections": [
          {
            "id": "general",
            "title": "General",
            "detail": "basic",
            "pointers": ["/requiredHost", "/defaultedPort", "/optionalNote", "/secretKey"]
          }
        ],
        "journeys": [
          { "id": "configure", "title": "Configure", "sections": ["general"] }
        ]
      },
      "artifacts": [
        { "id": "out", "format": "json", "fileName": "out.json", "targetUpdateMode": "create", "restartRequirement": "none", "projection": { "op": "literal", "value": {} } }
      ],
      "requiredCapabilities": [
        "projection.literal", "schema.bounds", "schema.integer", "schema.object", "schema.secret", "schema.string", "values.initial"
      ]
    }
    """));
}
