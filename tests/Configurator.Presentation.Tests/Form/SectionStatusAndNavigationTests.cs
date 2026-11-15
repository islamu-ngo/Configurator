namespace Configurator.Presentation.Tests;

using System.Text;
using Configurator.Core.Definitions;

public sealed class SectionStatusAndNavigationTests
{
    [Test]
    public async Task MultiSegmentPointersSeparateNestedSectionOwnershipWithoutDoubleCounting()
    {
        using var form = new DefinitionFormModel(SplitPointerWizardDefinition(), DefinitionSessionPrivacy.BrowserSecretsEnabled);

        await Assert.That(form.Projection.SectionStatuses["db-conn"].Progress.Required).IsEqualTo(1);
        await Assert.That(form.Projection.SectionStatuses["db-conn"].Progress.Missing).IsEqualTo(1);
        await Assert.That(form.Projection.SectionStatuses["db-sec"].Progress.Required).IsEqualTo(1);
        await Assert.That(form.Projection.SectionStatuses["db-sec"].Progress.Missing).IsEqualTo(1);
        await Assert.That(form.Projection.SectionStatuses["db-sec"].ConfigurationStatus)
            .IsEqualTo(SectionConfigurationStatus.SecretDeferred);
        await Assert.That(form.Projection.SectionStatuses["db-conn"].ConfigurationStatus)
            .IsEqualTo(SectionConfigurationStatus.MissingRequired);

        // Editing only /database/host completes db-conn without marking db-sec complete or double-counting.
        form.EditValue(DefinitionPath.FromSegments(["database", "host"]), "db.local");

        await Assert.That(form.Projection.SectionStatuses["db-conn"].ConfigurationStatus)
            .IsEqualTo(SectionConfigurationStatus.RequiredComplete);
        await Assert.That(form.Projection.SectionStatuses["db-conn"].Progress.Ready).IsEqualTo(1);
        await Assert.That(form.Projection.SectionStatuses["db-sec"].ConfigurationStatus)
            .IsEqualTo(SectionConfigurationStatus.SecretDeferred);
    }

    [Test]
    public async Task SectionStatusPrioritizesInvalidOptionalOverOptionalUnsetAndSeparatesCoverage()
    {
        using var form = new DefinitionFormModel(SplitPointerWizardDefinition());

        SectionStatusProjection optStatus = form.Projection.SectionStatuses["optional-sec"];
        await Assert.That(optStatus.ConfigurationStatus).IsEqualTo(SectionConfigurationStatus.OptionalUnset);
        await Assert.That(optStatus.Coverage).IsEqualTo(SectionReviewCoverage.NotVisited);

        // Invalid optional draft immediately elevates status to Invalid without altering review coverage.
        form.EditValue(["optionalPort"], "not-an-integer");
        await Assert.That(form.Projection.SectionStatuses["optional-sec"].ConfigurationStatus)
            .IsEqualTo(SectionConfigurationStatus.Invalid);
        await Assert.That(form.Projection.SectionStatuses["optional-sec"].Coverage)
            .IsEqualTo(SectionReviewCoverage.NotVisited);

        long baselineRevision = form.InputRevision;
        int viewNotifications = 0;
        form.ViewChanged += (_, _) => viewNotifications++;

        await Assert.That(form.RecordSectionOpened("optional-sec")).IsTrue();
        await Assert.That(form.Projection.SectionStatuses["optional-sec"].Coverage)
            .IsEqualTo(SectionReviewCoverage.Opened);
        await Assert.That(form.InputRevision).IsEqualTo(baselineRevision);
        await Assert.That(viewNotifications).IsEqualTo(1);

        await Assert.That(form.SetSectionReviewed("optional-sec")).IsTrue();
        await Assert.That(form.Projection.SectionStatuses["optional-sec"].Coverage)
            .IsEqualTo(SectionReviewCoverage.Reviewed);
        await Assert.That(form.InputRevision).IsEqualTo(baselineRevision);
        await Assert.That(viewNotifications).IsEqualTo(2);

        // Exposing a newly active field inside reviewed section downgrades Reviewed -> Opened + flags NewlyRelevant.
        form.EditValue(["enableExtraIdentity"], "true");
        SectionStatusProjection downgraded = form.Projection.SectionStatuses["identity-sec"];
        // Before identity-sec is reviewed, it stays NotVisited; test review then newly relevant in identity-sec:
        form.EditValue(["enableExtraIdentity"], "false");
        form.SetSectionReviewed("identity-sec");
        await Assert.That(form.Projection.SectionStatuses["identity-sec"].Coverage)
            .IsEqualTo(SectionReviewCoverage.Reviewed);
        await Assert.That(form.Projection.SectionStatuses["identity-sec"].HasNewlyRelevantSettings).IsFalse();

        form.EditValue(["enableExtraIdentity"], "true");
        downgraded = form.Projection.SectionStatuses["identity-sec"];
        await Assert.That(downgraded.Coverage).IsEqualTo(SectionReviewCoverage.Opened);
        await Assert.That(downgraded.HasNewlyRelevantSettings).IsTrue();
    }

