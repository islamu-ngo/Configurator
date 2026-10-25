namespace Configurator.Browser.Tests;

using System.Text;
using System.Text.Json.Nodes;
using Bunit;
using Configurator.Core.Definitions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

public sealed class BrowserAnswerBudgetTests
{
    [Test]
    public async Task AggregateAnswerBudgetKeepsControlsEditableAndReportsValueSafeRejection()
    {
        JsonNode definition = JsonNode.Parse(BrowserFixture.Definition.CanonicalBytes.Span)!;
        var fields = new JsonArray();
        for (int index = 0; index < 17; index++)
            fields.Add(new JsonObject
            {
                ["id"] = "text" + index,
                ["type"] = "string",
                ["title"] = "Text " + index,
                ["sensitivity"] = "public",
                ["disclosure"] = "guided"
            });
        definition["fields"] = fields;
        definition["artifacts"]![0]!["projection"] = JsonNode.Parse("""{"op":"copy","field":"text0"}""");
        SetupDefinition admitted = SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(definition.ToJsonString()));
        await using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, admitted));
        string oversizedPart = "synthetic-budget-canary" + new string('x', 65536 - "synthetic-budget-canary".Length);
        for (int index = 0; index < 17; index++)
            await component.Find($"[data-field='text{index}'] input").InputAsync(new ChangeEventArgs { Value = oversizedPart });
        await component.Find("#prepare-draft").ClickAsync(new MouseEventArgs());
        await Assert.That(component.Find("[role=status]").GetAttribute("data-code")).IsEqualTo("answer-budget-exceeded");
        await Assert.That(component.Find("[role=status]").TextContent).DoesNotContain("synthetic-budget-canary");
        await Assert.That(component.FindAll(".configuration-editor__artifact")).IsEmpty();
        await component.Find("[data-field='text16'] input").InputAsync(new ChangeEventArgs { Value = "" });
        await component.Find("[data-field='text15'] input").InputAsync(new ChangeEventArgs { Value = "" });
        await component.Find("#prepare-draft").ClickAsync(new MouseEventArgs());
        await Assert.That(component.Find("[role=status]").GetAttribute("data-code")).IsEqualTo("preparation-ready");
    }

    [Test]
    public async Task AggregateBundleLimitPublishesNoEarlierArtifact()
    {
        JsonNode definition = JsonNode.Parse(BrowserFixture.Definition.CanonicalBytes.Span)!;
        definition["fields"] = JsonNode.Parse("""[{"id":"payload","type":"string","title":"Payload","sensitivity":"public","disclosure":"guided"}]""");
        var artifacts = new JsonArray();
        for (int index = 0; index < 64; index++)
            artifacts.Add(new JsonObject
            {
                ["id"] = "output" + index,
                ["format"] = "json",
                ["fileName"] = "output" + index + ".json",
                ["applySemantics"] = "create",
                ["restart"] = "none",
                ["projection"] = JsonNode.Parse("""{"op":"copy","field":"payload"}""")
            });
        definition["artifacts"] = artifacts;
        SetupDefinition admitted = SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(definition.ToJsonString()));
        await using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, admitted));
        await component.Find("[data-field='payload'] input").InputAsync(new ChangeEventArgs { Value = new string('x', 65536) });
        await component.Find("#prepare-draft").ClickAsync(new MouseEventArgs());
        await Assert.That(component.FindAll(".configuration-editor__artifact")).IsEmpty();
        await Assert.That(component.Find("[role=status]").GetAttribute("data-code")).IsEqualTo("preparation-rejected");
        await Assert.That(component.Find("[role=alert]").TextContent).Contains("bundle-limit");
    }
}
