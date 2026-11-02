namespace Configurator.Presentation;

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
