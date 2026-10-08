namespace Configurator.Terminal.Tests;

using System.Drawing;
using System.Globalization;
using CommunityToolkit.Mvvm.Messaging;
using Configurator.Artifacts;
using Configurator.Core.Definitions;
using Configurator.Presentation;
using Configurator.Terminal;
using global::Terminal.Gui.App;
using global::Terminal.Gui.Input;
using global::Terminal.Gui.ViewBase;
using global::Terminal.Gui.Views;

public sealed class SetupTerminalGuiAdapterTests
{
    [Test]
    [NotInParallel]
    public async Task EnglishAndArabicResourcesResolveAllCurrentControls()
    {
        CultureInfo previous = CultureInfo.CurrentUICulture;
        try
        {
            string[] keys =
            [
                "WindowTitle", "OutputFile", "Close", "Ready", "Limitations", "TerminalTooSmall",
                "InvalidSecret", "SecretCommandsDisabled", "InvalidOutput", "Cancelled", "Writing",
                "DisplayMode", "ModeGuided", "ModeAdvanced", "ModeAllDeclared", "Choose", "Previous",
                "Next", "ApplyField", "PreviousItem", "NextItem", "RemoveItem", "Artifact",
                "PreparePreview", "SaveProtected", "PreviewReady", "DraftInvalid",
                "ProtectedDraftSaved", "ProtectedDraftFailed", "ValidateDraft", "ClearPrivate",
                "PrivateCleared", "InputChanged", "NoFields"
            ];
            foreach (string culture in new[] { "en", "ar" })
            {
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
                await Assert.That(keys.All(key => SetupTerminalText.Get(key) != key
                    && !string.IsNullOrWhiteSpace(SetupTerminalText.Get(key)))).IsTrue();
            }
        }
        finally { CultureInfo.CurrentUICulture = previous; }
    }

    [Test]
    public async Task SmallViewportHidesEditingAndNormalViewportRestoresNativeControls()
    {
        using var harness = new Harness();
        var window = harness.Window;
        window.Frame = new Rectangle(0, 0, 40, 12);
        window.ApplyViewportPolicy(window.Frame.Size);
        await Assert.That(window.SubViews.Where(view => view.CanFocus && view.Visible)).IsEmpty();
        await Assert.That(window.SubViews.OfType<Label>().Any(view =>
            view.Visible && view.Text == SetupTerminalText.Get("TerminalTooSmall"))).IsTrue();
        window.Frame = new Rectangle(0, 0, 100, 30);
        window.ApplyViewportPolicy(window.Frame.Size);
        foreach (View view in window.SubViews)
            await Assert.That(view.SetRelativeLayout(window.Viewport.Size)).IsTrue();
        await Assert.That(window.PublicInput.Visible).IsTrue();
        await Assert.That(window.MaskedInput.Visible).IsFalse();
        await Assert.That(window.SubViews.OfType<Button>().Where(view => view.Visible)
            .All(button => button.CanFocus)).IsTrue();
        View[] focusable = window.SubViews.Where(view => view.CanFocus && view.Visible).ToArray();
        await Assert.That(focusable.SelectMany((left, index) => focusable.Skip(index + 1)
            .Select(right => Rectangle.Intersect(left.Frame, right.Frame)))
            .All(rectangle => rectangle.Width == 0 || rectangle.Height == 0)).IsTrue();
    }

    [Test]
    [NotInParallel]
    public async Task NativeButtonsDoNotOverwriteAdjacentStatusOrArtifactLabels()
    {
        using var harness = new Harness(initialize: true);
        harness.Window.BeginInit();
        harness.Window.EndInit();
        harness.Window.Frame = new Rectangle(0, 0, 100, 30);
        harness.Window.ApplyViewportPolicy(harness.Window.Frame.Size);
        foreach (View view in harness.Window.SubViews)
            view.SetRelativeLayout(harness.Window.Viewport.Size);
        View[] labels = harness.Window.SubViews.OfType<Label>().Where(label => label.Visible).ToArray();
        View[] buttons = harness.Window.SubViews.OfType<Button>().Where(button => button.Visible).ToArray();
        foreach (View button in buttons)
            await Assert.That(button.Viewport.Height).IsGreaterThan(0);
        string[] overlaps = buttons.SelectMany(button => labels.Where(label =>
                Rectangle.Intersect(button.Frame, label.Frame) is { Width: > 0, Height: > 0 })
            .Select(label => $"{button.Text}:{button.Frame} intersects {label.Text}:{label.Frame}")).ToArray();
        await Assert.That(overlaps).IsEmpty();
    }

    [Test]
    [NotInParallel]
    public async Task TypedKeysReachMaskedFieldAndCannotRestoreHistoryAfterNavigation()
    {
        using var harness = new Harness(initialize: true, empty: true);
        harness.Window.Frame = new Rectangle(0, 0, 100, 30);
        harness.Window.ApplyViewportPolicy(harness.Window.Frame.Size);
        foreach (View view in harness.Window.SubViews)
            view.SetRelativeLayout(harness.Window.Viewport.Size);
        await Assert.That(harness.Window.PublicInput.SetFocus()).IsTrue();
        harness.Window.PublicInput.Text = "sample";
        harness.Window.MoveField(1);
        await Assert.That(harness.Window.MaskedInput.Secret).IsTrue();
        await Assert.That(harness.Window.MaskedInput.HasFocus).IsTrue();
        foreach (char character in "privateValue")
            await Assert.That(harness.Window.MaskedInput.NewKeyDownEvent(new Key(character))).IsTrue();
        await Assert.That(harness.Window.MaskedInput.Text).DoesNotContain("privateValue");
        await Assert.That(harness.Window.CommitField()).IsTrue();
        await Assert.That(harness.Form.Snapshot().Values["token"].GetString()).IsEqualTo("privateValue");
        harness.Window.MoveField(1);
        await Assert.That(harness.Secret.Count).IsEqualTo(0);
        await Assert.That(harness.Window.MaskedInput.Text).IsEmpty();
        harness.Window.ClearPrivateState();
        await Assert.That(harness.Form.Fields.Single(input => input.Definition.Id == "token").Text).IsEmpty();
        await Assert.That(harness.Window.MaskedInput.InvokeCommand(Command.Undo)).IsTrue();
        await Assert.That(harness.Window.MaskedInput.Text).IsEmpty();
    }

