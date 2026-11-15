namespace Configurator.Browser.Tests;

using System.Text;
using System.Text.Json.Nodes;
using Bunit;
using Configurator.Core.Definitions;
using Configurator.Presentation;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

public sealed class BrowserRichWidgetsAndGroupsTests
{
    [Test]
    public async Task RichMetadataAndWireCopyRespectDynamicPrivateBoundaryAndDoNotMaterializeValues()
    {
        var definition = CreateRichDefinition();
        await using var context = BrowserFixture.Context();
        var component = BrowserFixture.RenderWorkspace(context, definition);
        var form = component.FindComponents<ConfigurationField>()[0].Instance.Form;

        await Assert.That(component.Find("[data-field='timeoutMs'] [data-field-units]").TextContent).IsEqualTo("ms");
        await Assert.That(component.Find("[data-field='customScript'] textarea").GetAttribute("placeholder")).IsEqualTo("key: value");
        await Assert.That(component.Find("[data-field='customScript'] [data-if-omitted]").TextContent).Contains("Uses engine preset");
        await Assert.That(component.Find("[data-field='legacyMode'] [data-field-deprecated]").TextContent.Length).IsGreaterThan(0);
        await Assert.That(component.Find("[data-field='legacyMode'] [data-deprecation-guidance]").TextContent).Contains("Use providerMode instead.");
        await Assert.That(component.Find("[data-field='authMode'] [data-choice-explanation]").TextContent.Length).IsGreaterThan(0);

        await Assert.That(form.Snapshot().Values.ContainsKey("customScript")).IsFalse();
        await Assert.That(component.FindAll("[data-field='timeoutMs'] [data-action='copy-wire-key']").Count).IsEqualTo(1);

        await component.Find("[data-field='timeoutMs'] [data-action='copy-wire-key']").ClickAsync(new MouseEventArgs());
        await Assert.That(context.JSInterop.Invocations.Any(call => call.Identifier == "copy" ||
            call.Arguments.Any(arg => arg?.ToString()?.Contains("timeoutMs", StringComparison.Ordinal) == true))).IsTrue();

        await component.Find("#secret-consent").ChangeAsync(new ChangeEventArgs { Value = true });
        await component.Find("[data-field='privateMap'] input[id$='-new-key']").InputAsync(new ChangeEventArgs { Value = "MY_PRIVATE_VAR" });
        await component.Find("[data-field='privateMap'] [data-action='add-map-entry']").ClickAsync(new MouseEventArgs());
        await Assert.That(component.FindAll("[data-field='privateMap.MY_PRIVATE_VAR'] [data-action='copy-wire-key']")).IsEmpty();
    }

    [Test]
    public async Task RichWidgetsRenderMultilineCodeRadioSwitchAndSliderControls()
    {
        var definition = CreateRichDefinition();
        await using var context = BrowserFixture.Context();
        var component = BrowserFixture.RenderWorkspace(context, definition);
        var form = component.FindComponents<ConfigurationField>()[0].Instance.Form;

        var codeBox = component.Find("[data-field='customScript'] textarea");
        await Assert.That(codeBox.GetAttribute("rows")).IsEqualTo("6");
        await Assert.That(codeBox.GetAttribute("data-widget")).IsEqualTo("code");
        await Assert.That(codeBox.GetAttribute("data-syntax")).IsEqualTo("yaml");

        var noteBox = component.Find("[data-field='notes'] textarea");
        await Assert.That(noteBox.GetAttribute("rows")).IsEqualTo("4");
        await Assert.That(noteBox.GetAttribute("data-widget")).IsEqualTo("multiline");

        await Assert.That(component.FindAll("[data-field='authMode'] [role='radiogroup']").Count).IsEqualTo(1);
        await Assert.That(component.FindAll("[data-field='authMode'] [data-choice-explanation]").Count).IsEqualTo(2);

        var tlsSwitch = component.Find("[data-field='tlsEnabled'] [role='switch']");
        await Assert.That(tlsSwitch.GetAttribute("aria-checked")).IsEqualTo("mixed");
        await tlsSwitch.ChangeAsync(new ChangeEventArgs { Value = true });
        await Assert.That(form.Snapshot().Values["tlsEnabled"].GetBoolean()).IsTrue();
        await Assert.That(component.Find("[data-field='tlsEnabled'] [role='switch']").GetAttribute("aria-checked")).IsEqualTo("true");

        var slider = component.Find("[data-field='timeoutMs'] input[type='range']");
        await slider.ChangeAsync(new ChangeEventArgs { Value = "2500" });
        await Assert.That(form.Snapshot().Values["timeoutMs"].GetInt32()).IsEqualTo(2500);
    }

