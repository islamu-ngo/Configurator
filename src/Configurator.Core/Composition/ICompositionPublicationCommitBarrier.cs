namespace Configurator.Core.Composition;

using System.Text.Json;

public interface ICompositionPublicationCommitBarrier
{
    ValueTask AwaitPublicationCommitAsync(CancellationToken cancellationToken);
}
