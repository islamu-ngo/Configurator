namespace Configurator.Presentation.Tests;

using System.Text;
using Configurator.Core.Definitions;

public sealed class PresentationDualUxAndWidgetProjectionTests
{
    [Test]
    public async Task DualUxNamingAndWorkstream1MetadataProjectWithoutMaterializingDefaultText()
    {
        using var form = new DefinitionFormModel(RichMetadataDefinition(), DefinitionSessionPrivacy.NativeComplete);

        DefinitionEditorPresentation? script = form.GetEditorPresentation(["customScript"]);
        await Assert.That(script).IsNotNull();
        await Assert.That(script!.DisplayLabel).IsEqualTo("Custom Script");
        await Assert.That(script.WireToken).IsEqualTo("customScript");
        await Assert.That(script.Title).IsEqualTo("Custom Script");
        await Assert.That(script.Widget).IsEqualTo(DefinitionFieldWidget.Code);
        await Assert.That(script.Rows).IsEqualTo(8);
        await Assert.That(script.Syntax).IsEqualTo("yaml");
        await Assert.That(script.Placeholder).IsEqualTo("key: value");
        await Assert.That(script.DefaultText).IsEqualTo("Uses engine preset");
        await Assert.That(script.IfOmittedDisplay).IsEqualTo("If omitted: Uses engine preset");

        // Switching to Technical flips primary Title to WireToken without mutating answers/revision.
        long rev = form.InputRevision;
        form.SetWorkspaceNamingMode(WorkspaceNamingMode.Technical);
        await Assert.That(form.InputRevision).IsEqualTo(rev);
        DefinitionEditorPresentation technical = form.GetEditorPresentation(["customScript"])!;
        await Assert.That(technical.Title).IsEqualTo("customScript");
        await Assert.That(technical.SecondaryLabel).IsEqualTo("Custom Script");

        // defaultText never materializes into snapshot/answers.
        await Assert.That(form.Snapshot().Values.ContainsKey("customScript")).IsFalse();

        // Deprecation guidance on fields & choices + BooleanLabels + ItemTitlePointer + UniqueBy + OrderedPropertyNames.
        DefinitionEditorPresentation mode = form.GetEditorPresentation(["legacyMode"])!;
        await Assert.That(mode.IsDeprecated).IsTrue();
        await Assert.That(mode.DeprecationGuidance).IsEqualTo("Use providerMode instead.");
        await Assert.That(mode.Choices[0].IsDeprecated).IsTrue();
        await Assert.That(mode.Choices[0].DeprecationGuidance).IsEqualTo("Legacy option will be removed.");
        await Assert.That(mode.Choices[0].WireToken).IsEqualTo("LEGACY_ONE");

        DefinitionEditorPresentation toggle = form.GetEditorPresentation(["tlsEnabled"])!;
        await Assert.That(toggle.Widget).IsEqualTo(DefinitionFieldWidget.Switch);
        await Assert.That(toggle.BooleanLabels.TrueLabel).IsEqualTo("Enforce TLS");
        await Assert.That(toggle.BooleanLabels.FalseLabel).IsEqualTo("Plaintext allowed");

        DefinitionEditorPresentation rows = form.GetEditorPresentation(["tenants"])!;
        await Assert.That(rows.ItemTitlePointer).IsEqualTo("/slug");
        await Assert.That(rows.UniqueBy.Count).IsEqualTo(2);

        DefinitionEditorPresentation obj = form.GetEditorPresentation(["endpoint"])!;
        await Assert.That(string.Join(",", obj.OrderedPropertyNames)).IsEqualTo("host,port");

        // Controller impact summary.
        DefinitionEditorPresentation controller = form.GetEditorPresentation(["mode"])!;
        await Assert.That(controller.IsController).IsTrue();
        await Assert.That(controller.ControlledFieldCount).IsEqualTo(2);
        await Assert.That(controller.ControlledSectionCount).IsEqualTo(2);
        await Assert.That(controller.ControllerImpactSummary).IsEqualTo("Controls 2 settings across 2 sections");
    }

