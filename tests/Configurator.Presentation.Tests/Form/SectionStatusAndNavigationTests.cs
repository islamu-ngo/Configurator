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
