namespace Configurator.Desktop.Tests;

using System.Text.Json;
using Avalonia.Controls;
using Configurator.Core.Definitions;
using Configurator.Core.Profiles;
using Configurator.Desktop.ViewModels;
using Configurator.Desktop.ViewModels.Fields;
using Configurator.Desktop.Tests.ViewModels;

/// <summary>Native Linux workflow evidence; other host qualification is separate.</summary>
[NotInParallel]
public sealed class DesktopProfileWorkflowTests
{
    [Test]
    [Arguments("en")]
    [Arguments("ar")]
    public async Task ProfileControlsExposeFourModesMaskedEntryAndKeyboardFocus(string locale)
    {
        var session = Avalonia.Headless.HeadlessUnitTestSession.StartNew(typeof(DesktopApplicationTests));
        try
        {
            var result = await session.Dispatch(() =>
            {
                using var shell = DesktopTestContext.Root();
                var source = System.Text.Json.Nodes.JsonNode.Parse(DesktopApplicationTests.Definition())!;
                source["locales"] = new System.Text.Json.Nodes.JsonArray("ar", "en");
                source["localization"] = System.Text.Json.Nodes.JsonNode.Parse(
                    """{"defaultLocale":"en","translations":{"ar":{"direction":"rtl","overrides":{}}}}""");
                Definition admitted = DefinitionSourceCodec.ParseJson(System.Text.Encoding.UTF8.GetBytes(source.ToJsonString()));
                bool loaded = shell.Loader.LoadDefinition(admitted.CanonicalBytes);
                shell.Workspace.SelectedLocale = locale;
                var view = new Configurator.Desktop.Views.ProfileWorkspaceView { DataContext = shell };
                var window = new Avalonia.Controls.Window
                {
                    Content = view, Width = 700, Height = 800,
                    FlowDirection = locale == "ar" ? Avalonia.Media.FlowDirection.RightToLeft : Avalonia.Media.FlowDirection.LeftToRight
                };
                try
                {
                    window.Show();
                    var expander = Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(view)
                        .OfType<Avalonia.Controls.Expander>().Single();
                    expander.IsExpanded = true;
                    window.UpdateLayout();
                    Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                    var passphrase = view.FindControl<Avalonia.Controls.TextBox>("ProfilePassphrase")!;
                    var modes = view.FindControl<Avalonia.Controls.ComboBox>("ProfileMode")!;
                    return Task.FromResult((loaded, locale: shell.Workspace.SelectedLocale,
                        count: modes.Items.Count, focused: passphrase.Focus(),
                        masked: passphrase.PasswordChar != '\0',
                        named: !string.IsNullOrWhiteSpace(Avalonia.Automation.AutomationProperties.GetName(passphrase)),
                        directoryLtr: Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(view)
                            .OfType<TextBox>().Single(box =>
                                Avalonia.Automation.AutomationProperties.GetName(box) == shell.Workspace.Strings["ProfileDirectory"])
                            .FlowDirection == Avalonia.Media.FlowDirection.LeftToRight,
                        isolated: shell.Profiles.ReviewedProfile.StartsWith('\u2066') &&
                            shell.Profiles.ReviewedProfile.EndsWith('\u2069'),
                        localized: locale != "ar" || shell.Workspace.Strings["ProfileRecoveryHelp"].Any(c => c is >= '\u0600' and <= '\u06ff')));
                }
                finally { window.Close(); }
            }, CancellationToken.None);
            await Assert.That(result.loaded).IsTrue();
            await Assert.That(result.locale).IsEqualTo(locale);
            await Assert.That(result.count).IsEqualTo(4);
            await Assert.That(result.focused).IsTrue();
            await Assert.That(result.masked).IsTrue();
            await Assert.That(result.named).IsTrue();
            await Assert.That(result.directoryLtr).IsTrue();
            await Assert.That(result.isolated).IsTrue();
            await Assert.That(result.localized).IsTrue();
        }
        finally { await Task.Run(session.Dispose); }
    }

