namespace Configurator.Presentation.Tests;

using System.Text;
using Configurator.Core.Definitions;

public sealed class DefinitionFormUndoHistoryTests
{
    [Test]
    public async Task UndoRedoRestoresValuesOmissionsAndListRowIdentityWithBoundedCapacity()
    {
        using var form = new DefinitionFormModel(UndoDefinition());

        await Assert.That(form.CanUndo).IsFalse();
        await Assert.That(form.CanRedo).IsFalse();

        form.EditValue(["appName"], "Event Alpha");
        form.AddListItem(["endpoints"]);
        form.EditValue(["endpoints", "0"], "https://one.example");
        form.AddListItem(["endpoints"]);
        form.EditValue(["endpoints", "1"], "https://two.example");

        long secondRowId = form.GetListRowIdentities(["endpoints"])[1];
        await Assert.That(form.RemoveListItem(["endpoints"], 1)).IsTrue();
        await Assert.That(form.CanUndo).IsTrue();
        await Assert.That(form.LastUndoLabel).IsNotNull();

        await Assert.That(form.Undo()).IsTrue();
        IReadOnlyList<long> restoredIds = form.GetListRowIdentities(["endpoints"]);
        await Assert.That(restoredIds.Count).IsEqualTo(2);
        await Assert.That(restoredIds[1]).IsEqualTo(secondRowId);
        await Assert.That(form.ReadValue(["endpoints", "1"])!.Value.GetString()).IsEqualTo("https://two.example");

        await Assert.That(form.CanRedo).IsTrue();
        await Assert.That(form.Redo()).IsTrue();
        await Assert.That(form.GetListRowIdentities(["endpoints"]).Count).IsEqualTo(1);

        // Enforce 32-entry bounded capacity.
        for (int index = 0; index < 45; index++)
            form.EditValue(["appName"], "Value-" + index.ToString(System.Globalization.CultureInfo.InvariantCulture));

        int undoSteps = 0;
        while (form.Undo()) undoSteps++;
        await Assert.That(undoSteps).IsEqualTo(DefinitionFormUndoHistory.MaxCapacity);
        await Assert.That(DefinitionFormUndoHistory.MaxCapacity).IsEqualTo(32);
    }

    [Test]
    public async Task DisableSecretEntrySynchronouslyPurgesSecretUndoAndRedoHistory()
    {
        using var form = new DefinitionFormModel(UndoDefinition(), DefinitionSessionPrivacy.BrowserSecretsEnabled);

        form.EditValue(["appName"], "Public One");
        form.EditValue(["apiKey"], "very-secret-token-value-12345678");
        form.EditValue(["appName"], "Public Two");

        // Undo "Public Two" -> Secret step sits on Undo, Public Two sits on Redo; revoke consent while both exist.
        await Assert.That(form.Undo()).IsTrue();
        await Assert.That(form.Undo()).IsTrue();
        await Assert.That(form.ReadValue(["apiKey"])).IsNull();

        // Re-apply apiKey then keep another secret-bearing step on Redo as well before revoking.
        form.EditValue(["apiKey"], "super-secret-value-abcdef1234567890");
        form.EditValue(["appName"], "Public Three");

        // Now undo "Public Three", leaving it in Redo; then revoke consent!
        await Assert.That(form.Undo()).IsTrue();
        await Assert.That(form.CanRedo).IsTrue();

        form.SetSecretEntryEnabled(false);

        await Assert.That(form.ReadValue(["apiKey"])).IsNull();
        // Neither Redo nor Undo may ever resurrect "super-secret-value-abcdef1234567890".
        if (form.CanRedo)
        {
            form.Redo();
            await Assert.That(form.ReadValue(["apiKey"])).IsNull();
        }
        while (form.Undo())
            await Assert.That(form.ReadValue(["apiKey"])).IsNull();

        while (form.Redo())
            await Assert.That(form.ReadValue(["apiKey"])).IsNull();
    }

    [Test]
    public async Task ClearPrivateStateAndDisposePurgeSensitiveAndSecretHistory()
    {
        using var form = new DefinitionFormModel(UndoDefinition(), DefinitionSessionPrivacy.BrowserSecretsEnabled);
        form.EditValue(["appName"], "Public Initial");
        form.EditValue(["apiKey"], "disposal-secret-token-1234567890");
        form.EditValue(["appName"], "Public Final");

        form.ClearPrivateState();
        while (form.Undo())
            await Assert.That(form.ReadValue(["apiKey"])).IsNull();

        form.SetSecretEntryEnabled(true);
        form.EditValue(["apiKey"], "another-secret-token-0987654321");
        form.Dispose();

        await Assert.That(() => form.Undo()).Throws<ObjectDisposedException>();
    }

    private static Definition UndoDefinition() => DefinitionCodec.ParseJson(Encoding.UTF8.GetBytes("""
    {
      "schemaVersion": "0.0.1",
      "profile": "bounded-v0.0.1",
      "familyId": "a29150ab-2f25-445f-841e-d088df3a4f02",
      "revision": 1,
      "project": { "id": "undo-test", "publisher": "example", "name": "Undo Test" },
      "applicationVersions": ["1.0.0"],
      "entryPoints": ["setup"],
      "locales": ["en"],
      "configuration": {
        "type": "object",
        "additionalProperties": false,
        "x-configurator": { "sensitivity": "public" },
        "properties": {
          "appName": { "type": "string", "x-configurator": { "sensitivity": "public", "detail": "basic" } },
          "apiKey": {
            "type": "string",
            "minLength": 16,
            "x-configurator": {
              "sensitivity": "secret",
              "detail": "basic",
              "secret": { "kind": "password", "acquisition": "local" }
            }
          },
          "endpoints": {
            "type": "array",
            "x-configurator": { "sensitivity": "public", "detail": "standard" },
            "items": { "type": "string" }
          }
        }
      },
      "artifacts": [
        { "id": "out", "format": "json", "fileName": "out.json", "targetUpdateMode": "create", "restartRequirement": "none", "projection": { "op": "literal", "value": {} } }
      ],
      "requiredCapabilities": ["projection.literal", "schema.array", "schema.bounds", "schema.object", "schema.secret", "schema.string"]
    }
    """));
}
