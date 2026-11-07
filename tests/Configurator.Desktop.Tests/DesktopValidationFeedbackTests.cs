namespace Configurator.Desktop.Tests;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Configurator.Desktop.ViewModels.Fields;

public sealed class DesktopValidationFeedbackTests
{
    public static AppBuilder BuildAvaloniaApp() => DesktopApplicationTests.BuildAvaloniaApp();

    [Test]
    [NotInParallel]
    public async Task UntouchedRequiredFieldWaitsForValidationBeforeShowingInlineError()
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(DesktopValidationFeedbackTests));
        try
        {
            var result = await session.Dispatch(() =>
            {
                using var shell = DesktopTestContext.Root();
                shell.Loader.LoadDefinition(RequiredDefinition());
                FieldViewModel field = shell.Workspace.Fields.Single();
                var window = new Window { Content = new ContentControl { Content = field } };
                try
                {
                    window.Show();
                    window.UpdateLayout();
                    Dispatcher.UIThread.RunJobs();
                    bool ErrorVisible() => window.GetLogicalDescendants().OfType<TextBlock>()
                        .Any(control => control.Classes.Contains("error") && control.IsEffectivelyVisible);
                    bool untouched = ErrorVisible();
                    int missing = shell.Workspace.Form!.Projection.Progress.Missing;
                    shell.Workspace.Validate();
                    Dispatcher.UIThread.RunJobs();
                    return (untouched, attempted: ErrorVisible(), missing);
                }
                finally { window.Close(); }
            }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));
            await Assert.That(result.untouched).IsFalse();
            await Assert.That(result.missing).IsEqualTo(1);
            await Assert.That(result.attempted).IsTrue();
        }
        finally { await Task.Run(session.Dispose); }
    }

    [Test]
    [NotInParallel]
    public async Task NativeBlurRevealsGuidanceOnlyAfterEditing()
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(DesktopValidationFeedbackTests));
        try
        {
            var result = await session.Dispatch(() =>
            {
                using var shell = DesktopTestContext.Root();
                shell.Loader.LoadDefinition(RequiredDefinition());
                FieldViewModel field = shell.Workspace.Fields.Single();
                var next = new Button { Content = "Next" };
                var window = new Window
                {
                    Content = new StackPanel
                    {
                        Children = { new ContentControl { Content = field }, next }
                    }
                };
                try
                {
                    window.Show();
                    window.UpdateLayout();
                    Dispatcher.UIThread.RunJobs();
                    TextBox editor = window.GetLogicalDescendants().OfType<TextBox>()
                        .Single(control => control.IsEffectivelyVisible);
                    editor.Focus();
                    next.Focus();
                    bool untouchedBlur = field.ShowError;
                    editor.Focus();
                    editor.Text = "valid";
                    editor.Text = "";
                    bool whileEditing = field.ShowError;
                    next.Focus();
                    Dispatcher.UIThread.RunJobs();
                    return (untouchedBlur, whileEditing, afterBlur: field.ShowError,
                        stillInvalid: field.HasError, draft: editor.Text);
                }
                finally { window.Close(); }
            }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));
            await Assert.That(result.untouchedBlur).IsFalse();
            await Assert.That(result.whileEditing).IsFalse();
            await Assert.That(result.afterBlur).IsTrue();
            await Assert.That(result.stillInvalid).IsTrue();
            await Assert.That(result.draft).IsEqualTo("");
        }
        finally { await Task.Run(session.Dispose); }
    }

    private static byte[] RequiredDefinition() => """
        {"$schema":"urn:islamu:configurator:authoring:v0.0.1",
         "identity":{"familyId":"a96d2f6c-cb33-4c94-9315-5910c57060e8","revision":1},
         "project":{"id":"feedback","name":"Feedback"},"applicationVersions":["1"],
         "configuration":{"type":"object","additionalProperties":false,"required":["name"],
           "x-configurator":{"sensitivity":"public"},"properties":{
             "name":{"type":"string","minLength":1,"title":"Required setting",
               "x-configurator":{"sensitivity":"public","detail":"basic"}}}},
         "outputs":{"config.json":{"format":"json"}}}
        """u8.ToArray();
}
