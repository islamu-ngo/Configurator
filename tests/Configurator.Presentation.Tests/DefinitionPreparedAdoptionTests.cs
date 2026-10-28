namespace Configurator.Presentation.Tests;

using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using CommunityToolkit.Mvvm.Messaging;
using Configurator.Core.Artifacts;
using Configurator.Core.Definitions;

public sealed class DefinitionPreparedAdoptionTests
{
    private static readonly TimeSpan SignalTimeout = TimeSpan.FromSeconds(10);

    [Test]
    public async Task NeverStartedAdoptionClearsItsOwnedBytesAndCannotExecute()
    {
        using DefinitionArtifact artifact = Prepare();
        using DefinitionPreparedAdoption operation = DefinitionPresentationOperation.Adopt(artifact);
        ReadOnlyMemory<byte> owned = PendingBytes(operation);
        await Assert.That(owned.Span.SequenceEqual("""{"name":"sample"}"""u8)).IsTrue();

        operation.Dispose();
        operation.Dispose();

        await Assert.That(owned.ToArray().All(value => value == 0)).IsTrue();
        await Assert.That(async () => { await operation.ExecuteAsync(CancellationToken.None); }).Throws<ObjectDisposedException>();
        await Assert.That(artifact.Bytes.Span.SequenceEqual("""{"name":"sample"}"""u8)).IsTrue();
    }

    [Test]
    public async Task CancellationBeforeStartRetiresTheStagedCopy()
    {
        using DefinitionArtifact artifact = Prepare();
        using DefinitionPreparedAdoption operation = DefinitionPresentationOperation.Adopt(artifact);
        ReadOnlyMemory<byte> owned = PendingBytes(operation);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.That(async () => { await operation.ExecuteAsync(cancellation.Token); }).Throws<OperationCanceledException>();
        await Assert.That(owned.ToArray().All(value => value == 0)).IsTrue();
    }

    [Test]
    public async Task AdoptionTransfersOnceAndOperationDisposalCannotClearWorkspaceOutput()
    {
        using DefinitionArtifact artifact = Prepare();
        using DefinitionPreparedAdoption operation = DefinitionPresentationOperation.Adopt(artifact);
        artifact.Dispose();
        using SetupPresentationOutcome outcome = await operation.ExecuteAsync(CancellationToken.None);
        ReadOnlyMemory<byte> transferred = outcome.Output;
        operation.Dispose();

        await Assert.That(transferred.Span.SequenceEqual("""{"name":"sample"}"""u8)).IsTrue();
        await Assert.That(async () => { await operation.ExecuteAsync(CancellationToken.None); }).Throws<ObjectDisposedException>();
        outcome.Dispose();
        await Assert.That(transferred.ToArray().All(value => value == 0)).IsTrue();
    }

    [Test]
    [Arguments("cancel")]
    [Arguments("deactivate")]
    [Arguments("dispose")]
    public async Task LateAdoptedOutcomeCannotSurviveWorkspaceInvalidation(string invalidation)
    {
        using DefinitionArtifact artifact = Prepare();
        using DefinitionPreparedAdoption adoption = DefinitionPresentationOperation.Adopt(artifact);
        using SetupPresentationOutcome outcome = await adoption.ExecuteAsync(CancellationToken.None);
        ReadOnlyMemory<byte> capturedBeforeRelease = outcome.Output;
        var release = new TaskCompletionSource<SetupPresentationOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
        var discarded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var session = new SetupPresentationSession(new WeakReferenceMessenger());
        SetupWorkspaceId.TryCreate("prepared-late", out var id);
        using SetupPresentationWorkspace workspace = session.CreateWorkspace(id, new HeldOperation(release.Task));
        workspace.CompletionDiscarded += (_, _) => discarded.TrySetResult();
        workspace.Activate();
        Task execution = workspace.ExecuteAsync(Guid.NewGuid());

        if (invalidation == "cancel") workspace.Cancel();
        else if (invalidation == "deactivate") workspace.Deactivate();
        else workspace.Dispose();
        await execution.WaitAsync(SignalTimeout);
        release.SetResult(outcome);
        await discarded.Task.WaitAsync(SignalTimeout);
        await workspace.WaitForPendingOperationsAsync().WaitAsync(SignalTimeout);

        await Assert.That(workspace.Output.IsEmpty).IsTrue();
        await Assert.That(workspace.Result).IsNull();
        await Assert.That(capturedBeforeRelease.ToArray().All(value => value == 0)).IsTrue();
    }

