namespace Configurator.Presentation.Tests;

using System.Text;
using System.Text.Json;
using Configurator.Core.Definitions;

public sealed class DefinitionDocumentEditingTests
{
    [Test]
    public async Task CanonicalPreservesFalseNullEmptyAndDistinguishesResetFromOmit()
    {
        using var form = new DefinitionFormModel(Definition());
        form.SetValue(["reset"], JsonSerializer.SerializeToElement("old"));
        form.SetValue(["omit"], JsonSerializer.SerializeToElement("old"));
        DefinitionFieldInput facade = form.Fields.Single(field => field.Definition.Id == "text");
        int events = 0;
        form.InputChanged += (_, _) => events++;
        long before = form.InputRevision;
        using var session = form.OpenDocument();
        using var result = session.Parse(Envelope("""{"flag":false,"nullable":null,"text":"","literal.dot":"exact"}""", """["/omit"]"""));
        await Assert.That(result.Succeeded).IsTrue();
        await Assert.That(form.InputRevision).IsEqualTo(before);
        DefinitionDocumentApplyResult applied = session.Apply(result.Candidate!);
        await Assert.That(applied.Succeeded).IsTrue();
        await Assert.That(form.InputRevision).IsEqualTo(before + 1);
        await Assert.That(events).IsEqualTo(1);
        AnswerSnapshot answers = form.Snapshot();
        await Assert.That(answers.Values["flag"].GetBoolean()).IsFalse();
        await Assert.That(answers.Values["nullable"].ValueKind).IsEqualTo(JsonValueKind.Null);
        await Assert.That(answers.Values["text"].GetString()).IsEqualTo("");
        await Assert.That(answers.Values["literal.dot"].GetString()).IsEqualTo("exact");
        await Assert.That(answers.Values.ContainsKey("reset")).IsFalse();
        await Assert.That(answers.Omitted.Contains(DefinitionPath.Parse("/omit"))).IsTrue();
        await Assert.That(form.AnswerOrigin(["text"])).IsEqualTo(DefinitionAnswerOrigin.Explicit);
        await Assert.That(ReferenceEquals(facade, form.Fields.Single(field => field.Definition.Id == "text"))).IsTrue();
        facade.Text = "later";
        await Assert.That(form.Snapshot().Values["text"].GetString()).IsEqualTo("later");
        await Assert.That(session.Apply(result.Candidate!).Succeeded).IsFalse();
    }

    [Test]
    public async Task SourceExcludesDefaultsProtectedLeavesAndMixedCollections()
    {
        using var form = new DefinitionFormModel(Definition());
        form.SetValue(["secret"], JsonSerializer.SerializeToElement("synthetic-private"));
        form.SetValue(["mixed"], JsonSerializer.SerializeToElement(new { open = "shown", hidden = "synthetic-hidden" }));
        form.SetValue(["mixedRows"], JsonSerializer.SerializeToElement(new[] { new { open = "row", hidden = "synthetic-row" } }));
        using var session = form.OpenDocument();
        byte[] source = session.ReadSource();
        AnswerSnapshot projected = AnswerSnapshotCodec.ParseJson(source);
        await Assert.That(projected.Values.ContainsKey("secret")).IsFalse();
        await Assert.That(projected.Values.ContainsKey("mixedRows")).IsFalse();
        await Assert.That(projected.Values.ContainsKey("reset")).IsFalse();
        await Assert.That(projected.Values["mixed"].GetProperty("open").GetString()).IsEqualTo("shown");
        await Assert.That(projected.Values["mixed"].TryGetProperty("hidden", out _)).IsFalse();
        using var parsed = session.Parse(source);
        await Assert.That(parsed.Succeeded).IsTrue();
        await Assert.That(session.Apply(parsed.Candidate!).Succeeded).IsTrue();
        await Assert.That(form.Snapshot().Values["secret"].GetString()).IsEqualTo("synthetic-private");
        await Assert.That(form.Snapshot().Values["mixed"].GetProperty("hidden").GetString()).IsEqualTo("synthetic-hidden");
        await Assert.That(JsonSerializer.Serialize(session)).DoesNotContain("synthetic-");
        await Assert.That(JsonSerializer.Serialize(parsed)).DoesNotContain("synthetic-");
        await Assert.That(JsonSerializer.Serialize(parsed.Candidate)).DoesNotContain("synthetic-");
    }

