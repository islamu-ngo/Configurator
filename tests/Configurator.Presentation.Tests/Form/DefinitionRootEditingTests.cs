namespace Configurator.Presentation.Tests;

using System.Text.Json;
using System.Text.Json.Nodes;
using CommunityToolkit.Mvvm.Messaging;
using Configurator.Core.Artifacts;
using Configurator.Core.Definitions;

public sealed class DefinitionRootEditingTests
{
    private static readonly int[] RootRows = [1, 2];

    [Test]
    public async Task EmptyRootRemainsVisibleAndFocusableWithoutOrdinaryFields()
    {
        using var form = new DefinitionFormModel(Variant());
        await Assert.That(form.IsEditorVisible(Array.Empty<string>())).IsTrue();
        await Assert.That(form.FocusEditor(Array.Empty<string>())).IsTrue();
    }

    [Test]
    public async Task TypedRootVariantSupportsDisclosureCorrectionAndRetiredBranchIdentity()
    {
        using var form = new DefinitionFormModel(Variant());
        DefinitionPath root = DefinitionPath.FromSegments([]);
        DefinitionPath value = DefinitionPath.Parse("/value");
        await Assert.That(form.GetEditorPresentation(root)!.Schema.Variants.Count).IsEqualTo(2);
        await Assert.That(form.GetVariantTitle(root, 0)).IsEqualTo("first");
        await Assert.That(form.IsEditorVisible(root)).IsTrue();
        await Assert.That(form.SetEditorExpanded(root, true)).IsTrue();
        await Assert.That(form.IsEditorExpanded(root)).IsTrue();
        await Assert.That(form.SelectVariant(root, 0)).IsTrue();
        await Assert.That(form.IsEditorExpanded(value)).IsTrue();
        string identity = form.GetEditorIdentity(value);
        form.SetDiscovery("unmatched");
        await Assert.That(form.EditValue(value, "invalid")).IsFalse();
        DefinitionDiagnostic diagnostic = form.InputDiagnostics.Single();
        await Assert.That(form.TryResolveCorrectionPath(diagnostic.FieldId, diagnostic.Location!, out DefinitionPath correction)).IsTrue();
        await Assert.That(correction.Equals(value)).IsTrue();
        await Assert.That(form.FocusCorrection(diagnostic.FieldId, diagnostic.Location!)).IsTrue();
        await Assert.That(form.DiscoveryQuery).IsEqualTo(string.Empty);
        await Assert.That(form.IsEditorVisible(value)).IsTrue();
        await Assert.That(form.HasInputError(value)).IsTrue();
        await Assert.That(form.ReadDraftText(value)).IsEqualTo("invalid");
        await Assert.That(form.EditValue(value, "4")).IsTrue();
        await Assert.That(form.FocusCorrection(diagnostic.FieldId, diagnostic.Location!)).IsFalse();
        form.SelectVariant(root, 1);
        form.SelectVariant(root, 0);
        await Assert.That(form.GetEditorIdentity(value)).IsNotEqualTo(identity);
        await Assert.That(form.ReadDraftText(value)).IsNull();
    }

