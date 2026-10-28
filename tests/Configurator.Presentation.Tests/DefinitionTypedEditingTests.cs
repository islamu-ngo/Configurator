namespace Configurator.Presentation.Tests;

using System.Text.Json;
using Configurator.Core.Definitions;
using Configurator.Core.Artifacts;

public sealed class DefinitionTypedEditingTests
{
    [Test]
    public async Task SecretMapEntriesCannotBypassWorkspaceConsent()
    {
        SetupDefinition definition = SetupDefinitionSourceCodec.ParseJson("""
            {"$schema":"urn:islamu:configurator:authoring:v2",
             "identity":{"familyId":"198d708d-caba-4e48-a00d-b06242151617","revision":1},
             "project":{"id":"private-map","name":"Private map"},"applicationVersions":["1"],
             "configuration":{"type":"object","additionalProperties":false,"properties":{
               "map":{"type":"object","additionalProperties":{"type":"string","writeOnly":true}}}},
             "outputs":{"config.json":{"format":"json"}}}
            """u8.ToArray());
        using var form = new DefinitionFormModel(definition, DefinitionSessionPrivacy.BrowserNoSecrets);
        await Assert.That(form.SetValue(["map"], JsonSerializer.SerializeToElement(
            new Dictionary<string, string> { ["synthetic-private-key"] = "synthetic-private-value" }))).IsFalse();
        await Assert.That(form.Snapshot().Values.Count).IsEqualTo(0);
        form.SetSecretEntryEnabled(true);
        await Assert.That(form.SetValue(["map"], JsonSerializer.SerializeToElement(
            new Dictionary<string, string> { ["synthetic-private-key"] = "synthetic-private-value" }))).IsTrue();
        form.SetSecretEntryEnabled(false);
        await Assert.That(form.Snapshot().Values["map"].EnumerateObject().Count()).IsEqualTo(0);
    }

    [Test]
    public async Task EqualChoiceLabelsNeverDetermineTypedIdentityAndSuggestionsRequireAnAction()
    {
        using var form = new DefinitionFormModel(Definition());
        await Assert.That(form.Snapshot().Values.ContainsKey("choice")).IsFalse();
        DefinitionEditorPresentation display = form.GetEditorPresentation(["choice"])!;
        await Assert.That(display.Choices[0].Label).IsEqualTo(display.Choices[1].Label);
        await Assert.That(display.Choices[0].Value.GetInt32()).IsEqualTo(1);
        await Assert.That(display.Choices[1].Value.GetInt32()).IsEqualTo(2);
        await Assert.That(form.SelectChoice(["choice"], 0)).IsTrue();
        await Assert.That(form.Snapshot().Values["choice"].GetInt32()).IsEqualTo(1);
        await Assert.That(form.ApplySuggestion(["choice"])).IsTrue();
        await Assert.That(form.Snapshot().Values["choice"].GetInt32()).IsEqualTo(2);
        await Assert.That(form.SelectChoice(["choice"], 2)).IsFalse();
        await Assert.That(form.SetNull(["choice"])).IsFalse();
    }

    [Test]
    public async Task SafeMapCorrectionResolvesOnlyAtItsOriginalRevision()
    {
        using var form = new DefinitionFormModel(Definition());
        form.SetValue(["map"], JsonSerializer.SerializeToElement(
            new Dictionary<string, string> { ["synthetic-private-key"] = "invalid-integer" }));
        DefinitionDiagnostic diagnostic = form.Validate().Diagnostics.Single(item => item.Code == "value-invalid");
        await Assert.That(form.FocusCorrection(diagnostic.FieldId, diagnostic.Location!)).IsTrue();
        await Assert.That(diagnostic.ToString()).DoesNotContain("synthetic-private-key");
        form.EditValue(["map", "synthetic-private-key"], "3");
        await Assert.That(form.FocusCorrection(diagnostic.FieldId, diagnostic.Location!)).IsFalse();
    }

