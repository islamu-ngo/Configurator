namespace Configurator.Artifacts.Composition;

using System.Text.Json;

public sealed class CompositionImmediatePublicationCommitBarrier
    : ICompositionPublicationCommitBarrier
{
    public static CompositionImmediatePublicationCommitBarrier Instance { get; } = new();

    private CompositionImmediatePublicationCommitBarrier() { }

    public ValueTask AwaitPublicationCommitAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.CompletedTask;
    }
}
