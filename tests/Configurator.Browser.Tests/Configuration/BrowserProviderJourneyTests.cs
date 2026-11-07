namespace Configurator.Browser.Tests;

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bunit;
using Configurator.Core.Artifacts;
using Configurator.Core.Definitions;
using Configurator.Presentation;
using MudBlazor;

public sealed class BrowserProviderJourneyTests
{
    [Test]
    [Arguments("text")]
    [Arguments("choice")]
    [Arguments("boolean")]
    public async Task RequiredControlsFollowDependencyChangesWithoutAdditionalEdits(string kind)
    {
        JsonNode source = JsonNode.Parse(BrowserFixture.Definition.CanonicalBytes.Span)!;
        BrowserFixture.WithField(source, "requirement", kind switch
        {
            "choice" => """{"type":"string","enum":["alpha","beta"],"x-configurator":{"sensitivity":"public","detail":"basic"}}""",
            "boolean" => """{"type":"boolean","x-configurator":{"sensitivity":"public","detail":"basic"}}""",
            _ => """{"type":"string","x-configurator":{"sensitivity":"public","detail":"basic"}}"""
        });
        BrowserFixture.WithField(source, "requireAdditional", """{"type":"boolean","x-configurator":{"sensitivity":"public","detail":"basic"}}""", "relationship.presence");
        source["configuration"]!["dependentRequired"] = JsonNode.Parse("""{"requireAdditional":["requirement"]}""");
        var definition = DefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString()));
        await using var context = BrowserFixture.Context();
        using var form = new DefinitionFormModel(definition);
        form.EditValue(["requireAdditional"], "true");
        long revision = form.InputRevision;
        var component = context.Render<ConfigurationSectionFields>(parameters => parameters
            .Add(item => item.Form, form)
            .Add(item => item.Fields, form.Validate().Fields.Where(field => field.Id == "requirement").ToArray()));

        string selector = kind == "boolean" ? "[role='radiogroup']" : "input";
        await Assert.That(component.Find(selector).GetAttribute("aria-required")).IsEqualTo("true");
        await Assert.That(form.InputRevision).IsEqualTo(revision);
        component.Render(parameters => parameters.Add(item => item.Validation, form.Validate()));
        await Assert.That(component.Find(selector).GetAttribute("aria-invalid")).IsEqualTo("true");
        await component.InvokeAsync(() => form.EditValue(["requireAdditional"], null));
        revision = form.InputRevision;
        component.Render(parameters => parameters.Add(item => item.Validation, form.Validate()));
        await Assert.That(component.Find(selector).GetAttribute("aria-required")).IsEqualTo("false");
        await Assert.That(component.Find(selector).GetAttribute("aria-invalid")).IsEqualTo("false");
        await Assert.That(form.InputRevision).IsEqualTo(revision);
    }

    [Test]
    public async Task ProviderDropdownPreservesWireIdentitySwitchBackAndSecretRevocation()
    {
        var definition = BrowserDefinitionRegistry.LoadBundled().Current.Resolve(Guid.Parse("ea55cccf-dce5-4f88-8ea1-6dd211714edc"));
        await using var context = BrowserFixture.Context();
        using var form = new DefinitionFormModel(definition, DefinitionSessionPrivacy.BrowserSecretsEnabled);
        form.SetDisclosureOverride(FieldDisclosure.Advanced);
        form.EditValue(["select.feature.platform-config"], "true");
        form.EditValue(["env.SECRET_PROVIDER"], "Environment");
        form.EditValue(["env.BFF_SESSION_REDIS_ENDPOINT"], "redis://session.example.test:6379");
        form.EditValue(["env.BFF_SESSION_NAMESPACE"], "synthetic-event");
        form.EditValue(["env.BFF_SESSION_REDIS_PASSWORD"], "synthetic-bff-session-password");
        form.EditValue(["env.EVENT_DP_COMBINED_RING"], "synthetic-combined-authority-ring");
        form.EditValue(["env.EVENT_DP_COMBINED_ADMISSION"], "synthetic-combined-authority-admission");
        form.EditValue(["env.EVENT_DP_COMBINED_ADMISSION_SHA256"], new string('a', 64));
        form.EditValue(["env.EVENT_DP_COMBINED_MINIMUM_GENERATION"], "1");
        var component = context.Render<ConfigurationSectionFields>(parameters => parameters
            .Add(item => item.Form, form)
            .Add(item => item.Fields, form.Validate().Fields.Where(field => field.Id == "env.AUTHENTICATION_PROVIDER").ToArray()));
        await Assert.That(component.FindComponents<MudSelect<string>>().Count).IsEqualTo(1);
        await Choose("1");
        await Assert.That(form.Snapshot().Values["env.AUTHENTICATION_PROVIDER"].GetString()).IsEqualTo("keycloak");
        var retired = component.FindComponent<MudSelect<string>>().Instance.ValueChanged;
        await component.InvokeAsync(() =>
        {
            form.EditValue(["env.KEYCLOAK_ENDPOINT"], "https://identity.example.test");
            form.EditValue(["env.KEYCLOAK_REALM"], "synthetic-realm");
            form.EditValue(["env.KEYCLOAK_CLIENT_ID"], "synthetic-client");
            form.EditValue(["env.KEYCLOAK_BLAZOR_CLIENT_SECRET"], "synthetic-switch-back-secret");
            form.SelectLocale("ar");
        });
        component.Render();
        await Assert.That(component.FindComponent<MudSelect<string>>().Instance.Value).IsEqualTo("1");
        await Choose("0");
        long revision = form.InputRevision;
        await component.InvokeAsync(() => retired.InvokeAsync("2"));
        await Assert.That(form.InputRevision).IsEqualTo(revision);
        await Assert.That(form.Snapshot().Values["env.AUTHENTICATION_PROVIDER"].GetString()).IsEqualTo("local");
        await Assert.That(form.Snapshot().Values["env.KEYCLOAK_BLAZOR_CLIENT_SECRET"].GetString()).IsEqualTo("synthetic-switch-back-secret");
        DefinitionArtifactResult complete = DefinitionArtifactComposer.Compose(definition, "environment-standalone", form.Snapshot());
        using DefinitionArtifact? completeArtifact = complete.Artifact;
        await Assert.That(complete.Succeeded).IsTrue();
        await Assert.That(Encoding.UTF8.GetString(completeArtifact!.Bytes.Span)).DoesNotContain("KEYCLOAK_");
        using DefinitionDraftArtifactResult draft = DefinitionDraftArtifactComposer.Compose(definition, "environment-standalone", form.Snapshot());
        await Assert.That(draft.Succeeded).IsTrue();
        await Assert.That(Encoding.UTF8.GetString(draft.Artifact!.Bytes.Span)).DoesNotContain("KEYCLOAK_");
        await Assert.That(draft.Completions.Any(item => item.FieldPath.Contains("KEYCLOAK", StringComparison.Ordinal))).IsFalse();
        await Choose("1");
        await Assert.That(form.Validate().TryGetEffectiveValue(DefinitionPath.Parse("/env.KEYCLOAK_BLAZOR_CLIENT_SECRET"), out JsonElement restored)).IsTrue();
        await Assert.That(restored.GetString()).IsEqualTo("synthetic-switch-back-secret");
        await Choose("0");
        await component.InvokeAsync(() => form.SetSecretEntryEnabled(false));
        await Assert.That(form.Snapshot().Values.ContainsKey("env.KEYCLOAK_BLAZOR_CLIENT_SECRET")).IsFalse();
        await Assert.That(form.Snapshot().Values["env.AUTHENTICATION_PROVIDER"].GetString()).IsEqualTo("local");

        async Task Choose(string ordinal)
        {
            await component.InvokeAsync(() => component.FindComponent<MudSelect<string>>().Instance.ValueChanged.InvokeAsync(ordinal));
            component.Render();
        }
    }
}
