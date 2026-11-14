namespace Configurator.Terminal.Tests;

using System.Runtime.Versioning;
using Configurator.Artifacts.Profiles;
using Configurator.Core.Profiles;
using Configurator.Terminal;
using global::Terminal.Gui.Input;
using global::Terminal.Gui.ViewBase;
using global::Terminal.Gui.Views;

[SupportedOSPlatform("linux")]
public sealed class TerminalProfileWorkflowTests
{
    private const string Passphrase = "synthetic portable profile passphrase";

    [Test]
    [NotInParallel]
    [Arguments("en")]
    [Arguments("ar")]
    public async Task KeyboardCommandsSaveUnlockCompareUpgradeAndCheckPortableRecovery(string locale)
    {
        using var directory = new PrivateDirectory();
        using var form = TerminalDefinitionFixture.Form("synthetic-held-secret");
        form.SelectLocale(locale);
        using var dialog = new TerminalProfileDialog(form, keyring: new MissingKeyring());
        dialog.StoreRoot.Text = directory.Path;
        SupplyPassphrase(dialog);
        await Accept(dialog, "ProfileSaveBlind", locale);
        await Assert.That(dialog.StatusText.Contains("Succeeded=True", StringComparison.Ordinal)).IsTrue();
        await Assert.That(dialog.RecoveryReference?.State).IsEqualTo(ProfileRecoveryState.ConfiguredUnverified);
        await Assert.That(dialog.Passphrase.Text).IsEmpty();
        await Accept(dialog, "ProfileList", locale);
        SupplyPassphrase(dialog);
        await Accept(dialog, "ProfileOpen", locale);
        await Assert.That(form.Profile?.Mode).IsEqualTo(ProfileProtectionMode.ImportUpgradeOnly);
        await Assert.That(form.Snapshot().Values.ContainsKey("token")).IsFalse();
        SupplyPassphrase(dialog);
        await Accept(dialog, "ProfileCompare", locale);
        await Assert.That(dialog.StatusText.Contains("SchemaAvailable=True", StringComparison.Ordinal)).IsTrue();
        SupplyPassphrase(dialog);
        await Accept(dialog, "ProfileCheckRecovery", locale);
        await Assert.That(dialog.StatusText.Contains("VerifiedForSnapshot", StringComparison.Ordinal)).IsTrue();
        SupplyPassphrase(dialog);
        await Accept(dialog, "ProfileUpgrade", locale);
        await Assert.That(dialog.StatusText.Contains("Succeeded=True", StringComparison.Ordinal)).IsTrue();
        var store = new LocalProfileVaultStore(directory.Path);
        var revisions = await store.ListAsync();
        await Assert.That(revisions.Count).IsEqualTo(2);
        var reference = dialog.RecoveryReference!;
        var upgraded = revisions.Single(item => item.SnapshotId == reference.SnapshotId);
        using var codec = new NativeProfileContainerCodec();
        using var opened = await codec.OpenStoredAsync(store, upgraded, 0,
            System.Text.Encoding.UTF8.GetBytes(Passphrase));
        await Assert.That(opened.Mode).IsEqualTo(ProfileProtectionMode.ImportUpgradeOnly);
        dialog.ArtifactId.Text = "config";
        dialog.TargetPath.Text = System.IO.Path.Combine(directory.Path, "target.json");
        await Accept(dialog, "ProfileTarget", locale);
        await Assert.That(dialog.LastTargetReceipt?.InstallationEstablished).IsTrue();
        string target = await File.ReadAllTextAsync(dialog.TargetPath.Text);
        await Assert.That(target.Contains("synthetic-held-secret", StringComparison.Ordinal)).IsTrue();
        await Assert.That(dialog.StatusText.Contains("synthetic-held-secret", StringComparison.Ordinal)).IsFalse();
        await Assert.That(form.Snapshot().Values.ContainsKey("token")).IsFalse();
        dialog.RetirePath.Text = "/token";
        ButtonFor(dialog, "ProfileRetire", locale).InvokeCommand(Command.Accept);
        await Assert.That(form.Profile!.Slots.Single(slot => slot.Path == "/token").IsRetained).IsTrue();
        ButtonFor(dialog, "ProfileRetire", locale).InvokeCommand(Command.Accept);
        await Assert.That(form.Profile!.Slots.Single(slot => slot.Path == "/token").IsRetained).IsFalse();
    }

    [Test]
    [NotInParallel]
    public async Task DeviceOnlyRequiresAcknowledgementAndAbsentSshKeyringNeverFallsBack()
    {
        using var directory = new PrivateDirectory();
        using var form = TerminalDefinitionFixture.Form();
        using var dialog = new TerminalProfileDialog(form, keyring: new MissingKeyring());
        dialog.StoreRoot.Text = directory.Path;
        await Accept(dialog, "ProfileSaveBlind");
        await Assert.That(dialog.StatusText.Contains("DeviceBoundAcknowledgementRequired", StringComparison.Ordinal)).IsTrue();
        ButtonFor(dialog, "ProfilePortable").InvokeCommand(Command.Accept);
        ButtonFor(dialog, "ProfileAcknowledge").InvokeCommand(Command.Accept);
        await Accept(dialog, "ProfileSaveBlind");
        await Assert.That(dialog.StatusText.Contains("ServiceMissing", StringComparison.Ordinal)).IsTrue();
        await Assert.That(Directory.GetFiles(directory.Path)).IsEmpty();
    }

