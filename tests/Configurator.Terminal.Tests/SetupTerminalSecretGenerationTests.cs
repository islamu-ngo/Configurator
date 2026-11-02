namespace Configurator.Terminal.Tests;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using CommunityToolkit.Mvvm.Messaging;
using Configurator.Artifacts;
using Configurator.Core.Definitions;
using Configurator.Presentation;
using Configurator.Terminal;
using global::Terminal.Gui.App;
using global::Terminal.Gui.Input;

[NotInParallel]
public sealed class SetupTerminalSecretGenerationTests
{
    [Test]
    public async Task RejectedReplacementPreservesExistingPrivateInput()
    {
        using var buffer = new SetupTerminalSecretBuffer(8, urlSafeOnly: false);
        using var field = new SetupSecretTextField(buffer);
        await Assert.That(field.TryReplaceSensitiveInput("synthetic")).IsFalse();
        await Assert.That(field.TryReplaceSensitiveInput("sample")).IsTrue();
        await Assert.That(field.TryReplaceSensitiveInput("oversized-value")).IsFalse();
        await Assert.That(Encoding.UTF8.GetString(buffer.CopyUtf8Bytes())).IsEqualTo("sample");
        await Assert.That(field.Text).IsEqualTo("●●●●●●");
    }

    [Test]
    public async Task BufferDiagnosticDoesNotExposeObservedLength()
    {
        using var buffer = new SetupTerminalSecretBuffer(64, urlSafeOnly: false);
        string empty = buffer.ToString();
        buffer.TryReplace("sample");
        await Assert.That(buffer.ToString()).IsEqualTo(empty);
    }

    [Test]
    public async Task ExplicitRevealDoesNotEnableOrdinaryCopyAndClearingRemasks()
    {
        using var buffer = new SetupTerminalSecretBuffer(64, urlSafeOnly: false);
        using var field = new SetupSecretTextField(buffer);
        field.TryReplaceSensitiveInput("sample");
        field.SetRevealed(true);
        await Assert.That(field.Text).IsEqualTo("sample");
        await Assert.That(field.InvokeCommand(Command.Copy)).IsTrue();
        await Assert.That(field.InvokeCommand(Command.Cut)).IsTrue();
        await Assert.That(buffer.Count).IsEqualTo(6);
        field.SetRevealed(false);
        await Assert.That(field.Text).IsEqualTo("●●●●●●");
        field.ClearSensitiveState();
        await Assert.That(field.IsRevealed).IsFalse();
        await Assert.That(field.Text).IsEmpty();
    }