    [Test]
    public async Task LocaleSelectionChangesOnlyPublicTextAndDirection()
    {
        using var form = new DefinitionFormModel(Definition());
        form.EditValue(["nullable"], "9");
        long revision = form.InputRevision;
        int inputEvents = 0;
        form.InputChanged += (_, _) => inputEvents++;
        string snapshot = form.Snapshot().Values["nullable"].GetRawText();
        DefinitionFormProjection original = form.Projection;
        using DefinitionArtifact? before = DefinitionArtifactComposer.Compose(form.Definition, "config", form.Snapshot()).Artifact;
        await Assert.That(form.SelectLocale("ar")).IsTrue();
        await Assert.That(form.Direction).IsEqualTo("rtl");
        await Assert.That(form.LocalizedText(DefinitionPath.Parse("/configuration/properties/nullable/title"), "Count"))
            .IsEqualTo("Translated count");
        await Assert.That(form.LocalizedText(DefinitionPath.Parse("/project/name"), "Typed editing")).IsEqualTo("Typed editing");
        await Assert.That(form.GetEditorPresentation(["nullable"])!.Title).IsEqualTo("Translated count");
        await Assert.That(form.Projection.Entries.Single(entry => entry.FieldId == "nullable").Title).IsEqualTo("Translated count");
        await Assert.That(original.Entries.Single(entry => entry.FieldId == "nullable").Title).IsEqualTo("Count");
        using DefinitionArtifact? after = DefinitionArtifactComposer.Compose(form.Definition, "config", form.Snapshot()).Artifact;
        await Assert.That(before).IsNotNull();
        await Assert.That(after).IsNotNull();
        await Assert.That(after!.Digest).IsEqualTo(before!.Digest);
        await Assert.That(form.Snapshot().Values["nullable"].GetRawText()).IsEqualTo(snapshot);
        await Assert.That(form.InputRevision).IsEqualTo(revision);
        await Assert.That(inputEvents).IsEqualTo(0);
        await Assert.That(form.SelectLocale("undeclared")).IsFalse();
    }