    [Test]
    [Arguments("duplicate")]
    [Arguments("invalid")]
    [Arguments("unknown")]
    [Arguments("protected")]
    [Arguments("mixed")]
    [Arguments("oversized")]
    [Arguments("shape")]
    [Arguments("overlap")]
    [Arguments("nestedUnknown")]
    [Arguments("invalidArray")]
    [Arguments("fieldBudget")]
    [Arguments("depth")]
    public async Task RejectedCandidatePreservesIndependentWorkspaceState(string scenario)
    {
        using var form = new DefinitionFormModel(Definition());
        form.SetValue(["text"], JsonSerializer.SerializeToElement("retained"));
        form.SetValue(["rows"], JsonSerializer.Deserialize<JsonElement>("""["first","second"]"""));
        form.SetValue(["secret"], JsonSerializer.SerializeToElement("synthetic-private"));
        form.SetValue(["omit"], null);
        form.EditValue(["mixed", "hiddenCount"], "synthetic-draft");
        long revision = form.InputRevision;
        string row = form.GetEditorIdentity(["rows", "0"]);
        bool ready = form.Validate().IsReady;
        DefinitionAnswerOrigin origin = form.AnswerOrigin(["text"]);
        using var session = form.OpenDocument();
        byte[] bytes = scenario switch
        {
            "duplicate" => Envelope("""{"text":"one","text":"two"}"""),
            "invalid" => Envelope("""{"count":"not-an-integer"}"""),
            "unknown" => Envelope("""{"synthetic-unknown":"synthetic-private"}"""),
            "protected" => Envelope("""{"secret":"synthetic-private"}"""),
            "mixed" => Envelope("""{"mixedRows":[]}"""),
            "oversized" => new byte[4_194_305],
            "shape" => """{"answerContractVersion":"0.0.1","values":[],"omitted":[]}"""u8.ToArray(),
            "nestedUnknown" => Envelope("""{"pair":{"synthetic-unknown":[]}}"""),
            "invalidArray" => Envelope("""{"rows":[42]}"""),
            "fieldBudget" => Envelope("{\"text\":\"" + new string('x', 65_537) + "\"}"),
            "depth" => Envelope("""{"rows":""" + new string('[', 33) + "0" + new string(']', 33) + "}"),
            _ => Envelope("""{"text":"new"}""", """["/text"]""")
        };
        using var result = session.Parse(bytes);
        await Assert.That(result.Succeeded).IsFalse();
        await Assert.That(result.Candidate).IsNull();
        await Assert.That(form.Snapshot().Values["text"].GetString()).IsEqualTo("retained");
        await Assert.That(form.Snapshot().Values["rows"][1].GetString()).IsEqualTo("second");
        await Assert.That(form.Snapshot().Values["secret"].GetString()).IsEqualTo("synthetic-private");
        await Assert.That(form.Snapshot().Omitted.Contains(DefinitionPath.Parse("/omit"))).IsTrue();
        await Assert.That(form.ReadDraftText(["mixed", "hiddenCount"])).IsEqualTo("synthetic-draft");
        await Assert.That(form.AnswerOrigin(["text"])).IsEqualTo(origin);
        await Assert.That(form.GetEditorIdentity(["rows", "0"])).IsEqualTo(row);
        await Assert.That(form.InputRevision).IsEqualTo(revision);
        await Assert.That(form.Validate().IsReady).IsEqualTo(ready);
        await Assert.That(JsonSerializer.Serialize(result)).DoesNotContain("synthetic-");
    }

    [Test]
    public async Task PublicDraftBlocksSourceWhileUnrepresentedDraftSurvivesApply()
    {
        using var form = new DefinitionFormModel(Definition());
        form.EditValue(["count"], "invalid");
        using var blocked = form.OpenDocument();
        await Assert.That(() => blocked.ReadSource()).Throws<InvalidOperationException>();
        form.ResetValue(["count"]);
        form.EditValue(["mixed", "hiddenCount"], "synthetic-draft");
        using var session = form.OpenDocument();
        using var parsed = session.Parse(Envelope("""{"text":"new"}"""));
        await Assert.That(parsed.Succeeded).IsTrue();
        await Assert.That(session.Apply(parsed.Candidate!).Succeeded).IsTrue();
        await Assert.That(form.ReadDraftText(["mixed", "hiddenCount"])).IsEqualTo("synthetic-draft");
    }

