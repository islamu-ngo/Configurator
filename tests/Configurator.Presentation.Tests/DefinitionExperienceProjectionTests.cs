namespace Configurator.Presentation.Tests;

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Configurator.Core.Definitions;

public sealed class DefinitionExperienceProjectionTests
{
    [Test]
    public async Task InactiveControllerChainsResolveToActiveQuestionsWithoutChangingAnswers()
    {
        JsonObject source = JsonNode.Parse(Definition().CanonicalBytes.Span)!.AsObject();
        source["fields"] = JsonNode.Parse("""
        [{"id":"enabled","type":"boolean","default":false,"title":"Enable relay","sensitivity":"public","disclosure":"essential"},
         {"id":"mode","type":"boolean","default":true,"title":"Relay mode","sensitivity":"public","disclosure":"guided",
          "activeWhen":{"op":"equals","field":"enabled","value":true}},
         {"id":"port","type":"integer","title":"Port","sensitivity":"public","disclosure":"advanced",
          "activeWhen":{"op":"equals","field":"mode","value":true}}]
        """);
        source["artifacts"]![0]!["projection"] = JsonNode.Parse("""{"op":"literal","value":{}}""");
        using var form = new DefinitionFormModel(SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString())));
        long revision = form.InputRevision;
        var port = form.Projection.Entries.Single(entry => entry.FieldId == "port");
        await Assert.That(port.IsActive).IsFalse();
        await Assert.That(port.ActiveControllerFields.Count).IsEqualTo(1);
        await Assert.That(port.ActiveControllerFields[0]).IsEqualTo("enabled");
        await Assert.That(form.Snapshot().Values["enabled"].GetBoolean()).IsFalse();
        await Assert.That(form.InputRevision).IsEqualTo(revision);
    }

    [Test]
    public async Task InvalidSecretDraftIsNotCountedAsDeferredWork()
    {
        JsonObject source = JsonNode.Parse(Definition().CanonicalBytes.Span)!.AsObject();
        source["fields"] = JsonNode.Parse("""
        [{"id":"private_number","type":"integer","required":true,"title":"Private number",
          "sensitivity":"secret","disclosure":"guided"}]
        """);
        source["artifacts"]![0]!["projection"] = JsonNode.Parse("""{"op":"literal","value":{}}""");
        using var form = new DefinitionFormModel(
            SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString())),
            DefinitionSessionPrivacy.BrowserSecretsEnabled);
        form.EditValue(["private_number"], "synthetic-malformed-number");

        await Assert.That(form.Projection.Progress.Invalid).IsEqualTo(1);
        await Assert.That(form.Projection.Progress.Missing).IsEqualTo(0);
        await Assert.That(form.Projection.Progress.Deferred).IsEqualTo(0);
        await Assert.That(form.Projection.Progress.Ready).IsEqualTo(0);
    }

    [Test]
    public async Task LocalOptionalDisclosurePreservesRevisionAndCannotHideAnInvalidDraft()
    {
        JsonObject source = JsonNode.Parse(Definition().CanonicalBytes.Span)!.AsObject();
        source["fields"] = JsonNode.Parse("""
        [{"id":"options","type":"object","required":true,"title":"Options","sensitivity":"public","disclosure":"guided",
          "properties":[
            {"id":"host","type":"string","required":true,"title":"Host","sensitivity":"public","disclosure":"guided"},
            {"id":"limit","type":"integer","title":"Limit","sensitivity":"public","disclosure":"advanced"}]}]
        """);
        source["artifacts"]![0]!["projection"] = JsonNode.Parse("""{"op":"literal","value":{}}""");
        using var form = new DefinitionFormModel(
            SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString())));
        long revision = form.InputRevision;
        await Assert.That(form.SetEditorExpanded(["options"], true)).IsTrue();
        await Assert.That(form.IsEditorVisible(["options", "limit"])).IsTrue();
        await Assert.That(form.InputRevision).IsEqualTo(revision);
        form.EditValue(["options", "limit"], "invalid");
        form.SetEditorExpanded(["options"], false);
        await Assert.That(form.IsEditorVisible(["options", "limit"])).IsTrue();
        await Assert.That(form.Projection.SectionProgress["configure"].Invalid).IsEqualTo(1);
        await Assert.That(form.Projection.SectionProgress["configure"].Required).IsEqualTo(1);
        form.EditValue(["options", "limit"], "7");
        await Assert.That(form.IsEditorVisible(["options", "limit"])).IsTrue();
        form.SetEditorExpanded(["options"], false);
        await Assert.That(form.IsEditorVisible(["options", "limit"])).IsFalse();
        await Assert.That(form.InputRevision).IsEqualTo(revision + 2);
    }

    [Test]
    public async Task MissingRequiredSecretContainerIsNotADeferredLeaf()
    {
        JsonObject source = JsonNode.Parse(Definition().CanonicalBytes.Span)!.AsObject();
        source["fields"] = JsonNode.Parse("""
        [{"id":"keys","type":"list","itemType":"string","required":true,"minItems":1,
          "title":"Keys","sensitivity":"secret","disclosure":"guided"}]
        """);
        source["artifacts"]![0]!["projection"] = JsonNode.Parse("""{"op":"literal","value":{}}""");
        using var form = new DefinitionFormModel(
            SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString())),
            DefinitionSessionPrivacy.BrowserNoSecrets);

        await Assert.That(form.Projection.Progress.Required).IsEqualTo(1);
        await Assert.That(form.Projection.Progress.Missing).IsEqualTo(1);
        await Assert.That(form.Projection.Progress.Deferred).IsEqualTo(0);
    }

    [Test]
    public async Task EssentialNestedDecisionPromotesAncestorsWithoutExpandingOptionalSecrets()
    {
        JsonObject source = JsonNode.Parse(Definition().CanonicalBytes.Span)!.AsObject();
        source["fields"] = JsonNode.Parse("""
        [{"id":"options","type":"object","title":"Options","sensitivity":"public","disclosure":"advanced",
          "properties":[
            {"id":"enabled","type":"boolean","title":"Enable feature","sensitivity":"public","disclosure":"essential"},
            {"id":"token","type":"string","title":"Optional token","sensitivity":"secret","disclosure":"advanced"}]}]
        """);
        source["artifacts"]![0]!["projection"] = JsonNode.Parse("""{"op":"literal","value":{}}""");
        using var form = new DefinitionFormModel(
            SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString())));

        await Assert.That(form.IsEditorVisible(["options"])).IsTrue();
        await Assert.That(form.IsEditorVisible(["options", "enabled"])).IsTrue();
        await Assert.That(form.IsEditorVisible(["options", "token"])).IsFalse();
        await Assert.That(form.Snapshot().Values.Count).IsEqualTo(0);
    }

    [Test]
    public async Task MachineOnlyGroupsAreSharedDiscoverableSectionsWithoutInventedExperiences()
    {
        JsonObject source = JsonNode.Parse(Definition().CanonicalBytes.Span)!.AsObject();
        source["fields"]![0]!["group"] = "Network settings";
        using var form = new DefinitionFormModel(
            SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString())));
        form.SetDiscovery("network");

        await Assert.That(form.Experiences.Count).IsEqualTo(1);
        await Assert.That(form.Experiences.Single().Id).IsEqualTo("configure");
        await Assert.That(form.Projection.Matches.Single().Entry.FieldId).IsEqualTo("enabled");
        await Assert.That(form.Projection.Sections.Any(section => section.Title == "Network settings")).IsTrue();
        await Assert.That(form.Projection.Discovery.Facets[DefinitionDiscoveryFacet.Section]
            .Count(option => option.Count > 0)).IsEqualTo(1);
    }

    [Test]
    public async Task DisposalRetiresCachedProjectionAndPotentiallyPrivateDiscovery()
    {
        var form = new DefinitionFormModel(Definition());
        form.EditValue(["enabled"], "false");
        form.SetDiscovery("synthetic-private-query");
        _ = form.Projection;
        form.Dispose();

        await Assert.That(form.DiscoveryQuery).IsEqualTo("");
        await Assert.That(() => form.Projection).Throws<ObjectDisposedException>();
        await Assert.That(form.Fields.All(input => input.Text.Length == 0)).IsTrue();
    }

    [Test]
    public async Task ListCardinalityAndActualRequiredRowsRecomputeIndependentObligations()
    {
        JsonObject source = JsonNode.Parse(Definition().CanonicalBytes.Span)!.AsObject();
        source["fields"] = JsonNode.Parse("""
        [{"id":"rows","type":"list","itemType":"object","required":true,"minItems":1,
          "title":"Rows","sensitivity":"public","disclosure":"advanced",
          "properties":[{"id":"enabled","type":"boolean","required":true,
            "title":"Enabled","sensitivity":"public","disclosure":"guided"}]}]
        """);
        source["artifacts"]![0]!["projection"] = JsonNode.Parse("""{"op":"literal","value":{}}""");
        using var form = new DefinitionFormModel(
            SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString())));
        await Assert.That(form.Projection.Progress.Required).IsEqualTo(1);
        await Assert.That(form.Projection.Progress.Ready).IsEqualTo(0);
        form.AddListItem(["rows"]);
        await Assert.That(form.Projection.Progress.Required).IsEqualTo(2);
        await Assert.That(form.Projection.Progress.Ready).IsEqualTo(1);
        form.EditValue(["rows", "0", "enabled"], "false");
        await Assert.That(form.Projection.Progress.Ready).IsEqualTo(2);
        form.RemoveListItem(["rows"], 0);
        await Assert.That(form.Projection.Progress.Required).IsEqualTo(1);
        await Assert.That(form.Projection.Progress.Ready).IsEqualTo(0);
    }

    [Test]
    public async Task BindingKeyDiscoveryAndCorrectionResetFacetsWithoutMutatingAnswers()
    {
        JsonObject source = JsonNode.Parse(Definition().CanonicalBytes.Span)!.AsObject();
        source["artifacts"]![0]!["projection"] = JsonNode.Parse("""
        {"op":"object","members":{"ENABLE_INTEGRATION":{"op":"copy","field":"enabled"}}}
        """);
        using var form = new DefinitionFormModel(
            SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString())));
        form.EditValue(["enabled"], "false");
        form.SetDiscovery("ENABLE_INTEGRATION");
        await Assert.That(form.Projection.Matches.Single().Entry.FieldId).IsEqualTo("enabled");
        await Assert.That(form.Projection.Matches.Single().MatchReason).IsEqualTo("binding-key");
        long revision = form.InputRevision;
        form.SetDiscoverySelection(new("absent", facets:
            new Dictionary<DefinitionDiscoveryFacet, IReadOnlyList<string>>
            { [DefinitionDiscoveryFacet.Sensitivity] = ["secret"] }));
        await Assert.That(form.Projection.Matches.Count).IsEqualTo(0);
        await Assert.That(form.FocusEditor(["enabled"])).IsTrue();
        await Assert.That(form.DiscoveryQuery).IsEqualTo("");
        await Assert.That(form.DiscoverySelection.Facets.Count).IsEqualTo(0);
        await Assert.That(form.IsEditorVisible(["enabled"])).IsTrue();
        await Assert.That(form.InputRevision).IsEqualTo(revision);
    }

    [Test]
    public async Task AuthoredExperienceChangesOnlyViewStateAndOptionalExpansionRestoresDefaults()
    {
        JsonObject source = JsonNode.Parse(Definition().CanonicalBytes.Span)!.AsObject();
        source["presentation"] = JsonNode.Parse("""
        {"defaultExperience":"guided","sections":[
          {"id":"connection","title":"Connection","fields":["enabled","token"]}],
         "experiences":[
          {"id":"guided","title":"Get connected","initialDisclosure":"essential",
           "navigation":"guided","sections":["connection"]},
          {"id":"complete","title":"All settings","initialDisclosure":"advanced",
           "navigation":"sections","sections":["connection"]}]}
        """);
        using var form = new DefinitionFormModel(
            SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString())));
        form.EditValue(["enabled"], "false");
        long revision = form.InputRevision;
        int viewChanges = 0;
        form.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == "SelectedExperienceId") viewChanges++;
        };
        await Assert.That(form.VisibleFields.Count).IsEqualTo(1);
        form.SetDiscovery("token");
        await Assert.That(form.VisibleFields.Count).IsEqualTo(1);

        // Search can locate a collapsed editor without silently changing disclosure.
        await Assert.That(form.Projection.Matches.Single().Entry.FieldId).IsEqualTo("token");
        // Selection is tested through the shared API once admitted, not through a client preset.
        await Assert.That(form.SelectExperience("complete")).IsTrue();
        await Assert.That(form.IsEditorVisible(["token"])).IsTrue();
        await Assert.That(form.SelectExperience("unknown")).IsFalse();
        await Assert.That(form.SelectExperience("guided")).IsTrue();
        form.SetAllSectionsExpanded(true);
        await Assert.That(form.IsEditorVisible(["token"])).IsTrue();
        form.SetAllSectionsExpanded(false);
        await Assert.That(form.IsEditorVisible(["token"])).IsFalse();
        await Assert.That(form.InputRevision).IsEqualTo(revision);
        await Assert.That(viewChanges).IsEqualTo(2);
    }

    [Test]
    public async Task OptionalAdvancedSecretsDoNotForceEditorsOpen()
    {
        using var form = new DefinitionFormModel(Definition(), DefinitionSessionPrivacy.BrowserNoSecrets);

        await Assert.That(form.VisibleFields.Count).IsEqualTo(1);
        await Assert.That(form.VisibleFields.Single().Definition.Id).IsEqualTo("enabled");
        await Assert.That(form.CanEdit(["token"])).IsFalse();
        await Assert.That(form.Snapshot().Values.ContainsKey("token")).IsFalse();
    }

    [Test]
    public async Task InactiveInvalidDraftDoesNotBlockActiveConfiguration()
    {
        JsonObject source = JsonNode.Parse(Definition().CanonicalBytes.Span)!.AsObject();
        source["fields"]![1]!["type"] = "integer";
        source["fields"]![1]!["sensitivity"] = "public";
        source["fields"]![1]!["activeWhen"] =
            JsonNode.Parse("""{"op":"equals","field":"enabled","value":true}""");
        using var form = new DefinitionFormModel(
            SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString())));
        form.EditValue(["enabled"], "true");
        form.EditValue(["token"], "invalid-number");
        await Assert.That(form.HasInputErrors).IsTrue();

        form.EditValue(["enabled"], "false");

        await Assert.That(form.HasInputErrors).IsFalse();
        await Assert.That(form.Validate().IsReady).IsTrue();
    }

    [Test]
    public async Task ProgressCountsRequiredAnswersAndAcceptsExplicitFalse()
    {
        using var form = new DefinitionFormModel(Definition(), DefinitionSessionPrivacy.BrowserNoSecrets);
        await Assert.That(form.Projection.Progress.Required).IsEqualTo(1);
        await Assert.That(form.Projection.Progress.Ready).IsEqualTo(0);
        await Assert.That(form.Projection.Progress.Missing).IsEqualTo(1);

        form.EditValue(["enabled"], "false");

        await Assert.That(form.Projection.Progress.Required).IsEqualTo(1);
        await Assert.That(form.Projection.Progress.Ready).IsEqualTo(1);
        await Assert.That(form.Projection.Progress.Percent).IsEqualTo(100);
    }

    [Test]
    public async Task RequiredObjectDoesNotCountItsRequiredChildrenTwice()
    {
        JsonObject source = JsonNode.Parse(Definition().CanonicalBytes.Span)!.AsObject();
        source["fields"] = JsonNode.Parse("""
        [{"id":"settings","type":"object","required":true,"title":"Settings","sensitivity":"public","disclosure":"essential",
          "properties":[
            {"id":"name","type":"string","required":true,"title":"Name","sensitivity":"public","disclosure":"guided"},
            {"id":"port","type":"integer","required":true,"title":"Port","sensitivity":"public","disclosure":"guided"}]}]
        """);
        source["artifacts"]![0]!["projection"] = JsonNode.Parse("""{"op":"literal","value":{}}""");
        using var form = new DefinitionFormModel(
            SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString())));
        form.SetValue(["settings"], JsonSerializer.SerializeToElement(new { name = "configured" }));

        await Assert.That(form.Projection.Progress.Required).IsEqualTo(2);
        await Assert.That(form.Projection.Progress.Ready).IsEqualTo(1);
        await Assert.That(form.Projection.Progress.Missing).IsEqualTo(1);
    }

    [Test]
    public async Task DiscoveryMatchesPublicMetadataButNeverAnswerContents()
    {
        using var form = new DefinitionFormModel(Definition(), DefinitionSessionPrivacy.BrowserSecretsEnabled);
        form.EditValue(["token"], "synthetic-private-search-canary");
        form.SetDiscovery("synthetic-private-search-canary");
        await Assert.That(form.Projection.Matches.Count).IsEqualTo(0);

        long revision = form.InputRevision;
        form.SetDiscovery("token");

        await Assert.That(form.Projection.Matches.Count).IsEqualTo(1);
        await Assert.That(form.Projection.Matches.Single().Entry.FieldId).IsEqualTo("token");
        await Assert.That(form.InputRevision).IsEqualTo(revision);
    }

    [Test]
    public async Task NoRequiredAnswersHaveNoPercentageButInvalidOptionalDraftStillBlocks()
    {
        JsonObject source = JsonNode.Parse(Definition().CanonicalBytes.Span)!.AsObject();
        source["fields"]![0]!["required"] = false;
        source["fields"]![0]!["type"] = "integer";
        using var form = new DefinitionFormModel(
            SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString())));
        form.EditValue(["enabled"], "invalid-number");

        await Assert.That(form.Projection.Progress.Required).IsEqualTo(0);
        await Assert.That(form.Projection.Progress.Percent).IsNull();
        await Assert.That(form.Projection.Progress.Invalid).IsEqualTo(1);
        await Assert.That(form.HasInputErrors).IsTrue();
    }

    private static SetupDefinition Definition() => SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes("""
    {"schemaVersion":1,"familyId":"32d3b55d-7285-4b0f-8bac-77c9c9f32003","revision":1,
     "applicationId":"example","applicationVersions":["1"],"publisher":"example",
     "title":"Projection example","entryPoints":["setup"],"locales":["en"],
     "fields":[
       {"id":"enabled","type":"boolean","title":"Enable integration","required":true,
        "sensitivity":"public","disclosure":"essential"},
       {"id":"token","type":"string","title":"Integration credential","required":false,
        "sensitivity":"secret","disclosure":"advanced"}],
     "artifacts":[{"id":"config","format":"json","fileName":"service.json",
       "applySemantics":"create","restart":"process",
       "projection":{"op":"object","members":{"enabled":{"op":"copy","field":"enabled"}}}}]}
    """));
}