    [Test]
    [NotInParallel]
    public async Task MismatchedConfirmationAndUnsupportedAlgorithmsCannotOpenOrSave()
    {
        using var directory = new PrivateDirectory();
        using var form = TerminalDefinitionFixture.Form();
        using var dialog = new TerminalProfileDialog(form);
        dialog.StoreRoot.Text = directory.Path;
        dialog.Passphrase.TryReplaceSensitiveInput(Passphrase);
        dialog.Confirmation.TryReplaceSensitiveInput("different synthetic passphrase");
        await Accept(dialog, "ProfileSaveBlind");
        await Assert.That(dialog.StatusText.Contains("PassphraseConfirmationMismatch", StringComparison.Ordinal)).IsTrue();
        await Assert.That(Directory.GetFiles(directory.Path)).IsEmpty();
        SupplyPassphrase(dialog);
        await Accept(dialog, "ProfileSaveInspectable");
        string file = Directory.GetFiles(directory.Path).Single();
        string container = await File.ReadAllTextAsync(file);
        // Change the authenticated protected header to an unsupported algorithm without a valid tag.
        var document = System.Text.Json.Nodes.JsonNode.Parse(container)!;
        document["protected"] = Convert.ToBase64String(
            """{"enc":"A128GCM","typ":"cfgvault"}"""u8.ToArray())
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        await File.WriteAllTextAsync(file, document.ToJsonString());
        await Accept(dialog, "ProfileList");
        SupplyPassphrase(dialog);
        await Accept(dialog, "ProfileOpen");
        await Assert.That(form.Profile).IsNull();
        await Assert.That(dialog.StatusText.Contains(Passphrase, StringComparison.Ordinal)).IsFalse();
        await Assert.That(dialog.Passphrase.Text).IsEmpty();
    }

    [Test]
    [NotInParallel]
    [Arguments("en")]
    [Arguments("ar")]
    public async Task ComparisonHasNoRestoreAuthorityAndCancellationClearsSeparateCredentials(string locale)
    {
        using var directory = new PrivateDirectory();
        using var form = TerminalDefinitionFixture.Form();
        form.SelectLocale(locale);
        using var dialog = new TerminalProfileDialog(form);
        dialog.StoreRoot.Text = directory.Path;
        await Accept(dialog, "ProfileSaveComparison", locale);
        await Accept(dialog, "ProfileList", locale);
        await Accept(dialog, "ProfileCompare", locale);
        await Assert.That(form.Profile).IsNull();
        SupplyPassphrase(dialog);
        await Accept(dialog, "ProfileOpen", locale);
        await Assert.That(form.Profile).IsNull();
        SupplyPassphrase(dialog);
        dialog.Retire();
        await Assert.That(dialog.Passphrase.IsRevealed).IsFalse();
        await Assert.That(dialog.Confirmation.IsRevealed).IsFalse();
    }

    private static void SupplyPassphrase(TerminalProfileDialog dialog)
    {
        dialog.Passphrase.TryReplaceSensitiveInput(Passphrase);
        dialog.Confirmation.TryReplaceSensitiveInput(Passphrase);
    }

    [Test]
    [NotInParallel]
    [Arguments("en")]
    [Arguments("ar")]
    public async Task KeyboardFocusReachesSeparateMaskedFieldsAndLocalizedCommands(string locale)
    {
        using var application = global::Terminal.Gui.App.Application.Create();
        application.Init("dotnet");
        using var form = TerminalDefinitionFixture.Form();
        form.SelectLocale(locale);
        using var dialog = new TerminalProfileDialog(form);
        dialog.BeginInit();
        dialog.EndInit();
        dialog.Frame = new System.Drawing.Rectangle(0, 0, 78, 22);
        dialog.Layout(dialog.Frame.Size);
        dialog.StoreRoot.SetFocus();
        await Assert.That(dialog.StoreRoot.HasFocus).IsTrue();
        dialog.Passphrase.SetFocus();
        foreach (char character in Passphrase)
            dialog.Passphrase.NewKeyDownEvent(new Key(character));
        await Assert.That(dialog.Passphrase.HasFocus).IsTrue();
        await Assert.That(dialog.Passphrase.Text.Contains(Passphrase, StringComparison.Ordinal)).IsFalse();
        dialog.Confirmation.SetFocus();
        await Assert.That(dialog.Confirmation.HasFocus).IsTrue();
        Button save = ButtonFor(dialog, "ProfileSaveBlind", locale);
        save.SetFocus();
        dialog.Layout(dialog.Frame.Size);
        await Assert.That(save.HasFocus).IsTrue();
        await Assert.That(save.Frame.Top).IsGreaterThanOrEqualTo(dialog.Body.Viewport.Top);
        await Assert.That(save.Frame.Bottom).IsLessThanOrEqualTo(dialog.Body.Viewport.Bottom);
        await Assert.That(dialog.Passphrase.IsRevealed).IsFalse();
        dialog.Retire();
        await Assert.That(dialog.Passphrase.Text).IsEmpty();
        await Assert.That(dialog.Confirmation.Text).IsEmpty();
    }