    [Test]
    public async Task ArrayRowsResolveDynamicTitlesAndFlagUniqueByDuplicates()
    {
        var definition = CreateRichDefinition();
        await using var context = BrowserFixture.Context();
        var component = BrowserFixture.RenderWorkspace(context, definition);

        for (int index = 0; index < 2; index++)
        {
            await component.Find("[data-field='tenants'] [data-action='add-item']").ClickAsync(new MouseEventArgs());
            await component.Find($"[data-field='tenants.{index}.region'] input").InputAsync(new ChangeEventArgs { Value = "eu" });
            await component.Find($"[data-field='tenants.{index}.slug'] input").InputAsync(new ChangeEventArgs { Value = "primary" });
        }

        await Assert.That(component.Find("[data-list-item-index='0'] [data-item-title]").TextContent).IsEqualTo("primary");
        await Assert.That(component.FindAll("[data-field='tenants'] [data-duplicate-key]").Count).IsGreaterThan(0);
    }

    [Test]
    public async Task CollapsibleGroupsAutoExpandOnErrorsAndToggleHealthyCollapsedGroups()
    {
        var definition = CreateRichDefinition();
        await using var context = BrowserFixture.Context();
        var component = BrowserFixture.RenderWorkspace(context, definition);

        await BrowserFixture.SectionNavigation(context, component).Find("[data-section='security']").ClickAsync(new MouseEventArgs());

        var errorGroup = component.Find("[data-group='error-group']");
        await Assert.That(errorGroup.GetAttribute("data-group-expanded")).IsEqualTo("true");
        await Assert.That(errorGroup.QuerySelector("[data-group-toggle]")!.GetAttribute("aria-expanded")).IsEqualTo("true");

        var healthyGroup = component.Find("[data-group='healthy-collapsed']");
        await Assert.That(healthyGroup.GetAttribute("data-group-expanded")).IsEqualTo("false");
        await Assert.That(healthyGroup.QuerySelector("[data-group-toggle]")!.GetAttribute("aria-expanded")).IsEqualTo("false");

        await healthyGroup.QuerySelector("[data-group-toggle]")!.ClickAsync(new MouseEventArgs());
        await Assert.That(component.Find("[data-group='healthy-collapsed']").GetAttribute("data-group-expanded")).IsEqualTo("true");
        await Assert.That(component.FindAll("[data-field='optionalNote']").Count).IsEqualTo(1);
    }