    [Test]
    public async Task PartialRequiredObjectAppliesWithoutClaimingReadiness()
    {
        using var form = new DefinitionFormModel(Definition());
        using var session = form.OpenDocument();
        using var parsed = session.Parse(Envelope("""{"requiredObject":{"optional":"ok"}}"""));
        await Assert.That(parsed.Succeeded).IsTrue();
        await Assert.That(session.Apply(parsed.Candidate!).Succeeded).IsTrue();
        await Assert.That(form.Validate().IsReady).IsFalse();
        await Assert.That(form.Validate().Diagnostics.Any(item => item.Code == "value-required")).IsTrue();
        await Assert.That(form.Snapshot().Values["requiredObject"].GetProperty("optional").GetString()).IsEqualTo("ok");
    }

    [Test]
    public async Task ChangedContainerRetiresOnlyAffectedRowsAndPreservesSiblingOrigin()
    {
        using var form = new DefinitionFormModel(Definition());
        form.SetValue(["pair"], JsonSerializer.Deserialize<JsonElement>("""{"left":["a"],"right":["b"]}"""));
        string left = form.GetEditorIdentity(["pair", "left", "0"]);
        using var session = form.OpenDocument();
        using var parsed = session.Parse(Envelope("""{"pair":{"left":["changed"],"right":["b"]}}"""));
        // Allocate this identity after candidate capture, as a renderer may do.
        string right = form.GetEditorIdentity(["pair", "right", "0"]);
        DefinitionAnswerOrigin origin = form.AnswerOrigin(["pair", "right"]);
        await Assert.That(session.Apply(parsed.Candidate!).Succeeded).IsTrue();
        await Assert.That(form.GetEditorIdentity(["pair", "left", "0"])).IsNotEqualTo(left);
        await Assert.That(form.GetEditorIdentity(["pair", "right", "0"])).IsEqualTo(right);
        await Assert.That(form.AnswerOrigin(["pair", "right"])).IsEqualTo(origin);
    }

    [Test]
    public async Task ChangedImportedContainerBecomesExplicitWithoutRelabelingUntouchedSibling()
    {
        using var form = new DefinitionFormModel(Definition());
        DefinitionImportResult imported = await form.ImportAsync("json", _ =>
            Task.FromResult<ReadOnlyMemory<byte>>("""{"pair":{"left":["a"],"right":["b"]}}"""u8.ToArray()));
        await Assert.That(imported.Succeeded).IsTrue();
        await Assert.That(form.AnswerOrigin(["pair"])).IsEqualTo(DefinitionAnswerOrigin.Imported);
        using var session = form.OpenDocument();
        using var result = session.Parse(Envelope("""{"pair":{"left":["changed"],"right":["b"]}}"""));
        await Assert.That(session.Apply(result.Candidate!).Succeeded).IsTrue();
        await Assert.That(form.AnswerOrigin(["pair"])).IsEqualTo(DefinitionAnswerOrigin.Explicit);
        await Assert.That(form.AnswerOrigin(["pair", "left"])).IsEqualTo(DefinitionAnswerOrigin.Explicit);
        await Assert.That(form.AnswerOrigin(["pair", "right"])).IsEqualTo(DefinitionAnswerOrigin.Imported);
    }

    [Test]
    public async Task EnvironmentMergeAndReplaceUseExactActivePublicMappings()
    {
        using var form = new DefinitionFormModel(Definition());
        form.SetValue(["text"], JsonSerializer.SerializeToElement("old"));
        form.SetValue(["count"], JsonSerializer.SerializeToElement(7));
        using (var session = form.OpenDocument(DefinitionDocumentRepresentation.Environment, "env"))
        {
            using var parsed = session.Parse(Encoding.UTF8.GetBytes("\uFEFF# comment\r\nTEXT=\r\nDOT=a#b=c\\d\"e\r\n"));
            await Assert.That(parsed.Succeeded).IsTrue();
            await Assert.That(session.Apply(parsed.Candidate!).Succeeded).IsTrue();
            await Assert.That(form.Snapshot().Values["count"].GetInt32()).IsEqualTo(7);
            await Assert.That(form.Snapshot().Values["text"].GetString()).IsEqualTo("");
            await Assert.That(form.Snapshot().Values["literal.dot"].GetString()).IsEqualTo("a#b=c\\d\"e");
        }
        using var replace = form.OpenDocument(DefinitionDocumentRepresentation.Environment, "env");
        using var replacement = replace.Parse("TEXT=next\n"u8.ToArray(), DefinitionDocumentApplyMode.ReplaceRepresentedScope);
        await Assert.That(replacement.Candidate!.Changes.Any(change => change.Path.ToPointer() == "/count"
            && change.Kind == DefinitionDocumentChangeKind.Omit)).IsTrue();
        await Assert.That(replace.Apply(replacement.Candidate).Succeeded).IsTrue();
        await Assert.That(form.Snapshot().Values.ContainsKey("count")).IsFalse();
        await Assert.That(form.Snapshot().Omitted.Contains(DefinitionPath.Parse("/count"))).IsTrue();
        await Assert.That(form.Snapshot().Omitted.Contains(DefinitionPath.Parse("/reset"))).IsFalse();
    }

