namespace Configurator.Cli;

/// <summary>Separates staging, create-only publication and cleanup for truthful ordered receipts.</summary>
public interface ICliPublicFilePreparation : IDisposable
{
    ValueTask StageAsync(CancellationToken cancellationToken);
    ValueTask<bool> CommitAsync(CancellationToken cancellationToken);
}

