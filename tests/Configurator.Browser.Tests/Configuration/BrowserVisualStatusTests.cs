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

        // Switch to Compact mode.
        await component.Find("[data-layout-mode='compact']").ClickAsync(new MouseEventArgs());
        await Assert.That(component.FindAll(".configuration-field--compact").Count).IsGreaterThan(0);

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

        // Switching back to Guided removes CompactPresentation markup.
        await component.Find("[data-layout-mode='guided']").ClickAsync(new MouseEventArgs());
        await Assert.That(component.FindAll("[data-compact-status]")).IsEmpty();
        await Assert.That(component.FindAll(".configuration-field--compact")).IsEmpty();
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
