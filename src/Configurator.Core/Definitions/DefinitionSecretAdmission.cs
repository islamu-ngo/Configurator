namespace Configurator.Core.Definitions;

using System.Text.Json;

internal static class DefinitionSecretAdmission
{
    internal static DefinitionSecretSpecification Read(JsonElement secret, JsonElement schema)
    {
        Only(secret, "kind", "acquisition", "algorithm", "encoding", "constraints", "generation");
        string kind = Text(secret, "kind", "");
        string acquisition = Text(secret, "acquisition", "");
        Require(kind is "password" or "opaque-token" or "symmetric-key" or "private-key" or "password-hash" or "digest" or "connection-string");
        Require(acquisition is "local" or "existing" or "issuer");
        string? algorithm = OptionalText(secret, "algorithm");
        string? encoding = OptionalText(secret, "encoding");
        Require(kind switch
        {
            "symmetric-key" => algorithm is "HS256" or "HS384" or "HS512" or "AES128" or "AES192" or "AES256",
            "private-key" => algorithm is "RS256" or "PS256" or "ES256" or "ES384" or "ES512",
            "password-hash" => algorithm is "argon2id" or "pbkdf2-sha256" or "pbkdf2-sha512" or "aspnet-identity-v3" or "bcrypt",
            "digest" => algorithm is "SHA256" or "SHA384" or "SHA512",
            _ => algorithm is null
        });
        Require(kind switch
        {
            "opaque-token" or "symmetric-key" or "digest" => encoding is "base64" or "base64url" or "hex",
            "private-key" => encoding is "pkcs8-pem" or "jwk",
            _ => encoding is null
        });
        int minimumBytes = algorithm switch
        {
            "HS256" => 32,
            "HS384" => 48,
            "HS512" => 64,
            "AES128" => 16,
            "AES192" => 24,
            "AES256" => 32,
            _ => 0
        };
        int maximumBytes = algorithm is "AES128" or "AES192" or "AES256" ? minimumBytes : 4096;
        int iterations = algorithm switch { "pbkdf2-sha256" => 600000, "pbkdf2-sha512" or "aspnet-identity-v3" => 210000, _ => 2 };
        string? curve = algorithm switch { "ES256" => "P-256", "ES384" => "P-384", "ES512" => "P-521", _ => null };
        var constraints = new DefinitionSecretConstraints { MinBytes = minimumBytes, MaxBytes = maximumBytes, Iterations = iterations, Curve = curve };
        if (secret.TryGetProperty("constraints", out JsonElement source))
        {
            Only(source, kind switch
            {
                "password" => ["characterSet", "allowWhitespace", "maxUtf8Bytes", "minLowercase", "minUppercase", "minDigits", "minSymbols", "allowedSymbols", "forbiddenCharacters"],
                "opaque-token" or "symmetric-key" => ["minBytes", "maxBytes"],
                "private-key" => algorithm is "RS256" or "PS256" ? ["keySize"] : ["curve"],
                "password-hash" => algorithm switch
                {
                    "argon2id" => ["memoryKiB", "iterations", "parallelism", "saltBytes", "derivedBytes"],
                    "pbkdf2-sha256" or "pbkdf2-sha512" => ["iterations", "saltBytes", "derivedBytes"],
                    "bcrypt" => ["cost"],
                    _ => []
                },
                _ => []
            });
            constraints = constraints with
            {
                CharacterSet = Text(source, "characterSet", constraints.CharacterSet),
                AllowWhitespace = source.TryGetProperty("allowWhitespace", out JsonElement whitespace) ? whitespace.GetBoolean() : constraints.AllowWhitespace,
                MaxUtf8Bytes = source.TryGetProperty("maxUtf8Bytes", out _) ? Number(source, "maxUtf8Bytes", 0, 1, 65536) : null,
                MinLowercase = Number(source, "minLowercase", 0, 0, 1024),
                MinUppercase = Number(source, "minUppercase", 0, 0, 1024),
                MinDigits = Number(source, "minDigits", 0, 0, 1024),
                MinSymbols = Number(source, "minSymbols", 0, 0, 1024),
                AllowedSymbols = Text(source, "allowedSymbols", constraints.AllowedSymbols),
                ForbiddenCharacters = Text(source, "forbiddenCharacters", ""),
                MinBytes = Number(source, "minBytes", minimumBytes, minimumBytes, 4096),
                MaxBytes = Number(source, "maxBytes", maximumBytes, minimumBytes, maximumBytes),
                KeySize = Number(source, "keySize", 2048, 2048, 4096),
                Curve = Text(source, "curve", curve ?? "") is { Length: > 0 } suppliedCurve ? suppliedCurve : null,
                MemoryKiB = Number(source, "memoryKiB", 19456, 19456, 65536),
                Iterations = Number(source, "iterations", iterations, iterations, algorithm == "argon2id" ? 6 : 2000000),
                Parallelism = Number(source, "parallelism", 1, 1, 4),
                SaltBytes = Number(source, "saltBytes", 16, 16, 64),
                DerivedBytes = Number(source, "derivedBytes", 32, 16, 64),
                Cost = Number(source, "cost", 12, 10, 14)
            };
        }
        Require(constraints.CharacterSet is "unicode" or "ascii-printable");
        Require(constraints.MinBytes <= constraints.MaxBytes);
        Require(constraints.KeySize is 2048 or 3072 or 4096);
        Require(constraints.Curve == curve);
        Require(constraints.AllowedSymbols.Length <= 94 && constraints.AllowedSymbols.Distinct().Count() == constraints.AllowedSymbols.Length
            && constraints.AllowedSymbols.All(c => c is >= '!' and <= '~' && !char.IsAsciiLetterOrDigit(c)));
        Require(constraints.MinSymbols == 0 || constraints.AllowedSymbols.Length != 0);
        int minimum = Number(schema, "minLength", 0, 0, int.MaxValue);
        int? maximum = schema.ValueKind == JsonValueKind.Object && schema.TryGetProperty("maxLength", out _) ? Number(schema, "maxLength", 0, 0, int.MaxValue) : null;
        Require(maximum is null || minimum <= maximum);
        int required = constraints.MinLowercase + constraints.MinUppercase + constraints.MinDigits + constraints.MinSymbols;
        Require(maximum is null || required <= maximum);
        if (kind == "password")
        {
            Require((constraints.MinLowercase == 0 || "abcdefghijklmnopqrstuvwxyz".Any(c => !constraints.ForbiddenCharacters.Contains(c)))
                && (constraints.MinUppercase == 0 || "ABCDEFGHIJKLMNOPQRSTUVWXYZ".Any(c => !constraints.ForbiddenCharacters.Contains(c)))
                && (constraints.MinDigits == 0 || "0123456789".Any(c => !constraints.ForbiddenCharacters.Contains(c)))
                && (constraints.MinSymbols == 0 || constraints.AllowedSymbols.Any(c => !constraints.ForbiddenCharacters.Contains(c))));
            Require(constraints.MaxUtf8Bytes is null || Math.Max(required, minimum) <= constraints.MaxUtf8Bytes);
        }
        if (kind == "digest")
        {
            int bytes = algorithm switch { "SHA256" => 32, "SHA384" => 48, _ => 64 };
            int characters = EncodedLength(bytes, encoding!);
            Require(characters >= minimum && (maximum is null || characters <= maximum));
        }
        if (kind == "password-hash" && algorithm is "bcrypt" or "aspnet-identity-v3")
        {
            int characters = algorithm == "bcrypt" ? 60 : 84;
            Require(characters >= minimum && (maximum is null || characters <= maximum));
        }
        if (kind == "password-hash" && algorithm is "argon2id" or "pbkdf2-sha256" or "pbkdf2-sha512")
        {
            int characters = algorithm == "argon2id"
                ? FormattableString.Invariant($"$argon2id$v=19$m={constraints.MemoryKiB},t={constraints.Iterations},p={constraints.Parallelism}$").Length
                    + EncodedLength(constraints.SaltBytes, "base64url") + 1 + EncodedLength(constraints.DerivedBytes, "base64url")
                : FormattableString.Invariant($"{{\"version\":1,\"algorithm\":\"{algorithm}\",\"iterations\":{constraints.Iterations},\"salt\":\"\",\"derivedKey\":\"\"}}").Length
                    + EncodedLength(constraints.SaltBytes, "base64") + EncodedLength(constraints.DerivedBytes, "base64");
            Require(characters >= minimum && (maximum is null || characters <= maximum));
        }
        DefinitionSecretGeneration? generation = null;
        if (secret.TryGetProperty("generation", out JsonElement recipe))
        {
            Require(acquisition == "local" && kind != "connection-string");
            Only(recipe, kind switch
            {
                "password" => ["length"],
                "opaque-token" or "symmetric-key" => ["bytes"],
                "password-hash" or "digest" => ["input", "inputEncoding", "password"],
                _ => []
            });
            Require(kind is not ("password-hash" or "digest") || recipe.TryGetProperty("input", out _));
            string input = Text(recipe, "input", "entered");
            string inputEncoding = Text(recipe, "inputEncoding", "utf8");
            Require(input is "entered" or "generated");
            Require(inputEncoding is "utf8" or "base64" or "base64url" or "hex");
            Require(kind != "password-hash" || inputEncoding == "utf8");
            Require(input != "generated" || inputEncoding == "utf8");
            DefinitionSecretSpecification? password = null;
            if (recipe.TryGetProperty("password", out JsonElement passwordSource))
            {
                Require(input == "generated" && passwordSource.TryGetProperty("kind", out JsonElement passwordKind)
                    && passwordKind.GetString() == "password");
                password = Read(passwordSource, default);
                Require(password.Acquisition == "local" && password.Generation is not null);
                Require(algorithm != "bcrypt" || password.Generation!.Length <= 72);
            }
            Require(input != "generated" || password is not null);
            generation = new()
            {
                Length = Number(recipe, "length", 32, 1, 1024),
                Bytes = Number(recipe, "bytes", Math.Min(maximumBytes, Math.Max(32, minimumBytes)), 1, 4096),
                Input = input,
                InputEncoding = inputEncoding,
                Password = password
            };
            if (kind == "password")
            {
                Require(generation.Length >= Math.Max(minimum, required) && (maximum is null || generation.Length <= maximum)
                    && (constraints.MaxUtf8Bytes is null || generation.Length <= constraints.MaxUtf8Bytes));
                string alphabet = new(("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789" + constraints.AllowedSymbols)
                    .Where(c => !constraints.ForbiddenCharacters.Contains(c)).Distinct().ToArray());
                Require(alphabet.Length > 1 && (generation.Length - required) * Math.Log2(alphabet.Length) >= 128);
                Require((constraints.MinLowercase == 0 || alphabet.Any(char.IsAsciiLetterLower))
                    && (constraints.MinUppercase == 0 || alphabet.Any(char.IsAsciiLetterUpper))
                    && (constraints.MinDigits == 0 || alphabet.Any(char.IsAsciiDigit))
                    && (constraints.MinSymbols == 0 || alphabet.Any(c => !char.IsAsciiLetterOrDigit(c))));
            }
            if (kind is "opaque-token" or "symmetric-key")
            {
                Require(generation.Bytes >= Math.Max(16, constraints.MinBytes) && generation.Bytes <= constraints.MaxBytes);
                int outputLength = EncodedLength(generation.Bytes, encoding!);
                Require(outputLength >= minimum && (maximum is null || outputLength <= maximum));
            }
        }
        return new()
        {
            Kind = kind,
            Acquisition = acquisition,
            Algorithm = algorithm,
            Encoding = encoding,
            MinLength = minimum,
            MaxLength = maximum,
            Constraints = constraints,
            Generation = generation
        };
    }

