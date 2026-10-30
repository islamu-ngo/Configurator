namespace Configurator.Presentation.Tests;

using System.Text.Json;
using Configurator.Core.Artifacts;
using Configurator.Core.Definitions;

public sealed class EventDefinitionFormTests
{
    [Test]
    public async Task EventTenantCanPrepareFromRequiredIdentifiersAndExplicitSettings()
    {
        using var form = new DefinitionFormModel(await Definition(), DefinitionSessionPrivacy.BrowserNoSecrets);
        form.SetValue(["operation"], JsonSerializer.SerializeToElement("configuration-manifest"));
        form.SetValue(["configuration-manifest.metadata.name"], JsonSerializer.SerializeToElement("qa-config"));
        form.SetValue(["configuration-manifest.spec.instance.settings", "modules.tech_enabled"], JsonSerializer.SerializeToElement(true));
        await Assert.That(form.AddListItem(["configuration-manifest.tenants"])).IsTrue();
        form.SetValue(["configuration-manifest.tenants", "0", "metadata", "name"], JsonSerializer.SerializeToElement("tenant-one"));
        form.SetValue(["configuration-manifest.tenants", "0", "spec", "displayName"], JsonSerializer.SerializeToElement("Tenant One"));
        form.SupplyObject(["configuration-manifest.tenants", "0", "spec", "settings"]);
        form.SupplyObject(["configuration-manifest.tenants", "0", "spec", "documents"]);
        form.SupplyObject(["configuration-manifest.tenants", "0", "spec", "legalDocuments"]);
        using DefinitionDraftArtifactResult result = DefinitionDraftArtifactComposer.Compose(form.Definition, "configuration-manifest", form.Snapshot());
        await Assert.That(string.Join(',', result.Diagnostics.Select(diagnostic => diagnostic.ToString()))).IsEqualTo(string.Empty);
        await Assert.That(result.Succeeded).IsTrue();
        using JsonDocument output = JsonDocument.Parse(result.Artifact!.Bytes);
        JsonElement row = output.RootElement.GetProperty("spec").GetProperty("tenants")[0];
        await Assert.That(row.GetProperty("metadata").GetProperty("name").GetString()).IsEqualTo("tenant-one");
        await Assert.That(row.GetProperty("spec").GetProperty("settings").ValueKind).IsEqualTo(JsonValueKind.Object);
        await Assert.That(row.GetProperty("spec").GetProperty("documents").ValueKind).IsEqualTo(JsonValueKind.Object);
        await Assert.That(row.GetProperty("spec").GetProperty("legalDocuments").ValueKind).IsEqualTo(JsonValueKind.Object);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task EventManifestImportRecoversCheckedManifestWithoutChangingItsPublicValues(bool consent)
    {
        using var form = new DefinitionFormModel(await Definition(), consent
            ? DefinitionSessionPrivacy.BrowserSecretsEnabled : DefinitionSessionPrivacy.BrowserNoSecrets);
        form.SetValue(["operation"], JsonSerializer.SerializeToElement("configuration-manifest"));
        SetupAnswerSnapshot original = form.Snapshot();
        bool consumed = false;
        DefinitionImportResult result = await form.ImportAsync("configuration-manifest", token =>
        {
            consumed = true;
            return ReadArtifact(token);
        });
        await Assert.That(consumed).IsEqualTo(consent);
        await Assert.That(result.Code).IsEqualTo(consent ? "import-ready" : "import-secret-consent-required");
        await Assert.That(result.Succeeded).IsEqualTo(consent);
        await Assert.That(form.Snapshot().InputRevision).IsEqualTo(original.InputRevision + (consent ? 1 : 0));
        if (!consent)
        {
            await Assert.That(form.Snapshot().Values.Keys).IsEquivalentTo(original.Values.Keys);
            return;
        }
        await Assert.That(form.Snapshot().Values["configuration-manifest.metadata.name"].GetString()).IsEqualTo("example-deployment");
        await Assert.That(form.Snapshot().Values["configuration-manifest.tenants"][0]
            .GetProperty("metadata").GetProperty("name").GetString()).IsEqualTo("community");
    }

    [Test]
    public async Task ExplicitObjectSupplyRepresentsRequiredEmptySettingsWithoutInventingIdentifiers()
    {
        using var form = new DefinitionFormModel(await Definition(), DefinitionSessionPrivacy.BrowserNoSecrets);
        form.SetValue(["operation"], JsonSerializer.SerializeToElement("configuration-manifest"));
        await Assert.That(form.SupplyObject(["configuration-manifest.spec.instance.settings"])).IsTrue();
        await Assert.That(form.Snapshot().Values["configuration-manifest.spec.instance.settings"].EnumerateObject().Count()).IsEqualTo(0);
        await Assert.That(form.Validate().IsReady).IsFalse();
        await Assert.That(form.Validate().Diagnostics.Any(diagnostic => diagnostic.FieldId == "configuration-manifest.metadata.name" && diagnostic.Code == "value-required")).IsTrue();
        form.SetValue(["configuration-manifest.spec.instance.settings", "modules.tech_enabled"], JsonSerializer.SerializeToElement(true));
        await Assert.That(form.SupplyObject(["configuration-manifest.spec.instance.settings"])).IsTrue();
        await Assert.That(form.Snapshot().Values["configuration-manifest.spec.instance.settings"].GetProperty("modules.tech_enabled").GetBoolean()).IsTrue();
    }

    private static async Task<SetupDefinition> Definition() => SetupDefinitionCodec.ParseJson(
        await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Event", "definition.json")));

    private static async Task<ReadOnlyMemory<byte>> ReadArtifact(CancellationToken token) =>
        await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Event", "configuration-manifest-1-tenants.json"), token);
}
