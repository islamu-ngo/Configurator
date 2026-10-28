namespace Configurator.Presentation.Tests;

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Configurator.Core.Artifacts;
using Configurator.Core.Definitions;

public sealed class DefinitionCollectionEditingTests
{
    [Test]
    public async Task ViewNotificationsPreserveCurrentEditingIdentityAndDrafts()
    {
        using var form = new DefinitionFormModel(Definition());
        form.AddListItem(["rows"]);
        form.EditValue(["rows", "0", "metadata", "name"], "ordinary");
        form.EditValue(["rows", "0", "settings", "enabled"], "invalid");
        string identity = form.GetEditorIdentity(["rows", "0", "settings", "enabled"]);
        long revision = form.InputRevision;
        int inputs = 0;
        int views = 0;
        form.InputChanged += (_, _) => inputs++;
        form.ViewChanged += (_, _) =>
        {
            views++;
            if (form.InputRevision != revision || !form.HasInputErrors ||
                form.ReadDraftText(["rows", "0", "settings", "enabled"]) != "invalid")
                throw new InvalidOperationException("incoherent-view-transition");
            _ = Task.Run(() => form.Projection).WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
        };

        form.SetDiscovery("settings");
        form.SetEditorExpanded(["rows", "0"], true);
        form.SetDisclosureOverride(SetupFieldDisclosure.Advanced);

        await Assert.That(views).IsEqualTo(3);
        await Assert.That(inputs).IsEqualTo(0);
        await Assert.That(form.InputRevision).IsEqualTo(revision);
        await Assert.That(form.GetEditorIdentity(["rows", "0", "settings", "enabled"])).IsEqualTo(identity);
        await Assert.That(form.AnswerOrigin(["rows", "0", "metadata", "name"]))
            .IsEqualTo(DefinitionAnswerOrigin.Explicit);
        await Assert.That(form.Snapshot().Values["rows"][0].GetProperty("metadata")
            .GetProperty("name").GetString()).IsEqualTo("ordinary");
    }

    [Test]
    public async Task ListItemMetadataContainsDeclaredShapeButNeverRowAnswers()
    {
        using var form = new DefinitionFormModel(Definition(), DefinitionSessionPrivacy.BrowserSecretsEnabled);
        long revision = form.InputRevision;
        DefinitionNode metadata = form.GetListItemMetadata(["rows"]);
        await Assert.That(form.InputRevision).IsEqualTo(revision);
        await Assert.That(metadata.Type).IsEqualTo(DefinitionNodeType.Mapping);
        await Assert.That(metadata.Properties.ContainsKey("password")).IsTrue();
        await Assert.That(metadata.Properties["password"].Keywords.GetProperty("x-configurator").GetProperty("sensitivity").GetString()).IsEqualTo("secret");

        form.AddListItem(["rows"]);
        form.EditValue(["rows", "0", "password"], "synthetic-item-answer");
        await Assert.That(form.Snapshot().Values["rows"][0].GetProperty("password").GetString()).IsEqualTo("synthetic-item-answer");
        await Assert.That(JsonSerializer.Serialize(form.GetListItemMetadata(["rows"]))).DoesNotContain("synthetic-item-answer");
        form.RemoveListItem(["rows"], 0);
        await Assert.That(metadata.Equals(form.GetListItemMetadata(["rows"]))).IsTrue();
        await Assert.That(() => form.GetListItemMetadata(["rows", "0"])).Throws<ArgumentException>();
    }

    [Test]
    public async Task InvalidRootDeniesDescendantReadsInsteadOfReusingLastValidRows()
    {
        using var form = new DefinitionFormModel(Definition());
        form.AddListItem(["rows"]);
        form.EditValue(["rows", "0", "metadata", "name"], "ordinary");
        _ = form.Projection;
        _ = form.GetEditorIdentity(["rows", "0", "metadata", "name"]);
        form.Fields[0].Text = "not-json";

        await Assert.That(form.AnswerOrigin(["rows", "0", "metadata", "name"]))
            .IsEqualTo(DefinitionAnswerOrigin.Omitted);
        await Assert.That(form.IsEditorVisible(["rows", "0", "metadata", "name"])).IsFalse();
        await Assert.That(form.EditValue(["rows", "0", "metadata", "name"], "late")).IsFalse();
        await Assert.That(form.ReadDraftText(["rows", "0", "metadata", "name"])).IsNull();
    }

