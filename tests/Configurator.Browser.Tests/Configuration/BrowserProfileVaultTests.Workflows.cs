namespace Configurator.Browser.Tests;

using System.Buffers.Text;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bunit;
using Configurator.Browser.Profiles;
using Configurator.Browser.Services;
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
        await using BrowserProfileVaultController controller = CreateController(context, form);
        var panel = RenderProfile(context, controller);
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
        await using BrowserProfileVaultController controller = CreateController(context, form);
        var panel = RenderProfile(context, controller);
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
        await using BrowserProfileVaultController controller = CreateController(context, form);
        var panel = RenderProfile(context, controller);
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
        await using BrowserProfileVaultController controller = CreateController(context, form);
        var panel = RenderProfile(context, controller);
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
        await using BrowserProfileVaultController controller = CreateController(context, form);
        var panel = RenderProfile(context, controller);
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
        await using BrowserProfileVaultController controller = CreateController(context, form);
        var panel = RenderProfile(context, controller);

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

    [Test]
    public async Task ProfileControllerPreservesWorkerAcrossPanelRemountAndRetiresOnUnmountedRevocation()
    {
        await using var context = BrowserFixture.Context();
        using var runtime = new ProfileRuntime();
        context.Services.AddSingleton<IJSRuntime>(runtime);
        using var form = new DefinitionFormModel(Definition(), DefinitionSessionPrivacy.BrowserSecretsEnabled);
        await using BrowserProfileVaultController controller = CreateController(context, form);

        IRenderedComponent<ConfigurationVaultPanel> firstPanel = context.Render<ConfigurationVaultPanel>(
            parameters => parameters.Add(item => item.Controller, controller));
        await OpenThroughUiAsync(firstPanel, Container(form.Definition, ProfileProtectionMode.ImportUpgradeOnly));
        await Assert.That(form.BrowserProfile).IsNotNull();
        await Assert.That(controller.Fields.Count > 0).IsTrue();
        await Assert.That(firstPanel.FindAll("details[data-profile-panel]")).IsEmpty();

        // Type unsubmitted draft passphrase and reveal it before unmounting the panel.
        await firstPanel.Find("#profile-new-passphrase").InputAsync(new ChangeEventArgs { Value = PortablePassphrase });
        await firstPanel.Find("[data-passphrase-toggle='profile-new-passphrase']").ClickAsync(new MouseEventArgs());
        await Assert.That(firstPanel.Find("#profile-new-passphrase").GetAttribute("type")).IsEqualTo("text");

        // Unmount the panel (simulating switching End-Dock utilities or overlay transitions).
        firstPanel.Dispose();

        // Worker custody and comparison fields must survive panel unmount.
        WorkerModule worker = runtime.Modules.Single();
        await Assert.That(worker.Retired).IsFalse();
        await Assert.That(form.BrowserProfile).IsNotNull();
        await Assert.That(controller.Fields.Count > 0).IsTrue();

        // Remounting against the same controller preserves worker custody while local passphrases stay wiped.
        IRenderedComponent<ConfigurationVaultPanel> secondPanel = context.Render<ConfigurationVaultPanel>(
            parameters => parameters.Add(item => item.Controller, controller));
        await Assert.That(secondPanel.FindAll("[data-profile-task='active-profile']")).Count().IsEqualTo(1);
        await Assert.That(secondPanel.Find("#profile-new-passphrase").GetAttribute("value") ?? "").IsEqualTo("");
        await Assert.That(secondPanel.Find("#profile-new-passphrase").GetAttribute("type")).IsEqualTo("password");
        await Assert.That(secondPanel.Find("[data-passphrase-toggle='profile-new-passphrase']").GetAttribute("aria-pressed")).IsEqualTo("false");

        // Save snapshot so RecoveryReference exists, then unmount and revoke consent while unmounted.
        await secondPanel.Find("#profile-new-passphrase").InputAsync(new ChangeEventArgs { Value = PortablePassphrase });
        await secondPanel.Find("#profile-confirm-passphrase").InputAsync(new ChangeEventArgs { Value = PortablePassphrase });
        await secondPanel.Find("[data-action='profile-save']").ClickAsync(new MouseEventArgs());
        await Assert.That(controller.RecoveryReference).IsNotNull();

        secondPanel.Dispose();

        // Revoking consent while panel is unmounted must synchronously retire worker and clear RecoveryReference.
        form.SetSecretEntryEnabled(false);

        await Assert.That(worker.Retired).IsTrue();
        await Assert.That(form.BrowserProfile).IsNull();
        await Assert.That(controller.RecoveryReference).IsNull();
        await Assert.That(controller.Fields).IsEmpty();
    }

    [Test]
    public async Task ProfileControllerTracksIsBusyWipesPassphraseBeforeAwaitAndRetiresOnUnmountedEdit()
    {
        await using var context = BrowserFixture.Context();
        using var runtime = new ProfileRuntime { Hold = "unlock" };
        context.Services.AddSingleton<IJSRuntime>(runtime);
        using var form = new DefinitionFormModel(Definition(), DefinitionSessionPrivacy.BrowserSecretsEnabled);
        await using BrowserProfileVaultController controller = CreateController(context, form);

        IRenderedComponent<ConfigurationVaultPanel> panel = context.Render<ConfigurationVaultPanel>(
            parameters => parameters.Add(item => item.Controller, controller));
        await SelectVaultAsync(panel, Container(form.Definition, ProfileProtectionMode.ImportUpgradeOnly));
        await panel.Find("[data-passphrase-toggle='profile-open-passphrase']").ClickAsync(new MouseEventArgs());
        await Assert.That(panel.Find("#profile-open-passphrase").GetAttribute("type")).IsEqualTo("text");

        Task opening = panel.Find("[data-action='profile-open']").ClickAsync(new MouseEventArgs());
        WorkerModule module = runtime.Modules.Single();
        await module.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // While KDF/unlock is still in-flight: IsBusy is true AND view-local passphrase + visibility were already wiped before await!
        await Assert.That(controller.IsBusy).IsTrue();
        await Assert.That(panel.Find("#profile-open-passphrase").GetAttribute("value") ?? "").IsEqualTo("");
        await Assert.That(panel.Find("#profile-open-passphrase").GetAttribute("type")).IsEqualTo("password");

        // Unmount the panel while work is still in-flight, then mutate an answer -> controller retires worker synchronously.
        panel.Dispose();
        form.SetValue(DefinitionPath.Parse("/endpoint"), JsonSerializer.SerializeToElement("edited-while-unmounted"));

        await Assert.That(module.Retired).IsTrue();
        module.Release.TrySetResult();
        await opening.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(controller.IsBusy).IsFalse();
        await Assert.That(controller.Status).IsEqualTo("ProfileCancelled");
        await Assert.That(form.BrowserProfile).IsNull();
    }

    [Test]
    public async Task ProfileControllerEnforcesZeroStringPassphraseBoundaryAndPanelIsSyncDisposableOnly()
    {
        Type controllerType = typeof(BrowserProfileVaultController);
        const BindingFlags AllMembers = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        // Compile-time boundary: zero string fields or properties for passphrases/credentials on controller.
        foreach (FieldInfo field in controllerType.GetFields(AllMembers))
        {
            if (field.Name.Contains("passphrase", StringComparison.OrdinalIgnoreCase)
                || field.Name.Contains("credential", StringComparison.OrdinalIgnoreCase)
                || field.Name.Contains("confirmation", StringComparison.OrdinalIgnoreCase))
            {
                await Assert.That(field.FieldType).IsNotEqualTo(typeof(string));
            }
        }

        // OpenAsync, SaveAsync, and RecoverAsync must accept ONLY ReadOnlyMemory<byte> / collections of ReadOnlyMemory<byte>, never string.
        foreach (string methodName in new[] { nameof(BrowserProfileVaultController.OpenAsync), nameof(BrowserProfileVaultController.SaveAsync), nameof(BrowserProfileVaultController.RecoverAsync) })
        {
            MethodInfo method = controllerType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance)!;
            ParameterInfo[] parameters = method.GetParameters();
            await Assert.That(parameters.Any(parameter => parameter.ParameterType == typeof(string))).IsFalse();
            await Assert.That(parameters.Any(parameter =>
                parameter.ParameterType == typeof(ReadOnlyMemory<byte>)
                || parameter.ParameterType == typeof(IReadOnlyList<ReadOnlyMemory<byte>>))).IsTrue();
        }

        // ConfigurationVaultPanel implements IDisposable ONLY (never IAsyncDisposable).
        await Assert.That(typeof(IDisposable).IsAssignableFrom(typeof(ConfigurationVaultPanel))).IsTrue();
        await Assert.That(typeof(IAsyncDisposable).IsAssignableFrom(typeof(ConfigurationVaultPanel))).IsFalse();
    }

    [Test]
    public async Task ProfileControllerDisposesCleanlyBeforeFormDisposalInConfigurationEditor()
    {
        await using var context = BrowserFixture.Context();
        using var runtime = new ProfileRuntime(context.JSInterop.JSRuntime);
        context.Services.AddSingleton<IJSRuntime>(runtime);
        IRenderedComponent<ConfigurationEditor> editor = context.Render<ConfigurationEditor>(
            parameters => parameters.Add(item => item.Definition, Definition()));
        await BrowserFixture.SetSecretConsentAsync(editor, true);
        await BrowserFixture.OpenWorkspaceUtilityAsync(editor, "profiles");
        IRenderedComponent<ConfigurationVaultPanel> panel = editor.FindComponent<ConfigurationVaultPanel>();
        DefinitionFormModel form = panel.Instance.Controller.Form;
        await OpenThroughUiAsync(panel, Container(form.Definition, ProfileProtectionMode.ImportUpgradeOnly));
        WorkerModule worker = runtime.Modules.Single();
        await Assert.That(worker.Retired).IsFalse();

        // DiscardAsync must retire controller BEFORE form.Dispose() with zero ObjectDisposedException.
        await editor.InvokeAsync(() => editor.Instance.DiscardAsync());
        await Assert.That(worker.Retired).IsTrue();
    }

    private static BrowserProfileVaultController CreateController(BunitContext context, DefinitionFormModel form)
    {
        Guid workspace = Guid.NewGuid();
        ProfileWorkerAuthority Authority() => new(workspace, 1, form.InputRevision, 1,
            form.SessionPrivacy == DefinitionSessionPrivacy.BrowserSecretsEnabled);
        return new BrowserProfileVaultController(
            form,
            context.Services.GetRequiredService<IJSRuntime>(),
            context.Services.GetRequiredService<BrowserDefinitionRegistry>(),
            Authority);
    }

    private static IRenderedComponent<ConfigurationVaultPanel> RenderProfile(
        BunitContext context,
        BrowserProfileVaultController controller) =>
        context.Render<ConfigurationVaultPanel>(parameters => parameters.Add(item => item.Controller, controller));

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

    private sealed class ProfileRuntime(IJSRuntime? fallback = null) : IJSInProcessRuntime, IDisposable
    {
        private WorkerFactory? _module;
        internal string? Hold { get; init; }
        internal List<WorkerModule> Modules { get; } = [];
        internal List<byte[]> Downloads { get; } = [];
        public TValue Invoke<TValue>(string identifier, params object?[]? args) => throw new NotSupportedException();
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            InvokeAsync<TValue>(identifier, CancellationToken.None, args);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            if (identifier is "Blazor._internal.InputFile.init" or "Blazor._internal.domWrapper.focus")
                return ValueTask.FromResult(default(TValue)!);
            if (identifier == "import" && args?[0] is "./js/profile-worker-host.js")
            {
                _module ??= new WorkerFactory(this);
                return ValueTask.FromResult((TValue)(object)_module);
            }
            if (fallback is not null)
                return fallback.InvokeAsync<TValue>(identifier, cancellationToken, args);
            throw new NotSupportedException();
        }
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