    [Test]
    public async Task ErrorInCollapsedGroupAutoExpandsGroupWithoutHidingErrors()
    {
        using var form = new DefinitionFormModel(RichMetadataDefinition(), DefinitionSessionPrivacy.NativeComplete);

        DefinitionFormSection security = form.Projection.Sections.Single(section => section.Id == "security");
        DefinitionFormSectionGroup collapsedHealthy = security.Groups.Single(group => group.Id == "healthy-collapsed");
        DefinitionFormSectionGroup collapsedRequired = security.Groups.Single(group => group.Id == "error-group");

        await Assert.That(collapsedHealthy.InitiallyCollapsed).IsTrue();
        await Assert.That(collapsedHealthy.IsExpanded).IsFalse();

        // Missing required leaf inside collapsed group auto-expands the group immediately.
        await Assert.That(collapsedRequired.InitiallyCollapsed).IsTrue();
        await Assert.That(collapsedRequired.HasBlockingIssue).IsTrue();
        await Assert.That(collapsedRequired.IsExpanded).IsTrue();

        form.EditValue(["tlsEnabled"], "true"); // reveals optionalBroken in healthy-collapsed
        form.EditValue(["optionalBroken"], "-5"); // violates minimum 1
        security = form.Projection.Sections.Single(section => section.Id == "security");
        collapsedHealthy = security.Groups.Single(group => group.Id == "healthy-collapsed");
        await Assert.That(collapsedHealthy.HasBlockingIssue).IsTrue();
        await Assert.That(collapsedHealthy.IsExpanded).IsTrue();
    }

    [Test]
    public async Task CompactPropertyRowEnforcesBrowserNoSecretsAndExcludesPrivateValuesFromSearch()
    {
        using var form = new DefinitionFormModel(RichMetadataDefinition(), DefinitionSessionPrivacy.BrowserNoSecrets);

        IReadOnlyList<CompactPropertyRow> rows = form.GetCompactRows();
        CompactPropertyRow secretRow = rows.Single(row => row.WireToken == "secretToken");
        await Assert.That(secretRow.IsSecretLocked).IsTrue();
        await Assert.That(secretRow.ValuePreview).IsEqualTo("");

        // Enable consent, set secret, and verify preview is masked & excluded from query matching.
        form.SetSecretEntryEnabled(true);
        form.EditValue(["secretToken"], "top-secret-password-value-9999");

        rows = form.GetCompactRows();
        secretRow = rows.Single(row => row.WireToken == "secretToken");
        await Assert.That(secretRow.IsSecretLocked).IsFalse();
        await Assert.That(secretRow.ValuePreview).IsEqualTo("••••••••");
        await Assert.That(secretRow.MatchesQuery("top-secret-password")).IsFalse();
        await Assert.That(secretRow.MatchesQuery("secretToken")).IsTrue();

        // Private map key is redacted from wire token & search/sort.
        form.AddMapEntry(["privateMap"], "hidden-customer-key");
        CompactPropertyRow mapEntryRow = form.GetCompactRows().Single(row => row.DataPath.Segments.Contains("privateMap") && row.DataPath.Segments.Count == 2);
        await Assert.That(mapEntryRow.MatchesQuery("hidden-customer-key")).IsFalse();
        await Assert.That(mapEntryRow.SortKey.Contains("hidden-customer-key", StringComparison.Ordinal)).IsFalse();
    }

