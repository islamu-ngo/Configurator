namespace Configurator.Presentation.Tests;

internal sealed class ControlledPresentationOperation : ISetupPresentationOperation
{
    private int _nextInvocation;

    internal ControlledPresentationOperation(int invocationCount = 1)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(invocationCount, 1);
        Invocations = Enumerable.Range(0, invocationCount)
            .Select(_ => new ControlledPresentationInvocation()).ToArray();
    }

    internal IReadOnlyList<ControlledPresentationInvocation> Invocations { get; }

    public Task<SetupPresentationOutcome> ExecuteAsync(CancellationToken cancellationToken)
    {
        int index = Interlocked.Increment(ref _nextInvocation) - 1;
        if ((uint)index >= (uint)Invocations.Count)
            throw new InvalidOperationException("presentation-operation-invocation-exhausted");
        return Invocations[index].Start(cancellationToken);
    }

    internal ReadOnlyMemory<byte> Complete(int invocation, object result, ReadOnlyMemory<byte> output) =>
        Invocations[invocation].Complete(result, output);
}

internal sealed class ControlledPresentationInvocation
{
    private readonly TaskCompletionSource<SetupPresentationOutcome> _completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal TaskCompletionSource Cancelled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal Task<SetupPresentationOutcome> Start(CancellationToken cancellationToken)
    {
        cancellationToken.Register(() => Cancelled.TrySetResult());
        Started.TrySetResult();
        return _completion.Task;
    }

    internal ReadOnlyMemory<byte> Complete(object result, ReadOnlyMemory<byte> output)
    {
        SetupPresentationOutcome? outcome = new(result, output);
        try
        {
            // Capture the owned region before completion releases callbacks that may clear it.
            ReadOnlyMemory<byte> captured = outcome.Output;
            _completion.SetResult(outcome);
            outcome = null;
            return captured;
        }
        finally { outcome?.Dispose(); }
    }
}

internal sealed class ControlledGenerationAllocator(params long?[] allocations) : ISetupOperationGenerationAllocator
{
    private readonly Queue<long?> _allocations = new(allocations);

    public bool TryAllocate(out SetupOperationGeneration generation)
    {
        long? allocation = _allocations.Count == 0 ? null : _allocations.Dequeue();
        generation = allocation is { } value ? new(value) : default;
        return allocation is not null;
    }
}

internal sealed class CallbackSubscription(Action unsubscribe) : IDisposable
{
    private Action? _unsubscribe = unsubscribe;
    public void Dispose() => Interlocked.Exchange(ref _unsubscribe, null)?.Invoke();
}
