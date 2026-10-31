namespace Configurator.Browser.Tests;

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bunit;
using Configurator.Core.Definitions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

public sealed class BrowserTypedPrivacyTests
{
    [Test]
    public async Task ChoicePopupsPreserveEmptyStringFalseAndAbsence()
    {
        await using var context = BrowserFixture.Context();
        var provider = context.Render<MudBlazor.MudPopoverProvider>();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, WithField("emptyChoice", """
            {"type":"string","title":"Empty choice","enum":["","other"],"x-configurator":{"sensitivity":"public","detail":"standard"}}
            """)));
        var form = component.FindComponents<ConfigurationField>()[0].Instance.Form;
        await component.InvokeAsync(() => form.SetDisclosureOverride(SetupFieldDisclosure.Guided));
        Task OpenAsync(string field) => component.InvokeAsync(() => component.FindComponents<ConfigurationField>()
            .Single(item => string.Join('.', item.Instance.Path) == field).FindComponent<MudBlazor.MudSelect<string>>().Instance.OpenMenu());
        await OpenAsync("emptyChoice");
        await provider.FindAll("[role=option]")[1].ClickAsync(new MouseEventArgs());
        await Assert.That(form.Snapshot().Values["emptyChoice"].GetString()).IsEqualTo("");
        await OpenAsync("emptyChoice");
        await provider.FindAll("[role=option]")[0].ClickAsync(new MouseEventArgs());
        await Assert.That(form.Snapshot().Values.ContainsKey("emptyChoice")).IsFalse();
        await BrowserFixture.NotifyChoiceAsync(component, "mode", "cluster");
        await OpenAsync("replicas");
        await provider.FindAll("[role=option]")[2].ClickAsync(new MouseEventArgs());
        await Assert.That(form.Snapshot().Values["replicas"].GetBoolean()).IsFalse();
        var control = component.Find("[data-field='replicas'] div[role=combobox]");
        string labelId = control.GetAttribute("aria-labelledby")!;
        await Assert.That(component.Find("#" + labelId).TagName).IsEqualTo("LABEL");
        await OpenAsync("replicas");
        await provider.FindAll("[role=option]")[0].ClickAsync(new MouseEventArgs());
        await Assert.That(form.Snapshot().Values.ContainsKey("replicas")).IsFalse();
    }

    [Test]
    public async Task EmptyEnumSelectionAndAccessibleOmissionHaveDistinctIdentities()
    {
        await using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, WithField("emptyChoice", """
            {"type":"string","title":"Empty choice","enum":["","other"],"x-configurator":{"sensitivity":"public","detail":"standard"}}
            """)));
        var form = component.FindComponents<ConfigurationField>()[0].Instance.Form;
        var editor = component.FindComponents<ConfigurationField>().Single(item => item.Instance.Path.SequenceEqual(["emptyChoice"]));
        var options = editor.FindComponents<MudBlazor.MudSelectItem<string>>();
        await Assert.That(options[0].Instance.Value).IsNotEqualTo(options[1].Instance.Value);
        await component.InvokeAsync(() => editor.FindComponent<MudBlazor.MudSelect<string>>().Instance.ValueChanged.InvokeAsync(options[1].Instance.Value));
        await Assert.That(form.Snapshot().Values["emptyChoice"].GetRawText()).IsEqualTo("\"\"");
        var omit = await BrowserFixture.OpenOmissionAsync(context, component, "emptyChoice");
        await Assert.That(omit.GetAttribute("aria-label")).IsEqualTo("Omit Empty choice");
        await Assert.That(omit.GetAttribute("role")).IsEqualTo("menuitem");
        await omit.ClickAsync(new MouseEventArgs());
        await Assert.That(form.Snapshot().Values.ContainsKey("emptyChoice")).IsFalse();
        await Assert.That(editor.FindComponent<MudBlazor.MudSelect<string>>().Instance.Value).IsEqualTo("");
    }

    [Test]
    public async Task NestedSecretOmissionStaysAbsentAndOrdinaryEmptyStringStaysSupplied()
    {
        await using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, WithField("settings", """
            {"type":"object","title":"Settings","additionalProperties":false,"x-configurator":{"sensitivity":"public","detail":"standard"},"properties":{
              "ordinary":{"type":"string","title":"Ordinary setting","x-configurator":{"sensitivity":"public","detail":"standard"}},
              "secret":{"type":"string","title":"Secret setting","x-configurator":{"sensitivity":"secret","detail":"standard"}}}}
            """)));
        await component.Find("[data-field='settings.ordinary'] input").InputAsync(new ChangeEventArgs { Value = "erase" });
        await component.Find("[data-field='settings.ordinary'] input").InputAsync(new ChangeEventArgs { Value = "" });
        var form = component.FindComponents<ConfigurationField>()[0].Instance.Form;
        await Assert.That(form.Snapshot().Values["settings"].GetProperty("ordinary").GetRawText()).IsEqualTo("\"\"");
        await Assert.That(component.Find("[data-field='settings.secret'] .configuration-field-actions__menu button").HasAttribute("disabled")).IsTrue();
        await BrowserFixture.OmitAsync(context, component, "settings.ordinary");
        await Assert.That(form.Snapshot().Values["settings"].GetRawText()).IsEqualTo("{}");
    }

    [Test]
    public async Task RejectedNestedEditFencesImportBeforeAnyTypedValueExists()
    {
        await using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, WithField("settings", """
            {"type":"object","title":"Settings","additionalProperties":false,"x-configurator":{"sensitivity":"public","detail":"standard"},"properties":{
              "enabled":{"type":"boolean","title":"Enabled","x-configurator":{"sensitivity":"sensitive","detail":"standard"}}}}
            """)));
        await component.Find("#secret-consent").ChangeAsync(new ChangeEventArgs { Value = true });
        var form = component.FindComponents<ConfigurationField>()[0].Instance.Form;
        long before = form.InputRevision;
        var read = new TaskCompletionSource<ReadOnlyMemory<byte>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<DefinitionImportResult> import = form.ImportAsync("config", _ =>
        {
            entered.SetResult();
            return read.Task;
        });
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await component.Find("[data-field='settings.enabled'] input").InputAsync(new ChangeEventArgs { Value = "invalid" });
        read.SetResult("""{"mode":"single","hostname":"late.example","credential":"synthetic"}"""u8.ToArray());
        await Assert.That(form.InputRevision).IsEqualTo(before + 1);
        await Assert.That(form.HasInputErrors).IsTrue();
        await Assert.That((await import.WaitAsync(TimeSpan.FromSeconds(5))).Code).IsEqualTo("import-stale");
    }

    [Test]
    public async Task LocallyCompiledNestedSecretsRequireConsentAndRevocationClearsOutput()
    {
        await using var context = BrowserFixture.Context();
        var javascript = context.Services.GetRequiredService<IJSRuntime>();
        await using var policy = new BrowserOfflinePolicy(javascript);
        await using var source = new BrowserDefinitionSource(new BrowserDefinitionRegistry([]), javascript, policy);
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(BrowserDefinitionSourceTests.AuthoringJson));
        SetupDefinition definition = await source.ReadLocalAsync(input);
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, definition));
        component.Find("[data-field='HOST'] input").Input("example.test");
        await Assert.That(component.FindAll("[data-field='settings.secret'] input")).IsEmpty();
        component.Find("#secret-consent").Change(true);
        component.Find("[data-field='settings.secret'] input").Input("synthetic-local-nested-secret");
        component.Find("#prepare-complete").Click();
        await Assert.That(component.FindAll(".configuration-editor__artifact")).Count().IsEqualTo(1);
        component.Find("#secret-consent").Change(false);
        await Assert.That(component.FindAll(".configuration-editor__artifact")).IsEmpty();
        var form = component.FindComponents<ConfigurationField>()[0].Instance.Form;
        await Assert.That(form.Snapshot().Values["settings"].TryGetProperty("secret", out _)).IsFalse();
        await Assert.That(component.Markup).DoesNotContain("synthetic-local-nested-secret");
        await Assert.That(context.JSInterop.Invocations.Any(invocation => invocation.Identifier is "fetchDefinition" or "retainDefinition")).IsFalse();
    }

    [Test]
    public async Task SensitiveChildrenAreMaskedButEditableWithoutSecretConsent()
    {
        await using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, WithField("settings", """
            {"type":"object","title":"Settings","additionalProperties":false,"x-configurator":{"sensitivity":"sensitive","detail":"standard"},"properties":{
              "ordinary":{"type":"string","title":"Private setting","x-configurator":{"sensitivity":"public","detail":"standard"}},
              "secret":{"type":"string","title":"Secret setting","x-configurator":{"sensitivity":"secret","detail":"standard"}}}}
            """)));
        var ordinary = component.Find("[data-field='settings.ordinary'] input");
        await Assert.That(ordinary.GetAttribute("type")).IsEqualTo("password");
        await Assert.That(ordinary.HasAttribute("disabled")).IsFalse();
        ordinary.Input("synthetic-private-value");
        await Assert.That(component.FindAll("[data-field='settings.secret'] input")).IsEmpty();
        component.Find("#secret-consent").Change(true);
        component.Find("[data-field='settings.secret'] input").Input("synthetic-nested-secret");
        component.Find("#secret-consent").Change(false);
        var form = component.FindComponents<ConfigurationField>()[0].Instance.Form;
        await Assert.That(form.Snapshot().Values["settings"].GetProperty("ordinary").GetString()).IsEqualTo("synthetic-private-value");
        await Assert.That(form.Snapshot().Values["settings"].TryGetProperty("secret", out _)).IsFalse();
        await Assert.That(component.Markup).DoesNotContain("synthetic-nested-secret");
    }

    [Test]
    public async Task TypedFalseAndExplicitEmptyCollectionsRemainDistinctFromAbsence()
    {
        await using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, WithField("settings", """
            {"type":"object","title":"Settings","additionalProperties":false,"x-configurator":{"sensitivity":"public","detail":"standard"},"properties":{
              "enabled":{"type":"boolean","title":"Enabled","x-configurator":{"sensitivity":"public","detail":"standard"}},
              "names":{"type":"array","title":"Names","x-configurator":{"sensitivity":"public","detail":"standard"},
                "items":{"type":"string","x-configurator":{"sensitivity":"public"}},"maxItems":3}}}
            """, "schema.array", "schema.bounds")));
        var form = component.FindComponents<ConfigurationField>()[0].Instance.Form;
        await Assert.That(form.Snapshot().Values.ContainsKey("settings")).IsFalse();
        await BrowserFixture.NotifyChoiceAsync(component, "settings.enabled", "false");
        await Assert.That(form.Snapshot().Values["settings"].GetProperty("enabled").GetBoolean()).IsFalse();
        await BrowserFixture.NotifyChoiceAsync(component, "settings.enabled", "");
        await Assert.That(form.Snapshot().Values["settings"].EnumerateObject().Count()).IsEqualTo(0);
        component.Find("[data-field='settings.names'] [data-action='supply-list']").Click();
        JsonElement settings = form.Snapshot().Values["settings"];
        await Assert.That(settings.GetProperty("names").ValueKind).IsEqualTo(JsonValueKind.Array);
        await Assert.That(settings.GetProperty("names").GetArrayLength()).IsEqualTo(0);
    }

    [Test]
    public async Task SuppliedOptionalCompoundWithRequiredSecretOffersDraftOrConsentRecovery()
    {
        JsonNode document = JsonNode.Parse(WithField("settings", """
            {"type":"object","title":"Settings","additionalProperties":false,"required":["secret"],
             "x-configurator":{"sensitivity":"public","detail":"standard"},"properties":{
              "ordinary":{"type":"string","title":"Ordinary setting","x-configurator":{"sensitivity":"public","detail":"standard"}},
              "secret":{"type":"string","title":"Required secret","x-configurator":{"sensitivity":"secret","detail":"standard"}}}}
            """).CanonicalBytes.Span)!;
        document["configuration"]!["properties"]!.AsObject().Remove("credential");
        document["configuration"]!["required"] = new JsonArray("mode", "replicas", "hostname");
        var members = document["artifacts"]![0]!["projection"]!["members"]!.AsObject();
        members.Remove("credential");
        members["settings"] = JsonNode.Parse("""{"op":"copy","reference":{"scope":"root","path":"/settings"}}""");
        SetupDefinition definition = SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(document.ToJsonString()));
        await using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, definition));
        await BrowserFixture.NotifyChoiceAsync(component, "mode", "single");
        component.Find("[data-field='hostname'] input").Input("example.test");
        component.Find("[data-field='settings.ordinary'] input").Input("synthetic-ordinary-setting");
        component.Find("#prepare-complete").Click();
        await Assert.That(component.FindAll(".configuration-editor__artifact")).IsEmpty();
        await Assert.That(component.FindAll("[data-action='enable-secret-entry']")).Count().IsEqualTo(1);
        component.Find("[data-action='enable-secret-entry']").Click();
        component.Find("[data-field='settings.secret'] input").Input("synthetic-compound-secret");
        component.Find("#prepare-complete").Click();
        await Assert.That(component.FindAll(".configuration-editor__artifact")).Count().IsEqualTo(1);
        await Assert.That(component.Find(".configuration-editor__artifact").GetAttribute("data-readiness")).IsEqualTo("complete");
        component.Find("#secret-consent").Change(false);
        await Assert.That(component.FindAll(".configuration-editor__artifact")).IsEmpty();
        await Assert.That(component.Markup).DoesNotContain("synthetic-compound-secret");
    }

    private static SetupDefinition WithField(string name, string schema, params string[] capabilities)
    {
        JsonNode document = JsonNode.Parse(BrowserFixture.Definition.CanonicalBytes.Span)!;
        BrowserFixture.WithField(document, name, schema, capabilities);
        return SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(document.ToJsonString()));
    }
}
