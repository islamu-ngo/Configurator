namespace Configurator.Browser.Tests;

using System.Text.Json;
using Bunit;
using Configurator.Browser.Components.Configuration;
using Configurator.Browser.Services;
using Configurator.Core.Definitions;
using Configurator.Presentation;
using MudBlazor;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

public sealed class BrowserV2EditingTests
{
    private static readonly string?[] ChoiceOrdinals = ["", "0", "1", "2"];
    [Test]
    public async Task ConditionalRequiredLabelsFollowCoreFactsRatherThanStaticSchemaHints()
    {
        using var form = Form();
        await using var context = BrowserFixture.Context(new BrowserDefinitionRegistry([form.Definition]));
        var field = context.Render<ConfigurationField>(parameters => parameters
            .Add(component => component.Form, form).Add(component => component.Path, ["settings", "value"])
            .Add(component => component.Schema, form.Definition.Fields.Single(item => item.Name == "settings")
                .Node.Properties["value"]));
        await Assert.That(field.Find(".configuration-field__requirement").TextContent).IsEqualTo("Optional");
        form.SetValue(["settings", "enabled"], JsonSerializer.SerializeToElement(true));
        field.Render();
        await Assert.That(field.Find(".configuration-field__requirement").TextContent).IsEqualTo("Required");
        form.SetValue(["settings", "enabled"], JsonSerializer.SerializeToElement(false));
        field.Render();
        await Assert.That(field.Find(".configuration-field__requirement").TextContent).IsEqualTo("Optional");
    }
    [Test]
    public async Task LocalFilePickerUsesDeclaredDefaultLocaleBeforeAnswerCollection()
    {
        using var seed = Form();
        await using var context = BrowserFixture.Context(new BrowserDefinitionRegistry([]));
        await using var policy = new BrowserOfflinePolicy(context.JSInterop.JSRuntime);
        context.Services.AddSingleton(policy);
        var page = context.Render<Configurator.Browser.Pages.Open>();
        var input = page.FindComponent<InputFile>();
        await page.InvokeAsync(() => input.UploadFiles(
            InputFileContent.CreateFromBinary(seed.Definition.CanonicalBytes.ToArray(), "synthetic-local.json")));
        await page.Find("[data-action='open-workspace']").ClickAsync(new MouseEventArgs());
        await Assert.That(page.Find(".configuration-editor").GetAttribute("lang")).IsEqualTo("en");
        await Assert.That(page.Find("#configuration-locale").GetAttribute("value")).IsEqualTo("en");
    }

    [Test]
    public async Task LaunchAndWorkspaceUseDeclaredDefaultLocaleWithoutEditingAnswers()
    {
        using var seed = Form();
        var registry = new BrowserDefinitionRegistry([seed.Definition]);
        BrowserLaunchSelection selected = BrowserLaunchSelection.Resolve(registry.Current, seed.Definition.FamilyId.ToString("D"), "");
        await Assert.That(selected.Locale).IsEqualTo("en");
        await using var context = BrowserFixture.Context(registry);
        var editor = context.Render<ConfigurationEditor>(parameters => parameters
            .Add(component => component.Definition, seed.Definition)
            .Add(component => component.InitialLocale, "ar"));
        var form = editor.FindComponents<ConfigurationField>()[0].Instance.Form;
        long revision = form.InputRevision;
        await Assert.That(editor.Find(".configuration-editor").GetAttribute("lang")).IsEqualTo("ar");
        await Assert.That(editor.Find(".configuration-editor").GetAttribute("dir")).IsEqualTo("rtl");
        await Assert.That(editor.Find("[data-field='choice'] label").TextContent).Contains("Translated choice");
        editor.Find("#configuration-locale").Change("en");
        await Assert.That(editor.Find(".configuration-editor").GetAttribute("dir")).IsEqualTo("ltr");
        await Assert.That(form.InputRevision).IsEqualTo(revision);
        await Assert.That(form.Snapshot().Values.Count).IsEqualTo(0);
    }

