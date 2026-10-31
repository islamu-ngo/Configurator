namespace Configurator.Terminal.Tests;

using System.Text.Json;
using Configurator.Presentation;
using Configurator.Terminal;

[NotInParallel]
public sealed class SetupTerminalImportComparisonTests
{
    [Test]
    public async Task ComparisonDispatchDoesNotAdoptInputOrChangeCurrentRevision()
    {
        using var owner = new ControlledTerminalSynchronizationContext();
        using var form = new DefinitionFormModel(TerminalDefinitionFixture.Definition());
        using var operation = new SetupTerminalImportOperation(form, owner.Dispatch,
            (_, _) => Task.FromResult<ReadOnlyMemory<byte>>("""{"name":"imported-value"}"""u8.ToArray()));
        var settled = new TaskCompletionSource<SetupTerminalImportResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        operation.Settled += (_, result) => settled.TrySetResult(result);
        await owner.InvokeAsync(() =>
        {
            if (!operation.Start("synthetic-local.json", "config", compare: true))
                throw new InvalidOperationException("comparison-not-started");
        });
        SetupTerminalImportResult result = await settled.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await operation.Completion.WaitAsync(TimeSpan.FromSeconds(10));

        await Assert.That(result.Succeeded).IsTrue();
        await owner.InvokeAsync(() =>
        {
            if (form.InputRevision != 0 || form.Snapshot().Values.Count != 0 || form.ImportComparison is null)
                throw new InvalidOperationException("comparison-changed-workspace");
        });
    }

    [Test]
    public async Task LatePhysicalInputCannotPublishAfterCancellationAndCurrentEdit()
    {
        using var owner = new ControlledTerminalSynchronizationContext();
        using var form = new DefinitionFormModel(TerminalDefinitionFixture.Definition());
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<ReadOnlyMemory<byte>>(TaskCreationOptions.RunContinuationsAsynchronously);
        byte[] bytes = """{"name":"late-private-value"}"""u8.ToArray();
        using var operation = new SetupTerminalImportOperation(form, owner.Dispatch, (_, _) =>
        {
            entered.TrySetResult();
            return release.Task;
        });
        var settled = new TaskCompletionSource<SetupTerminalImportResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        operation.Settled += (_, result) => settled.TrySetResult(result);
        await owner.InvokeAsync(() => operation.Start("synthetic-local.json", "config", compare: true));
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await owner.InvokeAsync(() =>
        {
            operation.RequestCancellationAsync();
            form.SetValue(["name"], JsonSerializer.SerializeToElement("current-value"));
        });
        release.SetResult(bytes);
        SetupTerminalImportResult result = await settled.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await operation.Completion.WaitAsync(TimeSpan.FromSeconds(10));

        await Assert.That(result.IsCurrent).IsFalse();
        await Assert.That(bytes.All(value => value == 0)).IsTrue();
        await owner.InvokeAsync(() =>
        {
            if (form.ImportComparison is not null || form.Snapshot().Values["name"].GetString() != "current-value")
                throw new InvalidOperationException("stale-comparison-published");
        });
    }

    [Test]
    public async Task CooperativeComparisonSuspendsWithoutMutationAndCancellationDrainsCleanup()
    {
        using var owner = new ControlledTerminalSynchronizationContext();
        using var form = new DefinitionFormModel(TerminalDefinitionFixture.Definition());
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        byte[] bytes = """{"name":"compared-value"}"""u8.ToArray();
        using var operation = new SetupTerminalImportOperation(form, owner.Dispatch,
            (_, _) => Task.FromResult<ReadOnlyMemory<byte>>(bytes), cooperativeYield: async token =>
            {
                entered.TrySetResult();
                try { await release.Task.WaitAsync(token); }
                finally
                {
                    cancelled.TrySetResult();
                    await release.Task;
                }
            });
        try
        {
            await owner.InvokeAsync(() => operation.Start("synthetic-local.json", "config", compare: true));
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await owner.InvokeAsync(() =>
            {
                if (!operation.IsBusy || operation.Completion.IsCompleted ||
                    form.InputRevision != 0 || form.ImportComparison is not null)
                    throw new InvalidOperationException("comparison-did-not-suspend");
                operation.RequestCancellationAsync();
            });
            await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await Assert.That(operation.IsBusy).IsTrue();
            await Assert.That(operation.Completion.IsCompleted).IsFalse();
            release.TrySetResult();
            await operation.Completion.WaitAsync(TimeSpan.FromSeconds(5));
            await Assert.That(bytes.All(value => value == 0)).IsTrue();
            await owner.InvokeAsync(() =>
            {
                if (form.InputRevision != 0 || form.Snapshot().Values.Count != 0 || form.ImportComparison is not null)
                    throw new InvalidOperationException("cancelled-comparison-published");
            });
        }
        finally
        {
            release.TrySetResult();
            await operation.Completion.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task RetainedAdoptionOwnsCompletionWithoutReadingOrReplacingReport(bool cancel)
    {
        using var owner = new ControlledTerminalSynchronizationContext();
        using var form = new DefinitionFormModel(TerminalDefinitionFixture.Definition());
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var exiting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cleanup = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        bool adopting = false;
        int reads = 0;
        using var operation = new SetupTerminalImportOperation(form, owner.Dispatch, (_, _) =>
        {
            Interlocked.Increment(ref reads);
            return Task.FromResult<ReadOnlyMemory<byte>>("""{"name":"adopted-value"}"""u8.ToArray());
        }, cooperativeYield: async token =>
        {
            if (!adopting) return;
            entered.TrySetResult();
            try { await release.Task.WaitAsync(token); }
            finally
            {
                exiting.TrySetResult();
                await cleanup.Task;
            }
        });
        try
        {
            await owner.InvokeAsync(() => operation.Start("synthetic-local.json", "config", compare: true));
            await operation.Completion.WaitAsync(TimeSpan.FromSeconds(5));
            await owner.InvokeAsync(() =>
            {
                var report = form.ImportComparison;
                if (report is null) throw new InvalidOperationException("comparison-not-retained");
                adopting = true;
                if (!operation.StartAdoption("config") || !ReferenceEquals(report, form.ImportComparison))
                    throw new InvalidOperationException("adoption-replaced-report");
            });
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await owner.InvokeAsync(() =>
            {
                if (!operation.IsBusy || operation.Completion.IsCompleted || form.InputRevision != 0 ||
                    operation.Start("replacement.json", "config") || operation.StartAdoption("config"))
                    throw new InvalidOperationException("adoption-ownership-lost");
                if (cancel) operation.RequestCancellationAsync();
                else release.TrySetResult();
            });
            await exiting.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await Assert.That(operation.IsBusy).IsTrue();
            await Assert.That(operation.Completion.IsCompleted).IsFalse();
            cleanup.TrySetResult();
            await operation.Completion.WaitAsync(TimeSpan.FromSeconds(5));
            await Assert.That(reads).IsEqualTo(1);
            await owner.InvokeAsync(() =>
            {
                if (cancel)
                {
                    if (form.InputRevision != 0 || form.Snapshot().Values.Count != 0 || form.ImportComparison is not null)
                        throw new InvalidOperationException("cancelled-adoption-mutated-workspace");
                }
                else if (form.InputRevision != 1 || form.Snapshot().Values["name"].GetString() != "adopted-value")
                    throw new InvalidOperationException("retained-adoption-not-applied");
            });
        }
        finally
        {
            release.TrySetResult();
            cleanup.TrySetResult();
            await operation.Completion.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }
}
