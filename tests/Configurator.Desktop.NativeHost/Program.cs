namespace Configurator.Desktop.NativeHost;

using Avalonia.Input;

using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Messaging;
using Configurator.Artifacts;
using Configurator.Core.Artifacts;
using Configurator.Core.Definitions;
using Configurator.Desktop.Resources;
using Configurator.Desktop.Services;
using Configurator.Desktop.ViewModels;
using Configurator.Desktop.ViewModels.Fields;
using Configurator.Desktop.Views;

internal static class Program
{
    private static readonly string[] RequiredChecks =
    [
        "D01-offline-intake", "D02-typed-controls", "D03-activation-disclosure",
        "D04-json-yaml-dotenv-import", "D05-current-composable-preparation",
        "D06-protected-single-conflict", "D07-bundle-preflight-recovery",
        "D09-edit-cancel-stale-import", "D10-value-free-feedback-retry",
        "D12-event-one-tenant", "D12-event-two-tenants", "D12-event-three-tenants",
        "D09-close-disposes-workspace", "D14-physical-settlement-receipt",
        "D15-close-drain-settles", "D16-close-deadline-unknown", "D17-close-stay-force",
        "D18-shared-editing-presence-localized-actions",
        "D19-typed-map-variant-null-reset-locale",
        "D20-read-only-import-comparison", "D21-secret-generation-validation",
        "D22-deployment-context-destinations",
        "D23-root-map-literal-controls", "D24-root-variant-retirement",
        "D25-appearance-preference-contrast", "D26-guided-root-correction",
        "D27-responsive-scale-focus", "D28-collection-page-identity",
        "D29-private-metadata-discovery", "D30-selected-output-review"
    ];
    private static readonly HashSet<string> Completed = new(StringComparer.Ordinal);
    private static string _currentCheck = "native-startup";
    private static string _currentStep = "startup";
    private static string? _captureDirectory;

    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Length is not (4 or 6) || args[0] != "--expected-rid" || args[1] != "linux-x64" ||
            args[2] != "--minimum-expected-checks" ||
            !int.TryParse(args[3], NumberStyles.None, CultureInfo.InvariantCulture, out int minimum) ||
            minimum != 30 || RequiredChecks.Length != 30)
            return Reject("arguments-invalid");
        if (args.Length == 6)
        {
            if (args[4] != "--capture-directory" || !Path.IsPathFullyQualified(args[5]))
                return Reject("arguments-invalid");
            _captureDirectory = args[5];
            Directory.CreateDirectory(_captureDirectory);
        }
        if (!OperatingSystem.IsLinux() || RuntimeInformation.ProcessArchitecture != Architecture.X64)
            return Reject("expected-rid-mismatch");
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DISPLAY")))
            return Reject("native-x11-display-required");
        string culture = Environment.GetEnvironmentVariable("CONFIGURATOR_NATIVE_UI_CULTURE") ?? "en";
        if (culture is not ("en" or "ar")) return Reject("native-ui-culture-invalid");
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.CurrentUICulture;

        // A stalled startup or UI dispatcher must also fail within a bounded wall-clock interval.
        var elapsed = Stopwatch.StartNew();
        using var watchdog = new Timer(_ =>
        {
            Console.Error.WriteLine($"native-qualification:timeout:{_currentCheck}:{_currentStep}");
            Environment.Exit(1);
        }, null, TimeSpan.FromSeconds(90), Timeout.InfiniteTimeSpan);
        try
        {
            using var lifetime = new ClassicDesktopStyleApplicationLifetime { Args = [], ShutdownMode = ShutdownMode.OnExplicitShutdown };
            AppBuilder builder = AppBuilder.Configure<App>().UsePlatformDetect();
            builder.SetupWithLifetime(lifetime);
            if (lifetime.MainWindow is not MainWindow window || window.DataContext is not DesktopShellViewModel shell)
                return Reject("native-application-missing");
            if (window.TryGetPlatformHandle()?.HandleDescriptor != "XID")
                return Reject("native-backend-mismatch");
            window.Opened += (_, _) => Dispatcher.UIThread.Post(async () =>
            {
                int code = 1;
                try
                {
                    Require(window.TryGetPlatformHandle() is { Handle: not 0 }, "native-window-missing");
                    await RunAsync(window, shell);
                    Require(Completed.SetEquals(RequiredChecks) && Completed.Count >= minimum, "checks-incomplete");
                    if (_captureDirectory is not null)
                        File.WriteAllText(Path.Combine(_captureDirectory, "qualification.json"), JsonSerializer.Serialize(new
                        {
                            schemaVersion = 2,
                            rid = "linux-x64",
                            backend = "X11",
                            uiCulture = CultureInfo.CurrentUICulture.Name,
                            runtime = RuntimeInformation.FrameworkDescription,
                            durationMilliseconds = elapsed.Elapsed.TotalMilliseconds,
                            requiredChecks = RequiredChecks,
                            completedChecks = Completed.Order(StringComparer.Ordinal).ToArray(),
                            payloadAssemblies = PayloadIdentities()
                        }));
                    Console.WriteLine($"native-qualification:passed:{Completed.Count}:linux-x64:X11");
                    Console.WriteLine("qualification-limits:manual-dialogs-keyboard-rtl-scaling-assistive-technology-and-published-payload-required");
                    code = 0;
                }
                catch (QualificationException exception)
                {
                    Console.Error.WriteLine($"native-qualification:failed:{_currentCheck}:{exception.Code}");
                }
                catch (DefinitionException exception)
                {
                    Console.Error.WriteLine($"native-qualification:failed:{_currentCheck}:{exception.Code}");
                }
                catch (TimeoutException)
                {
                    Console.Error.WriteLine($"native-qualification:failed:{_currentCheck}:operation-timeout");
                }
                catch (Exception exception)
                {
                    Console.Error.WriteLine($"native-qualification:failed:{_currentCheck}:{_currentStep}:{exception.GetType().Name}");
                }
                finally
                {
                    shell.Dispose();
                    lifetime.Shutdown(code);
                }
            });
            return lifetime.Start([]);
        }
        catch (Exception)
        {
            return Reject("native-startup-failed");
        }
    }

    private static async Task DeploymentContextAsync(MainWindow window, DesktopShellViewModel shell, string directory)
    {
        Definition definition = DefinitionSourceCodec.ParseJson(System.Text.Encoding.UTF8.GetBytes("""
            {"$schema":"urn:islamu:configurator:authoring:v0.0.1",
             "identity":{"familyId":"54dd865a-e11f-4b54-9fd7-753e05831e8b","revision":1},
             "project":{"id":"native-scope","name":"Native scope"},"applicationVersions":["1"],
             "localization":{"defaultLocale":"en","translations":{"ar":{"direction":"rtl","overrides":{
              "/deployment/recipes/0/title":"منفصل","/deployment/recipes/0/scopes/1/title":"لوحة التحكم"}}}},
             "configuration":{"type":"object","additionalProperties":false,"x-configurator":{"sensitivity":"public"},"required":["recipe","scope","worker","dashboard"],"properties":{
              "recipe":{"type":"string","enum":["split"],"x-configurator":{"sensitivity":"public","initialValue":"split"}},
              "scope":{"type":"string","enum":["all","dashboard"],"x-configurator":{"sensitivity":"public","initialValue":"all"}},
              "worker":{"type":"string","x-configurator":{"sensitivity":"public","initialValue":"synthetic-worker"}},
              "dashboard":{"type":"string","x-configurator":{"sensitivity":"public","initialValue":"synthetic-dashboard"}}}},
             "deployment":{"recipePointer":"/recipe","scopePointer":"/scope","components":[{"id":"worker","title":"Worker"},{"id":"dashboard","title":"Dashboard"}],
              "recipes":[{"id":"split","title":"Split","scopes":[
               {"id":"all","title":"All","componentIds":["worker","dashboard"],"artifactIds":["worker","dashboard"]},
               {"id":"dashboard","title":"Dashboard","componentIds":["dashboard"],"artifactIds":["dashboard"]}]}]},
             "outputs":{
              "worker.json":{"id":"worker","format":"json","consumerIds":["worker"],"bindings":{"/value":{"scope":"root","path":"/worker"}}},
              "dashboard.json":{"id":"dashboard","format":"json","consumerIds":["dashboard"],"bindings":{"/value":{"scope":"root","path":"/dashboard"}}}}}
            """));
        Require(shell.Loader.LoadDefinition(definition.CanonicalBytes), "deployment-load");
        shell.Workspace.SelectedLocale = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        window.UpdateLayout();
        ComboBox selector = window.GetNamedControl<ComboBox>("ConfigurationContext");
        Require(selector.IsVisible && selector.Focus() && selector.IsFocused, "deployment-selector-focus");
        Require(!string.IsNullOrWhiteSpace(AutomationProperties.GetName(selector)), "deployment-selector-name");
        shell.Publisher.ArtifactId = "worker";
        Require(await shell.Publisher.PrepareAsync() == DesktopPreparationOutcome.Prepared && shell.Publisher.CanSave, "deployment-prepare");
        long revision = shell.Workspace.Form!.InputRevision;
        selector.SelectedItem = shell.Workspace.ConfigurationChoices.Single(choice => choice.ScopeId == "dashboard");
        Require(shell.Workspace.Form.InputRevision == revision + 1 &&
            shell.Workspace.Form.ConfigurationContext?.ScopeId == "dashboard", "deployment-atomic-binding");
        Require(shell.Publisher.Artifacts.Count == 1 && shell.Publisher.ArtifactId == "dashboard" &&
            !shell.Publisher.CanSave && shell.Publisher.Preparation is null, "deployment-stale-output");
        shell.Publisher.OutputDirectory = directory;
        shell.Publisher.FileName = "deployment-dashboard.json";
        Require(await shell.Publisher.PrepareAsync() == DesktopPreparationOutcome.Prepared, "deployment-scoped-prepare");
        ProtectedArtifactCommitResult saved = await shell.Publisher.SaveAsync();
        string output = Path.Combine(directory, "deployment-dashboard.json");
        Require(saved.VerificationSucceeded && await File.ReadAllTextAsync(output) == """{"value":"synthetic-dashboard"}""",
            "deployment-scoped-output");
        Require(OperatingSystem.IsLinux() && File.GetUnixFileMode(output) == (UnixFileMode.UserRead | UnixFileMode.UserWrite),
            "deployment-scoped-permissions");
        selector.SelectedItem = shell.Workspace.ConfigurationChoices.Single(choice => choice.ScopeId == "all");
        Require(shell.Publisher.Artifacts.Count == 2 && shell.Workspace.Form.ConfigurationContext?.ScopeId == "all",
            "deployment-switch-back");
        await CaptureAsync(window, "deployment-context");
    }

    private static async Task RunAsync(MainWindow window, DesktopShellViewModel shell)
    {
        FormWorkspaceViewModel workspace = shell.Workspace;
        ArtifactPublisherViewModel publisher = shell.Publisher;
        string directory = Directory.CreateTempSubdirectory("configurator-native-").FullName;
        try
        {
            await CaptureAsync(window, "open");
            _currentCheck = "D01-offline-intake";
            Require(workspace.Form is null && !publisher.CanPrepare && !publisher.CanSave, "default-target");
            string invalid = Path.Combine(directory, "invalid.json");
            await File.WriteAllTextAsync(invalid, "{}");
            await LoadAsync(window, shell, invalid, false);
            string definitionPath = Path.Combine(directory, "native-definition.json");
            byte[] definition = NativeDefinition();
            _ = DefinitionCodec.ParseJson(definition);
            await File.WriteAllBytesAsync(definitionPath, definition);
            await LoadAsync(window, shell, definitionPath, true);
            await CaptureAsync(window, "loaded");
            Require(shell.Loader.DefinitionPin.Contains(workspace.Form!.Definition.Digest.Value, StringComparison.Ordinal), "pin-missing");
            window.GetNamedControl<TextBox>("OutputDirectory").Text = directory;
            Require(window.GetNamedControl<ComboBox>("Artifact").SelectedIndex >= 0 &&
                window.GetNamedControl<ComboBox>("Artifact").SelectionBoxItem is not null, "artifact-label-missing");
            Require(window.GetNamedControl<ComboBox>("Experience").SelectedItem is not null &&
                workspace.SelectedExperience?.Id == "configure" &&
                !string.IsNullOrWhiteSpace(workspace.SelectedExperience.Title), "neutral-experience-missing");
            Pass();

            _currentCheck = "D02-typed-controls";
            _currentStep = "scalar-inputs";
            Input<TextBox>(window, "Service name").Text = "Native service";
            Input<TextBox>(window, "Owner name").Text = "Synthetic private owner";
            Input<TextBox>(window, "Owner age").Text = "23";
            var hosts = (ListFieldViewModel)workspace.Fields.Single(field => field.Id == "hosts");
            _currentStep = "list-add";
            Click(CommandButton(window, hosts.AddItemCommand));
            await FlushAsync();
            _currentStep = "list-input";
            Input<TextBox>(window, hosts.Items.Single()).Text = "native.example";
            await FlushAsync();
            var values = workspace.Form!.Snapshot().Values;
            Require(values["owner"].GetProperty("age").GetInt32() == 23 &&
                values["hosts"][0].GetString() == "native.example" &&
                Input<TextBox>(window, "Owner name").PasswordChar != '\0', "typed-answer");
            _currentStep = "list-remove";
            Click(CommandButton(window, hosts.Items.Single().RemoveCommand));
            await FlushAsync();
            Require(workspace.Form.Snapshot().Values["hosts"].GetArrayLength() == 0, "list-removal");
            Click(CommandButton(window, hosts.AddItemCommand));
            await FlushAsync();
            Input<TextBox>(window, hosts.Items.Single()).Text = "native.example";
            await FlushAsync();
            Pass();

            _currentCheck = "D03-activation-disclosure";
            Require(!IsEditorVisible(window, "Port") && !IsEditorVisible(window, "Advanced note"), "inactive-disclosure");
            SelectChoice(window, shell, "mode", "remote");
            await FlushAsync();
            Require(Editor(window, "Port").IsVisible, "activation");
            long revision = workspace.Form.InputRevision;
            window.GetNamedControl<CheckBox>("OptionalEditors").IsChecked = true;
            await FlushAsync();
            Require(Editor(window, "Advanced note").IsVisible && Editor(window, "Owner").IsVisible &&
                workspace.Form.InputRevision == revision, "expansion-mutates-input");
            window.GetNamedControl<CheckBox>("OptionalEditors").IsChecked = false;
            await FlushAsync();
            Require(!IsEditorVisible(window, "Advanced note"), "authored-detail-not-restored");
            window.GetNamedControl<CheckBox>("OptionalEditors").IsChecked = true;
            await FlushAsync();
            Input<TextBox>(window, "Advanced note").Text = "too-long-note";
            await FlushAsync();
            window.GetNamedControl<CheckBox>("OptionalEditors").IsChecked = false;
            await FlushAsync();
            Require(Editor(window, "Advanced note").IsVisible && Editor(window, "Owner").IsVisible, "invalid-hidden");
            Input<TextBox>(window, "Advanced note").Text = "";
            SelectChoice(window, shell, "mode", "local");
            await FlushAsync();
            await CaptureAsync(window, "edited");
            Pass();

            _currentCheck = "D04-json-yaml-dotenv-import";
            Review(window, shell, import: true);
            window.GetNamedControl<ComboBox>("ImportPurpose").SelectedIndex = 1;
            foreach (var (id, file, content) in new[]
            {
                ("config", "import.json", "{\"name\":\"JSON imported\"}"),
                ("yaml", "import.yaml", "name: YAML imported\n"),
                ("environment", "import.env", "NAME=dotenv-imported\n")
            })
            {
                _currentStep = "import-" + id;
                Review(window, shell, import: true);
                window.GetNamedControl<ComboBox>("Artifact").SelectedItem = shell.Publisher.Artifacts.Single(item => item.Id == id);
                shell.Navigation.ConfigureCommand.Execute(null);
                Click(CommandButton(window, workspace.Fields.Single(field => field.Id == "name").ResetCommand));
                await FlushAsync();
                string path = Path.Combine(directory, file);
                await File.WriteAllTextAsync(path, content);
                window.GetNamedControl<TextBox>("ExistingFilePath").Text = path;
                await ActionAsync(window, shell, "Import", () => shell.Status.StartsWith("import-", StringComparison.Ordinal));
                Require(shell.Status == "import-ready", shell.Status switch
                {
                    "import-unreadable" => "import-unreadable",
                    "import-field-invalid" => "import-field-invalid",
                    "import-unsupported" => "import-unsupported",
                    "inputs-changed" => "import-status-invalidated",
                    "validation-invalid" => "import-invalidated-validation",
                    _ => "inverse-import-rejected"
                });
                Require(workspace.Form!.Snapshot().Values["name"].GetString() == (id switch
                { "config" => "JSON imported", "yaml" => "YAML imported", _ => "dotenv-imported" }), "inverse-import");
            }
            Select(window, shell, "config");
            Pass();

            _currentCheck = "D05-current-composable-preparation";
            _currentStep = "invalid-nested-input";
            Input<TextBox>(window, "Owner age").Text = "invalid-integer";
            await FlushAsync();
            await ActionAsync(window, shell, "PrepareSelection", () => shell.Status.StartsWith("validation-invalid", StringComparison.Ordinal));
            Require(!publisher.CanSave && workspace.Form!.HasInputErrors, "nested-invalid-omitted");
            _currentStep = "correct-nested-input";
            Input<TextBox>(window, "Owner age").Text = "23";
            await FlushAsync();
            _currentStep = "clear-export-selection";
            foreach (string id in publisher.SelectedArtifactIds.ToArray()) Include(window, id, false);
            await FlushAsync();
            _currentStep = "prepare-empty-selection";
            Require(!window.GetNamedControl<Button>("PrepareSelection").IsEffectivelyEnabled &&
                !publisher.CanPrepare && !publisher.CanSaveSelection, "empty-selection-ready");
            _currentStep = "inactive-output-excluded";
            Include(window, "remote", true);
            await FlushAsync();
            Require(await publisher.PrepareSelectionAsync() != DesktopPreparationOutcome.Prepared &&
                !publisher.CanSaveSelection, "inactive-selection-ready");
            Select(window, shell, "config");
            _currentStep = "prepare-current-selection";
            await PrepareAsync(window, shell);
            Require(publisher.Preparation!.DefinitionDigest == workspace.Form!.Definition.Digest, "preparation-pin");
            Pass();

            _currentCheck = "D06-protected-single-conflict";
            await ActionAsync(window, shell, "SaveSelection", () => !shell.IsSaving && shell.Status.StartsWith("save-", StringComparison.Ordinal));
            string target = Path.Combine(directory, "service.json");
            await VerifyJsonAsync(target, """{"name":"dotenv-imported","owner":{"name":"Synthetic private owner","age":23},"hosts":["native.example"]}""");
            Require(OperatingSystem.IsLinux() && File.GetUnixFileMode(target) == (UnixFileMode.UserRead | UnixFileMode.UserWrite), "single-permissions");
            byte[] original = await File.ReadAllBytesAsync(target);
            await PrepareAsync(window, shell);
            await ActionAsync(window, shell, "SaveSelection", () => !shell.IsSaving && shell.Status.StartsWith("save-", StringComparison.Ordinal));
            Require((await File.ReadAllBytesAsync(target)).AsSpan().SequenceEqual(original) && !publisher.CanSave, "overwrite-or-retained-save");
            Pass();

            _currentCheck = "D07-bundle-preflight-recovery";
            Include(window, "environment", true);
            await PrepareBundleAsync(window, shell);
            Require(publisher.CanSaveBundle, "bundle-not-ready");
            await ActionAsync(window, shell, "SaveSelection", () => !shell.IsSaving && shell.Status.StartsWith("bundle-save-", StringComparison.Ordinal));
            Require(!File.Exists(Path.Combine(directory, "service.env")) && (await File.ReadAllBytesAsync(target)).AsSpan().SequenceEqual(original), "bundle-partial-preflight");
            File.Delete(target);
            await PrepareBundleAsync(window, shell);
            await ActionAsync(window, shell, "SaveSelection", () => !shell.IsSaving && shell.Status.StartsWith("bundle-save-", StringComparison.Ordinal));
            await VerifyJsonAsync(target, """{"name":"dotenv-imported","owner":{"name":"Synthetic private owner","age":23},"hosts":["native.example"]}""");
            Require(await File.ReadAllTextAsync(Path.Combine(directory, "service.env")) == "NAME=dotenv-imported\n", "bundle-environment-bytes");
            Require(OperatingSystem.IsLinux() && File.GetUnixFileMode(Path.Combine(directory, "service.env")) == (UnixFileMode.UserRead | UnixFileMode.UserWrite) && !publisher.CanSaveBundle, "bundle-permissions");
            await LargePartialBundleAsync();
            Pass();

            _currentCheck = "D09-edit-cancel-stale-import";
            Select(window, shell, "config");
            await PrepareAsync(window, shell);
            Input<TextBox>(window, "Service name").Text = "Current edit";
            await FlushAsync();
            Require(!publisher.CanSave && !publisher.CanSaveBundle, "edit-stale-preparation");
            var delayed = new TaskCompletionSource<ReadOnlyMemory<byte>>(TaskCreationOptions.RunContinuationsAsynchronously);
            Task<bool> pending = shell.ImportReview.ImportAsync(_ => delayed.Task);
            Input<TextBox>(window, "Service name").Text = "Later edit";
            await FlushAsync();
            delayed.SetResult("{\"name\":\"Stale import\"}"u8.ToArray());
            Require(!await pending.WaitAsync(TimeSpan.FromSeconds(10)) && workspace.Form!.Snapshot().Values["name"].GetString() == "Later edit", "stale-import");
            await PrepareAsync(window, shell);
            Click(window.GetNamedControl<Button>("Cancel"));
            Require(!publisher.CanSave && !publisher.CanSaveBundle && !shell.IsSaving, "cancel-stale-preparation");
            Pass();

            _currentCheck = "D10-value-free-feedback-retry";
            window.GetNamedControl<TextBox>("FileName").Text = "../unsafe.json";
            await PrepareAsync(window, shell);
            await ActionAsync(window, shell, "SaveSelection", () => !shell.IsSaving && shell.Status.StartsWith("save-", StringComparison.Ordinal));
            Require(!publisher.CanSave && !shell.IsSaving, "failure-not-consumed");
            foreach (Control control in window.GetLogicalDescendants().OfType<Control>())
            {
                RequireSafe(AutomationProperties.GetName(control), directory);
                RequireSafe(AutomationProperties.GetHelpText(control), directory);
            }
            RequireSafe(window.GetNamedControl<TextBlock>("Status").Text, directory);
            RequireSafe(window.GetNamedControl<TextBlock>("StatusHelp").Text, directory);
            window.GetNamedControl<TextBox>("FileName").Text = "retry.json";
            await PrepareAsync(window, shell);
            Require(publisher.CanSave, "retry-unavailable");
            Click(window.GetNamedControl<Button>("Cancel"));
            Pass();

            _currentCheck = "D18-shared-editing-presence-localized-actions";
            await SharedEditingAsync();
            Pass();

            _currentCheck = "D19-typed-map-variant-null-reset-locale";
            await StructuredEditingAsync(window, shell, directory);
            Pass();

            _currentCheck = "D20-read-only-import-comparison";
            await ImportComparisonAsync(window, shell, directory, definitionPath);
            Pass();

            _currentCheck = "D21-secret-generation-validation";
            await SecretControlsAsync(window, shell);
            Pass();

            _currentCheck = "D22-deployment-context-destinations";
            await DeploymentContextAsync(window, shell, directory);
            Pass();

            _currentCheck = "D23-root-map-literal-controls";
            await RootMapAsync(window, shell, directory);
            Pass();

            _currentCheck = "D24-root-variant-retirement";
            await RootVariantAsync(window, shell);
            Pass();

            await RedesignAsync(window, shell, directory);

            for (int count = 1; count <= 3; count++)
            {
                _currentCheck = count switch { 1 => "D12-event-one-tenant", 2 => "D12-event-two-tenants", _ => "D12-event-three-tenants" };
                await EventAsync(window, shell, directory, count);
                Pass();
            }

            _currentCheck = "D14-physical-settlement-receipt";
            await PhysicalSettlementAsync();
            Pass();

            await CloseLifecycleAsync();

            _currentCheck = "D09-close-disposes-workspace";
            await PrepareAsync(window, shell);
            var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            window.Closed += (_, _) => closed.TrySetResult();
            window.Close();
            await closed.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Require(!publisher.CanSave && !publisher.CanSaveBundle, "close-retains-preparation");
            Pass();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static async Task RedesignAsync(MainWindow window, DesktopShellViewModel shell, string directory)
    {
        _currentStep = "redesign";
        _currentCheck = "D25-appearance-preference-contrast";
        Application application = Application.Current!;
        ThemeVariant? originalTheme = application.RequestedThemeVariant;
        Action<PlatformColorValues>? notify = null;
        using (var theme = new DesktopTheme(application, changed =>
        {
            notify = changed;
            return () => notify = null;
        }))
        using (var appearanceShell = PublicationShell((_, _, _) =>
            Task.FromResult(ProtectedArtifactCommitResult.Installed(true)), theme: theme))
        {
            var appearanceWindow = new MainWindow { DataContext = appearanceShell };
            try
            {
                appearanceWindow.Show();
                Require(appearanceShell.Loader.LoadDefinition(NativeDefinition()), "appearance-load");
                appearanceShell.Workspace.SelectedLocale = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
                await appearanceShell.Publisher.PrepareSelectionAsync();
                var preparation = appearanceShell.Publisher.Preparation;
                long revision = appearanceShell.Workspace.Form!.InputRevision;
                await FlushAsync();
                var selector = appearanceWindow.GetNamedControl<ComboBox>("Appearance");
                Require(selector.ItemCount == 3 && selector.Focus(), "appearance-selector");
                foreach (int index in new[] { 1, 2 })
                {
                    selector.SelectedIndex = index;
                    await FlushAsync();
                    Require(application.RequestedThemeVariant == (index == 1 ? ThemeVariant.Light : ThemeVariant.Dark),
                        "appearance-preference");
                    Require(selector.IsFocused && !string.IsNullOrWhiteSpace(
                        ControlAutomationPeer.CreatePeerForElement(selector)?.GetName()), "appearance-semantics");
                    RequireContrast(appearanceWindow);
                }
                selector.IsDropDownOpen = true;
                await FrameAsync(appearanceWindow);
                Require(selector.IsDropDownOpen && appearanceWindow.OpenedPopups.Count > 0,
                    "appearance-popup-missing");
                await CaptureAsync(appearanceWindow, "appearance-popup", appearanceWindow.OpenedPopups[^1]);
                selector.IsDropDownOpen = false;
                notify!(new PlatformColorValues { ContrastPreference = ColorContrastPreference.High });
                selector.SelectedIndex = 1;
                await FlushAsync();
                Require(appearanceWindow.ActualThemeVariant == DesktopTheme.HighContrast &&
                    theme.Preference == DesktopAppearance.Light, "appearance-contrast-precedence");
                RequireContrast(appearanceWindow);
                await CaptureAsync(appearanceWindow, "appearance-contrast");
                notify!(new PlatformColorValues { ContrastPreference = ColorContrastPreference.NoPreference });
                await FlushAsync();
                Require(appearanceWindow.ActualThemeVariant == ThemeVariant.Light &&
                    revision == appearanceShell.Workspace.Form.InputRevision && preparation is not null &&
                    ReferenceEquals(preparation, appearanceShell.Publisher.Preparation), "appearance-restoration");
            }
            finally { appearanceWindow.Close(); }
        }
        application.RequestedThemeVariant = originalTheme;
        Pass();

        _currentCheck = "D26-guided-root-correction";
        Require(shell.Loader.LoadDefinition(RootDefinition("Conformance.RootVariant")), "guided-root-load");
        shell.Workspace.SelectedLocale = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        await FlushAsync();
        var root = (RootFieldViewModel)shell.Workspace.Fields.Single();
        var variant = (VariantFieldViewModel)root.Editor;
        Input<ComboBox>(window, variant.Title).SelectedIndex = 1;
        await FlushAsync();
        Input<TextBox>(window, variant.Children.Single(child => child.Id == "endpoint")).Text = "synthetic-queue";
        await FlushAsync();
        Review(window, shell);
        Require(!shell.Workspace.Validate(), "guided-root-invalid");
        await FlushAsync();
        var token = variant.Children.Single(child => child.Id == "token");
        TextBox correction = Input<TextBox>(window, token);
        Require(shell.Navigation.IsConfigure, "guided-root-correction-stage");
        Require(correction.IsFocused, "guided-root-correction-focus");
        Require(correction.PasswordChar != '\0', "guided-root-correction-mask");
        Require(root.HasError && !shell.Publisher.CanSaveSelection, "guided-root-correction-readiness");
        Require(!window.GetNamedControl<Button>("SaveSelection").IsEffectivelyVisible, "hidden-review-action");
        await CaptureAsync(window, "guided-root-error");
        Pass();

        _currentCheck = "D27-responsive-scale-focus";
        Require(shell.Loader.LoadDefinition(NativeDefinition()), "responsive-load");
        shell.Workspace.SelectedLocale = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        await FlushAsync();
        TextBox editor = Input<TextBox>(window, "Service name");
        long inputRevision = shell.Workspace.Form!.InputRevision;
        double editorFontSize = editor.FontSize;
        var textSizes = window.GetLogicalDescendants().OfType<TextBlock>()
            .Where(control => control.IsEffectivelyVisible)
            .Select(control => (control, size: control.FontSize)).ToArray();
        double width = window.Width, height = window.Height;
        try
        {
            window.Resources["ConfiguratorBodyFontSize"] = 28d;
            window.Resources["ConfiguratorHeadingFontSize"] = 48d;
            window.Resources["ConfiguratorTitleFontSize"] = 52d;
            foreach (var size in new[] { new Size(600, 540), new Size(960, 800), new Size(1440, 1000) })
            {
                await ResizeAsync(window, size);
                Require(ReferenceEquals(editor, Input<TextBox>(window, "Service name")) &&
                    editor.FontSize == editorFontSize * 2, "responsive-editor-text-scale");
                Require(textSizes.Length > 0 && textSizes.All(item =>
                    item.control.FontSize == item.size * 2), "responsive-all-text-scale");
                Require(editor.Focus(), "responsive-focus");
                editor.BringIntoView();
                await FrameAsync(window);
                var scroll = window.GetNamedControl<ScrollViewer>("WorkspaceScroll");
                Point location = editor.TranslatePoint(default, scroll) ??
                    throw new QualificationException("responsive-editor-detached");
                Require(editor.IsFocused && editor.IsEffectivelyVisible && editor.Bounds.Width > 0 &&
                    location.X >= 0 && location.Y >= 0 &&
                    location.X + editor.Bounds.Width <= scroll.Bounds.Width + 1 &&
                    location.Y + editor.Bounds.Height <= scroll.Bounds.Height + 1 &&
                    shell.Workspace.Form.InputRevision == inputRevision, "responsive-focus-visibility");
            }
            await CaptureAsync(window, "responsive-text-200");
        }
        finally
        {
            window.Resources["ConfiguratorBodyFontSize"] = 14d;
            window.Resources["ConfiguratorHeadingFontSize"] = 24d;
            window.Resources["ConfiguratorTitleFontSize"] = 26d;
            await ResizeAsync(window, new Size(width, height));
        }
        Pass();

        _currentCheck = "D28-collection-page-identity";
        JsonNode paging = JsonNode.Parse(NativeSource())!;
        paging["identity"]!["familyId"] = "3ad6ae53-415d-46fa-b6d1-c51939fd567a";
        paging["configuration"] = JsonNode.Parse("""
            {"type":"object","additionalProperties":false,"x-configurator":{"sensitivity":"public"},
             "properties":{"rows":{"type":"array","title":"Rows",
              "items":{"type":"integer","x-configurator":{"sensitivity":"public"}},
              "x-configurator":{"sensitivity":"public","detail":"basic"}}}}
            """);
        paging["configuration"]!["properties"]!["rows"]!["x-configurator"]!["initialValue"] =
            new JsonArray(Enumerable.Range(0, 51).Select(value => (JsonNode?)JsonValue.Create(value)).ToArray());
        paging["outputs"] = JsonNode.Parse("""{"config.json":{"id":"config","format":"json"}}""");
        Require(shell.Loader.LoadDefinition(System.Text.Encoding.UTF8.GetBytes(paging.ToJsonString())), "paging-load");
        shell.Workspace.SelectedLocale = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        await FlushAsync();
        var list = (ListFieldViewModel)shell.Workspace.Fields.Single();
        var survivor = list.Items[50];
        string identity = shell.Workspace.Form!.GetEditorIdentity(["rows", "50"]);
        Require(window.GetLogicalDescendants().OfType<TextBox>().Count(control =>
            control.IsEffectivelyVisible && control.DataContext is FieldViewModel field && list.Items.Contains(field)) == 25,
            "paging-initial-mount");
        survivor.RequestFocus();
        await FrameAsync(window);
        TextBox draft = Input<TextBox>(window, survivor);
        Require(draft.IsFocused && window.GetLogicalDescendants().OfType<TextBox>().Count(control =>
            control.IsEffectivelyVisible && control.DataContext is FieldViewModel field && list.Items.Contains(field)) == 1,
            "paging-off-page-focus");
        draft.Text = "unfinished-number";
        list.RemoveItemCommand.Execute(list.Items[0]);
        await FrameAsync(window);
        TextBox remounted = Input<TextBox>(window, survivor);
        Require(ReferenceEquals(list.Items[49], survivor) &&
            shell.Workspace.Form.GetEditorIdentity(["rows", "49"]) == identity &&
            remounted.Text == "unfinished-number" && survivor.HasError &&
            window.GetLogicalDescendants().OfType<TextBox>().Count(control =>
                control.IsEffectivelyVisible && control.DataContext is FieldViewModel field && list.Items.Contains(field)) == 25,
            "paging-survivor-identity");
        await CaptureAsync(window, "collection-page-identity");
        Pass();

        _currentCheck = "D29-private-metadata-discovery";
        JsonNode discovery = JsonNode.Parse(EditingSource())!;
        discovery["identity"]!["familyId"] = "94ad4245-43bc-422c-953a-14bf66047ab8";
        discovery["configuration"]!["properties"]!["named"]!["x-configurator"]!["sensitivity"] = "sensitive";
        Require(shell.Loader.LoadDefinition(System.Text.Encoding.UTF8.GetBytes(discovery.ToJsonString())), "discovery-load:" + shell.Status);
        shell.Workspace.SelectedLocale = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        shell.Workspace.Form!.SetValue(["named"], JsonSerializer.SerializeToElement(
            new Dictionary<string, object>
            {
                ["caption"] = "synthetic-private-caption-9d02",
                ["synthetic-private-key-9d02"] = 4
            }));
        await FlushAsync();
        Require(await shell.Publisher.PrepareSelectionAsync() == DesktopPreparationOutcome.Prepared, "discovery-prepare");
        var retained = shell.Publisher.Preparation;
        inputRevision = shell.Workspace.Form!.InputRevision;
        shell.Navigation.ConfigureCommand.Execute(null);
        await FlushAsync();
        TextBox search = window.GetNamedControl<TextBox>("ConfigurationSearch");
        ListBox results = window.GetNamedControl<ListBox>("DiscoveryResults");
        foreach (string query in new[] { "synthetic-private-caption-9d02", "synthetic-private-key-9d02" })
        {
            search.Text = query;
            await FrameAsync(window);
            Require(results.ItemCount == 0 && shell.Navigation.DiscoveryTotal == 0 &&
                inputRevision == shell.Workspace.Form.InputRevision &&
                ReferenceEquals(retained, shell.Publisher.Preparation), "discovery-private-match");
        }
        search.Text = "Named values";
        await FrameAsync(window);
        Require(results.ItemCount > 0 && results.IsEffectivelyVisible &&
            !string.IsNullOrWhiteSpace(ControlAutomationPeer.CreatePeerForElement(search)?.GetName()),
            "discovery-public-metadata");
        await CaptureAsync(window, "metadata-discovery");
        Pass();

        _currentCheck = "D30-selected-output-review";
        Require(shell.Loader.LoadDefinition(NativeDefinition()), "selected-output-load");
        shell.Workspace.SelectedLocale = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        Select(window, shell, "config");
        Include(window, "environment", true);
        string outputDirectory = Directory.CreateDirectory(Path.Combine(directory, "selected-output")).FullName;
        shell.Publisher.OutputDirectory = outputDirectory;
        shell.Publisher.FileName = "qualification-ignored.json";
        await FlushAsync();
        Require(!window.GetNamedControl<TextBox>("FileName").IsEffectivelyVisible &&
            !window.GetNamedControl<Button>("SaveSelection").IsEffectivelyEnabled, "selected-output-review-state");
        CheckBox[] selections = window.GetLogicalDescendants().OfType<CheckBox>()
            .Where(control => control.DataContext is ArtifactSelectionViewModel { IsSelected: true }).ToArray();
        Require(selections.Length == 2 && selections.All(control => control.IsEffectivelyVisible &&
            !string.IsNullOrWhiteSpace(ControlAutomationPeer.CreatePeerForElement(control)?.GetName())),
            "selected-output-semantics");
        await PrepareBundleAsync(window, shell);
        Require(shell.Publisher.CanSaveSelection, "selected-output-prepared");
        window.GetNamedControl<Expander>("OptionalImport").IsExpanded = true;
        await CaptureAsync(window, "selected-output-review");
        var popup = new Popup
        {
            PlacementTarget = window.GetNamedControl<Button>("PrepareSelection"),
            Child = new TextBox { Text = "Synthetic popup editor" }
        };
        var popupOwner = (StackPanel)window.GetNamedControl<ScrollViewer>("WorkspaceScroll").Content!;
        popupOwner.Children.Add(popup);
        try
        {
            popup.Open();
            await FrameAsync(window);
            Require(popup.IsOpen, "selected-output-popup-open");
            await CaptureAsync(window, "selected-output-popup", popup);
            window.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape });
            await FrameAsync(window);
            Require(!popup.IsOpen && shell.Publisher.CanSaveSelection, "selected-output-popup-escape");
        }
        finally
        {
            popup.Close();
            popupOwner.Children.Remove(popup);
        }
        await ActionAsync(window, shell, "SaveSelection", () => !shell.IsBusy && !shell.Publisher.CanSaveSelection);
        Require(shell.Publisher.PublicationHistory[^1] is { IsSettled: true } receipt &&
            receipt.Outcomes.Count == 2 && receipt.Outcomes.All(outcome => outcome.Result is { Succeeded: true }) &&
            File.Exists(Path.Combine(outputDirectory, "service.json")) &&
            File.Exists(Path.Combine(outputDirectory, "service.env")) &&
            !File.Exists(Path.Combine(outputDirectory, "qualification-ignored.json")) &&
            window.GetNamedControl<TextBlock>("PublicationReceipt").IsEffectivelyVisible &&
            !window.GetNamedControl<Button>("SaveSelection").IsEffectivelyEnabled, "selected-output-save-result");
        await CaptureAsync(window, "selected-output-save-result");
        await PrepareBundleAsync(window, shell);
        Include(window, "environment", false);
        await FlushAsync();
        Require(!shell.Publisher.CanSaveSelection &&
            window.GetNamedControl<TextBox>("FileName").IsEffectivelyVisible, "selected-output-retirement");
        Pass();
    }

    private static void RequireContrast(MainWindow window)
    {
        Color Resolve(string role)
        {
            Require(window.TryFindResource("Configurator" + role + "Brush", window.ActualThemeVariant,
                out object? value) && value is SolidColorBrush, "contrast-resource");
            return ((SolidColorBrush)value!).Color;
        }
        static double Channel(byte channel)
        {
            double value = channel / 255.0;
            return value <= .04045 ? value / 12.92 : Math.Pow((value + .055) / 1.055, 2.4);
        }
        static double Luminance(Color color) =>
            .2126 * Channel(color.R) + .7152 * Channel(color.G) + .0722 * Channel(color.B);
        foreach (string background in new[] { "Canvas", "Surface", "Input" })
        foreach (string foreground in new[] { "Text", "SecondaryText", "Error", "Focus", "Border" })
        {
            double a = Luminance(Resolve(background)), b = Luminance(Resolve(foreground));
            Require((Math.Max(a, b) + .05) / (Math.Min(a, b) + .05) >=
                (foreground is "Focus" or "Border" ? 3 : 4.5), "contrast-ratio");
        }
    }

    private static async Task FrameAsync(MainWindow window)
    {
        window.UpdateLayout();
        await FlushAsync();
        var rendered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        window.RequestAnimationFrame(_ => rendered.TrySetResult());
        await rendered.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }

    private static async Task ResizeAsync(MainWindow window, Size size)
    {
        var resized = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void Changed(object? sender, WindowResizedEventArgs args)
        {
            if (args.ClientSize == size) resized.TrySetResult();
        }
        window.Resized += Changed;
        try
        {
            window.Width = size.Width;
            window.Height = size.Height;
            if (window.ClientSize == size) resized.TrySetResult();
            await resized.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await FrameAsync(window);
        }
        finally { window.Resized -= Changed; }
    }

    private static byte[] RootDefinition(string resource)
    {
        using Stream stream = typeof(Program).Assembly.GetManifestResourceStream(resource)
            ?? throw new QualificationException("root-fixture-missing");
        using var bytes = new MemoryStream();
        stream.CopyTo(bytes);
        JsonNode source = JsonNode.Parse(System.Text.Encoding.UTF8.GetString(bytes.ToArray()))!;
        source["identity"]!["familyId"] = resource switch
        {
            "Conformance.RootMap" => "fbb85d13-d9b1-4af9-9a96-94885bbab587",
            "Conformance.RootVariant" => "50171829-21dd-436e-af92-d0682a7fc040",
            _ => throw new QualificationException("root-fixture-unknown")
        };
        source["configuration"]!["title"] = "Root settings";
        source["localization"] = JsonNode.Parse("""
            {"defaultLocale":"en","translations":{"ar":{"direction":"rtl",
             "overrides":{"/configuration/title":"إعدادات الجذر"}}}}
            """);
        return DefinitionSourceCodec.ParseJson(System.Text.Encoding.UTF8.GetBytes(source.ToJsonString())).CanonicalBytes.ToArray();
    }

    private static async Task RootMapAsync(MainWindow window, DesktopShellViewModel shell, string directory)
    {
        Require(shell.Loader.LoadDefinition(RootDefinition("Conformance.RootMap")), "root-map-load");
        shell.Workspace.SelectedLocale = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        await FlushAsync();
        ComboBox purpose = window.GetNamedControl<ComboBox>("ImportPurpose");
        Require(purpose.SelectedIndex == shell.ImportReview.ImportPurposeIndex &&
            purpose.SelectedIndex == 0 && purpose.SelectionBoxItem is not null, "root-import-purpose-localized-selection");
        Require(ControlAutomationPeer.CreatePeerForElement((ComboBoxItem)purpose.SelectedItem!)?.GetName() ==
            shell.ImportReview.ImportPurposes[0],
            "root-import-purpose-localized-caption");
        var root = (RootFieldViewModel)shell.Workspace.Fields.Single();
        var map = (MapFieldViewModel)root.Editor;
        TextBox key = Input<TextBox>(window, map.NewKeyLabel);
        Require(key.Focus() && key.IsFocused, "root-map-key-focus");
        key.Text = "a/b";
        Click(CommandButton(window, map.AddEntryCommand));
        await FlushAsync();
        var first = (EnumFieldViewModel)map.Entries.Single();
        Input<ComboBox>(window, first.Name).SelectedIndex = 2;
        key.Text = "A";
        Click(CommandButton(window, map.AddEntryCommand));
        await FlushAsync();
        var second = (EnumFieldViewModel)map.Entries.Single(entry => entry.Id == "A");
        Input<ComboBox>(window, second.Name).SelectedIndex = 3;
        Click(CommandButton(window, first.SetNullCommand));
        await FlushAsync();
        Require(ReferenceEquals(map.Entries.Single(entry => entry.Id == "a/b"), first), "root-map-stable-entry");
        Require(shell.Workspace.Form!.Snapshot().Values["a/b"].ValueKind == JsonValueKind.Null &&
            shell.Workspace.Form.Snapshot().Values["A"].GetInt32() == 30, "root-map-typed-values");
        shell.Publisher.ArtifactId = "config";
        shell.Publisher.OutputDirectory = directory;
        shell.Publisher.FileName = "native-root-map.json";
        Require(await shell.Publisher.PrepareAsync() == DesktopPreparationOutcome.Prepared, "root-map-prepare");
        ProtectedArtifactCommitResult saved = await shell.Publisher.SaveAsync();
        Require(saved.VerificationSucceeded &&
            await File.ReadAllTextAsync(Path.Combine(directory, "native-root-map.json")) == """{"A":30,"a/b":null}""",
            "root-map-protected-output");
        await CaptureAsync(window, "root-map-ready");
        Click(CommandButton(window, map.ResetCommand));
        await FlushAsync();
        Require(shell.Workspace.Form.Snapshot().Values.Count == 0, "root-map-reset");
        window.GetNamedControl<ComboBox>("ImportPurpose").SelectedIndex = 0;
        window.GetNamedControl<TextBox>("ExistingFilePath").Text = Path.Combine(directory, "native-root-map.json");
        await ActionAsync(window, shell, "Import", () => shell.ImportReview.HasImportComparison && !shell.IsBusy);
        Require(shell.ImportReview.CanFillImportFields, "root-map-import-adoption-unavailable");
        Click(CommandButton(window, shell.ImportReview.FillImportFieldsCommand));
        await shell.ImportReview.FillImportFieldsCommand.ExecutionTask!.WaitAsync(TimeSpan.FromSeconds(10));
        await FlushAsync();
        Require(shell.Workspace.Form.Snapshot().Values["a/b"].ValueKind == JsonValueKind.Null &&
            shell.Workspace.Form.Snapshot().Values["A"].GetInt32() == 30 &&
            map.Entries.Count == 2 && !first.ClearCommand.CanExecute(null), "root-map-import-projection");
        await CaptureAsync(window, "root-map-imported");
    }

    private static async Task RootVariantAsync(MainWindow window, DesktopShellViewModel shell)
    {
        Require(shell.Loader.LoadDefinition(RootDefinition("Conformance.RootVariant")), "root-variant-load");
        shell.Workspace.SelectedLocale = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        await FlushAsync();
        var root = (RootFieldViewModel)shell.Workspace.Fields.Single();
        var variant = (VariantFieldViewModel)root.Editor;
        ComboBox selector = Input<ComboBox>(window, variant.Title);
        Require(selector.Focus() && selector.IsFocused, "root-variant-selector-focus");
        selector.SelectedIndex = 0;
        await FlushAsync();
        var directory = (StringFieldViewModel)variant.Children.Single(child => child.Id == "directory");
        Input<TextBox>(window, directory.Name).Text = "native-spool";
        selector.SelectedIndex = 1;
        await FlushAsync();
        long revision = shell.Workspace.Form!.InputRevision;
        directory.Text = "retired-edit";
        Require(shell.Workspace.Form.InputRevision == revision &&
            !shell.Workspace.Form.Snapshot().Values.ContainsKey("directory"), "root-variant-retired-editor");
        var token = (StringFieldViewModel)variant.Children.Single(child => child.Id == "token");
        Require(Input<TextBox>(window, token.Name).PasswordChar != '\0', "root-variant-secret-mask");
        Require(!shell.Workspace.Validate() && root.HasError, "root-variant-required-correction");
        await CaptureAsync(window, "root-variant-correction");
        selector.SelectedIndex = 0;
        await FlushAsync();
        var fresh = (StringFieldViewModel)variant.Children.Single(child => child.Id == "directory");
        Require(!ReferenceEquals(directory, fresh) && fresh.Text.Length == 0 &&
            !shell.Workspace.Form.Snapshot().Values.ContainsKey("token"), "root-variant-fresh-branch");
        fresh.RequestFocus();
        await FlushAsync();
        Require(Input<TextBox>(window, fresh.Name).IsFocused, "root-variant-fresh-focus");
        await CaptureAsync(window, "root-variant-fresh");
    }

    private static byte[] NativeDefinition() => DefinitionSourceCodec.ParseJson(NativeSource()).CanonicalBytes.ToArray();

    private static byte[] NativeSource() => """
        {
          "$schema":"urn:islamu:configurator:authoring:v0.0.1",
          "identity":{"familyId":"8508c82b-2f4a-42b5-af54-0e48c8c89a6e","revision":1},
          "project":{"id":"example.native-qualification","name":"Native qualification fixture","publisher":"example"},
          "applicationVersions":["1.0"],"entryPoints":["setup"],
          "localization":{"defaultLocale":"en","translations":{"ar":{"direction":"rtl","overrides":{}}}},
          "configuration":{"type":"object","additionalProperties":false,"required":["name"],
            "x-configurator":{"sensitivity":"public"},"properties":{
              "name":{"type":"string","title":"Service name","default":"Descriptive only",
                "x-configurator":{"initialValue":"Example service","sensitivity":"public","detail":"basic"}},
              "mode":{"type":"string","title":"Mode","enum":["local","remote"],
                "description":"Remote mode enables the port.",
                "x-configurator":{"initialValue":"local","sensitivity":"public","detail":"standard"}},
              "port":{"type":"integer","title":"Port","minimum":1,"maximum":65535,
                "x-configurator":{"initialValue":443,"sensitivity":"public","detail":"standard",
                  "applicableWhen":{"op":"equals","reference":{"scope":"root","path":"/mode"},"value":"remote"}}},
              "owner":{"type":"object","title":"Owner","additionalProperties":false,
                "x-configurator":{"sensitivity":"public","detail":"standard"},"properties":{
                  "name":{"type":"string","title":"Owner name","x-configurator":{"sensitivity":"sensitive","detail":"standard"}},
                  "age":{"type":"integer","title":"Owner age","x-configurator":{"sensitivity":"public","detail":"standard"}}}},
              "hosts":{"type":"array","title":"Hosts","maxItems":3,
                "items":{"type":"string","x-configurator":{"sensitivity":"public"}},
                "x-configurator":{"sensitivity":"public","detail":"standard"}},
              "note":{"type":"string","title":"Advanced note","maxLength":8,
                "x-configurator":{"sensitivity":"public","detail":"advanced"}}
            },
            "if":{"properties":{"mode":{"const":"remote"}},"required":["mode"]},"then":{"required":["port"]}},
          "outputs":{
            "service.json":{"id":"config","format":"json","targetUpdateMode":"create","restartRequirement":"process",
              "bindings":{"/name":{"scope":"root","path":"/name"},"/owner":{"scope":"root","path":"/owner"},
                "/hosts":{"scope":"root","path":"/hosts"}}},
            "service.env":{"id":"environment","format":"dotenv","surface":"process-environment","environmentDialect":"docker-run","targetUpdateMode":"create","restartRequirement":"process",
              "bindings":{"NAME":{"scope":"root","path":"/name"}}},
            "service.yaml":{"id":"yaml","format":"yaml","targetUpdateMode":"replace","restartRequirement":"none",
              "bindings":{"/name":{"scope":"root","path":"/name"}}},
            "remote.json":{"id":"remote","format":"json","targetUpdateMode":"create","restartRequirement":"none",
              "projection":{"op":"object","when":{"op":"equals","reference":{"scope":"root","path":"/mode"},"value":"remote"},
                "members":{"port":{"op":"copy","reference":{"scope":"root","path":"/port"}}}}}
          }
        }
        """u8.ToArray();

    private static byte[] EditingDefinition() => DefinitionSourceCodec.ParseJson(EditingSource()).CanonicalBytes.ToArray();

    private static byte[] EditingSource() => """
        {
          "$schema":"urn:islamu:configurator:authoring:v0.0.1",
          "identity":{"familyId":"8508c82b-2f4a-42b5-af54-0e48c8c89a6e","revision":2},
          "project":{"id":"example.native-editing","name":"Native structured editing","publisher":"example"},
          "applicationVersions":["1.0"],
          "configuration":{"type":"object","additionalProperties":false,"x-configurator":{"sensitivity":"public"},"properties":{
            "empty.object":{"title":"Empty object","type":"object","additionalProperties":false,"properties":{},
              "x-configurator":{"sensitivity":"public","detail":"basic"}},
            "empty.list":{"title":"Empty list","type":"array","maxItems":0,
              "items":{"type":"string","x-configurator":{"sensitivity":"public"}},
              "x-configurator":{"sensitivity":"public","detail":"basic"}},
            "rows.part":{"title":"Rows","type":"array",
              "x-configurator":{"sensitivity":"public","detail":"basic",
                "initialValue":[{"count.part":1,"note.part":"first"},{"count.part":2,"note.part":"second"}]},
              "items":{"type":"object","additionalProperties":false,"x-configurator":{"sensitivity":"public"},"properties":{
                "count.part":{"title":"Count","type":"integer","x-configurator":{"sensitivity":"public","detail":"basic"}},
                "note.part":{"title":"Note","type":"string","x-configurator":{"sensitivity":"public","detail":"basic"}}}}},
            "named":{"title":"Named values","type":"object","maxProperties":2,"required":["caption"],
              "properties":{"caption":{"type":"string","title":"Fixed caption","minLength":1,
                "x-configurator":{"sensitivity":"public","detail":"basic"}}},
              "additionalProperties":{"type":"integer","minimum":1,"x-configurator":{"sensitivity":"public","detail":"basic"}},
              "x-configurator":{"sensitivity":"public","detail":"basic"}},
            "provider":{"title":"Provider","type":"object","x-configurator":{"sensitivity":"public","detail":"basic"},"oneOf":[
              {"type":"object","additionalProperties":false,"required":["kind"],"x-configurator":{"sensitivity":"public"},"properties":{
                "kind":{"type":"string","const":"first","x-configurator":{"sensitivity":"public","detail":"basic"}},
                "count":{"title":"Branch count","type":"integer","x-configurator":{"sensitivity":"public","detail":"basic"}}}},
              {"type":"object","additionalProperties":false,"required":["kind"],"x-configurator":{"sensitivity":"public"},"properties":{
                "kind":{"type":"string","const":"second","x-configurator":{"sensitivity":"public","detail":"basic"}},
                "other":{"type":"string","x-configurator":{"sensitivity":"public","detail":"basic"}}}}]},
            "choice":{"title":"Choice","type":["integer","null"],"enum":[1,2,null],"default":2,
              "x-configurator":{"sensitivity":"public","detail":"basic","initialValue":1,"suggestedValue":2,
                "choices":[{"value":1,"label":"Equal"},{"value":2,"label":"Equal"},{"value":null,"label":"Null"}]}}
          }},
          "localization":{"defaultLocale":"en","translations":{"ar":{"direction":"rtl","overrides":{
            "/configuration/properties/choice/x-configurator/choices/0/label":"Translated equal",
            "/configuration/properties/choice/x-configurator/choices/1/label":"Translated equal"}}}},
          "outputs":{"config.json":{"id":"config","format":"json","targetUpdateMode":"create","restartRequirement":"process"}}
        }
        """u8.ToArray();

    private static async Task SharedEditingAsync()
    {
        _currentStep = "shared-editing-actions";
        string culture = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        using var shell = PublicationShell((_, _, _) =>
            Task.FromResult(ProtectedArtifactCommitResult.Installed(true)));
        var window = new MainWindow { DataContext = shell };
        try
        {
            window.Show();
            Require(shell.Loader.LoadDefinition(EditingDefinition()), "editing-definition");
            shell.Workspace.SelectedLocale = culture;
            await FlushAsync();
            var form = shell.Workspace.Form!;
            long revision = form.InputRevision;
            shell.Workspace.OptionalEditorsExpanded = true;
            shell.Workspace.OptionalEditorsExpanded = false;
            await FlushAsync();
            Require(form.InputRevision == revision && !form.Snapshot().Values.ContainsKey("empty.object") &&
                !form.Snapshot().Values.ContainsKey("empty.list"), "browsing-supplied-answer");
            foreach (FieldViewModel field in shell.Workspace.Fields.Where(field =>
                field.Id is "empty.object" or "empty.list"))
            {
                Button[] buttons = window.GetLogicalDescendants().OfType<Button>()
                    .Where(button => ReferenceEquals(button.DataContext, field)).ToArray();
                Button supply = buttons.Single(button => ReferenceEquals(button.Command, field.SupplyCommand));
                Control clear = CommandButton(window, field.ClearCommand);
                Require(Equals(supply.Content, shell.Workspace.Strings.SupplyLabel) &&
                    Equals(((MenuItem)clear).Header, shell.Workspace.Strings.ClearLabel), "localized-labels-" + culture);
                Require(AutomationProperties.GetName(supply) == shell.Workspace.Strings.SupplyName(field.Title) &&
                    AutomationProperties.GetName(clear) == shell.Workspace.Strings.ClearName(field.Title), "localized-names-" + culture);
                Require(AutomationProperties.GetHelpText(supply) == shell.Workspace.Strings.SupplyHelp &&
                    AutomationProperties.GetHelpText(clear) == shell.Workspace.Strings.ClearHelp, "localized-help-" + culture);
                Click(supply);
                await FlushAsync();
                Require(supply.IsFocused && form.Snapshot().Values[field.Id].GetRawText() ==
                    (field is ObjectFieldViewModel ? "{}" : "[]"), "empty-presence-or-focus");
                Click(clear);
                await FlushAsync();
                Require(supply.IsFocused && !form.Snapshot().Values.ContainsKey(field.Id), "clear-presence-or-focus");
            }
            var list = (ListFieldViewModel)shell.Workspace.Fields.Single(field => field.Id == "rows.part");
            var survivor = (ObjectFieldViewModel)list.Items[1];
            survivor.IsExpanded = true;
            await FlushAsync();
            TextBox count = window.GetLogicalDescendants().OfType<TextBox>()
                .Single(control => ReferenceEquals(control.DataContext, survivor.Children.Single(child => child.Id == "count.part")));
            Require(count.Focus(), "row-focus");
            count.Text = "unfinished-number";
            await FlushAsync();
            Require(survivor.Children.Single(child => child.Id == "count.part").HasError &&
                !survivor.Children.Single(child => child.Id == "note.part").HasError, "sibling-error-leak");
            Require(form.RemoveListItem(["rows.part"], 0), "row-removal");
            await FlushAsync();
            Require(ReferenceEquals(list.Items.Single(), survivor) && count.IsFocused &&
                count.Text == "unfinished-number", "survivor-draft-or-focus");
            await CaptureAsync(window, "shared-editing-" + culture);
            count.Text = "4";
            await FlushAsync();
            Require(shell.Workspace.Validate() &&
                form.Snapshot().Values["rows.part"].GetRawText() == """[{"count.part":4,"note.part":"second"}]""",
                "corrected-row");
        }
        finally { window.Close(); }
    }

    private static async Task StructuredEditingAsync(MainWindow window, DesktopShellViewModel shell, string directory)
    {
        Require(shell.Loader.LoadDefinition(EditingDefinition()), "structured-definition");
        await FlushAsync();
        var workspace = shell.Workspace;
        var form = workspace.Form!;
        var publisher = shell.Publisher;
        var choice = (EnumFieldViewModel)workspace.Fields.Single(field => field.Id == "choice");
        ComboBox choices = Input<ComboBox>(window, "Choice");
        Require(choice.Choices[1].Label == choice.Choices[2].Label &&
            choices.SelectedIndex == 1 && !form.Snapshot().Values.ContainsKey("choice"), "initial-not-descriptive-default");
        choices.SelectedIndex = 2;
        await FlushAsync();
        Require(form.Snapshot().Values["choice"].GetInt32() == 2, "equal-label-typed-choice");
        Click(CommandButton(window, choice.SetNullCommand));
        await FlushAsync();
        Require(form.Snapshot().Values["choice"].ValueKind == JsonValueKind.Null, "explicit-null-not-supplied");
        Click(CommandButton(window, choice.ClearCommand));
        await FlushAsync();
        Require(!form.Snapshot().Values.ContainsKey("choice") && choices.SelectedIndex == 0, "omission-restores-initial");
        Click(CommandButton(window, choice.ResetCommand));
        await FlushAsync();
        Require(!form.Snapshot().Values.ContainsKey("choice") && choices.SelectedIndex == 1, "reset-not-initial");
        Click(CommandButton(window, choice.ApplySuggestionCommand));
        await FlushAsync();
        Require(form.Snapshot().Values["choice"].GetInt32() == 2, "deliberate-suggestion");
        Click(CommandButton(window, choice.ResetCommand));
        await FlushAsync();

        var map = (MapFieldViewModel)workspace.Fields.Single(field => field.Id == "named");
        window.GetLogicalDescendants().OfType<TextBox>().Single(control =>
            ReferenceEquals(control.DataContext, map) && AutomationProperties.GetName(control) == map.NewKeyLabel).Text = "literal/key";
        await FlushAsync();
        Click(CommandButton(window, map.AddEntryCommand));
        await FlushAsync();
        FieldViewModel entry = map.Entries.Single();
        TextBox number = window.GetLogicalDescendants().OfType<TextBox>()
            .Single(control => ReferenceEquals(control.DataContext, entry));
        FieldViewModel caption = map.Children.Single(child => child.Id == "caption");
        TextBox captionInput = Input<TextBox>(window, "Fixed caption");
        Require(caption is StringFieldViewModel && caption.IsRequired &&
            captionInput.IsVisible, "mixed-map-fixed-schema");
        Require(!workspace.Validate(), "mixed-map-missing-fixed-ready");
        await FlushAsync();
        Require(captionInput.IsFocused, "mixed-map-fixed-correction");
        captionInput.Text = "fixed caption";
        await FlushAsync();
        number.Text = "unfinished-number";
        await FlushAsync();
        Require(entry.HasError && !workspace.Validate(), "map-invalid-draft-ready");
        number.Text = "7";
        await FlushAsync();
        Require(!entry.HasError && form.Snapshot().Values["named"].GetProperty("literal/key").GetInt32() == 7,
            "map-key-or-value-not-typed");

        var provider = (VariantFieldViewModel)workspace.Fields.Single(field => field.Id == "provider");
        Input<ComboBox>(window, "Provider").SelectedItem = provider.Choices[0];
        await FlushAsync();
        TextBox retired = Input<TextBox>(window, "Branch count");
        retired.Text = "5";
        await FlushAsync();
        Select(window, shell, "config");
        window.GetNamedControl<TextBox>("OutputDirectory").Text = directory;
        await PrepareAsync(window, shell);
        Input<ComboBox>(window, "Provider").SelectedItem = provider.Choices[1];
        await FlushAsync();
        Require(!publisher.CanSave && !publisher.CanSaveBundle &&
            form.Snapshot().Values["provider"].GetRawText() == """{"kind":"second"}""", "branch-keeps-stale-output");
        long switchedRevision = form.InputRevision;
        retired.Text = "99";
        await FlushAsync();
        Require(form.InputRevision == switchedRevision &&
            form.Snapshot().Values["provider"].GetRawText() == """{"kind":"second"}""", "retired-branch-writes-answer");

        window.GetNamedControl<TextBox>("FileName").Text = "typed-before.json";
        await PrepareAsync(window, shell);
        await ActionAsync(window, shell, "SaveSelection", () => !shell.IsSaving && shell.Status.StartsWith("save-", StringComparison.Ordinal));
        string beforePath = Path.Combine(directory, "typed-before.json");
        await VerifyJsonAsync(beforePath, """
            {"choice":1,"named":{"caption":"fixed caption","literal/key":7},"provider":{"kind":"second"},
             "rows.part":[{"count.part":1,"note.part":"first"},{"count.part":2,"note.part":"second"}]}
            """);
        window.GetNamedControl<TextBox>("FileName").Text = "typed-after.json";
        await PrepareAsync(window, shell);
        var prepared = publisher.Preparation;
        long revision = form.InputRevision;
        JsonElement answers = JsonSerializer.SerializeToElement(form.Snapshot().Values);
        foreach (string locale in new[] { "ar", "en" })
        {
            var changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            void ViewChanged(object? sender, EventArgs args)
            {
                if (form.Locale == locale) changed.TrySetResult();
            }
            form.ViewChanged += ViewChanged;
            try
            {
                window.GetNamedControl<ComboBox>("DefinitionLocale").SelectedItem = locale;
                await changed.Task.WaitAsync(TimeSpan.FromSeconds(10));
                await FlushAsync();
            }
            finally { form.ViewChanged -= ViewChanged; }
            Require(form.InputRevision == revision && ReferenceEquals(prepared, publisher.Preparation) && publisher.CanSave &&
                JsonElement.DeepEquals(answers, JsonSerializer.SerializeToElement(form.Snapshot().Values)) &&
                choice.SelectedIndex == 1 && choice.Choices[1].Label == choice.Choices[2].Label &&
                choice.Choices[1].Label == (locale == "ar" ? "Translated equal" : "Equal") &&
                window.FlowDirection == (locale == "ar" ? Avalonia.Media.FlowDirection.RightToLeft : Avalonia.Media.FlowDirection.LeftToRight) &&
                ReferenceEquals(number, window.GetLogicalDescendants().OfType<TextBox>()
                    .Single(control => ReferenceEquals(control.DataContext, entry))), "locale-mutates-answer-or-preparation");
            await CaptureAsync(window, "typed-editing-" + locale);
        }
        await ActionAsync(window, shell, "SaveSelection", () => !shell.IsSaving && shell.Status.StartsWith("save-", StringComparison.Ordinal));
        string afterPath = Path.Combine(directory, "typed-after.json");
        byte[] beforeBytes = await File.ReadAllBytesAsync(beforePath);
        byte[] afterBytes = await File.ReadAllBytesAsync(afterPath);
        Require(beforeBytes.AsSpan().SequenceEqual(afterBytes) &&
            OperatingSystem.IsLinux() && File.GetUnixFileMode(beforePath) == (UnixFileMode.UserRead | UnixFileMode.UserWrite) &&
            File.GetUnixFileMode(afterPath) == (UnixFileMode.UserRead | UnixFileMode.UserWrite), "locale-output-or-protection");
    }

    private static async Task CloseLifecycleAsync()
    {
        string largeName = new('x', 65536);
        byte[] expected = System.Text.Encoding.UTF8.GetBytes("{\"name\":\"" + largeName + "\"}");
        foreach (string check in new[] { "D15-close-drain-settles", "D16-close-deadline-unknown", "D17-close-stay-force" })
        {
            _currentCheck = check;
            _currentStep = "close-drain";
            var clock = new NativeCloseClock();
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            ReadOnlyMemory<byte> borrowed = default;
            using var shell = PublicationShell(async (_, bytes, _) =>
            {
                borrowed = bytes;
                entered.TrySetResult();
                await release.Task.WaitAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
                return ProtectedArtifactCommitResult.Installed(true);
            }, clock);
            var window = new MainWindow { DataContext = shell };
            int closeCount = 0;
            window.Closed += (_, _) => { closeCount++; closed.TrySetResult(); };
            try
            {
                window.Show();
                Require(shell.Loader.LoadDefinition(NativeDefinition()), "close-definition");
                Require(shell.Workspace.Form!.SelectLocale(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName),
                    "close-workspace-locale");
                await FlushAsync();
                Input<TextBox>(window, "Service name").Text = largeName;
                await FlushAsync();
                Select(window, shell, "config");
                await PrepareAsync(window, shell);
                Click(window.GetNamedControl<Button>("SaveSelection"));
                await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
                Require(borrowed.Span.SequenceEqual(expected), "close-large-publication");
                Task physical = shell.ActiveCompletion;
                Task decision = CloseDecisionAsync(shell);
                window.Close();
                window.Close();
                Require(window.IsVisible && !closed.Task.IsCompleted && !physical.IsCompleted &&
                    !shell.Publisher.CanSave, "close-released-pending-work");
                if (check != "D15-close-drain-settles")
                {
                    Require(clock.DueTime == TimeSpan.FromSeconds(2), "close-deadline-budget");
                    clock.Expire();
                    await decision.WaitAsync(TimeSpan.FromSeconds(10));
                    await FlushAsync();
                    Require(window.IsVisible && !closed.Task.IsCompleted && shell.CloseDecisionRequired &&
                        shell.Publisher.CurrentPublication is { IsUnknown: true, IsSettled: false }, "close-unknown-not-retained");
                    Button stay = window.GetNamedControl<Button>("StayOpen");
                    Button force = window.GetNamedControl<Button>("ForceClose");
                    Require(stay.IsEffectivelyVisible && stay.IsEffectivelyEnabled &&
                        force.IsEffectivelyVisible && force.IsEffectivelyEnabled &&
                        ReferenceEquals(stay.Command, shell.StayOpenCommand) &&
                        ReferenceEquals(force.Command, shell.ForceCloseCommand) &&
                        !string.IsNullOrWhiteSpace(AutomationProperties.GetName(stay)) &&
                        !string.IsNullOrWhiteSpace(AutomationProperties.GetName(force)) &&
                        !string.IsNullOrWhiteSpace(AutomationProperties.GetHelpText(stay)) &&
                        !string.IsNullOrWhiteSpace(AutomationProperties.GetHelpText(force)) &&
                        stay.Focus(), "close-controls-unreachable");
                    await CaptureAsync(window, check + "-unknown");
                    RequireSafe(window.GetNamedControl<TextBlock>("CloseStatus").Text, "Example service");
                    if (check == "D17-close-stay-force")
                    {
                        Click(stay);
                        await FlushAsync();
                        Require(window.IsVisible && !shell.IsClosing && shell.IsBusy &&
                            !physical.IsCompleted && !shell.Publisher.CanSave, "stay-open-released-owner");
                        decision = CloseDecisionAsync(shell);
                        window.Close();
                        clock.Expire();
                        await decision.WaitAsync(TimeSpan.FromSeconds(10));
                        Click(force);
                        await closed.Task.WaitAsync(TimeSpan.FromSeconds(10));
                        Require(!physical.IsCompleted &&
                            borrowed.Span.SequenceEqual(expected), "force-close-cleared-consumer");
                    }
                }
                release.TrySetResult();
                await physical.WaitAsync(TimeSpan.FromSeconds(10));
                await closed.Task.WaitAsync(TimeSpan.FromSeconds(10));
                await FlushAsync();
                Require(closeCount == 1 && !window.IsVisible && window.DataContext is null &&
                    shell.Workspace.Form is null && !shell.Publisher.CanSave &&
                    borrowed.ToArray().All(value => value == 0), "close-late-callback-or-cleanup");
                Require(shell.Publisher.PublicationHistory.Single().Outcomes.Single().Result is
                { InstallationEstablished: true, VerificationSucceeded: true }, "close-installation-lost");
                Pass();
            }
            finally
            {
                release.TrySetResult();
                await shell.ActiveCompletion.WaitAsync(TimeSpan.FromSeconds(10));
                window.Close();
            }
        }
    }

    private static Task CloseDecisionAsync(DesktopShellViewModel shell)
    {
        var signal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void Changed(object? sender, PropertyChangedEventArgs args)
        {
            if (!shell.CloseDecisionRequired) return;
            shell.PropertyChanged -= Changed;
            signal.TrySetResult();
        }
        shell.PropertyChanged += Changed;
        return signal.Task;
    }

    private sealed class NativeCloseClock : TimeProvider
    {
        private readonly List<CloseTimer> _timers = [];
        internal TimeSpan DueTime { get; private set; }
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            DueTime = dueTime;
            var timer = new CloseTimer(callback, state);
            _timers.Add(timer);
            return timer;
        }
        internal void Expire()
        {
            foreach (CloseTimer timer in _timers.ToArray()) timer.Fire();
        }
        private sealed class CloseTimer(TimerCallback callback, object? state) : ITimer
        {
            private int _finished;
            public bool Change(TimeSpan dueTime, TimeSpan period) => Volatile.Read(ref _finished) == 0;
            internal void Fire()
            {
                if (Interlocked.Exchange(ref _finished, 1) != 0) return;
                callback(state);
            }
            public void Dispose() => Interlocked.Exchange(ref _finished, 1);
            public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
        }
    }

    private static async Task LargePartialBundleAsync()
    {
        _currentStep = "large-concurrent-bundle-conflict";
        string directory = Directory.CreateTempSubdirectory("configurator-native-partial-").FullName;
        string largeName = new('x', 65536);
        byte[] expected = System.Text.Encoding.UTF8.GetBytes("{\"name\":\"" + largeName + "\"}");
        byte[] concurrent = "synthetic-concurrent-output\n"u8.ToArray();
        var writer = new ProtectedArtifactWriter();
        bool conflictCreated = false;
        using var shell = PublicationShell(async (path, bytes, token) =>
        {
            using ProtectedArtifactPreparation preparation = await writer.PrepareAsync(
                Configurator.Core.ArtifactKind.Unknown, path, bytes, token);
            return await preparation.CommitAsync(token);
        }, publishBundle: (artifacts, output, observe, token) =>
        {
            Require(artifacts.Select(artifact => artifact.ArtifactId).SequenceEqual(["config", "yaml"]),
                "large-partial-selection");
            return ProtectedArtifactBundlePublisher.PublishAsync(writer, artifacts, output, outcome =>
            {
                observe(outcome);
                if (outcome.ArtifactId != "config") return;
                Require(outcome.Result is { InstallationEstablished: true, VerificationSucceeded: true },
                    "large-partial-first-installation");
                if (OperatingSystem.IsLinux())
                {
                    using var conflict = new FileStream(Path.Combine(output, "service.yaml"), new FileStreamOptions
                    {
                        Mode = FileMode.CreateNew,
                        Access = FileAccess.Write,
                        Share = FileShare.None,
                        UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite
                    });
                    conflict.Write(concurrent);
                    conflictCreated = true;
                }
            }, token);
        });
        var window = new MainWindow { DataContext = shell };
        try
        {
            window.Show();
            Require(shell.Loader.LoadDefinition(NativeDefinition()), "large-partial-definition");
            Require(shell.Workspace.Form!.SelectLocale(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName),
                "large-partial-workspace-locale");
            shell.Publisher.OutputDirectory = directory;
            await FlushAsync();
            Input<TextBox>(window, "Service name").Text = largeName;
            foreach (string id in shell.Publisher.SelectedArtifactIds.ToArray()) Include(window, id, false);
            Include(window, "config", true);
            Include(window, "yaml", true);
            await FlushAsync();
            await PrepareBundleAsync(window, shell);
            await CaptureAsync(window, "large-partial-preparation");
            Require(shell.Publisher.CanSaveBundle, "large-partial-not-ready:" + shell.Status);
            await ActionAsync(window, shell, "SaveSelection",
                () => !shell.IsSaving && shell.Status.StartsWith("bundle-save-", StringComparison.Ordinal));
            await shell.ActiveCompletion.WaitAsync(TimeSpan.FromSeconds(10));
            await FlushAsync();
            ProtectedArtifactPublicationReceipt receipt = shell.Publisher.PublicationHistory.Single();
            await CaptureAsync(window, "large-partial-bundle");
            Require(conflictCreated && receipt.IsSettled && !receipt.IsUnknown && !receipt.CleanupFailed
                && receipt.Outcomes.Count == 2
                && receipt.Outcomes[0] is { ArtifactId: "config", Result.InstallationEstablished: true, Result.VerificationSucceeded: true }
                && receipt.Outcomes[1] is { ArtifactId: "yaml", Result.Status: ProtectedArtifactStatus.TargetChanged, Result.InstallationEstablished: false }
                && !shell.Publisher.CanSaveBundle, "large-partial-receipt");
            Require((await File.ReadAllBytesAsync(Path.Combine(directory, "service.json"))).AsSpan().SequenceEqual(expected)
                && (await File.ReadAllBytesAsync(Path.Combine(directory, "service.yaml"))).AsSpan().SequenceEqual(concurrent),
                "large-partial-prefix-or-overwrite");
            Require(OperatingSystem.IsLinux()
                && File.GetUnixFileMode(Path.Combine(directory, "service.json")) == (UnixFileMode.UserRead | UnixFileMode.UserWrite)
                && Directory.GetFileSystemEntries(directory).Length == 2, "large-partial-permissions-or-cleanup");
            TextBlock facts = window.GetNamedControl<TextBlock>("PublicationReceipt");
            Require(facts.IsEffectivelyVisible && !string.IsNullOrWhiteSpace(facts.Text)
                && !facts.Text.Contains(largeName[..32], StringComparison.Ordinal), "large-partial-visible-safe-receipt");
        }
        finally
        {
            await shell.ActiveCompletion.WaitAsync(TimeSpan.FromSeconds(10));
            window.Close();
            Directory.Delete(directory, true);
        }
    }

    private static async Task PhysicalSettlementAsync()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        ReadOnlyMemory<byte> borrowed = default;
        byte[] consumed = [];
        using var shell = PublicationShell(async (_, bytes, _) =>
        {
            borrowed = bytes;
            entered.TrySetResult();
            await release.Task.WaitAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
            consumed = bytes.ToArray();
            return ProtectedArtifactCommitResult.Installed(false, cleanupFailed: true);
        });
        var window = new MainWindow { DataContext = shell };
        try
        {
            window.Show();
            Require(shell.Loader.LoadDefinition(NativeDefinition()), "settlement-definition");
            Require(shell.Workspace.Form!.SelectLocale(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName),
                "settlement-workspace-locale");
            await FlushAsync();
            Select(window, shell, "environment");
            await PrepareAsync(window, shell);
            TextBox retainedControl = Input<TextBox>(window, "Service name");
            Click(window.GetNamedControl<Button>("SaveSelection"));
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Task completion = shell.ActiveCompletion;
            Click(window.GetNamedControl<Button>("Cancel"));
            Require(!completion.IsCompleted && shell.IsBusy && !shell.Publisher.CanSave &&
                await shell.Publisher.PrepareAsync() != DesktopPreparationOutcome.Prepared, "physical-owner-released");
            Require(borrowed.Span.SequenceEqual("NAME=Example service\n"u8), "borrowed-input-cleared");
            await CaptureAsync(window, "settlement-pending");
            release.TrySetResult();
            await completion.WaitAsync(TimeSpan.FromSeconds(10));
            await FlushAsync();
            ProtectedArtifactPublicationReceipt receipt = shell.Publisher.PublicationHistory.Single();
            Require(consumed.AsSpan().SequenceEqual("NAME=Example service\n"u8) &&
                borrowed.ToArray().All(value => value == 0), "physical-consumption-or-cleanup");
            Require(receipt.IsSettled && receipt.CleanupFailed &&
                receipt.Outcomes.Single().Result is { InstallationEstablished: true, VerificationSucceeded: false } &&
                !shell.Publisher.CanSave, "historical-installation-lost");
            TextBlock facts = window.GetNamedControl<TextBlock>("PublicationReceipt");
            Require(facts.Text is { Length: > 0 } text &&
                !text.Contains("Example service", StringComparison.Ordinal), "receipt-rendering");
            Require(!string.IsNullOrWhiteSpace(AutomationProperties.GetName(facts)) &&
                !string.IsNullOrWhiteSpace(AutomationProperties.GetHelpText(facts)) &&
                ReferenceEquals(retainedControl, Input<TextBox>(window, "Service name")), "receipt-control-identity");
            Require(window.GetNamedControl<Button>("Cancel").Focus(), "settlement-keyboard-focus");
            await CaptureAsync(window, "settlement-receipt");
        }
        finally
        {
            release.TrySetResult();
            await shell.ActiveCompletion.WaitAsync(TimeSpan.FromSeconds(10));
            window.Close();
        }
    }

    private static DesktopShellViewModel PublicationShell(
        Func<string, ReadOnlyMemory<byte>, CancellationToken, Task<ProtectedArtifactCommitResult>> save,
        TimeProvider? timeProvider = null,
        Func<IReadOnlyList<DefinitionArtifact>, string, Action<ProtectedArtifactBundleOutcome>, CancellationToken,
            Task<ProtectedArtifactBundleResult>>? publishBundle = null,
        DesktopTheme? theme = null)
    {
        var messenger = new StrongReferenceMessenger();
        var dialogs = new NoDialogs();
        DefinitionLoaderViewModel? loader = null;
        FormWorkspaceViewModel? workspace = null;
        ArtifactPublisherViewModel? publisher = null;
        DesktopImportReviewViewModel? importReview = null;
        try
        {
            loader = new(messenger, dialogs);
            workspace = new(messenger);
            publisher = new(messenger, dialogs, save, Path.GetTempPath(), true, publishBundle);
            importReview = new(messenger, dialogs, publisher);
            var shell = new DesktopShellViewModel(messenger, loader, workspace, publisher, importReview, timeProvider, theme);
            loader = null;
            workspace = null;
            publisher = null;
            importReview = null;
            return shell;
        }
        finally
        {
            importReview?.Dispose();
            loader?.Dispose();
            workspace?.Dispose();
            publisher?.Dispose();
        }
    }

    private sealed class NoDialogs : IStorageDialogService
    {
        public Task<StorageSelection<Uri>> SelectAsync(DesktopPathSelectionKind kind, CancellationToken cancellationToken = default) =>
            Task.FromResult(new StorageSelection<Uri>(StorageSelectionStatus.Cancelled));

        public Task<StorageSelection<BoundedFileInput>> ReadImportAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new StorageSelection<BoundedFileInput>(StorageSelectionStatus.Cancelled));
    }

    private static async Task EventAsync(MainWindow window, DesktopShellViewModel shell, string directory, int count)
    {
        using var measurement = new EventValidationMeasurement();
        _currentStep = "event-load";
        await LoadAsync(window, shell, Path.Combine(AppContext.BaseDirectory, "Fixtures", "Event", "definition.json"), true);
        SelectChoice(window, shell, "operation", "configuration-manifest");
        window.GetNamedControl<CheckBox>("OptionalEditors").IsChecked = true;
        Select(window, shell, "configuration-manifest");
        await FlushAsync();
        _currentStep = "event-name";
        var name = RevealField(window, "configuration-manifest.metadata.name");
        name.GetLogicalDescendants().OfType<TextBox>().Single(control => control.IsEffectivelyVisible).Text = "qa-config";
        _currentStep = "event-settings";
        var settings = RevealField(window, "configuration-manifest.spec.instance.settings");
        _currentStep = "event-settings-value";
        SetBoolean(settings, "modules.tech_enabled", true);
        _currentStep = "event-tenants";
        var tenants = RevealField(window, "configuration-manifest.tenants");
        var rows = (ListFieldViewModel)shell.Workspace.Fields.Single(field => field.Id == "configuration-manifest.tenants");
        for (int index = 0; index < count; index++)
        {
            _currentStep = "event-tenants-" + index.ToString(CultureInfo.InvariantCulture) + "-add";
            Console.WriteLine($"native-step:{_currentCheck}:{_currentStep}:validations={measurement.Count}");
            Click(CommandButton(tenants, rows.AddItemCommand));
            _currentStep = "event-tenants-" + index.ToString(CultureInfo.InvariantCulture) + "-layout";
            await FlushAsync();
            Console.WriteLine($"native-step:{_currentCheck}:{_currentStep}:validations={measurement.Count}");
            _currentStep = "event-tenants-" + index.ToString(CultureInfo.InvariantCulture) + "-identifier";
            Console.WriteLine($"native-step:{_currentCheck}:{_currentStep}:validations={measurement.Count}");
            var row = (ObjectFieldViewModel)rows.Items[index];
            var metadata = (ObjectFieldViewModel)row.Children.Single(field => field.Id == "metadata");
            var specification = (ObjectFieldViewModel)row.Children.Single(field => field.Id == "spec");
            row.IsExpanded = true;
            metadata.IsExpanded = true;
            specification.IsExpanded = true;
            await FlushAsync();
            Input<TextBox>(tenants, metadata.Children.Single(field => field.Id == "name")).Text = "tenant-" + index.ToString(CultureInfo.InvariantCulture);
            _currentStep = "event-tenants-" + index.ToString(CultureInfo.InvariantCulture) + "-display-name";
            Console.WriteLine($"native-step:{_currentCheck}:{_currentStep}:validations={measurement.Count}");
            Input<TextBox>(tenants, specification.Children.Single(field => field.Id == "displayName")).Text = "Tenant " + index.ToString(CultureInfo.InvariantCulture);
            foreach (string member in new[] { "settings", "documents", "legalDocuments" })
            {
                _currentStep = "event-tenants-" + index.ToString(CultureInfo.InvariantCulture) + "-supply-" + member;
                Console.WriteLine($"native-step:{_currentCheck}:{_currentStep}:validations={measurement.Count}");
                Click(CommandButton(window, specification.Children.Single(field => field.Id == member).SupplyCommand));
            }
        }
        await FlushAsync();
        _currentStep = "event-prepare";
        Select(window, shell, "configuration-manifest");
        foreach (string id in shell.Publisher.SelectedArtifactIds.ToArray()) Include(window, id, false);
        Include(window, "configuration-manifest", true);
        window.GetNamedControl<TextBox>("OutputDirectory").Text = directory;
        window.GetNamedControl<TextBox>("FileName").Text = "event-" + count.ToString(CultureInfo.InvariantCulture) + ".json";
        await PrepareAsync(window, shell);
        _currentStep = "event-save";
        await ActionAsync(window, shell, "SaveSelection", () => !shell.IsSaving && shell.Status.StartsWith("save-", StringComparison.Ordinal));
        string output = Path.Combine(directory, "event-" + count.ToString(CultureInfo.InvariantCulture) + ".json");
        using var document = JsonDocument.Parse(await File.ReadAllBytesAsync(output));
        _currentStep = "event-output";
        JsonElement actual = document.RootElement;
        Require(actual.GetProperty("$schema").GetString() == "https://schemas.islamu.org/event/configuration-manifest/v1alpha2/schema.json" &&
            actual.GetProperty("apiVersion").GetString() == "configuration.islamu.org/v1alpha2" &&
            actual.GetProperty("kind").GetString() == "ConfigurationManifest" &&
            actual.GetProperty("metadata").GetProperty("name").GetString() == "qa-config", "event-manifest-identity");
        JsonElement spec = actual.GetProperty("spec");
        Require(spec.GetProperty("instance").GetProperty("settings").GetProperty("modules.tech_enabled").GetBoolean() &&
            spec.GetProperty("tenants").GetArrayLength() == count, "event-manifest-count");
        int rowIndex = 0;
        foreach (JsonElement row in spec.GetProperty("tenants").EnumerateArray())
        {
            Require(row.GetProperty("metadata").GetProperty("name").GetString() == "tenant-" + rowIndex.ToString(CultureInfo.InvariantCulture) &&
                row.GetProperty("spec").GetProperty("displayName").GetString() == "Tenant " + rowIndex.ToString(CultureInfo.InvariantCulture), "event-tenant-values");
            foreach (string member in new[] { "settings", "documents", "legalDocuments" })
                Require(row.GetProperty("spec").GetProperty(member).ValueKind == JsonValueKind.Object, "event-required-objects");
            rowIndex++;
        }
        await PrepareBundleAsync(window, shell);
        Require(shell.Publisher.CanSaveSelection && shell.Publisher.SelectedArtifactIds.SequenceEqual(["configuration-manifest"]), "event-active-outputs");
        Click(window.GetNamedControl<Button>("Cancel"));
    }

    private static async Task LoadAsync(MainWindow window, DesktopShellViewModel shell, string path, bool success)
    {
        window.GetNamedControl<TextBox>("DefinitionPath").Text = path;
        await ActionAsync(window, shell, "LoadDefinition", () => success ? shell.Status == "definition-loaded" :
            shell.Status.StartsWith("definition-invalid", StringComparison.Ordinal) || shell.Status == "definition-unreadable");
        Require((shell.Workspace.Form is not null) == success && !shell.Publisher.CanSave, "definition-admission");
        if (success)
            Require(shell.Workspace.Form!.SelectLocale(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName),
                "native-workspace-locale");
        await FlushAsync();
    }

    private sealed class EventValidationMeasurement : IObserver<DiagnosticListener>,
        IObserver<KeyValuePair<string, object?>>, IDisposable
    {
        private readonly IDisposable _listeners;
        private readonly List<IDisposable> _subscriptions = [];
        private int _count;
        internal EventValidationMeasurement() => _listeners = DiagnosticListener.AllListeners.Subscribe(this);
        internal int Count => Volatile.Read(ref _count);
        public void OnNext(DiagnosticListener listener)
        {
            if (listener.Name == "Configurator.Core.Preparation") _subscriptions.Add(listener.Subscribe(this));
        }
        public void OnNext(KeyValuePair<string, object?> item)
        {
            if (item.Key != "ValidationCompleted" || Interlocked.Increment(ref _count) != 25) return;
            string frames = string.Join(" > ", new StackTrace(false).GetFrames().Take(36)
                .Select(frame => frame.GetMethod())
                .Where(method => method is not null)
                .Select(method => method!.DeclaringType?.FullName + "." + method.Name));
            Console.WriteLine("native-validation-stack:" + frames);
        }
        public void OnCompleted() { }
        public void OnError(Exception error) { }
        public void Dispose()
        {
            _listeners.Dispose();
            foreach (IDisposable subscription in _subscriptions) subscription.Dispose();
        }
    }

    private static async Task ImportComparisonAsync(MainWindow window, DesktopShellViewModel shell,
        string directory, string definitionPath)
    {
        await LoadAsync(window, shell, definitionPath, true);
        ArtifactPublisherViewModel publisher = shell.Publisher;
        DesktopImportReviewViewModel review = shell.ImportReview;
        var form = shell.Workspace.Form!;
        Require(window.GetNamedControl<ComboBox>("ImportPurpose").SelectedIndex == 0, "comparison-not-default");
        Select(window, shell, "config");
        window.GetNamedControl<ComboBox>("Artifact").SelectedItem = publisher.Artifacts.Single(item => item.Id == "config");
        TextBox name = Input<TextBox>(window, "Service name");
        name.Text = "Current native work";
        await PrepareAsync(window, shell);
        var preparation = publisher.Preparation;
        long revision = form.InputRevision;
        string path = Path.Combine(directory, "comparison.json");
        await File.WriteAllTextAsync(path, """{"name":"Imported candidate"}""");
        window.GetNamedControl<TextBox>("ExistingFilePath").Text = path;
        await ActionAsync(window, shell, "Import", () => review.HasImportComparison);
        Require(form.InputRevision == revision && name.Text == "Current native work" &&
            ReferenceEquals(preparation, publisher.Preparation) && publisher.CanSave,
            "comparison-mutated-current-work");
        Require(!review.CanFillImportFields && !await review.FillImportFieldsAsync(), "comparison-overwrote-conflict");

        await File.WriteAllTextAsync(path, """{"name":7}""");
        await ActionAsync(window, shell, "Import", () => review.HasImportComparison && !shell.IsBusy);
        ImportFindingViewModel finding = review.ImportFindings.First(item => item.CanExplain);
        Click(CommandButton(window, finding.ExplainCommand));
        Require(review.ImportExplanation.Length > 0 && form.InputRevision == revision &&
            name.Text == "Current native work" && ReferenceEquals(preparation, publisher.Preparation),
            "comparison-schema-mutated-workspace");
        Require(!review.CanFillImportFields, "invalid-comparison-adoptable");
        await CaptureAsync(window, "import-comparison");

        TextBox age = Input<TextBox>(window, "Owner age");
        age.Text = "invalid-native-draft";
        long draftRevision = form.InputRevision;
        await File.WriteAllTextAsync(path, """{"name":"Imported candidate"}""");
        await ActionAsync(window, shell, "Import", () => review.HasImportComparison && !shell.IsBusy);
        Require(age.Text == "invalid-native-draft" && form.InputRevision == draftRevision,
            "comparison-committed-draft");
        shell.Navigation.ConfigureCommand.Execute(null);
        window.UpdateLayout();
        Click(CommandButton(window, ((FieldViewModel)age.DataContext!).ResetCommand));
        Click(CommandButton(window, shell.Workspace.Fields.Single(field => field.Id == "name").ResetCommand));
        await FlushAsync();
        await ActionAsync(window, shell, "Import", () => review.HasImportComparison && !shell.IsBusy);
        Require(review.CanFillImportFields, "comparison-pristine-adoption-unavailable:" + form.ImportAutofillCode);
        Click(CommandButton(window, review.FillImportFieldsCommand));
        await review.FillImportFieldsCommand.ExecutionTask!.WaitAsync(TimeSpan.FromSeconds(10));
        await FlushAsync();
        Require(form.Snapshot().Values["name"].GetString() == "Imported candidate" &&
            !review.HasImportComparison && publisher.Preparation is null,
            "comparison-deliberate-adoption-failed");
    }

    private static async Task SecretControlsAsync(MainWindow window, DesktopShellViewModel shell)
    {
        byte[] definition = DefinitionSourceCodec.ParseJson("""
            {
              "$schema":"urn:islamu:configurator:authoring:v0.0.1",
              "identity":{"familyId":"63895608-e6ed-43a5-884c-62bdd1f00913","revision":1},
              "project":{"id":"example.native-secrets","name":"Native secret controls","publisher":"example"},
              "applicationVersions":["1.0"],
              "configuration":{"type":"object","additionalProperties":false,"required":["password"],
                "x-configurator":{"sensitivity":"public"},"properties":{
                  "password":{"type":"string","title":"Synthetic password","minLength":16,"maxLength":64,
                    "x-configurator":{"detail":"basic","secret":{"kind":"password","acquisition":"local",
                      "constraints":{"characterSet":"ascii-printable","allowWhitespace":false,
                        "minLowercase":1,"minUppercase":1,"minDigits":1,"minSymbols":1},
                      "generation":{"length":32}}}},
                  "digest":{"type":"string","title":"Synthetic digest",
                    "x-configurator":{"detail":"basic","secret":{"kind":"digest","acquisition":"local",
                      "algorithm":"SHA256","encoding":"hex","generation":{"input":"generated",
                        "password":{"kind":"password","acquisition":"local","generation":{"length":32}}}}}},
                  "privateKey":{"type":"string","title":"Synthetic private key",
                    "x-configurator":{"detail":"basic","secret":{"kind":"private-key","acquisition":"local",
                      "algorithm":"ES256","encoding":"pkcs8-pem","generation":{}}}}}},
              "outputs":{"config.json":{"id":"config","format":"json","targetUpdateMode":"create","restartRequirement":"process"}}
            }
            """u8.ToArray()).CanonicalBytes.ToArray();
        Require(shell.Loader.LoadDefinition(definition), "secret-definition");
        await FlushAsync();
        var field = (StringFieldViewModel)shell.Workspace.Fields.Single(item => item.Id == "password");
        TextBox input = Input<TextBox>(window, "Synthetic password");
        Require(input.PasswordChar != '\0', "secret-default-unmasked");
        input.Text = "short";
        await FlushAsync();
        Require(!shell.Workspace.Validate() && field.HasError, "secret-invalid-ready");
        await FlushAsync();
        Require(input.IsFocused, "secret-correction-focus");
        Click(CommandButton(window, field.GenerateCommand));
        await field.GenerateCommand.ExecutionTask!.WaitAsync(TimeSpan.FromSeconds(20));
        await FlushAsync();
        Require(input.Text is { Length: 32 } && !field.HasError && shell.Workspace.Validate(), "secret-generation-invalid");
        Require(input.PasswordChar != '\0', "secret-generation-unmasked");
        Click(CommandButton(window, field.RevealCommand));
        await FlushAsync();
        Require(input.PasswordChar == '\0', "secret-reveal-failed");
        Click(CommandButton(window, field.RevealCommand));
        await FlushAsync();
        Require(input.PasswordChar != '\0', "secret-hide-failed");
        var clipboard = window.Clipboard ?? throw new QualificationException("secret-clipboard-unavailable");
        try
        {
            Click(CommandButton(window, field.CopyCommand));
            await field.CopyCommand.ExecutionTask!.WaitAsync(TimeSpan.FromSeconds(10));
            Require(await clipboard.TryGetTextAsync() == input.Text, "secret-explicit-copy-failed");
        }
        finally { await clipboard.ClearAsync(); }
        Click(CommandButton(window, field.RevealCommand));
        input.Text = "another-short";
        await FlushAsync();
        Require(input.PasswordChar != '\0' && field.HasError, "secret-edit-retained-reveal");
        Require(!field.ErrorText.Contains("another-short", StringComparison.Ordinal), "secret-diagnostic-value");
        Click(CommandButton(window, field.GenerateCommand));
        await field.GenerateCommand.ExecutionTask!.WaitAsync(TimeSpan.FromSeconds(20));
        await FlushAsync();
        Require(!field.HasError && shell.Workspace.Validate(), "secret-correction-failed");
        var digest = (StringFieldViewModel)shell.Workspace.Fields.Single(item => item.Id == "digest");
        Click(CommandButton(window, digest.GenerateCommand));
        await digest.GenerateCommand.ExecutionTask!.WaitAsync(TimeSpan.FromSeconds(20));
        await FlushAsync();
        Require(digest.HasGeneratedPassword && digest.GeneratedPassword.Length == 32 && digest.Text.Length == 64,
            "secret-derived-result-missing");
        TextBox original = window.GetLogicalDescendants().OfType<TextBox>()
            .Single(control => ReferenceEquals(control.DataContext, digest) && control.IsReadOnly);
        Require(original.PasswordChar != '\0', "secret-original-unmasked");
        await CaptureAsync(window, "secret-generated");
        try
        {
            Click(CommandButton(window, digest.CopyGeneratedPasswordCommand));
            await digest.CopyGeneratedPasswordCommand.ExecutionTask!.WaitAsync(TimeSpan.FromSeconds(10));
            Require(await clipboard.TryGetTextAsync() == digest.GeneratedPassword, "secret-original-copy-failed");
            Click(CommandButton(window, digest.CopyCommand));
            await digest.CopyCommand.ExecutionTask!.WaitAsync(TimeSpan.FromSeconds(10));
            Require(await clipboard.TryGetTextAsync() == digest.Text, "secret-digest-copy-failed");
        }
        finally { await clipboard.ClearAsync(); }
        Click(CommandButton(window, digest.DismissSecretResultCommand));
        await FlushAsync();
        Require(!digest.HasGeneratedPassword && original.Text == string.Empty && digest.Text.Length == 64,
            "secret-original-dismissal-failed");
        var key = (StringFieldViewModel)shell.Workspace.Fields.Single(item => item.Id == "privateKey");
        Click(CommandButton(window, key.GenerateCommand));
        await key.GenerateCommand.ExecutionTask!.WaitAsync(TimeSpan.FromSeconds(20));
        await FlushAsync();
        Require(key.OperationStatus == "secret-generated", "secret-pem-operation-" + key.OperationStatus);
        TextBox keyInput = Input<TextBox>(window, "Synthetic private key");
        string generatedPem = key.Text;
        Require(keyInput.AcceptsReturn, "secret-pem-editor-not-multiline");
        Require(generatedPem.Contains('\n'), "secret-pem-value-not-multiline");
        try
        {
            Click(CommandButton(window, key.CopyCommand));
            await key.CopyCommand.ExecutionTask!.WaitAsync(TimeSpan.FromSeconds(10));
            Require(await clipboard.TryGetTextAsync() == generatedPem, "secret-pem-copy-changed");
            keyInput.Text = string.Empty;
            await FlushAsync();
            var pasted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            void Pasted(object? sender, TextChangedEventArgs args) => pasted.TrySetResult();
            keyInput.TextChanged += Pasted;
            try
            {
                keyInput.Paste();
                await pasted.Task.WaitAsync(TimeSpan.FromSeconds(10));
                await FlushAsync();
                Require(key.Text == generatedPem && !key.HasError, "secret-pem-paste-changed");
            }
            finally { keyInput.TextChanged -= Pasted; }
        }
        finally { await clipboard.ClearAsync(); }
        // The UI dispatcher cannot resume generation's first yield until this edit is complete.
        Click(CommandButton(window, field.GenerateCommand));
        input.Text = "synthetic-edited-during-generation";
        await field.GenerateCommand.ExecutionTask!.WaitAsync(TimeSpan.FromSeconds(20));
        await FlushAsync();
        Require(input.Text == "synthetic-edited-during-generation" && field.OperationStatus == "secret-stale",
            "secret-stale-overwrote-edit");
    }

    private static async Task PrepareBundleAsync(MainWindow window, DesktopShellViewModel shell)
    {
        Review(window, shell);
        Click(window.GetNamedControl<Button>("PrepareSelection"));
        await shell.Publisher.PrepareSelectionCommand.ExecutionTask!.WaitAsync(TimeSpan.FromSeconds(10));
        await FlushAsync();
        Require(!shell.IsBusy, "bundle-preparation-owner-not-released");
    }

    private static async Task PrepareAsync(MainWindow window, DesktopShellViewModel shell)
    {
        Review(window, shell);
        await ActionAsync(window, shell, "PrepareSelection", () => (shell.Publisher.CanSave && shell.Status.StartsWith("artifact-ready:", StringComparison.Ordinal)) ||
            shell.Status.StartsWith("validation-invalid", StringComparison.Ordinal));
        Require(shell.Publisher.CanSave, "prepare-failed:" + shell.Status);
    }

    private static async Task ActionAsync(MainWindow window, DesktopShellViewModel shell, string button, Func<bool> finished)
    {
        if (button is "Import" or "SaveSelection" or "PrepareSelection")
            Review(window, shell, import: button == "Import");
        await FlushAsync();
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void Changed(object? sender, PropertyChangedEventArgs args)
        {
            if (finished()) completion.TrySetResult();
        }
        shell.PropertyChanged += Changed;
        shell.Publisher.PropertyChanged += Changed;
        try
        {
            Click(window.GetNamedControl<Button>(button));
            if (finished()) completion.TrySetResult();
            try { await completion.Task.WaitAsync(TimeSpan.FromSeconds(10)); }
            catch (TimeoutException) { throw new QualificationException("action-timeout:" + button + ":" + shell.Status); }
            await FlushAsync();
        }
        finally
        {
            shell.PropertyChanged -= Changed;
            shell.Publisher.PropertyChanged -= Changed;
        }
    }

    private static void Review(MainWindow window, DesktopShellViewModel shell, bool import = false)
    {
        shell.Navigation.ReviewCommand.Execute(null);
        window.UpdateLayout();
        if (import)
        {
            window.GetNamedControl<Expander>("OptionalImport").IsExpanded = true;
            window.UpdateLayout();
        }
    }

    private static void Select(MainWindow window, DesktopShellViewModel shell, string id)
    {
        Review(window, shell);
        foreach (string selected in shell.Publisher.SelectedArtifactIds.Where(selected => selected != id).ToArray())
            Include(window, selected, false);
        Include(window, id, true);
    }

    private static void SelectChoice(MainWindow window, DesktopShellViewModel shell, string fieldId, string value)
    {
        JsonElement choices = shell.Workspace.Form!.Definition.Fields.Single(field => field.Id == fieldId)
            .Node.Keywords.GetProperty("enum");
        int index = choices.EnumerateArray().Select((choice, position) => (choice, position))
            .Single(item => item.choice.ValueKind == JsonValueKind.String && item.choice.GetString() == value).position;
        FieldViewModel field = shell.Workspace.Fields.Single(field => field.Id == fieldId);
        field.RequestFocus();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        Input<ComboBox>(window, field).SelectedIndex = index + 1;
    }

    private static Control CommandButton(Control owner, System.Windows.Input.ICommand command)
    {
        Button? button = owner.GetLogicalDescendants().OfType<Button>().SingleOrDefault(button => ReferenceEquals(button.Command, command));
        if (button is not null)
        {
            if (owner is MainWindow fieldWindow && fieldWindow.DataContext is DesktopShellViewModel fieldShell &&
                button.DataContext is FieldViewModel)
            {
                fieldShell.Navigation.ConfigureCommand.Execute(null);
                fieldWindow.UpdateLayout();
            }
            return button;
        }
        MenuItem header = owner.GetLogicalDescendants().OfType<MenuItem>().Single(item =>
            item.Items.OfType<MenuItem>().Any(action => ReferenceEquals(action.Command, command)));
        if (owner is MainWindow window && window.DataContext is DesktopShellViewModel shell &&
            header.DataContext is FieldViewModel)
        {
            shell.Navigation.ConfigureCommand.Execute(null);
            window.UpdateLayout();
        }
        header.IsSubMenuOpen = true;
        Dispatcher.UIThread.RunJobs();
        return header.Items.OfType<MenuItem>().Single(item => ReferenceEquals(item.Command, command));
    }

    private static void Include(MainWindow window, string id, bool selected)
    {
        Review(window, (DesktopShellViewModel)window.DataContext!);
        CheckBox control = window.GetLogicalDescendants().OfType<CheckBox>()
            .Single(control => control.DataContext is ArtifactSelectionViewModel artifact && artifact.Id == id);
        control.IsChecked = selected;
        control.RaiseEvent(new RoutedEventArgs(CheckBox.ClickEvent));
    }

    private static async Task VerifyJsonAsync(string path, string expected)
    {
        using var actual = JsonDocument.Parse(await File.ReadAllBytesAsync(path));
        using var specification = JsonDocument.Parse(expected);
        Require(JsonElement.DeepEquals(actual.RootElement, specification.RootElement), "output-bytes");
    }

    /// <summary>Captures only synthetic fixture workspaces, never operator inputs or picker contents.</summary>
    private static async Task CaptureAsync(MainWindow window, string state, Popup? activePopup = null)
    {
        if (_captureDirectory is null) return;
        double originalWidth = window.Width;
        double originalHeight = window.Height;
        ThemeVariant? originalTheme = window.RequestedThemeVariant;
        var scroll = window.GetNamedControl<ScrollViewer>("WorkspaceScroll");
        Vector originalOffset = scroll.Offset;
        foreach (ThemeVariant theme in new[] { ThemeVariant.Light, ThemeVariant.Dark, DesktopTheme.HighContrast })
        {
            window.RequestedThemeVariant = theme;
            foreach (var (width, height, name) in new[] { (960, 800, "desktop"), (600, 540, "minimum"), (1440, 1000, "wide") })
            {
                var resized = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                void SizeChanged(object? sender, WindowResizedEventArgs args)
                {
                    if (args.ClientSize.Width == width && args.ClientSize.Height == height)
                        resized.TrySetResult();
                }
                window.Resized += SizeChanged;
                try
                {
                    window.Width = width;
                    window.Height = height;
                    if (window.ClientSize.Width == width && window.ClientSize.Height == height)
                        resized.TrySetResult();
                    try { await resized.Task.WaitAsync(TimeSpan.FromSeconds(10)); }
                    catch (TimeoutException) { throw new QualificationException("capture-resize-timeout"); }
                    window.UpdateLayout();
                    await FlushAsync();
                    foreach (string position in state.StartsWith("typed-editing-", StringComparison.Ordinal) ||
                        state is "selected-output-review" or "selected-output-save-result"
                        ? new[] { "", "middle", "end" } : new[] { "", "end" })
                    {
                        double distance = Math.Max(0, scroll.Extent.Height - scroll.Viewport.Height);
                        scroll.Offset = new Vector(0, position == "end" ? distance : 0);
                        if (position == "middle")
                        {
                            Control anchorControl = state.StartsWith("typed-editing-", StringComparison.Ordinal)
                                ? Input<TextBox>(window, "Fixed caption")
                                : window.GetNamedControl<ComboBox>("ImportPurpose");
                            Require(anchorControl.IsEffectivelyVisible, "capture-middle-control-hidden");
                            Point anchor = anchorControl.TranslatePoint(default, scroll)
                                ?? throw new QualificationException("capture-map-anchor");
                            scroll.Offset = new Vector(0, Math.Max(0, scroll.Offset.Y + anchor.Y - 60));
                        }
                        var rendered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                        window.RequestAnimationFrame(_ => rendered.TrySetResult());
                        await rendered.Task.WaitAsync(TimeSpan.FromSeconds(10));
                        double scale = window.RenderScaling;
                        var pixels = new PixelSize((int)Math.Ceiling(window.ClientSize.Width * scale),
                            (int)Math.Ceiling(window.ClientSize.Height * scale));
                        string captureName = $"{state}-{name}-{theme.Key}{(position.Length == 0 ? "" : "-" + position)}";
                        double rasterDpi;
                        using (var bitmap = new RenderTargetBitmap(pixels, new Vector(96 * scale, 96 * scale)))
                        {
                            bitmap.Render(window);
                            rasterDpi = bitmap.Dpi.X;
                            bitmap.Save(Path.Combine(_captureDirectory, captureName + ".png"), PngBitmapEncoderOptions.Default);
                        }
                        if (activePopup is { Child: Control popupChild })
                        {
                            Require(activePopup.IsOpen && popupChild.Bounds.Width > 0 && popupChild.Bounds.Height > 0,
                                "capture-popup-not-rendered");
                            TopLevel popupRoot = TopLevel.GetTopLevel(popupChild) ??
                                throw new QualificationException("capture-popup-root-missing");
                            var popupRendered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                            popupRoot.RequestAnimationFrame(_ => popupRendered.TrySetResult());
                            await popupRendered.Task.WaitAsync(TimeSpan.FromSeconds(10));
                            double popupScale = popupRoot.RenderScaling;
                            using var popupBitmap = new RenderTargetBitmap(
                                new PixelSize((int)Math.Ceiling(popupRoot.ClientSize.Width * popupScale),
                                    (int)Math.Ceiling(popupRoot.ClientSize.Height * popupScale)),
                                new Vector(96 * popupScale, 96 * popupScale));
                            popupBitmap.Render(popupRoot);
                            popupBitmap.Save(Path.Combine(_captureDirectory, captureName + "-popup.png"),
                                PngBitmapEncoderOptions.Default);
                        }
                        double FontRole(string role)
                        {
                            Require(window.TryFindResource(role, window.ActualThemeVariant, out object? value) &&
                                value is double, "capture-font-role-missing");
                            return (double)value!;
                        }
                        File.WriteAllText(Path.Combine(_captureDirectory, captureName + ".json"),
                            JsonSerializer.Serialize(new
                            {
                                schemaVersion = 2,
                                check = _currentCheck,
                                state,
                                viewport = new { width = window.ClientSize.Width, height = window.ClientSize.Height },
                                pixels = new { width = pixels.Width, height = pixels.Height },
                                renderScale = scale,
                                rasterDpi,
                                uiCulture = CultureInfo.CurrentUICulture.Name,
                                definitionLocale = (window.DataContext as DesktopShellViewModel)?.Workspace.SelectedLocale,
                                appearance = window.ActualThemeVariant.Key.ToString(),
                                appearancePreference = (window.DataContext as DesktopShellViewModel)?.AppearanceIndex,
                                bodyFontSize = FontRole("ConfiguratorBodyFontSize"),
                                headingFontSize = FontRole("ConfiguratorHeadingFontSize"),
                                titleFontSize = FontRole("ConfiguratorTitleFontSize"),
                                textScale = FontRole("ConfiguratorBodyFontSize") / 14d,
                                scrollOffset = new { x = scroll.Offset.X, y = scroll.Offset.Y },
                                renderedTextFontSizes = window.GetLogicalDescendants().OfType<TextBlock>()
                                    .Where(control => control.IsEffectivelyVisible).Select(control => control.FontSize)
                                    .Distinct().Order().ToArray(),
                                popupCaptured = activePopup?.IsOpen == true,
                                payloadAssemblies = PayloadIdentities()
                            }));
                    }
                }
                finally { window.Resized -= SizeChanged; }
            }
        }
        window.RequestedThemeVariant = originalTheme;
        await ResizeAsync(window, new Size(originalWidth, originalHeight));
        scroll.Offset = originalOffset;
        window.UpdateLayout();
        await FlushAsync();
    }

    private static object[] PayloadIdentities() => AppDomain.CurrentDomain.GetAssemblies()
        .Where(assembly => !assembly.IsDynamic &&
            assembly.GetName().Name?.StartsWith("Configurator.", StringComparison.Ordinal) == true)
        .OrderBy(assembly => assembly.GetName().Name, StringComparer.Ordinal)
        .Select(assembly =>
        {
            Require(!string.IsNullOrEmpty(assembly.Location), "payload-assembly-location-unavailable");
            using Stream payload = File.OpenRead(assembly.Location);
            return (object)new
            {
                name = assembly.GetName().Name,
                moduleVersionId = assembly.ManifestModule.ModuleVersionId,
                sha256 = Convert.ToHexStringLower(SHA256.HashData(payload))
            };
        }).ToArray();

    private static void RequireSafe(string? value, string privatePath) => Require(value is null ||
        (!value.Contains(privatePath, StringComparison.Ordinal) && !value.Contains("Synthetic private owner", StringComparison.Ordinal)), "sensitive-feedback");
    private static StackPanel Editor(MainWindow window, string title) =>
        window.GetLogicalDescendants().OfType<StackPanel>()
            .First(item => item.DataContext is FieldViewModel model && model.Title == title);
    private static bool IsEditorVisible(MainWindow window, string title) =>
        window.GetLogicalDescendants().OfType<StackPanel>().Any(item =>
            item.DataContext is FieldViewModel model && model.Title == title && item.IsEffectivelyVisible);
    private static StackPanel RevealField(MainWindow window, string id)
    {
        var shell = (DesktopShellViewModel)window.DataContext!;
        shell.Workspace.Fields.Single(field => field.Id == id).RequestFocus();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        return window.GetLogicalDescendants().OfType<StackPanel>()
            .First(item => item.DataContext is FieldViewModel model && model.Id == id);
    }
    private static T Input<T>(MainWindow window, string title) where T : Control
    {
        ((DesktopShellViewModel)window.DataContext!).Navigation.ConfigureCommand.Execute(null);
        window.UpdateLayout();
        return Named<T>(window, title);
    }
    private static T Input<T>(Control owner, FieldViewModel field) where T : Control
    {
        T[] controls = owner.GetLogicalDescendants().OfType<T>()
            .Where(control => ReferenceEquals(control.DataContext, field) &&
                AutomationProperties.GetName(control) == field.Name).ToArray();
        Require(controls.Length == 1, (controls.Length == 0 ? "field-input-missing:" : "field-input-ambiguous:") + _currentStep);
        return controls[0];
    }
    private static void SetBoolean(Control owner, string title, bool value)
    {
        Control[] inputs = owner.GetLogicalDescendants().OfType<Control>().Where(control =>
            control is ComboBox or TextBox && control.DataContext is BooleanFieldViewModel &&
            AutomationProperties.GetName(control) == title && control.IsEffectivelyVisible).ToArray();
        Require(inputs.Length == 1, inputs.Length == 0 ? "boolean-editor-missing" : "boolean-editor-ambiguous");
        if (inputs[0] is ComboBox choice) choice.SelectedIndex = value ? 1 : 2;
        else
        {
            var masked = (TextBox)inputs[0];
            Require(masked.PasswordChar != '\0' && ((BooleanFieldViewModel)masked.DataContext!).RequiresMasking,
                "protected-boolean-unmasked");
            masked.Text = value ? "true" : "false";
        }
    }
    private static T Named<T>(Control owner, string name) where T : Control => owner.GetLogicalDescendants().OfType<T>().Single(control => AutomationProperties.GetName(control) == name);
    private static void Click(Control control)
    {
        Require(control.IsEffectivelyEnabled, "action-unavailable");
        if (control is MenuItem item)
        {
            Require(item.Command?.CanExecute(item.CommandParameter) == true, "action-disabled");
            item.Focus();
            item.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });
            return;
        }
        var button = (Button)control;
        var command = button.Command ?? throw new QualificationException("action-unbound");
        Require(command.CanExecute(button.CommandParameter), "action-disabled");
        button.Focus();
        command.Execute(button.CommandParameter);
    }
    private static async Task FlushAsync() => await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
    private static void Require(bool condition, string code)
    {
        if (!condition) throw new QualificationException(code);
    }
    private static void Pass()
    {
        Require(Completed.Add(_currentCheck), "duplicate-check");
        Console.WriteLine($"native-check:{_currentCheck}:passed");
    }
    private static int Reject(string code)
    {
        Console.Error.WriteLine($"native-qualification:{code}");
        return 1;
    }

    private sealed class QualificationException(string code) : Exception
    {
        public string Code { get; } = code;
    }
}
