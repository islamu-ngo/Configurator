namespace Configurator.Desktop.Tests.ViewModels;

using System.Text;
using System.Text.Json.Nodes;
using Configurator.Core.Definitions;
using Configurator.Desktop.Services;
using Configurator.Desktop.ViewModels.Fields;
using Configurator.Presentation;

public sealed class SecretFieldViewModelTests
{
    [Test]
    [Arguments("password", null, null)]
    [Arguments("opaque-token", null, "base64url")]
    [Arguments("symmetric-key", "HS256", "base64")]
    [Arguments("symmetric-key", "HS384", "base64url")]
    [Arguments("symmetric-key", "HS512", "hex")]
    [Arguments("symmetric-key", "AES128", "base64")]
    [Arguments("symmetric-key", "AES192", "base64url")]
    [Arguments("symmetric-key", "AES256", "hex")]
    [Arguments("password-hash", "argon2id", null)]
    [Arguments("password-hash", "pbkdf2-sha256", null)]
    [Arguments("password-hash", "pbkdf2-sha512", null)]
    [Arguments("password-hash", "aspnet-identity-v3", null)]
    [Arguments("password-hash", "bcrypt", null)]
    [Arguments("digest", "SHA256", "hex")]
    [Arguments("digest", "SHA384", "base64")]
    [Arguments("digest", "SHA512", "base64url")]
    [Arguments("private-key", "RS256", "pkcs8-pem")]
    [Arguments("private-key", "RS256", "jwk")]
    [Arguments("private-key", "PS256", "jwk")]
    [Arguments("private-key", "ES256", "pkcs8-pem")]
    [Arguments("private-key", "ES256", "jwk")]
    [Arguments("private-key", "ES384", "jwk")]
    [Arguments("private-key", "ES512", "jwk")]
    public async Task SupportedProfilesRunThroughDesktopGenerationAndRemainMasked(string kind, string? algorithm, string? encoding)
    {
        var specification = new JsonObject { ["kind"] = kind, ["acquisition"] = "local", ["generation"] = new JsonObject() };
        if (kind is "password-hash" or "digest") specification["generation"]!["input"] = "entered";
        if (kind == "symmetric-key")
            specification["generation"]!["bytes"] = algorithm switch
            {
                "HS384" => 48,
                "HS512" => 64,
                "AES128" => 16,
                "AES192" => 24,
                _ => 32
            };
        if (algorithm is not null) specification["algorithm"] = algorithm;
        if (encoding is not null) specification["encoding"] = encoding;
        using var form = CreateForm(specification: specification);
        using var factory = new FormFieldFactory();
        var field = (StringFieldViewModel)factory.Create(form).Single();
        if (kind is "password-hash" or "digest") field.SecretInput = "synthetic-derivation-input";
        await field.GenerateCommand.ExecuteAsync(null);
        await Assert.That(field.OperationStatus).IsEqualTo("secret-generated");
        await Assert.That(field.Text.Length > 0).IsTrue();
        await Assert.That(field.PasswordChar).IsEqualTo('●');
        await Assert.That(field.SecretInput).IsEmpty();
        await Assert.That(form.AnswerOrigin(["password"])).IsEqualTo(DefinitionAnswerOrigin.Generated);
        await Assert.That(form.Validate().IsReady).IsTrue();
    }

    [Test]
    public async Task TypedSecretWithoutSensitivityIsMaskedAndInvalidInputStaysCorrectable()
    {
        using var form = CreateForm();
        using var factory = new FormFieldFactory();
        var field = (StringFieldViewModel)factory.Create(form).Single();
        field.Text = "short";
        await Assert.That(field.PasswordChar).IsEqualTo('●');
        await Assert.That(field.Text).IsEqualTo("short");
        await Assert.That(field.HasError).IsTrue();
        field.RequestFocus();
        await Assert.That(field.FocusRequestVersion > 0).IsTrue();
    }

    [Test]
    public async Task ConnectionStringClassificationMasksWithoutInventingGeneration()
    {
        using var form = CreateForm(specification: new JsonObject { ["kind"] = "connection-string", ["acquisition"] = "existing" });
        using var factory = new FormFieldFactory();
        var field = (StringFieldViewModel)factory.Create(form).Single();
        await Assert.That(field.PasswordChar).IsEqualTo('●');
        await Assert.That(field.GenerateCommand.CanExecute(null)).IsFalse();
    }

    [Test]
    public async Task GenerateReplacesOnlyOnExplicitActionAndEditsHideTheValue()
    {
        using var form = CreateForm();
        using var factory = new FormFieldFactory();
        var field = (StringFieldViewModel)factory.Create(form).Single();
        field.Text = "short";
        await field.GenerateCommand.ExecuteAsync(null);
        await Assert.That(field.Text.Length).IsEqualTo(24);
        await Assert.That(form.Validate().IsReady).IsTrue();
        await Assert.That(field.PasswordChar).IsEqualTo('●');
        field.RevealCommand.Execute(null);
        await Assert.That(field.PasswordChar).IsEqualTo('\0');
        field.SecretInput = "synthetic-transient-input";
        field.Text = "edited";
        await Assert.That(field.PasswordChar).IsEqualTo('●');
        await Assert.That(field.SecretInput).IsEmpty();
        await Assert.That(field.HasError).IsTrue();
        field.Dispose();
        await Assert.That(field.RevealCommand.CanExecute(null)).IsFalse();
        await Assert.That(field.CopyCommand.CanExecute(null)).IsFalse();
        await Assert.That(field.GenerateCommand.CanExecute(null)).IsFalse();
    }

