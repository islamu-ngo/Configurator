namespace Configurator.Core.Definitions;

/// <summary>Exact public wire spellings, independent of CLR names and numeric enum values.</summary>
public static class DefinitionSecretVocabulary
{
    internal static bool IsDefined(DefinitionSecretSpecification policy) =>
        Enum.IsDefined(policy.Kind) && Enum.IsDefined(policy.Acquisition)
        && (policy.Algorithm is null || Enum.IsDefined(policy.Algorithm.Value))
        && (policy.Encoding is null || Enum.IsDefined(policy.Encoding.Value))
        && Enum.IsDefined(policy.Constraints.CharacterSet)
        && (policy.Constraints.Curve is null || Enum.IsDefined(policy.Constraints.Curve.Value))
        && (policy.Generation is null || Enum.IsDefined(policy.Generation.Input) && Enum.IsDefined(policy.Generation.InputEncoding));

    public static DefinitionSecretKind ParseKind(string token) => token switch
    {
        "password" => DefinitionSecretKind.Password,
        "opaque-token" => DefinitionSecretKind.OpaqueToken,
        "symmetric-key" => DefinitionSecretKind.SymmetricKey,
        "private-key" => DefinitionSecretKind.PrivateKey,
        "password-hash" => DefinitionSecretKind.PasswordHash,
        "digest" => DefinitionSecretKind.Digest,
        "connection-string" => DefinitionSecretKind.ConnectionString,
        _ => throw new DefinitionException("secret-policy-invalid"),
    };

    public static string Token(DefinitionSecretKind value) => value switch
    {
        DefinitionSecretKind.Password => "password",
        DefinitionSecretKind.OpaqueToken => "opaque-token",
        DefinitionSecretKind.SymmetricKey => "symmetric-key",
        DefinitionSecretKind.PrivateKey => "private-key",
        DefinitionSecretKind.PasswordHash => "password-hash",
        DefinitionSecretKind.Digest => "digest",
        DefinitionSecretKind.ConnectionString => "connection-string",
        _ => throw new DefinitionException("secret-policy-invalid"),
    };

    public static DefinitionSecretAcquisition ParseAcquisition(string token) => token switch
    {
        "local" => DefinitionSecretAcquisition.Local,
        "existing" => DefinitionSecretAcquisition.Existing,
        "issuer" => DefinitionSecretAcquisition.Issuer,
        _ => throw new DefinitionException("secret-policy-invalid"),
    };

    public static string Token(DefinitionSecretAcquisition value) => value switch
    {
        DefinitionSecretAcquisition.Local => "local",
        DefinitionSecretAcquisition.Existing => "existing",
        DefinitionSecretAcquisition.Issuer => "issuer",
        _ => throw new DefinitionException("secret-policy-invalid"),
    };

    public static DefinitionSecretAlgorithm ParseAlgorithm(string token) => token switch
    {
        "HS256" => DefinitionSecretAlgorithm.Hs256,
        "HS384" => DefinitionSecretAlgorithm.Hs384,
        "HS512" => DefinitionSecretAlgorithm.Hs512,
        "AES128" => DefinitionSecretAlgorithm.Aes128,
        "AES192" => DefinitionSecretAlgorithm.Aes192,
        "AES256" => DefinitionSecretAlgorithm.Aes256,
        "RS256" => DefinitionSecretAlgorithm.Rs256,
        "PS256" => DefinitionSecretAlgorithm.Ps256,
        "ES256" => DefinitionSecretAlgorithm.Es256,
        "ES384" => DefinitionSecretAlgorithm.Es384,
        "ES512" => DefinitionSecretAlgorithm.Es512,
        "argon2id" => DefinitionSecretAlgorithm.Argon2id,
        "pbkdf2-sha256" => DefinitionSecretAlgorithm.Pbkdf2Sha256,
        "pbkdf2-sha512" => DefinitionSecretAlgorithm.Pbkdf2Sha512,
        "aspnet-identity-v3" => DefinitionSecretAlgorithm.AspNetIdentityV3,
        "bcrypt" => DefinitionSecretAlgorithm.Bcrypt,
        "SHA256" => DefinitionSecretAlgorithm.Sha256,
        "SHA384" => DefinitionSecretAlgorithm.Sha384,
        "SHA512" => DefinitionSecretAlgorithm.Sha512,
        _ => throw new DefinitionException("secret-policy-invalid"),
    };