    [Test]
    public async Task HiddenOptionalFieldsAndDualDenominatorsUpdateWithoutAdvancingInputRevision()
    {
        using var form = new DefinitionFormModel(SplitPointerWizardDefinition());
        form.SetDisclosureOverride(FieldDisclosure.Essential);

        SectionStatusProjection optStatus = form.Projection.SectionStatuses["optional-sec"];
        await Assert.That(optStatus.AvailableOptionalCount).IsGreaterThan(0);
        await Assert.That(optStatus.HiddenOptionalTitles.Contains("Optional Port")).IsTrue();

        WorkspaceProgressSummary summary = form.Projection.ProgressSummary;
        await Assert.That(summary.TotalRequired).IsEqualTo(2);
        await Assert.That(summary.OpenedSections).IsEqualTo(0);
        await Assert.That(summary.ActiveSections).IsEqualTo(4);

        long revBefore = form.InputRevision;
        form.RecordSectionOpened("db-conn");
        form.SetWorkspaceDensity(WorkspaceDensity.Compact);
        form.SetWorkspaceNamingMode(WorkspaceNamingMode.Technical);
        form.SetWorkspaceLayoutMode(WorkspaceLayoutMode.Compact);
        await Assert.That(form.InputRevision).IsEqualTo(revBefore);
        await Assert.That(form.Projection.ProgressSummary.OpenedSections).IsEqualTo(1);
    }

    [Test]
    public async Task WizardNavigationComputesStepOrderAndNextRequiredErrorUnvisitedJumps()
    {
        using var form = new DefinitionFormModel(SplitPointerWizardDefinition(), DefinitionSessionPrivacy.BrowserSecretsEnabled);
        await Assert.That(form.SelectExperience("guidedWizard")).IsTrue();

        WorkspaceNavigationProjection nav = form.Projection.Navigation;
        await Assert.That(nav.IsWizardNavigation).IsTrue();
        await Assert.That(nav.WizardStepIndex).IsEqualTo(0);
        await Assert.That(nav.WizardStepCount).IsEqualTo(4);
        await Assert.That(nav.CurrentSectionId).IsEqualTo("db-conn");
        await Assert.That(nav.CanAdvanceWizardStep).IsTrue();
        await Assert.That(nav.NextStepTitle).IsEqualTo("Database Security");

        form.RecordSectionOpened("db-conn");
        await Assert.That(form.Projection.Navigation.NextUnvisitedSectionId).IsEqualTo("db-sec");
        await Assert.That(form.Projection.Navigation.NextRequiredSectionId).IsEqualTo("db-sec");

        form.EditValue(["optionalPort"], "99999"); // max is 65535 -> Invalid
        await Assert.That(form.Projection.Navigation.NextErrorSectionId).IsEqualTo("optional-sec");

        form.RecordSectionOpened("optional-sec");
        await Assert.That(form.Projection.Navigation.WizardStepIndex).IsEqualTo(2);
    }

    [Test]
    public async Task ContextSummaryAndNextActionableDiagnosticReflectAtomicScopeTransitionsAndRejectStaleRevision()
    {
        using var form = new DefinitionFormModel(DeploymentScopeDefinition());
        var initialSummary = form.Projection.ContextSummary;
        await Assert.That(initialSummary).IsNotNull();
        await Assert.That(initialSummary!.RecipeId).IsEqualTo("standalone");
        await Assert.That(initialSummary.ScopeId).IsEqualTo("all");
        await Assert.That(initialSummary.Summary).IsEqualTo("Standalone · All components");
        await Assert.That(string.Join("|", initialSummary.ComponentTitles)).IsEqualTo("Worker|Dashboard");
        await Assert.That(string.Join("|", initialSummary.ArtifactFileNames)).IsEqualTo("dashboard.json|worker.json");

        // Next actionable diagnostic prioritizes active missing required work.
        var nextDiag = form.GetNextActionableDiagnostic();
        await Assert.That(nextDiag).IsNotNull();
        await Assert.That(nextDiag!.FieldId).IsEqualTo("worker");

        long staleRevision = form.InputRevision;
        await Assert.That(form.SelectConfigurationContext("split", "dashboard")).IsTrue();
        await Assert.That(form.Projection.ContextSummary!.Summary).IsEqualTo("Separate processes · Dashboard only");
        await Assert.That(string.Join("|", form.Projection.ContextSummary.ComponentTitles)).IsEqualTo("Dashboard");
        await Assert.That(string.Join("|", form.Projection.ContextSummary.ArtifactFileNames)).IsEqualTo("dashboard.json");
        await Assert.That(form.Projection.ProgressSummary.TotalRequired).IsEqualTo(0);
        await Assert.That(form.Projection.ProgressSummary.RequiredPercent).IsNull();
        await Assert.That(form.Projection.SectionStatuses["configure"].Progress.Percent).IsNull();
        await Assert.That(form.GetNextActionableDiagnostic()).IsNull();

        // Late context confirmation from a superseded revision is rejected without mutating state.
        long currentRevision = form.InputRevision;
        await Assert.That(form.SelectConfigurationContext("standalone", "all", expectedInputRevision: staleRevision)).IsFalse();
        await Assert.That(form.InputRevision).IsEqualTo(currentRevision);
        await Assert.That(form.Projection.ContextSummary.ScopeId).IsEqualTo("dashboard");
    }