    [Test]
    public async Task ClipboardFailurePreservesAnswerAndReportsCapabilityFailure()
    {
        using var form = CreateForm();
        using var factory = new FormFieldFactory(new UnavailableClipboard());
        var field = (StringFieldViewModel)factory.Create(form).Single();
        field.Text = "synthetic-only-password";
        await field.CopyCommand.ExecuteAsync(null);
        await Assert.That(field.OperationStatus).IsEqualTo("clipboard-unavailable");
        await Assert.That(field.Text).IsEqualTo("synthetic-only-password");
        await Assert.That(field.PasswordChar).IsEqualTo('●');
    }

    [Test]
    public async Task ValidationGuidanceChangesWithoutEchoingPrivateInput()
    {
        using var form = CreateForm();
        using var factory = new FormFieldFactory();
        var field = (StringFieldViewModel)factory.Create(form).Single();
        await Assert.That(field.ErrorText).IsEmpty();
        field.Text = "short";
        await Assert.That(field.ErrorText.Length > 0).IsTrue();
        await Assert.That(field.ErrorText.Contains("short", StringComparison.Ordinal)).IsFalse();
        field.Text = "synthetic-only-password";
        await Assert.That(field.ErrorText).IsEmpty();
    }

    [Test]
    public async Task OriginalPasswordResultBelongsToItsEditorAndAnyEditRetiresIt()
    {
        using var form = CreateForm(generatedInput: true);
        using var factory = new FormFieldFactory();
        var fields = factory.Create(form).Cast<StringFieldViewModel>().ToArray();
        await fields[0].GenerateCommand.ExecuteAsync(null);
        await Assert.That(fields[0].HasGeneratedPassword).IsTrue();
        await Assert.That(fields[1].HasGeneratedPassword).IsFalse();
        await Assert.That(fields[1].GeneratedPassword).IsEmpty();
        await fields[1].GenerateCommand.ExecuteAsync(null);
        await Assert.That(fields[0].HasGeneratedPassword).IsFalse();
        await Assert.That(fields[0].GeneratedPassword).IsEmpty();
        await Assert.That(fields[1].HasGeneratedPassword).IsTrue();
        fields[0].Text = "synthetic-edit";
        await Assert.That(fields[1].GeneratedPassword).IsEmpty();
    }

    private sealed class UnavailableClipboard : ISecretClipboardService
    {
        public Task<bool> CopyAsync(string value) => Task.FromResult(false);
    }

    internal static DefinitionFormModel CreateForm(bool generatedInput = false, JsonObject? specification = null)
    {
        var source = JsonNode.Parse(DesktopApplicationTests.Definition())!.AsObject();
        source.Remove("presentation");
        source["configuration"] = JsonNode.Parse("""
            {"type":"object","additionalProperties":false,"required":["password"],
             "x-configurator":{"sensitivity":"public"},"properties":{
               "password":{"type":"string","title":"Password","minLength":16,"maxLength":64,
                 "x-configurator":{"detail":"basic","secret":{"kind":"password","acquisition":"local","generation":{"length":24}}}}}}
            """);
        if (generatedInput)
            source["configuration"] = JsonNode.Parse("""
                {"type":"object","additionalProperties":false,"x-configurator":{"sensitivity":"public"},"properties":{
                  "first":{"type":"string","title":"First digest","x-configurator":{"detail":"basic",
                    "secret":{"kind":"digest","acquisition":"local","algorithm":"SHA256","encoding":"hex",
                      "generation":{"input":"generated","password":{"kind":"password","acquisition":"local","generation":{"length":24}}}}}},
                  "second":{"type":"string","title":"Second digest","x-configurator":{"detail":"basic",
                    "secret":{"kind":"digest","acquisition":"local","algorithm":"SHA256","encoding":"hex",
                      "generation":{"input":"generated","password":{"kind":"password","acquisition":"local","generation":{"length":24}}}}}}}}
                """);
        if (specification is not null)
        {
            JsonObject field = source["configuration"]!["properties"]!["password"]!.AsObject();
            field.Remove("minLength");
            field.Remove("maxLength");
            field["x-configurator"]!["secret"] = specification;
        }
        source["requiredCapabilities"] = JsonNode.Parse(generatedInput || specification is not null ? """
            ["projection.literal","schema.object","schema.secret","schema.string"]
            """ : """
            ["projection.literal","schema.bounds","schema.object","schema.secret","schema.string"]
            """);
        source["artifacts"] = JsonNode.Parse("""
            [{"id":"config","format":"json","fileName":"config.json","targetUpdateMode":"create","restartRequirement":"process",
              "projection":{"op":"literal","value":{}}}]
            """);
        return new DefinitionFormModel(DefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString())));
    }
}
