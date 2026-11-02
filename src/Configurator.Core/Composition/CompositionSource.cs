namespace Configurator.Core.Composition;

using System.Text.Json;

public abstract record CompositionSource
{
    private protected CompositionSource(CompositionSourceKind kind) => Kind = kind;

    public CompositionSourceKind Kind { get; }
}
