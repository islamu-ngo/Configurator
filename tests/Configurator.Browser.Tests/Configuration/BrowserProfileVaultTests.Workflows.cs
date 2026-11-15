namespace Configurator.Browser.Tests;

using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bunit;
using Configurator.Browser.Profiles;
using Configurator.Browser.Worker.Profiles;
using Configurator.Core.Definitions;
using Configurator.Core.Profiles;
using Configurator.Presentation;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

public sealed partial class BrowserProfileVaultTests
{
    private const string RetainedSentinel = "synthetic-browser-retained-only";
    private const string PortablePassphrase = "synthetic-portable-passphrase";

    [Test]
    [Arguments(ProfileProtectionMode.ImportUpgradeOnly)]
    [Arguments(ProfileProtectionMode.InspectableEncrypted)]
    public async Task OpeningThroughTheUiKeepsRetainedValuesOutOfFormAndOrdinaryReplies(ProfileProtectionMode mode)
    {
        await using var context = BrowserFixture.Context();
        using var runtime = new ProfileRuntime();
        context.Services.AddSingleton<IJSRuntime>(runtime);
        using var form = new DefinitionFormModel(Definition(), DefinitionSessionPrivacy.BrowserSecretsEnabled);
        var panel = RenderProfile(context, form);
        await OpenThroughUiAsync(panel, Container(form.Definition, mode));

        await Assert.That(form.BrowserProfile!.Mode).IsEqualTo(mode);
        await Assert.That(form.Snapshot().Values.ContainsKey("token")).IsFalse();
        await Assert.That(panel.Markup.Contains(RetainedSentinel, StringComparison.Ordinal)).IsFalse();
        await Assert.That(runtime.Modules.SelectMany(module => module.SafeReplies)
            .Any(reply => reply.Contains(RetainedSentinel, StringComparison.Ordinal))).IsFalse();
        await panel.Find("[data-action='profile-download-private']").ClickAsync(new MouseEventArgs());
        await Assert.That(runtime.Downloads.Count).IsEqualTo(1);
        await Assert.That(Encoding.UTF8.GetString(runtime.Downloads[0])).Contains(RetainedSentinel);
        await Assert.That(form.Snapshot().Values.ContainsKey("token")).IsFalse();
        await Assert.That(panel.FindAll("pre").Count).IsEqualTo(0);
    }

    [Test]
    public async Task InspectRequiresItsCheckboxThenRetiresWorkerAfterOrdinaryAdoption()
    {
        await using var context = BrowserFixture.Context();
        using var runtime = new ProfileRuntime();
        context.Services.AddSingleton<IJSRuntime>(runtime);
        using var form = new DefinitionFormModel(Definition(), DefinitionSessionPrivacy.BrowserSecretsEnabled);
        var panel = RenderProfile(context, form);
        await OpenThroughUiAsync(panel, Container(form.Definition, ProfileProtectionMode.InspectableEncrypted));
        await Assert.That(panel.Find("[data-action='profile-inspect']").HasAttribute("disabled")).IsTrue();

        await panel.Find("#profile-inspect-consent").ChangeAsync(new ChangeEventArgs { Value = true });
        await panel.Find("[data-action='profile-inspect']").ClickAsync(new MouseEventArgs());

        await Assert.That(form.Snapshot().Values["token"].GetString()).IsEqualTo(RetainedSentinel);
        await Assert.That(form.RequiresBrowserProfileComposition).IsFalse();
        await Assert.That(runtime.Modules.Single().Retired).IsTrue();
        await Assert.That(panel.FindAll("[data-action='profile-download-private']").Count).IsEqualTo(0);
    }

