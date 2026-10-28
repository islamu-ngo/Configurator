namespace Configurator.Browser.Tests;

using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;
using Bunit;
using Configurator.Core.Definitions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

public sealed class BrowserAnswerBudgetTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task PreparingAdoptsEachAdmittedOutputWithoutRecomposingIt(bool draft)
    {
        JsonNode definition = JsonNode.Parse(BrowserFixture.Definition.CanonicalBytes.Span)!;
        definition["configuration"]!["required"] = new JsonArray();
        definition["configuration"]!["properties"] = JsonNode.Parse("""
            {"name":{"type":"string","title":"Name","x-configurator":{"sensitivity":"public","detail":"basic","initialValue":"sample"}}}
            """);
        definition["artifacts"] = JsonNode.Parse("""
            [{"id":"first","format":"json","fileName":"first.json","targetUpdateMode":"create","restartRequirement":"none",
              "projection":{"op":"object","members":{"name":{"op":"copy","reference":{"scope":"root","path":"/name"}}}}},
             {"id":"second","format":"json","fileName":"second.json","targetUpdateMode":"create","restartRequirement":"none",
              "projection":{"op":"literal","value":{"order":[3,1,4,2]}}}]
            """);
        BrowserFixture.SetCapabilities(definition, "projection.copy", "projection.literal", "projection.object",
            "schema.object", "schema.string", "values.initial");
        SetupDefinition admitted = SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(definition.ToJsonString()));
        await using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, admitted));
        using var activity = new Activity(nameof(PreparingAdoptsEachAdmittedOutputWithoutRecomposingIt));
        activity.Start();
        using var measurement = new ComposedOutputs(activity);
        await component.Find(draft ? "#prepare-draft" : "#prepare-complete").ClickAsync(new MouseEventArgs());
        await Assert.That(component.FindAll(".configuration-editor__artifact").Count).IsEqualTo(2);
        await Assert.That(measurement.Artifacts.Count).IsEqualTo(2);
        await Assert.That(measurement.Artifacts.SequenceEqual(["first", "second"], StringComparer.Ordinal)).IsTrue();
    }

    [Test]
    public async Task AggregateAnswerBudgetKeepsControlsEditableAndReportsValueSafeRejection()
    {
        JsonNode definition = JsonNode.Parse(BrowserFixture.Definition.CanonicalBytes.Span)!;
        var fields = new JsonObject();
        for (int index = 0; index < 17; index++)
            fields.Add("text" + index, new JsonObject
            {
                ["type"] = "string",
                ["title"] = "Text " + index,
                ["x-configurator"] = new JsonObject { ["sensitivity"] = "public", ["detail"] = "standard" }
            });
        definition["configuration"]!["required"] = new JsonArray();
        definition["configuration"]!["properties"] = fields;
        definition["artifacts"]![0]!["projection"] = JsonNode.Parse("""{"op":"copy","reference":{"scope":"root","path":"/text0"}}""");
        BrowserFixture.SetCapabilities(definition, "projection.copy", "schema.object", "schema.string");
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
        definition["configuration"]!["required"] = new JsonArray();
        definition["configuration"]!["properties"] = JsonNode.Parse("""
            {"payload":{"type":"string","title":"Payload","x-configurator":{"sensitivity":"public","detail":"standard"}}}
            """);
        var artifacts = new JsonArray();
        for (int index = 0; index < 64; index++)
            artifacts.Add(new JsonObject
            {
                ["id"] = "output" + index,
                ["format"] = "json",
                ["fileName"] = "output" + index + ".json",
                ["targetUpdateMode"] = "create",
                ["restartRequirement"] = "none",
                ["projection"] = JsonNode.Parse("""{"op":"copy","reference":{"scope":"root","path":"/payload"}}""")
            });
        definition["artifacts"] = artifacts;
        BrowserFixture.SetCapabilities(definition, "projection.copy", "schema.object", "schema.string");
        SetupDefinition admitted = SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(definition.ToJsonString()));
        await using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, admitted));
        await component.Find("[data-field='payload'] input").InputAsync(new ChangeEventArgs { Value = new string('x', 65536) });
        await component.Find("#prepare-draft").ClickAsync(new MouseEventArgs());
        await Assert.That(component.FindAll(".configuration-editor__artifact")).IsEmpty();
        await Assert.That(component.Find("[role=status]").GetAttribute("data-code")).IsEqualTo("preparation-rejected");
        await Assert.That(component.Find("[role=alert]").TextContent).Contains("bundle-limit");
    }

    private sealed class ComposedOutputs : IObserver<DiagnosticListener>,
        IObserver<KeyValuePair<string, object?>>, IDisposable
    {
        private readonly Activity _activity;
        private readonly List<IDisposable> _subscriptions = [];
        private readonly IDisposable _listeners;
        public ComposedOutputs(Activity activity)
        {
            _activity = activity;
            _listeners = DiagnosticListener.AllListeners.Subscribe(this);
        }
        public List<string> Artifacts { get; } = [];
        public void OnNext(DiagnosticListener value)
        {
            if (value.Name == "Configurator.Core.Preparation") _subscriptions.Add(value.Subscribe(this));
        }
        public void OnNext(KeyValuePair<string, object?> value)
        {
            if (ReferenceEquals(Activity.Current, _activity) && value.Key == "ArtifactComposed")
                Artifacts.Add((string)value.Value!);
        }
        public void OnCompleted() { }
        public void OnError(Exception error) { }
        public void Dispose()
        {
            _listeners.Dispose();
            foreach (IDisposable subscription in _subscriptions) subscription.Dispose();
        }
    }
}
