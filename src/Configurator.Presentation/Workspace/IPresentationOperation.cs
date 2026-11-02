namespace Configurator.Presentation;

public interface IPresentationOperation
{
    Task<PresentationOutcome> ExecuteAsync(CancellationToken cancellationToken);
}