    [Test]
    public async Task UiReplacementWaitsForWorkerRetirementAndDownloadsOnlyTheReplacement()
    {
        await using var context = BrowserFixture.Context();
        using var runtime = new ProfileRuntime();
        context.Services.AddSingleton<IJSRuntime>(runtime);
        using var form = new DefinitionFormModel(Definition(), DefinitionSessionPrivacy.BrowserSecretsEnabled);
        var panel = RenderProfile(context, form);
        await OpenThroughUiAsync(panel, Container(form.Definition, ProfileProtectionMode.ImportUpgradeOnly));
        WorkerModule module = runtime.Modules.Single();
        module.Hold = "retire-field";
        await panel.Find("[data-profile-replace='/token']").ClickAsync(new MouseEventArgs());
        Task replacing = panel.Find("[data-action='profile-confirm-replace']").ClickAsync(new MouseEventArgs());
        await module.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(form.CanEdit(DefinitionPath.Parse("/token"))).IsFalse();
        module.Release.TrySetResult();
        await replacing.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(form.CanEdit(DefinitionPath.Parse("/token"))).IsTrue();
        await panel.InvokeAsync(() => form.SetValue(DefinitionPath.Parse("/token"),
            JsonSerializer.SerializeToElement("synthetic-new-token")));
        await panel.Find("[data-action='profile-download-private']").ClickAsync(new MouseEventArgs());

        await Assert.That(runtime.Downloads.Count).IsEqualTo(1);
        string artifact = Encoding.UTF8.GetString(runtime.Downloads[0]);
        await Assert.That(artifact).Contains("synthetic-new-token");
        await Assert.That(artifact.Contains(RetainedSentinel, StringComparison.Ordinal)).IsFalse();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task CancelOrRevocationDuringUnlockCannotResurrectProfile(bool revoke)
    {
        await using var context = BrowserFixture.Context();
        using var runtime = new ProfileRuntime { Hold = "unlock" };
        context.Services.AddSingleton<IJSRuntime>(runtime);
        using var form = new DefinitionFormModel(Definition(), DefinitionSessionPrivacy.BrowserSecretsEnabled);
        var panel = RenderProfile(context, form);
        await SelectVaultAsync(panel, Container(form.Definition, ProfileProtectionMode.ImportUpgradeOnly));
        Task opening = panel.Find("[data-action='profile-open']").ClickAsync(new MouseEventArgs());
        WorkerModule module = runtime.Modules.Single();
        await module.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        if (revoke) await panel.InvokeAsync(() => form.SetSecretEntryEnabled(false));
        else await panel.Find("[data-action='profile-cancel']").ClickAsync(new MouseEventArgs());
        await Assert.That(module.Retired).IsTrue();
        module.Release.TrySetResult();
        await opening.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.That(panel.Find("[data-profile-status]").GetAttribute("data-profile-status"))
            .IsEqualTo("ProfileCancelled");
        await Assert.That(form.BrowserProfile).IsNull();
        await Assert.That(form.Snapshot().Values.ContainsKey("token")).IsFalse();
        await Assert.That(panel.FindAll("[data-action='profile-download-private']").Count).IsEqualTo(0);
        await Assert.That(panel.Find("#profile-open-passphrase").GetAttribute("value") ?? "").IsEqualTo("");
    }

    [Test]
    public async Task SaveAndExactCopyRecoveryUseSeparateWorkerCustodyWithoutAdoptingAnswers()
    {
        await using var context = BrowserFixture.Context();
        using var runtime = new ProfileRuntime();
        context.Services.AddSingleton<IJSRuntime>(runtime);
        using var form = new DefinitionFormModel(Definition(), DefinitionSessionPrivacy.BrowserSecretsEnabled);
        form.SetValue(DefinitionPath.Parse("/endpoint"), JsonSerializer.SerializeToElement("current-endpoint"));
        form.SetValue(DefinitionPath.Parse("/token"), JsonSerializer.SerializeToElement("synthetic-current-token"));
        var panel = RenderProfile(context, form);
        await panel.Find("#profile-new-passphrase").InputAsync(new ChangeEventArgs { Value = PortablePassphrase });
        await panel.Find("#profile-confirm-passphrase").InputAsync(new ChangeEventArgs { Value = PortablePassphrase });
        await panel.Find("[data-action='profile-save']").ClickAsync(new MouseEventArgs());
        await Assert.That(panel.Find("[data-profile-status]").GetAttribute("data-profile-status")).IsEqualTo("ProfileSavedUnverified");
        long revision = form.InputRevision;
        var input = panel.FindComponents<InputFile>().Single(component =>
            component.Instance.AdditionalAttributes?["id"]?.ToString() == "profile-recovery-file");
        await panel.InvokeAsync(() => input.UploadFiles(InputFileContent.CreateFromBinary(runtime.Downloads.Single(), "synthetic-saved.cfgvault")));
        await panel.Find("#profile-recovery-passphrase").InputAsync(new ChangeEventArgs { Value = PortablePassphrase });
        await panel.Find("[data-action='profile-recover']").ClickAsync(new MouseEventArgs());

        await Assert.That(panel.Find("[data-profile-status]").GetAttribute("data-profile-status")).IsEqualTo("ProfileRecoveryVerified");
        await Assert.That(runtime.Modules.Count).IsEqualTo(2);
        await Assert.That(runtime.Modules[0].Retired).IsFalse();
        await Assert.That(runtime.Modules[1].Retired).IsTrue();
        await Assert.That(form.InputRevision).IsEqualTo(revision);
        await Assert.That(form.BrowserProfile).IsNull();
    }

    [Test]
    [Arguments(ProfileProtectionMode.ImportUpgradeOnly)]
    [Arguments(ProfileProtectionMode.InspectableEncrypted)]
    public async Task ProfileVaultSeparatesTaskBoundariesAndModeSpecificDisclosures(ProfileProtectionMode mode)
    {
        await using var context = BrowserFixture.Context();
        using var runtime = new ProfileRuntime();
        context.Services.AddSingleton<IJSRuntime>(runtime);
        using var form = new DefinitionFormModel(Definition(), DefinitionSessionPrivacy.BrowserSecretsEnabled);
        var panel = RenderProfile(context, form);

        await Assert.That(panel.FindAll("[data-profile-task='compare']").Count).IsEqualTo(1);
        await Assert.That(panel.FindAll("[data-profile-task='open']").Count).IsEqualTo(1);
        await Assert.That(panel.FindAll("[data-profile-task='save']").Count).IsEqualTo(1);
        await Assert.That(panel.Find("[data-profile-recovery-state]").GetAttribute("data-profile-recovery-state")).IsEqualTo("unverified");

        await OpenThroughUiAsync(panel, Container(form.Definition, mode));
        var activeTask = panel.Find("[data-profile-task='active-profile']");
        string expectedModeToken = mode == ProfileProtectionMode.InspectableEncrypted ? "inspectable-encrypted" : "upgrade-only";
        await Assert.That(activeTask.QuerySelector("[data-profile-mode-disclosure]")?.GetAttribute("data-profile-mode-disclosure")).IsEqualTo(expectedModeToken);

        await panel.Find("#profile-new-passphrase").InputAsync(new ChangeEventArgs { Value = PortablePassphrase });
        await panel.Find("#profile-confirm-passphrase").InputAsync(new ChangeEventArgs { Value = PortablePassphrase });
        await panel.Find("[data-action='profile-save']").ClickAsync(new MouseEventArgs());
        await Assert.That(panel.FindAll("[data-profile-task='verify-recovery']").Count).IsEqualTo(1);
        await Assert.That(panel.Find("[data-profile-recovery-state]").GetAttribute("data-profile-recovery-state")).IsEqualTo("unverified");

        var input = panel.FindComponents<InputFile>().Single(component =>
            component.Instance.AdditionalAttributes?["id"]?.ToString() == "profile-recovery-file");
        await panel.InvokeAsync(() => input.UploadFiles(InputFileContent.CreateFromBinary(runtime.Downloads.Single(), "synthetic-saved.cfgvault")));
        await panel.Find("#profile-recovery-passphrase").InputAsync(new ChangeEventArgs { Value = PortablePassphrase });
        await panel.Find("[data-action='profile-recover']").ClickAsync(new MouseEventArgs());
        await Assert.That(panel.Find("[data-profile-recovery-state]").GetAttribute("data-profile-recovery-state")).IsEqualTo("verified");
    }

    private static IRenderedComponent<ConfigurationVaultPanel> RenderProfile(BunitContext context, DefinitionFormModel form)
    {
        Guid workspace = Guid.NewGuid();
        ProfileWorkerAuthority Authority() => new(workspace, 1, form.InputRevision, 1,
            form.SessionPrivacy == DefinitionSessionPrivacy.BrowserSecretsEnabled);
        return context.Render<ConfigurationVaultPanel>(parameters => parameters.Add(item => item.Form, form)
            .Add(item => item.Ready, true).Add(item => item.CaptureAuthority, Authority));
    }

    private static async Task SelectVaultAsync(IRenderedComponent<ConfigurationVaultPanel> panel, byte[] container)
    {
        await panel.Find("#profile-open-passphrase").InputAsync(new ChangeEventArgs { Value = PortablePassphrase });
        var input = panel.FindComponents<InputFile>().Single(component =>
            component.Instance.AdditionalAttributes?["id"]?.ToString() == "profile-vault-file");
        await panel.InvokeAsync(() => input.UploadFiles(InputFileContent.CreateFromBinary(container, "synthetic.cfgvault")));
    }

    private static async Task OpenThroughUiAsync(IRenderedComponent<ConfigurationVaultPanel> panel, byte[] container)
    {
        await SelectVaultAsync(panel, container);
        await panel.Find("[data-action='profile-open']").ClickAsync(new MouseEventArgs());
    }

    private static byte[] Container(Definition definition, ProfileProtectionMode mode)
    {
        var answers = new AnswerSnapshot(0, new Dictionary<string, JsonElement>
        {
            ["token"] = JsonSerializer.SerializeToElement(RetainedSentinel),
            ["endpoint"] = JsonSerializer.SerializeToElement("old-endpoint")
        });
        byte[] payload = ProfilePayloadCodec.Serialize(definition, answers, mode, Guid.NewGuid(), Guid.NewGuid(),
            "synthetic-source", "synthetic-context");
        try { return SyntheticCodec.Wrap(payload); }
        finally { CryptographicOperations.ZeroMemory(payload); }
    }

    // A synthetic container codec is the external cryptographic boundary. These tests exercise
    // the real worker, transport and UI custody; they do not claim authentication or KDF evidence.
    private sealed class SyntheticCodec : IProfileContainerCodec
    {
        public Guid? DeviceRecipientId => null;
        public ValueTask<byte[]> SealAsync(ReadOnlyMemory<byte> payload,
            IReadOnlyList<ReadOnlyMemory<byte>> recipientMaterials, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(Wrap(payload.Span));
        }
        public ValueTask<UnlockedProfile> OpenAsync(ReadOnlyMemory<byte> container, int recipientIndex,
            ReadOnlyMemory<byte> passphraseUtf8, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using JsonDocument document = JsonDocument.Parse(container);
            byte[] payload = Base64Url.DecodeFromChars(document.RootElement.GetProperty("ciphertext").GetString()!);
            try { return ValueTask.FromResult(ProfilePayloadCodec.Admit(payload)); }
            finally { CryptographicOperations.ZeroMemory(payload); }
        }
        internal static byte[] Wrap(ReadOnlySpan<byte> payload) => JsonSerializer.SerializeToUtf8Bytes(new
        {
            @protected = Base64Url.EncodeToString("""{"enc":"A256GCM","typ":"cfgvault"}"""u8),
            iv = "AAAAAAAAAAAAAAAA", ciphertext = Base64Url.EncodeToString(payload), tag = "AAAAAAAAAAAAAAAAAAAAAA",
            header = new { alg = "PBES2-HS512+A256KW", p2c = 600000, p2s = "AAAAAAAAAAAAAAAA" },
            encrypted_key = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"
        });
    }

    private sealed class ProfileRuntime : IJSInProcessRuntime, IDisposable
    {
        private WorkerFactory? _module;
        internal string? Hold { get; init; }
        internal List<WorkerModule> Modules { get; } = [];
        internal List<byte[]> Downloads { get; } = [];
        public TValue Invoke<TValue>(string identifier, params object?[]? args) => throw new NotSupportedException();
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            if (identifier is "Blazor._internal.InputFile.init" or "Blazor._internal.domWrapper.focus")
                return ValueTask.FromResult(default(TValue)!);
            if (identifier != "import" || args?[0] is not "./js/profile-worker-host.js") throw new NotSupportedException();
            _module ??= new WorkerFactory(this);
            return ValueTask.FromResult((TValue)(object)_module);
        }
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
            InvokeAsync<TValue>(identifier, args);
        public void Dispose()
        {
            foreach (WorkerModule module in Modules) module.Dispose();
            foreach (byte[] bytes in Downloads) CryptographicOperations.ZeroMemory(bytes);
        }
    }