    [Test]
    [Arguments("TEXT=a\nTEXT=b\n")]
    [Arguments("TEXT=a\ntext=b\n")]
    [Arguments("UNKNOWN=synthetic-private\n")]
    [Arguments("LITERAL=read-only\n")]
    [Arguments("CONTROLLER=true\n")]
    [Arguments("ACTIVE=hidden\n")]
    [Arguments("COUNT=01\n")]
    public async Task EnvironmentRejectsInvalidKeysWithoutPartialMutation(string source)
    {
        using var form = new DefinitionFormModel(Definition());
        form.SetValue(["text"], JsonSerializer.SerializeToElement("old"));
        long revision = form.InputRevision;
        using var session = form.OpenDocument(DefinitionDocumentRepresentation.Environment, "env");
        using var result = session.Parse(Encoding.UTF8.GetBytes(source));
        await Assert.That(result.Succeeded).IsFalse();
        await Assert.That(form.InputRevision).IsEqualTo(revision);
        await Assert.That(form.Snapshot().Values["text"].GetString()).IsEqualTo("old");
        await Assert.That(JsonSerializer.Serialize(result)).DoesNotContain("UNKNOWN");
        await Assert.That(JsonSerializer.Serialize(result)).DoesNotContain("synthetic-private");
    }

    [Test]
    public async Task UnknownKeyDiagnosticsHaveBoundedCoordinatesWithoutPrivateNames()
    {
        using var form = new DefinitionFormModel(Definition());
        using var session = form.OpenDocument(DefinitionDocumentRepresentation.Environment, "env");
        byte[] source = Encoding.UTF8.GetBytes(string.Join('\n', Enumerable.Range(0, 120).Select(i => $"PRIVATE_{i}=value")));
        using var parsed = session.Parse(source);
        await Assert.That(parsed.Diagnostics.Count).IsEqualTo(100);
        await Assert.That(parsed.DiagnosticsTruncated).IsTrue();
        await Assert.That(parsed.Diagnostics[99].Line).IsEqualTo(100);
        await Assert.That(JsonSerializer.Serialize(parsed)).DoesNotContain("PRIVATE");
    }

    [Test]
    [Arguments(DefinitionSessionPrivacy.BrowserNoSecrets, false)]
    [Arguments(DefinitionSessionPrivacy.BrowserSecretsEnabled, true)]
    [Arguments(DefinitionSessionPrivacy.NativeComplete, true)]
    public async Task AcquisitionRequiresRealWorkspacePolicy(DefinitionSessionPrivacy privacy, bool allowed)
    {
        using var form = new DefinitionFormModel(Definition(), privacy);
        using var session = form.OpenDocument();
        bool read = false;
        using var parsed = await session.ParseAsync(_ =>
        {
            read = true;
            return Task.FromResult(Envelope("""{"text":"ok"}"""));
        });
        await Assert.That(read).IsEqualTo(allowed);
        await Assert.That(parsed.Succeeded).IsEqualTo(allowed);
        await Assert.That(session.Coverage.Count > 0).IsTrue();
        if (!allowed) await Assert.That(() => session.ReadSource()).Throws<InvalidOperationException>();
    }