    [Test]
    public async Task DiscoveryFilterAndWorkspaceCommandsOperateStrictlyOverMetadata()
    {
        using var form = new DefinitionFormModel(RichMetadataDefinition(), DefinitionSessionPrivacy.BrowserSecretsEnabled);
        form.EditValue(["tlsEnabled"], "true");
        form.EditValue(["optionalBroken"], "-10");

        form.SetDiscoverySelection(new DefinitionDiscoverySelection(filter: DefinitionDiscoveryFilter.DeferredSecret));
        await Assert.That(form.Projection.Matches.Any(m => m.Entry.FieldId == "secretToken")).IsTrue();

        form.EditValue(["mode"], "advanced");

        form.SetDiscoverySelection(new DefinitionDiscoverySelection(filter: DefinitionDiscoveryFilter.Invalid));
        await Assert.That(form.Projection.Matches.Any(m => m.Entry.FieldId == "optionalBroken")).IsTrue();
        await Assert.That(form.Projection.Matches.Any(m => m.Entry.FieldId == "mode")).IsFalse();

        form.SetDiscoverySelection(new DefinitionDiscoverySelection(filter: DefinitionDiscoveryFilter.Modified));
        await Assert.That(form.Projection.Matches.Any(m => m.Entry.FieldId == "mode")).IsTrue();

        IReadOnlyList<WorkspaceCommandDescriptor> commands = form.GetAvailableCommands("compact");
        await Assert.That(commands.Any(command => command.Id == WorkspaceCommandId.SwitchToCompact)).IsTrue();
    }

    [Test]
    public async Task FocusEditorExpandsObstructingGroupAndRejectsStaleCorrectionRevisions()
    {
        using var form = new DefinitionFormModel(RichMetadataDefinition(), DefinitionSessionPrivacy.NativeComplete);
        form.EditValue(["tlsEnabled"], "true");

        DefinitionFormSection security = form.Projection.Sections.Single(section => section.Id == "security");
        DefinitionFormSectionGroup collapsedHealthy = security.Groups.Single(group => group.Id == "healthy-collapsed");
        await Assert.That(collapsedHealthy.IsExpanded).IsFalse();

        bool focused = form.FocusEditor(["optionalBroken"]);
        await Assert.That(focused).IsTrue();

        security = form.Projection.Sections.Single(section => section.Id == "security");
        collapsedHealthy = security.Groups.Single(group => group.Id == "healthy-collapsed");
        await Assert.That(collapsedHealthy.IsExpanded).IsTrue();

        long staleRevision = form.InputRevision;
        var staleLocation = new DefinitionCorrectionLocation(staleRevision, []);
        form.EditValue(["optionalBroken"], "42");
        await Assert.That(form.FocusCorrection("optionalBroken", staleLocation)).IsFalse();
    }

