namespace Configurator.Presentation.Tests;

using System.Text;
using Configurator.Core.Definitions;
using Configurator.Core.Secrets;

public sealed class SecretGeneratorSessionTests
{
    [Test]
    public async Task PreviewDoesNotMutateAnswersUntilExplicitApply()
    {
        using var form = new DefinitionFormModel(PasswordAndNoteDefinition(), DefinitionSessionPrivacy.BrowserSecretsEnabled);
        using DefinitionSecretGeneratorSession? session = form.CreateSecretGeneratorSession(["password"]);
        await Assert.That(session).IsNotNull();
        await Assert.That(session!.CanGenerate).IsTrue();

        DefinitionSecretOperationResult preview = await session.GeneratePreviewAsync();
        await Assert.That(preview.Succeeded).IsTrue();
        await Assert.That(session.HasCandidate).IsTrue();
        await Assert.That(session.CanApply).IsTrue();
        string? previewValue = session.CopyPreview();
        await Assert.That(previewValue).IsNotNull();
        await Assert.That(session.ToString()).DoesNotContain(previewValue!);

        // Preview never mutates form answers or advances input revision.
        await Assert.That(form.ReadValue(["password"])).IsNull();
        await Assert.That(form.Snapshot().Values.ContainsKey("password")).IsFalse();
        await Assert.That(form.InputRevision).IsEqualTo(0);

        DefinitionSecretOperationResult applied = await session.ApplyAsync();
        await Assert.That(applied.Succeeded).IsTrue();
        await Assert.That(form.ReadValue(["password"])!.Value.GetString()).IsEqualTo(previewValue);
        await Assert.That(form.AnswerOrigin(["password"])).IsEqualTo(DefinitionAnswerOrigin.Generated);
        await Assert.That(session.HasCandidate).IsFalse();
        await Assert.That(session.CopyPreview()).IsNull();
    }

    [Test]
    public async Task RevokingConsentSynchronouslyClearsCandidateAndBlocksApply()
    {
        using var form = new DefinitionFormModel(PasswordAndNoteDefinition(), DefinitionSessionPrivacy.BrowserSecretsEnabled);
        using DefinitionSecretGeneratorSession session = form.CreateSecretGeneratorSession(["password"])!;

        DefinitionSecretOperationResult preview = await session.GeneratePreviewAsync();
        await Assert.That(preview.Succeeded).IsTrue();
        await Assert.That(session.HasCandidate).IsTrue();

        form.SetSecretEntryEnabled(false);

        await Assert.That(session.HasCandidate).IsFalse();
        await Assert.That(session.CanApply).IsFalse();
        await Assert.That(session.CanGenerate).IsFalse();
        await Assert.That(session.CopyPreview()).IsNull();

        DefinitionSecretOperationResult applyAfterRevoke = await session.ApplyAsync();
        await Assert.That(applyAfterRevoke.Succeeded).IsFalse();
        await Assert.That(form.ReadValue(["password"])).IsNull();
    }

    [Test]
    public async Task UnrelatedEditFencesPendingPreviewAsStale()
    {
        using var form = new DefinitionFormModel(PasswordAndNoteDefinition(), DefinitionSessionPrivacy.BrowserSecretsEnabled);
        using DefinitionSecretGeneratorSession session = form.CreateSecretGeneratorSession(["password"])!;

        await Assert.That((await session.GeneratePreviewAsync()).Succeeded).IsTrue();
        form.EditValue(["note"], "edited-after-preview");

        DefinitionSecretOperationResult staleApply = await session.ApplyAsync();
        await Assert.That(staleApply.Succeeded).IsFalse();
        await Assert.That(staleApply.Code).IsEqualTo("secret-stale");
        await Assert.That(form.ReadValue(["password"])).IsNull();
        await Assert.That(session.HasCandidate).IsFalse();
    }

    [Test]
    public async Task CancelAndDisposeClearCandidateWithoutPublishingAndExposeComplianceFlags()
    {
        using var form = new DefinitionFormModel(PasswordAndNoteDefinition(), DefinitionSessionPrivacy.BrowserSecretsEnabled);
        using DefinitionSecretGeneratorSession session = form.CreateSecretGeneratorSession(["password"])!;

        await Assert.That(session.Bounds.SupportsOptions).IsTrue();
        await Assert.That(session.MeetsPolicy).IsTrue();
        await Assert.That(session.MeetsEntropyFloor).IsTrue();

        session.UpdateOptions(new SecretGenerationOptions { Length = 10 });
        await Assert.That(session.MeetsEntropyFloor).IsFalse();
        await Assert.That(session.MeetsPolicy).IsFalse();
        await Assert.That((await session.GeneratePreviewAsync()).Succeeded).IsFalse();

        session.UpdateOptions(SecretGenerationOptions.FromBounds(session.Bounds));
        await Assert.That(session.MeetsPolicy).IsTrue();
        await Assert.That((await session.GeneratePreviewAsync()).Succeeded).IsTrue();

        session.Cancel();
        await Assert.That(session.HasCandidate).IsFalse();
        await Assert.That(session.CopyPreview()).IsNull();
        await Assert.That(form.ReadValue(["password"])).IsNull();

        session.Dispose();
        await Assert.That(() => session.CopyPreview()).Throws<ObjectDisposedException>();
    }

    private static Definition PasswordAndNoteDefinition() => DefinitionCodec.ParseJson(Encoding.UTF8.GetBytes("""
    {
      "schemaVersion": "0.0.1",
      "profile": "bounded-v0.0.1",
      "familyId": "e9c27549-8b9f-4e43-8a6b-2f76c74f0e11",
      "revision": 1,
      "project": {"id": "example", "name": "Example", "publisher": "example"},
      "applicationVersions": ["1"],
      "entryPoints": ["setup"],
      "locales": ["en"],
      "localization": {"defaultLocale": "en", "translations": {}},
      "requiredCapabilities": ["projection.copy", "projection.object", "schema.bounds", "schema.object", "schema.secret", "schema.string"],
      "presentation": {
        "defaultJourney": "setup",
        "sections": [{"id": "general", "title": "General", "pointers": ["/password", "/note"]}],
        "journeys": [{"id": "setup", "title": "Setup", "sections": ["general"]}]
      },
      "configuration": {
        "type": "object",
        "additionalProperties": false,
        "x-configurator": {"sensitivity": "public"},
        "properties": {
          "note": {
            "type": "string",
            "title": "Note",
            "x-configurator": {"sensitivity": "public", "detail": "basic"}
          },
          "password": {
            "type": "string",
            "title": "Password",
            "minLength": 12,
            "maxLength": 64,
            "x-configurator": {
              "sensitivity": "secret",
              "detail": "basic",
              "secret": {
                "kind": "password",
                "acquisition": "local",
                "constraints": {
                  "characterSet": "ascii-printable",
                  "allowWhitespace": false,
                  "minLowercase": 2,
                  "minUppercase": 2,
                  "minDigits": 2,
                  "minSymbols": 2,
                  "allowedSymbols": "!@#$",
                  "forbiddenCharacters": "0O"
                },
                "generation": {"length": 32}
              }
            }
          }
        }
      },
      "artifacts": [{
        "id": "config",
        "format": "json",
        "fileName": "config.json",
        "targetUpdateMode": "create",
        "restartRequirement": "process",
        "projection": {"op": "object", "members": {"note": {"op": "copy", "reference": {"scope": "root", "path": "/note"}}}}
      }]
    }
    """));
}
