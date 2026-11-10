namespace Configurator.Presentation.Store;

using Configurator.Core.Definitions;

/// <summary>Outcome of opening one explicitly selected store revision.</summary>
public abstract record DefinitionStoreResult
{
    private DefinitionStoreResult() { }
    public sealed record Success(Definition Definition) : DefinitionStoreResult;
    public sealed record ProjectNotFound : DefinitionStoreResult;
    public sealed record RevisionNotFound : DefinitionStoreResult;
    public sealed record SnapshotMismatch : DefinitionStoreResult;
    public sealed record CorruptAsset : DefinitionStoreResult;
}

/// <summary>Value-safe catalogue admission failure.</summary>
public sealed class DefinitionStoreException(string code) : Exception(code)
{
    public string Code { get; } = code;
}