    private static Definition RichMetadataDefinition() => DefinitionCodec.ParseJson(Encoding.UTF8.GetBytes("""
    {
      "schemaVersion": "0.0.1",
      "profile": "bounded-v0.0.1",
      "familyId": "1c0dc1a0-5b8d-4bb4-b7aa-5e400a2cd303",
      "revision": 1,
      "project": { "id": "rich-ui", "publisher": "example", "name": "Rich UI Test" },
      "applicationVersions": ["1.0.0"],
      "entryPoints": ["setup"],
      "locales": ["en"],
      "configuration": {
        "type": "object",
        "additionalProperties": false,
        "x-configurator": { "sensitivity": "public" },
        "properties": {
          "mode": {
            "type": "string",
            "enum": ["standard", "advanced"],
            "x-configurator": { "sensitivity": "public", "detail": "basic", "initialValue": "standard" }
          },
          "customScript": {
            "type": "string",
            "x-configurator": {
              "sensitivity": "public",
              "detail": "basic",
              "widget": "code",
              "rows": 8,
              "syntax": "yaml",
              "placeholder": "key: value",
              "defaultText": "Uses engine preset"
            }
          },
          "legacyMode": {
            "type": "string",
            "deprecated": true,
            "enum": ["LEGACY_ONE", "MODERN"],
            "x-configurator": {
              "sensitivity": "public",
              "detail": "basic",
              "deprecationMessage": "Use providerMode instead.",
              "choices": [
                { "value": "LEGACY_ONE", "deprecated": true, "deprecationMessage": "Legacy option will be removed." },
                { "value": "MODERN" }
              ]
            }
          },
          "tlsEnabled": {
            "type": "boolean",
            "x-configurator": {
              "sensitivity": "public",
              "detail": "basic",
              "widget": "switch",
              "booleanLabels": { "true": "Enforce TLS", "false": "Plaintext allowed" }
            }
          },
          "tenants": {
            "type": "array",
            "x-configurator": {
              "sensitivity": "public",
              "detail": "standard",
              "itemTitlePointer": "/slug",
              "uniqueBy": ["/region", "/slug"]
            },
            "items": {
              "type": "object",
              "additionalProperties": false,
              "required": ["region", "slug"],
              "properties": {
                "region": { "type": "string", "x-configurator": { "sensitivity": "public" } },
                "slug": { "type": "string", "x-configurator": { "sensitivity": "public" } }
              }
            }
          },
          "endpoint": {
            "type": "object",
            "additionalProperties": false,
            "x-configurator": { "sensitivity": "public", "detail": "standard" },
            "properties": {
              "host": { "type": "string" },
              "port": { "type": "integer" }
            }
          },
          "secretToken": {
            "type": "string",
            "minLength": 16,
            "x-configurator": {
              "sensitivity": "secret",
              "detail": "basic",
              "secret": { "kind": "opaque-token", "acquisition": "local", "encoding": "base64url" },
              "applicableWhen": { "op": "equals", "reference": { "scope": "root", "path": "/mode" }, "value": "standard" }
            }
          },
          "optionalBroken": {
            "type": "integer",
            "minimum": 1,
            "x-configurator": {
              "sensitivity": "public",
              "detail": "basic",
              "applicableWhen": { "op": "equals", "reference": { "scope": "root", "path": "/tlsEnabled" }, "value": true }
            }
          },
          "enterpriseAudit": {
            "type": "string",
            "x-configurator": {
              "sensitivity": "public",
              "detail": "basic",
              "applicableWhen": { "op": "equals", "reference": { "scope": "root", "path": "/mode" }, "value": "advanced" }
            }
          },
          "privateMap": {
            "type": "object",
            "x-configurator": { "sensitivity": "sensitive", "detail": "standard" },
            "additionalProperties": { "type": "string" }
          }
        },
        "required": ["secretToken"]
      },
      "presentation": {
        "defaultJourney": "main",
        "navigation": "sections",
        "sections": [
          { "id": "general", "title": "General", "detail": "basic", "pointers": ["/mode", "/customScript", "/legacyMode", "/tlsEnabled", "/tenants", "/endpoint", "/privateMap"] },
          {
            "id": "security",
            "title": "Security",
            "detail": "basic",
            "pointers": ["/secretToken", "/optionalBroken"],
            "groups": [
              { "id": "error-group", "title": "Required Keys", "collapsed": true, "pointers": ["/secretToken"] },
              { "id": "healthy-collapsed", "title": "Tuning", "collapsed": true, "pointers": ["/optionalBroken"] }
            ]
          },
          { "id": "compliance", "title": "Compliance", "detail": "basic", "pointers": ["/enterpriseAudit"] }
        ],
        "journeys": [
          { "id": "main", "title": "Main", "sections": ["general", "security", "compliance"] }
        ]
      },
      "artifacts": [
        { "id": "out", "format": "json", "fileName": "out.json", "targetUpdateMode": "create", "restartRequirement": "none", "projection": { "op": "literal", "value": {} } }
      ],
      "requiredCapabilities": [
        "predicate.root", "projection.literal", "schema.array", "schema.boolean", "schema.bounds",
        "schema.enum", "schema.integer", "schema.map", "schema.object", "schema.secret", "schema.string", "schema.uniqueBy", "values.initial"
      ]
    }
    """));
}