    [Test]
    [Arguments(1)]
    [Arguments(2)]
    public async Task PortableRevisionRestoresIndependentlyAndRekeyPreservesOldCopy(int mode)
    {
        if (!OperatingSystem.IsLinux()) return;
        string directory = PrivateDirectory();
        string passphrase = Guid.NewGuid().ToString("N");
        string replacementPassphrase = Guid.NewGuid().ToString("N");
        string answer = Guid.NewGuid().ToString("N");
        using var shell = DesktopTestContext.Root();
        using var definition = SecretFieldViewModelTests.CreateForm();
        shell.Loader.LoadDefinition(definition.Definition.CanonicalBytes);
        shell.Workspace.Form!.SetValue(DefinitionPath.Parse("/password"), JsonSerializer.SerializeToElement(answer));
        ProfileManagerViewModel profiles = shell.Profiles;
        profiles.Directory = directory;
        profiles.ModeIndex = mode;
        try
        {
            await profiles.SaveAsync(passphrase, passphrase);
            await Assert.That(profiles.RecoveryStatus).IsEqualTo("ConfiguredUnverified").Because(profiles.Status);
            await profiles.RefreshCommand.ExecuteAsync(null);
            await Assert.That(profiles.Revisions.Count).IsEqualTo(1);
            profiles.SelectedRevision = profiles.Revisions.Single();
            Guid oldSnapshot = profiles.SelectedRevision.SnapshotId;
            await profiles.CheckRecoveryAsync(passphrase);
            await Assert.That(profiles.RecoveryStatus).IsEqualTo("VerifiedForSnapshot");
            await profiles.OpenAsync(passphrase);
            await Assert.That(profiles.Profile!.SnapshotId).IsEqualTo(oldSnapshot);
            await Assert.That(profiles.Profile.Mode).IsEqualTo(mode == 1
                ? ProfileProtectionMode.ImportUpgradeOnly : ProfileProtectionMode.InspectableEncrypted);
            if (mode == 1)
            {
                await Assert.That(shell.Workspace.Form.Snapshot().Values.ContainsKey("password")).IsFalse();
                await Assert.That(shell.Workspace.Fields.OfType<StringFieldViewModel>().Single().Text).IsEmpty();
                await Assert.That(shell.Workspace.Fields.OfType<StringFieldViewModel>().Single().RevealCommand.CanExecute(null)).IsFalse();
                await Assert.That(shell.Workspace.Fields.OfType<StringFieldViewModel>().Single().CopyCommand.CanExecute(null)).IsFalse();
                await Assert.That(profiles.RetainedSlots.Single().IsRetained).IsTrue();
                await Assert.That(profiles.Replace(profiles.RetainedSlots.Single(), false)).IsFalse();
                await Assert.That(profiles.RetainedSlots.Single().IsRetained).IsTrue();
            }
            await profiles.SaveAsync(replacementPassphrase, replacementPassphrase);
            await profiles.RefreshCommand.ExecuteAsync(null);
            await Assert.That(profiles.Revisions.Count).IsEqualTo(2);
            profiles.SelectedRevision = profiles.Revisions.Single(item => item.SnapshotId == oldSnapshot);
            await profiles.OpenAsync(passphrase);
            await Assert.That(profiles.Profile!.SnapshotId).IsEqualTo(oldSnapshot);
            await Assert.That(profiles.Status).IsEqualTo("profile-opened");
        }
        finally { Directory.Delete(directory, true); }
    }