    [Test]
    [NotInParallel]
    public async Task NativePrepareAndSaveButtonsUseTheSharedWorkspace()
    {
        if (!OperatingSystem.IsLinux()) return;
        string directory = Path.Combine(Path.GetTempPath(), "configurator-native-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            using var harness = new Harness(initialize: true, directory: directory);
            var settled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            harness.Workspace.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(SetupPresentationWorkspace.IsBusy) && !harness.Workspace.IsBusy)
                    settled.TrySetResult();
            };
            Button prepare = harness.Window.SubViews.OfType<Button>().Single(button => button.Text == SetupTerminalText.Get("PreparePreview"));
            Button save = harness.Window.SubViews.OfType<Button>().Single(button => button.Text == SetupTerminalText.Get("SaveProtected"));
            await Assert.That(prepare.InvokeCommand(Command.Accept)).IsTrue();
            await Assert.That(harness.Operation.IsPrepared).IsTrue();
            await Assert.That(harness.Window.StatusText).DoesNotContain("privateValue");
            await Assert.That(save.InvokeCommand(Command.Accept)).IsTrue();
            await settled.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await Assert.That(await File.ReadAllTextAsync(Path.Combine(directory, "config.json")))
                .IsEqualTo("""{"name":"sample","token":"privateValue"}""");
            await Assert.That(harness.Secret.Count).IsEqualTo(0);
        }
        finally { Directory.Delete(directory, true); }
    }

    [Test]
    [NotInParallel]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ClearOrSignalCancelsInFlightWriteBeforeCommit(bool signal)
    {
        if (!OperatingSystem.IsLinux()) return;
        string directory = Path.Combine(Path.GetTempPath(), "configurator-cancel-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            using var harness = new Harness(initialize: true, directory: directory,
                beforeCommit: async token => { reached.TrySetResult(); await resume.Task.WaitAsync(token); });
            await Assert.That(await harness.Operation.PrepareAsync()).IsTrue();
            Task execution = harness.Workspace.ExecuteAsync(Guid.NewGuid());
            await reached.Task.WaitAsync(TimeSpan.FromSeconds(5));
            if (signal) harness.Window.RequestStopFromSignal();
            else harness.Window.ClearPrivateState();
            await execution.WaitAsync(TimeSpan.FromSeconds(5));
            await Assert.That(harness.Workspace.IsBusy).IsFalse();
            await Assert.That(harness.Operation.IsPrepared).IsFalse();
            await Assert.That(harness.Form.Fields.Single(input => input.Definition.Id == "token").Text).IsEmpty();
            await Assert.That(harness.Secret.Count).IsEqualTo(0);
            await Assert.That(Directory.EnumerateFiles(directory)).IsEmpty();
        }
        finally { Directory.Delete(directory); }
    }

    [Test]
    public async Task UncommittedFieldEditsInvalidatePreparedArtifactImmediately()
    {
        using var harness = new Harness();
        await Assert.That(await harness.Operation.PrepareAsync()).IsTrue();
        harness.Window.PublicInput.Text = "changed but not applied";
        await Assert.That(harness.Operation.IsPrepared).IsFalse();
        await Assert.That(harness.Workspace.Output).IsEmpty();
    }

    private sealed class Harness : IDisposable
    {
        private readonly IApplication _application;
        private readonly SetupPresentationSession _session;
        internal Harness(bool initialize = false, bool empty = false, string? directory = null,
            Func<CancellationToken, Task>? beforeCommit = null)
        {
            _application = Application.Create();
            if (initialize) _application.Init("dotnet");
            Form = empty ? new DefinitionFormModel(TerminalDefinitionFixture.Definition()) : TerminalDefinitionFixture.Form();
            Secret = new SetupTerminalSecretBuffer(262144, urlSafeOnly: false);
            Operation = new SetupTerminalArtifactOperation(Form, new ProtectedArtifactWriter(), directory ?? Path.GetTempPath(), beforeCommit);
            _session = new SetupPresentationSession(new StrongReferenceMessenger());
            SetupWorkspaceId.TryCreate("definition", out var id);
            Workspace = _session.CreateWorkspace(id, Operation);
            Workspace.Activate();
            Window = new SetupTerminalWindow(_application, Workspace, Operation, Secret);
            Window.Frame = new Rectangle(0, 0, 100, 30);
            Window.ApplyViewportPolicy(Window.Frame.Size);
        }
        internal DefinitionFormModel Form { get; }
        internal SetupTerminalSecretBuffer Secret { get; }
        internal SetupTerminalArtifactOperation Operation { get; }
        internal SetupPresentationWorkspace Workspace { get; }
        internal SetupTerminalWindow Window { get; }
        public void Dispose()
        {
            Window.Dispose();
            Workspace.Dispose();
            Operation.Dispose();
            Secret.Dispose();
            Form.Dispose();
            _session.Dispose();
            _application.Dispose();
        }
    }
}
