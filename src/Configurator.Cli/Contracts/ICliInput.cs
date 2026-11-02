namespace Configurator.Cli;

public interface ICliInput
{
    ValueTask<CliInputBuffer> ReadAsync(string path, int maximumBytes, CancellationToken cancellationToken);
}

