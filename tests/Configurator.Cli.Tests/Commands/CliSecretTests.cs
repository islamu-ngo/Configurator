using System.Text;
using System.Text.Json;

namespace Configurator.Cli.Tests;

public sealed class CliSecretTests
{
    private const string GeneratedDigest = """
        {"kind":"digest","acquisition":"local","algorithm":"SHA256","encoding":"hex",
        "generation":{"input":"generated","password":{"kind":"password","acquisition":"local","generation":{"length":32}}}}
        """;
    private const string Password = """{"kind":"password","acquisition":"local","generation":{"length":32}}""";

    [Test]
    [Arguments("issuer")]
    [Arguments("existing")]
    public async Task NonlocalAcquisitionRefusesSecretGeneration(string acquisition)
    {
        var io = SecretFixture("{\"kind\":\"password\",\"acquisition\":\"" + acquisition + "\"}");
        await Assert.That(await Generate(io)).IsEqualTo(CliExitCode.Blocked);
        await Assert.That(io.Files.ContainsKey("new.json")).IsFalse();
    }

    [Test]
    public async Task ExplicitSecretStdinIsSeparateFromContextualAnswers()
    {
        var io = SecretFixture("""{"kind":"digest","acquisition":"local","algorithm":"SHA256","encoding":"hex","generation":{"input":"entered"}}""");
        io.Files["-"] = "abc"u8.ToArray();
        await Assert.That(await io.RunAsync("secret", "derive", "--definition", "definition.json", "--pointer", "/password",
            "--input", "answers.json", "--output", "new.json", "--secret-input-stdin", "--machine"))
            .IsEqualTo(CliExitCode.Success);
        using JsonDocument answer = JsonDocument.Parse(io.Files["new.json"]);
        await Assert.That(answer.RootElement.GetProperty("values").GetProperty("password").GetString())
            .IsEqualTo("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad");
    }

    [Test]
    [Arguments("opaque-token", null, "hex")]
    [Arguments("opaque-token", null, "base64")]
    [Arguments("opaque-token", null, "base64url")]
    [Arguments("symmetric-key", "HS256", "base64url")]
    [Arguments("symmetric-key", "HS384", "base64url")]
    [Arguments("symmetric-key", "HS512", "base64url")]
    [Arguments("symmetric-key", "AES128", "base64")]
    [Arguments("symmetric-key", "AES192", "base64")]
    [Arguments("symmetric-key", "AES256", "base64")]
    [Arguments("password-hash", "argon2id", null)]
    [Arguments("password-hash", "pbkdf2-sha256", null)]
    [Arguments("password-hash", "pbkdf2-sha512", null)]
    [Arguments("password-hash", "aspnet-identity-v3", null)]
    [Arguments("password-hash", "bcrypt", null)]
    [Arguments("digest", "SHA256", "hex")]
    [Arguments("digest", "SHA384", "base64")]
    [Arguments("digest", "SHA512", "base64url")]
    [Arguments("private-key", "RS256", "pkcs8-pem")]
    [Arguments("private-key", "PS256", "pkcs8-pem")]
    [Arguments("private-key", "ES256", "pkcs8-pem")]
    [Arguments("private-key", "ES384", "pkcs8-pem")]
    [Arguments("private-key", "ES512", "pkcs8-pem")]
    [Arguments("private-key", "RS256", "jwk")]
    [Arguments("private-key", "PS256", "jwk")]
    [Arguments("private-key", "ES256", "jwk")]
    [Arguments("private-key", "ES384", "jwk")]
    [Arguments("private-key", "ES512", "jwk")]
    public async Task EveryAdmittedProfileUsesTheCliOperation(string kind, string? algorithm, string? encoding)
    {
        bool sidecar = kind is "password-hash" or "digest";
        var recipe = new System.Text.Json.Nodes.JsonObject();
        if (sidecar)
        {
            recipe["input"] = "generated";
            recipe["password"] = System.Text.Json.Nodes.JsonNode.Parse(Password);
        }
        var specification = new System.Text.Json.Nodes.JsonObject
        { ["kind"] = kind, ["acquisition"] = "local", ["generation"] = recipe };
        if (algorithm is not null) specification["algorithm"] = algorithm;
        if (encoding is not null) specification["encoding"] = encoding;
        var io = SecretFixture(specification.ToJsonString());
        await Assert.That(await Generate(io, sidecar ? ["--generated-password-output", "password.txt"] : []))
            .IsEqualTo(CliExitCode.Success);
        await Assert.That(io.Files.ContainsKey("new.json")).IsTrue();
        await Assert.That(io.Files.ContainsKey("password.txt")).IsEqualTo(sidecar);
        await Assert.That(CliMachineContractVerifier.Validate(io.StandardOutput)).IsEmpty();
    }