    [Test]
    public async Task ReplacementGenerationRejectsAnOlderAdoptedOutcome()
    {
        using DefinitionArtifact artifact = Prepare();
        using DefinitionPreparedAdoption first = DefinitionPresentationOperation.Adopt(artifact);
        using DefinitionPreparedAdoption second = DefinitionPresentationOperation.Adopt(artifact);
        using SetupPresentationOutcome old = await first.ExecuteAsync(CancellationToken.None);
        using SetupPresentationOutcome current = await second.ExecuteAsync(CancellationToken.None);
        ReadOnlyMemory<byte> oldBytes = old.Output;
        ReadOnlyMemory<byte> currentBytes = current.Output;
        var release = new TaskCompletionSource<SetupPresentationOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
        var discarded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var session = new SetupPresentationSession(new WeakReferenceMessenger());
        SetupWorkspaceId.TryCreate("prepared-replacement", out var id);
        using SetupPresentationWorkspace workspace = session.CreateWorkspace(id, new ReplacingOperation(release.Task, current));
        workspace.CompletionDiscarded += (_, _) => discarded.TrySetResult();
        Task previousExecution = workspace.ExecuteAsync(Guid.NewGuid());
        await workspace.ExecuteAsync(Guid.NewGuid()).WaitAsync(SignalTimeout);
        release.SetResult(old);
        await previousExecution.WaitAsync(SignalTimeout);
        await discarded.Task.WaitAsync(SignalTimeout);
        await workspace.WaitForPendingOperationsAsync().WaitAsync(SignalTimeout);

        await Assert.That(ReferenceEquals(workspace.Result, current.CoreResult)).IsTrue();
        await Assert.That(workspace.Output.Span.SequenceEqual("""{"name":"sample"}"""u8)).IsTrue();
        await Assert.That(oldBytes.ToArray().All(value => value == 0)).IsTrue();
        workspace.Dispose();
        await Assert.That(currentBytes.ToArray().All(value => value == 0)).IsTrue();
    }

    [Test]
    public async Task OneMeasuredCompositionSuppliesAnIndependentAdoptedCopy()
    {
        using var activity = new Activity(nameof(OneMeasuredCompositionSuppliesAnIndependentAdoptedCopy));
        activity.Start();
        using var measurement = new PreparationMeasurement(activity);
        using DefinitionArtifact artifact = Prepare();
        using DefinitionPreparedAdoption adoption = DefinitionPresentationOperation.Adopt(artifact);
        artifact.Dispose();
        using SetupPresentationOutcome outcome = await adoption.ExecuteAsync(CancellationToken.None);
        var metadata = (DefinitionPreparationMetadata)outcome.CoreResult;

        await Assert.That(outcome.Output.Span.SequenceEqual("""{"name":"sample"}"""u8)).IsTrue();
        await Assert.That(metadata.InputRevision).IsEqualTo(7L);
        await Assert.That(metadata.Digest!.Value.Value)
            .IsEqualTo("976cd2d13306ef7b411425368f511318aed70ea05278b4e7559099f7820eaf1b");
        await Assert.That(measurement.Events.Count(item => item == "ArtifactComposed")).IsEqualTo(1);
        await Assert.That(measurement.Events.Count(item => item == "ValidationCompleted")).IsEqualTo(1);
    }

    [Test]
    public async Task RejectedWorkspaceCreationLeavesStagedOwnersResponsibleForCleanup()
    {
        using DefinitionArtifact artifact = Prepare();
        using DefinitionPreparedAdoption first = DefinitionPresentationOperation.Adopt(artifact);
        using DefinitionPreparedAdoption second = DefinitionPresentationOperation.Adopt(artifact);
        ReadOnlyMemory<byte> firstBytes = PendingBytes(first);
        ReadOnlyMemory<byte> secondBytes = PendingBytes(second);
        using var session = new SetupPresentationSession(new WeakReferenceMessenger());
        session.Dispose();
        SetupWorkspaceId.TryCreate("rejected-adoption", out var id);
        try
        {
            await Assert.That(() =>
            {
                using SetupPresentationWorkspace workspace = session.CreateWorkspace(id, first);
            }).Throws<ObjectDisposedException>();
        }
        finally
        {
            first.Dispose();
            second.Dispose();
        }
        await Assert.That(firstBytes.ToArray().All(value => value == 0)).IsTrue();
        await Assert.That(secondBytes.ToArray().All(value => value == 0)).IsTrue();
    }