    private static Button ButtonFor(TerminalProfileDialog dialog, string key, string locale = "en") =>
        dialog.Body.SubViews.OfType<Button>().Single(button => button.Text == TerminalStrings.Get(key, locale));

    [Test]
    [NotInParallel]
    public async Task KeyboardTraversalReachesCancelAndKeepsEveryFocusedControlVisible()
    {
        using var application = global::Terminal.Gui.App.Application.Create();
        application.Init("dotnet");
        using var form = TerminalDefinitionFixture.Form();
        using var dialog = new TerminalProfileDialog(form);
        dialog.BeginInit();
        dialog.EndInit();
        dialog.Frame = new System.Drawing.Rectangle(0, 0, 78, 22);
        dialog.Layout(dialog.Frame.Size);
        dialog.StoreRoot.SetFocus();
        Button cancel = ButtonFor(dialog, "DialogCancel");
        bool reachedCancel = false;
        for (int index = 0; index <= dialog.Body.SubViews.Count(view => view.CanFocus); index++)
        {
            dialog.Body.AdvanceFocus(NavigationDirection.Forward, TabBehavior.TabStop);
            dialog.Layout(dialog.Frame.Size);
            if (dialog.Body.Focused is not { } focused) continue;
            reachedCancel |= ReferenceEquals(focused, cancel);
            await Assert.That(focused.Frame.Top).IsGreaterThanOrEqualTo(dialog.Body.Viewport.Top);
            await Assert.That(focused.Frame.Bottom).IsLessThanOrEqualTo(dialog.Body.Viewport.Bottom);
        }
        await Assert.That(reachedCancel).IsTrue();
    }

    [Test]
    [NotInParallel]
    [Arguments("en")]
    [Arguments("ar")]
    public async Task RecoveryGuidanceReflowsBeforeCommandsAtMinimumTerminalSize(string locale)
    {
        using var application = global::Terminal.Gui.App.Application.Create();
        application.Init("dotnet");
        using var form = TerminalDefinitionFixture.Form();
        form.SelectLocale(locale);
        using var dialog = new TerminalProfileDialog(form);
        dialog.BeginInit();
        dialog.EndInit();
        dialog.Frame = new System.Drawing.Rectangle(0, 0, 118, 38);
        dialog.Layout(dialog.Frame.Size);
        int wideHeight = dialog.Body.SubViews.OfType<Label>()
            .Where(label => label.Frame.Width > 32 && !string.IsNullOrEmpty(label.Text))
            .Sum(label => label.Frame.Height);
        dialog.Frame = new System.Drawing.Rectangle(0, 0, 78, 22);
        dialog.Layout(dialog.Frame.Size);
        Label[] guidance = dialog.Body.SubViews.OfType<Label>()
            .Where(label => label.Frame.Width > 32 && !string.IsNullOrEmpty(label.Text)).ToArray();
        await Assert.That(guidance.Sum(label => label.Frame.Height)).IsGreaterThan(wideHeight);
        await Assert.That(dialog.Body.SubViews.OfType<Button>().Min(button => button.Frame.Top))
            .IsGreaterThanOrEqualTo(guidance.Max(label => label.Frame.Bottom));
        await Assert.That(dialog.Body.GetContentHeight()).IsGreaterThan(dialog.Body.Viewport.Height);
    }

    private static async Task Accept(TerminalProfileDialog dialog, string key, string locale = "en")
    {
        ButtonFor(dialog, key, locale).InvokeCommand(Command.Accept);
        await dialog.Completion.WaitAsync(TimeSpan.FromSeconds(30));
    }

    private sealed class MissingKeyring : IOsKeyringProvider
    {
        public ValueTask<OsKeyringResult> LookupAsync(Guid keyId, OsKeyringPurpose purpose,
            CancellationToken cancellationToken = default) => throw new OsKeyringException(OsKeyringStatus.ServiceMissing);
        public ValueTask<OsKeyringResult> CreateAsync(OsKeyringPurpose purpose,
            CancellationToken cancellationToken = default) => throw new OsKeyringException(OsKeyringStatus.ServiceMissing);
    }

    private sealed class PrivateDirectory : IDisposable
    {
        internal PrivateDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "terminal-profile-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
        internal string Path { get; }
        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
