namespace Configurator.Artifacts.Composition;

internal sealed class CompositionDirectoryLimits
{
    internal int Directories => 256;
    internal int Files => 1024;
    internal int EntriesPerDirectory => 256;
    internal int RelativePathCharacters => 512;
    internal int PathDepth => 16;
    internal int PerFileBytes => 524288;
    internal int AggregateDirectoryBytes => 4194304;
}