    [Test]
    public async Task DraftAdoptionSnapshotsOnlyItsOwnCompletionLocations()
    {
        SetupDefinition definition = Definition(secret: true);
        using DefinitionDraftArtifactResult result = DefinitionDraftArtifactComposer.Compose(definition, "config",
            new SetupAnswerSnapshot(9, new Dictionary<string, JsonElement>()));
        var completions = new List<DefinitionDraftCompletion>(result.Completions)
        {
            new("another", "unrelated", "/unrelated")
        };
        using DefinitionPreparedAdoption adoption = DefinitionPresentationOperation.Adopt(result.Artifact!, completions);
        completions.Clear();
        result.Dispose();
        using SetupPresentationOutcome outcome = await adoption.ExecuteAsync(CancellationToken.None);
        var metadata = (DefinitionPreparationMetadata)outcome.CoreResult;

        await Assert.That(metadata.Succeeded).IsTrue();
        await Assert.That(metadata.IsDraft).IsTrue();
        await Assert.That(metadata.InputRevision).IsEqualTo(9L);
        await Assert.That(metadata.Completions.Single()).IsEqualTo(new DefinitionDraftCompletion("config", "/name", "/name"));
        await Assert.That(outcome.Output.Span.SequenceEqual("""{"name":""}"""u8)).IsTrue();
        await Assert.That(metadata.Digest!.Value.Value)
            .IsEqualTo("1390696a77e5d6f4375e9b36450c26bb67e99662fcd31b71cecd8ceda332040e");
    }

    private static ReadOnlyMemory<byte> PendingBytes(DefinitionPreparedAdoption operation) =>
        ((SetupPresentationOutcome)typeof(DefinitionPreparedAdoption)
            .GetField("_prepared", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(operation)!).Output;

    private static DefinitionArtifact Prepare()
    {
        SetupDefinition definition = Definition();
        var answers = new SetupAnswerSnapshot(7, new Dictionary<string, JsonElement>
        {
            ["name"] = JsonSerializer.SerializeToElement("sample")
        });
        return DefinitionArtifactComposer.Compose(definition, "config", answers).Artifact!;
    }

    private static SetupDefinition Definition(bool secret = false) => SetupDefinitionCodec.ParseJson(
        Encoding.UTF8.GetBytes("""
            {"schemaVersion":2,"profile":"bounded-v2","familyId":"e201806e-18ec-40ee-8540-f5d1fa742ce1","revision":1,
             "project":{"id":"example.service","publisher":"example","name":"Example"},
             "applicationVersions":["1.0"],"entryPoints":["setup"],"locales":["en"],
             "configuration":{"type":"object","additionalProperties":false,"required":["name"],
               "x-configurator":{"sensitivity":"public"},"properties":{
                 "name":{"type":"string","title":"Name",
                   "x-configurator":{"sensitivity":"secret","detail":"standard"}}}},
             "artifacts":[{"id":"config","format":"json","fileName":"config.json","targetUpdateMode":"create","restartRequirement":"none",
               "projection":{"op":"object","members":{"name":{"op":"copy","reference":{"scope":"root","path":"/name"}}}}}],
             "requiredCapabilities":["projection.copy","projection.object","schema.object","schema.string"]}
            """.Replace("\"sensitivity\":\"secret\"", secret ? "\"sensitivity\":\"secret\"" : "\"sensitivity\":\"public\"",
                StringComparison.Ordinal)));

    private sealed class HeldOperation(Task<SetupPresentationOutcome> pending) : ISetupPresentationOperation
    {
        public Task<SetupPresentationOutcome> ExecuteAsync(CancellationToken cancellationToken) => pending;
    }

    private sealed class ReplacingOperation(Task<SetupPresentationOutcome> first, SetupPresentationOutcome replacement)
        : ISetupPresentationOperation
    {
        private bool _started;
        public Task<SetupPresentationOutcome> ExecuteAsync(CancellationToken cancellationToken)
        {
            if (_started) return Task.FromResult(replacement);
            _started = true;
            return first;
        }
    }

    private sealed class PreparationMeasurement : IObserver<DiagnosticListener>,
        IObserver<KeyValuePair<string, object?>>, IDisposable
    {
        private readonly Activity _activity;
        private readonly IDisposable _listeners;
        private readonly List<IDisposable> _subscriptions = [];
        public PreparationMeasurement(Activity activity)
        {
            _activity = activity;
            _listeners = DiagnosticListener.AllListeners.Subscribe(this);
        }
        public List<string> Events { get; } = [];
        public void OnNext(DiagnosticListener value)
        {
            if (value.Name == "Configurator.Core.Preparation") _subscriptions.Add(value.Subscribe(this));
        }
        public void OnNext(KeyValuePair<string, object?> value)
        {
            if (ReferenceEquals(Activity.Current, _activity)) Events.Add(value.Key);
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