    [Test]
    public async Task TypedChoicesRetainDistinctOrdinalsAndNullOmissionResetAreSeparateActions()
    {
        using var form = Form();
        await using var context = BrowserFixture.Context(new BrowserDefinitionRegistry([form.Definition]));
        var field = context.Render<ConfigurationField>(parameters => parameters
            .Add(component => component.Form, form).Add(component => component.Path, ["choice"])
            .Add(component => component.Schema, form.Definition.Fields.Single(item => item.Name == "choice").Node));
        await Assert.That(field.FindComponents<MudSelectItem<string>>().Select(item => item.Instance.Value))
            .IsEquivalentTo(ChoiceOrdinals);
        var staleReset = field.FindComponent<ConfigurationFieldActions>().Instance.OnReset;
        await field.InvokeAsync(() => field.FindComponent<ConfigurationFieldActions>().Instance.OnSuggest.InvokeAsync());
        await Assert.That(form.Snapshot().Values["choice"].GetInt32()).IsEqualTo(2);
        await field.InvokeAsync(() => staleReset.InvokeAsync());
        await Assert.That(form.Snapshot().Values["choice"].GetInt32()).IsEqualTo(2);
        field.Render();
        await field.InvokeAsync(() => field.FindComponent<ConfigurationFieldActions>().Instance.OnNull.InvokeAsync());
        await Assert.That(form.Snapshot().Values["choice"].ValueKind).IsEqualTo(JsonValueKind.Null);
        field.Render();
        await field.InvokeAsync(() => field.FindComponent<ConfigurationFieldActions>().Instance.OnOmit.InvokeAsync());
        await Assert.That(form.Validate().TryGetEffectiveValue(DefinitionPath.Parse("/choice"), out _)).IsFalse();
        field.Render();
        long before = form.InputRevision;
        await field.InvokeAsync(() => field.FindComponent<ConfigurationFieldActions>().Instance.OnReset.InvokeAsync());
        await Assert.That(form.InputRevision).IsEqualTo(before + 1);
        await Assert.That(form.Validate().TryGetEffectiveValue(DefinitionPath.Parse("/choice"), out JsonElement initialized)).IsTrue();
        await Assert.That(initialized.GetInt32()).IsEqualTo(1);
    }

    [Test]
    public async Task RetiredVariantCallbackCannotEditAfterViewOnlyUnmountAndRemount()
    {
        using var form = Form();
        await using var context = BrowserFixture.Context(new BrowserDefinitionRegistry([form.Definition]));
        form.SetEditorExpanded(["provider"], true);
        var field = context.Render<ConfigurationField>(parameters => parameters
            .Add(component => component.Form, form).Add(component => component.Path, ["provider"])
            .Add(component => component.Schema, form.Definition.Fields.Single(item => item.Name == "provider").Node));
        var stale = field.FindComponent<MudSelect<string>>().Instance.ValueChanged;
        form.SetEditorExpanded(["provider"], false);
        field.Render();
        form.SetEditorExpanded(["provider"], true);
        field.Render();
        await field.InvokeAsync(() => stale.InvokeAsync("1"));
        await Assert.That(form.Snapshot().Values.ContainsKey("provider")).IsFalse();
        await field.InvokeAsync(() => field.FindComponent<MudSelect<string>>().Instance.ValueChanged.InvokeAsync("1"));
        await Assert.That(form.Snapshot().Values["provider"].GetProperty("kind").GetString()).IsEqualTo("second");
    }

