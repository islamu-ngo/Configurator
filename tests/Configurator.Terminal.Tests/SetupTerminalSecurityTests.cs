namespace Configurator.Terminal.Tests;

using System.Security.Cryptography;
using Configurator.Artifacts;
using Configurator.Terminal;
using global::Terminal.Gui.Input;

public sealed class SetupTerminalSecurityTests
{
    [Test]
    [Arguments("--secret")]
    [Arguments("--answers")]
    public async Task ProcessArgumentsCannotTransportPrivateAnswers(string argument)
    {
        await Assert.That(SetupTerminalProgram.Run([argument, Convert.ToHexString(RandomNumberGenerator.GetBytes(32))]))
            .IsEqualTo(64);
        await Assert.That(SetupTerminalDefinitionSelection.TryParse([], out _)).IsFalse();
    }

    [Test]
    [Arguments("../outside")]
    [Arguments("/tmp/private")]
    [Arguments("-")]
    [Arguments("..")]
    public async Task SaveCannotEscapeTheExplicitLocalDirectory(string name)
    {
        using var form = TerminalDefinitionFixture.Form();
        using var operation = new SetupTerminalArtifactOperation(form, new ProtectedArtifactWriter(), Path.GetTempPath())
        { FileName = name };
        await Assert.That(await operation.PrepareAsync()).IsTrue();
        var outcome = await operation.ExecuteAsync(CancellationToken.None);
        await Assert.That(((SetupTerminalArtifactResult)outcome.CoreResult).Status).IsEqualTo(ProtectedArtifactStatus.InvalidRequest);
        await Assert.That(operation.IsPrepared).IsFalse();
    }

    [Test]
    public async Task SecretBufferRejectsControlCharactersAndRedactsItsProjection()
    {
        using var secret = new SetupTerminalSecretBuffer();
        await Assert.That(secret.TryReplace("opaque\n")).IsFalse();
        await Assert.That(secret.Count).IsEqualTo(0);
        await Assert.That(secret.ToString()).Contains("Redacted", StringComparison.Ordinal);
    }

    [Test]
    public async Task UnicodeProtectedInputHonorsBoundsAndClearsCharacterStorage()
    {
        using var secret = new SetupTerminalSecretBuffer(8, urlSafeOnly: false);
        await Assert.That(secret.TryReplace("سلام")).IsTrue();
        await Assert.That(secret.TryAppend("x")).IsFalse();
        secret.RemoveLast();
        await Assert.That(secret.Count).IsEqualTo(3);
        secret.Clear();
        await Assert.That(secret.Count).IsEqualTo(0);
        await Assert.That(() => secret.CopyUtf8Bytes()).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task SecretFieldBlocksClipboardContextAndUndoHistory()
    {
        using var secret = new SetupTerminalSecretBuffer();
        using var field = new SetupSecretTextField(secret);
        await Assert.That(field.NewKeyDownEvent(new Key('c'))).IsTrue();
        await Assert.That(secret.Count).IsEqualTo(1);
        await Assert.That(field.Text).IsEqualTo("●");
        foreach (Command command in new[]
        {
            Command.Copy, Command.Cut, Command.Paste, Command.Undo, Command.Redo, Command.Context,
            Command.CutToEndOfLine, Command.CutToStartOfLine
        }) await Assert.That(field.InvokeCommand(command)).IsTrue();
        await Assert.That(secret.Count).IsEqualTo(1);
        field.ClearSensitiveState();
        await Assert.That(field.InvokeCommand(Command.Undo)).IsTrue();
        await Assert.That(field.Text).IsEmpty();
        await Assert.That(secret.Count).IsEqualTo(0);
    }

    [Test]
    public async Task PreCancelledSaveClearsPreparedBytes()
    {
        using var form = TerminalDefinitionFixture.Form();
        using var operation = new SetupTerminalArtifactOperation(form, new ProtectedArtifactWriter(), Path.GetTempPath());
        await Assert.That(await operation.PrepareAsync()).IsTrue();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.That(async () => await operation.ExecuteAsync(cancellation.Token)).Throws<OperationCanceledException>();
        await Assert.That(operation.IsPrepared).IsFalse();
    }

    [Test]
    public async Task SignalDuringPreparedWriteCancelsBeforeAtomicCommitAndClearsState()
    {
        if (!OperatingSystem.IsLinux()) return;
        string directory = Path.Combine(Path.GetTempPath(), "configurator-signal-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            using var form = TerminalDefinitionFixture.Form();
            using var secret = new SetupTerminalSecretBuffer();
            using var cancellation = new CancellationTokenSource();
            using var operation = new SetupTerminalArtifactOperation(form, new ProtectedArtifactWriter(), directory,
                async token => { reached.TrySetResult(); await resume.Task.WaitAsync(token); });
            await Assert.That(await operation.PrepareAsync()).IsTrue();
            secret.TryReplace("opaque");
            using var signals = new SetupTerminalSignalScope(secret, cancellation.Cancel);
            Task execution = operation.ExecuteAsync(cancellation.Token);
            await reached.Task.WaitAsync(TimeSpan.FromSeconds(5));
            signals.RequestStop();
            await Assert.That(async () => await execution).Throws<OperationCanceledException>();
            await Assert.That(secret.Count).IsEqualTo(0);
            await Assert.That(operation.IsPrepared).IsFalse();
            await Assert.That(Directory.EnumerateFiles(directory)).IsEmpty();
        }
        finally { Directory.Delete(directory); }
    }

    [Test]
    public async Task SharedWriterKeepsTerminalStagingAnonymous()
    {
        if (!OperatingSystem.IsLinux()) return;
        string directory = Path.Combine(Path.GetTempPath(), "configurator-staging-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            using var form = TerminalDefinitionFixture.Form();
            using var operation = new SetupTerminalArtifactOperation(form, new ProtectedArtifactWriter(), directory,
                _ =>
                {
                    if (Directory.EnumerateFiles(directory).Any())
                        throw new InvalidOperationException("replaceable-staging-entry");
                    return Task.CompletedTask;
                });
            await Assert.That(await operation.PrepareAsync()).IsTrue();
            var outcome = await operation.ExecuteAsync(CancellationToken.None);
            await Assert.That(((SetupTerminalArtifactResult)outcome.CoreResult).Status).IsEqualTo(ProtectedArtifactStatus.Written);
        }
        finally { Directory.Delete(directory, true); }
    }
}
