namespace Configurator.Core.Definitions;

public enum DefinitionSecretEncoding
{
    Utf8 = 1,
    Base64,
    Base64Url,
    Hex,
    Pkcs8Pem,
    Jwk,
}
