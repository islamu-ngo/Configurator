namespace Configurator.Presentation.Tests;

using System.Text.Json;
using Configurator.Core.Artifacts;
using Configurator.Core.Definitions;

public sealed class EventDefinitionFormTests
{
    [Test]
    public async Task FreshEnvironmentInitializesOnlyBenignSelectionAndPreservesDeliberateChoices()
    {
        using var form = new DefinitionFormModel(await Definition(), DefinitionSessionPrivacy.BrowserNoSecrets);
        DefinitionPath output = DefinitionPath.Parse("/operation");
        DefinitionPath inclusion = DefinitionPath.Parse("/select.feature.platform-config");
        await Assert.That(form.Snapshot().Values.Count).IsEqualTo(0);
        await Assert.That(form.Validate().TryGetEffectiveValue(output, out JsonElement initialOutput)).IsTrue();
        await Assert.That(initialOutput.GetString()).IsEqualTo("environment");
        await Assert.That(form.Validate().TryGetEffectiveValue(inclusion, out JsonElement initialInclusion)).IsTrue();
        await Assert.That(initialInclusion.GetBoolean()).IsTrue();
        await Assert.That(string.Join(',', form.Definition.Artifacts.Select(artifact => artifact.Id).Order(StringComparer.Ordinal)))
            .IsEqualTo("configuration-manifest,environment-api,environment-blazor,environment-compose,environment-compose-api,environment-compose-blazor,environment-standalone,legal-drafts,operator-identity,tenant-package");
        await Assert.That(form.ConfigurationContext!.RecipeId).IsEqualTo("standalone");
        await Assert.That(form.ConfigurationContext.ScopeId).IsEqualTo("all");
        await Assert.That(form.SetValue(["operation"], JsonSerializer.SerializeToElement("configuration-manifest"))).IsTrue();
        await Assert.That(form.SetValue(["select.feature.platform-config"], JsonSerializer.SerializeToElement(false))).IsTrue();
        await Assert.That(form.Validate().TryGetEffectiveValue(output, out JsonElement deliberateOutput)).IsTrue();
        await Assert.That(deliberateOutput.GetString()).IsEqualTo("configuration-manifest");
        await Assert.That(form.Snapshot().Values["select.feature.platform-config"].GetBoolean()).IsFalse();
        await Assert.That(form.SetValue(["operation"], null)).IsTrue();
        await Assert.That(form.Validate().TryGetEffectiveValue(output, out _)).IsFalse();
        await Assert.That(form.Snapshot().Omitted.Contains(output)).IsTrue();
        await Assert.That(form.ResetValue(["operation"])).IsTrue();
        await Assert.That(form.Validate().TryGetEffectiveValue(output, out JsonElement resetOutput)).IsTrue();
        await Assert.That(resetOutput.GetString()).IsEqualTo("environment");
        await Assert.That(form.Snapshot().Values["select.feature.platform-config"].GetBoolean()).IsFalse();
    }

    [Test]
    public async Task PlatformInclusionDoesNotSupplyTargetSettingsAndOmissionSurvivesOutputChanges()
    {
        using var form = new DefinitionFormModel(await Definition(), DefinitionSessionPrivacy.BrowserNoSecrets);
        DefinitionPath inclusion = DefinitionPath.Parse("/select.feature.platform-config");
        DefinitionValidationResult initial = form.Validate();
        await Assert.That(form.Snapshot().Values.Keys.Any(name => name.StartsWith("env.", StringComparison.Ordinal))).IsFalse();
        string[] basics = ["env.API_ENDPOINT", "env.AUTHENTICATION_PROVIDER", "env.PUBLIC_BASE_URL"];
        foreach (string fieldName in basics)
            await Assert.That(initial.TryGetEffectiveValue(DefinitionPath.FromSegments([fieldName]), out _)).IsFalse();
        string[] targetSwitches =
        [
            "env.AGENT_BROWSER_SEED_ENABLED", "env.AI_TOOL_PROPOSALS_ENABLED",
            "env.EMAIL_DISPATCH_RABBITMQ_ENABLED", "env.LISTMONK_ENABLED", "env.MCP_ENABLED",
            "env.REPORTING_ENABLED", "env.STORAGE_RECONCILIATION_ENABLED",
            "env.WEBHOOKS_ENABLED", "env.WEB_PUSH_ENABLED"
        ];
        foreach (string fieldName in targetSwitches)
            if (initial.TryGetEffectiveValue(DefinitionPath.FromSegments([fieldName]), out JsonElement enabled))
                await Assert.That(enabled.GetBoolean()).IsFalse();

        await Assert.That(form.SetValue(["select.feature.platform-config"], null)).IsTrue();
        await Assert.That(form.SetValue(["operation"], JsonSerializer.SerializeToElement("tenant-package"))).IsTrue();
        await Assert.That(form.SetValue(["operation"], JsonSerializer.SerializeToElement("environment"))).IsTrue();
        await Assert.That(form.SelectLocale("ar")).IsTrue();
        await Assert.That(form.SelectLocale("en")).IsTrue();
        await Assert.That(form.Validate().TryGetEffectiveValue(inclusion, out _)).IsFalse();
        await Assert.That(form.Snapshot().Omitted.Contains(inclusion)).IsTrue();
        await Assert.That(form.Snapshot().Values.ContainsKey("select.feature.platform-config")).IsFalse();
        await Assert.That(form.ResetValue(["select.feature.platform-config"])).IsTrue();
        await Assert.That(form.Validate().TryGetEffectiveValue(inclusion, out JsonElement reset)).IsTrue();
        await Assert.That(reset.GetBoolean()).IsTrue();
    }

