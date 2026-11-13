namespace Configurator.Presentation.Tests;

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CommunityToolkit.Mvvm.Messaging;
using Configurator.Core.Definitions;

public sealed class DefinitionPrivateClearTests
{
    private static readonly TimeSpan SignalTimeout = TimeSpan.FromSeconds(5);

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task WarmedFactsCannotRestoreRevokedSecretsOrOutputAfterLateImport(bool replaceForm)
    {
        JsonObject source = JsonNode.Parse(Definition().CanonicalBytes.Span)!.AsObject();
        source["configuration"]!["properties"]!["rows"] = JsonNode.Parse("""
        {"type":"array","title":"Rows","x-configurator":{"sensitivity":"public","detail":"standard"},
          "items":{"type":"object","additionalProperties":false,"x-configurator":{"sensitivity":"public"},"properties":{
            "name":{"type":"string","title":"Name","x-configurator":{"sensitivity":"public","detail":"standard"}},
            "password":{"type":"string","title":"Password","x-configurator":{"sensitivity":"secret","detail":"advanced"}}}}}
        """);
        source["requiredCapabilities"] = new JsonArray(
            "projection.copy", "projection.object", "schema.array", "schema.object", "schema.string");
        source["artifacts"]![0]!["projection"]!["members"]!["rows"] =
            JsonNode.Parse("""{"op":"copy","reference":{"scope":"root","path":"/rows"}}""");
        Definition definition = DefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString()));
        using var form = new DefinitionFormModel(definition, DefinitionSessionPrivacy.BrowserSecretsEnabled);
        form.EditValue(["title"], "initial-public");
        form.EditValue(["password"], "synthetic-private-root");
        form.SetValue(["rows"], JsonSerializer.SerializeToElement(new[]
        {
            new { name = "row-public", password = "synthetic-private-row" }
        }));
        form.SetDiscovery("password");
        _ = form.Projection;
        string identity = form.GetEditorIdentity(["rows", "0", "name"]);
        using var session = new PresentationSession(new WeakReferenceMessenger());
        WorkspaceId.TryCreate("old-output", out WorkspaceId oldId);
        using var workspace = session.CreateWorkspace(oldId,
            new DefinitionPresentationOperation(definition, "config", form.Snapshot()));
        await workspace.ExecuteAsync(Guid.NewGuid());
        ReadOnlyMemory<byte> oldOutput = workspace.Output;
        await Assert.That(oldOutput.IsEmpty).IsFalse();
        var pendingRead = new TaskCompletionSource<ReadOnlyMemory<byte>>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<DefinitionImportResult> import = form.ImportAsync("config", _ => pendingRead.Task);

        bool reentered = false;
        form.Fields.Single(field => field.Definition.Id == "password").PropertyChanged += (_, args) =>
        {
            if (reentered || args.PropertyName != nameof(DefinitionFieldInput.Text)) return;
            reentered = true;
            form.EditValue(["title"], "reentrant-public");
            _ = form.Projection;
        };
        EventHandler failing = (_, _) => throw new InvalidOperationException("synthetic-observer");
        form.InputChanged += failing;
        form.InputChanged += (_, _) => workspace.Deactivate();
        AggregateException? failure = null;
        try { form.SetSecretEntryEnabled(false); }
        catch (AggregateException exception) { failure = exception; }
        finally { form.InputChanged -= failing; }

        await Assert.That(failure).IsNotNull();
        await Assert.That(reentered).IsTrue();
        await Assert.That(workspace.Output.IsEmpty).IsTrue();
        await Assert.That(oldOutput.ToArray().All(value => value == 0)).IsTrue();
        await Assert.That(form.Snapshot().Values.ContainsKey("password")).IsFalse();
        await Assert.That(form.Snapshot().Values["rows"][0].TryGetProperty("password", out _)).IsFalse();
        await Assert.That(form.GetEditorIdentity(["rows", "0", "name"])).IsEqualTo(identity);
        await Assert.That(form.Projection.Entries.Single(entry => entry.FieldId == "password").IsProvided).IsFalse();
        form.SetSecretEntryEnabled(true);
        form.EditValue(["title"], "current-public");
        using var replacement = new DefinitionFormModel(definition, DefinitionSessionPrivacy.BrowserSecretsEnabled);
        if (replaceForm) form.Dispose();
        DefinitionFormModel current = replaceForm ? replacement : form;
        if (replaceForm) current.EditValue(["title"], "replacement-public");
        WorkspaceId.TryCreate("current-output", out WorkspaceId currentId);
        using var currentWorkspace = session.CreateWorkspace(currentId,
            new DefinitionPresentationOperation(definition, "config", current.Snapshot()));
        await currentWorkspace.ExecuteAsync(Guid.NewGuid());
        ReadOnlyMemory<byte> currentOutput = currentWorkspace.Output;
        byte[] late = """{"settings":{"password":"synthetic-late-secret"}}"""u8.ToArray();
        pendingRead.SetResult(late);
        DefinitionImportResult result = await import.WaitAsync(SignalTimeout);

        await Assert.That(result.Succeeded).IsFalse();
        await Assert.That(result.Code).IsEqualTo("import-stale");
        await Assert.That(late.All(value => value == 0)).IsTrue();
        await Assert.That(current.Snapshot().Values["title"].GetString())
            .IsEqualTo(replaceForm ? "replacement-public" : "current-public");
        await Assert.That(current.Snapshot().Values.ContainsKey("password")).IsFalse();
        await Assert.That(currentWorkspace.Output.IsEmpty).IsFalse();
        using (JsonDocument document = JsonDocument.Parse(currentWorkspace.Output))
        {
            await Assert.That(document.RootElement.GetProperty("title").GetString())
                .IsEqualTo(replaceForm ? "replacement-public" : "current-public");
            await Assert.That(document.RootElement.TryGetProperty("password", out _)).IsFalse();
        }
        _ = current.Projection;
        current.Dispose();
        currentWorkspace.Dispose();
        await Assert.That(currentOutput.ToArray().All(value => value == 0)).IsTrue();
        await Assert.That(() => current.Projection).Throws<ObjectDisposedException>();
    }

    [Test]
    public async Task ClearPreservesPublicRootsAndClearsProtectedRootsInOneRevision()
    {
        using var form = new DefinitionFormModel(Definition());
        form.Fields.Single(field => field.Definition.Id == "title").Text = "public-title";
        form.Fields.Single(field => field.Definition.Id == "token").Text = "synthetic-token";
        form.Fields.Single(field => field.Definition.Id == "password").Text = "synthetic-password";
        DefinitionFieldInput settings = form.Fields.Single(field => field.Definition.Id == "settings");
        settings.Text = """{"name":"public-sibling","password":"nested-secret"}""";
        form.Fields.Single(field => field.Definition.Id == "password").Text = new string('x', 65_537);
        long before = form.InputRevision;
        int revisions = 0;
        form.InputChanged += (_, _) => revisions++;
        bool changed = form.ClearPrivateState();
        AnswerSnapshot snapshot = form.Snapshot();
        await Assert.That(changed).IsTrue();
        await Assert.That(snapshot.InputRevision).IsEqualTo(before + 1);
        await Assert.That(revisions).IsEqualTo(1);
        await Assert.That(snapshot.Values.Count).IsEqualTo(2);
        await Assert.That(snapshot.Values["title"].GetString()).IsEqualTo("public-title");
        await Assert.That(snapshot.Values["settings"].GetProperty("name").GetString()).IsEqualTo("public-sibling");
        await Assert.That(snapshot.Values["settings"].TryGetProperty("password", out _)).IsFalse();
        await Assert.That(settings.Text).DoesNotContain("nested-secret");
        await Assert.That(settings.Text).Contains("public-sibling");
        await Assert.That(form.Fields.All(field => field.InputDiagnosticCode is null)).IsTrue();
    }

    [Test]
    public async Task NestedMutationNotifiesAfterRevisionAndOutsideFormGate()
    {
        using var form = new DefinitionFormModel(Definition());
        long observedRevision = -1;
        System.ComponentModel.PropertyChangedEventHandler subscriber = (_, args) =>
        {
            if (args.PropertyName != nameof(DefinitionFieldInput.Text)) return;
            observedRevision = Task.Run(form.Snapshot).WaitAsync(SignalTimeout).GetAwaiter().GetResult().InputRevision;
        };
        form.Fields.Single(field => field.Definition.Id == "settings").PropertyChanged += subscriber;
        try { form.SetValue(["settings", "name"], JsonSerializer.SerializeToElement("public-sibling")); }
        finally { form.Fields.Single(field => field.Definition.Id == "settings").PropertyChanged -= subscriber; }
        await Assert.That(observedRevision).IsEqualTo(1);
    }

    [Test]
    public async Task ClearEmptyProtectedRootsStillFencesPendingImport()
    {
        using var form = new DefinitionFormModel(Definition());
        var read = new TaskCompletionSource<ReadOnlyMemory<byte>>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<DefinitionImportResult> import = form.ImportAsync("config", _ => read.Task);
        form.ClearPrivateState();
        read.SetResult("""{"password":"late-secret"}"""u8.ToArray());
        await Assert.That((await import.WaitAsync(SignalTimeout)).Succeeded).IsFalse();
        await Assert.That(form.InputRevision).IsEqualTo(1);
        await Assert.That(form.Snapshot().Values.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ImportNotificationCannotRestoreSecretsAfterReentrantClear()
    {
        using var form = new DefinitionFormModel(Definition());
        bool cleared = false;
        form.Fields.Single(field => field.Definition.Id == "token").PropertyChanged += (_, args) =>
        {
            if (cleared || args.PropertyName != nameof(DefinitionFieldInput.Text)) return;
            cleared = true;
            form.ClearPrivateState();
        };
        byte[] source = """{"title":"public-title","token":"synthetic-token","password":"synthetic-password","settings":{"password":"nested-secret"}}"""u8.ToArray();
        await form.ImportAsync("config", _ => Task.FromResult<ReadOnlyMemory<byte>>(source));
        await Assert.That(cleared).IsTrue();
        await Assert.That(form.Snapshot().Values.Count).IsEqualTo(2);
        await Assert.That(form.Snapshot().Values.ContainsKey("title")).IsTrue();
        await Assert.That(form.Snapshot().Values["settings"].ValueKind).IsEqualTo(JsonValueKind.Object);
        await Assert.That(form.Snapshot().Values["settings"].EnumerateObject().Any()).IsFalse();
        await Assert.That(form.Fields.Single(field => field.Definition.Id == "settings").Text).DoesNotContain("nested-secret");
        await Assert.That(source.All(value => value == 0)).IsTrue();
    }

    [Test]
    public async Task ClearFencesPendingImportAndStillCompletesWhenSubscriberThrows()
    {
        using var form = new DefinitionFormModel(Definition());
        form.Fields.Single(field => field.Definition.Id == "token").Text = "synthetic-token";
        form.Fields.Single(field => field.Definition.Id == "password").Text = "synthetic-password";
        var read = new TaskCompletionSource<ReadOnlyMemory<byte>>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<DefinitionImportResult> import = form.ImportAsync("config", _ => read.Task);
        int invalidations = 0;
        form.InputChanged += (_, _) => invalidations++;
        System.ComponentModel.PropertyChangedEventHandler subscriber = (_, _) => throw new InvalidOperationException("synthetic-subscriber");
        form.Fields.Single(field => field.Definition.Id == "token").PropertyChanged += subscriber;
        Exception? observed = null;
        try { form.ClearPrivateState(); }
        catch (Exception exception) { observed = exception; }
        finally { form.Fields.Single(field => field.Definition.Id == "token").PropertyChanged -= subscriber; }
        byte[] source = """{"password":"late-secret"}"""u8.ToArray();
        read.SetResult(source);
        DefinitionImportResult result = await import.WaitAsync(SignalTimeout);
        await Assert.That(observed).IsNotNull();
        await Assert.That(form.Snapshot().Values.Count).IsEqualTo(0);
        await Assert.That(invalidations).IsEqualTo(1);
        await Assert.That(result.Succeeded).IsFalse();
        await Assert.That(source.All(value => value == 0)).IsTrue();
    }

    [Test]
    public async Task FailingInputSubscriberCannotSkipRemainingInvalidationObservers()
    {
        using var form = new DefinitionFormModel(Definition());
        form.Fields.Single(field => field.Definition.Id == "token").Text = "synthetic-token";
        EventHandler failing = (_, _) => throw new InvalidOperationException("synthetic-subscriber");
        int invalidations = 0;
        form.InputChanged += failing;
        form.InputChanged += (_, _) => invalidations++;
        AggregateException? observed = null;
        try { form.ClearPrivateState(); }
        catch (AggregateException exception) { observed = exception; }
        finally { form.InputChanged -= failing; }
        await Assert.That(observed).IsNotNull();
        await Assert.That(invalidations).IsEqualTo(1);
        await Assert.That(form.Snapshot().Values.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ClearPrunesNestedProtectedDraftsDiagnosticsAndOriginsWhileKeepingPublicDrafts()
    {
        using var form = new DefinitionFormModel(MixedSourceDefinition());
        DefinitionFieldInput owner = form.Fields.Single(field => field.Definition.Id == "owner");
        form.EditValue(["owner", "count"], "not-an-integer");
        form.EditValue(["owner", "secretCount"], "not-a-secret-integer");
        form.SetValue(["owner", "name"], JsonSerializer.SerializeToElement("public-sibling"));
        form.SetValue(["owner", "token"], JsonSerializer.SerializeToElement("nested-secret"));
        owner.Text = new string('z', 65_537);
        await Assert.That(owner.InputDiagnosticCode).IsEqualTo("answer-budget-exceeded");

        form.ClearPrivateState();

        AnswerSnapshot snapshot = form.Snapshot();
        await Assert.That(snapshot.Values["owner"].GetProperty("name").GetString()).IsEqualTo("public-sibling");
        await Assert.That(snapshot.Values["owner"].TryGetProperty("token", out _)).IsFalse();
        await Assert.That(owner.HasAnswerBudgetError).IsFalse();
        await Assert.That(owner.InputDiagnosticCode).IsEqualTo("input-type-invalid");
        await Assert.That(owner.HasInputError(["owner"])).IsFalse();
        await Assert.That(owner.HasInputError(["owner", "count"])).IsTrue();
        await Assert.That(owner.HasInputError(["owner", "secretCount"])).IsFalse();
        await Assert.That(owner.Text).DoesNotContain("nested-secret");
        await Assert.That(owner.Text).DoesNotContain("zzz");
        await Assert.That(form.ReadDraftText(["owner", "count"])).IsEqualTo("not-an-integer");
        await Assert.That(form.ReadDraftText(["owner", "secretCount"])).IsNull();
        await Assert.That(form.ReadDraftText(["owner"])).IsNull();
        await Assert.That(form.AnswerOrigin(["owner", "name"])).IsEqualTo(DefinitionAnswerOrigin.Explicit);
        await Assert.That(form.AnswerOrigin(["owner", "token"])).IsEqualTo(DefinitionAnswerOrigin.Omitted);
    }

    [Test]
    public async Task ClearPreservesNullableJsonNullAndPrunesMapsArraysVariantsAndLiteralRoots()
    {
        using var form = new DefinitionFormModel(MixedSourceDefinition());
        await Assert.That(form.SetNull(["nullableOwner"])).IsTrue();
        DefinitionFieldInput nullableOwner = form.Fields.Single(field => field.Definition.Id == "nullableOwner");
        nullableOwner.Text = "not-json-{";
        await Assert.That(nullableOwner.InputDiagnosticCode).IsNotNull();

        using (JsonDocument mapDoc = JsonDocument.Parse("""{"entry-a":{"publicTag":"keep-a","token":"secret-a"}}"""))
            await Assert.That(form.SetValue(["mixedMap"], mapDoc.RootElement)).IsTrue();
        await Assert.That(form.SupplyObject(["secretMap", "synthetic-dyn-key"])).IsTrue();
        await Assert.That(form.SetValue(["secretMap", "synthetic-dyn-key", "publicChild"], null)).IsTrue();

        using (JsonDocument rowsDoc = JsonDocument.Parse("""[{"label":"row-0","token":"secret-row"}]"""))
            await Assert.That(form.SetValue(["mixedRows"], rowsDoc.RootElement)).IsTrue();
        using (JsonDocument privRows = JsonDocument.Parse("""[{"publicChild":"row-priv"}]"""))
            await Assert.That(form.SetValue(["sensitiveRows"], privRows.RootElement)).IsTrue();

        await Assert.That(form.SelectVariant(["mixedVariant"], 0)).IsTrue();
        await Assert.That(form.SetValue(["mixedVariant", "mode"], JsonSerializer.SerializeToElement("keep-variant"))).IsTrue();
        await Assert.That(form.SetValue(["mixedVariant", "token"], JsonSerializer.SerializeToElement("secret-variant"))).IsTrue();
        await Assert.That(form.SetValue(["owner", "name"], JsonSerializer.SerializeToElement("keep-owner-name"))).IsTrue();
        await Assert.That(form.SetValue(["owner", "lockedGroup"], JsonSerializer.SerializeToElement(
            new Dictionary<string, string> { ["publicChild"] = "inside-sensitive-group" }))).IsTrue();
        form.Fields.Single(field => field.Definition.Id == "unmatchedVariant").Text = """{"extra":"unmatched-raw"}""";

        form.ClearPrivateState();

        AnswerSnapshot snapshot = form.Snapshot();
        await Assert.That(snapshot.Values["nullableOwner"].ValueKind).IsEqualTo(JsonValueKind.Null);
        await Assert.That(nullableOwner.Text).IsEqualTo("null");
        await Assert.That(nullableOwner.InputDiagnosticCode).IsNull();
        await Assert.That(snapshot.Values["owner"].GetProperty("name").GetString()).IsEqualTo("keep-owner-name");
        await Assert.That(snapshot.Values["owner"].TryGetProperty("lockedGroup", out _)).IsFalse();

        await Assert.That(snapshot.Values["mixedMap"].GetProperty("entry-a").GetProperty("publicTag").GetString()).IsEqualTo("keep-a");
        await Assert.That(snapshot.Values["mixedMap"].GetProperty("entry-a").TryGetProperty("token", out _)).IsFalse();
        await Assert.That(snapshot.Values["secretMap"].EnumerateObject().Any()).IsFalse();
        await Assert.That(snapshot.Omitted.Any(path =>
            path.Segments.Any(segment => string.Equals(segment, "synthetic-dyn-key", StringComparison.Ordinal)))).IsFalse();

        await Assert.That(snapshot.Values["mixedRows"].GetArrayLength()).IsEqualTo(1);
        await Assert.That(snapshot.Values["mixedRows"][0].GetProperty("label").GetString()).IsEqualTo("row-0");
        await Assert.That(snapshot.Values["mixedRows"][0].TryGetProperty("token", out _)).IsFalse();
        await Assert.That(snapshot.Values.ContainsKey("sensitiveRows")).IsFalse();

        await Assert.That(snapshot.Values["mixedVariant"].GetProperty("mode").GetString()).IsEqualTo("keep-variant");
        await Assert.That(snapshot.Values["mixedVariant"].TryGetProperty("token", out _)).IsFalse();
        await Assert.That(snapshot.Values.ContainsKey("unmatchedVariant")).IsFalse();

        using var rootMapForm = new DefinitionFormModel(DefinitionSourceCodec.ParseJson("""
            {"$schema":"urn:islamu:configurator:authoring:v0.0.1",
             "identity":{"familyId":"eba722b7-f132-444b-8c44-21cece008ca9","revision":1},
             "project":{"id":"root-map-clear","name":"RootMapClear"},"applicationVersions":["1"],
             "configuration":{"type":"object","x-configurator":{"sensitivity":"public"},"additionalProperties":{
               "type":"object","additionalProperties":false,"x-configurator":{"sensitivity":"public"},"properties":{
                 "name":{"type":"string","x-configurator":{"sensitivity":"public"}},
                 "token":{"type":"string","writeOnly":true}}}},
             "outputs":{"config.json":{"format":"json"}}}
            """u8.ToArray()));
        await Assert.That(rootMapForm.SetValue(["pub-entry"], JsonSerializer.SerializeToElement(
            new Dictionary<string, string> { ["name"] = "public-root-entry", ["token"] = "root-secret" }))).IsTrue();
        rootMapForm.ClearPrivateState();
        AnswerSnapshot rootSnapshot = rootMapForm.Snapshot();
        await Assert.That(rootSnapshot.Values["pub-entry"].GetProperty("name").GetString()).IsEqualTo("public-root-entry");
        await Assert.That(rootSnapshot.Values["pub-entry"].TryGetProperty("token", out _)).IsFalse();
    }

    private static Definition MixedSourceDefinition() => DefinitionSourceCodec.ParseJson("""
        {"$schema":"urn:islamu:configurator:authoring:v0.0.1",
         "identity":{"familyId":"eba722b7-f132-444b-8c44-21cece008ca8","revision":1},
         "project":{"id":"mixed-clear","name":"MixedClear"},"applicationVersions":["1"],
         "configuration":{"type":"object","additionalProperties":false,"x-configurator":{"sensitivity":"public"},"properties":{
           "owner":{"type":"object","additionalProperties":false,"x-configurator":{"sensitivity":"public"},"properties":{
             "name":{"type":"string","x-configurator":{"sensitivity":"public"}},
             "count":{"type":"integer","x-configurator":{"sensitivity":"public"}},
             "secretCount":{"type":"integer","x-configurator":{"sensitivity":"sensitive"}},
             "token":{"type":"string","writeOnly":true},
             "lockedGroup":{"type":"object","additionalProperties":false,"x-configurator":{"sensitivity":"sensitive"},"properties":{
               "publicChild":{"type":"string","x-configurator":{"sensitivity":"public"}}}}}},
           "nullableOwner":{"type":["object","null"],"additionalProperties":false,"x-configurator":{"sensitivity":"public"},"properties":{
             "token":{"type":"string","writeOnly":true}}},
           "mixedMap":{"type":"object","x-configurator":{"sensitivity":"public"},"additionalProperties":{
             "type":"object","additionalProperties":false,"x-configurator":{"sensitivity":"public"},"properties":{
               "publicTag":{"type":"string","x-configurator":{"sensitivity":"public"}},
               "token":{"type":"string","writeOnly":true}}}},
           "secretMap":{"type":"object","x-configurator":{"sensitivity":"public"},"additionalProperties":{
             "type":"object","additionalProperties":false,"x-configurator":{"sensitivity":"sensitive"},"properties":{
               "publicChild":{"type":"string","x-configurator":{"sensitivity":"public"}}}}},
           "mixedRows":{"type":"array","x-configurator":{"sensitivity":"public"},"items":{
             "type":"object","additionalProperties":false,"x-configurator":{"sensitivity":"public"},"properties":{
               "label":{"type":"string","x-configurator":{"sensitivity":"public"}},
               "token":{"type":"string","writeOnly":true}}}},
           "sensitiveRows":{"type":"array","x-configurator":{"sensitivity":"public"},"items":{
             "type":"object","additionalProperties":false,"x-configurator":{"sensitivity":"sensitive"},"properties":{
               "publicChild":{"type":"string","x-configurator":{"sensitivity":"public"}}}}},
           "mixedVariant":{"type":"object","x-configurator":{"sensitivity":"public"},"oneOf":[
             {"type":"object","additionalProperties":false,"required":["kind"],"x-configurator":{"sensitivity":"public"},"properties":{
               "kind":{"type":"string","const":"mixed","x-configurator":{"sensitivity":"public"}},
               "mode":{"type":"string","x-configurator":{"sensitivity":"public"}},
               "token":{"type":"string","writeOnly":true}}},
             {"type":"object","additionalProperties":false,"required":["kind"],"x-configurator":{"sensitivity":"public"},"properties":{
               "kind":{"type":"string","const":"plain","x-configurator":{"sensitivity":"public"}}}}]},
           "unmatchedVariant":{"type":"object","x-configurator":{"sensitivity":"public"},"oneOf":[
             {"type":"object","additionalProperties":false,"required":["kind"],"x-configurator":{"sensitivity":"public"},"properties":{
               "kind":{"type":"string","const":"pub","x-configurator":{"sensitivity":"public"}}}},
             {"type":"object","additionalProperties":false,"required":["kind"],"x-configurator":{"sensitivity":"public"},"properties":{
               "kind":{"type":"string","const":"priv","x-configurator":{"sensitivity":"public"}},
               "token":{"type":"string","writeOnly":true}}}]}
         }},"outputs":{"config.json":{"format":"json"}}}
        """u8.ToArray());

    private static Definition Definition() => DefinitionCodec.ParseJson(Encoding.UTF8.GetBytes("""
    {
      "schemaVersion":"0.0.1","profile":"bounded-v0.0.1","familyId":"eba722b7-f132-444b-8c44-21cece008ca7","revision":1,
      "project":{"id":"example","name":"Example","publisher":"example"},"applicationVersions":["1"],
      "entryPoints":["setup"],"locales":["en"],
      "requiredCapabilities":["projection.copy","projection.object","schema.object","schema.string"],
      "configuration":{"type":"object","additionalProperties":false,"x-configurator":{"sensitivity":"public"},"properties":{
        "title":{"type":"string","title":"Title","x-configurator":{"sensitivity":"public","detail":"standard"}},
        "token":{"type":"string","title":"Token","x-configurator":{"sensitivity":"sensitive","detail":"standard"}},
        "password":{"type":"string","title":"Password","x-configurator":{"sensitivity":"secret","detail":"standard"}},
        "settings":{"type":"object","additionalProperties":false,"title":"Settings","x-configurator":{"sensitivity":"public","detail":"standard"},"properties":{
          "name":{"type":"string","title":"Name","x-configurator":{"sensitivity":"public","detail":"standard"}},
          "password":{"type":"string","title":"Password","x-configurator":{"sensitivity":"secret","detail":"standard"}}}}}},
      "artifacts":[{"id":"config","format":"json","fileName":"config.json","targetUpdateMode":"create","restartRequirement":"process",
        "projection":{"op":"object","members":{
          "title":{"op":"copy","reference":{"scope":"root","path":"/title"}},
          "token":{"op":"copy","reference":{"scope":"root","path":"/token"}},
          "password":{"op":"copy","reference":{"scope":"root","path":"/password"}},
          "settings":{"op":"copy","reference":{"scope":"root","path":"/settings"}}}}}]
    }
    """));
}