    [Test]
    public async Task NativePublicationCreatesRestrictedFilesAndRefusesExistingDestinations()
    {
        var io = SecretFixture(GeneratedDigest);
        string directory = Path.Combine(Path.GetTempPath(), "configurator-secret-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string sidecar = Path.Combine(directory, "password.txt");
            string outputPath = Path.Combine(directory, "answers.json");
            using var output = new MemoryStream();
            using var error = new MemoryStream();
            var writer = new CliProgram.SystemWriter(output);
            var invocation = new CliInvocation(["secret", "generate", "--definition", "definition.json", "--pointer", "/password",
                "--input", "answers.json", "--output", outputPath, "--generated-password-output", sidecar, "--machine"],
                CliMode.Machine, new(io, writer, new CliProgram.SystemWriter(error), CliFixture.RejectingAuthoringFiles, 65_536, 4 * 1024 * 1024));
            CliExitCode first = await new CliApplication().RunAsync(invocation);
            await Assert.That(first).IsEqualTo(OperatingSystem.IsLinux() ? CliExitCode.Success : CliExitCode.Blocked);
            await Assert.That(Encoding.UTF8.GetString(output.ToArray())).DoesNotContain(directory);
            if (OperatingSystem.IsLinux())
            {
                await Assert.That(File.GetUnixFileMode(sidecar)).IsEqualTo(UnixFileMode.UserRead | UnixFileMode.UserWrite);
                await Assert.That(File.GetUnixFileMode(outputPath)).IsEqualTo(UnixFileMode.UserRead | UnixFileMode.UserWrite);
                byte[] original = await File.ReadAllBytesAsync(sidecar);
                await Assert.That(original.Length).IsEqualTo(32);
                await Assert.That(await new CliApplication().RunAsync(invocation)).IsEqualTo(CliExitCode.Blocked);
                byte[] retained = await File.ReadAllBytesAsync(sidecar);
                await Assert.That(retained.AsSpan().SequenceEqual(original)).IsTrue();
                System.Security.Cryptography.CryptographicOperations.ZeroMemory(retained);
                System.Security.Cryptography.CryptographicOperations.ZeroMemory(original);
            }
            else
            {
                await Assert.That(File.Exists(sidecar)).IsFalse();
                await Assert.That(File.Exists(outputPath)).IsFalse();
            }
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Test]
    public async Task CancellationAfterSidecarRetainsEstablishedPublicationFacts()
    {
        var io = SecretFixture(GeneratedDigest);
        using var cancellation = new CancellationTokenSource();
        io.ArtifactPublished = _ => cancellation.Cancel();
        string[] arguments = ["secret", "generate", "--definition", "definition.json", "--pointer", "/password",
            "--input", "answers.json", "--output", "new.json", "--generated-password-output", "password.txt", "--machine"];
        CliExitCode exit = await new CliApplication().RunAsync(new(arguments, CliMode.Machine,
            new(io, io, io, CliFixture.RejectingAuthoringFiles, 65_536, 4 * 1024 * 1024)), cancellation.Token);
        await Assert.That(exit).IsEqualTo(CliExitCode.Cancelled);
        using JsonDocument receipt = JsonDocument.Parse(io.StandardOutput);
        await Assert.That(receipt.RootElement.GetProperty("artifacts")[0].GetProperty("installation").GetString()).IsEqualTo("established");
        await Assert.That(receipt.RootElement.GetProperty("artifacts")[1].GetProperty("writeStatus").GetString()).IsEqualTo("not-attempted");
        await Assert.That(io.Files.ContainsKey("password.txt")).IsTrue();
        await Assert.That(io.Files.ContainsKey("new.json")).IsFalse();
    }

    [Test]
    public async Task ConcurrentAnswerConflictDoesNotEraseTheSavedPassword()
    {
        var io = SecretFixture(GeneratedDigest);
        io.ArtifactPublished = _ => io.Files["new.json"] = "other-writer"u8.ToArray();
        await Assert.That(await Generate(io, "--generated-password-output", "password.txt")).IsEqualTo(CliExitCode.Blocked);
        await Assert.That(io.Files.ContainsKey("password.txt")).IsTrue();
        await Assert.That(Encoding.UTF8.GetString(io.Files["new.json"])).IsEqualTo("other-writer");
        await Assert.That(CliMachineContractVerifier.Validate(io.StandardOutput)).IsEmpty();
    }

    [Test]
    public async Task UnsupportedProtectedWriterRefusesBeforePublication()
    {
        var io = SecretFixture(Password);
        io.ArtifactDestinationsBlocked = true;
        await Assert.That(await Generate(io)).IsEqualTo(CliExitCode.Blocked);
        await Assert.That(io.Files.ContainsKey("new.json")).IsFalse();
    }

    [Test]
    public async Task NestedArrayPointerPreservesOtherRows()
    {
        var io = NestedFixture();
        await Assert.That(await GenerateAt(io, "/users/1/password")).IsEqualTo(CliExitCode.Success);
        using JsonDocument answer = JsonDocument.Parse(io.Files["new.json"]);
        JsonElement users = answer.RootElement.GetProperty("values").GetProperty("users");
        await Assert.That(users[0].GetProperty("password").GetString()).IsEqualTo("synthetic-first-row");
        await Assert.That(users[1].GetProperty("password").GetString()!.Length).IsEqualTo(32);
    }

    [Test]
    public async Task ExplicitGenerationSuppliesAnOmittedLeaf()
    {
        var io = SecretFixture(Password);
        var answers = System.Text.Json.Nodes.JsonNode.Parse(Encoding.UTF8.GetString(io.Files["answers.json"]))!;
        answers["values"]!.AsObject().Clear();
        answers["omitted"] = new System.Text.Json.Nodes.JsonArray("/password");
        io.Files["answers.json"] = Encoding.UTF8.GetBytes(answers.ToJsonString());
        await Assert.That(await Generate(io)).IsEqualTo(CliExitCode.Success);
        using JsonDocument output = JsonDocument.Parse(io.Files["new.json"]);
        await Assert.That(output.RootElement.GetProperty("values").GetProperty("password").GetString()!.Length).IsEqualTo(32);
        await Assert.That(output.RootElement.GetProperty("omitted").EnumerateArray().Any()).IsFalse();
    }

    [Test]
    public async Task OmittedAncestorCannotBeRepopulatedByGeneration()
    {
        var io = NestedFixture();
        var answers = System.Text.Json.Nodes.JsonNode.Parse(Encoding.UTF8.GetString(io.Files["answers.json"]))!;
        answers["values"]!.AsObject().Remove("users");
        answers["omitted"] = new System.Text.Json.Nodes.JsonArray("/users");
        io.Files["answers.json"] = Encoding.UTF8.GetBytes(answers.ToJsonString());
        await Assert.That(await GenerateAt(io, "/users/1/password")).IsEqualTo(CliExitCode.Blocked);
        await Assert.That(io.Files.ContainsKey("new.json")).IsFalse();
    }

    [Test]
    public async Task ReadOnlyAncestorRefusesGeneration()
    {
        var io = NestedFixture();
        var definition = System.Text.Json.Nodes.JsonNode.Parse(Encoding.UTF8.GetString(io.Files["definition.json"]))!;
        definition["configuration"]!["properties"]!["users"]!["readOnly"] = true;
        io.Files["definition.json"] = Encoding.UTF8.GetBytes(definition.ToJsonString());
        await Assert.That(await GenerateAt(io, "/users/1/password")).IsEqualTo(CliExitCode.Blocked);
    }

    [Test]
    public async Task LiteralEscapedMapPointerNeverEntersTheReceipt()
    {
        var io = SecretFixture(Password);
        var definition = System.Text.Json.Nodes.JsonNode.Parse(Encoding.UTF8.GetString(io.Files["definition.json"]))!;
        definition["configuration"]!["properties"]!["accounts"] = System.Text.Json.Nodes.JsonNode.Parse(
            "{\"type\":\"object\",\"additionalProperties\":{\"type\":\"string\",\"x-configurator\":{\"secret\":" + Password + "}}}");
        io.Files["definition.json"] = Encoding.UTF8.GetBytes(definition.ToJsonString());
        io.Files["answers.json"] = "{\"answerContractVersion\":\"0.0.1\",\"values\":{\"service.name\":\"private-answer-marker\",\"accounts\":{\"private/account~name\":\"synthetic-existing\"}},\"omitted\":[]}"u8.ToArray();
        await Assert.That(await GenerateAt(io, "/accounts/private~1account~0name")).IsEqualTo(CliExitCode.Success);
        await Assert.That(Encoding.UTF8.GetString(io.StandardOutput)).DoesNotContain("private~1account");
        await Assert.That(Encoding.UTF8.GetString(io.StandardOutput)).DoesNotContain("private/account");
        using JsonDocument answer = JsonDocument.Parse(io.Files["new.json"]);
        await Assert.That(answer.RootElement.GetProperty("values").GetProperty("accounts").GetProperty("private/account~name").GetString()!.Length).IsEqualTo(32);
    }

    [Test]
    public async Task DeclaredMapSentinelNameCannotBorrowAnotherEntriesActivity()
    {
        var io = SecretFixture(Password);
        var definition = System.Text.Json.Nodes.JsonNode.Parse(Encoding.UTF8.GetString(io.Files["definition.json"]))!;
        var secret = System.Text.Json.Nodes.JsonNode.Parse("{\"type\":\"string\",\"x-configurator\":{\"secret\":" + Password + "}}")!;
        var inactive = secret.DeepClone();
        inactive["x-configurator"]!["applicableWhen"] = System.Text.Json.Nodes.JsonNode.Parse(
            """{"op":"equals","reference":{"scope":"root","path":"/enabled"},"value":false}""");
        definition["configuration"]!["properties"]!["accounts"] = new System.Text.Json.Nodes.JsonObject
        {
            ["type"] = "object",
            ["properties"] = new System.Text.Json.Nodes.JsonObject
            {
                ["@entry"] = new System.Text.Json.Nodes.JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["properties"] = new System.Text.Json.Nodes.JsonObject { ["0"] = inactive }
                }
            },
            ["additionalProperties"] = secret
        };
        io.Files["definition.json"] = Encoding.UTF8.GetBytes(definition.ToJsonString());
        io.Files["answers.json"] = "{\"answerContractVersion\":\"0.0.1\",\"values\":{\"service.name\":\"private-answer-marker\",\"enabled\":true,\"accounts\":{\"@entry\":{\"0\":\"unchanged\"},\"foo\":\"synthetic-existing\"}},\"omitted\":[]}"u8.ToArray();
        await Assert.That(await GenerateAt(io, "/accounts/@entry/0")).IsEqualTo(CliExitCode.Blocked);
        await Assert.That(io.Files.ContainsKey("new.json")).IsFalse();
    }