    [Test]
    public async Task LiteralDottedChildrenRemainDistinctAndSurvivingRowTokensStayStable()
    {
        JsonObject source = JsonNode.Parse(Definition().CanonicalBytes.Span)!.AsObject();
        JsonObject metadata = source["configuration"]!["properties"]!["rows"]!["items"]!["properties"]!["metadata"]!.AsObject();
        JsonObject properties = metadata["properties"]!.AsObject();
        JsonNode name = properties["name"]!;
        properties.Remove("name");
        properties["literal.name"] = name;
        metadata["required"] = new JsonArray("literal.name");
        using var form = new DefinitionFormModel(
            SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString())));
        form.AddListItem(["rows"]);
        form.AddListItem(["rows"]);
        form.EditValue(["rows", "1", "metadata", "literal.name"], "ordinary");
        string identity = form.GetEditorIdentity(["rows", "1", "metadata", "literal.name"]);
        _ = form.Projection;
        form.SetDiscovery("literal.name");
        form.RemoveListItem(["rows"], 0);

        await Assert.That(form.GetEditorIdentity(["rows", "0", "metadata", "literal.name"])).IsEqualTo(identity);
        await Assert.That(form.Snapshot().Values["rows"][0].GetProperty("metadata")
            .GetProperty("literal.name").GetString()).IsEqualTo("ordinary");
        await Assert.That(form.FocusEditor(["rows", "0", "metadata", "literal", "name"])).IsFalse();
        await Assert.That(form.FocusEditor(["rows", "0", "metadata", "literal.name"])).IsTrue();
        await Assert.That(form.AnswerOrigin(["rows", "0", "metadata", "literal.name"]))
            .IsEqualTo(DefinitionAnswerOrigin.Explicit);
    }

    [Test]
    public async Task MaterializedDefaultsAndImportedRowsKeepOriginsAcrossCollectionMutations()
    {
        using var form = new DefinitionFormModel(Definition());
        form.AddListItem(["rows"]);
        form.SupplyObject(["rows", "0", "settings"]);
        await Assert.That(form.AnswerOrigin(["rows", "0", "settings", "enabled"]))
            .IsEqualTo(DefinitionAnswerOrigin.Defaulted);
        form.EditValue(["rows", "0", "settings", "enabled"], "true");
        await Assert.That(form.AnswerOrigin(["rows", "0", "settings", "enabled"]))
            .IsEqualTo(DefinitionAnswerOrigin.Explicit);
        form.ResetValue(["rows"]);
        await form.ImportAsync("config", _ => Task.FromResult<ReadOnlyMemory<byte>>(
            """{"rows":[{"metadata":{"name":"imported"},"settings":{"enabled":true}}]}"""u8.ToArray()));
        await Assert.That(form.AnswerOrigin(["rows", "0", "metadata", "name"]))
            .IsEqualTo(DefinitionAnswerOrigin.Imported);
        form.AddListItem(["rows"]);
        form.RemoveListItem(["rows"], 1);
        await Assert.That(form.AnswerOrigin(["rows", "0", "metadata", "name"]))
            .IsEqualTo(DefinitionAnswerOrigin.Imported);
    }

    [Test]
    public async Task CorrectionDisclosureFollowsSurvivingRowInsteadOfItsOldIndex()
    {
        JsonObject source = JsonNode.Parse(Definition().CanonicalBytes.Span)!.AsObject();
        JsonObject metadata = source["configuration"]!["properties"]!["rows"]!["items"]!["properties"]!["metadata"]!.AsObject();
        metadata["required"] = new JsonArray();
        metadata["properties"]!["name"]!["x-configurator"]!["detail"] = "advanced";
        using var form = new DefinitionFormModel(
            SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString())));
        form.AddListItem(["rows"]);
        form.AddListItem(["rows"]);
        await Assert.That(form.IsEditorVisible(["rows", "1", "metadata", "name"])).IsFalse();
        form.FocusEditor(["rows", "1", "metadata", "name"]);
        form.SetValue(["rows", "1", "metadata", "name"], JsonSerializer.SerializeToElement("configured"));
        form.RemoveListItem(["rows"], 0);

        await Assert.That(form.IsEditorVisible(["rows", "0", "metadata", "name"])).IsTrue();
        form.SetAllSectionsExpanded(false);
        await Assert.That(form.IsEditorVisible(["rows", "0", "metadata", "name"])).IsFalse();
    }

    [Test]
    public async Task RevocationClearsSharedSecretDraftAndRejectsLateEditorEvents()
    {
        JsonObject source = JsonNode.Parse(Definition().CanonicalBytes.Span)!.AsObject();
        source["configuration"]!["properties"]!["rows"]!["items"]!["properties"]!["password"]!["type"] = "integer";
        source["requiredCapabilities"] = new JsonArray(
            "projection.copy", "projection.object", "schema.array", "schema.boolean", "schema.bounds",
            "schema.integer", "schema.object", "schema.string", "values.initial");
        using var form = new DefinitionFormModel(
            SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString())),
            DefinitionSessionPrivacy.BrowserSecretsEnabled);
        form.AddListItem(["rows"]);
        form.EditValue(["rows", "0", "password"], "synthetic-secret-draft");
        await Assert.That(form.ReadDraftText(["rows", "0", "password"])).IsEqualTo("synthetic-secret-draft");
        form.SetDiscovery("password");
        form.SetAllSectionsExpanded(false);
        form.SetSecretEntryEnabled(false);

        await Assert.That(form.ReadDraftText(["rows", "0", "password"])).IsNull();
        await Assert.That(form.HasInputError(["rows", "0", "password"])).IsFalse();
        await Assert.That(form.EditValue(["rows", "0", "password"], "late-secret-draft")).IsFalse();
        form.SetSecretEntryEnabled(true);
        await Assert.That(form.ReadDraftText(["rows", "0", "password"])).IsNull();
        await Assert.That(form.Snapshot().Values["rows"][0].TryGetProperty("password", out _)).IsFalse();
    }

    [Test]
    public async Task NonexistentRowsCannotRetainDraftsAndNestedInvalidDraftsBlockPreparation()
    {
        using var form = new DefinitionFormModel(Definition());
        form.AddListItem(["rows"]);
        await Assert.That(form.EditValue(["rows", "40", "settings", "enabled"], "invalid")).IsFalse();
        await Assert.That(form.HasInputError(["rows", "40", "settings", "enabled"])).IsFalse();
        await Assert.That(form.EditValue(["rows", "0", "settings", "enabled"], "invalid")).IsFalse();
        await Assert.That(form.HasInputErrors).IsTrue();
    }

    [Test]
    public async Task ViewChangesAndSiblingRemovalPreserveDraftAndSemanticRowIdentity()
    {
        using var form = new DefinitionFormModel(Definition());
        form.AddListItem(["rows"]);
        form.AddListItem(["rows"]);
        string identity = form.GetEditorIdentity(["rows", "1", "settings", "enabled"]);
        form.EditValue(["rows", "1", "settings", "enabled"], "not-a-boolean");
        form.SetAllSectionsExpanded(true);
        form.SetDisclosureOverride(SetupFieldDisclosure.Essential);
        await Assert.That(form.IsEditorVisible(["rows", "1", "settings", "enabled"])).IsTrue();
        form.SetDisclosureOverride(SetupFieldDisclosure.Advanced);
        form.SetDiscovery("settings");
        form.RemoveListItem(["rows"], 0);

        await Assert.That(form.ReadDraftText(["rows", "0", "settings", "enabled"])).IsEqualTo("not-a-boolean");
        await Assert.That(form.GetEditorIdentity(["rows", "0", "settings", "enabled"])).IsEqualTo(identity);
        await Assert.That(form.HasInputErrors).IsTrue();
        await Assert.That(form.EditValue(["rows", "0", "settings", "enabled"], "false")).IsTrue();
        await Assert.That(form.ReadDraftText(["rows", "0", "settings", "enabled"])).IsNull();
        await Assert.That(form.HasInputErrors).IsFalse();
    }

    [Test]
    public async Task TypedRowsMaterializeOnlyDeclaredContainersAndEveryEditAdvancesRevision()
    {
        using var form = new DefinitionFormModel(Definition(), DefinitionSessionPrivacy.BrowserNoSecrets);
        await Assert.That(form.AddListItem(["rows"])).IsTrue();
        await Assert.That(form.Snapshot().Values["rows"][0].EnumerateObject().Count()).IsEqualTo(0);
        form.SupplyObject(["rows", "0", "metadata"]);
        form.SupplyObject(["rows", "0", "settings"]);
        JsonElement row = form.Snapshot().Values["rows"][0];
        await Assert.That(row.GetProperty("metadata").EnumerateObject().Count()).IsEqualTo(0);
        await Assert.That(row.GetProperty("settings").EnumerateObject().Count()).IsEqualTo(0);
        await Assert.That(form.Validate().TryGetEffectiveValue(DefinitionPath.Parse("/rows/0/settings/enabled"), out JsonElement initialized)).IsTrue();
        await Assert.That(initialized.GetBoolean()).IsTrue();
        await Assert.That(form.SetValue(["rows", "0", "metadata", "name"], JsonSerializer.SerializeToElement("first"))).IsTrue();
        await Assert.That(form.AddListItem(["rows"])).IsTrue();
        await Assert.That(form.AddListItem(["rows"])).IsFalse();
        await Assert.That(form.SetValue(["rows", "1", "metadata", "name"], JsonSerializer.SerializeToElement("second"))).IsTrue();
        await Assert.That(form.SupplyObject(["rows", "1", "settings"])).IsTrue();
        await Assert.That(form.RemoveListItem(["rows"], 0)).IsTrue();
        await Assert.That(form.InputRevision).IsEqualTo(8);
        using DefinitionArtifact? artifact = DefinitionArtifactComposer.Compose(Definition(), "config", form.Snapshot()).Artifact;
        await Assert.That(Encoding.UTF8.GetString(artifact!.Bytes.Span))
            .IsEqualTo("""{"rows":[{"metadata":{"name":"second"},"settings":{"enabled":true}}]}""");
    }

    [Test]
    public async Task PerLeafConsentRetainsSafeRowsAndClearsSecretsWithoutRestoringRemovedRows()
    {
        using var form = new DefinitionFormModel(Definition(), DefinitionSessionPrivacy.BrowserNoSecrets);
        form.AddListItem(["rows"]);
        await Assert.That(form.CanEdit(["rows", "0", "metadata", "name"])).IsTrue();
        await Assert.That(form.CanEdit(["rows", "0", "password"])).IsFalse();
        await Assert.That(form.SetValue(["rows", "0", "password"], JsonSerializer.SerializeToElement("synthetic-private-row"))).IsFalse();
        form.SetSecretEntryEnabled(true);
        form.SetValue(["rows", "0", "metadata", "name"], JsonSerializer.SerializeToElement("safe"));
        form.SetValue(["rows", "0", "password"], JsonSerializer.SerializeToElement("synthetic-private-row"));
        form.AddListItem(["rows"]);
        form.SetValue(["rows", "1", "metadata", "name"], JsonSerializer.SerializeToElement("removed"));
        form.RemoveListItem(["rows"], 1);
        form.SetSecretEntryEnabled(false);
        JsonElement rows = form.Snapshot().Values["rows"];
        await Assert.That(rows.GetArrayLength()).IsEqualTo(1);
        await Assert.That(rows[0].GetProperty("metadata").GetProperty("name").GetString()).IsEqualTo("safe");
        await Assert.That(rows[0].TryGetProperty("password", out _)).IsFalse();
    }

    private static SetupDefinition Definition() => SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes("""
    {"schemaVersion":2,"profile":"bounded-v2","familyId":"03b3abf0-08e0-4e56-b94a-b78d25d28901","revision":1,
     "project":{"id":"example","name":"Example","publisher":"example"},
     "applicationVersions":["1"],"entryPoints":["setup"],"locales":["en"],
     "requiredCapabilities":["projection.copy","projection.object","schema.array","schema.boolean","schema.bounds","schema.object","schema.string","values.initial"],
     "configuration":{"type":"object","additionalProperties":false,"x-configurator":{"sensitivity":"public"},"properties":{
       "rows":{"type":"array","maxItems":2,"title":"Rows","x-configurator":{"sensitivity":"public","detail":"standard"},
         "items":{"type":"object","additionalProperties":false,"required":["metadata","settings"],"x-configurator":{"sensitivity":"public"},"properties":{
           "metadata":{"type":"object","additionalProperties":false,"required":["name"],"title":"Metadata","x-configurator":{"sensitivity":"public","detail":"standard"},"properties":{
             "name":{"type":"string","title":"Name","x-configurator":{"sensitivity":"public","detail":"standard"}}}},
           "settings":{"type":"object","additionalProperties":false,"title":"Settings","x-configurator":{"sensitivity":"public","detail":"standard"},"properties":{
             "enabled":{"type":"boolean","title":"Enabled","x-configurator":{"sensitivity":"public","detail":"standard","initialValue":true}}}},
           "password":{"type":"string","title":"Password","x-configurator":{"sensitivity":"secret","detail":"standard"}}}}}}},
     "artifacts":[{"id":"config","format":"json","fileName":"config.json","targetUpdateMode":"create","restartRequirement":"process",
       "projection":{"op":"object","members":{"rows":{"op":"copy","reference":{"scope":"root","path":"/rows"}}}}}]}
    """));
}
