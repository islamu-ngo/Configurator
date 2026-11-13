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
        var definition = BrowserDefinitionRegistry.LoadBundled().Current.Resolve(Guid.Parse("f79259e8-af23-49b0-8d8d-6e371cc96080"));
        await using var context = BrowserFixture.Context();
        using var form = new DefinitionFormModel(definition, DefinitionSessionPrivacy.BrowserSecretsEnabled);
        form.SetDisclosureOverride(FieldDisclosure.Advanced);
        form.EditValue(["secrets", "provider"], "Environment");
        form.EditValue(["recipientAuthorities", "bffSession", "redisEndpoint"], "redis://session.example.test:6379");
        form.EditValue(["recipientAuthorities", "bffSession", "namespace"], "synthetic-event");
        form.EditValue(["recipientAuthorities", "bffSession", "redisPassword"], "synthetic-bff-session-password");
        form.EditValue(["recipientAuthorities", "dataProtection", "combined", "ring"], "synthetic-combined-authority-ring");
        form.EditValue(["recipientAuthorities", "dataProtection", "combined", "admission"], "synthetic-combined-authority-admission");
        form.EditValue(["recipientAuthorities", "dataProtection", "combined", "admissionSha256"], new string('a', 64));
        form.EditValue(["recipientAuthorities", "dataProtection", "combined", "minimumGeneration"], "1");
        var component = context.Render<ConfigurationSectionFields>(parameters => parameters
            .Add(item => item.Form, form)
            .Add(item => item.Fields, form.Validate().Fields.Where(field => field.Id == "platform").ToArray()));
        await Assert.That(Provider().FindComponents<MudSelect<string>>().Count).IsEqualTo(1);
        await Choose("1");
        await Assert.That(form.Snapshot().Values["platform"].GetProperty("authenticationProvider").GetString()).IsEqualTo("keycloak");
        var retired = Provider().FindComponent<MudSelect<string>>().Instance.ValueChanged;
        await component.InvokeAsync(() =>
        {
            form.EditValue(["keycloak", "endpoint"], "https://identity.example.test");
            form.EditValue(["keycloak", "realm"], "synthetic-realm");
            form.EditValue(["keycloak", "clientId"], "synthetic-client");
            form.EditValue(["keycloak", "blazorClientSecret"], "synthetic-switch-back-secret");
            form.SelectLocale("ar");
        });
        component.Render();
        // Public choices inside a group that also contains protected settings stay unmasked, while the group container
        // and any declared sensitive or secret members remain masked and bound to exact wire tokens.
        await Assert.That(form.GetFieldValidation(AuthenticationProviderPath).RequiresMasking).IsFalse();
        await Assert.That(form.GetFieldValidation(["platform"]).RequiresMasking).IsTrue();
        await Assert.That(form.GetFieldValidation(["keycloak", "blazorClientSecret"]).RequiresMasking).IsTrue();
        await Assert.That(form.Snapshot().Values["platform"].GetProperty("authenticationProvider").GetString()).IsEqualTo("keycloak");
        await Choose("0");
        long revision = form.InputRevision;
        await component.InvokeAsync(() => retired.InvokeAsync("2"));
        await Assert.That(form.InputRevision).IsEqualTo(revision);
        await Assert.That(form.Snapshot().Values["platform"].GetProperty("authenticationProvider").GetString()).IsEqualTo("local");
        await Assert.That(form.Snapshot().Values["keycloak"].GetProperty("blazorClientSecret").GetString()).IsEqualTo("synthetic-switch-back-secret");
        DefinitionArtifactResult complete = DefinitionArtifactComposer.Compose(definition, "environment-standalone", form.Snapshot());
        using DefinitionArtifact? completeArtifact = complete.Artifact;
        await Assert.That(complete.Succeeded).IsTrue();
        await Assert.That(Encoding.UTF8.GetString(completeArtifact!.Bytes.Span)).DoesNotContain("KEYCLOAK_");
        using DefinitionDraftArtifactResult draft = DefinitionDraftArtifactComposer.Compose(definition, "environment-standalone", form.Snapshot());
        await Assert.That(draft.Succeeded).IsTrue();
        await Assert.That(Encoding.UTF8.GetString(draft.Artifact!.Bytes.Span)).DoesNotContain("KEYCLOAK_");
        await Assert.That(draft.Completions.Any(item => item.FieldPath.Contains("KEYCLOAK", StringComparison.Ordinal))).IsFalse();
        await Choose("1");
        await Assert.That(form.Validate().TryGetEffectiveValue(DefinitionPath.Parse("/keycloak/blazorClientSecret"), out JsonElement restored)).IsTrue();
        await Assert.That(restored.GetString()).IsEqualTo("synthetic-switch-back-secret");
        await Choose("0");
        await component.InvokeAsync(() => form.SetSecretEntryEnabled(false));
        await Assert.That(form.Snapshot().Values.TryGetValue("keycloak", out JsonElement keycloak) && keycloak.TryGetProperty("blazorClientSecret", out _)).IsFalse();
        await Assert.That(form.Snapshot().Values["platform"].GetProperty("authenticationProvider").GetString()).IsEqualTo("local");

        async Task Choose(string ordinal)
        {
            await component.InvokeAsync(() => Provider().FindComponent<MudSelect<string>>().Instance.ValueChanged.InvokeAsync(ordinal));
            component.Render();
        }

        // The provider is one member of the platform group, which renders several other choices.
        IRenderedComponent<ConfigurationField> Provider() => component.FindComponents<ConfigurationField>()
            .Single(field => field.Instance.Path.SequenceEqual(AuthenticationProviderPath));
    }

    private static readonly string[] AuthenticationProviderPath = ["platform", "authenticationProvider"];
}