    [Test]
    public async Task TypedAliasedMapListRetainsRowsDraftsOriginsAndOmissions()
    {
        Definition definition = DefinitionSourceCodec.ParseJson("""
            {"$schema":"urn:islamu:configurator:authoring:v0.0.1",
             "identity":{"familyId":"198d708d-caba-4e48-a00d-b06242151624","revision":1},
             "project":{"id":"root-alias-rows","name":"Literal rows"},"applicationVersions":["1"],
             "configuration":{"type":"object","x-configurator":{"sensitivity":"public"},
               "properties":{"literal/key":{"type":"string","x-configurator":{"sensitivity":"public"}}},
               "additionalProperties":{"type":"array","x-configurator":{"sensitivity":"public"},
                 "items":{"type":"object","additionalProperties":false,"x-configurator":{"sensitivity":"public"},
                   "properties":{"count":{"type":"integer","x-configurator":{"sensitivity":"public"}},
                     "optional":{"type":"integer","x-configurator":{"sensitivity":"public","initialValue":4}},
                     "nested":{"type":"array","items":{"type":"integer","x-configurator":{"sensitivity":"public"}},
                       "x-configurator":{"sensitivity":"public"}}}}}},
             "outputs":{"config.json":{"id":"config","format":"json"}}}
            """u8.ToArray());
        using var form = new DefinitionFormModel(definition);
        string key = form.Fields.Single().Definition.Id;
        DefinitionPath root = DefinitionPath.FromSegments([]);
        DefinitionPath rows = DefinitionPath.FromSegments([key]);
        DefinitionPath survivorCount = DefinitionPath.FromSegments([key, "1", "count"]);
        await Assert.That(form.GetListItemMetadata(rows).Type).IsEqualTo(DefinitionNodeType.Mapping);
        var imported = new Dictionary<string, JsonElement>
        {
            ["literal/key"] = JsonSerializer.SerializeToElement("untouched"),
            [key] = JsonSerializer.Deserialize<JsonElement>("""[{"count":1},{"count":2}]""")
        };
        await Assert.That((await form.ImportAsync("config", _ =>
            Task.FromResult<ReadOnlyMemory<byte>>(JsonSerializer.SerializeToUtf8Bytes(imported)))).Succeeded).IsTrue();
        await Assert.That(form.AddMapEntry(root, key)).IsFalse();
        await Assert.That(form.AddMapEntry(root, "new/entry")).IsTrue();
        string survivor = form.GetEditorIdentity(survivorCount);
        await Assert.That(form.EditValue(DefinitionPath.FromSegments([key, "1", "optional"]), null)).IsTrue();
        await Assert.That(form.EditValue(survivorCount, "invalid")).IsFalse();
        await Assert.That(form.AddListItem(rows)).IsTrue();
        await Assert.That(form.RemoveListItem(rows, 0)).IsTrue();
        DefinitionPath count = DefinitionPath.FromSegments([key, "0", "count"]);
        await Assert.That(form.GetEditorIdentity(count)).IsEqualTo(survivor);
        await Assert.That(form.ReadDraftText(count)).IsEqualTo("invalid");
        await Assert.That(form.HasInputError(count)).IsTrue();
        await Assert.That(form.AnswerOrigin(count)).IsEqualTo(DefinitionAnswerOrigin.Imported);
        await Assert.That(form.Snapshot().Omitted.Contains(DefinitionPath.FromSegments([key, "0", "optional"]))).IsTrue();
        await Assert.That(form.GetEditorState(count)!.IsProvided).IsTrue();
        await Assert.That(form.Validate().TryGetEffectiveValue(count, out JsonElement retained)).IsTrue();
        await Assert.That(retained.GetInt32()).IsEqualTo(2);
        DefinitionDiagnostic diagnostic = form.InputDiagnostics.Single();
        await Assert.That(form.TryResolveCorrectionPath(diagnostic.FieldId, diagnostic.Location!, out DefinitionPath correction)).IsTrue();
        await Assert.That(correction.Equals(count)).IsTrue();
        await Assert.That(form.FocusCorrection(diagnostic.FieldId, diagnostic.Location!)).IsTrue();
        await Assert.That(form.IsEditorVisible(count)).IsTrue();
        DefinitionPath nested = DefinitionPath.FromSegments([key, "0", "nested"]);
        await Assert.That(form.SupplyList(nested)).IsTrue();
        await Assert.That(form.AddListItem(nested)).IsTrue();
        await Assert.That(form.EditValue(DefinitionPath.FromSegments([key, "0", "nested", "0"]), "7")).IsTrue();
        await Assert.That(form.Snapshot().Values[key][0].GetProperty("nested")[0].GetInt32()).IsEqualTo(7);
        await Assert.That(form.Snapshot().Values["literal/key"].GetString()).IsEqualTo("untouched");
    }

