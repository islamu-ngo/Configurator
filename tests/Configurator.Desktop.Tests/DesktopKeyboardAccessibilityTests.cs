namespace Configurator.Desktop.Tests;

using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Automation;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Configurator.Desktop.ViewModels;
using Configurator.Desktop.ViewModels.Fields;
using Configurator.Desktop.Views;

public sealed class DesktopKeyboardAccessibilityTests
{
    public static Avalonia.AppBuilder BuildAvaloniaApp() => DesktopApplicationTests.BuildAvaloniaApp();

    [Test]
    [NotInParallel]
    public async Task ValidationRevealsAndFocusesInvalidVariantChildFromReview()
    {
        using Stream source = typeof(DesktopKeyboardAccessibilityTests).Assembly
            .GetManifestResourceStream("Conformance.RootVariant")!;
        using var bytes = new MemoryStream();
        await source.CopyToAsync(bytes);
        var session = HeadlessUnitTestSession.StartNew(typeof(DesktopKeyboardAccessibilityTests));
        try
        {
            var result = await session.Dispatch(() =>
            {
                using var shell = DesktopTestContext.Root();
                shell.Loader.LoadDefinition(bytes.ToArray());
                var window = new MainWindow { DataContext = shell };
                try
                {
                    window.Show();
                    window.UpdateLayout();
                    Dispatcher.UIThread.RunJobs();
                    var root = (RootFieldViewModel)shell.Workspace.Fields.Single();
                    var variant = (VariantFieldViewModel)root.Editor;
                    variant.SelectedChoice = variant.Choices[1];
                    variant.Children.Single(field => field.Id == "endpoint").Text = "synthetic-queue";
                    variant.IsExpanded = false;
                    shell.Navigation.ReviewCommand.Execute(null);
                    bool invalid = !shell.Workspace.Validate();
                    window.UpdateLayout();
                    Dispatcher.UIThread.RunJobs();
                    var token = variant.Children.Single(field => field.Id == "token");
                    var input = window.GetLogicalDescendants().OfType<TextBox>()
                        .Single(control => ReferenceEquals(control.DataContext, token) &&
                            AutomationProperties.GetName(control) == token.Name);
                    return (invalid, configure: shell.Navigation.IsConfigure, expanded: variant.IsExpanded,
                        focused: input.IsFocused, masked: input.PasswordChar != '\0',
                        focusName: window.FocusManager?.GetFocusedElement() is Control focusedControl
                            ? AutomationProperties.GetName(focusedControl) : "none",
                        tokenError: token.HasError, request: token.FocusRequestVersion,
                        requestCurrent: token.IsFocusRequestCurrent,
                        ready: shell.Publisher.CanSaveSelection);
                }
                finally { window.Close(); }
            }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));
            await Assert.That(result.invalid).IsTrue();
            await Assert.That(result.configure).IsTrue();
            await Assert.That(result.expanded).IsTrue();
            await Assert.That(result.focused).IsTrue().Because(
                $"Actual focus={result.focusName}; error={result.tokenError}; request={result.request}; current={result.requestCurrent}");
            await Assert.That(result.masked).IsTrue();
            await Assert.That(result.ready).IsFalse();
        }
        finally { await Task.Run(session.Dispose); }
    }

    [Test]
    [NotInParallel]
    [Arguments("en")]
    [Arguments("ar")]
    public async Task AutomationIdentitySurvivesLocaleResizeAndTextScaleWithoutAnswerAnnouncements(string locale)
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(DesktopKeyboardAccessibilityTests));
        try
        {
            var result = await session.Dispatch(() =>
            {
                using var shell = DesktopTestContext.Root();
                shell.Loader.LoadDefinition(ViewModels.DefinitionEditingTests.Definition().CanonicalBytes);
                var window = new MainWindow { DataContext = shell };
                try
                {
                    window.Show();
                    var field = shell.Workspace.Fields.Single(item => item.Id == "text");
                    field.Text = "synthetic-answer-announcement-canary";
                    window.UpdateLayout();
                    Dispatcher.UIThread.RunJobs();
                    TextBox Editor() => window.GetLogicalDescendants().OfType<TextBox>()
                        .Single(input => ReferenceEquals(input.DataContext, field) &&
                            AutomationProperties.GetName(input) == field.Name);
                    var editor = Editor();
                    string? identity = AutomationProperties.GetAutomationId(editor);
                    shell.Workspace.SelectedLocale = locale;
                    window.Resources["ConfiguratorBodyFontSize"] = 28d;
                    window.Resources["ConfiguratorHeadingFontSize"] = 48d;
                    window.Resources["ConfiguratorTitleFontSize"] = 52d;
                    window.Width = 600;
                    window.Height = 540;
                    window.UpdateLayout();
                    Dispatcher.UIThread.RunJobs();
                    var status = window.GetNamedControl<TextBlock>("Status");
                    editor.Focus();
                    window.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
                    window.KeyRelease(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
                    bool forwardVisible = window.FocusManager?.GetFocusedElement() is Control next &&
                        !ReferenceEquals(next, editor) && next.IsEffectivelyVisible && next.IsEffectivelyEnabled;
                    window.KeyPress(Key.Tab, RawInputModifiers.Shift, PhysicalKey.Tab, null);
                    window.KeyRelease(Key.Tab, RawInputModifiers.Shift, PhysicalKey.Tab, null);
                    return (same: ReferenceEquals(editor, Editor()),
                        stable: identity == AutomationProperties.GetAutomationId(Editor()),
                        opaque: identity is not null && identity.StartsWith("field-", StringComparison.Ordinal) && !identity.Contains("text", StringComparison.Ordinal),
                        localized: !string.IsNullOrWhiteSpace(AutomationProperties.GetName(editor)),
                        polite: AutomationProperties.GetLiveSetting(status) == AutomationLiveSetting.Polite,
                        safe: status.Text?.Contains("synthetic-answer-announcement-canary", StringComparison.Ordinal) != true,
                        forwardVisible, returned: editor.IsFocused,
                        size: editor.FontSize);
                }
                finally { window.Close(); }
            }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));
            await Assert.That(result.same).IsTrue();
            await Assert.That(result.stable).IsTrue();
            await Assert.That(result.opaque).IsTrue();
            await Assert.That(result.localized).IsTrue();
            await Assert.That(result.polite).IsTrue();
            await Assert.That(result.safe).IsTrue();
            await Assert.That(result.forwardVisible).IsTrue();
            await Assert.That(result.returned).IsTrue();
            await Assert.That(result.size).IsEqualTo(28);
        }
        finally { await Task.Run(session.Dispose); }
    }

    [Test]
    [NotInParallel]
    public async Task HandledEditingKeysDoNotPrepareAndPopupEscapeDoesNotDiscardPreparation()
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(DesktopKeyboardAccessibilityTests));
        try
        {
            var result = await session.Dispatch(async () =>
            {
                using var shell = DesktopTestContext.Root();
                shell.Loader.LoadDefinition(ViewModels.DefinitionEditingTests.Definition().CanonicalBytes);
                var window = new MainWindow { DataContext = shell };
                try
                {
                    window.Show();
                    window.UpdateLayout();
                    Dispatcher.UIThread.RunJobs();
                    var editor = window.GetNamedControl<TextBox>("DefinitionPath");
                    shell.Navigation.OpenCommand.Execute(null);
                    window.UpdateLayout();
                    editor.Focus();
                    EventHandler<KeyEventArgs> consume = (_, args) => args.Handled = true;
                    editor.AddHandler(InputElement.KeyDownEvent, consume, RoutingStrategies.Bubble);
                    window.KeyPress(Key.Enter, RawInputModifiers.Control, PhysicalKey.Enter, null);
                    window.KeyRelease(Key.Enter, RawInputModifiers.Control, PhysicalKey.Enter, null);
                    bool editingWon = shell.ReviewAndPrepareCommand.ExecutionTask is null;
                    editor.RemoveHandler(InputElement.KeyDownEvent, consume);
                    await shell.ReviewAndPrepareCommand.ExecuteAsync(null);
                    bool prepared = shell.Publisher.CanSaveSelection;
                    var popup = new Popup
                    {
                        PlacementTarget = window,
                        Child = new TextBox { Text = "Synthetic popup editor" }
                    };
                    var popupOwner = (StackPanel)window.GetNamedControl<ScrollViewer>("WorkspaceScroll").Content!;
                    popupOwner.Children.Add(popup);
                    popup.Open();
                    Dispatcher.UIThread.RunJobs();
                    window.RaiseEvent(new KeyEventArgs
                    {
                        RoutedEvent = InputElement.KeyDownEvent,
                        Key = Key.Escape
                    });
                    bool popupClosed = !popup.IsOpen;
                    bool retained = shell.Publisher.CanSaveSelection;
                    popup.Close();
                    popupOwner.Children.Remove(popup);
                    window.RaiseEvent(new KeyEventArgs
                    {
                        RoutedEvent = InputElement.KeyDownEvent,
                        Key = Key.Escape
                    });
                    return (editingWon, prepared, popupClosed, retained,
                        discarded: !shell.Publisher.CanSaveSelection);
                }
                finally { window.Close(); }
            }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));
            await Assert.That(result.editingWon).IsTrue();
            await Assert.That(result.prepared).IsTrue();
            await Assert.That(result.popupClosed).IsTrue();
            await Assert.That(result.retained).IsTrue();
            await Assert.That(result.discarded).IsTrue();
        }
        finally { await Task.Run(session.Dispose); }
    }
}
