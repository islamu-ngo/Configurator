namespace Configurator.Core.Definitions;

public enum DefinitionSecretKind
{
    Password = 1,
    OpaqueToken,
    SymmetricKey,
    PrivateKey,
    PasswordHash,
    Digest,
    ConnectionString,
}
