namespace Configurator.Desktop.Tests;

using Configurator.Artifacts;
using Configurator.Core.Artifacts;
using CommunityToolkit.Mvvm.Messaging;
using Configurator.Desktop.Messages;
using Configurator.Desktop.ViewModels;

public sealed class DesktopPublicationLifetimeTests
{
    [Test]
    [Arguments(false, "cancel")]
    [Arguments(true, "cancel")]
    [Arguments(false, "token")]
    [Arguments(true, "token")]
    [Arguments(false, "edit")]
    [Arguments(true, "edit")]
    [Arguments(false, "dispose")]
    [Arguments(true, "dispose")]
    public async Task RetiredPreparationSettlesWithoutRunningQueuedOwnerAdoption(bool bundle, string retirement)
    {
        var owner = new HeldOwnerContext();
        using var cancellation = new CancellationTokenSource();
        using var shell = DesktopTestContext.Root(protectedOutputAvailable: true);
        shell.Loader.LoadDefinition(DesktopApplicationTests.Definition());
        Task<DesktopPreparationOutcome> preparing = Task.FromResult(DesktopPreparationOutcome.Invalid);
        owner.Run(() => preparing = bundle
            ? shell.Publisher.PrepareBundleAsync(cancellation.Token)
            : shell.Publisher.PrepareAsync(cancellation.Token));
        await owner.Posted.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Task physical = shell.ActiveCompletion;
        await Assert.That(physical.IsCompleted).IsFalse();
        await Assert.That(shell.IsBusy).IsTrue();
        owner.Run(() =>
        {
            switch (retirement)
            {
                case "cancel": shell.Cancel(); break;
                case "token": cancellation.Cancel(); break;
                case "edit": shell.Workspace.Form!.Fields.Single(field => field.Definition.Id == "name").Text = "Changed"; break;
                case "dispose": shell.Dispose(); break;
            }
        });
        try
        {
            await physical.WaitAsync(TimeSpan.FromSeconds(10));
            await Assert.That(await preparing.WaitAsync(TimeSpan.FromSeconds(10))).IsEqualTo(
                retirement is "cancel" or "token" ? DesktopPreparationOutcome.Cancelled : DesktopPreparationOutcome.Retired);
            await Assert.That(shell.Publisher.Preparation).IsNull();
            await Assert.That(shell.Publisher.CanSave || shell.Publisher.CanSaveBundle).IsFalse();
            owner.Drain();
            await Assert.That(shell.Publisher.Preparation).IsNull();
            await Assert.That(shell.Publisher.CanSave || shell.Publisher.CanSaveBundle).IsFalse();
            await Assert.That(shell.IsBusy).IsFalse();
        }
        finally { owner.Drain(); }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task PreparationAdoptsOnlyOnItsCapturedOwner(bool bundle)
    {
        var owner = new HeldOwnerContext();
        using var shell = DesktopTestContext.Root(protectedOutputAvailable: true);
        shell.Loader.LoadDefinition(DesktopApplicationTests.Definition());
        bool offOwnerNotification = false;
        shell.Publisher.PropertyChanged += (_, _) =>
            offOwnerNotification |= !ReferenceEquals(SynchronizationContext.Current, owner);
        Task<DesktopPreparationOutcome> preparing = Task.FromResult(DesktopPreparationOutcome.Invalid);
        owner.Run(() => preparing = bundle ? shell.Publisher.PrepareBundleAsync() : shell.Publisher.PrepareAsync());
        await owner.Posted.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await Assert.That(shell.Publisher.Preparation).IsNull();
        await Assert.That(shell.Publisher.CanSave || shell.Publisher.CanSaveBundle).IsFalse();
        owner.Drain();
        await Assert.That(await preparing.WaitAsync(TimeSpan.FromSeconds(10))).IsEqualTo(DesktopPreparationOutcome.Prepared);
        owner.Drain();
        await Assert.That(bundle ? shell.Publisher.CanSaveBundle : shell.Publisher.CanSave).IsTrue();
        await Assert.That(offOwnerNotification).IsFalse();
        owner.Run(shell.Dispose);
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task PreparationFaultIsTypedAndCannotLeavePreparedOutput(bool bundle, bool unrequestedCancellation)
    {
        var messenger = new WeakReferenceMessenger();
        using var shell = DesktopTestContext.Root(messenger: messenger, protectedOutputAvailable: true);
        shell.Loader.LoadDefinition(DesktopApplicationTests.Definition());
        var observer = new object();
        messenger.Register<object, StatusChangedMessage>(observer, (_, message) =>
        {
            if (!message.Status.StartsWith("artifact-ready:", StringComparison.Ordinal) &&
                !message.Status.StartsWith("bundle-selected:", StringComparison.Ordinal)) return;
            if (unrequestedCancellation) throw new OperationCanceledException("synthetic-private-fault");
            throw new InvalidOperationException("synthetic-private-fault");
        });
        DesktopPreparationOutcome result = await (bundle
            ? shell.Publisher.PrepareBundleAsync() : shell.Publisher.PrepareAsync());
        await Assert.That(result).IsEqualTo(DesktopPreparationOutcome.Faulted);
        await Assert.That(shell.Publisher.Preparation).IsNull();
        await Assert.That(shell.Publisher.CanSave || shell.Publisher.CanSaveBundle).IsFalse();
        await Assert.That(shell.IsBusy).IsFalse();
        await Assert.That(shell.Status).IsEqualTo("preparation-fault");
        messenger.UnregisterAll(observer);
        await Assert.That(await (bundle ? shell.Publisher.PrepareBundleAsync() : shell.Publisher.PrepareAsync()))
            .IsEqualTo(DesktopPreparationOutcome.Prepared);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task SavePhysicallySettlesAfterOwnerContextStops(bool bundle)
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var owner = new HeldOwnerContext();
        ReadOnlyMemory<byte> borrowed = default;
        DefinitionArtifact? borrowedArtifact = null;
        using var shell = DesktopTestContext.Root(save: async (_, bytes, _) =>
        {
            borrowed = bytes;
            entered.TrySetResult();
            await release.Task.WaitAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
            return ProtectedArtifactCommitResult.Installed(false, cleanupFailed: true);
        }, protectedOutputAvailable: true, publishBundle: async (artifacts, _, observe, _) =>
        {
            borrowedArtifact = artifacts[0];
            observe(new("environment", ProtectedArtifactCommitResult.Installed(false, cleanupFailed: true)));
            entered.TrySetResult();
            await release.Task.WaitAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
            throw new IOException("synthetic-private-fault");
        });
        shell.Loader.LoadDefinition(DesktopApplicationTests.Definition());
        shell.Publisher.ArtifactId = "environment";
        shell.Publisher.SelectArtifact("config", false);
        shell.Publisher.SelectArtifact("environment", true);
        await (bundle ? shell.Publisher.PrepareBundleAsync() : shell.Publisher.PrepareAsync());
        Task saving = Task.CompletedTask;
        owner.Run(() => saving = bundle ? shell.Publisher.SaveBundleAsync() : shell.Publisher.SaveAsync());
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Task physical = shell.ActiveCompletion;
        owner.Run(shell.Dispose);
        bool offOwnerNotification = false;
        shell.PropertyChanged += (_, _) =>
            offOwnerNotification |= !ReferenceEquals(SynchronizationContext.Current, owner);
        release.TrySetResult();
        try
        {
            await physical.WaitAsync(TimeSpan.FromSeconds(5));
            await saving.WaitAsync(TimeSpan.FromSeconds(5));
            if (bundle) await Assert.That(() => borrowedArtifact!.Bytes).Throws<ObjectDisposedException>();
            else await Assert.That(borrowed.ToArray().All(value => value == 0)).IsTrue();
            await Assert.That(shell.Publisher.PublicationHistory.Single().IsSettled).IsTrue();
            await Assert.That(shell.Publisher.PublicationHistory.Single().Outcomes.Single()
                .Result!.InstallationEstablished).IsTrue();
            await Assert.That(offOwnerNotification).IsFalse();
        }
        finally { owner.Drain(); }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task SaveConsumerRunsOutsideOwnerSynchronizationContext(bool bundle)
    {
        SynchronizationContext? consumerContext = null;
        using var shell = DesktopTestContext.Root(save: (_, _, _) =>
        {
            consumerContext = SynchronizationContext.Current;
            return Task.FromResult(ProtectedArtifactCommitResult.Installed(true));
        }, protectedOutputAvailable: true, publishBundle: (_, _, _, _) =>
        {
            consumerContext = SynchronizationContext.Current;
            return Task.FromException<ProtectedArtifactBundleResult>(new IOException("synthetic-save-fault"));
        });
        shell.Loader.LoadDefinition(DesktopApplicationTests.Definition());
        shell.Publisher.ArtifactId = "environment";
        await Assert.That(await (bundle ? shell.Publisher.PrepareBundleAsync() : shell.Publisher.PrepareAsync()))
            .IsEqualTo(DesktopPreparationOutcome.Prepared);
        SynchronizationContext? previous = SynchronizationContext.Current;
        Task saving;
        SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());
        try { saving = bundle ? shell.Publisher.SaveBundleAsync() : shell.Publisher.SaveAsync(); }
        finally { SynchronizationContext.SetSynchronizationContext(previous); }
        await saving.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(consumerContext).IsNull();
        await Assert.That(shell.Publisher.PublicationHistory.Single().IsSettled).IsTrue();
    }

    [Test]
    [Arguments("cancel")]
    [Arguments("edit")]
    [Arguments("dispose")]
    public async Task BorrowedSaveBytesSurviveInvalidationUntilConsumerSettles(string invalidation)
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        ReadOnlyMemory<byte> borrowed = default;
        byte[] consumed = [];
        using var shell = DesktopTestContext.Root(save: async (_, bytes, _) =>
        {
            borrowed = bytes;
            entered.TrySetResult();
            await release.Task.WaitAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
            consumed = bytes.ToArray();
            return ProtectedArtifactCommitResult.Installed(false, cleanupFailed: true);
        }, protectedOutputAvailable: true);
        shell.Loader.LoadDefinition(DesktopApplicationTests.Definition());
        shell.Publisher.ArtifactId = "environment";
        await Assert.That(await shell.Publisher.PrepareAsync()).IsEqualTo(DesktopPreparationOutcome.Prepared);
        Task<ProtectedArtifactCommitResult> saving = shell.Publisher.SaveAsync();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try
        {
            if (invalidation == "dispose")
            {
                shell.Dispose();
                shell.Dispose();
            }
            else if (invalidation == "edit")
                shell.Workspace.Form!.Fields.Single(field => field.Definition.Id == "name").Text = "Changed";
            else shell.Cancel();
            await Assert.That(borrowed.Span.SequenceEqual("NAME=Example service\n"u8)).IsTrue();
            await Assert.That(shell.IsBusy).IsTrue();
            await Assert.That(shell.Publisher.CanSave).IsFalse();
            if (invalidation != "dispose")
                await Assert.That(await shell.Publisher.PrepareAsync()).IsEqualTo(DesktopPreparationOutcome.Busy);
        }
        finally { release.TrySetResult(); }
        ProtectedArtifactCommitResult result = await saving.WaitAsync(TimeSpan.FromSeconds(10));
        await Assert.That(consumed.AsSpan().SequenceEqual("NAME=Example service\n"u8)).IsTrue();
        await Assert.That(result.InstallationEstablished).IsTrue();
        await Assert.That(result.CleanupFailed).IsTrue();
        await Assert.That(borrowed.ToArray().All(value => value == 0)).IsTrue();
        await Assert.That(shell.IsBusy).IsFalse();
        await Assert.That(shell.Publisher.CanSave).IsFalse();
        ProtectedArtifactPublicationReceipt receipt = shell.Publisher.PublicationHistory.Single();
        await Assert.That(receipt.IsSettled).IsTrue();
        await Assert.That(receipt.Outcomes.Single().Result).IsEqualTo(result);
        await Assert.That(receipt.CleanupFailed).IsTrue();
        await Assert.That(shell.Publisher.PublicationSummary).DoesNotContain("Example service");
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task DefinitionAndImportReadsRetainBorrowedMemoryAndCancellationUntilCleanup(bool definitionRead)
    {
        byte[] input = definitionRead ? DesktopApplicationTests.Definition() : """{"name":"Imported"}"""u8.ToArray();
        using var stream = new HeldReadStream(input);
        using var shell = DesktopTestContext.Root(
            openDefinition: definitionRead ? _ => stream : null,
            openImport: definitionRead ? null : _ => stream);
        Task<bool> loading;
        if (definitionRead)
        {
            shell.Loader.DefinitionPath = "synthetic.json";
            loading = shell.Loader.LoadDefinitionAsync();
        }
        else
        {
            shell.Loader.LoadDefinition(DesktopApplicationTests.Definition());
            shell.ImportReview.ExistingFilePath = "synthetic.json";
            loading = shell.ImportReview.ImportFileAsync();
        }
        await stream.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Task settlement = shell.ActiveCompletion;
        try
        {
            shell.Dispose();
            shell.Dispose();
            await Assert.That(stream.Borrowed.Span[..input.Length].SequenceEqual(input)).IsTrue();
            await Assert.That(settlement.IsCompleted).IsFalse();
            await Assert.That(shell.IsBusy).IsTrue();
            // A disposed CTS would make registration throw while the consumer still owns its token.
            using CancellationTokenRegistration registration = stream.Token.Register(() => { });
        }
        finally { stream.Release.TrySetResult(); }
        await Assert.That(await loading.WaitAsync(TimeSpan.FromSeconds(10))).IsFalse();
        await settlement.WaitAsync(TimeSpan.FromSeconds(10));
        await Assert.That(stream.Borrowed.ToArray().All(value => value == 0)).IsTrue();
        await Assert.That(stream.CloseCount).IsEqualTo(1);
        await Assert.That(shell.Workspace.Form).IsNull();
        await Assert.That(shell.IsBusy).IsFalse();
    }

    [Test]
    public async Task FaultedBundleRetainsObservedPrefixAndBorrowedArtifactsAfterRepeatedDisposal()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        DefinitionArtifact? borrowed = null;
        byte[] consumed = [];
        using var shell = DesktopTestContext.Root(protectedOutputAvailable: true,
            publishBundle: async (artifacts, _, observe, _) =>
            {
                borrowed = artifacts[0];
                observe(new("environment", ProtectedArtifactCommitResult.Installed(false, cleanupFailed: true)));
                entered.TrySetResult();
                await release.Task.WaitAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
                consumed = borrowed.Bytes.ToArray();
                throw new IOException("synthetic-private-path-and-exception");
            });
        shell.Loader.LoadDefinition(DesktopApplicationTests.Definition());
        shell.Publisher.SelectArtifact("config", false);
        shell.Publisher.SelectArtifact("environment", true);
        shell.Publisher.SelectArtifact("yaml", true);
        await Assert.That(await shell.Publisher.PrepareBundleAsync()).IsEqualTo(DesktopPreparationOutcome.Prepared);
        Task<ProtectedArtifactBundleResult?> saving = shell.Publisher.SaveBundleAsync();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Task settlement = shell.ActiveCompletion;
        try
        {
            shell.Dispose();
            shell.Dispose();
            await Assert.That(settlement.IsCompleted).IsFalse();
            ProtectedArtifactPublicationReceipt active = shell.Publisher.CurrentPublication!;
            await Assert.That(active.Outcomes[0].Result!.InstallationEstablished).IsTrue();
            await Assert.That(active.Outcomes[0].Result!.VerificationSucceeded).IsFalse();
            await Assert.That(active.Outcomes[1].Result).IsNull();
            await Assert.That(active.IsSettled).IsFalse();
            await Assert.That(active.CleanupFailed).IsTrue();
        }
        finally { release.TrySetResult(); }
        await Assert.That(await saving.WaitAsync(TimeSpan.FromSeconds(10))).IsNull();
        await settlement.WaitAsync(TimeSpan.FromSeconds(10));
        await Assert.That(consumed.AsSpan().SequenceEqual("NAME=Example service\n"u8)).IsTrue();
        await Assert.That(() => borrowed!.Bytes).Throws<ObjectDisposedException>();
        ProtectedArtifactPublicationReceipt receipt = shell.Publisher.PublicationHistory.Single();
        await Assert.That(receipt.Failed && receipt.IsUnknown && receipt.IsSettled).IsTrue();
        await Assert.That(receipt.Outcomes[0].Result!.InstallationEstablished).IsTrue();
        await Assert.That(shell.Publisher.PublicationSummary).DoesNotContain("synthetic-private");
        await Assert.That(shell.Publisher.PublicationSummary).DoesNotContain("Example service");
    }

    [Test]
    public async Task RealBundleObserverRetainsInstalledPrefixWhileFollowingWorkIsPending()
    {
        if (!OperatingSystem.IsLinux()) return;
        string directory = Directory.CreateTempSubdirectory("desktop-prefix-").FullName;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            using var shell = DesktopTestContext.Root(outputDirectory: directory, protectedOutputAvailable: true,
                publishBundle: async (artifacts, destination, observe, token) =>
                {
                    await Task.Yield();
                    return await ProtectedArtifactBundlePublisher.PublishAsync(new ProtectedArtifactWriter(),
                        artifacts, destination, outcome =>
                        {
                            observe(outcome);
                            if (outcome.ArtifactId != "environment") return;
                            entered.TrySetResult();
                            release.Task.WaitAsync(TimeSpan.FromSeconds(10), CancellationToken.None).GetAwaiter().GetResult();
                        }, token);
                });
            shell.Loader.LoadDefinition(DesktopApplicationTests.Definition());
            shell.Publisher.SelectArtifact("config", false);
            shell.Publisher.SelectArtifact("environment", true);
            shell.Publisher.SelectArtifact("yaml", true);
            await Assert.That(await shell.Publisher.PrepareBundleAsync()).IsEqualTo(DesktopPreparationOutcome.Prepared);
            Task<ProtectedArtifactBundleResult?> saving = shell.Publisher.SaveBundleAsync();
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            try
            {
                shell.Cancel();
                ProtectedArtifactPublicationReceipt active = shell.Publisher.CurrentPublication!;
                await Assert.That(active.Outcomes[0].Result!.Succeeded).IsTrue();
                await Assert.That(active.Outcomes[1].Result).IsNull();
                await Assert.That(active.IsSettled).IsFalse();
                await Assert.That(shell.Publisher.PublicationSummary).Contains("unknown");
                await Assert.That(shell.Publisher.CanSaveBundle).IsFalse();
            }
            finally { release.TrySetResult(); }
            ProtectedArtifactBundleResult result = (await saving.WaitAsync(TimeSpan.FromSeconds(10)))!;
            await Assert.That(result.CompletionCode).IsEqualTo(ProtectedArtifactBundleCompletionCode.Cancelled);
            ProtectedArtifactPublicationReceipt receipt = shell.Publisher.PublicationHistory.Single();
            await Assert.That(receipt.Outcomes[0].Result!.Succeeded).IsTrue();
            await Assert.That(receipt.Outcomes[1].Result).IsNull();
            await Assert.That(File.ReadAllBytes(Path.Combine(directory, "service.env"))
                .AsSpan().SequenceEqual("NAME=Example service\n"u8)).IsTrue();
            await Assert.That(File.Exists(Path.Combine(directory, "service.yaml"))).IsFalse();
            await Assert.That(shell.Publisher.CanSaveBundle).IsFalse();
        }
        finally
        {
            release.TrySetResult();
            Directory.Delete(directory, true);
        }
    }