    [Test]
    public async Task TypedMapChoiceSuggestionAndNullNeverAddressAnOrdinaryAlias()
    {
        using var form = new DefinitionFormModel(DefinitionSourceCodec.ParseJson("""
            {"$schema":"urn:islamu:configurator:authoring:v0.0.1",
             "identity":{"familyId":"198d708d-caba-4e48-a00d-b06242151625","revision":1},
             "project":{"id":"root-alias-choice","name":"Literal choices"},"applicationVersions":["1"],
             "configuration":{"type":"object","x-configurator":{"sensitivity":"public"},
               "properties":{"literal/key":{"type":"string","readOnly":true,"x-configurator":{"sensitivity":"public"}}},
               "additionalProperties":{"type":["integer","null"],"enum":[1,4,null],
                 "x-configurator":{"sensitivity":"public","suggestedValue":4}}},
             "outputs":{"config.json":{"id":"config","format":"json"}}}
            """u8.ToArray()));
        string key = form.Fields.Single(field => field.Definition.Name == "literal/key").Definition.Id;
        DefinitionPath path = DefinitionPath.FromSegments([key]);
        form.EditValue(DefinitionPath.Parse("/literal~1key"), "untouched");
        await Assert.That(form.GetEditorPresentation(path)!.Choices.Count).IsEqualTo(3);
        await Assert.That(form.SelectChoice(path, 0)).IsTrue();
        await Assert.That(form.CanEdit(path)).IsTrue();
        await Assert.That(form.CanEdit(DefinitionPath.Parse("/literal~1key"))).IsFalse();
        await Assert.That(form.Snapshot().Values[key].GetInt32()).IsEqualTo(1);
        await Assert.That(form.ApplySuggestion(path)).IsTrue();
        await Assert.That(form.Snapshot().Values[key].GetInt32()).IsEqualTo(4);
        await Assert.That(form.SetNull(path)).IsTrue();
        await Assert.That(form.Snapshot().Values[key].ValueKind).IsEqualTo(JsonValueKind.Null);
        await Assert.That(form.Snapshot().Values["literal/key"].GetString()).IsEqualTo("untouched");
    }

    [Test]
    public async Task RootMapEditsRetainDeclaredAndAdditionalValues()
    {
        using var form = new DefinitionFormModel(Map());
        form.EditValue(["anchor"], "kept");
        await Assert.That(form.AddMapEntry([], "entry")).IsTrue();
        await Assert.That(form.EditValue(["entry"], "7")).IsTrue();
        await Assert.That(form.Snapshot().Values["anchor"].GetString()).IsEqualTo("kept");
        await Assert.That(form.Snapshot().Values["entry"].GetInt32()).IsEqualTo(7);
    }

    [Test]
    public async Task RootVariantSelectionAllowsTypedChildCorrection()
    {
        using var form = new DefinitionFormModel(Variant());
        await Assert.That(form.SelectVariant([], 0)).IsTrue();
        await Assert.That(form.EditValue(["value"], "3")).IsTrue();
        await Assert.That(form.Snapshot().Values["kind"].GetString()).IsEqualTo("first");
        await Assert.That(form.Snapshot().Values["value"].GetInt32()).IsEqualTo(3);
    }

    [Test]
    public async Task RootMapImportAndDocumentAdoptionShareAcceptedOwnership()
    {
        using var form = new DefinitionFormModel(Map());
        DefinitionFieldInput anchor = form.Fields.Single();
        await Assert.That((await form.ImportAsync("config", _ =>
            Task.FromResult<ReadOnlyMemory<byte>>("""{"anchor":"kept","entry":7}"""u8.ToArray()))).Succeeded).IsTrue();
        await Assert.That(form.AnswerOrigin(["entry"])).IsEqualTo(DefinitionAnswerOrigin.Imported);
        using var document = form.OpenDocument();
        using var parsed = document.Parse(
            """{"answerContractVersion":"0.0.1","values":{"anchor":"kept","entry":9},"omitted":[]}"""u8.ToArray());
        await Assert.That(parsed.Succeeded).IsTrue();
        long before = form.InputRevision;
        int changes = 0;
        form.InputChanged += (_, _) => changes++;
        await Assert.That(document.Apply(parsed.Candidate!).Succeeded).IsTrue();
        await Assert.That(form.InputRevision).IsEqualTo(before + 1);
        await Assert.That(changes).IsEqualTo(1);
        await Assert.That(ReferenceEquals(form.Fields.Single(), anchor)).IsTrue();
        await Assert.That(anchor.Text).IsEqualTo("kept");
        await Assert.That(form.Snapshot().Values["entry"].GetInt32()).IsEqualTo(9);
    }

    [Test]
    public async Task RootVariantDocumentCanSelectAnotherBranchWithoutRetainingOldPayload()
    {
        using var form = new DefinitionFormModel(Variant());
        form.SelectVariant([], 0);
        form.EditValue(["value"], "7");
        string original = form.GetEditorIdentity(["kind"]);
        using var document = form.OpenDocument();
        using var parsed = document.Parse(
            """{"answerContractVersion":"0.0.1","values":{"kind":"second","other":"new"},"omitted":[]}"""u8.ToArray());
        await Assert.That(parsed.Succeeded).IsTrue();
        await Assert.That(document.Apply(parsed.Candidate!).Succeeded).IsTrue();
        await Assert.That(form.Snapshot().Values.ContainsKey("value")).IsFalse();
        await Assert.That(form.Snapshot().Values["other"].GetString()).IsEqualTo("new");
        await Assert.That(form.GetEditorIdentity(["kind"])).IsNotEqualTo(original);
    }