    [Test]
    [Arguments("edit")]
    [Arguments("revoke")]
    [Arguments("replace")]
    [Arguments("dispose")]
    [Arguments("text")]
    [Arguments("session")]
    [Arguments("cancel")]
    public async Task PendingAcquisitionCannotPublishAfterRetirement(string action)
    {
        using var form = new DefinitionFormModel(Definition(), DefinitionSessionPrivacy.BrowserSecretsEnabled);
        using var session = form.OpenDocument();
        using var cancellation = new CancellationTokenSource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<DefinitionDocumentParseResult> pending = session.ParseAsync(_ =>
        {
            entered.SetResult();
            return release.Task;
        }, cancellationToken: cancellation.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        switch (action)
        {
            case "edit": form.SetValue(["text"], JsonSerializer.SerializeToElement("newer")); break;
            case "revoke": form.SetSecretEntryEnabled(false); break;
            case "replace": form.OpenDocument().Dispose(); break;
            case "dispose": form.Dispose(); break;
            case "text": session.InvalidateText(); break;
            case "session": session.Dispose(); break;
            default: cancellation.Cancel(); break;
        }
        byte[] bytes = Envelope("""{"text":"late"}""");
        release.SetResult(bytes);
        using var parsed = await pending.WaitAsync(TimeSpan.FromSeconds(10));
        await Assert.That(parsed.Succeeded).IsFalse();
        await Assert.That(bytes.All(value => value == 0)).IsTrue();
        if (action == "edit") await Assert.That(form.Snapshot().Values["text"].GetString()).IsEqualTo("newer");
    }

    [Test]
    [Arguments("text")]
    [Arguments("edit")]
    [Arguments("revoke")]
    [Arguments("replace")]
    [Arguments("dispose")]
    [Arguments("candidate")]
    [Arguments("cancel")]
    public async Task RetiredCandidateCannotApplyOrRebase(string action)
    {
        using var form = new DefinitionFormModel(Definition(), DefinitionSessionPrivacy.BrowserSecretsEnabled);
        using var session = form.OpenDocument();
        using var result = session.Parse(Envelope("""{"text":"candidate"}"""));
        using var cancellation = new CancellationTokenSource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<DefinitionDocumentApplyResult> pending = Task.Run(async () =>
        {
            entered.SetResult();
            await release.Task;
            return session.Apply(result.Candidate!, cancellation.Token);
        });
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        switch (action)
        {
            case "text": session.InvalidateText(); break;
            case "edit": form.SetValue(["text"], JsonSerializer.SerializeToElement("newer")); break;
            case "revoke": form.SetSecretEntryEnabled(false); break;
            case "replace": form.OpenDocument(DefinitionDocumentRepresentation.Environment, "env").Dispose(); break;
            case "dispose": form.Dispose(); break;
            case "candidate": result.Dispose(); break;
            default: cancellation.Cancel(); break;
        }
        long revision = form.InputRevision;
        release.SetResult();
        DefinitionDocumentApplyResult applied = await pending.WaitAsync(TimeSpan.FromSeconds(10));
        await Assert.That(applied.Succeeded).IsFalse();
        await Assert.That(form.InputRevision).IsEqualTo(revision);
        if (action == "cancel") await Assert.That(session.Apply(result.Candidate!).Succeeded).IsFalse();
        if (action == "edit") await Assert.That(form.Snapshot().Values["text"].GetString()).IsEqualTo("newer");
    }

    [Test]
    public async Task ObserverFailureDoesNotRejectCommitOrUndoReentrantEdit()
    {
        using var form = new DefinitionFormModel(Definition());
        using var session = form.OpenDocument();
        using var parsed = session.Parse(Envelope("""{"text":"candidate"}"""));
        bool edited = false;
        form.InputChanged += (_, _) =>
        {
            if (!edited)
            {
                edited = true;
                form.SetValue(["text"], JsonSerializer.SerializeToElement("newer"));
            }
            throw new InvalidOperationException("synthetic-observer");
        };
        DefinitionDocumentApplyResult result = session.Apply(parsed.Candidate!);
        await Assert.That(result.Succeeded).IsTrue();
        await Assert.That(result.ObserverFailed).IsTrue();
        await Assert.That(form.Snapshot().Values["text"].GetString()).IsEqualTo("newer");
        await Assert.That(form.InputRevision).IsEqualTo(2);
        await Assert.That(JsonSerializer.Serialize(result)).DoesNotContain("synthetic-observer");
    }

    [Test]
    public async Task OriginalParseCancellationRetiresCandidateEvenWithoutApplyToken()
    {
        using var form = new DefinitionFormModel(Definition());
        using var session = form.OpenDocument();
        using var cancellation = new CancellationTokenSource();
        using var parsed = session.Parse(Envelope("""{"text":"candidate"}"""), cancellationToken: cancellation.Token);
        cancellation.Cancel();
        await Assert.That(session.Apply(parsed.Candidate!).Succeeded).IsFalse();
        await Assert.That(form.InputRevision).IsEqualTo(0);
        await Assert.That(form.Snapshot().Values.Count).IsEqualTo(0);
    }

    [Test]
    public async Task NewParseRetiresPriorCandidateAndCopiesCallerMemory()
    {
        using var form = new DefinitionFormModel(Definition());
        using var session = form.OpenDocument();
        byte[] source = Envelope("""{"text":"first"}""");
        using var first = session.Parse(source);
        Array.Fill(source, (byte)'x');
        using var second = session.Parse(Envelope("""{"text":"second"}"""));
        await Assert.That(session.Apply(first.Candidate!).Succeeded).IsFalse();
        await Assert.That(session.Apply(second.Candidate!).Succeeded).IsTrue();
        await Assert.That(form.Snapshot().Values["text"].GetString()).IsEqualTo("second");
        second.Dispose();
        await Assert.That(form.Snapshot().Values["text"].GetString()).IsEqualTo("second");
    }

    [Test]
    public async Task CandidateHasNoCallerMutableAnswerStorage()
    {
        using var form = new DefinitionFormModel(Definition());
        using var session = form.OpenDocument();
        byte[] bytes = Envelope("""{"text":"captured"}""");
        using var parsed = session.Parse(bytes);
        Array.Fill(bytes, (byte)'x');
        await Assert.That(session.Apply(parsed.Candidate!).Succeeded).IsTrue();
        await Assert.That(form.Snapshot().Values["text"].GetString()).IsEqualTo("captured");
    }

    [Test]
    public async Task CanonicalRoundtripRetainsDormantSuppliedAnswersWithoutActivatingThem()
    {
        using var form = new DefinitionFormModel(Definition());
        form.SetValue(["controller"], JsonSerializer.SerializeToElement(true));
        form.SetValue(["active"], JsonSerializer.SerializeToElement("retained"));
        form.SetValue(["controller"], JsonSerializer.SerializeToElement(false));
        using var session = form.OpenDocument();
        using var parsed = session.Parse(session.ReadSource());
        await Assert.That(parsed.Succeeded).IsTrue();
        await Assert.That(session.Apply(parsed.Candidate!).Succeeded).IsTrue();
        await Assert.That(form.Snapshot().Values["active"].GetString()).IsEqualTo("retained");
        await Assert.That(form.Snapshot().Values["controller"].GetBoolean()).IsFalse();
    }

    [Test]
    public async Task ForeignFormCannotAcquireCandidateAuthority()
    {
        using var firstForm = new DefinitionFormModel(Definition());
        using var secondForm = new DefinitionFormModel(Definition());
        using var first = firstForm.OpenDocument();
        using var second = secondForm.OpenDocument();
        using var parsed = first.Parse(Envelope("""{"text":"first"}"""));
        await Assert.That(second.Apply(parsed.Candidate!).Succeeded).IsFalse();
        await Assert.That(secondForm.InputRevision).IsEqualTo(0);
        await Assert.That(first.Apply(parsed.Candidate!).Succeeded).IsTrue();
    }

    [Test]
    public async Task EnvironmentSessionRequiresAnAdmittedArtifactBeforeReplacingExistingLease()
    {
        using var form = new DefinitionFormModel(Definition());
        using var session = form.OpenDocument();
        using var parsed = session.Parse(Envelope("""{"text":"retained"}"""));
        await Assert.That(() => form.OpenDocument(DefinitionDocumentRepresentation.Environment, "synthetic-private-id"))
            .Throws<ArgumentException>();
        await Assert.That(session.Apply(parsed.Candidate!).Succeeded).IsTrue();
    }

    [Test]
    public async Task ControllerEditRetiresEnvironmentScopeAndReopenIncludesNewMapping()
    {
        using var form = new DefinitionFormModel(Definition());
        using var environment = form.OpenDocument(DefinitionDocumentRepresentation.Environment, "env");
        using var parsed = environment.Parse("TEXT=old\n"u8.ToArray());
        await Assert.That(environment.EnvironmentScope!.Mappings.Any(item => item.Key == "ACTIVE")).IsFalse();
        form.SetValue(["controller"], JsonSerializer.SerializeToElement(true));
        await Assert.That(environment.Apply(parsed.Candidate!).Succeeded).IsFalse();
        using var reopened = form.OpenDocument(DefinitionDocumentRepresentation.Environment, "env");
        await Assert.That(reopened.EnvironmentScope!.Mappings.Any(item => item.Key == "ACTIVE")).IsTrue();
        using var next = reopened.Parse("ACTIVE=current\n"u8.ToArray());
        await Assert.That(next.Succeeded).IsTrue();
        await Assert.That(reopened.Apply(next.Candidate!).Succeeded).IsTrue();
        await Assert.That(form.Snapshot().Values["active"].GetString()).IsEqualTo("current");
        await Assert.That(form.Snapshot().Values["controller"].GetBoolean()).IsTrue();
    }

    [Test]
    public async Task PendingOlderReadCannotReplaceNewerParsedCandidate()
    {
        using var form = new DefinitionFormModel(Definition());
        using var session = form.OpenDocument();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<DefinitionDocumentParseResult> pending = session.ParseAsync(_ =>
        {
            entered.SetResult();
            return release.Task;
        });
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        using var current = session.Parse(Envelope("""{"text":"current"}"""));
        release.SetResult(Envelope("""{"text":"stale"}"""));
        using var old = await pending.WaitAsync(TimeSpan.FromSeconds(10));
        await Assert.That(old.Succeeded).IsFalse();
        await Assert.That(session.Apply(current.Candidate!).Succeeded).IsTrue();
        await Assert.That(form.Snapshot().Values["text"].GetString()).IsEqualTo("current");
    }

    [Test]
    [Arguments("edit")]
    [Arguments("text")]
    [Arguments("revoke")]
    [Arguments("replace")]
    [Arguments("dispose")]
    public async Task CompletedParseCannotPublishAfterAQueuedLifecycleChange(string action)
    {
        using var form = new DefinitionFormModel(Definition(), DefinitionSessionPrivacy.BrowserSecretsEnabled);
        using var session = form.OpenDocument();
        var parsed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        byte[] bytes = Envelope("""{"text":"parsed"}""");
        Task<DefinitionDocumentParseResult> pending = session.ParseAsync(_ => Task.FromResult(bytes),
            cooperativeYield: _ =>
            {
                parsed.SetResult();
                return new ValueTask(release.Task);
            });
        await parsed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await Assert.That(bytes.All(value => value == 0)).IsTrue();
        switch (action)
        {
            case "edit": form.SetValue(["text"], JsonSerializer.SerializeToElement("newer")); break;
            case "text": session.InvalidateText(); break;
            case "revoke": form.SetSecretEntryEnabled(false); break;
            case "replace": form.OpenDocument().Dispose(); break;
            default: form.Dispose(); break;
        }
        long revision = form.InputRevision;
        release.SetResult();
        using var result = await pending.WaitAsync(TimeSpan.FromSeconds(10));
        await Assert.That(result.Succeeded).IsFalse();
        await Assert.That(result.Candidate).IsNull();
        await Assert.That(form.InputRevision).IsEqualTo(revision);
        if (action == "edit") await Assert.That(form.Snapshot().Values["text"].GetString()).IsEqualTo("newer");
    }

    [Test]
    public async Task FailedReadReturnsOnlySafeDiagnostic()
    {
        using var form = new DefinitionFormModel(Definition());
        using var session = form.OpenDocument();
        using var result = await session.ParseAsync(_ => throw new IOException("synthetic-private-path"));
        await Assert.That(result.Diagnostics.Single().Code).IsEqualTo("document-read-failed");
        await Assert.That(JsonSerializer.Serialize(result)).DoesNotContain("synthetic-private-path");
        await Assert.That(form.InputRevision).IsEqualTo(0);
    }

    [Test]
    public async Task SourceCannotSilentlyReplaceAnInvalidSuppliedValue()
    {
        using var form = new DefinitionFormModel(Definition());
        form.SetValue(["count"], JsonSerializer.SerializeToElement("invalid"));
        using var session = form.OpenDocument();
        await Assert.That(() => session.ReadSource()).Throws<InvalidOperationException>();
        await Assert.That(form.Snapshot().Values["count"].GetString()).IsEqualTo("invalid");
    }

    private static byte[] Envelope(string values, string omitted = "[]") =>
        Encoding.UTF8.GetBytes($$"""{"answerContractVersion":"0.0.1","values":{{values}},"omitted":{{omitted}}}""");

    private static Definition Definition() => DefinitionSourceCodec.ParseJson("""
        {
          "$schema":"urn:islamu:configurator:authoring:v0.0.1",
          "identity":{"familyId":"198d708d-caba-4e48-a00d-b06242151617","revision":1},
          "project":{"id":"document","name":"Document"},"applicationVersions":["1"],
          "configuration":{"type":"object","additionalProperties":false,
            "x-configurator":{"sensitivity":"public"},
            "properties":{
              "text":{"type":"string","x-configurator":{"sensitivity":"public"}},
              "literal.dot":{"type":"string","x-configurator":{"sensitivity":"public"}},
              "count":{"type":"integer","x-configurator":{"sensitivity":"public"}},
              "flag":{"type":"boolean","x-configurator":{"sensitivity":"public"}},
              "nullable":{"type":["string","null"],"x-configurator":{"sensitivity":"public"}},
              "reset":{"type":"string","x-configurator":{"sensitivity":"public","initialValue":"default"}},
              "omit":{"type":"string","x-configurator":{"sensitivity":"public","initialValue":"default"}},
              "secret":{"type":"string","writeOnly":true},
              "controller":{"type":"boolean","x-configurator":{"sensitivity":"public","initialValue":false}},
              "active":{"type":"string","x-configurator":{"sensitivity":"public",
                "applicableWhen":{"op":"equals","reference":{"scope":"root","path":"/controller"},"value":true}}},
              "rows":{"type":"array","x-configurator":{"sensitivity":"public"},
                "items":{"type":"string","x-configurator":{"sensitivity":"public"}}},
              "pair":{"type":"object","additionalProperties":false,"x-configurator":{"sensitivity":"public"},
                "properties":{
                  "left":{"type":"array","x-configurator":{"sensitivity":"public"},"items":{"type":"string","x-configurator":{"sensitivity":"public"}}},
                  "right":{"type":"array","x-configurator":{"sensitivity":"public"},"items":{"type":"string","x-configurator":{"sensitivity":"public"}}}}},
              "mixed":{"type":"object","additionalProperties":false,"x-configurator":{"sensitivity":"public"},
                "properties":{"open":{"type":"string","x-configurator":{"sensitivity":"public"}},
                  "hidden":{"type":"string","writeOnly":true},"hiddenCount":{"type":"integer","writeOnly":true}}},
              "mixedRows":{"type":"array","x-configurator":{"sensitivity":"public"},
                "items":{"type":"object","additionalProperties":false,"x-configurator":{"sensitivity":"public"},
                  "properties":{"open":{"type":"string","x-configurator":{"sensitivity":"public"}},"hidden":{"type":"string","writeOnly":true}}}},
              "requiredObject":{"type":"object","additionalProperties":false,"required":["needed"],
                "x-configurator":{"sensitivity":"public"},"properties":{
                  "needed":{"type":"string","x-configurator":{"sensitivity":"public"}},
                  "optional":{"type":"string","x-configurator":{"sensitivity":"public"}}}}
            }},
          "outputs":{"config.env":{"id":"env","format":"dotenv","projection":{"op":"object","members":{
            "TEXT":{"op":"copy","reference":{"scope":"root","path":"/text"}},
            "COUNT":{"op":"copy","reference":{"scope":"root","path":"/count"}},
            "DOT":{"op":"copy","reference":{"scope":"root","path":"/literal.dot"}},
            "CONTROLLER":{"op":"copy","reference":{"scope":"root","path":"/controller"}},
            "ACTIVE":{"op":"copy","reference":{"scope":"root","path":"/active"}},
            "LITERAL":{"op":"literal","value":"fixed"}
          }}},
          "config.json":{"id":"json","format":"json","projection":{"op":"object","members":{
            "pair":{"op":"copy","reference":{"scope":"root","path":"/pair"}}
          }}}}
        }
        """u8.ToArray());
}
