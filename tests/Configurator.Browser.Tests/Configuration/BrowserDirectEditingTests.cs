namespace Configurator.Browser.Tests;

using System.Text;
using System.Text.Json.Nodes;
using Bunit;
using Configurator.Core.Definitions;
using Microsoft.AspNetCore.Components;
using MudBlazor;

public sealed class BrowserDirectEditingTests
{
    [Test]
    public async Task OrdinaryAdvancedInputsAndShortChoicesMountWithoutSupplyingAnswers()
    {
        await using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, Definition()));
        var form = component.FindComponents<ConfigurationField>()[0].Instance.Form;

        await Assert.That(component.FindAll("[data-field='nickname'] input[type=text]")).Count().IsEqualTo(1);
        await Assert.That(component.FindAll("[data-field='feature'] input[type=radio]")).Count().IsEqualTo(3);
        var radios = component.FindAll("[data-field='feature'] input[type=radio]");
        await Assert.That(component.Find("#" + radios[0].GetAttribute("aria-labelledby")).TextContent).IsEqualTo("Not supplied");
        await Assert.That(component.Find("#" + radios[1].GetAttribute("aria-labelledby")).TextContent).IsEqualTo("Yes");
        await Assert.That(component.Find("#" + radios[2].GetAttribute("aria-labelledby")).TextContent).IsEqualTo("No");
        var flavor = component.FindComponents<ConfigurationField>().Single(item => item.Instance.Path.SequenceEqual(["flavor"]));
        await Assert.That(flavor.FindComponents<MudSelect<string>>().Count).IsEqualTo(1);
        await Assert.That(component.FindAll("[data-action='expand-field']")).IsEmpty();
        await Assert.That(component.FindAll("[data-field='credential'] input")).IsEmpty();
        await Assert.That(form.Snapshot().Values.Count).IsEqualTo(0);
        await Assert.That(form.InputRevision).IsEqualTo(0);
    }

    [Test]
    public async Task DirectChoicesKeepUntouchedFalseAndExplicitEmptyDistinct()
    {
        await using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, Definition()));
        var form = component.FindComponents<ConfigurationField>()[0].Instance.Form;
        await Assert.That(form.Snapshot().Values.ContainsKey("feature")).IsFalse();
        await Assert.That(form.Snapshot().Values.ContainsKey("flavor")).IsFalse();

        var feature = component.FindComponents<ConfigurationField>().Single(item => item.Instance.Path.SequenceEqual(["feature"]));
        await component.InvokeAsync(() => feature.FindComponent<MudRadioGroup<string>>().Instance.ValueChanged.InvokeAsync("false"));
        await Assert.That(form.Snapshot().Values["feature"].GetBoolean()).IsFalse();
        var flavor = component.FindComponents<ConfigurationField>().Single(item => item.Instance.Path.SequenceEqual(["flavor"]));
        await component.InvokeAsync(() => flavor.FindComponent<MudSelect<string>>().Instance.ValueChanged.InvokeAsync("0"));
        await Assert.That(form.Snapshot().Values["flavor"].GetString()).IsEqualTo(string.Empty);
        await Assert.That(form.Snapshot().Values.ContainsKey("nickname")).IsFalse();
        await Assert.That(form.InputRevision).IsEqualTo(2);
    }

    private static Definition Definition()
    {
        JsonNode source = JsonNode.Parse(BrowserFixture.Definition.CanonicalBytes.Span)!;
        BrowserFixture.WithField(source, "nickname", """
            {"type":"string","title":"Nickname","x-configurator":{"sensitivity":"public","detail":"advanced"}}
            """);
        BrowserFixture.WithField(source, "feature", """
            {"type":"boolean","title":"Optional feature","x-configurator":{"sensitivity":"public","detail":"advanced"}}
            """, "schema.boolean");
        BrowserFixture.WithField(source, "flavor", """
            {"type":"string","title":"Flavor","enum":["","alpha","beta"],"x-configurator":{"sensitivity":"public","detail":"advanced"}}
            """, "schema.enum");
        return DefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString()));
    }
}
