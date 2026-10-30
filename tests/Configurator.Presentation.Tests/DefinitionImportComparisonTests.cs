namespace Configurator.Presentation.Tests;

using System.Text.Json;
using CommunityToolkit.Mvvm.Messaging;
using Configurator.Core.Definitions;

public sealed class DefinitionImportComparisonTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task BrowserRequiresRawInputConsentBeforeReadingEvenForPublicDefinitions(bool malformed)
    {
        using var form = new DefinitionFormModel(Definition(), DefinitionSessionPrivacy.BrowserNoSecrets);
        int reads = 0;
        DefinitionImportResult result = await form.ImportAsync("config", _ =>
        {
            reads++;
            return Task.FromResult<ReadOnlyMemory<byte>>(malformed
                ? """{"synthetic-obsolete-credential":"synthetic-private-value" """u8.ToArray()
                : """{"name":"imported","synthetic-obsolete-credential":"synthetic-private-value"}"""u8.ToArray());
        });

        await Assert.That(reads).IsEqualTo(0);
        await Assert.That(result.Code).IsEqualTo("import-secret-consent-required");
        await Assert.That(result.Succeeded).IsFalse();
        await Assert.That(result.Answers).IsNull();
        await Assert.That(form.InputRevision).IsEqualTo(0);
        await Assert.That(form.Snapshot().Values.Count).IsEqualTo(0);
        await Assert.That(result.ToString()).DoesNotContain("synthetic-private");
    }

    [Test]
    public async Task AutofillCannotEraseAnInvalidDraftEvenWhenRecoveredValueEqualsCommittedValue()
    {
        using var form = new DefinitionFormModel(Definition());
        form.SetValue(["count"], JsonSerializer.SerializeToElement(7));
        form.EditValue(["count"], "synthetic-invalid-draft");
        SetupAnswerSnapshot original = form.Snapshot();
        DefinitionImportResult result = await form.ImportAsync("config",
            _ => Task.FromResult<ReadOnlyMemory<byte>>("""{"count":7}"""u8.ToArray()));

        await Assert.That(result.Succeeded).IsFalse();
        await Assert.That(result.Code).IsEqualTo("answer-conflict");
        await Assert.That(result.Answers).IsNull();
        await Assert.That(form.InputRevision).IsEqualTo(original.InputRevision);
        await Assert.That(form.ReadDraftText(["count"])).IsEqualTo("synthetic-invalid-draft");
        await Assert.That(form.HasInputError(["count"])).IsTrue();
        await Assert.That(form.Snapshot().Values["count"].GetInt32()).IsEqualTo(7);
    }

    [Test]
    public async Task CompareKeepsDirtyAnswersDraftsOriginsAndRevisionIndependentOfFileAssessment()
    {
        using var empty = new DefinitionFormModel(Definition());
        using var dirty = new DefinitionFormModel(Definition());
        dirty.SetValue(["name"], JsonSerializer.SerializeToElement("synthetic-workspace-private"));
        dirty.SetValue(["count"], JsonSerializer.SerializeToElement(7));
        dirty.EditValue(["count"], "synthetic-invalid-draft");
        SetupAnswerSnapshot original = dirty.Snapshot();
        DefinitionAnswerOrigin origin = dirty.AnswerOrigin(["count"]);
        DefinitionImportComparisonResult first = await empty.CompareAsync("config", ReadValid);
        DefinitionImportComparisonResult second = await dirty.CompareAsync("config", ReadValid);

        await Assert.That(first.Succeeded).IsTrue();
        await Assert.That(second.Succeeded).IsTrue();
        await Assert.That(second.Report!.Compatibility).IsEqualTo(first.Report!.Compatibility);
        await Assert.That(JsonSerializer.Serialize(second.Report)).IsEqualTo(JsonSerializer.Serialize(first.Report));
        await Assert.That(dirty.InputRevision).IsEqualTo(original.InputRevision);
        await Assert.That(dirty.Snapshot().Values["name"].GetString()).IsEqualTo("synthetic-workspace-private");
        await Assert.That(dirty.Snapshot().Values["count"].GetInt32()).IsEqualTo(7);
        await Assert.That(dirty.ReadDraftText(["count"])).IsEqualTo("synthetic-invalid-draft");
        await Assert.That(dirty.AnswerOrigin(["count"])).IsEqualTo(origin);
        await Assert.That(dirty.AdoptImportComparison().Code).IsEqualTo("answer-conflict");
        await Assert.That(dirty.InputRevision).IsEqualTo(original.InputRevision);
    }

    [Test]
    public async Task BrowserComparisonDeniesAcquisitionWithoutConsent()
    {
        using var form = new DefinitionFormModel(Definition(), DefinitionSessionPrivacy.BrowserNoSecrets);
        bool consumed = false;
        DefinitionImportComparisonResult result = await form.CompareAsync("config", _ =>
        {
            consumed = true;
            return ReadValid(CancellationToken.None);
        });
        await Assert.That(result.Succeeded).IsFalse();
        await Assert.That(result.Code).IsEqualTo("import-secret-consent-required");
        await Assert.That(consumed).IsFalse();
        await Assert.That(form.ImportComparison).IsNull();
    }

    [Test]
    [Arguments("revoke")]
    [Arguments("edit")]
    [Arguments("clear")]
    [Arguments("dispose")]
    [Arguments("cancel")]
    public async Task InvalidatingPendingReadPreventsLateComparisonPublicationAndClearsOwnedBytes(string action)
    {
        using var form = new DefinitionFormModel(Definition(), DefinitionSessionPrivacy.BrowserSecretsEnabled);
        using var cancellation = new CancellationTokenSource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<ReadOnlyMemory<byte>>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<DefinitionImportComparisonResult> pending = form.CompareAsync("config", _ =>
        {
            entered.SetResult();
            return release.Task;
        }, cancellation.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        switch (action)
        {
            case "revoke": form.SetSecretEntryEnabled(false); break;
            case "edit": form.SetValue(["name"], JsonSerializer.SerializeToElement("later")); break;
            case "clear": form.ClearImportComparison(); break;
            case "dispose": form.Dispose(); break;
            case "cancel": cancellation.Cancel(); break;
        }
        byte[] bytes = """{"count":7}"""u8.ToArray();
        release.SetResult(bytes);
        DefinitionImportComparisonResult result = await pending.WaitAsync(TimeSpan.FromSeconds(10));

        await Assert.That(result.Succeeded).IsFalse();
        await Assert.That(result.Report).IsNull();
        await Assert.That(form.ImportComparison).IsNull();
        await Assert.That(bytes.All(value => value == 0)).IsTrue();
    }

    [Test]
    public async Task ReplacingComparisonKeepsOnlyLatestProposalAndAdoptsOnceDeliberately()
    {
        using var form = new DefinitionFormModel(Definition());
        var release = new TaskCompletionSource<ReadOnlyMemory<byte>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<DefinitionImportComparisonResult> older = form.CompareAsync("config", _ =>
        {
            entered.SetResult();
            return release.Task;
        });
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        DefinitionImportComparisonResult current = await form.CompareAsync("config", ReadValid);
        release.SetResult("""{"count":9}"""u8.ToArray());
        DefinitionImportComparisonResult stale = await older.WaitAsync(TimeSpan.FromSeconds(10));

        await Assert.That(current.Succeeded).IsTrue();
        await Assert.That(stale.Succeeded).IsFalse();
        await Assert.That(form.InputRevision).IsEqualTo(0);
        await Assert.That(form.Snapshot().Values.Count).IsEqualTo(0);
        DefinitionImportResult adopted = form.AdoptImportComparison();
        await Assert.That(adopted.Succeeded).IsTrue();
        await Assert.That(form.InputRevision).IsEqualTo(1);
        await Assert.That(form.Snapshot().Values["count"].GetInt32()).IsEqualTo(7);
        await Assert.That(form.AnswerOrigin(["count"])).IsEqualTo(DefinitionAnswerOrigin.Imported);
        await Assert.That(form.ImportComparison).IsNull();
        await Assert.That(form.AdoptImportComparison().Succeeded).IsFalse();
        await Assert.That(form.InputRevision).IsEqualTo(1);
    }

    private static Task<ReadOnlyMemory<byte>> ReadValid(CancellationToken _) =>
        Task.FromResult<ReadOnlyMemory<byte>>("""{"count":7}"""u8.ToArray());

    [Test]
    public async Task ImportedRowsAndMapEntriesExplainOnlySchemaWithoutSelectingCurrentEntities()
    {
        using var form = new DefinitionFormModel(Definition());
        form.SetValue(["rows"], JsonSerializer.SerializeToElement(new[]
        {
            new { count = 2 }, new { count = 1 }
        }));
        form.SetValue(["map"], JsonSerializer.SerializeToElement(
            new Dictionary<string, int> { ["synthetic-current-key"] = 8 }));
        SetupAnswerSnapshot original = form.Snapshot();
        int inputChanges = 0;
        form.InputChanged += (_, _) => inputChanges++;
        DefinitionImportComparisonResult result = await form.CompareAsync("config", _ =>
            Task.FromResult<ReadOnlyMemory<byte>>("""
                {"rows":[{"count":"synthetic-private-value"}],
                 "map":{"synthetic-private-key":"synthetic-private-value"}}
                """u8.ToArray()));

        await Assert.That(result.Succeeded).IsTrue();
        await Assert.That(result.Report!.Compatibility).IsEqualTo(DefinitionImportCompatibility.ChangesRequired);
        foreach (string field in new[] { "rows", "map" })
        {
            DefinitionImportFinding finding = result.Report.Findings.First(item => item.Schema?.FieldId == field);
            DefinitionImportSchemaExplanation? explanation = form.ExplainImportFinding(finding);
            await Assert.That(explanation).IsNotNull();
            await Assert.That(explanation!.Path.SequenceEqual([field])).IsTrue();
        }
        await Assert.That(inputChanges).IsEqualTo(0);
        await Assert.That(form.InputRevision).IsEqualTo(original.InputRevision);
        await Assert.That(form.Snapshot().Values["rows"].GetRawText()).IsEqualTo(original.Values["rows"].GetRawText());
        await Assert.That(form.Snapshot().Values["map"].GetRawText()).IsEqualTo(original.Values["map"].GetRawText());
        await Assert.That(JsonSerializer.Serialize(result)).DoesNotContain("synthetic-private");
        await Assert.That(form.AdoptImportComparison().Succeeded).IsFalse();
    }

    [Test]
    public async Task ComparisonReadFailureIsValueFreeAndRetainsNoProposal()
    {
        using var form = new DefinitionFormModel(Definition());
        DefinitionImportComparisonResult result = await form.CompareAsync("config",
            _ => throw new IOException("synthetic-private-path"));
        await Assert.That(result.Succeeded).IsFalse();
        await Assert.That(result.Code).IsEqualTo("import-read-failed");
        await Assert.That(form.ImportComparison).IsNull();
        await Assert.That(form.InputRevision).IsEqualTo(0);
        await Assert.That(JsonSerializer.Serialize(result)).DoesNotContain("synthetic-private");
    }

    [Test]
    public async Task AutofillCannotSilentlyDiscardRootEntriesWithoutOwnedEditorStorage()
    {
        SetupDefinition definition = SetupDefinitionSourceCodec.ParseJson("""
            {
              "$schema":"urn:islamu:configurator:authoring:v2",
              "identity":{"familyId":"198d708d-caba-4e48-a00d-b06242151617","revision":1},
              "project":{"id":"root-map","name":"Root map"},
              "applicationVersions":["1"],
              "configuration":{"type":"object","properties":{"anchor":{"type":"string"}},
                "additionalProperties":{"type":"integer"}},
              "outputs":{"config.json":{"id":"config","format":"json",
                "projection":{"op":"copy","reference":{"scope":"root","path":""}}}}
            }
            """u8.ToArray());
        using var form = new DefinitionFormModel(definition);
        DefinitionImportComparisonResult compared = await form.CompareAsync("config", _ =>
            Task.FromResult<ReadOnlyMemory<byte>>("""{"anchor":"known","private-entry":1}"""u8.ToArray()));

        await Assert.That(compared.Succeeded).IsTrue();
        await Assert.That(compared.Report!.Compatibility).IsEqualTo(DefinitionImportCompatibility.Compatible);
        await Assert.That(form.CanAdoptImportComparison).IsFalse();
        await Assert.That(form.AdoptImportComparison().Succeeded).IsFalse();
        await Assert.That(form.InputRevision).IsEqualTo(0);
        await Assert.That(form.Snapshot().Values.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ComparisonRetainsActualPreparedOutputAndWorkspaceGeneration()
    {
        using var form = new DefinitionFormModel(Definition());
        form.SetValue(["count"], JsonSerializer.SerializeToElement(7));
        using var session = new SetupPresentationSession(new WeakReferenceMessenger());
        if (!SetupWorkspaceId.TryCreate("comparison", out SetupWorkspaceId id))
            throw new InvalidOperationException("workspace-id");
        using var workspace = session.CreateWorkspace(id,
            new DefinitionPresentationOperation(form.Definition, "config", form.Snapshot()));
        workspace.Activate();
        form.InputChanged += (_, _) =>
        {
            workspace.Deactivate();
            workspace.Activate();
        };
        await workspace.ExecuteAsync(Guid.NewGuid());
        object? prepared = workspace.Result;
        SetupOperationGeneration generation = workspace.Generation;
        await Assert.That(workspace.Output.Span.SequenceEqual("""{"count":7}"""u8)).IsTrue();
        DefinitionImportComparisonResult result = await form.CompareAsync("config", ReadValid);

        await Assert.That(result.Succeeded).IsTrue();
        await Assert.That(ReferenceEquals(workspace.Result, prepared)).IsTrue();
        await Assert.That(workspace.Generation).IsEqualTo(generation);
        await Assert.That(workspace.Output.Span.SequenceEqual("""{"count":7}"""u8)).IsTrue();
    }

    private static SetupDefinition Definition() => SetupDefinitionSourceCodec.ParseJson("""
        {
          "$schema":"urn:islamu:configurator:authoring:v2",
          "identity":{"familyId":"198d708d-caba-4e48-a00d-b06242151617","revision":1},
          "project":{"id":"comparison","name":"Comparison"},
          "applicationVersions":["1"],
          "configuration":{
            "type":"object","additionalProperties":false,
            "properties":{
              "name":{"type":"string","x-configurator":{"sensitivity":"public"}},
              "count":{"type":"integer","x-configurator":{"sensitivity":"public"}},
              "rows":{"type":"array","items":{
                "type":"object","additionalProperties":false,"properties":{"count":{"type":"integer"}}
              }},
              "map":{"type":"object","additionalProperties":{"type":"integer"}}
            }
          },
          "outputs":{
            "config.json":{
              "id":"config","format":"json",
              "projection":{"op":"object","members":{
                "name":{"op":"copy","reference":{"scope":"root","path":"/name"}},
                "count":{"op":"copy","reference":{"scope":"root","path":"/count"}},
                "rows":{"op":"copy","reference":{"scope":"root","path":"/rows"}},
                "map":{"op":"copy","reference":{"scope":"root","path":"/map"}}
              }}
            }
          }
        }
        """u8.ToArray());
}