    public static string Token(DefinitionSecretAlgorithm value) => value switch
    {
        DefinitionSecretAlgorithm.Hs256 => "HS256",
        DefinitionSecretAlgorithm.Hs384 => "HS384",
        DefinitionSecretAlgorithm.Hs512 => "HS512",
        DefinitionSecretAlgorithm.Aes128 => "AES128",
        DefinitionSecretAlgorithm.Aes192 => "AES192",
        DefinitionSecretAlgorithm.Aes256 => "AES256",
        DefinitionSecretAlgorithm.Rs256 => "RS256",
        DefinitionSecretAlgorithm.Ps256 => "PS256",
        DefinitionSecretAlgorithm.Es256 => "ES256",
        DefinitionSecretAlgorithm.Es384 => "ES384",
        DefinitionSecretAlgorithm.Es512 => "ES512",
        DefinitionSecretAlgorithm.Argon2id => "argon2id",
        DefinitionSecretAlgorithm.Pbkdf2Sha256 => "pbkdf2-sha256",
        DefinitionSecretAlgorithm.Pbkdf2Sha512 => "pbkdf2-sha512",
        DefinitionSecretAlgorithm.AspNetIdentityV3 => "aspnet-identity-v3",
        DefinitionSecretAlgorithm.Bcrypt => "bcrypt",
        DefinitionSecretAlgorithm.Sha256 => "SHA256",
        DefinitionSecretAlgorithm.Sha384 => "SHA384",
        DefinitionSecretAlgorithm.Sha512 => "SHA512",
        _ => throw new DefinitionException("secret-policy-invalid"),
    };

    public static DefinitionSecretEncoding ParseEncoding(string token) => token switch
    {
        "utf8" => DefinitionSecretEncoding.Utf8,
        "base64" => DefinitionSecretEncoding.Base64,
        "base64url" => DefinitionSecretEncoding.Base64Url,
        "hex" => DefinitionSecretEncoding.Hex,
        "pkcs8-pem" => DefinitionSecretEncoding.Pkcs8Pem,
        "jwk" => DefinitionSecretEncoding.Jwk,
        _ => throw new DefinitionException("secret-policy-invalid"),
    };

    public static string Token(DefinitionSecretEncoding value) => value switch
    {
        DefinitionSecretEncoding.Utf8 => "utf8",
        DefinitionSecretEncoding.Base64 => "base64",
        DefinitionSecretEncoding.Base64Url => "base64url",
        DefinitionSecretEncoding.Hex => "hex",
        DefinitionSecretEncoding.Pkcs8Pem => "pkcs8-pem",
        DefinitionSecretEncoding.Jwk => "jwk",
        _ => throw new DefinitionException("secret-policy-invalid"),
    };

    public static DefinitionSecretInput ParseInput(string token) => token switch
    {
        "entered" => DefinitionSecretInput.Entered,
        "generated" => DefinitionSecretInput.Generated,
        _ => throw new DefinitionException("secret-policy-invalid"),
    };

    public static string Token(DefinitionSecretInput value) => value switch
    {
        DefinitionSecretInput.Entered => "entered",
        DefinitionSecretInput.Generated => "generated",
        _ => throw new DefinitionException("secret-policy-invalid"),
    };

    public static DefinitionSecretCharacterSet ParseCharacterSet(string token) => token switch
    {
        "unicode" => DefinitionSecretCharacterSet.Unicode,
        "ascii-printable" => DefinitionSecretCharacterSet.AsciiPrintable,
        _ => throw new DefinitionException("secret-policy-invalid"),
    };

    public static string Token(DefinitionSecretCharacterSet value) => value switch
    {
        DefinitionSecretCharacterSet.Unicode => "unicode",
        DefinitionSecretCharacterSet.AsciiPrintable => "ascii-printable",
        _ => throw new DefinitionException("secret-policy-invalid"),
    };

    public static DefinitionSecretCurve ParseCurve(string token) => token switch
    {
        "P-256" => DefinitionSecretCurve.P256,
        "P-384" => DefinitionSecretCurve.P384,
        "P-521" => DefinitionSecretCurve.P521,
        _ => throw new DefinitionException("secret-policy-invalid"),
    };

    public static string Token(DefinitionSecretCurve value) => value switch
    {
        DefinitionSecretCurve.P256 => "P-256",
        DefinitionSecretCurve.P384 => "P-384",
        DefinitionSecretCurve.P521 => "P-521",
        _ => throw new DefinitionException("secret-policy-invalid"),
    };

}