    [Test]
    public async Task SavedRevisionsHaveDistinctVisibleIdentitiesAndAnIndependentRecoveryReference()
    {
        if (!OperatingSystem.IsLinux()) return;
        string directory = PrivateDirectory();
        var session = Avalonia.Headless.HeadlessUnitTestSession.StartNew(typeof(DesktopApplicationTests));
        try
        {
            var result = await session.Dispatch(async () =>
            {
                using var shell = DesktopTestContext.Root();
                using var source = SecretFieldViewModelTests.CreateForm();
                shell.Loader.LoadDefinition(source.Definition.CanonicalBytes);
                shell.Workspace.Form!.SetValue(DefinitionPath.Parse("/password"),
                    JsonSerializer.SerializeToElement("synthetic-profile-display-token"));
                shell.Profiles.Directory = directory;
                var view = new Configurator.Desktop.Views.ProfileWorkspaceView { DataContext = shell };
                var window = new Window { Content = view, Width = 700, Height = 800 };
                try
                {
                    window.Show();
                    Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(view)
                        .OfType<Expander>().Single().IsExpanded = true;
                    await shell.Profiles.SaveAsync("synthetic portable profile passphrase", "synthetic portable profile passphrase");
                    await shell.Profiles.SaveAsync("synthetic portable profile passphrase", "synthetic portable profile passphrase");
                    await shell.Profiles.RefreshCommand.ExecuteAsync(null);
                    window.UpdateLayout();
                    Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                    string[] text = Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(view)
                        .OfType<TextBlock>().Select(block => block.Text ?? "").ToArray();
                    ProfileRecoveryReference reference = shell.Profiles.RecoveryReference!;
                    bool referenceVisible = Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(view)
                        .OfType<TextBox>().Any(box => box.IsReadOnly && box.IsEffectivelyVisible &&
                            box.Text == reference.Digest.Value);
                    return (count: shell.Profiles.Revisions.Count,
                        distinct: shell.Profiles.Revisions.Select(revision => revision.SnapshotId).Distinct().Count(),
                        identitiesVisible: shell.Profiles.Revisions.All(revision =>
                            text.Contains(revision.ProfileId.ToString("D"), StringComparer.Ordinal) &&
                            text.Contains(revision.SnapshotId.ToString("D"), StringComparer.Ordinal)),
                        referenceVisible,
                        referenceMatches: shell.Profiles.Revisions.Any(revision =>
                            revision.ProfileId == reference.ProfileId && revision.SnapshotId == reference.SnapshotId));
                }
                finally { window.Close(); }
            }, CancellationToken.None);
            await Assert.That(result.count).IsEqualTo(2);
            await Assert.That(result.distinct).IsEqualTo(2);
            await Assert.That(result.identitiesVisible).IsTrue();
            await Assert.That(result.referenceVisible).IsTrue();
            await Assert.That(result.referenceMatches).IsTrue();
        }
        finally
        {
            await Task.Run(session.Dispose);
            Directory.Delete(directory, true);
        }
    }

    [Test]
    [Arguments(1, 2)]
    [Arguments(2, 1)]
    public async Task OpeningProjectsTheAuthenticatedModeInsteadOfThePreviousSelection(int savedMode, int previousSelection)
    {
        if (!OperatingSystem.IsLinux()) return;
        string directory = PrivateDirectory();
        using var shell = DesktopTestContext.Root();
        shell.Loader.LoadDefinition(DesktopApplicationTests.Definition());
        shell.Profiles.Directory = directory;
        shell.Profiles.ModeIndex = savedMode;
        try
        {
            await shell.Profiles.SaveAsync("synthetic portable profile passphrase", "synthetic portable profile passphrase");
            await shell.Profiles.RefreshCommand.ExecuteAsync(null);
            shell.Profiles.SelectedRevision = shell.Profiles.Revisions.Single();
            shell.Profiles.ModeIndex = previousSelection;
            await shell.Profiles.OpenAsync("synthetic portable profile passphrase");
            await Assert.That(shell.Profiles.Profile!.Mode).IsEqualTo(savedMode == 1
                ? ProfileProtectionMode.ImportUpgradeOnly : ProfileProtectionMode.InspectableEncrypted);
            await Assert.That(shell.Profiles.ModeIndex).IsEqualTo(savedMode);
        }
        finally { Directory.Delete(directory, true); }
    }

    [Test]
    public async Task UnsupportedAlgorithmNeverAdoptsAnswers()
    {
        if (!OperatingSystem.IsLinux()) return;
        string directory = PrivateDirectory();
        using var shell = DesktopTestContext.Root();
        shell.Loader.LoadDefinition(DesktopApplicationTests.Definition());
        shell.Profiles.Directory = directory;
        string passphrase = Guid.NewGuid().ToString("N");
        try
        {
            await shell.Profiles.SaveAsync(passphrase, passphrase);
            string file = Directory.GetFiles(directory).Single();
            var container = System.Text.Json.Nodes.JsonNode.Parse(await File.ReadAllBytesAsync(file))!;
            container["header"]!["alg"] = "unsupported";
            await File.WriteAllTextAsync(file, container.ToJsonString());
            await shell.Profiles.RefreshCommand.ExecuteAsync(null);
            shell.Profiles.SelectedRevision = shell.Profiles.Revisions.Single();
            await shell.Profiles.OpenAsync(passphrase);
            await Assert.That(shell.Profiles.Profile).IsNull();
            await Assert.That(shell.Profiles.Status).IsEqualTo("profile-InvalidContainer");
        }
        finally { Directory.Delete(directory, true); }
    }

