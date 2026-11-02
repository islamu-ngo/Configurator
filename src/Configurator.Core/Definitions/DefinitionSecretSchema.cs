namespace Configurator.Core.Definitions;

using System.Text.Json.Nodes;
using static Configurator.Core.Definitions.DefinitionSchema;

internal static class DefinitionSecretSchema
{
    internal static JsonObject Create(bool passwordInput = false)
    {
        var variants = new JsonArray();
        string[] kinds = passwordInput ? ["password"]
            : ["password", "opaque-token", "symmetric-key", "private-key", "password-hash", "digest", "connection-string"];
        foreach (string kind in kinds)
        {
            string?[] algorithms = kind switch
            {
                "symmetric-key" => ["HS256", "HS384", "HS512", "AES128", "AES192", "AES256"],
                "private-key" => ["RS256", "PS256", "ES256", "ES384", "ES512"],
                "password-hash" => ["argon2id", "pbkdf2-sha256", "pbkdf2-sha512", "aspnet-identity-v3", "bcrypt"],
                "digest" => ["SHA256", "SHA384", "SHA512"],
                _ => [null]
            };
            foreach (string? algorithm in algorithms)
                variants.Add(SecretVariant(kind, algorithm, passwordInput));
        }
        return new JsonObject { ["oneOf"] = variants };
    }

    private static JsonObject SecretVariant(string kind, string? algorithm, bool passwordInput)
    {
        var required = new List<string> { "kind", "acquisition" };
        var properties = new JsonObject
        {
            ["kind"] = Choice(kind),
            ["acquisition"] = passwordInput ? Choice("local") : Choice("local", "existing", "issuer")
        };
        if (algorithm is not null)
        {
            properties["algorithm"] = Choice(algorithm);
            required.Add("algorithm");
        }
        if (kind is "opaque-token" or "symmetric-key" or "private-key" or "digest")
        {
            properties["encoding"] = kind == "private-key" ? Choice("pkcs8-pem", "jwk") : Choice("base64", "base64url", "hex");
            required.Add("encoding");
        }
        var constraints = new JsonObject();
        if (kind == "password")
        {
            constraints["characterSet"] = Choice("unicode", "ascii-printable");
            constraints["allowWhitespace"] = new JsonObject { ["type"] = "boolean" };
            constraints["allowedSymbols"] = new JsonObject { ["type"] = "string", ["maxLength"] = 94 };
            constraints["forbiddenCharacters"] = new JsonObject { ["type"] = "string", ["maxLength"] = 1024 };
            constraints["maxUtf8Bytes"] = Integer(1, 65536);
            foreach (string key in new[] { "minLowercase", "minUppercase", "minDigits", "minSymbols" })
                constraints[key] = Integer(0, 1024);
        }
        if (kind is "opaque-token" or "symmetric-key")
        {
            int minimum = algorithm switch
            {
                "HS256" or "AES256" => 32,
                "HS384" => 48,
                "HS512" => 64,
                "AES128" => 16,
                "AES192" => 24,
                _ => 0
            };
            int maximum = algorithm is "AES128" or "AES192" or "AES256" ? minimum : 4096;
            constraints["minBytes"] = Integer(minimum, maximum);
            constraints["maxBytes"] = Integer(minimum, maximum);
        }
        if (kind == "private-key")
        {
            if (algorithm is "RS256" or "PS256")
                constraints["keySize"] = new JsonObject { ["enum"] = new JsonArray(2048, 3072, 4096) };
            else constraints["curve"] = Choice(algorithm switch { "ES256" => "P-256", "ES384" => "P-384", _ => "P-521" });
        }
        if (kind == "password-hash")
        {
            if (algorithm is "argon2id" or "pbkdf2-sha256" or "pbkdf2-sha512")
            {
                constraints["saltBytes"] = Integer(16, 64);
                constraints["derivedBytes"] = Integer(16, 64);
                constraints["iterations"] = algorithm == "argon2id" ? Integer(2, 6)
                    : Integer(algorithm == "pbkdf2-sha256" ? 600000 : 210000, 2000000);
            }
            if (algorithm == "argon2id")
            {
                constraints["memoryKiB"] = Integer(19456, 65536);
                constraints["parallelism"] = Integer(1, 4);
            }
            if (algorithm == "bcrypt") constraints["cost"] = Integer(10, 14);
        }
        properties["constraints"] = Closed(constraints);
        if (kind != "connection-string")
        {
            var generation = new JsonObject();
            if (kind == "password") generation["length"] = Integer(1, 1024);
            if (kind is "opaque-token" or "symmetric-key") generation["bytes"] = Integer(16, 4096);
            if (kind is "password-hash" or "digest")
            {
                generation["input"] = Choice("entered", "generated");
                generation["inputEncoding"] = kind == "password-hash" ? Choice("utf8") : Choice("utf8", "hex", "base64", "base64url");
                generation["password"] = Create(passwordInput: true);
            }
            properties["generation"] = Closed(generation, kind is "password-hash" or "digest" ? ["input"] : []);
        }
        if (passwordInput) required.Add("generation");
        JsonObject result = Closed(properties, required.ToArray());
        result["if"] = new JsonObject { ["required"] = new JsonArray("generation") };
        result["then"] = new JsonObject { ["properties"] = new JsonObject { ["acquisition"] = Choice("local") } };
        return result;
    }

    private static JsonObject Integer(int minimum, int maximum) => new()
    {
        ["type"] = "integer",
        ["minimum"] = minimum,
        ["maximum"] = maximum
    };
}