    [Test]
    public async Task InitialRootVariantDocumentRejectsProtectedSuppliedValuesWithoutAdoption()
    {
        JsonObject source = JsonNode.Parse(NestedVariant().CanonicalBytes.Span)!.AsObject();
        source["configuration"] = source["configuration"]!["properties"]!["provider"]!.DeepClone();
        using var form = new DefinitionFormModel(DefinitionCodec.ParseJson(JsonSerializer.SerializeToUtf8Bytes(source)));
        using var document = form.OpenDocument();
        using var parsed = document.Parse(
            """{"answerContractVersion":"0.0.1","values":{"kind":"first","password":"synthetic-private-value"},"omitted":[]}"""u8.ToArray());
        await Assert.That(parsed.Succeeded).IsFalse();
        await Assert.That(form.InputRevision).IsEqualTo(0);
        await Assert.That(form.Snapshot().Values.Count).IsEqualTo(0);
        await Assert.That(JsonSerializer.Serialize(parsed.Diagnostics)).DoesNotContain("synthetic-private-value");
    }

    [Test]
    public async Task RootMapInvalidDraftReportsOnlyTypedCoordinatesAndRetainsAcceptedValue()
    {
        using var form = new DefinitionFormModel(Map());
        form.SetValue(["synthetic-private-key"], JsonSerializer.SerializeToElement(7));
        await Assert.That(form.EditValue(["synthetic-private-key"], "invalid")).IsFalse();
        await Assert.That(form.HasInputErrors).IsTrue();
        await Assert.That(form.Snapshot().Values["synthetic-private-key"].GetInt32()).IsEqualTo(7);
        await Assert.That(JsonSerializer.Serialize(form.InputDiagnostics)).DoesNotContain("synthetic-private-key");
        await Assert.That(form.EditValue(["synthetic-private-key"], "9")).IsTrue();
        await Assert.That(form.HasInputErrors).IsFalse();
    }

    [Test]
    public async Task RootMapRowsKeepSurvivorIdentityDraftAndOriginAcrossRemoval()
    {
        using var form = new DefinitionFormModel(DefinitionSourceCodec.ParseJson("""
            {"$schema":"urn:islamu:configurator:authoring:v0.0.1",
             "identity":{"familyId":"198d708d-caba-4e48-a00d-b06242151622","revision":1},
             "project":{"id":"root-rows","name":"Root rows"},"applicationVersions":["1"],
             "configuration":{"type":"object","x-configurator":{"sensitivity":"public"},
               "additionalProperties":{"type":"array","x-configurator":{"sensitivity":"public"},
                 "items":{"type":"integer","x-configurator":{"sensitivity":"public"}}}},
             "outputs":{"config.json":{"id":"config","format":"json"}}}
            """u8.ToArray()));
        form.SetValue(["entry"], JsonSerializer.SerializeToElement(RootRows));
        string survivor = form.GetEditorIdentity(["entry", "1"]);
        form.EditValue(["entry", "1"], "invalid");
        await Assert.That(form.RemoveListItem(["entry"], 0)).IsTrue();
        await Assert.That(form.GetEditorIdentity(["entry", "0"])).IsEqualTo(survivor);
        await Assert.That(form.ReadDraftText(["entry", "0"])).IsEqualTo("invalid");
        await Assert.That(form.Snapshot().Values["entry"][0].GetInt32()).IsEqualTo(2);
    }

    [Test]
    public async Task OrdinaryFacadeReplacementRetiresReusedBranchCoordinates()
    {
        using var form = new DefinitionFormModel(NestedVariant());
        DefinitionFieldInput provider = form.Fields.Single();
        provider.Text = """{"kind":"first","value":7}""";
        string original = form.GetEditorIdentity(["provider", "value"]);
        provider.Text = """{"kind":"second","value":8}""";
        provider.Text = """{"kind":"first","value":9}""";
        await Assert.That(form.GetEditorIdentity(["provider", "value"])).IsNotEqualTo(original);
        await Assert.That(form.Snapshot().Values["provider"].GetProperty("value").GetInt32()).IsEqualTo(9);
    }