    private sealed class WorkerFactory(ProfileRuntime runtime) : IJSInProcessObjectReference
    {
        public TValue Invoke<TValue>(string identifier, params object?[]? args)
        {
            if (identifier != "createBridge" || args is { Length: > 0 }) throw new NotSupportedException();
            var bridge = new WorkerModule(runtime.Downloads) { Hold = runtime.Hold };
            runtime.Modules.Add(bridge);
            return (TValue)(object)bridge;
        }
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            throw new NotSupportedException();
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
            throw new NotSupportedException();
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class WorkerModule(List<byte[]> downloads) : IJSInProcessObjectReference
    {
        private ProfileWorkerSession? _session;
        private byte[] _output = [];
        internal string? Hold { get; set; }
        internal bool Retired { get; private set; }
        internal List<string> SafeReplies { get; } = [];
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TValue Invoke<TValue>(string identifier, params object?[]? args)
        {
            if (identifier == "takeDisclosure")
            {
                byte[] disclosure = _output;
                _output = [];
                return (TValue)(object)disclosure;
            }
            if (identifier == "download") downloads.Add(_output.ToArray());
            if (identifier is "retire" or "discard" or "download")
            {
                CryptographicOperations.ZeroMemory(_output);
                _output = [];
            }
            if (identifier == "retire")
            {
                Retired = true;
                _session?.Dispose();
                _session = null;
            }
            return default!;
        }
        public async ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            if (identifier == "open")
            {
                _session ??= new ProfileWorkerSession(new SyntheticCodec());
                return default!;
            }
            if (identifier != "execute") throw new NotSupportedException();
            ProfileWorkerSession session = _session!;
            ProfileWorkerRequest request = JsonSerializer.Deserialize((string)args![1]!,
                ProfileWorkerJsonContext.Default.ProfileWorkerRequest)!;
            using var input = new ProfileWorkerInput((byte[])args[2]!, (byte[])args[3]!,
                (byte[])args[4]!, (byte[])args[5]!,
                ((byte[][])args[6]!).Select(bytes => (ReadOnlyMemory<byte>)bytes).ToArray());
            if (request.Operation == Hold)
            {
                Entered.TrySetResult();
                await Release.Task;
            }
            try
            {
                using ProfileWorkerResult result = await session.ExecuteAsync(request, input);
                _output = result.Output.ToArray();
                string response = JsonSerializer.Serialize(result.Reply, ProfileWorkerJsonContext.Default.ProfileWorkerReply);
                SafeReplies.Add(response);
                return (TValue)(object)response;
            }
            catch (ObjectDisposedException) { throw new JSException("profile-retired"); }
        }
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
            InvokeAsync<TValue>(identifier, args);
        public void Dispose()
        {
            _session?.Dispose();
            _session = null;
            CryptographicOperations.ZeroMemory(_output);
            _output = [];
        }
        public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
    }
}