    [Test]
    [Arguments("environment", "event-standalone.env")]
    [Arguments("configuration-manifest", "configuration-manifest.json")]
    [Arguments("tenant-package", "tenant-package.json")]
    [Arguments("operator-identity", "operator-identity.json")]
    [Arguments("legal-drafts", "legal-drafts.json")]
    public async Task EveryOutputChoicePreservesItsArtifactIdentityAndExplicitAnswer(string choice, string fileName)
    {
        using var form = new DefinitionFormModel(await Definition(), DefinitionSessionPrivacy.BrowserNoSecrets);
        DefinitionOutputSelector selector = form.Definition.Presentation!.OutputSelector!;
        await Assert.That(selector.Location.ToPointer()).IsEqualTo("/operation");
        await Assert.That(selector.Options.Count).IsEqualTo(5);
        DefinitionOutputOption option = selector.Options.Single(option => option.Value == choice);
        string artifactId = choice == "environment" ? "environment-standalone" : choice;
        string[] expectedArtifacts = choice == "environment"
            ? ["environment-standalone", "environment-api", "environment-blazor", "environment-compose", "environment-compose-api", "environment-compose-blazor"]
            : [choice];
        await Assert.That(option.ArtifactIds.SequenceEqual(expectedArtifacts)).IsTrue();
        await Assert.That(form.Definition.Artifacts.Single(artifact => artifact.Id == artifactId).FileName).IsEqualTo(fileName);
        await Assert.That(form.SetValue(["operation"], JsonSerializer.SerializeToElement(choice))).IsTrue();
        await Assert.That(form.SelectLocale("ar")).IsTrue();
        await Assert.That(form.Snapshot().Values["operation"].GetString()).IsEqualTo(choice);
        await Assert.That(form.EligibleArtifacts.Count).IsEqualTo(1);
        await Assert.That(form.EligibleArtifacts.Single().Id).IsEqualTo(artifactId);
        await Assert.That(form.Validate().TryGetEffectiveValue(
            DefinitionPath.Parse("/operation"), out JsonElement effective)).IsTrue();
        await Assert.That(effective.GetString()).IsEqualTo(choice);
    }

    [Test]
    [Arguments("fresh", true)]
    [Arguments("explicit", false)]
    [Arguments("omitted", false)]
    public async Task ManifestImportReplacesOnlyAnUnansweredOutputChoice(string state, bool succeeds)
    {
        using var form = new DefinitionFormModel(await Definition(), DefinitionSessionPrivacy.BrowserSecretsEnabled);
        DefinitionPath operation = DefinitionPath.Parse("/operation");
        if (state == "explicit")
            await Assert.That(form.SetValue(["operation"], JsonSerializer.SerializeToElement("environment"))).IsTrue();
        else if (state == "omitted")
            await Assert.That(form.SetValue(["operation"], null)).IsTrue();
        AnswerSnapshot before = form.Snapshot();

        DefinitionImportResult result = await form.ImportAsync("configuration-manifest", ReadArtifact);

        await Assert.That(result.Succeeded).IsEqualTo(succeeds);
        await Assert.That(result.Code).IsEqualTo(succeeds ? "import-ready" : "answer-conflict");
        if (succeeds)
        {
            await Assert.That(form.Snapshot().Values["operation"].GetString()).IsEqualTo("configuration-manifest");
            await Assert.That(form.Snapshot().Values["configuration-manifest.metadata.name"].GetString()).IsEqualTo("example-deployment");
            return;
        }
        await Assert.That(form.Snapshot().InputRevision).IsEqualTo(before.InputRevision);
        await Assert.That(form.Snapshot().Values.Keys).IsEquivalentTo(before.Values.Keys);
        await Assert.That(form.Snapshot().Omitted.Contains(operation)).IsEqualTo(state == "omitted");
        if (state == "explicit")
            await Assert.That(form.Snapshot().Values["operation"].GetString()).IsEqualTo("environment");
    }

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
        AnswerSnapshot original = form.Snapshot();
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

    private static async Task<Definition> Definition() => DefinitionCodec.ParseJson(
        await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Event", "definition.json")));

    private static async Task<ReadOnlyMemory<byte>> ReadArtifact(CancellationToken token) =>
        await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Event", "configuration-manifest-1-tenants.json"), token);
}