    [Test]
    [Arguments("literal/slash")]
    [Arguments("literal~tilde")]
    [Arguments("0")]
    public async Task RootMapLiteralKeysRespectBoundsAndRemoveWithoutPartialAdoption(string key)
    {
        JsonObject source = JsonNode.Parse(Map().CanonicalBytes.Span)!.AsObject();
        source["configuration"]!["maxProperties"] = 2;
        source["requiredCapabilities"] = JsonSerializer.SerializeToNode(source["requiredCapabilities"]!.AsArray()
            .Select(value => value!.GetValue<string>()).Append("schema.bounds").Order(StringComparer.Ordinal));
        using var form = new DefinitionFormModel(DefinitionCodec.ParseJson(JsonSerializer.SerializeToUtf8Bytes(source)));
        form.EditValue(["anchor"], "kept");
        await Assert.That(form.AddMapEntry([], key)).IsTrue();
        long revision = form.InputRevision;
        await Assert.That(form.AddMapEntry([], key)).IsFalse();
        await Assert.That(form.AddMapEntry([], "overflow")).IsFalse();
        await Assert.That(form.InputRevision).IsEqualTo(revision);
        await Assert.That(form.EditValue([key], "7")).IsTrue();
        await Assert.That(form.Snapshot().Values[key].GetInt32()).IsEqualTo(7);
        await Assert.That(form.EditValue([key], null)).IsTrue();
        using DefinitionArtifact artifact = DefinitionArtifactComposer.Compose(form.Definition, "config", form.Snapshot()).Artifact!;
        await Assert.That(artifact.Bytes.Span.SequenceEqual("""{"anchor":"kept"}"""u8)).IsTrue();
    }

    [Test]
    public async Task RootMapPublicDocumentPreservesUnrepresentedEntriesAndProtectedProperties()
    {
        JsonObject source = JsonNode.Parse(Map().CanonicalBytes.Span)!.AsObject();
        source["configuration"]!["properties"]!["password"] = JsonNode.Parse(
            """{"type":"string","writeOnly":true}""");
        using var form = new DefinitionFormModel(DefinitionCodec.ParseJson(JsonSerializer.SerializeToUtf8Bytes(source)));
        form.EditValue(["anchor"], "before");
        form.EditValue(["password"], "synthetic-retained-secret");
        form.EditValue(["entry"], "7");
        using var document = form.OpenDocument();
        using var parsed = document.Parse(
            """{"answerContractVersion":"0.0.1","values":{"anchor":"after"},"omitted":[]}"""u8.ToArray());
        await Assert.That(parsed.Succeeded).IsTrue();
        await Assert.That(document.Apply(parsed.Candidate!).Succeeded).IsTrue();
        await Assert.That(form.Snapshot().Values["anchor"].GetString()).IsEqualTo("after");
        await Assert.That(form.Snapshot().Values["entry"].GetInt32()).IsEqualTo(7);
        await Assert.That(form.Snapshot().Values["password"].GetString()).IsEqualTo("synthetic-retained-secret");
    }

    [Test]
    public async Task LiteralRootEntryCannotAliasADeclaredPropertiesGeneratedIdentifier()
    {
        JsonObject source = JsonNode.Parse(Map().CanonicalBytes.Span)!.AsObject();
        source["configuration"]!["properties"]!["literal/key"] = JsonNode.Parse(
            """{"type":"integer","x-configurator":{"sensitivity":"public"}}""");
        using var form = new DefinitionFormModel(DefinitionCodec.ParseJson(JsonSerializer.SerializeToUtf8Bytes(source)));
        string generated = form.Fields.Single(field => field.Definition.Name == "literal/key").Definition.Id;
        DefinitionPath entry = DefinitionPath.FromSegments([generated]);
        DefinitionPath property = DefinitionPath.Parse("/literal~1key");
        await Assert.That(form.AddMapEntry([], generated)).IsTrue();
        await Assert.That(form.EditValue(entry, "7")).IsTrue();
        await Assert.That(form.EditValue(property, "9")).IsTrue();
        await Assert.That(form.Snapshot().Values[generated].GetInt32()).IsEqualTo(7);
        await Assert.That(form.Snapshot().Values["literal/key"].GetInt32()).IsEqualTo(9);
        await Assert.That(form.GetEditorIdentity(entry)).IsNotEqualTo(form.GetEditorIdentity(property));
        await Assert.That(form.EditValue(entry, null)).IsTrue();
        await Assert.That(form.Snapshot().Omitted.Contains(entry)).IsTrue();
        await Assert.That(form.Snapshot().Omitted.Contains(property)).IsFalse();
        await Assert.That(form.Snapshot().Values["literal/key"].GetInt32()).IsEqualTo(9);
    }

