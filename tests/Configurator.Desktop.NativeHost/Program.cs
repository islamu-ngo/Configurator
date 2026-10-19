namespace Configurator.Desktop.NativeHost;

using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Configurator.Core.Definitions;
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
        "D09-close-disposes-workspace"
    ];
    private static readonly HashSet<string> Completed = new(StringComparer.Ordinal);
    private static string _currentCheck = "native-startup";
    private static string? _captureDirectory;

    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Length is not (4 or 6) || args[0] != "--expected-rid" || args[1] != "linux-x64" ||
            args[2] != "--minimum-expected-checks" ||
            !int.TryParse(args[3], NumberStyles.None, CultureInfo.InvariantCulture, out int minimum) ||
            minimum < 10 || minimum > RequiredChecks.Length)
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

        // A stalled startup or UI dispatcher must also fail within a bounded wall-clock interval.
        using var watchdog = new Timer(_ =>
        {
            Console.Error.WriteLine("native-qualification:timeout");
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
                    Console.WriteLine($"native-qualification:passed:{Completed.Count}:linux-x64:X11");
                    Console.WriteLine("qualification-limits:manual-dialogs-keyboard-rtl-scaling-assistive-technology-and-published-payload-required");
                    code = 0;
                }
                catch (QualificationException exception)
                {
                    Console.Error.WriteLine($"native-qualification:failed:{_currentCheck}:{exception.Code}");
                }
                catch (SetupDefinitionException exception)
                {
                    Console.Error.WriteLine($"native-qualification:failed:{_currentCheck}:{exception.Code}");
                }
                catch (TimeoutException)
                {
                    Console.Error.WriteLine($"native-qualification:failed:{_currentCheck}:operation-timeout");
                }
                catch (Exception)
                {
                    Console.Error.WriteLine($"native-qualification:failed:{_currentCheck}");
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

    private static async Task RunAsync(MainWindow window, DesktopShellViewModel shell)
    {
        FormWorkspaceViewModel workspace = shell.Workspace;
        ArtifactPublisherViewModel publisher = shell.Publisher;
        string directory = Directory.CreateTempSubdirectory("configurator-native-").FullName;
        try
        {
            _currentCheck = "D01-offline-intake";
            Require(workspace.Form is null && !publisher.CanPrepare && !publisher.CanSave, "default-target");
            string invalid = Path.Combine(directory, "invalid.json");
            await File.WriteAllTextAsync(invalid, "{}");
            await LoadAsync(window, shell, invalid, false);
            _ = SetupDefinitionCodec.ParseJson(await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "NativeDefinition.json")));
            await LoadAsync(window, shell, Path.Combine(AppContext.BaseDirectory, "NativeDefinition.json"), true);
            Require(shell.Loader.DefinitionPin.Contains(workspace.Form!.Definition.Digest.Value, StringComparison.Ordinal), "pin-missing");
            window.GetControl<TextBox>("OutputDirectory").Text = directory;
            Require(window.GetControl<ComboBox>("Artifact").SelectedIndex >= 0 &&
                window.GetControl<ComboBox>("Artifact").SelectionBoxItem is not null, "artifact-label-missing");
            await CaptureAsync(window, "loaded");
            Pass();

            _currentCheck = "D02-typed-controls";
            Input<TextBox>(window, "Service name").Text = "Native service";
            Input<TextBox>(window, "Owner name").Text = "Synthetic private owner";
            Input<TextBox>(window, "Owner age").Text = "23";
            Click(Named<Button>(window, "Add item to Hosts"));
            await FlushAsync();
            Input<TextBox>(window, "Hosts item").Text = "native.example";
            await FlushAsync();
            var values = workspace.Form!.Snapshot().Values;
            Require(values["owner"].GetProperty("age").GetInt32() == 23 &&
                values["hosts"][0].GetString() == "native.example" &&
                Input<TextBox>(window, "Owner name").PasswordChar != '\0', "typed-answer");
            Click(Named<Button>(window, "Remove item from Hosts"));
            await FlushAsync();
            Require(workspace.Form.Snapshot().Values["hosts"].GetArrayLength() == 0, "list-removal");
            Click(Named<Button>(window, "Add item to Hosts"));
            await FlushAsync();
            Input<TextBox>(window, "Hosts item").Text = "native.example";
            await FlushAsync();
            Pass();

            _currentCheck = "D03-activation-disclosure";
            Require(!Editor(window, "Port").IsVisible && !Editor(window, "Advanced note").IsVisible, "inactive-disclosure");
            Input<ComboBox>(window, "Mode").SelectedItem = "remote";
            await FlushAsync();
            Require(Editor(window, "Port").IsVisible, "activation");
            window.GetControl<ComboBox>("DisplayMode").SelectedIndex = (int)DesktopDisplayMode.Advanced;
            await FlushAsync();
            Input<TextBox>(window, "Advanced note").Text = "too-long-note";
            await FlushAsync();
            window.GetControl<ComboBox>("DisplayMode").SelectedIndex = (int)DesktopDisplayMode.Essential;
            await FlushAsync();
            Require(Editor(window, "Advanced note").IsVisible && Editor(window, "Owner").IsVisible, "invalid-hidden");
            Input<TextBox>(window, "Advanced note").Text = "";
            Input<ComboBox>(window, "Mode").SelectedItem = "local";
            await FlushAsync();
            await CaptureAsync(window, "edited");
            Pass();

            _currentCheck = "D04-json-yaml-dotenv-import";
            foreach (var (id, file, content) in new[]
            {
                ("config", "import.json", "{\"name\":\"JSON imported\"}"),
                ("yaml", "import.yaml", "name: YAML imported\n"),
                ("environment", "import.env", "NAME=dotenv-imported\n")
            })
            {
                Select(window, shell, id);
                Click(Named<Button>(window, "Clear Service name"));
                await FlushAsync();
                string path = Path.Combine(directory, file);
                await File.WriteAllTextAsync(path, content);
                window.GetControl<TextBox>("ExistingFilePath").Text = path;
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
            Input<TextBox>(window, "Owner age").Text = "invalid-integer";
            await FlushAsync();
            await ActionAsync(window, shell, "Prepare", () => shell.Status.StartsWith("validation-invalid", StringComparison.Ordinal));
            Require(!publisher.CanSave && workspace.Form!.HasInputErrors, "nested-invalid-omitted");
            Input<TextBox>(window, "Owner age").Text = "23";
            await FlushAsync();
            foreach (string id in publisher.SelectedArtifactIds.ToArray()) Include(window, id, false);
            Click(window.GetControl<Button>("PrepareBundle"));
            Require(!publisher.CanSaveBundle, "empty-selection-ready");
            Include(window, "remote", true);
            Click(window.GetControl<Button>("PrepareBundle"));
            Require(!publisher.CanSaveBundle, "inactive-selection-ready");
            Include(window, "remote", false);
            Include(window, "config", true);
            await PrepareAsync(window, shell);
            Require(publisher.Preparation!.DefinitionDigest == workspace.Form!.Definition.Digest, "preparation-pin");
            Pass();

            _currentCheck = "D06-protected-single-conflict";
            await ActionAsync(window, shell, "Save", () => !shell.IsSaving && shell.Status.StartsWith("save-", StringComparison.Ordinal));
            string target = Path.Combine(directory, "service.json");
            await VerifyJsonAsync(target, """{"name":"dotenv-imported","owner":{"name":"Synthetic private owner","age":23},"hosts":["native.example"]}""");
            Require(OperatingSystem.IsLinux() && File.GetUnixFileMode(target) == (UnixFileMode.UserRead | UnixFileMode.UserWrite), "single-permissions");
            byte[] original = await File.ReadAllBytesAsync(target);
            await PrepareAsync(window, shell);
            await ActionAsync(window, shell, "Save", () => !shell.IsSaving && shell.Status.StartsWith("save-", StringComparison.Ordinal));
            Require((await File.ReadAllBytesAsync(target)).AsSpan().SequenceEqual(original) && !publisher.CanSave, "overwrite-or-retained-save");
            Pass();

            _currentCheck = "D07-bundle-preflight-recovery";
            Include(window, "environment", true);
            Click(window.GetControl<Button>("PrepareBundle"));
            Require(publisher.CanSaveBundle, "bundle-not-ready");
            await ActionAsync(window, shell, "SaveBundle", () => !shell.IsSaving && shell.Status.StartsWith("bundle-save-", StringComparison.Ordinal));
            Require(!File.Exists(Path.Combine(directory, "service.env")) && (await File.ReadAllBytesAsync(target)).AsSpan().SequenceEqual(original), "bundle-partial-preflight");
            File.Delete(target);
            Click(window.GetControl<Button>("PrepareBundle"));
            await ActionAsync(window, shell, "SaveBundle", () => !shell.IsSaving && shell.Status.StartsWith("bundle-save-", StringComparison.Ordinal));
            await VerifyJsonAsync(target, """{"name":"dotenv-imported","owner":{"name":"Synthetic private owner","age":23},"hosts":["native.example"]}""");
            Require(await File.ReadAllTextAsync(Path.Combine(directory, "service.env")) == "NAME=dotenv-imported\n", "bundle-environment-bytes");
            Require(OperatingSystem.IsLinux() && File.GetUnixFileMode(Path.Combine(directory, "service.env")) == (UnixFileMode.UserRead | UnixFileMode.UserWrite) && !publisher.CanSaveBundle, "bundle-permissions");
            Pass();

            _currentCheck = "D09-edit-cancel-stale-import";
            await PrepareAsync(window, shell);
            Input<TextBox>(window, "Service name").Text = "Current edit";
            await FlushAsync();
            Require(!publisher.CanSave && !publisher.CanSaveBundle, "edit-stale-preparation");
            var delayed = new TaskCompletionSource<ReadOnlyMemory<byte>>(TaskCreationOptions.RunContinuationsAsynchronously);
            Task<bool> pending = publisher.ImportAsync(_ => delayed.Task);
            Input<TextBox>(window, "Service name").Text = "Later edit";
            await FlushAsync();
            delayed.SetResult("{\"name\":\"Stale import\"}"u8.ToArray());
            Require(!await pending.WaitAsync(TimeSpan.FromSeconds(10)) && workspace.Form!.Snapshot().Values["name"].GetString() == "Later edit", "stale-import");
            await PrepareAsync(window, shell);
            Click(window.GetControl<Button>("Cancel"));
            Require(!publisher.CanSave && !publisher.CanSaveBundle && !shell.IsSaving, "cancel-stale-preparation");
            Pass();

            _currentCheck = "D10-value-free-feedback-retry";
            window.GetControl<TextBox>("FileName").Text = "../unsafe.json";
            await PrepareAsync(window, shell);
            await ActionAsync(window, shell, "Save", () => !shell.IsSaving && shell.Status.StartsWith("save-", StringComparison.Ordinal));
            Require(!publisher.CanSave && !shell.IsSaving, "failure-not-consumed");
            foreach (Control control in window.GetLogicalDescendants().OfType<Control>())
            {
                RequireSafe(AutomationProperties.GetName(control), directory);
                RequireSafe(AutomationProperties.GetHelpText(control), directory);
            }
            RequireSafe(window.GetControl<TextBlock>("Status").Text, directory);
            RequireSafe(window.GetControl<TextBlock>("StatusHelp").Text, directory);
            window.GetControl<TextBox>("FileName").Text = "retry.json";
            await PrepareAsync(window, shell);
            Require(publisher.CanSave, "retry-unavailable");
            Click(window.GetControl<Button>("Cancel"));
            Pass();

            for (int count = 1; count <= 3; count++)
            {
                _currentCheck = count switch { 1 => "D12-event-one-tenant", 2 => "D12-event-two-tenants", _ => "D12-event-three-tenants" };
                await EventAsync(window, shell, directory, count);
                Pass();
            }

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

    private static async Task EventAsync(MainWindow window, DesktopShellViewModel shell, string directory, int count)
    {
        await LoadAsync(window, shell, Path.Combine(AppContext.BaseDirectory, "Fixtures", "Event", "definition.json"), true);
        window.GetControl<ComboBox>("DisplayMode").SelectedIndex = (int)DesktopDisplayMode.Advanced;
        Input<ComboBox>(window, "Artifact to prepare").SelectedItem = "configuration-manifest";
        await FlushAsync();
        var name = Field(window, "configuration-manifest.metadata.name");
        name.GetLogicalDescendants().OfType<TextBox>().Single().Text = "qa-config";
        var settings = Field(window, "configuration-manifest.spec.instance.settings");
        SetBoolean(settings, "modules.tech_enabled", true);
        var tenants = Field(window, "configuration-manifest.tenants");
        for (int index = 0; index < count; index++)
        {
            Click(Named<Button>(tenants, "Add item to Tenants"));
            await FlushAsync();
            tenants.GetLogicalDescendants().OfType<TextBox>().Where(control => AutomationProperties.GetName(control) == "Tenant identifier").ElementAt(index).Text = "tenant-" + index.ToString(CultureInfo.InvariantCulture);
            tenants.GetLogicalDescendants().OfType<TextBox>().Where(control => AutomationProperties.GetName(control) == "Display name").ElementAt(index).Text = "Tenant " + index.ToString(CultureInfo.InvariantCulture);
        }
        await FlushAsync();
        Select(window, shell, "configuration-manifest");
        foreach (string id in shell.Publisher.SelectedArtifactIds.ToArray()) Include(window, id, false);
        Include(window, "configuration-manifest", true);
        window.GetControl<TextBox>("OutputDirectory").Text = directory;
        window.GetControl<TextBox>("FileName").Text = "event-" + count.ToString(CultureInfo.InvariantCulture) + ".json";
        await PrepareAsync(window, shell);
        await ActionAsync(window, shell, "Save", () => !shell.IsSaving && shell.Status.StartsWith("save-", StringComparison.Ordinal));
        string output = Path.Combine(directory, "event-" + count.ToString(CultureInfo.InvariantCulture) + ".json");
        using var document = JsonDocument.Parse(await File.ReadAllBytesAsync(output));
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
        Click(window.GetControl<Button>("PrepareBundle"));
        Require(shell.Publisher.CanSaveBundle && shell.Publisher.SelectedArtifactIds.SequenceEqual(["configuration-manifest"]), "event-active-outputs");
        Click(window.GetControl<Button>("Cancel"));
    }

    private static async Task LoadAsync(MainWindow window, DesktopShellViewModel shell, string path, bool success)
    {
        window.GetControl<TextBox>("DefinitionPath").Text = path;
        await ActionAsync(window, shell, "LoadDefinition", () => success ? shell.Status == "definition-loaded" :
            shell.Status.StartsWith("definition-invalid", StringComparison.Ordinal) || shell.Status == "definition-unreadable");
        Require((shell.Workspace.Form is not null) == success && !shell.Publisher.CanSave, "definition-admission");
        await FlushAsync();
    }

    private static async Task PrepareAsync(MainWindow window, DesktopShellViewModel shell)
    {
        await ActionAsync(window, shell, "Prepare", () => (shell.Publisher.CanSave && shell.Status.StartsWith("artifact-ready:", StringComparison.Ordinal)) ||
            shell.Status.StartsWith("validation-invalid", StringComparison.Ordinal));
        Require(shell.Publisher.CanSave, "prepare-failed:" + shell.Status);
    }

    private static async Task ActionAsync(MainWindow window, DesktopShellViewModel shell, string button, Func<bool> finished)
    {
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
            Click(window.GetControl<Button>(button));
            if (finished()) completion.TrySetResult();
            try { await completion.Task.WaitAsync(TimeSpan.FromSeconds(10)); }
            catch (TimeoutException) { throw new QualificationException("action-timeout:" + button); }
            await FlushAsync();
        }
        finally
        {
            shell.PropertyChanged -= Changed;
            shell.Publisher.PropertyChanged -= Changed;
        }
    }

    private static void Select(MainWindow window, DesktopShellViewModel shell, string id) =>
        window.GetControl<ComboBox>("Artifact").SelectedItem = shell.Publisher.Artifacts.Single(item => item.Id == id);

    private static void Include(MainWindow window, string id, bool selected)
    {
        CheckBox control = Named<CheckBox>(window, $"Include {id} in bundle");
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
    private static async Task CaptureAsync(MainWindow window, string state)
    {
        if (_captureDirectory is null) return;
        double originalWidth = window.Width;
        double originalHeight = window.Height;
        ThemeVariant? originalTheme = window.RequestedThemeVariant;
        var scroll = window.GetControl<ScrollViewer>("WorkspaceScroll");
        Vector originalOffset = scroll.Offset;
        foreach (ThemeVariant theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
        {
            window.RequestedThemeVariant = theme;
            foreach (var (width, height, name) in new[] { (960, 800, "desktop"), (600, 540, "minimum") })
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
                    foreach (bool end in new[] { false, true })
                    {
                        scroll.Offset = end ? new Vector(0, scroll.Extent.Height - scroll.Viewport.Height) : default;
                        var rendered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                        window.RequestAnimationFrame(_ => rendered.TrySetResult());
                        await rendered.Task.WaitAsync(TimeSpan.FromSeconds(10));
                        using var bitmap = new RenderTargetBitmap(new PixelSize(width, height));
                        bitmap.Render(window);
                        bitmap.Save(Path.Combine(_captureDirectory, $"{state}-{name}-{theme.Key}{(end ? "-end" : "")}.png"),
                            PngBitmapEncoderOptions.Default);
                    }
                }
                finally { window.Resized -= SizeChanged; }
            }
        }
        window.RequestedThemeVariant = originalTheme;
        window.Width = originalWidth;
        window.Height = originalHeight;
        scroll.Offset = originalOffset;
        window.UpdateLayout();
        await FlushAsync();
    }

    private static void RequireSafe(string? value, string privatePath) => Require(value is null ||
        (!value.Contains(privatePath, StringComparison.Ordinal) && !value.Contains("Synthetic private owner", StringComparison.Ordinal)), "sensitive-feedback");
    private static StackPanel Editor(MainWindow window, string title) =>
        window.GetLogicalDescendants().OfType<StackPanel>()
            .First(item => item.DataContext is FieldViewModel model && model.Title == title);
    private static StackPanel Field(MainWindow window, string id) =>
        window.GetLogicalDescendants().OfType<StackPanel>()
            .First(item => item.DataContext is FieldViewModel model && model.Id == id);
    private static T Input<T>(MainWindow window, string title) where T : Control => Named<T>(window, title);
    private static void SetBoolean(Control owner, string title, bool value)
    {
        if (owner.GetLogicalDescendants().OfType<CheckBox>().SingleOrDefault(control =>
            AutomationProperties.GetName(control) == title && control.IsVisible) is { } choice)
            choice.IsChecked = value;
        else
            Named<TextBox>(owner, title).Text = value ? "true" : "false";
    }
    private static T Named<T>(Control owner, string name) where T : Control => owner.GetLogicalDescendants().OfType<T>().Single(control => AutomationProperties.GetName(control) == name);
    private static void Click(Button button)
    {
        Require(button.IsEffectivelyEnabled, "action-unavailable");
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
