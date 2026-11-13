namespace Configurator.Browser.Tests;

using Bunit;
using Configurator.Browser.Models;
using Configurator.Core.Definitions;
using Configurator.Presentation;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;

public sealed class BrowserRootEditingTests
{
    [Test]
    public async Task RootHeadingUsesTheLocalizedConfigurationTitle()
    {
        using Stream original = Resource("Conformance.RootMap");
        JsonNode source = JsonNode.Parse(original)!;
        source["configuration"]!["title"] = "Base root title";
        source["localization"] = JsonNode.Parse("""
            {"defaultLocale":"en","translations":{"ar":{"direction":"rtl","overrides":{
              "/configuration/title":"\u0639\u0646\u0648\u0627\u0646 \u0627\u0644\u062c\u0630\u0631"}}}}
            """);
        var definition = DefinitionSourceCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString()));
        await using var context = BrowserFixture.Context();
        var root = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, definition));
        await Assert.That(root.Find("[data-configuration-root] h3").TextContent).IsEqualTo("Base root title");
        await root.InvokeAsync(() => Field(root).Instance.Form.SelectLocale("ar"));
        await Assert.That(root.Find("[data-configuration-root] h3").TextContent)
            .IsEqualTo("\u0639\u0646\u0648\u0627\u0646 \u0627\u0644\u062c\u0630\u0631");
    }

    [Test]
    public async Task LiteralRootCollectionRemovalPreservesTheSurvivingEditorAndRejectsItsOldCallback()
    {
        using Stream sourceStream = Resource("Conformance.RootMap");
        JsonNode source = JsonNode.Parse(sourceStream)!;
        source["configuration"]!["additionalProperties"] = JsonNode.Parse("""
            {"type":"object","additionalProperties":false,"x-configurator":{"sensitivity":"public"},"properties":{
              "items":{"type":"array","maxItems":3,"x-configurator":{"sensitivity":"public"},"items":{
                "type":"object","additionalProperties":false,"x-configurator":{"sensitivity":"public"},"properties":{
                  "choice":{"type":"string","enum":["first","second"],"x-configurator":{"sensitivity":"public"}}}}}}}
            """);
        var definition = DefinitionSourceCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString()));
        await using var context = BrowserFixture.Context();
        var root = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, definition));
        await AddKey(root, "group");
        await Field(root, "group", "items").Find("[data-action='add-item']").ClickAsync(new MouseEventArgs());
        await Field(root, "group", "items").Find("[data-action='add-item']").ClickAsync(new MouseEventArgs());
        await Choose(root, ["group", "items", "0", "choice"], "0");
        await Choose(root, ["group", "items", "1", "choice"], "1");
        var survivor = Field(root, "group", "items", "1", "choice");
        var stale = survivor.FindComponent<MudSelect<string>>().Instance.ValueChanged;
        await Field(root, "group", "items").FindAll("[data-action='remove-item']")[0].ClickAsync(new MouseEventArgs());
        await Assert.That(Field(root, "group", "items", "0", "choice").Instance).IsSameReferenceAs(survivor.Instance);
        await root.InvokeAsync(() => stale.InvokeAsync("0"));
        await Assert.That(Field(root).Instance.Form.Snapshot().Values["group"].GetProperty("items")[0].GetProperty("choice").GetString()).IsEqualTo("second");
    }

    [Test]
    public async Task LiteralKeyEqualToAnOrdinaryFieldIdentityDoesNotEditThatField()
    {
        JsonNode source = JsonNode.Parse(Definition("Conformance.RootMap").CanonicalBytes.Span)!;
        source["configuration"]!["properties"] = new JsonObject
        {
            ["declared/name"] = source["configuration"]!["additionalProperties"]!.DeepClone()
        };
        var definition = DefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString()));
        string key = definition.Fields.Single().Id;
        await using var context = BrowserFixture.Context();
        var root = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, definition));
        var form = Field(root).Instance.Form;
        await Choose(root, ["declared/name"], "1");
        await AddKey(root, key);
        await Choose(root, [key], "2");
        await Assert.That(form.Snapshot().Values["declared/name"].GetInt32()).IsEqualTo(7);
        await Assert.That(form.Snapshot().Values[key].GetInt32()).IsEqualTo(30);
        await Action(root, [key], actions => actions.OnOmit);
        await Assert.That(form.Snapshot().Values["declared/name"].GetInt32()).IsEqualTo(7);
        await Assert.That(form.Snapshot().Values.ContainsKey(key)).IsFalse();
    }

    [Test]
    [Arguments("variant")]
    [Arguments("revoke")]
    [Arguments("replacement")]
    public async Task RetiredRootImportCannotRestoreItsAnswers(string transition)
    {
        await using var context = BrowserFixture.Context();
        var root = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, Definition("Conformance.RootVariant")));
        var form = Field(root).Instance.Form;
        await root.Find("#secret-consent").ChangeAsync(new ChangeEventArgs { Value = true });
        await Choose(root, [], "0");
        using var held = new HeldStream("""{"mode":"disk","directory":"synthetic-late-directory"}"""u8.ToArray());
        Task? loading = null;
        await root.InvokeAsync(() => { loading = root.Instance.LoadAsync(held); });
        await held.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        if (transition == "variant") await Choose(root, [], "1");
        else if (transition == "revoke") await root.Find("#secret-consent").ChangeAsync(new ChangeEventArgs { Value = false });
        else await root.InvokeAsync(() => root.Render(parameters => parameters.Add(item => item.Definition, Definition("Conformance.RootMap"))));
        held.Release.TrySetResult();
        await loading!.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(root.Markup).DoesNotContain("synthetic-late-directory");
        if (transition == "variant")
        {
            await Assert.That(form.Snapshot().Values["mode"].GetString()).IsEqualTo("queue");
            await Assert.That(form.Snapshot().Values.ContainsKey("directory")).IsFalse();
        }
        else if (transition == "replacement")
            await Assert.That(Field(root).Instance.Form.Snapshot().Values).IsEmpty();
        else await Assert.That(form.Snapshot().Values.ContainsKey("directory")).IsFalse();
    }

    [Test]
    public async Task ReviewCorrectionReturnsToTheExactRootControl()
    {
        await using var context = BrowserFixture.Context();
        var root = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, Definition("Conformance.RootVariant")));
        await Choose(root, [], "0");
        var form = Field(root).Instance.Form;
        await Assert.That(form.Validate().Diagnostics.Single().FieldId).IsEqualTo("directory");
        await BrowserFixture.SelectPanelAsync(root, "review");
        await root.Find("[data-correction='directory']").ClickAsync(new MouseEventArgs());
        await Assert.That(Field(root, "directory").Find("input").HasAttribute("disabled")).IsFalse();
        await Assert.That(Field(root, "directory").Instance.FocusFirstControl).IsTrue();
        await Type(root, ["directory"], "spool");
        await Assert.That(Field(root).Instance.Form.Validate().IsReady).IsTrue();
    }

    [Test]
    public async Task UnlocatedRootMapCorrectionOpensTheRootEntryControl()
    {
        await using var context = BrowserFixture.Context();
        var root = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, Definition("Conformance.RootMap")));
        await BrowserFixture.SelectPanelAsync(root, "review");
        await root.Find("[data-correction='']").ClickAsync(new MouseEventArgs());
        await Assert.That(Field(root).Instance.FocusFirstControl).IsTrue();
        string id = Field(root).Instance.Form.GetEditorIdentity(DefinitionPath.FromSegments([]));
        await Assert.That(Field(root).Find("#" + id + "-new-key").HasAttribute("disabled")).IsFalse();
    }

    [Test]
    public async Task LiteralRootKeysKeepOrdinalChoicesNullAndCaseDistinct()
    {
        await using var context = BrowserFixture.Context();
        var root = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, Definition("Conformance.RootMap")));
        var form = Field(root).Instance.Form;
        foreach (string key in new[] { "a/b", "a~b", "A", "0" })
            await AddKey(root, key);
        await Choose(root, ["a/b"], "2");
        await Choose(root, ["a~b"], "3");
        await Choose(root, ["A"], "0");
        await Choose(root, ["0"], "1");
        await Assert.That(form.Snapshot().Values["a/b"].GetInt32()).IsEqualTo(30);
        await Assert.That(form.Snapshot().Values["a~b"].ValueKind).IsEqualTo(System.Text.Json.JsonValueKind.Null);
        await Assert.That(form.Snapshot().Values["A"].GetInt32()).IsEqualTo(0);
        await Assert.That(form.Snapshot().Values["0"].GetInt32()).IsEqualTo(7);
        await Assert.That(form.Validate().IsReady).IsTrue();
        await Assert.That(context.JSInterop.Invocations.Any(call => call.Arguments.Any(argument => argument is byte[]))).IsFalse();
    }

    [Test]
    public async Task RootBranchChangeDisposesCallbacksAndConsentCannotBeRevived()
    {
        await using var context = BrowserFixture.Context();
        var root = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, Definition("Conformance.RootVariant")));
        var form = Field(root).Instance.Form;
        await Choose(root, [], "0");
        await Assert.That(form.GetFieldValidation(DefinitionPath.Parse("/directory")).RequiresMasking).IsFalse();
        await Assert.That(form.GetFieldValidation(DefinitionPath.Parse("/batch")).CanApplySuggestion).IsTrue();
        await Type(root, ["directory"], "spool");
        var retired = Field(root, "batch").FindComponent<MudSelect<string>>().Instance.ValueChanged;
        await Choose(root, [], "1");
        long revision = form.InputRevision;
        await root.InvokeAsync(() => retired.InvokeAsync("2"));
        await Assert.That(form.InputRevision).IsEqualTo(revision);
        await Assert.That(form.Snapshot().Values.ContainsKey("batch")).IsFalse();
        await Assert.That(Field(root, "token").FindAll("[data-secret-denied]").Count).IsEqualTo(1);
        await root.Find("#secret-consent").ChangeAsync(new ChangeEventArgs { Value = true });
        await Type(root, ["endpoint"], "queue.example.invalid");
        await Type(root, ["token"], "synthetic-token");
        await Assert.That(Field(root, "endpoint").Find("input").GetAttribute("type")).IsNotEqualTo("password");
        await Assert.That(Field(root, "token").Find("input").GetAttribute("type")).IsEqualTo("password");
        await root.Find("#secret-consent").ChangeAsync(new ChangeEventArgs { Value = false });
        await Assert.That(form.Snapshot().Values.ContainsKey("token")).IsFalse();
        await root.InvokeAsync(() => retired.InvokeAsync("1"));
        await Assert.That(form.Snapshot().Values.ContainsKey("batch")).IsFalse();
    }

    [Test]
    public async Task RootVariantOmissionResetSuggestionAndNullRemainSeparate()
    {
        await using var context = BrowserFixture.Context();
        using Stream sourceStream = Resource("Conformance.RootVariant");
        JsonNode source = JsonNode.Parse(sourceStream)!;
        JsonNode token = source["configuration"]!["oneOf"]![1]!["properties"]!["token"]!;
        token.AsObject().Remove("writeOnly");
        token["x-configurator"]!["sensitivity"] = "public";
        var definition = DefinitionSourceCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString()));
        var root = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, definition));
        var form = Field(root).Instance.Form;
        await Choose(root, [], "0");
        await Type(root, ["directory"], "spool");
        await Action(root, ["batch"], actions => actions.OnOmit);
        await Assert.That(form.Snapshot().Omitted.Contains(DefinitionPath.Parse("/batch"))).IsTrue();
        await Action(root, ["batch"], actions => actions.OnReset);
        await Assert.That(form.Validate().TryGetEffectiveValue(DefinitionPath.Parse("/batch"), out var initial)).IsTrue();
        await Assert.That(initial.GetInt32()).IsEqualTo(2);
        await Assert.That(form.GetFieldValidation(DefinitionPath.Parse("/batch")).CanApplySuggestion).IsTrue();
        await Assert.That(Field(root, "batch").FindComponent<ConfigurationFieldActions>().Instance.InputRevision).IsEqualTo(form.InputRevision);
        await Assert.That(Field(root, "batch").FindComponent<ConfigurationFieldActions>().Instance.Facts.CanApplySuggestion).IsTrue();
        await Action(root, ["batch"], actions => actions.OnSuggest);
        await Assert.That(form.Snapshot().Values["batch"].GetInt32()).IsEqualTo(3);
        await Action(root, ["note"], actions => actions.OnNull);
        await Assert.That(form.Snapshot().Values["note"].ValueKind).IsEqualTo(System.Text.Json.JsonValueKind.Null);
        await Action(root, ["note"], actions => actions.OnOmit);
        await Assert.That(form.Snapshot().Values.ContainsKey("note")).IsFalse();
    }

    [Test]
    public async Task PublicSiblingsRenderUnmaskedWhileDeclaredSensitiveAncestorMasksBrowserControls()
    {
        await using var context = BrowserFixture.Context();
        Definition definition = DefinitionSourceCodec.ParseJson("""
            {"$schema":"urn:islamu:configurator:authoring:v0.0.1",
             "identity":{"familyId":"198d708d-caba-4e48-a00d-b06242151620","revision":1},
             "project":{"id":"browser-masking","name":"BrowserMasking"},"applicationVersions":["1"],
             "configuration":{"type":"object","additionalProperties":false,"x-configurator":{"sensitivity":"public"},"properties":{
               "mixed":{"type":"object","additionalProperties":false,"x-configurator":{"sensitivity":"public","detail":"basic"},"properties":{
                 "host":{"type":"string","x-configurator":{"sensitivity":"public","detail":"basic","suggestedValue":"public.example.invalid"}},
                 "flag":{"type":"boolean","x-configurator":{"sensitivity":"public","detail":"basic"}},
                 "token":{"type":"string","writeOnly":true,"x-configurator":{"detail":"basic"}}}},
               "sensitiveGroup":{"type":"object","additionalProperties":false,"x-configurator":{"sensitivity":"sensitive","detail":"basic"},"properties":{
                 "host":{"type":"string","x-configurator":{"sensitivity":"public","detail":"basic"}},
                 "flag":{"type":"boolean","x-configurator":{"sensitivity":"public","detail":"basic"}}}}
             }},"outputs":{"config.json":{"format":"json"}}}
            """u8.ToArray());
        var root = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, definition));
        var form = root.FindComponents<ConfigurationField>()[0].Instance.Form;
        await root.InvokeAsync(() =>
        {
            form.SetAllSectionsExpanded(true);
            form.SupplyObject(["mixed"]);
            form.SupplyObject(["sensitiveGroup"]);
            form.SetValue(["mixed", "host"], JsonSerializer.SerializeToElement("public.example.invalid"));
            form.SetValue(["sensitiveGroup", "host"], JsonSerializer.SerializeToElement("masked-host"));
        });
        root.Render();

        IRenderedComponent<ConfigurationField> mixedHost = root.FindComponents<ConfigurationField>()
            .Single(field => field.Instance.Path.SequenceEqual(["mixed", "host"]));
        IRenderedComponent<ConfigurationField> mixedFlag = root.FindComponents<ConfigurationField>()
            .Single(field => field.Instance.Path.SequenceEqual(["mixed", "flag"]));
        IRenderedComponent<ConfigurationField> lockedHost = root.FindComponents<ConfigurationField>()
            .Single(field => field.Instance.Path.SequenceEqual(["sensitiveGroup", "host"]));
        IRenderedComponent<ConfigurationField> lockedFlag = root.FindComponents<ConfigurationField>()
            .Single(field => field.Instance.Path.SequenceEqual(["sensitiveGroup", "flag"]));

        await Assert.That(mixedHost.Find("input").GetAttribute("type")).IsNotEqualTo("password");
        await Assert.That(mixedFlag.FindAll("[role='radiogroup']").Count).IsEqualTo(1);
        await Assert.That(lockedHost.Find("input").GetAttribute("type")).IsEqualTo("password");
        await Assert.That(lockedFlag.FindAll("[role='radiogroup']").Count).IsEqualTo(0);
    }

    [Test]
    [Arguments("Conformance.RootMap")]
    [Arguments("Conformance.RootVariant")]
    public async Task RootDefinitionsMountAccessibleRecursiveControls(string resource)
    {
        await using var context = BrowserFixture.Context();
        var root = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, Definition(resource)));
        await Assert.That(root.FindAll("[data-configuration-root]").Count).IsEqualTo(1);
        await Assert.That(root.Find("[data-configuration-root] fieldset legend").TextContent.Length > 0).IsTrue();
        await Assert.That(root.FindAll("[data-configuration-root] input, [data-configuration-root] select, [data-configuration-root] button").Count > 0).IsTrue();
    }

    [Test]
    [Arguments("Conformance.RootMap")]
    [Arguments("Conformance.RootVariant")]
    public async Task RootDefinitionsAreAdmittedAtTheBrowserLaunchBoundary(string resource)
    {
        var definition = Definition(resource);
        var selection = BrowserLaunchSelection.Resolve(new DefinitionRegistry([definition]), definition.FamilyId.ToString("D"), "");
        await Assert.That(selection.Definition).IsEqualTo(definition);
    }

    internal static Definition Definition(string resource)
    {
        using Stream input = Resource(resource);
        using var bytes = new MemoryStream();
        input.CopyTo(bytes);
        return DefinitionSourceCodec.ParseJson(bytes.ToArray());
    }

    internal static Stream Resource(string name) => typeof(BrowserRootEditingTests).Assembly.GetManifestResourceStream(name)
        ?? throw new InvalidDataException("Missing conformance resource.");

    internal static IRenderedComponent<ConfigurationField> Field(IRenderedComponent<ConfigurationEditor> root, params string[] path) =>
        root.FindComponents<ConfigurationField>().Single(field => field.Instance.LiteralPath && field.Instance.Path.SequenceEqual(path));

    internal static Task Choose(IRenderedComponent<ConfigurationEditor> root, string[] path, string ordinal) =>
        root.InvokeAsync(() => Field(root, path).FindComponent<MudSelect<string>>().Instance.ValueChanged.InvokeAsync(ordinal));

    internal static Task Type(IRenderedComponent<ConfigurationEditor> root, string[] path, string text) =>
        Field(root, path).Find("input").InputAsync(new ChangeEventArgs { Value = text });

    internal static async Task AddKey(IRenderedComponent<ConfigurationEditor> root, string key)
    {
        var owner = Field(root);
        string id = owner.Instance.Form.GetEditorIdentity(DefinitionPath.FromSegments([]));
        await owner.Find("#" + id + "-new-key").InputAsync(new ChangeEventArgs { Value = key });
        await Field(root).Find("[data-action='add-map-entry']").ClickAsync(new MouseEventArgs());
    }

    internal static Task Action(IRenderedComponent<ConfigurationEditor> root, string[] path,
        Func<ConfigurationFieldActions, EventCallback> action) =>
        root.InvokeAsync(() => action(Field(root, path).FindComponent<ConfigurationFieldActions>().Instance).InvokeAsync());

    private sealed class HeldStream(byte[] bytes) : MemoryStream(bytes)
    {
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            Entered.TrySetResult();
            await Release.Task.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None);
            return await base.ReadAsync(buffer, CancellationToken.None);
        }
    }
}