    [Test]
    public async Task OversizedSecretTransportCannotReachGeneration()
    {
        var io = SecretFixture("""{"kind":"digest","acquisition":"local","algorithm":"SHA256","encoding":"hex","generation":{"input":"entered"}}""");
        io.Files["private.txt"] = new byte[65_537];
        await Assert.That(await io.RunAsync("secret", "derive", "--definition", "definition.json", "--pointer", "/password",
            "--input", "answers.json", "--output", "new.json", "--secret-input-file", "private.txt", "--machine"))
            .IsEqualTo(CliExitCode.Data);
        await Assert.That(io.Files.ContainsKey("new.json")).IsFalse();
    }

    [Test]
    public async Task GenerationChangesOnlyTheSelectedAnswerAndHidesItsDigest()
    {
        var io = SecretFixture(Password);
        await Assert.That(await Generate(io)).IsEqualTo(CliExitCode.Success);
        using JsonDocument answer = JsonDocument.Parse(io.Files["new.json"]);
        await Assert.That(answer.RootElement.GetProperty("values").GetProperty("password").GetString()!.Length).IsEqualTo(32);
        await Assert.That(answer.RootElement.GetProperty("values").GetProperty("service.name").GetString()).IsEqualTo("private-answer-marker");
        using JsonDocument receipt = JsonDocument.Parse(io.StandardOutput);
        await Assert.That(receipt.RootElement.GetProperty("artifacts")[0].TryGetProperty("digest", out _)).IsFalse();
        await Assert.That(CliMachineContractVerifier.Validate(io.StandardOutput)).IsEmpty();
    }