    [Test]
    public async Task ExplicitRootMapDescendantClearsAncestorOmission()
    {
        JsonObject source = JsonNode.Parse(Map().CanonicalBytes.Span)!.AsObject();
        source["configuration"]!["additionalProperties"] = JsonNode.Parse("""
            {"type":"object","additionalProperties":false,"x-configurator":{"sensitivity":"public"},
              "properties":{"count":{"type":"integer","x-configurator":{"sensitivity":"public"}}}}
            """);
        using var form = new DefinitionFormModel(DefinitionCodec.ParseJson(JsonSerializer.SerializeToUtf8Bytes(source)));
        DefinitionPath entry = DefinitionPath.Parse("/entry");
        DefinitionPath count = DefinitionPath.Parse("/entry/count");
        form.EditValue(count, "7");
        form.EditValue(entry, null);
        await Assert.That(form.EditValue(count, "9")).IsTrue();
        await Assert.That(form.Snapshot().Omitted.Contains(entry)).IsFalse();
        await Assert.That(form.Validate().TryGetEffectiveValue(count, out JsonElement effective)).IsTrue();
        await Assert.That(effective.GetInt32()).IsEqualTo(9);
        await Assert.That(form.AnswerOrigin(entry)).IsEqualTo(DefinitionAnswerOrigin.Explicit);
    }

    [Test]
    public async Task RootMapSecretConsentRevocationClearsOnlySecretLeaves()
    {
        JsonObject source = JsonNode.Parse(Map().CanonicalBytes.Span)!.AsObject();
        source["configuration"]!["additionalProperties"] = JsonNode.Parse(
            """{"type":"string","writeOnly":true,"x-configurator":{"sensitivity":"secret"}}""");
        JsonArray capabilities = source["requiredCapabilities"]!.AsArray();
        capabilities.RemoveAt(capabilities.Select((value, index) => (value, index))
            .Single(item => item.value!.GetValue<string>() == "schema.integer").index);
        using var form = new DefinitionFormModel(DefinitionCodec.ParseJson(JsonSerializer.SerializeToUtf8Bytes(source)),
            DefinitionSessionPrivacy.BrowserSecretsEnabled);
        form.EditValue(["anchor"], "safe");
        await Assert.That(form.EditValue(["private-entry"], "synthetic-secret")).IsTrue();
        form.SetSecretEntryEnabled(false);
        await Assert.That(form.Snapshot().Values.ContainsKey("private-entry")).IsFalse();
        await Assert.That(form.Snapshot().Values["anchor"].GetString()).IsEqualTo("safe");
        form.SetSecretEntryEnabled(true);
        await Assert.That(form.Snapshot().Values.ContainsKey("private-entry")).IsFalse();
    }

    [Test]
    public async Task RootMapSecretGenerationUsesLiteralCoordinatesAndRetiresOnRevocation()
    {
        Definition definition = DefinitionSourceCodec.ParseJson("""
            {"$schema":"urn:islamu:configurator:authoring:v0.0.1",
             "identity":{"familyId":"198d708d-caba-4e48-a00d-b06242151623","revision":1},
             "project":{"id":"root-secret","name":"Root secret"},"applicationVersions":["1"],
             "configuration":{"type":"object","x-configurator":{"sensitivity":"public"},
               "properties":{"literal/key":{"type":"string","x-configurator":{"sensitivity":"public"}}},
               "additionalProperties":{"type":"string","x-configurator":{"secret":{
                 "kind":"password","acquisition":"local","generation":{"length":24}}}}},
             "outputs":{"config.json":{"id":"config","format":"json"}}}
            """u8.ToArray());
        using var form = new DefinitionFormModel(definition, DefinitionSessionPrivacy.BrowserSecretsEnabled);
        string key = form.Fields.Single().Definition.Id;
        DefinitionPath path = DefinitionPath.FromSegments([key]);
        form.EditValue(DefinitionPath.Parse("/literal~1key"), "untouched");
        form.AddMapEntry(DefinitionPath.FromSegments([]), key);
        await Assert.That(form.CanGenerateSecret(path)).IsTrue();
        await Assert.That(form.GetSecretValidation(path, showErrors: true).IsActive).IsTrue();
        await Assert.That((await form.GenerateSecretAsync(path)).Succeeded).IsTrue();
        await Assert.That(form.Snapshot().Values[key].GetString()!.Length).IsEqualTo(24);
        await Assert.That(form.AnswerOrigin(path)).IsEqualTo(DefinitionAnswerOrigin.Generated);
        form.SetSecretEntryEnabled(false);
        await Assert.That(form.Snapshot().Values.ContainsKey(key)).IsFalse();
        await Assert.That(form.GetSecretValidation(path).IsDeferred).IsTrue();
        await Assert.That(form.Snapshot().Values["literal/key"].GetString()).IsEqualTo("untouched");
        await Assert.That(form.CanGenerateSecret(path)).IsFalse();
    }

