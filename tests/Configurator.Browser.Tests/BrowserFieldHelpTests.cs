namespace Configurator.Browser.Tests;

using System.Text;
using System.Text.Json.Nodes;
using Bunit;
using Configurator.Browser.Layout;
using Configurator.Core.Definitions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

public sealed class BrowserFieldHelpTests
{
    [Test]
    public async Task OmissionMenuStaysClosedBetweenEditsAndReopensWithCurrentAnswer()
    {
        await using var context = BrowserFixture.Context();
        JsonNode source = JsonNode.Parse(BrowserFixture.Definition.CanonicalBytes.Span)!;
        source["fields"]!.AsArray().Single(field => field!["id"]!.GetValue<string>() == "hostname")!["required"] = false;
        var definition = SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString()));
        var component = RenderWorkspace(context, definition);
        var form = component.FindComponents<ConfigurationField>()[0].Instance.Form;
        for (int edit = 0; edit < 2; edit++)
        {
            await component.Find("[data-field='hostname'] input").InputAsync(new ChangeEventArgs { Value = "synthetic-" + edit + ".test" });
            await Assert.That(component.FindAll("[role='menuitem']")).IsEmpty();
            await Assert.That(component.Find("[data-field='hostname'] button[aria-haspopup='menu']").GetAttribute("aria-expanded")).IsEqualTo("false");
            long revision = form.InputRevision;
            var omission = await BrowserFixture.OpenOmissionAsync(context, component, "hostname");
            await Assert.That(form.InputRevision).IsEqualTo(revision);
            await Assert.That(component.Find("[data-field='hostname'] button[aria-haspopup='menu']").GetAttribute("aria-expanded")).IsEqualTo("true");
            await omission.ClickAsync(new MouseEventArgs());
            await Assert.That(form.Snapshot().Values.ContainsKey("hostname")).IsFalse();
        }
    }

    [Test]
    public async Task UnchangedFieldActionsReadCurrentBindingDelegate()
    {
        await using var context = BrowserFixture.Context();
        var component = RenderWorkspace(context, BrowserFixture.Definition);
        var actions = component.FindComponents<ConfigurationFieldActions>()
            .Single(item => item.Instance.Path.Count == 1 && item.Instance.Path[0] == "hostname");
        Func<(IReadOnlyList<string>, IReadOnlyList<string>)> previous = () => (new[] { "old-artifact" }, new[] { "OLD_KEY" });
        Func<(IReadOnlyList<string>, IReadOnlyList<string>)> current = () => (new[] { "current-artifact" }, new[] { "CURRENT_KEY" });
        actions.Render(parameters => parameters.Add(item => item.GetBindings, previous));
        actions.Render(parameters => parameters.Add(item => item.GetBindings, current));
        await actions.Find("[data-action='field-help']").ClickAsync(new MouseEventArgs());
        var inspector = component.FindComponent<ConfigurationInspector>().Instance;
        await Assert.That(inspector.OutputArtifactIds.Count).IsEqualTo(1);
        await Assert.That(inspector.OutputArtifactIds[0]).IsEqualTo("current-artifact");
        await Assert.That(inspector.BindingKeys.Count).IsEqualTo(1);
        await Assert.That(inspector.BindingKeys[0]).IsEqualTo("CURRENT_KEY");
    }

    [Test]
    public async Task InspectorNamesItsDialogAndShowsOnlyInertPublicMetadata()
    {
        await using var context = BrowserFixture.Context();
        JsonNode source = JsonNode.Parse(BrowserFixture.Definition.CanonicalBytes.Span)!;
        source["fields"]!.AsArray().Single(field => field!["id"]!.GetValue<string>() == "credential")!["guidance"] =
            JsonNode.Parse("""{ "instructions":"Acquire this value locally and review target instructions.", "links":[{"title":"Public guide","url":"https://docs.example.test/configure"}]}""");
        source["artifacts"]![0]!["projection"] = JsonNode.Parse("""{"op":"object","members":{"CREDENTIAL":{"op":"copy","field":"credential"}}}""");
        var definition = SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString()));
        var component = RenderWorkspace(context, definition);
        await component.Find("#secret-consent").ChangeAsync(new ChangeEventArgs { Value = true });
        await component.Find("[data-field='credential'] input").InputAsync(new ChangeEventArgs { Value = "synthetic-help-private-answer" });
        await component.Find("[data-field='credential'] [data-action='field-help']").ClickAsync(new MouseEventArgs());
        var dialog = component.Find("[role='dialog']");
        string titleId = dialog.GetAttribute("aria-labelledby")!;
        await Assert.That(component.Find("#" + titleId).TextContent.Length).IsGreaterThan(0);
        await Assert.That(dialog.QuerySelectorAll("script")).IsEmpty();
        await Assert.That(dialog.QuerySelector("[data-help-instructions]")!.ChildElementCount).IsEqualTo(0);
        await Assert.That(dialog.QuerySelector("[data-definition-path]")!.TextContent).IsEqualTo("credential");
        await Assert.That(dialog.TextContent).DoesNotContain("synthetic-help-private-answer");
        await Assert.That(dialog.QuerySelector("[data-output-key]")!.TextContent).IsEqualTo("CREDENTIAL");
        var link = dialog.QuerySelector("a")!;
        await Assert.That(link.GetAttribute("rel")).Contains("noopener");
        await Assert.That(link.GetAttribute("rel")).Contains("noreferrer");
        await Assert.That(link.GetAttribute("referrerpolicy")).IsEqualTo("no-referrer");
        await component.Find("[aria-label='Close field help']").ClickAsync(new MouseEventArgs());
        await Assert.That(component.FindAll("[role='dialog']")).IsEmpty();
    }

    [Test]
    [Arguments(false, true)]
    [Arguments(true, true)]
    [Arguments(true, false)]
    public async Task ConsentOrWorkspaceReplacementClosesOnlyItsRetiredFieldInspector(bool replace, bool consent)
    {
        await using var context = BrowserFixture.Context();
        var component = RenderWorkspace(context, BrowserFixture.Definition);
        var editor = component.FindComponent<ConfigurationEditor>();
        var form = editor.FindComponents<ConfigurationField>()[0].Instance.Form;
        if (consent) await editor.InvokeAsync(() => form.SetSecretEntryEnabled(true));
        await component.Find("[data-field='hostname'] [data-action='field-help']").ClickAsync(new MouseEventArgs());
        await Assert.That(component.FindAll("[role='dialog']").Count).IsEqualTo(1);
        if (replace) editor.Render(parameters => parameters.Add(item => item.Definition, BrowserFixture.Revision(2)));
        else await editor.InvokeAsync(() => form.SetSecretEntryEnabled(false));
        await Assert.That(component.FindAll("[role='dialog']")).IsEmpty();
        await component.Find("[data-field='hostname'] [data-action='field-help']").ClickAsync(new MouseEventArgs());
        await Assert.That(component.FindAll("[role='dialog']").Count).IsEqualTo(1);
        await component.Find("[aria-label='Close field help']").ClickAsync(new MouseEventArgs());
        await Assert.That(component.FindAll("[role='dialog']")).IsEmpty();
    }

    private static IRenderedComponent<MainLayout> RenderWorkspace(BunitContext context, SetupDefinition definition) =>
        context.Render<MainLayout>(parameters => parameters.Add(item => item.Body, builder =>
        {
            builder.OpenComponent<ConfigurationEditor>(0);
            builder.AddAttribute(1, nameof(ConfigurationEditor.Definition), definition);
            builder.CloseComponent();
        }));
}