    [Test]
    public async Task ContinuousVisibilityExposesAllSectionsAndLockedSecretsWhilePreservingSelectedExperienceOnRoundTrip()
    {
        Definition definition = ContinuousExperienceDefinition();
        using var form = new DefinitionFormModel(
            definition,
            DefinitionSessionPrivacy.BrowserNoSecrets,
            editorVisibility: DefinitionEditorVisibilityPolicy.Authored);
        form.SetDisclosureOverride(FieldDisclosure.Essential);

        await Assert.That(form.SelectedExperienceId).IsEqualTo("essential");
        await Assert.That(form.EditorVisibility).IsEqualTo(DefinitionEditorVisibilityPolicy.Authored);
        await Assert.That(form.IsEditorVisible(["hostName"])).IsTrue();
        await Assert.That(form.IsEditorVisible(["securityToken"])).IsFalse();
        await Assert.That(form.IsEditorVisible(["advancedAudit"])).IsFalse();
        await Assert.That(form.IsEditorVisible(["clusterNode"])).IsFalse();

        // Transition atomically to Compact + Continuous visibility.
        form.SetWorkspaceLayoutMode(WorkspaceLayoutMode.Compact, DefinitionEditorVisibilityPolicy.Continuous);

        await Assert.That(form.WorkspaceLayoutMode).IsEqualTo(WorkspaceLayoutMode.Compact);
        await Assert.That(form.EditorVisibility).IsEqualTo(DefinitionEditorVisibilityPolicy.Continuous);
        await Assert.That(form.SelectedExperienceId).IsEqualTo("essential");
        await Assert.That(string.Join("|", form.Projection.Sections.Select(section => section.Id)))
            .IsEqualTo("general|security|cluster");

        // Active fields in sections outside "essential", including locked secret fields under BrowserNoSecrets, are visible.
        await Assert.That(form.IsEditorVisible(["hostName"])).IsTrue();
        await Assert.That(form.IsEditorVisible(["securityToken"])).IsTrue();
        await Assert.That(form.IsEditorVisible(["advancedAudit"])).IsTrue();
        // Inapplicable sections remain invisible.
        await Assert.That(form.IsEditorVisible(["clusterNode"])).IsFalse();

        // Focusing an editor in a section outside SelectedExperienceId expands its group without mutating SelectedExperienceId.
        await Assert.That(form.Projection.Sections.Single(s => s.Id == "security").Groups.Single(g => g.Id == "audit-group").IsExpanded).IsFalse();
        await Assert.That(form.FocusEditor(["advancedAudit"], preserveDiscovery: true)).IsTrue();
        await Assert.That(form.SelectedExperienceId).IsEqualTo("essential");
        await Assert.That(form.Projection.Sections.Single(s => s.Id == "security").Groups.Single(g => g.Id == "audit-group").IsExpanded).IsTrue();

        await Assert.That(form.EditValue(["advancedAudit"], "strict-audit")).IsTrue();

        // Returning to Guided restores Authored visibility and original SelectedExperienceId while keeping edited answers.
        form.SetWorkspaceLayoutMode(WorkspaceLayoutMode.Guided);

        await Assert.That(form.WorkspaceLayoutMode).IsEqualTo(WorkspaceLayoutMode.Guided);
        await Assert.That(form.EditorVisibility).IsEqualTo(DefinitionEditorVisibilityPolicy.Authored);
        await Assert.That(form.SelectedExperienceId).IsEqualTo("essential");
        await Assert.That(form.Snapshot().Values["advancedAudit"].GetString()).IsEqualTo("strict-audit");
    }