    [Test]
    public async Task UnavailableClipboardPreservesInputWithoutInvokingTransfer()
    {
        using var buffer = new SetupTerminalSecretBuffer(64, urlSafeOnly: false);
        using var field = new SetupSecretTextField(buffer);
        field.TryReplaceSensitiveInput("sample");
        await Assert.That(field.TryCopy(new UnavailableClipboard())).IsFalse();
        await Assert.That(field.TryCopy(new UnavailableClipboard(supported: true))).IsFalse();
        await Assert.That(field.TryCopy(new global::Terminal.Gui.Drivers.FakeClipboard(false, false))).IsFalse();
        byte[] bytes = buffer.CopyUtf8Bytes();
        try { await Assert.That(Encoding.UTF8.GetString(bytes)).IsEqualTo("sample"); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
        await Assert.That(field.Text).IsEqualTo("●●●●●●");
    }

    [Test]
    public async Task ExplicitRevealNeverInterpretsControlSequences()
    {
        using var buffer = new SetupTerminalSecretBuffer(64, urlSafeOnly: false);
        using var field = new SetupSecretTextField(buffer);
        field.TryReplaceSensitiveInput("sample\u001b]52;c;payload\a");
        field.SetRevealed(true);
        await Assert.That(field.Text.Any(char.IsControl)).IsFalse();
        await Assert.That(buffer.CopyText()).IsEqualTo("sample\u001b]52;c;payload\a");
    }

    private sealed class UnavailableClipboard(bool supported = false) : IClipboard
    {
        public bool IsSupported => supported;
        public string GetClipboardData() => throw new NotSupportedException();
        public void SetClipboardData(string text) => throw new InvalidOperationException("Unexpected transfer");
        public bool TryGetClipboardData(out string result) { result = ""; return false; }
        public bool TrySetClipboardData(string text) => supported ? false : throw new InvalidOperationException("Unexpected transfer");
    }

    private sealed class RecordingClipboard : IClipboard
    {
        public bool IsSupported => true;
        public string Contents { get; private set; } = "";
        public string GetClipboardData() => Contents;
        public void SetClipboardData(string text) => Contents = text;
        public bool TryGetClipboardData(out string result) { result = Contents; return true; }
        public bool TrySetClipboardData(string text) { Contents = text; return true; }
    }

    [Test]
    public async Task GenerationUsesSharedPolicyAndRemainsMaskedAfterFieldNavigation()
    {
        using var harness = new Harness();
        Task generation = harness.Window.GenerateSecretAsync(["password"]);
        harness.Window.MoveField(1);
        await generation.WaitAsync(TimeSpan.FromSeconds(10));
        await Assert.That(harness.Form.Snapshot().Values["password"].GetString()!.Length).IsEqualTo(24);
        await Assert.That(harness.Form.Snapshot().Values.ContainsKey("second")).IsFalse();
        await Assert.That(harness.Window.PublicInput.Text).IsEmpty();
        await Assert.That(harness.Window.StatusText).DoesNotContain(harness.Form.Snapshot().Values["password"].GetString()!);
    }

    [Test]
    public async Task RevealChangesOnlyVisualStateAndClearingRetiresIt()
    {
        using var harness = new Harness();
        harness.Window.MaskedInput.TryReplaceSensitiveInput("synthetic-value");
        harness.Window.CommitField();
        long revision = harness.Form.InputRevision;
        harness.Window.ToggleSecretVisibility();
        await Assert.That(harness.Window.MaskedInput.IsRevealed).IsTrue();
        await Assert.That(harness.Form.InputRevision).IsEqualTo(revision);
        harness.Window.ClearPrivateState();
        await Assert.That(harness.Window.MaskedInput.IsRevealed).IsFalse();
        await Assert.That(harness.Window.MaskedInput.Text).IsEmpty();
    }

    [Test]
    public async Task HeadlessLinuxCopyReportsUnavailableAndPreservesTheAnswer()
    {
        if (!OperatingSystem.IsLinux()) return;
        string? display = Environment.GetEnvironmentVariable("DISPLAY");
        try
        {
            Environment.SetEnvironmentVariable("DISPLAY", null);
            using var harness = new Harness();
            harness.Window.MaskedInput.TryReplaceSensitiveInput("synthetic-value");
            harness.Window.CopySecret();
            await Assert.That(harness.Window.StatusText).IsEqualTo(SetupTerminalText.Get("SecretClipboardUnavailable"));
            await Assert.That(harness.Form.Snapshot().Values["password"].GetString()).IsEqualTo("synthetic-value");
            await Assert.That(harness.Window.MaskedInput.IsRevealed).IsFalse();
        }
        finally { Environment.SetEnvironmentVariable("DISPLAY", display); }
    }

    [Test]
    [Arguments("opaque-token", null, "base64url")]
    [Arguments("symmetric-key", "HS256", "base64url")]
    [Arguments("symmetric-key", "AES256", "hex")]
    [Arguments("password-hash", "argon2id", null)]
    [Arguments("password-hash", "pbkdf2-sha256", null)]
    [Arguments("password-hash", "aspnet-identity-v3", null)]
    [Arguments("password-hash", "bcrypt", null)]
    [Arguments("digest", "SHA256", "hex")]
    [Arguments("private-key", "ES256", "jwk")]
    [Arguments("private-key", "RS256", "pkcs8-pem")]
    public async Task SupportedSecretKindsUseSharedGenerationWithoutPublicValueDisclosure(string kind, string? algorithm, string? encoding)
    {
        var policy = new JsonObject { ["kind"] = kind, ["acquisition"] = "local", ["generation"] = new JsonObject() };
        if (kind is "password-hash" or "digest") policy["generation"]!["input"] = "entered";
        if (algorithm is not null) policy["algorithm"] = algorithm;
        if (encoding is not null) policy["encoding"] = encoding;
        using var harness = new Harness(policy);
        char[] input = kind is "password-hash" or "digest" ? "synthetic-input".ToCharArray() : [];
        await harness.Window.GenerateSecretAsync(["password"], input).WaitAsync(TimeSpan.FromSeconds(30));
        await Assert.That(harness.Form.AnswerOrigin(["password"])).IsEqualTo(DefinitionAnswerOrigin.Generated);
        await Assert.That(harness.Form.GetSecretValidation(["password"], showErrors: true).Failures).IsEmpty();
        await Assert.That(harness.Window.MaskedInput.IsRevealed).IsFalse();
        await Assert.That(harness.Window.MaskedInput.Text.All(character => character == '●')).IsTrue();
        await Assert.That(harness.Window.PublicInput.Text).IsEmpty();
        await Assert.That(input.All(character => character == '\0')).IsTrue();
        if (encoding == "pkcs8-pem")
        {
            string original = harness.Form.Snapshot().Values["password"].GetString()!;
            await Assert.That(original).Contains("\n");
            harness.Window.ToggleSecretVisibility();
            var clipboard = new RecordingClipboard();
            await Assert.That(harness.Window.MaskedInput.TryCopy(clipboard)).IsTrue();
            await Assert.That(clipboard.Contents).IsEqualTo(original);
            await Assert.That(harness.Form.Snapshot().Values["password"].GetString()).IsEqualTo(original);
        }
    }

    private sealed class Harness : IDisposable
    {
        private readonly IApplication _application = Application.Create();
        private readonly SetupPresentationSession _session = new(new StrongReferenceMessenger());
        private readonly SetupTerminalSecretBuffer _buffer = new(4096, urlSafeOnly: false);
        private readonly SetupTerminalArtifactOperation _operation;
        private readonly SetupPresentationWorkspace _workspace;
        internal Harness(JsonObject? policy = null)
        {
            const string source = """
                {"$schema":"urn:islamu:configurator:authoring:v0.0.1",
                 "identity":{"familyId":"9282a2b1-84a0-436d-8c4d-1c75b81d314c","revision":1},
                 "project":{"id":"example","name":"Example","publisher":"example"},"applicationVersions":["1"],"entryPoints":["setup"],
                 "configuration":{"type":"object","additionalProperties":false,
                   "x-configurator":{"sensitivity":"public"},"properties":{
                     "password":{"type":"string","minLength":16,"maxLength":32768,"x-configurator":{"detail":"basic",
                       "secret":{"kind":"password","acquisition":"local","generation":{"length":24}}}},
                     "second":{"type":"string","x-configurator":{"sensitivity":"public","detail":"basic"}}}},
                 "outputs":{"config.json":{"id":"config","format":"json","targetUpdateMode":"create","restartRequirement":"process",
                   "projection":{"op":"object","members":{}}}}}
                """;
            JsonObject definition = JsonNode.Parse(source)!.AsObject();
            if (policy is not null) definition["configuration"]!["properties"]!["password"]!["x-configurator"]!["secret"] = policy;
            Form = new(SetupDefinitionSourceCodec.ParseJson(Encoding.UTF8.GetBytes(definition.ToJsonString())));
            _operation = new(Form, new ProtectedArtifactWriter(), Path.GetTempPath());
            SetupWorkspaceId.TryCreate("definition", out var id);
            _workspace = _session.CreateWorkspace(id, _operation);
            _workspace.Activate();
            Window = new(_application, _workspace, _operation, _buffer, dispatch: callback => callback());
            Window.Frame = new System.Drawing.Rectangle(0, 0, 100, 30);
            Window.ApplyViewportPolicy(Window.Frame.Size);
        }
        internal DefinitionFormModel Form { get; }
        internal SetupTerminalWindow Window { get; }
        public void Dispose()
        {
            Window.Dispose();
            _workspace.Dispose();
            _operation.Dispose();
            _buffer.Dispose();
            Form.Dispose();
            _session.Dispose();
            _application.Dispose();
        }
    }
}