    [Test]
    public async Task HistoryRetainsSixteenCopiedSettlementsPlusOneIndependentActiveAttempt()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int attempts = 0;
        using var shell = DesktopTestContext.Root(save: async (_, _, _) =>
        {
            if (++attempts == 19)
            {
                entered.TrySetResult();
                await release.Task.WaitAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
            }
            return ProtectedArtifactCommitResult.Installed(true);
        }, protectedOutputAvailable: true);
        shell.Loader.LoadDefinition(DesktopApplicationTests.Definition());
        var ids = new List<Guid>();
        for (int index = 0; index < 18; index++)
        {
            await Assert.That(await shell.Publisher.PrepareAsync()).IsEqualTo(DesktopPreparationOutcome.Prepared);
            await shell.Publisher.SaveAsync();
            ids.Add(shell.Publisher.LatestPublication!.AttemptId);
        }
        IReadOnlyList<ProtectedArtifactPublicationReceipt> snapshot = shell.Publisher.PublicationHistory;
        await Assert.That(snapshot.Select(receipt => receipt.AttemptId).SequenceEqual(ids.Skip(2))).IsTrue();
        await Assert.That(await shell.Publisher.PrepareAsync()).IsEqualTo(DesktopPreparationOutcome.Prepared);
        Task<ProtectedArtifactCommitResult> pending = shell.Publisher.SaveAsync();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try
        {
            shell.Cancel();
            await Assert.That(shell.Publisher.PublicationHistory).Count().IsEqualTo(16);
            await Assert.That(shell.Publisher.CurrentPublication!.IsSettled).IsFalse();
            await Assert.That(shell.Publisher.CurrentPublication.Outcomes).Count().IsEqualTo(1);
        }
        finally { release.TrySetResult(); }
        await pending.WaitAsync(TimeSpan.FromSeconds(10));
        await Assert.That(shell.Publisher.PublicationHistory).Count().IsEqualTo(16);
        await Assert.That(snapshot.Select(receipt => receipt.AttemptId).SequenceEqual(ids.Skip(2))).IsTrue();
        await Assert.That(shell.Publisher.PublicationHistory[0].AttemptId).IsEqualTo(ids[3]);
        await Assert.That(shell.Publisher.CurrentPublication).IsNull();
    }

    private sealed class HeldOwnerContext : SynchronizationContext
    {
        private readonly System.Collections.Concurrent.ConcurrentQueue<Action> _pending = new();
        internal TaskCompletionSource Posted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override void Post(SendOrPostCallback callback, object? state)
        {
            _pending.Enqueue(() => callback(state));
            Posted.TrySetResult();
        }

        internal void Run(Action action)
        {
            SynchronizationContext? previous = Current;
            SetSynchronizationContext(this);
            try { action(); }
            finally { SetSynchronizationContext(previous); }
        }

        internal void Drain()
        {
            while (_pending.TryDequeue(out Action? action)) Run(action);
        }
    }

    private sealed class HeldReadStream(byte[] input) : MemoryStream
    {
        public override long Length => input.Length;
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal Memory<byte> Borrowed { get; private set; }
        internal CancellationToken Token { get; private set; }
        internal int CloseCount { get; private set; }
        private bool _read;

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_read) return 0;
            _read = true;
            Borrowed = buffer;
            Token = cancellationToken;
            input.CopyTo(buffer);
            Entered.TrySetResult();
            await Release.Task.WaitAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
            return input.Length;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) CloseCount++;
            base.Dispose(disposing);
        }
    }
}
