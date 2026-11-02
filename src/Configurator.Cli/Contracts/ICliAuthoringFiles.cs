namespace Configurator.Cli;

/// <summary>Supplies an invocation's public authoring authority, independently of private answer I/O.</summary>
public interface ICliAuthoringFiles
{
    ValueTask<string> DiscoverSourceAsync(string root, string? source, CancellationToken cancellationToken);
    ValueTask<CliInputBuffer> ReadAsync(string path, int maximumBytes, CancellationToken cancellationToken);
    ValueTask<bool> ExistsAsync(string path, CancellationToken cancellationToken);
    ICliPublicFilePreparation PrepareCreate(string path, ReadOnlyMemory<byte> bytes, int maximumBytes, bool acceptIdentical);
}

