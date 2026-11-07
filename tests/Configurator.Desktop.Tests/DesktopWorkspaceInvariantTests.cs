namespace Configurator.Desktop.Tests;

using System.Collections.Concurrent;
using Configurator.Desktop.ViewModels;

public sealed class DesktopWorkspaceInvariantTests
{
    [Test]
    [Arguments("answer")]
    [Arguments("selection")]
    [Arguments("replacement")]
    [Arguments("destination")]
    [Arguments("filename")]
    public async Task LatePreparationCannotReviveSaveAcrossStageAndPageNavigation(string invalidation)
    {
        string directory = Directory.CreateTempSubdirectory("configurator-navigation-").FullName;
        var owner = new HeldOwnerContext();
        using var shell = DesktopTestContext.Root(outputDirectory: directory);
        using var navigation = new DesktopNavigationViewModel(shell.Workspace);
        try
        {
            shell.Loader.LoadDefinition(DesktopApplicationTests.Definition());
            Task<DesktopPreparationOutcome> pending = Task.FromResult(DesktopPreparationOutcome.Invalid);
            owner.Run(() => pending = shell.Publisher.PrepareSelectionAsync());
            await owner.Posted.Task.WaitAsync(TimeSpan.FromSeconds(10));
            owner.Run(() =>
            {
                navigation.Stage = DesktopWorkspaceStage.Open;
                switch (invalidation)
                {
                    case "answer": shell.Workspace.Fields.Single(field => field.Id == "name").Text = "current"; break;
                    case "selection": shell.Publisher.SelectArtifact("config", false); break;
                    case "replacement": shell.Loader.LoadDefinition(DesktopApplicationTests.Definition(2)); break;
                    case "destination": shell.Publisher.OutputDirectory = directory + "-changed"; break;
                    case "filename": shell.Publisher.FileName = "changed.json"; break;
                }
                navigation.Stage = DesktopWorkspaceStage.Review;
                owner.Drain();
            });
            await Assert.That(await pending.WaitAsync(TimeSpan.FromSeconds(10))).IsEqualTo(DesktopPreparationOutcome.Retired);
            owner.Drain();
            await shell.Publisher.SaveSelectionAsync();
            await Assert.That(shell.Publisher.Preparation).IsNull();
            await Assert.That(shell.Publisher.CanSave).IsFalse();
            await Assert.That(shell.Publisher.CanSaveSelection).IsFalse();
            await Assert.That(Directory.GetFiles(directory)).IsEmpty();
            await Assert.That(navigation.Stage).IsEqualTo(DesktopWorkspaceStage.Review);
        }
        finally
        {
            owner.Drain();
            Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class HeldOwnerContext : SynchronizationContext
    {
        private readonly ConcurrentQueue<Action> _pending = new();
        internal TaskCompletionSource Posted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override void Post(SendOrPostCallback callback, object? state)
        {
            _pending.Enqueue(() => callback(state));
            Posted.TrySetResult();
        }
        internal void Run(Action action)
        {
            SynchronizationContext? previous = Current;
            SetSynchronizationContext(this);
            try { action(); }
            finally { SetSynchronizationContext(previous); }
        }
        internal void Drain()
        {
            while (_pending.TryDequeue(out Action? action)) Run(action);
        }
    }
}
