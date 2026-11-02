namespace Configurator.Presentation;

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
