namespace Configurator.Core.Composition;

using System.Text.Json;

public sealed record CompositionDirectorySource : CompositionSource
{
    public CompositionDirectorySource(string rootDirectory)
        : base(CompositionSourceKind.Directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        RootDirectory = rootDirectory;
    }

    internal string RootDirectory { get; }

    public override string ToString() => $"{nameof(CompositionDirectorySource)}:{Kind}";
}