    [Test]
    public async Task DerivationUsesExplicitBoundedUtf8Input()
    {
        var io = SecretFixture("""{"kind":"digest","acquisition":"local","algorithm":"SHA256","encoding":"hex","generation":{"input":"entered"}}""");
        io.Files["secret.txt"] = "abc"u8.ToArray();
        await Assert.That(await io.RunAsync("secret", "derive", "--definition", "definition.json", "--pointer", "/password",
            "--input", "answers.json", "--output", "new.json", "--secret-input-file", "secret.txt", "--machine"))
            .IsEqualTo(CliExitCode.Success);
        using JsonDocument answer = JsonDocument.Parse(io.Files["new.json"]);
        await Assert.That(answer.RootElement.GetProperty("values").GetProperty("password").GetString())
            .IsEqualTo("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad");
        await Assert.That(Encoding.UTF8.GetString(io.StandardOutput)).DoesNotContain("ba7816bf");
    }

    [Test]
    public async Task PartialContextProducesIncompleteReadinessWithoutDiscardingOutput()
    {
        var io = SecretFixture(Password);
        io.Files["answers.json"] = "{\"answerContractVersion\":\"0.0.1\",\"values\":{},\"omitted\":[]}"u8.ToArray();
        await Assert.That(await Generate(io)).IsEqualTo(CliExitCode.Success);
        using JsonDocument receipt = JsonDocument.Parse(io.StandardOutput);
        await Assert.That(receipt.RootElement.GetProperty("readiness").GetProperty("state").GetString()).IsEqualTo("incomplete");
        await Assert.That(io.Files.ContainsKey("new.json")).IsTrue();
    }