    [Test]
    public async Task UndoRedoAndSnackbarRestorePublicEditsAndPurgeSecretsOnRevocation()
    {
        var definition = CreateRichDefinition();
        await using var context = BrowserFixture.Context();
        var component = BrowserFixture.RenderWorkspace(context, definition);
        var form = component.FindComponents<ConfigurationField>()[0].Instance.Form;

        await component.Find("[data-field='notes'] textarea").InputAsync(new ChangeEventArgs { Value = "first note" });
        await component.Find("[data-field='notes'] textarea").InputAsync(new ChangeEventArgs { Value = "second note" });
        await Assert.That(form.Snapshot().Values["notes"].GetString()).IsEqualTo("second note");

        await component.Find(".configuration-editor").KeyDownAsync(new KeyboardEventArgs { Key = "z", CtrlKey = true });
        await Assert.That(form.Snapshot().Values["notes"].GetString()).IsEqualTo("first note");

        await component.Find(".configuration-editor").KeyDownAsync(new KeyboardEventArgs { Key = "Z", CtrlKey = true, ShiftKey = true });
        await Assert.That(form.Snapshot().Values["notes"].GetString()).IsEqualTo("second note");

        var omission = await BrowserFixture.OpenOmissionAsync(context, component, "notes");
        await omission.ClickAsync(new MouseEventArgs());
        await Assert.That(form.Snapshot().Values.ContainsKey("notes")).IsFalse();

        await component.Find("[data-snackbar-undo]").ClickAsync(new MouseEventArgs());
        await Assert.That(form.Snapshot().Values["notes"].GetString()).IsEqualTo("second note");

        await component.Find("#secret-consent").ChangeAsync(new ChangeEventArgs { Value = true });
        await BrowserFixture.SectionNavigation(context, component).Find("[data-section='security']").ClickAsync(new MouseEventArgs());
        await component.Find("[data-field='secretToken'] input").InputAsync(new ChangeEventArgs { Value = "synthetic-secret-entry-123456" });
        await Assert.That(form.Snapshot().Values["secretToken"].GetString()).IsEqualTo("synthetic-secret-entry-123456");
        await component.Find("#secret-consent").ChangeAsync(new ChangeEventArgs { Value = false });
        await Assert.That(form.Snapshot().Values.ContainsKey("secretToken")).IsFalse();
        await Assert.That(form.CanRedo).IsFalse();

        // Undo/Redo after consent revocation restores public edits without reviving the revoked secret.
        await component.Find(".configuration-editor").KeyDownAsync(new KeyboardEventArgs { Key = "z", CtrlKey = true });
        await Assert.That(form.Snapshot().Values["notes"].GetString()).IsEqualTo("first note");
        await Assert.That(form.Snapshot().Values.ContainsKey("secretToken")).IsFalse();
        await component.Find(".configuration-editor").KeyDownAsync(new KeyboardEventArgs { Key = "Z", CtrlKey = true, ShiftKey = true });
        await Assert.That(form.Snapshot().Values["notes"].GetString()).IsEqualTo("second note");
        await Assert.That(form.Snapshot().Values.ContainsKey("secretToken")).IsFalse();
    }

    [Test]
    public async Task CtrlKCommandPaletteFiltersAndExecutesCommandsAndQuestionMarkOpensShortcutsDialog()
    {
        var definition = CreateRichDefinition();
        await using var context = BrowserFixture.Context();
        var component = BrowserFixture.RenderWorkspace(context, definition);

        await component.Find(".configuration-editor").KeyDownAsync(new KeyboardEventArgs { Key = "k", CtrlKey = true });
        await Assert.That(component.FindAll("#workspace-search-results").Count).IsEqualTo(1);

        await component.Find("[data-discovery-filter='missing-required']").ClickAsync(new MouseEventArgs());
        await Assert.That(component.FindAll("[data-discovery-result='requiredHost']").Count).IsEqualTo(1);
        await Assert.That(component.FindAll("[data-discovery-result='timeoutMs']")).IsEmpty();

        await component.Find("[data-discovery-filter='deferred-secret']").ClickAsync(new MouseEventArgs());
        await Assert.That(component.FindAll("[data-discovery-result='secretToken']").Count).IsEqualTo(1);
        await Assert.That(component.FindAll("[data-discovery-result='requiredHost']")).IsEmpty();

        await component.Find("#configuration-search").InputAsync(new ChangeEventArgs { Value = "review" });
        await component.Find("[data-command-id='SwitchToReview']").ClickAsync(new MouseEventArgs());
        await Assert.That(component.FindAll("[data-workflow-panel='review']").Count).IsEqualTo(1);

        await component.Find(".configuration-editor").KeyDownAsync(new KeyboardEventArgs { Key = "?" });
        await Assert.That(component.FindAll("[data-shortcuts-dialog]").Count).IsEqualTo(1);
    }