    [Test]
    public async Task UnsupportedConsumerCapabilitiesRefuseBeforeAFormCanCollectAnswers()
    {
        await Assert.That(() => new DefinitionFormModel(Definition(),
            supportedCapabilities: new HashSet<string>(StringComparer.Ordinal)))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task TypedMapEntryEditsPreserveLiteralKeysAndInvalidDrafts()
    {
        using var form = new DefinitionFormModel(Definition());
        await Assert.That(form.SetValue(["map"], JsonSerializer.SerializeToElement(
            new Dictionary<string, int> { ["literal/key"] = 4 }))).IsTrue();
        await Assert.That(form.EditValue(["map", "literal/key"], "-")).IsFalse();
        await Assert.That(form.HasInputErrors).IsTrue();
        await Assert.That(JsonSerializer.Serialize(form.InputDiagnostics)).DoesNotContain("literal/key");
        await Assert.That(form.EditValue(["map", "literal/key"], "8")).IsTrue();
        await Assert.That(form.Snapshot().Values["map"].GetProperty("literal/key").GetInt32()).IsEqualTo(8);
        await Assert.That(form.HasInputErrors).IsFalse();
    }

    [Test]
    public async Task SelectedVariantExposesItsDeclaredChildrenWithoutGuessingAnotherBranch()
    {
        using var form = new DefinitionFormModel(Definition());
        await Assert.That(form.SetValue(["provider"],
            JsonSerializer.SerializeToElement(new { kind = "first", count = 2 }))).IsTrue();
        await Assert.That(form.EditValue(["provider", "count"], "3")).IsTrue();
        await Assert.That(form.EditValue(["provider", "other"], "hidden")).IsFalse();
        await Assert.That(form.Snapshot().Values["provider"].GetProperty("count").GetInt32()).IsEqualTo(3);
        long beforeReselection = form.InputRevision;
        await Assert.That(form.SelectVariant(["provider"], 0)).IsTrue();
        await Assert.That(form.InputRevision).IsEqualTo(beforeReselection + 1);
        await Assert.That(form.Snapshot().Values["provider"].GetProperty("count").GetInt32()).IsEqualTo(3);
        long revision = form.InputRevision;
        await Assert.That(form.SelectVariant(["provider"], 1)).IsTrue();
        await Assert.That(form.Snapshot().Values["provider"].GetRawText()).IsEqualTo("""{"kind":"second"}""");
        await Assert.That(form.InputRevision).IsEqualTo(revision + 1);
        await Assert.That(form.EditValue(["provider", "count"], "4")).IsFalse();
        await Assert.That(form.EditValue(["provider", "other"], "supplied")).IsTrue();
    }

    [Test]
    public async Task NullOmissionAndResetRemainDistinctAndDoNotSeedAbsentContainers()
    {
        using var form = new DefinitionFormModel(Definition());
        await Assert.That(form.SetValue(["nullable"], JsonSerializer.SerializeToElement<object?>(null))).IsTrue();
        await Assert.That(form.Snapshot().Values["nullable"].ValueKind).IsEqualTo(JsonValueKind.Null);
        await Assert.That(form.EditValue(["nullable"], null)).IsTrue();
        await Assert.That(form.Validate().TryGetEffectiveValue(DefinitionPath.Parse("/nullable"), out _)).IsFalse();
        await Assert.That(form.ResetValue(["nullable"])).IsTrue();
        await Assert.That(form.Validate().TryGetEffectiveValue(DefinitionPath.Parse("/nullable"), out JsonElement reset)).IsTrue();
        await Assert.That(reset.GetInt32()).IsEqualTo(7);
        await Assert.That(form.Validate().TryGetEffectiveValue(DefinitionPath.Parse("/absent"), out _)).IsFalse();
    }

    private static SetupDefinition Definition() => SetupDefinitionSourceCodec.ParseJson("""
        {"$schema":"urn:islamu:configurator:authoring:v2",
         "identity":{"familyId":"198d708d-caba-4e48-a00d-b06242151616","revision":1},
         "project":{"id":"typed-editing","name":"Typed editing"},"applicationVersions":["1"],
         "configuration":{"type":"object","additionalProperties":false,"x-configurator":{"sensitivity":"public"},"properties":{
           "map":{"type":"object","additionalProperties":{"type":"integer"}},
           "choice":{"type":"integer","enum":[1,2],"x-configurator":{"sensitivity":"public",
             "suggestedValue":2,"choices":[{"value":1,"label":"Equal"},{"value":2,"label":"Equal"}]}},
           "provider":{"type":"object","x-configurator":{"sensitivity":"public"},"oneOf":[
             {"type":"object","additionalProperties":false,"x-configurator":{"sensitivity":"public"},"required":["kind"],"properties":{"kind":{"type":"string","const":"first","x-configurator":{"sensitivity":"public"}},"count":{"type":"integer"}}},
             {"type":"object","additionalProperties":false,"x-configurator":{"sensitivity":"public"},"required":["kind"],"properties":{"kind":{"type":"string","const":"second","x-configurator":{"sensitivity":"public"}},"other":{"type":"string"}}}]},
           "nullable":{"type":["integer","null"],"title":"Count","x-configurator":{"sensitivity":"public","initialValue":7}},
           "absent":{"type":"object","additionalProperties":false,"required":["child"],"x-configurator":{"sensitivity":"public"},"properties":{"child":{"type":"string","x-configurator":{"sensitivity":"public","initialValue":"synthetic"}}}}
         }},
         "localization":{"defaultLocale":"en","translations":{"ar":{"direction":"rtl",
           "overrides":{"/configuration/properties/nullable/title":"Translated count"}}}},
         "outputs":{"config.json":{"id":"config","format":"json"}}}
        """u8.ToArray());
}
