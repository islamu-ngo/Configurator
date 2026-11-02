namespace Configurator.Presentation;

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