    [Test]
    public async Task NavigatingToFieldInCollapsedGroupExpandsGroupAndPreservesTypedPresenceAndLogicalRowIdentity()
    {
        var definition = CreateRichDefinition();
        await using var context = BrowserFixture.Context();
        var component = BrowserFixture.RenderWorkspace(context, definition);
        var form = component.FindComponents<ConfigurationField>()[0].Instance.Form;

        await Assert.That(form.Snapshot().Values.ContainsKey("tlsEnabled")).IsFalse();
        await component.Find("[data-field='tlsEnabled'] [role='switch']").ChangeAsync(new ChangeEventArgs { Value = false });
        await Assert.That(form.Snapshot().Values["tlsEnabled"].GetBoolean()).IsFalse();
        await Assert.That(component.Find("[data-field='tlsEnabled'] [role='switch']").GetAttribute("aria-checked")).IsEqualTo("false");

        await Assert.That(form.Snapshot().Values.ContainsKey("tenants")).IsFalse();
        await component.Find("[data-field='tenants'] [data-action='supply-list']").ClickAsync(new MouseEventArgs());
        await Assert.That(form.Snapshot().Values["tenants"].GetArrayLength()).IsEqualTo(0);

        await component.Find("[data-field='tenants'] [data-action='add-item']").ClickAsync(new MouseEventArgs());
        await component.Find("[data-field='tenants.0.region'] input").InputAsync(new ChangeEventArgs { Value = "eu" });
        await component.Find("[data-field='tenants.0.slug'] input").InputAsync(new ChangeEventArgs { Value = "alpha" });
        await component.Find("[data-field='tenants'] [data-action='add-item']").ClickAsync(new MouseEventArgs());
        await component.Find("[data-field='tenants.1.region'] input").InputAsync(new ChangeEventArgs { Value = "us" });
        await component.Find("[data-field='tenants.1.slug'] input").InputAsync(new ChangeEventArgs { Value = "beta" });

        IReadOnlyList<long> beforeIds = form.GetListRowIdentities(["tenants"]);
        await Assert.That(beforeIds.Count).IsEqualTo(2);
        await component.Find("[data-list-item-index='0'] [data-action='remove-item']").ClickAsync(new MouseEventArgs());
        IReadOnlyList<long> afterIds = form.GetListRowIdentities(["tenants"]);
        await Assert.That(afterIds.Count).IsEqualTo(1);
        await Assert.That(afterIds[0]).IsEqualTo(beforeIds[1]);

        await component.InvokeAsync(() => form.FocusEditor(["optionalNote"]));
        await BrowserFixture.SectionNavigation(context, component).Find("[data-section='security']").ClickAsync(new MouseEventArgs());
        await Assert.That(component.Find("[data-group='healthy-collapsed']").GetAttribute("data-group-expanded")).IsEqualTo("true");
        await Assert.That(component.FindAll("[data-field='optionalNote']").Count).IsEqualTo(1);
    }