    [Test]
    public async Task FirstWriteFailureNeverPublishesTheDerivedAnswer()
    {
        var io = SecretFixture(GeneratedDigest);
        io.ArtifactFailurePath = "password.txt";
        await Assert.That(await Generate(io, "--generated-password-output", "password.txt")).IsEqualTo(CliExitCode.Io);
        await Assert.That(io.Files.ContainsKey("new.json")).IsFalse();
        await Assert.That(io.Files.ContainsKey("password.txt")).IsFalse();
    }

    [Test]
    public async Task DestinationsMustBeDistinctAndCannotReplaceTheInput()
    {
        var io = SecretFixture(GeneratedDigest);
        await Assert.That(await Generate(io, "--generated-password-output", "./new.json")).IsEqualTo(CliExitCode.Usage);
        await Assert.That(io.Files.ContainsKey("new.json")).IsFalse();
    }

    [Test]
    public async Task GeneratedInputRequiresAnExplicitPlaintextDestination()
    {
        var io = SecretFixture(GeneratedDigest);
        await Assert.That(await Generate(io)).IsEqualTo(CliExitCode.Usage);
        await Assert.That(Encoding.UTF8.GetString(io.StandardOutput)).Contains("generated-password-output-required");
        await Assert.That(io.Files.ContainsKey("new.json")).IsFalse();
    }

    [Test]
    public async Task ExistingPlaintextDestinationPreventsAnswerPublication()
    {
        var io = SecretFixture(GeneratedDigest);
        io.Files["password.txt"] = "preserved"u8.ToArray();
        await Assert.That(await Generate(io, "--generated-password-output", "password.txt"))
            .IsEqualTo(CliExitCode.Blocked);
        await Assert.That(io.Files.ContainsKey("new.json")).IsFalse();
        await Assert.That(Encoding.UTF8.GetString(io.Files["password.txt"])).IsEqualTo("preserved");
    }

