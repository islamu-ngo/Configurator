namespace Configurator.Presentation;

using System.Security.Cryptography;

public enum OperationStatus
{
    Succeeded,
    Cancelled,
    Rejected
}

public enum OperationInvalidationReason
{
    Replaced,
    Cancelled,
    Deactivated,
    Disposed,
    SessionTerminated,
    Faulted
}

public interface IPresentationOperation
{
    Task<PresentationOutcome> ExecuteAsync(CancellationToken cancellationToken);
}

/// <summary>Owns a private copy of operation output; disposal clears that copy.</summary>
public sealed class PresentationOutcome : IDisposable
{
    private byte[] _output;

    public PresentationOutcome(object coreResult, ReadOnlyMemory<byte> output)
    {
        ArgumentNullException.ThrowIfNull(coreResult);
        CoreResult = coreResult;
        _output = output.ToArray();
    }

    public object CoreResult { get; }

    public ReadOnlyMemory<byte> Output => _output;

    public void Dispose() => CryptographicOperations.ZeroMemory(Interlocked.Exchange(ref _output, []));

    public override string ToString() => nameof(PresentationOutcome);
}

public sealed class OperationSettledMessage
{
    public OperationSettledMessage(
        Guid sessionId,
        WorkspaceId workspaceId,
        Guid operationId,
        OperationGeneration generation,
        OperationStatus status)
    {
        SessionId = sessionId;
        WorkspaceId = workspaceId;
        OperationId = operationId;
        Generation = generation;
        Status = status;
    }

    public OperationGeneration Generation { get; }

    public Guid OperationId { get; }

    public Guid SessionId { get; }

    public OperationStatus Status { get; }

    public WorkspaceId WorkspaceId { get; }

    public override string ToString() => nameof(OperationSettledMessage);
}

public sealed class OperationInvalidatedEventArgs : EventArgs
{
    public OperationInvalidatedEventArgs(
        OperationGeneration generation,
        OperationInvalidationReason reason)
    {
        Generation = generation;
        Reason = reason;
    }

    public OperationGeneration Generation { get; }

    public OperationInvalidationReason Reason { get; }

    public override string ToString() => nameof(OperationInvalidatedEventArgs);
}

public sealed class CompletionDiscardedEventArgs : EventArgs
{
    public CompletionDiscardedEventArgs(
        Guid operationId,
        OperationGeneration generation)
    {
        OperationId = operationId;
        Generation = generation;
    }

    public OperationGeneration Generation { get; }

    public Guid OperationId { get; }

    public override string ToString() => nameof(CompletionDiscardedEventArgs);
}