    private static Definition ContinuousExperienceDefinition() => DefinitionCodec.ParseJson(Encoding.UTF8.GetBytes("""
    {
      "schemaVersion": "0.0.1",
      "profile": "bounded-v0.0.1",
      "familyId": "7f84708c-227a-42db-a9a2-f5870a1eb202",
      "revision": 1,
      "project": { "id": "continuous-test", "publisher": "example", "name": "Continuous Experience Test" },
      "applicationVersions": ["1.0.0"],
      "entryPoints": ["setup"],
      "locales": ["en"],
      "configuration": {
        "type": "object",
        "additionalProperties": false,
        "x-configurator": { "sensitivity": "public" },
        "required": ["hostName"],
        "properties": {
          "hostName": {
            "type": "string",
            "title": "Host Name",
            "x-configurator": { "sensitivity": "public", "detail": "basic" }
          },
          "enableCluster": {
            "type": "boolean",
            "title": "Enable Cluster",
            "x-configurator": { "sensitivity": "public", "detail": "basic", "initialValue": false }
          },
          "securityToken": {
            "type": "string",
            "title": "Security Token",
            "x-configurator": {
              "sensitivity": "secret",
              "detail": "advanced",
              "secret": { "kind": "password", "acquisition": "local" }
            }
          },
          "advancedAudit": {
            "type": "string",
            "title": "Advanced Audit Mode",
            "x-configurator": { "sensitivity": "public", "detail": "advanced" }
          },
          "clusterNode": {
            "type": "string",
            "title": "Cluster Node",
            "x-configurator": {
              "sensitivity": "public",
              "detail": "standard",
              "applicableWhen": { "op": "equals", "reference": { "scope": "root", "path": "/enableCluster" }, "value": true }
            }
          }
        }
      },
      "presentation": {
        "defaultJourney": "essential",
        "navigation": "sections",
        "sections": [
          { "id": "general", "title": "General", "detail": "basic", "pointers": ["/hostName", "/enableCluster"] },
          {
            "id": "security",
            "title": "Security",
            "detail": "advanced",
            "pointers": ["/securityToken", "/advancedAudit"],
            "groups": [
              {
                "id": "audit-group",
                "title": "Audit Settings",
                "detail": "advanced",
                "collapsed": true,
                "pointers": ["/advancedAudit"]
              }
            ]
          },
          {
            "id": "cluster",
            "title": "Cluster",
            "detail": "standard",
            "applicableWhen": { "op": "equals", "reference": { "scope": "root", "path": "/enableCluster" }, "value": true },
            "pointers": ["/clusterNode"]
          }
        ],
        "journeys": [
          { "id": "essential", "title": "Essential", "sections": ["general"] },
          { "id": "full", "title": "Full", "sections": ["security", "general", "cluster"] }
        ]
      },
      "artifacts": [
        { "id": "out", "format": "json", "fileName": "out.json", "targetUpdateMode": "create", "restartRequirement": "none", "projection": { "op": "literal", "value": {} } }
      ],
      "requiredCapabilities": [
        "predicate.root", "projection.literal", "schema.boolean", "schema.object", "schema.secret", "schema.string", "values.initial"
      ]
    }
    """));

    private static Definition DeploymentScopeDefinition() => DefinitionSourceCodec.ParseJson(Encoding.UTF8.GetBytes("""
        {"$schema":"urn:islamu:configurator:authoring:v0.0.1",
         "identity":{"familyId":"d66d02af-6a66-4396-9717-452e8eb9541b","revision":1},
         "project":{"id":"example.scope","name":"Worker and dashboard"},"applicationVersions":["1.0"],
         "entryPoints":["worker","dashboard"],
         "configuration":{"type":"object","additionalProperties":false,"required":["worker"],"x-configurator":{"sensitivity":"public"},
          "properties":{
           "recipe":{"type":"string","enum":["standalone","split"],"x-configurator":{"sensitivity":"public","initialValue":"standalone"}},
           "scope":{"type":"string","enum":["all","worker","dashboard"],"x-configurator":{"sensitivity":"public","initialValue":"all"}},
           "worker":{"type":"string","x-configurator":{"sensitivity":"secret"}},
           "dashboard":{"type":"string","x-configurator":{"sensitivity":"public","initialValue":"synthetic-dashboard"}}}},
         "deployment":{"recipePointer":"/recipe","scopePointer":"/scope",
          "components":[{"id":"worker","title":"Worker"},{"id":"dashboard","title":"Dashboard"}],
          "recipes":[{"id":"standalone","title":"Standalone","scopes":[
           {"id":"all","title":"All components","componentIds":["worker","dashboard"],"artifactIds":["worker","dashboard"]}]},
           {"id":"split","title":"Separate processes","scopes":[
            {"id":"all","title":"All components","componentIds":["worker","dashboard"],"artifactIds":["worker","dashboard"]},
            {"id":"worker","title":"Worker only","componentIds":["worker"],"artifactIds":["worker"]},
            {"id":"dashboard","title":"Dashboard only","componentIds":["dashboard"],"artifactIds":["dashboard"]}]}]},
         "outputs":{
          "worker.json":{"id":"worker","format":"json","consumerIds":["worker"],"projection":{"op":"object","members":{
           "value":{"op":"copy","reference":{"scope":"root","path":"/worker"}}}}},
          "dashboard.json":{"id":"dashboard","format":"json","consumerIds":["dashboard"],"projection":{"op":"object","members":{
           "value":{"op":"copy","reference":{"scope":"root","path":"/dashboard"}}}}}}}
        """));

