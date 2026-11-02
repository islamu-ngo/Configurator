namespace Configurator.Desktop.Tests;

using CommunityToolkit.Mvvm.Messaging;
using Configurator.Desktop.Messages;

public sealed class DesktopStatusGuidanceTests
{
    [Test]
    public async Task DiagnosticTextCannotChooseCorrectionGuidanceAndStaleMessagesCannotReplaceIt()
    {
        var messenger = new WeakReferenceMessenger();
        using var shell = DesktopTestContext.Root(messenger: messenger);
        var notifications = new List<string?>();
        shell.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);
        messenger.Send(new StatusChangedMessage("save-complete", Guidance: DesktopStatusGuidance.SelectionNonlocal));
        string nonlocal = shell.StatusHelp;
        messenger.Send(new StatusChangedMessage("arbitrary:diagnostic:identifiers", Guidance: DesktopStatusGuidance.SelectionNonlocal));
        await Assert.That(shell.StatusHelp).IsEqualTo(nonlocal);
        messenger.Send(new StatusChangedMessage("arbitrary:diagnostic:identifiers", Guidance: DesktopStatusGuidance.SaveFailed));
        await Assert.That(shell.StatusHelp == nonlocal).IsFalse();
        await Assert.That(notifications.Contains(nameof(shell.StatusHelp))).IsTrue();
        shell.Cancel();
        string cancelled = shell.StatusHelp;
        messenger.Send(new StatusChangedMessage("late-completion", 0, DesktopStatusGuidance.SelectionNonlocal));
        await Assert.That(shell.Status).IsEqualTo("operation-cancelled");
        await Assert.That(shell.StatusHelp).IsEqualTo(cancelled);
    }
}
