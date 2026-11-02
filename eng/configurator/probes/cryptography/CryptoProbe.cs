using System.Diagnostics;
using Configurator.Core.Definitions;
using Configurator.Core.Secrets;

namespace Configurator.DependencyProbes.Cryptography;

internal sealed record CryptoProbeProfile(string Name, DefinitionSecretSpecification Specification, string Input = "");
internal sealed record CryptoProbeResult(string Profile, string Status, long ElapsedMilliseconds, long HeapBefore, long HeapAfter);

internal static class CryptoProbe
{
    public const int ExpectedProfileCount = 37;

    public static IEnumerable<CryptoProbeProfile> Profiles()
    {
        var password = new DefinitionSecretSpecification
        {
            Kind = "password",
            Acquisition = "local",
            MinLength = 16,
            MaxLength = 128,
            Constraints = new() { CharacterSet = "ascii-printable", AllowWhitespace = false, MinLowercase = 1, MinUppercase = 1, MinDigits = 1, MinSymbols = 1 },
            Generation = new() { Length = 32 },
        };
        yield return new("password", password);
        foreach (string encoding in new[] { "base64", "base64url", "hex" })
            yield return new($"opaque-token/{encoding}", new() { Kind = "opaque-token", Acquisition = "local", Encoding = encoding, Generation = new() { Bytes = 32 } });
        foreach (var (algorithm, bytes) in new[] { ("HS256", 32), ("HS384", 48), ("HS512", 64), ("AES128", 16), ("AES192", 24), ("AES256", 32) })
            yield return new($"symmetric-key/{algorithm}", new() { Kind = "symmetric-key", Acquisition = "local", Algorithm = algorithm, Encoding = "base64url", Constraints = new() { MinBytes = bytes, MaxBytes = bytes }, Generation = new() { Bytes = bytes } });
        foreach (string algorithm in new[] { "argon2id", "pbkdf2-sha256", "pbkdf2-sha512", "aspnet-identity-v3", "bcrypt" })
            yield return new($"password-hash/{algorithm}", new()
            {
                Kind = "password-hash",
                Acquisition = "local",
                Algorithm = algorithm,
                Constraints = new() { Iterations = algorithm switch { "pbkdf2-sha256" => 600000, "pbkdf2-sha512" or "aspnet-identity-v3" => 210000, _ => 2 } },
                Generation = new() { Input = "entered" },
            }, "synthetic-probe-password");
        yield return new("password-hash/generated-input", new()
        {
            Kind = "password-hash",
            Acquisition = "local",
            Algorithm = "aspnet-identity-v3",
            Generation = new() { Input = "generated", Password = password },
        });
        foreach (string algorithm in new[] { "SHA256", "SHA384", "SHA512" })
            yield return new($"digest/{algorithm}", new() { Kind = "digest", Acquisition = "local", Algorithm = algorithm, Encoding = "hex", Generation = new() { Input = "entered", InputEncoding = "utf8" } }, "abc");
        foreach (string encoding in new[] { "pkcs8-pem", "jwk" })
        {
            foreach (string algorithm in new[] { "RS256", "PS256" })
                foreach (int bits in new[] { 2048, 3072, 4096 })
                    yield return new($"private-key/{algorithm}/{bits}/{encoding}", new() { Kind = "private-key", Acquisition = "local", Algorithm = algorithm, Encoding = encoding, Constraints = new() { KeySize = bits }, Generation = new() });
            foreach (var (algorithm, curve) in new[] { ("ES256", "P-256"), ("ES384", "P-384"), ("ES512", "P-521") })
                yield return new($"private-key/{algorithm}/{encoding}", new() { Kind = "private-key", Acquisition = "local", Algorithm = algorithm, Encoding = encoding, Constraints = new() { Curve = curve }, Generation = new() });
        }
    }

    public static CryptoProbeResult Run(CryptoProbeProfile profile)
    {
        long before = GC.GetTotalMemory(false);
        var watch = Stopwatch.StartNew();
        string status;
        try
        {
            using var result = SecretGenerator.Generate(profile.Specification, profile.Input.AsSpan());
            status = !result.Succeeded || result.Output is null ? "generation-failed"
                : SecretValueValidator.Validate(profile.Specification, result.Output.CopyValue()).Count != 0 ? "validation-failed"
                : profile.Specification.Generation?.Input == "generated" && result.GeneratedPassword is null ? "original-password-missing"
                : "passed";
            if (status == "passed" && profile.Name == "digest/SHA256"
                && result.Output!.CopyValue() != "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad")
                status = "known-vector-failed";
        }
        catch (Exception)
        {
            // Exceptions may contain key material; the probe records only a fixed failure classification.
            status = "operation-threw";
        }
        watch.Stop();
        return new(profile.Name, status, watch.ElapsedMilliseconds, before, GC.GetTotalMemory(false));
    }
}
