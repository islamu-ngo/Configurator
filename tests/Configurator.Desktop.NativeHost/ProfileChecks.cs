namespace Configurator.Desktop.NativeHost;

using System.ComponentModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Configurator.Artifacts.Profiles;
using Configurator.Core.Profiles;
using Configurator.Desktop.ViewModels;
using Configurator.Desktop.Views;

internal static partial class Program
{
    private static async Task ProfilesAsync(MainWindow window, DesktopShellViewModel shell, string directory)
    {
        if (!OperatingSystem.IsLinux()) throw new PlatformNotSupportedException();
        const string secret = "synthetic-native-held-profile-secret";
        const string passphrase = "synthetic-native-profile-passphrase";
        byte[] definition = Encoding.UTF8.GetBytes("""
            {"$schema":"urn:islamu:configurator:authoring:v0.0.1",
             "identity":{"familyId":"7de66880-f9cc-4eb3-a442-ddc9aaeed036","revision":1},
             "project":{"id":"native-profile","name":"Synthetic profile"},"applicationVersions":["1"],
             "localization":{"defaultLocale":"en","translations":{"ar":{"direction":"rtl","overrides":{}}}},
             "configuration":{"type":"object","additionalProperties":false,"required":["token"],
               "properties":{"token":{"type":"string","title":"Profile token",
                 "x-configurator":{"sensitivity":"secret"}}}},
             "outputs":{"profile.json":{"id":"config","format":"json","bindings":{"/token":{"scope":"root","path":"/token"}}}}}
            """);
        _currentCheck = "D31-profile-library-custody";
        Require(shell.Loader.LoadDefinition(definition), "profile-definition-load");
        Require(shell.Workspace.Form!.SelectLocale(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName),
            "profile-locale");
        TextBox secretInput = Input<TextBox>(window, "Profile token");
        Require(secretInput.PasswordChar != '\0', "profile-secret-editor-unmasked");
        secretInput.Text = secret;
        await FlushAsync();
        byte[] payload = ProfilePayloadCodec.Serialize(shell.Workspace.Form!.Definition,
            shell.Workspace.Form.Snapshot(), ProfileProtectionMode.ImportUpgradeOnly,
            Guid.Parse("55555555-5555-4555-8555-555555555555"),
            Guid.Parse("66666666-6666-4666-8666-666666666666"), "local", "synthetic");
        byte[] material = Encoding.UTF8.GetBytes(passphrase);
        byte[] container;
        using (var codec = new NativeProfileContainerCodec())
        {
            try
            {
                container = await codec.SealAsync(payload, [material]);
                using UnlockedProfile opened = await codec.OpenAsync(container, 0, material);
                Require(opened.Mode == ProfileProtectionMode.ImportUpgradeOnly, "profile-mode");
                bool refused = false;
                try { _ = ProfilePayloadCodec.InspectAnswers(opened); }
                catch (ProfileException exception) { refused = exception.Code == ProfileFailureCode.ModeRestricted; }
                Require(refused, "profile-inspection-disclosed");
                using JsonDocument baseline = JsonDocument.Parse(
                    CompareBaselineCodec.Serialize(CompareBaselineCodec.Create(opened)));
                Require(!baseline.RootElement.GetRawText().Contains(secret, StringComparison.Ordinal),
                    "profile-comparison-disclosed");
            }
            finally
            {
                CryptographicOperations.ZeroMemory(payload);
                CryptographicOperations.ZeroMemory(material);
            }
        }
        string storeDirectory = Path.Combine(directory, "profiles");
        Directory.CreateDirectory(storeDirectory);
        File.SetUnixFileMode(storeDirectory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var store = new LocalProfileVaultStore(storeDirectory);
        try
        {
            var saved = await store.SaveAsync(Guid.Parse("55555555-5555-4555-8555-555555555555"),
                Guid.Parse("66666666-6666-4666-8666-666666666666"), ProfileFileKind.Encrypted, container);
            Require(saved.Outcomes.Count == 1 && saved.Outcomes[0].Result?.InstallationEstablished == true,
                "profile-store-not-installed");
        }
        finally { CryptographicOperations.ZeroMemory(container); }
        Pass();

        _currentCheck = "D32-profile-controls-focus";
        shell.Profiles.Directory = storeDirectory;
        await shell.Profiles.RefreshCommand.ExecuteAsync(null);
        Require(shell.Profiles.Revisions.Count == 1, "profile-discovery-empty");
        shell.Profiles.SelectedRevision = shell.Profiles.Revisions.Single();
        shell.Navigation.OpenCommand.Execute(null);
        ProfileWorkspaceView view = await ProfileViewAsync(window);
        TextBox password = view.GetNamedControl<TextBox>("ProfilePassphrase");
        TextBox confirmation = view.GetNamedControl<TextBox>("ProfileConfirmation");
        Require(password.PasswordChar != '\0' && confirmation.PasswordChar != '\0', "profile-password-unmasked");
        password.Text = passphrase;
        confirmation.Text = passphrase;
        Require(password.Focus(), "profile-focus-refused");
        await FlushAsync();
        Require(password.IsFocused, "profile-focus-missing");
        await CaptureAsync(window, "profile-open-masked");
        await ProfileActionAsync(view, shell, "ProfileOpen", () => shell.Profiles.Status == "profile-opened");
        Require(string.IsNullOrEmpty(password.Text) && string.IsNullOrEmpty(confirmation.Text),
            "profile-credential-not-retired");
        Require(password.IsFocused && shell.Profiles.HasProfile && shell.Profiles.RetainedSlots.Count == 1,
            "profile-open-custody-focus");
        Require(!shell.Workspace.Form!.Snapshot().Values.Values.Any(value =>
            value.GetRawText().Contains(secret, StringComparison.Ordinal)), "profile-snapshot-disclosed");
        foreach (string stage in new[] { "open", "review", "settings" })
        {
            if (stage == "review") shell.Navigation.ReviewCommand.Execute(null);
            if (stage == "settings")
            {
                shell.Navigation.SettingsCommand.Execute(null);
                shell.Navigation.SelectedSettingsSection = shell.Navigation.SettingsSections.Single(
                    section => section.Id == DesktopSettingsSectionId.PrivacyAndHost);
            }
            view = await ProfileViewAsync(window);
            password = view.GetNamedControl<TextBox>("ProfilePassphrase");
            Require(password.PasswordChar != '\0' && password.Focus(), "profile-stage-masking-focus");
            await FlushAsync();
            Require(password.IsFocused, "profile-stage-focus");
            foreach (Control control in window.GetLogicalDescendants().OfType<Control>().Where(control => control.IsEffectivelyVisible))
            {
                Require(!(AutomationProperties.GetName(control) ?? "").Contains(secret, StringComparison.Ordinal)
                    && !(AutomationProperties.GetHelpText(control) ?? "").Contains(secret, StringComparison.Ordinal),
                    "profile-automation-disclosed");
                if (control is TextBlock text)
                    Require(!(text.Text ?? "").Contains(secret, StringComparison.Ordinal), "profile-text-disclosed");
                if (control is TextBox input)
                    Require(!(input.Text ?? "").Contains(secret, StringComparison.Ordinal), "profile-editor-disclosed");
            }
            await CaptureAsync(window, "profile-" + stage + "-held");
        }
        Pass();

        _currentCheck = "D33-profile-protected-target";
        string target = Path.Combine(directory, "profile-target.json");
        await shell.Profiles.SaveTargetAsync("config", directory, "profile-target.json");
        Require(File.Exists(target), "profile-target-missing");
        using (JsonDocument result = JsonDocument.Parse(await File.ReadAllBytesAsync(target)))
            Require(result.RootElement.GetProperty("token").GetString() == secret, "profile-target-value");
        Require(File.GetUnixFileMode(target) == (UnixFileMode.UserRead | UnixFileMode.UserWrite),
            "profile-target-permissions");
        byte[] original = await File.ReadAllBytesAsync(target);
        try
        {
            await shell.Profiles.SaveTargetAsync("config", directory, "profile-target.json");
            Require((await File.ReadAllBytesAsync(target)).AsSpan().SequenceEqual(original), "profile-target-overwritten");
            Require(shell.Profiles.Status.Contains("TargetExists", StringComparison.Ordinal), "profile-target-conflict-unreported");
        }
        finally { CryptographicOperations.ZeroMemory(original); }
        await CaptureAsync(window, "profile-protected-target");
        Pass();
    }

    private static async Task<ProfileWorkspaceView> ProfileViewAsync(MainWindow window)
    {
        window.UpdateLayout();
        await FlushAsync();
        var shell = (DesktopShellViewModel)window.DataContext!;
        Control[] owners = window.GetLogicalDescendants().OfType<Control>().Where(control =>
            shell.Navigation.Stage switch
            {
                DesktopWorkspaceStage.Open => control is DefinitionLoaderView,
                DesktopWorkspaceStage.Review => control is ArtifactPublicationView,
                DesktopWorkspaceStage.Settings => control is SettingsWorkspaceView,
                _ => false
            }).ToArray();
        Require(owners.Length == 1 && owners[0].IsVisible, "profile-stage-owner");
        ProfileWorkspaceView[] visible = owners[0].GetLogicalDescendants().OfType<ProfileWorkspaceView>().ToArray();
        Require(visible.Length != 0, "profile-view-missing");
        Require(visible.Length == 1, "profile-view-ambiguous");
        ProfileWorkspaceView view = visible[0];
        Require(view.IsEffectivelyVisible, "profile-view-hidden");
        Expander[] expanders = view.GetLogicalDescendants().OfType<Expander>().ToArray();
        Require(expanders.Length == 1, "profile-expander-count");
        expanders[0].IsExpanded = true;
        window.UpdateLayout();
        await FlushAsync();
        return view;
    }

    private static async Task ProfileActionAsync(ProfileWorkspaceView view, DesktopShellViewModel shell,
        string label, Func<bool> finished)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void Changed(object? sender, PropertyChangedEventArgs args)
        {
            if (finished()) completion.TrySetResult();
        }
        shell.Profiles.PropertyChanged += Changed;
        try
        {
            Button button = view.GetLogicalDescendants().OfType<Button>().Single(button =>
                Equals(button.Content, shell.Workspace.Strings[label]));
            Require(button.IsEffectivelyEnabled, "profile-action-unavailable");
            button.Focus();
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            if (finished()) completion.TrySetResult();
            await completion.Task.WaitAsync(TimeSpan.FromSeconds(20));
            await FlushAsync();
        }
        finally { shell.Profiles.PropertyChanged -= Changed; }
    }
}