    private static Definition SplitPointerWizardDefinition() => DefinitionCodec.ParseJson(Encoding.UTF8.GetBytes("""
    {
      "schemaVersion": "0.0.1",
      "profile": "bounded-v0.0.1",
      "familyId": "6f84708c-227a-42db-a9a2-f5870a1eb101",
      "revision": 1,
      "project": { "id": "nav-test", "publisher": "example", "name": "Navigation Test" },
      "applicationVersions": ["1.0.0"],
      "entryPoints": ["setup"],
      "locales": ["en"],
      "configuration": {
        "type": "object",
        "additionalProperties": false,
        "x-configurator": { "sensitivity": "public" },
        "required": ["database"],
        "properties": {
          "database": {
            "type": "object",
            "additionalProperties": false,
            "required": ["host", "password"],
            "x-configurator": { "sensitivity": "public", "detail": "basic", "initialValue": {} },
            "properties": {
              "host": { "type": "string", "title": "Database Host", "x-configurator": { "sensitivity": "public", "detail": "basic" } },
              "password": {
                "type": "string",
                "title": "Database Password",
                "minLength": 24,
                "x-configurator": {
                  "sensitivity": "secret",
                  "detail": "basic",
                  "secret": {
                    "kind": "password",
                    "acquisition": "local",
                    "constraints": { "characterSet": "ascii-printable" },
                    "generation": { "length": 24 }
                  }
                }
              }
            }
          },
          "optionalPort": {
            "type": "integer",
            "title": "Optional Port",
            "minimum": 1,
            "maximum": 65535,
            "x-configurator": { "sensitivity": "public", "detail": "standard" }
          },
          "enableExtraIdentity": {
            "type": "boolean",
            "title": "Enable Extra Identity",
            "x-configurator": { "sensitivity": "public", "detail": "basic", "initialValue": false }
          },
          "extraIssuer": {
            "type": "string",
            "title": "Extra Issuer",
            "x-configurator": {
              "sensitivity": "public",
              "detail": "standard",
              "applicableWhen": { "op": "equals", "reference": { "scope": "root", "path": "/enableExtraIdentity" }, "value": true }
            }
          }
        }
      },
      "presentation": {
        "defaultJourney": "guidedWizard",
        "navigation": "wizard",
        "sections": [
          { "id": "db-conn", "title": "Database Connection", "detail": "basic", "pointers": ["/database/host"] },
          { "id": "db-sec", "title": "Database Security", "detail": "basic", "pointers": ["/database/password"] },
          { "id": "optional-sec", "title": "Optional Tuning", "detail": "standard", "pointers": ["/optionalPort"] },
          { "id": "identity-sec", "title": "Identity", "detail": "standard", "pointers": ["/enableExtraIdentity", "/extraIssuer"] }
        ],
        "journeys": [
          { "id": "guidedWizard", "title": "Guided Wizard", "sections": ["db-conn", "db-sec", "optional-sec", "identity-sec"] },
          { "id": "allSections", "title": "All Sections", "sections": ["db-conn", "db-sec", "optional-sec", "identity-sec"] }
        ]
      },
      "artifacts": [
        { "id": "out", "format": "json", "fileName": "out.json", "targetUpdateMode": "create", "restartRequirement": "none", "projection": { "op": "literal", "value": {} } }
      ],
      "requiredCapabilities": [
        "predicate.root", "projection.literal", "schema.boolean", "schema.bounds",
        "schema.integer", "schema.object", "schema.secret", "schema.string", "values.initial"
      ]
    }
    """));
}
