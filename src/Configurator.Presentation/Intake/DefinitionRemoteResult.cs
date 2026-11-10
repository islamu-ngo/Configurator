namespace Configurator.Presentation.Intake;

using Configurator.Core.Definitions;

/// <summary>Describes a remote definition admission outcome without exposing transport content.</summary>
public abstract record DefinitionRemoteResult
{
    private DefinitionRemoteResult() { }

    public sealed record Success(Definition Definition) : DefinitionRemoteResult;
    public sealed record InvalidUri : DefinitionRemoteResult;
    public sealed record BlockedHost : DefinitionRemoteResult;
    public sealed record PayloadTooLarge : DefinitionRemoteResult;
    public sealed record NetworkError : DefinitionRemoteResult;
    public sealed record ParseError : DefinitionRemoteResult;
}
