namespace Configurator.Presentation.Intake;

/// <summary>Retrieves and admits a public remote definition without workspace data.</summary>
public interface IDefinitionRemoteFetcher : IDisposable
{
    Task<DefinitionRemoteResult> FetchAsync(string uri, CancellationToken cancellationToken = default);
}
