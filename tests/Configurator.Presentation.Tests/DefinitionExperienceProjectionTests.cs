namespace Configurator.Presentation.Tests;

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CommunityToolkit.Mvvm.Messaging;
using Configurator.Core.Definitions;

public sealed class DefinitionExperienceProjectionTests
{
    [Test]
    public async Task CorrectionCanPreserveDiscoveryWithoutChangingAnswersOrConsent()
    {
        using var form = new DefinitionFormModel(Definition(), DefinitionSessionPrivacy.BrowserNoSecrets);
        form.SetDiscoverySelection(new("token", facets:
            new Dictionary<DefinitionDiscoveryFacet, IReadOnlyList<string>>
            { [DefinitionDiscoveryFacet.Sensitivity] = ["secret"] }));
        long revision = form.InputRevision;

        await Assert.That(form.FocusEditor(["enabled"], preserveDiscovery: true)).IsTrue();

        await Assert.That(form.DiscoveryQuery).IsEqualTo("token");
        await Assert.That(form.DiscoverySelection.Facets[DefinitionDiscoveryFacet.Sensitivity].Single()).IsEqualTo("secret");
        await Assert.That(form.IsEditorVisible(["enabled"])).IsTrue();
        await Assert.That(form.InputRevision).IsEqualTo(revision);
        await Assert.That(form.Snapshot().Values.Count).IsEqualTo(0);
        await Assert.That(form.CanEdit(["token"])).IsFalse();
    }