    [Test]
    public async Task TypedMapEntriesRenderTheirDeclaredScalarEditors()
    {
        using var form = Form();
        await using var context = BrowserFixture.Context(new BrowserDefinitionRegistry([form.Definition]));
        form.SetValue(["map"], JsonSerializer.SerializeToElement(new Dictionary<string, int> { ["literal/key"] = 4 }));
        var field = context.Render<ConfigurationField>(parameters => parameters
            .Add(component => component.Form, form)
            .Add(component => component.Path, ["map"])
            .Add(component => component.Schema, form.Definition.Fields.Single(item => item.Name == "map").Node));
        await Assert.That(field.FindAll("[data-field='map.literal/key'] input").Count).IsEqualTo(1);
        await Assert.That(field.Markup).DoesNotContain("<textarea");
    }

    [Test]
    public async Task ProviderBranchSelectionRendersOnlyTheSelectedDeclaredChildren()
    {
        using var form = Form();
        await using var context = BrowserFixture.Context(new BrowserDefinitionRegistry([form.Definition]));
        form.SetValue(["provider"], JsonSerializer.SerializeToElement(new { kind = "first", count = 2 }));
        var field = context.Render<ConfigurationField>(parameters => parameters
            .Add(component => component.Form, form)
            .Add(component => component.Path, ["provider"])
            .Add(component => component.Schema, form.Definition.Fields.Single(item => item.Name == "provider").Node));
        await Assert.That(field.FindComponents<MudSelect<string>>().Count).IsGreaterThanOrEqualTo(1);
        await Assert.That(field.FindAll("[data-field='provider.count']").Count).IsEqualTo(1);
        await Assert.That(field.FindAll("[data-field='provider.other']").Count).IsEqualTo(0);
    }

    private static DefinitionFormModel Form() => new(SetupDefinitionSourceCodec.ParseJson("""
        {"$schema":"urn:islamu:configurator:authoring:v2",
         "identity":{"familyId":"104f9cb6-a3bd-498f-a0c0-10d57a7d4732","revision":1},
         "project":{"id":"browser-typed","name":"Browser typed"},"applicationVersions":["1"],
         "configuration":{"type":"object","additionalProperties":false,"x-configurator":{"sensitivity":"public"},"properties":{
           "settings":{"type":"object","additionalProperties":false,"x-configurator":{"sensitivity":"public","initialValue":{}},
             "properties":{"enabled":{"type":"boolean","x-configurator":{"sensitivity":"public","initialValue":false}},
               "value":{"type":"integer","x-configurator":{"sensitivity":"public","detail":"basic"}}},
             "if":{"properties":{"enabled":{"const":true}},"required":["enabled"]},"then":{"required":["value"]}},
           "map":{"type":"object","x-configurator":{"sensitivity":"public"},"additionalProperties":{"type":"integer","x-configurator":{"sensitivity":"public"}}},
           "choice":{"type":["integer","null"],"enum":[1,2,null],"title":"Choice","x-configurator":{"sensitivity":"public","detail":"basic",
             "initialValue":1,"suggestedValue":2,"choices":[{"value":1,"label":"Equal"},{"value":2,"label":"Equal"},{"value":null,"label":"Null"}]}},
           "provider":{"type":"object","x-configurator":{"sensitivity":"public"},"oneOf":[
             {"type":"object","additionalProperties":false,"required":["kind"],"x-configurator":{"sensitivity":"public"},"properties":{
               "kind":{"type":"string","const":"first","x-configurator":{"sensitivity":"public"}},"count":{"type":"integer","x-configurator":{"sensitivity":"public"}}}},
             {"type":"object","additionalProperties":false,"required":["kind"],"x-configurator":{"sensitivity":"public"},"properties":{
               "kind":{"type":"string","const":"second","x-configurator":{"sensitivity":"public"}},"other":{"type":"string","x-configurator":{"sensitivity":"public"}}}}]}}},
         "localization":{"defaultLocale":"en","translations":{"ar":{"direction":"rtl","overrides":{
           "/configuration/properties/choice/title":"Translated choice"}}}},
         "outputs":{"config.json":{"id":"config","format":"json"}}}
        """u8.ToArray()), DefinitionSessionPrivacy.BrowserNoSecrets);
}