    [Test]
    public async Task ConfirmedRetirementCannotRestoreRetainedAuthorityThroughUndo()
    {
        using var shell = DesktopTestContext.Root();
        using var source = SecretFieldViewModelTests.CreateForm();
        shell.Loader.LoadDefinition(source.Definition.CanonicalBytes);
        string answer = Guid.NewGuid().ToString("N");
        source.SetValue(DefinitionPath.Parse("/password"), JsonSerializer.SerializeToElement(answer));
        byte[] payload = ProfilePayloadCodec.Serialize(source.Definition, source.Snapshot(),
            ProfileProtectionMode.ImportUpgradeOnly, Guid.NewGuid(), Guid.NewGuid(), "local", "synthetic");
        try
        {
            bool admitted = await shell.Workspace.Form!.OpenProfileAsync(_ =>
                Task.FromResult(ProfilePayloadCodec.Admit(payload)));
            await Assert.That(admitted).IsTrue();
            await Assert.That(shell.Profiles.Replace(shell.Profiles.RetainedSlots.Single(), true)).IsTrue();
            shell.Workspace.Form.Undo();
            await Assert.That(shell.Profiles.RetainedSlots.Single().IsRetained).IsFalse();
            await Assert.That(shell.Workspace.Form.Snapshot().Values.ContainsKey("password")).IsFalse();
        }
        finally { System.Security.Cryptography.CryptographicOperations.ZeroMemory(payload); }
    }

    [Test]
    public async Task ComparisonSaveOmitsRecoverableSecretsAndRejectsRestore()
    {
        if (!OperatingSystem.IsLinux()) return;
        string directory = PrivateDirectory();
        using var shell = DesktopTestContext.Root();
        using var source = SecretFieldViewModelTests.CreateForm();
        shell.Loader.LoadDefinition(source.Definition.CanonicalBytes);
        shell.Workspace.Form!.SetValue(DefinitionPath.Parse("/password"),
            JsonSerializer.SerializeToElement(Guid.NewGuid().ToString("N")));
        shell.Profiles.Directory = directory;
        shell.Profiles.ModeIndex = 0;
        try
        {
            await shell.Profiles.SaveAsync("", "");
            await shell.Profiles.RefreshCommand.ExecuteAsync(null);
            shell.Profiles.SelectedRevision = shell.Profiles.Revisions.Single();
            await shell.Profiles.CompareAsync();
            await Assert.That(shell.Profiles.Status).IsEqualTo("profile-public-unchanged-secret-unavailable");
            await shell.Profiles.OpenAsync("");
            await Assert.That(shell.Profiles.Profile).IsNull();
            await Assert.That(shell.Profiles.Status).IsEqualTo("profile-InvalidPayload");
        }
        finally { Directory.Delete(directory, true); }
    }

    [Test]
    public async Task PassphraseConfirmationFailureCreatesNoRevision()
    {
        if (!OperatingSystem.IsLinux()) return;
        string directory = PrivateDirectory();
        using var shell = DesktopTestContext.Root();
        shell.Loader.LoadDefinition(DesktopApplicationTests.Definition());
        shell.Profiles.Directory = directory;
        try
        {
            await shell.Profiles.SaveAsync(Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N"));
            await Assert.That(shell.Profiles.Status).IsEqualTo("profile-InvalidPassphrase");
            await Assert.That(Directory.GetFiles(directory)).IsEmpty();
        }
        finally { Directory.Delete(directory, true); }
    }

    [Test]
    public async Task FailedDeviceLookupDoesNotPublishOrSubstitutePortableRecipient()
    {
        if (!OperatingSystem.IsLinux()) return;
        string directory = PrivateDirectory();
        using var shell = DesktopTestContext.Root();
        shell.Loader.LoadDefinition(DesktopApplicationTests.Definition());
        shell.Profiles.Directory = directory;
        shell.Profiles.UseDevice = true;
        shell.Profiles.DeviceKeyId = Guid.Empty.ToString();
        string material = Guid.NewGuid().ToString("N");
        try
        {
            await shell.Profiles.SaveAsync(material, material);
            await Assert.That(shell.Profiles.Status).IsEqualTo("profile-keyring-InvalidRequest");
            await Assert.That(Directory.GetFiles(directory)).IsEmpty();
            await Assert.That(shell.Workspace.Form!.Profile).IsNull();
        }
        finally { Directory.Delete(directory, true); }
    }

    [System.Runtime.Versioning.SupportedOSPlatform("linux")]
    private static string PrivateDirectory()
    {
        string directory = Directory.CreateTempSubdirectory("configurator-profile-workflow-").FullName;
        File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return directory;
    }
}
