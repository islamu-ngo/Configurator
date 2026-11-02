namespace Configurator.Core.Composition;

using System.Text.Json;

public enum CompositionFailureCode
{
    None = 0,
    InvalidSource,
    InvalidDocument,
    InvalidKey,
    DuplicateKey,
    KeyCollision,
    InvalidScalar,
    UnsupportedYamlGrammar,
    LimitExceeded,
    UnsafePath,
    PathCollision,
    UnsafeEntry,
    UnsupportedFilesystem,
    SourceChanged,
    SourceConflict,
    ForbiddenAuthority,
    ContractInvalid,
    Cancelled
}