    [Test]
    public async Task ReusedCoordinatesCannotAuthorizeRetiredRootCompletion()
    {
        using var form = new DefinitionFormModel(NestedVariant());
        form.SelectVariant(["provider"], 0);
        string original = form.GetEditorIdentity(["provider", "value"]);
        var release = new TaskCompletionSource<ReadOnlyMemory<byte>>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<DefinitionImportResult> pending = form.ImportAsync("config", _ => release.Task);
        form.SelectVariant(["provider"], 1);
        form.SelectVariant(["provider"], 0);
        release.SetResult("""{"provider":{"kind":"first","value":91}}"""u8.ToArray());
        await Assert.That((await pending.WaitAsync(TimeSpan.FromSeconds(10))).Succeeded).IsFalse();
        await Assert.That(form.GetEditorIdentity(["provider", "value"])).IsNotEqualTo(original);
        await Assert.That(form.Snapshot().Values["provider"].TryGetProperty("value", out _)).IsFalse();
    }

    [Test]
    public async Task RetiredBranchCompletionCannotRestoreSecretsOrPreparedOutput()
    {
        using var form = new DefinitionFormModel(NestedVariant(), DefinitionSessionPrivacy.BrowserSecretsEnabled);
        form.SelectVariant(["provider"], 0);
        JsonObject supplied = JsonNode.Parse(form.Snapshot().Values["provider"].GetRawText())!.AsObject();
        supplied["password"] = "synthetic-retired-secret";
        form.SetValue(["provider"], JsonSerializer.SerializeToElement(supplied));
        using DefinitionArtifact artifact = DefinitionArtifactComposer.Compose(form.Definition, "config", form.Snapshot()).Artifact!;
        using DefinitionPreparedAdoption adoption = DefinitionPresentationOperation.Adopt(artifact);
        using PresentationOutcome outcome = await adoption.ExecuteAsync(CancellationToken.None);
        ReadOnlyMemory<byte> retiredBytes = outcome.Output;
        var completion = new TaskCompletionSource<PresentationOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
        var discarded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var presentation = new PresentationSession(new WeakReferenceMessenger());
        WorkspaceId.TryCreate("retired-root", out WorkspaceId id);
        using PresentationWorkspace workspace = presentation.CreateWorkspace(id, new HeldOperation(completion.Task));
        workspace.CompletionDiscarded += (_, _) => discarded.TrySetResult();
        form.InputChanged += (_, _) => { workspace.Deactivate(); workspace.Activate(); };
        workspace.Activate();
        Task execution = workspace.ExecuteAsync(Guid.NewGuid());
        using var document = form.OpenDocument();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<DefinitionDocumentParseResult> pending = document.ParseAsync(_ =>
        {
            entered.SetResult();
            return release.Task;
        });
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        string original = form.GetEditorIdentity(["provider", "value"]);
        form.SelectVariant(["provider"], 1);
        form.SelectVariant(["provider"], 0);
        form.SetSecretEntryEnabled(false);
        byte[] bytes = """{"answerContractVersion":"0.0.1","values":{"provider":{"kind":"first","value":91}},"omitted":[]}"""u8.ToArray();
        release.SetResult(bytes);
        completion.SetResult(outcome);
        await execution.WaitAsync(TimeSpan.FromSeconds(10));
        await discarded.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await workspace.WaitForPendingOperationsAsync().WaitAsync(TimeSpan.FromSeconds(10));
        using DefinitionDocumentParseResult parsed = await pending.WaitAsync(TimeSpan.FromSeconds(10));
        await Assert.That(parsed.Succeeded).IsFalse();
        await Assert.That(bytes.All(value => value == 0)).IsTrue();
        await Assert.That(form.GetEditorIdentity(["provider", "value"])).IsNotEqualTo(original);
        await Assert.That(form.CopyGeneratedPassword()).IsNull();
        await Assert.That(form.Snapshot().Values["provider"].TryGetProperty("value", out _)).IsFalse();
        await Assert.That(form.Snapshot().Values["provider"].TryGetProperty("password", out _)).IsFalse();
        await Assert.That(workspace.Output.IsEmpty).IsTrue();
        await Assert.That(workspace.Result).IsNull();
        await Assert.That(retiredBytes.ToArray().All(value => value == 0)).IsTrue();
    }