    private static Definition CreateRichDefinition() => DefinitionCodec.ParseJson(Encoding.UTF8.GetBytes("""
    {
      "schemaVersion": "0.0.1",
      "profile": "bounded-v0.0.1",
      "familyId": "a1b2c3d4-5b8d-4bb4-b7aa-5e400a2cd303",
      "revision": 1,
      "project": { "id": "rich-browser-ui", "publisher": "example", "name": "Rich Browser UI Test" },
      "applicationVersions": ["1.0.0"],
      "entryPoints": ["setup"],
      "locales": ["en"],
      "configuration": {
        "type": "object",
        "additionalProperties": false,
        "x-configurator": { "sensitivity": "public" },
        "properties": {
          "timeoutMs": {
            "type": "integer",
            "title": "Timeout",
            "minimum": 100,
            "maximum": 5000,
            "x-configurator": {
              "sensitivity": "public",
              "detail": "basic",
              "units": "ms",
              "widget": "slider"
            }
          },
          "customScript": {
            "type": "string",
            "title": "Custom script",
            "x-configurator": {
              "sensitivity": "public",
              "detail": "basic",
              "widget": "code",
              "rows": 6,
              "syntax": "yaml",
              "placeholder": "key: value",
              "defaultText": "Uses engine preset"
            }
          },
          "notes": {
            "type": "string",
            "title": "Operator notes",
            "x-configurator": {
              "sensitivity": "public",
              "detail": "basic",
              "widget": "multiline",
              "rows": 4
            }
          },
          "authMode": {
            "type": "string",
            "title": "Auth mode",
            "enum": ["local", "oidc"],
            "x-configurator": {
              "sensitivity": "public",
              "detail": "basic",
              "widget": "radio",
              "choices": [
                { "value": "local", "label": "Local", "explanation": "Stores hashed accounts locally." },
                { "value": "oidc", "label": "OIDC", "explanation": "Delegates sign-in to your identity provider." }
              ]
            }
          },
          "legacyMode": {
            "type": "string",
            "title": "Legacy mode",
            "deprecated": true,
            "enum": ["OLD", "NEW"],
            "x-configurator": {
              "sensitivity": "public",
              "detail": "basic",
              "deprecationMessage": "Use providerMode instead."
            }
          },
          "tlsEnabled": {
            "type": "boolean",
            "title": "Enable TLS",
            "x-configurator": {
              "sensitivity": "public",
              "detail": "basic",
              "widget": "switch",
              "booleanLabels": { "true": "Enforce TLS", "false": "Plaintext allowed" }
            }
          },
          "tenants": {
            "type": "array",
            "title": "Tenants",
            "x-configurator": {
              "sensitivity": "public",
              "detail": "basic",
              "itemTitlePointer": "/slug",
              "uniqueBy": ["/region", "/slug"]
            },
            "items": {
              "type": "object",
              "additionalProperties": false,
              "required": ["region", "slug"],
              "properties": {
                "region": { "type": "string", "title": "Region", "x-configurator": { "sensitivity": "public" } },
                "slug": { "type": "string", "title": "Slug", "x-configurator": { "sensitivity": "public" } }
              }
            }
          },
          "privateMap": {
            "type": "object",
            "title": "Private overrides",
            "x-configurator": { "sensitivity": "sensitive", "detail": "basic" },
            "additionalProperties": { "type": "string" }
          },
          "secretToken": {
            "type": "string",
            "title": "Secret token",
            "minLength": 16,
            "x-configurator": { "sensitivity": "secret", "detail": "basic" }
          },
          "requiredHost": {
            "type": "string",
            "title": "Required host",
            "x-configurator": { "sensitivity": "public", "detail": "basic" }
          },
          "optionalNote": {
            "type": "string",
            "title": "Optional tuning note",
            "x-configurator": { "sensitivity": "public", "detail": "basic" }
          }
        },
        "required": ["requiredHost", "secretToken"]
      },
      "presentation": {
        "defaultJourney": "main",
        "navigation": "sections",
        "sections": [
          {
            "id": "general",
            "title": "General",
            "detail": "basic",
            "pointers": ["/requiredHost", "/timeoutMs", "/customScript", "/notes", "/authMode", "/legacyMode", "/tlsEnabled", "/tenants", "/privateMap"]
          },
          {
            "id": "security",
            "title": "Security",
            "detail": "basic",
            "pointers": ["/secretToken", "/optionalNote"],
            "groups": [
              { "id": "error-group", "title": "Required Keys", "collapsed": true, "pointers": ["/secretToken"] },
              { "id": "healthy-collapsed", "title": "Tuning", "collapsed": true, "pointers": ["/optionalNote"] }
            ]
          }
        ],
        "journeys": [
          { "id": "main", "title": "Main", "sections": ["general", "security"] }
        ]
      },
      "artifacts": [
        { "id": "out", "format": "json", "fileName": "out.json", "targetUpdateMode": "create", "restartRequirement": "none", "projection": { "op": "literal", "value": {} } }
      ],
      "requiredCapabilities": [
        "projection.literal", "schema.array", "schema.boolean", "schema.bounds",
        "schema.enum", "schema.integer", "schema.map", "schema.object", "schema.string", "schema.uniqueBy"
      ]
    }
    """));
}