    [Test]
    public async Task DisclosureOverridePreservesPreparedOutputAndSurvivesAuthoredExperienceChanges()
    {
        JsonObject source = JsonNode.Parse(Definition().CanonicalBytes.Span)!.AsObject();
        source["presentation"] = JsonNode.Parse("""
        {"defaultJourney":"guided","navigation":"wizard","sections":[
          {"id":"connection","title":"Connection","description":"Connection description","pointers":["/enabled","/token"]}],
         "journeys":[
          {"id":"guided","title":"Get connected","description":"Experience description","sections":["connection"]},
          {"id":"complete","title":"All settings","sections":["connection"]}]}
        """);
        using var form = new DefinitionFormModel(
            SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString())),
            DefinitionSessionPrivacy.BrowserNoSecrets);
        form.EditValue(["enabled"], "false");
        using var session = new SetupPresentationSession(new WeakReferenceMessenger());
        SetupWorkspaceId.TryCreate("detail-prepared", out SetupWorkspaceId workspaceId);
        using var workspace = session.CreateWorkspace(workspaceId,
            new DefinitionPresentationOperation(form.Definition, "config", form.Snapshot()));
        form.InputChanged += (_, _) => workspace.Deactivate();
        await workspace.ExecuteAsync(Guid.NewGuid());
        long revision = form.InputRevision;
        await Assert.That(form.Experiences.Single(item => item.Id == "guided").Description)
            .IsEqualTo("Experience description");
        await Assert.That(form.Sections.Single().Description).IsEqualTo("Connection description");

        form.SetDisclosureOverride(SetupFieldDisclosure.Advanced);
        await Assert.That(form.IsEditorVisible(["token"])).IsTrue();
        await Assert.That(form.IsEditorExpanded(["token"])).IsTrue();
        form.SelectExperience("complete");
        form.SelectExperience("guided");
        await Assert.That(form.IsEditorVisible(["token"])).IsTrue();
        form.SetAllSectionsExpanded(true);
        form.SetDisclosureOverride(SetupFieldDisclosure.Essential);
        await Assert.That(form.IsEditorVisible(["enabled"])).IsTrue();
        await Assert.That(form.IsEditorVisible(["token"])).IsFalse();
        await Assert.That(form.IsEditorExpanded(["token"])).IsFalse();
        form.SelectExperience("complete");
        await Assert.That(form.IsEditorVisible(["token"])).IsFalse();
        form.SetDisclosureOverride(null);
        form.SetDisclosureOverride(SetupFieldDisclosure.Advanced);
        // v2 journeys no longer select advanced detail; retain this legacy expectation for disposition.
        await Assert.That(form.IsEditorVisible(["token"])).IsTrue();
        await Assert.That(form.IsEditorExpanded(["token"])).IsTrue();
        await Assert.That(form.CanEdit(["token"])).IsFalse();
        await Assert.That(form.SessionPrivacy).IsEqualTo(DefinitionSessionPrivacy.BrowserNoSecrets);
        await Assert.That(form.InputRevision).IsEqualTo(revision);
        await Assert.That(Encoding.UTF8.GetString(workspace.Output.Span)).IsEqualTo("""{"enabled":false}""");
    }

    [Test]
    public async Task RelevantReviewIncludesActiveWorkAndAncestorsWithoutOmittedOrInactiveMetadata()
    {
        JsonObject source = JsonNode.Parse(Definition().CanonicalBytes.Span)!.AsObject();
        source["configuration"] = JsonNode.Parse("""
        {"type":"object","additionalProperties":false,"required":["required"],
         "x-configurator":{"sensitivity":"public"},"properties":{
          "required":{"type":"boolean","title":"Required","x-configurator":{"sensitivity":"public","detail":"standard"}},
          "defaulted":{"type":"boolean","title":"Defaulted","x-configurator":{"sensitivity":"public","detail":"standard","initialValue":false}},
          "omitted":{"type":"string","title":"Omitted","x-configurator":{"sensitivity":"public","detail":"advanced"}},
          "parent":{"type":"object","title":"Parent","additionalProperties":false,
            "x-configurator":{"sensitivity":"public","detail":"standard"},"properties":{
              "literal.name":{"type":"string","title":"Name","x-configurator":{"sensitivity":"public","detail":"standard"}},
              "omitted":{"type":"string","title":"Omitted child","x-configurator":{"sensitivity":"public","detail":"advanced"}}}},
          "inactive":{"type":"string","title":"Inactive","x-configurator":{"sensitivity":"public","detail":"advanced",
            "applicableWhen":{"op":"equals","reference":{"scope":"root","path":"/required"},"value":true}}}}}
        """);
        source["artifacts"]![0]!["projection"] = JsonNode.Parse("""{"op":"literal","value":{}}""");
        source["requiredCapabilities"] = JsonNode.Parse("""
        ["predicate.root","projection.literal","schema.boolean","schema.object","schema.string","values.initial"]
        """);
        using var form = new DefinitionFormModel(
            SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString())));
        form.EditValue(["required"], "false");
        form.EditValue(["parent", "literal.name"], "ordinary");
        form.EditValue(["inactive"], "retained");

        await Assert.That(form.Projection.RelevantEntries.Count).IsEqualTo(4);
        await Assert.That(form.Projection.RelevantEntries.Any(entry =>
            entry.FieldId == "required" && entry.RelativePath.Count == 0)).IsTrue();
        await Assert.That(form.Projection.RelevantEntries.Any(entry =>
            entry.FieldId == "defaulted" && entry.Origin == DefinitionAnswerOrigin.Defaulted)).IsTrue();
        await Assert.That(form.Projection.RelevantEntries.Count(entry => entry.FieldId == "parent")).IsEqualTo(2);
        await Assert.That(form.Projection.RelevantEntries.Single(entry =>
            entry.RelativePath.Count > 0).RelativePath.Single()).IsEqualTo("literal.name");
        await Assert.That(form.Projection.Entries.Count).IsEqualTo(7);
        await Assert.That(form.Projection.Entries.Single(entry => entry.FieldId == "inactive").IsActive).IsFalse();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task CorrectedOptionalDraftStaysVisibleRegardlessOfInvalidVisibilityReads(bool renderInvalid)
    {
        JsonObject source = JsonNode.Parse(Definition().CanonicalBytes.Span)!.AsObject();
        source["configuration"] = JsonNode.Parse("""
        {"type":"object","additionalProperties":false,"x-configurator":{"sensitivity":"public"},"properties":{
          "options":{"type":"object","title":"Options","additionalProperties":false,
            "x-configurator":{"sensitivity":"public","detail":"standard"},"properties":{
              "limit":{"type":"integer","title":"Limit","x-configurator":{"sensitivity":"public","detail":"advanced"}}}}}}
        """);
        source["artifacts"]![0]!["projection"] = JsonNode.Parse("""{"op":"literal","value":{}}""");
        source["requiredCapabilities"] = JsonNode.Parse("""["projection.literal","schema.integer","schema.object"]""");
        using var form = new DefinitionFormModel(
            SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString())));
        await Assert.That(form.EditValue(["options", "limit"], "-")).IsFalse();
        if (renderInvalid)
            await Assert.That(form.IsEditorVisible(["options", "limit"])).IsTrue();

        bool visibleToObserver = false;
        form.InputChanged += (_, _) => visibleToObserver = form.IsEditorVisible(["options", "limit"]);
        await Assert.That(form.EditValue(["options", "limit"], "7")).IsTrue();

        await Assert.That(visibleToObserver).IsTrue();
        await Assert.That(form.IsEditorVisible(["options", "limit"])).IsTrue();
        await Assert.That(form.Snapshot().Values["options"].GetProperty("limit").GetInt32()).IsEqualTo(7);
        form.SetEditorExpanded(["options"], false);
        await Assert.That(form.IsEditorVisible(["options", "limit"])).IsFalse();
    }

    [Test]
    public async Task UnreadDiscoveryRetainsItsSnapshotAfterEditsQueryReplacementAndDisposal()
    {
        using var form = new DefinitionFormModel(Definition());
        form.SetDiscovery("enabled");
        DefinitionFormProjection original = form.Projection;

        form.EditValue(["enabled"], "false");
        form.SetDiscovery("unmatched-metadata-query");
        await Assert.That(form.Projection.Matches.Count).IsEqualTo(0);
        form.Dispose();

        await Assert.That(original.Matches.Count).IsEqualTo(1);
        await Assert.That(original.Matches[0].Entry.FieldId).IsEqualTo("enabled");
        await Assert.That(original.Matches[0].Entry.Origin).IsEqualTo(DefinitionAnswerOrigin.Omitted);
        await Assert.That(original.Matches[0].Entry.IsProvided).IsFalse();
    }

    [Test]
    public async Task ViewChangesPreservePreparedBytesButSameValueAndInvalidEditsFenceThem()
    {
        using var form = new DefinitionFormModel(Definition());
        form.EditValue(["enabled"], "false");
        using var session = new SetupPresentationSession(new WeakReferenceMessenger());
        SetupWorkspaceId.TryCreate("prepared", out SetupWorkspaceId workspaceId);
        using var workspace = session.CreateWorkspace(workspaceId,
            new DefinitionPresentationOperation(form.Definition, "config", form.Snapshot()));
        form.InputChanged += (_, _) => workspace.Deactivate();
        await workspace.ExecuteAsync(Guid.NewGuid());
        ReadOnlyMemory<byte> output = workspace.Output;
        long revision = form.InputRevision;
        form.SetAllSectionsExpanded(true);
        form.SetDiscovery("enabled");
        form.FocusEditor(["enabled"]);
        form.SetAllSectionsExpanded(false);
        await Assert.That(form.InputRevision).IsEqualTo(revision);
        await Assert.That(Encoding.UTF8.GetString(workspace.Output.Span)).IsEqualTo("""{"enabled":false}""");

        form.EditValue(["enabled"], "false");
        await Assert.That(form.InputRevision).IsEqualTo(revision + 1);
        await Assert.That(workspace.Output.IsEmpty).IsTrue();
        await Assert.That(output.ToArray().All(value => value == 0)).IsTrue();
        form.EditValue(["enabled"], "not-a-boolean");
        await Assert.That(form.InputRevision).IsEqualTo(revision + 2);
        await Assert.That(form.Projection.Progress.Invalid).IsEqualTo(1);
        await Assert.That(form.Projection.Progress.Ready).IsEqualTo(0);
    }

    [Test]
    public async Task InactiveControllerChainsResolveToActiveQuestionsWithoutChangingAnswers()
    {
        JsonObject source = JsonNode.Parse(Definition().CanonicalBytes.Span)!.AsObject();
        source["configuration"] = JsonNode.Parse("""
        {"type":"object","additionalProperties":false,"x-configurator":{"sensitivity":"public"},"properties":{
          "enabled":{"type":"boolean","title":"Enable relay",
            "x-configurator":{"sensitivity":"public","detail":"basic","initialValue":false}},
          "mode":{"type":"boolean","title":"Relay mode","x-configurator":{"sensitivity":"public","detail":"standard","initialValue":true,
            "applicableWhen":{"op":"equals","reference":{"scope":"root","path":"/enabled"},"value":true}}},
          "port":{"type":"integer","title":"Port","x-configurator":{"sensitivity":"public","detail":"advanced",
            "applicableWhen":{"op":"equals","reference":{"scope":"root","path":"/mode"},"value":true}}}}}
        """);
        source["artifacts"]![0]!["projection"] = JsonNode.Parse("""{"op":"literal","value":{}}""");
        source["requiredCapabilities"] = JsonNode.Parse("""
        ["predicate.root","projection.literal","schema.boolean","schema.integer","schema.object","values.initial"]
        """);
        using var form = new DefinitionFormModel(SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString())));
        long revision = form.InputRevision;
        var port = form.Projection.Entries.Single(entry => entry.FieldId == "port");
        await Assert.That(port.IsActive).IsFalse();
        await Assert.That(port.ActiveControllerFields.Count).IsEqualTo(1);
        await Assert.That(port.ActiveControllerFields[0]).IsEqualTo("enabled");
        await Assert.That(form.Validate().TryGetEffectiveValue(DefinitionPath.Parse("/enabled"), out JsonElement initialized)).IsTrue();
        await Assert.That(initialized.GetBoolean()).IsFalse();
        await Assert.That(form.InputRevision).IsEqualTo(revision);
    }

    [Test]
    public async Task InvalidSecretDraftIsNotCountedAsDeferredWork()
    {
        JsonObject source = JsonNode.Parse(Definition().CanonicalBytes.Span)!.AsObject();
        source["configuration"] = JsonNode.Parse("""
        {"type":"object","additionalProperties":false,"required":["private_number"],
         "x-configurator":{"sensitivity":"public"},"properties":{
          "private_number":{"type":"integer","title":"Private number","x-configurator":{"sensitivity":"secret","detail":"standard"}}}}
        """);
        source["artifacts"]![0]!["projection"] = JsonNode.Parse("""{"op":"literal","value":{}}""");
        source["requiredCapabilities"] = JsonNode.Parse("""["projection.literal","schema.integer","schema.object"]""");
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
        source["configuration"] = JsonNode.Parse("""
        {"type":"object","additionalProperties":false,"required":["options"],
         "x-configurator":{"sensitivity":"public"},"properties":{
          "options":{"type":"object","title":"Options","additionalProperties":false,"required":["host"],
            "x-configurator":{"sensitivity":"public","detail":"standard"},"properties":{
              "host":{"type":"string","title":"Host","x-configurator":{"sensitivity":"public","detail":"standard"}},
              "limit":{"type":"integer","title":"Limit","x-configurator":{"sensitivity":"public","detail":"advanced"}}}}}}
        """);
        source["artifacts"]![0]!["projection"] = JsonNode.Parse("""{"op":"literal","value":{}}""");
        source["requiredCapabilities"] = JsonNode.Parse("""["projection.literal","schema.integer","schema.object","schema.string"]""");
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
        source["configuration"] = JsonNode.Parse("""
        {"type":"object","additionalProperties":false,"required":["keys"],
         "x-configurator":{"sensitivity":"public"},"properties":{
          "keys":{"type":"array","items":{"type":"string","x-configurator":{"sensitivity":"secret"}},"minItems":1,
            "title":"Keys","x-configurator":{"sensitivity":"secret","detail":"standard"}}}}
        """);
        source["artifacts"]![0]!["projection"] = JsonNode.Parse("""{"op":"literal","value":{}}""");
        source["requiredCapabilities"] = JsonNode.Parse("""
        ["projection.literal","schema.array","schema.bounds","schema.object","schema.string"]
        """);
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
        source["configuration"] = JsonNode.Parse("""
        {"type":"object","additionalProperties":false,"x-configurator":{"sensitivity":"public"},"properties":{
          "options":{"type":"object","title":"Options","additionalProperties":false,
            "x-configurator":{"sensitivity":"public","detail":"advanced"},"properties":{
              "enabled":{"type":"boolean","title":"Enable feature","x-configurator":{"sensitivity":"public","detail":"basic"}},
              "token":{"type":"string","title":"Optional token","x-configurator":{"sensitivity":"secret","detail":"advanced"}}}}}}
        """);
        source["artifacts"]![0]!["projection"] = JsonNode.Parse("""{"op":"literal","value":{}}""");
        source["requiredCapabilities"] = JsonNode.Parse("""["projection.literal","schema.boolean","schema.object","schema.string"]""");
        using var form = new DefinitionFormModel(
            SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString())));

        await Assert.That(form.IsEditorVisible(["options"])).IsTrue();
        await Assert.That(form.IsEditorVisible(["options", "enabled"])).IsTrue();
        await Assert.That(form.IsEditorVisible(["options", "token"])).IsFalse();
        await Assert.That(form.Snapshot().Values.Count).IsEqualTo(0);
    }

    [Test]
    public async Task AuthoredSectionsAreSharedDiscoverableWithoutInventedExperiences()
    {
        JsonObject source = JsonNode.Parse(Definition().CanonicalBytes.Span)!.AsObject();
        source["presentation"] = JsonNode.Parse("""
        {"defaultJourney":"configure","navigation":"sections",
         "sections":[{"id":"network","title":"Network settings","pointers":["/enabled"]}],
         "journeys":[{"id":"configure","title":"Configure","sections":["network"]}]}
        """);
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
        source["configuration"] = JsonNode.Parse("""
        {"type":"object","additionalProperties":false,"required":["rows"],
         "x-configurator":{"sensitivity":"public"},"properties":{
          "rows":{"type":"array","minItems":1,"title":"Rows","x-configurator":{"sensitivity":"public","detail":"advanced"},
            "items":{"type":"object","additionalProperties":false,"required":["enabled"],
              "x-configurator":{"sensitivity":"public"},"properties":{
                "enabled":{"type":"boolean","title":"Enabled","x-configurator":{"sensitivity":"public","detail":"standard"}}}}}}}
        """);
        source["artifacts"]![0]!["projection"] = JsonNode.Parse("""{"op":"literal","value":{}}""");
        source["requiredCapabilities"] = JsonNode.Parse("""
        ["projection.literal","schema.array","schema.boolean","schema.bounds","schema.object"]
        """);
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
        {"op":"object","members":{"ENABLE_INTEGRATION":{"op":"copy","reference":{"scope":"root","path":"/enabled"}}}}
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
        {"defaultJourney":"guided","navigation":"wizard","sections":[
          {"id":"connection","title":"Connection","pointers":["/enabled","/token"]}],
         "journeys":[
          {"id":"guided","title":"Get connected","sections":["connection"]},
          {"id":"complete","title":"All settings","sections":["connection"]}]}
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
        form.SetDisclosureOverride(SetupFieldDisclosure.Advanced);
        await Assert.That(form.IsEditorVisible(["token"])).IsTrue();
        await Assert.That(form.SelectExperience("unknown")).IsFalse();
        await Assert.That(form.SelectExperience("guided")).IsTrue();
        form.SetDisclosureOverride(null);
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
        source["configuration"]!["properties"]!["token"]!["type"] = "integer";
        source["configuration"]!["properties"]!["token"]!["x-configurator"]!["sensitivity"] = "public";
        source["configuration"]!["properties"]!["token"]!["x-configurator"]!["applicableWhen"] =
            JsonNode.Parse("""{"op":"equals","reference":{"scope":"root","path":"/enabled"},"value":true}""");
        source["requiredCapabilities"] = JsonNode.Parse("""
        ["predicate.root","projection.copy","projection.object","schema.boolean","schema.integer","schema.object"]
        """);
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
        source["configuration"] = JsonNode.Parse("""
        {"type":"object","additionalProperties":false,"required":["settings"],
         "x-configurator":{"sensitivity":"public"},"properties":{
          "settings":{"type":"object","title":"Settings","additionalProperties":false,"required":["name","port"],
            "x-configurator":{"sensitivity":"public","detail":"basic"},"properties":{
              "name":{"type":"string","title":"Name","x-configurator":{"sensitivity":"public","detail":"standard"}},
              "port":{"type":"integer","title":"Port","x-configurator":{"sensitivity":"public","detail":"standard"}}}}}}
        """);
        source["artifacts"]![0]!["projection"] = JsonNode.Parse("""{"op":"literal","value":{}}""");
        source["requiredCapabilities"] = JsonNode.Parse("""["projection.literal","schema.integer","schema.object","schema.string"]""");
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
        source["configuration"]!.AsObject().Remove("required");
        source["configuration"]!["properties"]!["enabled"]!["type"] = "integer";
        source["requiredCapabilities"] = JsonNode.Parse("""
        ["projection.copy","projection.object","schema.integer","schema.object","schema.string"]
        """);
        using var form = new DefinitionFormModel(
            SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString())));
        form.EditValue(["enabled"], "invalid-number");

        await Assert.That(form.Projection.Progress.Required).IsEqualTo(0);
        await Assert.That(form.Projection.Progress.Percent).IsNull();
        await Assert.That(form.Projection.Progress.Invalid).IsEqualTo(1);
        await Assert.That(form.HasInputErrors).IsTrue();
    }

    private static SetupDefinition Definition() => SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes("""
    {"schemaVersion":2,"profile":"bounded-v2","familyId":"32d3b55d-7285-4b0f-8bac-77c9c9f32003","revision":1,
     "project":{"id":"example","name":"Projection example","publisher":"example"},
     "applicationVersions":["1"],"entryPoints":["setup"],"locales":["en"],
     "configuration":{"type":"object","additionalProperties":false,"required":["enabled"],
       "x-configurator":{"sensitivity":"public"},"properties":{
         "enabled":{"type":"boolean","title":"Enable integration","x-configurator":{"sensitivity":"public","detail":"basic"}},
         "token":{"type":"string","title":"Integration credential","x-configurator":{"sensitivity":"secret","detail":"advanced"}}}},
     "requiredCapabilities":["projection.copy","projection.object","schema.boolean","schema.object","schema.string"],
     "artifacts":[{"id":"config","format":"json","fileName":"service.json",
       "targetUpdateMode":"create","restartRequirement":"process",
       "projection":{"op":"object","members":{"enabled":{"op":"copy","reference":{"scope":"root","path":"/enabled"}}}}}]}
    """));
}