    private static int EncodedLength(int bytes, string encoding) => encoding switch
    {
        "hex" => bytes * 2,
        "base64" => 4 * ((bytes + 2) / 3),
        _ => (bytes * 8 + 5) / 6
    };

    private static void Only(JsonElement value, params string[] names)
    {
        Require(value.ValueKind == JsonValueKind.Object);
        foreach (JsonProperty property in value.EnumerateObject()) Require(names.Contains(property.Name, StringComparer.Ordinal));
    }

    private static string? OptionalText(JsonElement source, string name)
    {
        if (!source.TryGetProperty(name, out JsonElement value)) return null;
        Require(value.ValueKind == JsonValueKind.String);
        return value.GetString();
    }

    private static string Text(JsonElement source, string name, string fallback)
    {
        string result = OptionalText(source, name) ?? fallback;
        Require(result.Length <= 1024);
        return result;
    }

    private static int Number(JsonElement source, string name, int fallback, int minimum, int maximum)
    {
        int result = source.ValueKind == JsonValueKind.Object && source.TryGetProperty(name, out JsonElement value) ? value.GetInt32() : fallback;
        Require(result >= minimum && result <= maximum);
        return result;
    }

    private static void Require(bool condition)
    {
        if (!condition) throw new DefinitionException("secret-policy-invalid");
    }
}