    internal static Definition Map() => DefinitionSourceCodec.ParseJson("""
        {"$schema":"urn:islamu:configurator:authoring:v0.0.1",
         "identity":{"familyId":"198d708d-caba-4e48-a00d-b06242151617","revision":1},
         "project":{"id":"root-map","name":"Root map"},"applicationVersions":["1"],
         "configuration":{"type":"object","x-configurator":{"sensitivity":"public"},
           "properties":{"anchor":{"type":"string","x-configurator":{"sensitivity":"public"}}},
           "additionalProperties":{"type":"integer","x-configurator":{"sensitivity":"public"}}},
         "outputs":{"config.json":{"id":"config","format":"json",
           "projection":{"op":"copy","reference":{"scope":"root","path":""}}}}}
        """u8.ToArray());

    internal static Definition Variant() => DefinitionSourceCodec.ParseJson("""
        {"$schema":"urn:islamu:configurator:authoring:v0.0.1",
         "identity":{"familyId":"198d708d-caba-4e48-a00d-b06242151618","revision":1},
         "project":{"id":"root-variant","name":"Root variant"},"applicationVersions":["1"],
         "configuration":{"type":"object","x-configurator":{"sensitivity":"public"},"oneOf":[
           {"type":"object","additionalProperties":false,"required":["kind"],"x-configurator":{"sensitivity":"public"},
             "properties":{"kind":{"type":"string","const":"first","x-configurator":{"sensitivity":"public"}},"value":{"type":"integer","x-configurator":{"sensitivity":"public"}}}},
           {"type":"object","additionalProperties":false,"required":["kind"],"x-configurator":{"sensitivity":"public"},
             "properties":{"kind":{"type":"string","const":"second","x-configurator":{"sensitivity":"public"}},"other":{"type":"string","x-configurator":{"sensitivity":"public"}}}}]},
         "outputs":{"config.json":{"id":"config","format":"json"}}}
        """u8.ToArray());

    private static Definition NestedVariant() => DefinitionSourceCodec.ParseJson("""
        {"$schema":"urn:islamu:configurator:authoring:v0.0.1",
         "identity":{"familyId":"198d708d-caba-4e48-a00d-b06242151619","revision":1},
         "project":{"id":"nested-variant","name":"Nested variant"},"applicationVersions":["1"],
         "configuration":{"type":"object","additionalProperties":false,"x-configurator":{"sensitivity":"public"},
           "properties":{"provider":{"type":"object","x-configurator":{"sensitivity":"public"},"oneOf":[
             {"type":"object","additionalProperties":false,"required":["kind"],"x-configurator":{"sensitivity":"public"},
               "properties":{"kind":{"type":"string","const":"first","x-configurator":{"sensitivity":"public"}},"value":{"type":"integer"},"password":{"type":"string","writeOnly":true}}},
             {"type":"object","additionalProperties":false,"required":["kind"],"x-configurator":{"sensitivity":"public"},
               "properties":{"kind":{"type":"string","const":"second","x-configurator":{"sensitivity":"public"}},"value":{"type":"integer"},"password":{"type":"string","writeOnly":true}}}]}}},
         "outputs":{"config.json":{"id":"config","format":"json"}}}
        """u8.ToArray());

    private sealed class HeldOperation(Task<PresentationOutcome> completion) : IPresentationOperation
    {
        public Task<PresentationOutcome> ExecuteAsync(CancellationToken cancellationToken) => completion;
    }
}