    [Test]
    public async Task AnswerWriteFailurePreservesOriginalPasswordAndTruthfulReceipt()
    {
        var io = SecretFixture(GeneratedDigest);
        io.ArtifactFailurePath = "new.json";
        await Assert.That(await Generate(io, "--generated-password-output", "password.txt"))
            .IsEqualTo(CliExitCode.Io);
        await Assert.That(io.Files.ContainsKey("password.txt")).IsTrue();
        await Assert.That(io.Files.ContainsKey("new.json")).IsFalse();
        await Assert.That(Encoding.UTF8.GetString(io.Files["answers.json"])).IsEqualTo(CliFixture.Answers);
        using JsonDocument receipt = JsonDocument.Parse(io.StandardOutput);
        JsonElement sidecar = receipt.RootElement.GetProperty("artifacts")[0];
        await Assert.That(sidecar.GetProperty("id").GetString()).IsEqualTo("generated-password-output");
        await Assert.That(sidecar.GetProperty("installation").GetString()).IsEqualTo("established");
        await Assert.That(sidecar.TryGetProperty("digest", out _)).IsFalse();
        await Assert.That(Encoding.UTF8.GetString(io.StandardOutput).Contains(
            Encoding.UTF8.GetString(io.Files["password.txt"]), StringComparison.Ordinal)).IsFalse();
    }

    [Test]
    public async Task DerivationRequiresOnePrivateInputTransport()
    {
        var io = new CliFixture();
        await Assert.That(await io.RunAsync("secret", "derive", "--definition", "definition.json",
            "--pointer", "/password", "--input", "answers.json", "--output", "new.json", "--machine"))
            .IsEqualTo(CliExitCode.Usage);
        using JsonDocument receipt = JsonDocument.Parse(io.StandardOutput);
        await Assert.That(receipt.RootElement.GetProperty("diagnostics")[0].GetProperty("code").GetString())
            .IsEqualTo("secret-input-required");
        await Assert.That(io.Files.ContainsKey("new.json")).IsFalse();
    }

    [Test]
    public async Task PrivateValuesCannotBeSuppliedInArguments()
    {
        var io = new CliFixture();
        await Assert.That(await io.RunAsync("secret", "derive", "--secret", "synthetic-private-marker", "--machine"))
            .IsEqualTo(CliExitCode.Usage);
        await Assert.That(Encoding.UTF8.GetString(io.StandardOutput)).DoesNotContain("synthetic-private-marker");
    }

    private static CliFixture SecretFixture(string specification)
    {
        var io = new CliFixture();
        var definition = System.Text.Json.Nodes.JsonNode.Parse(CliFixture.Definition)!;
        definition["configuration"]!["properties"]!["password"] = System.Text.Json.Nodes.JsonNode.Parse(
            "{\"type\":\"string\",\"x-configurator\":{\"secret\":" + specification + "}}");
        io.Files["definition.json"] = Encoding.UTF8.GetBytes(definition.ToJsonString());
        return io;
    }

    private static Task<CliExitCode> Generate(CliFixture io, params string[] extra) => io.RunAsync(
        ["secret", "generate", "--definition", "definition.json", "--pointer", "/password", "--input", "answers.json",
            "--output", "new.json", "--machine", .. extra]);

    private static Task<CliExitCode> GenerateAt(CliFixture io, string pointer) => io.RunAsync(
        "secret", "generate", "--definition", "definition.json", "--pointer", pointer,
        "--input", "answers.json", "--output", "new.json", "--machine");

    private static CliFixture NestedFixture()
    {
        var io = SecretFixture(Password);
        var definition = System.Text.Json.Nodes.JsonNode.Parse(Encoding.UTF8.GetString(io.Files["definition.json"]))!;
        definition["configuration"]!["properties"]!["users"] = System.Text.Json.Nodes.JsonNode.Parse(
            "{\"type\":\"array\",\"items\":{\"type\":\"object\",\"additionalProperties\":false,\"properties\":{\"password\":{\"type\":\"string\",\"x-configurator\":{\"secret\":" + Password + "}}}}}");
        io.Files["definition.json"] = Encoding.UTF8.GetBytes(definition.ToJsonString());
        io.Files["answers.json"] = "{\"answerContractVersion\":\"0.0.1\",\"values\":{\"service.name\":\"private-answer-marker\",\"users\":[{\"password\":\"synthetic-first-row\"},{}]},\"omitted\":[]}"u8.ToArray();
        return io;
    }
}
