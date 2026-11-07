namespace Configurator.Desktop.Tests;

using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Configurator.Desktop.ViewModels;
using Configurator.Desktop.Views;
using Configurator.Artifacts;
using System.Text;
using System.Text.Json.Nodes;
using Avalonia.LogicalTree;

public sealed class DesktopOutputReviewTests
{
    public static Avalonia.AppBuilder BuildAvaloniaApp() => DesktopApplicationTests.BuildAvaloniaApp();

    [Test]
    public async Task MissingRequiredSecretCannotGrantCompletePreparedOutput()
    {
        JsonObject source = JsonNode.Parse(DesktopApplicationTests.Definition())!.AsObject();
        JsonObject metadata = source["configuration"]!["properties"]!["name"]!["x-configurator"]!.AsObject();
        metadata["sensitivity"] = "secret";
        metadata.Remove("initialValue");
        using var shell = DesktopTestContext.Root();
        await Assert.That(shell.Loader.LoadDefinition(Encoding.UTF8.GetBytes(source.ToJsonString()))).IsTrue();
        await Assert.That(await shell.Publisher.PrepareSelectionAsync()).IsEqualTo(DesktopPreparationOutcome.Invalid);
        await Assert.That(shell.Publisher.Preparation).IsNull();
        await Assert.That(shell.Publisher.CanSaveSelection).IsFalse();
        await Assert.That(await shell.Publisher.SaveSelectionAsync()).IsNull();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ImportSourceChangesPreserveExportsAndRetireCurrentOrPendingComparison(bool pending)
    {
        using var shell = DesktopTestContext.Root();
        shell.Loader.LoadDefinition(DesktopApplicationTests.Definition());
        shell.Publisher.FileName = "chosen.json";
        await shell.Publisher.PrepareSelectionAsync();
        var preparation = shell.Publisher.Preparation;
        var supplied = new TaskCompletionSource<ReadOnlyMemory<byte>>(TaskCreationOptions.RunContinuationsAsynchronously);
        byte[] bytes = """{"name":"synthetic-import"}"""u8.ToArray();
        Task<bool> comparison = shell.ImportReview.CompareAsync(_ => supplied.Task);
        if (!pending)
        {
            supplied.SetResult(bytes);
            await Assert.That(await comparison.WaitAsync(TimeSpan.FromSeconds(10))).IsTrue();
        }
        shell.Publisher.ArtifactId = "yaml";
        supplied.TrySetResult(bytes);
        bool accepted = await comparison.WaitAsync(TimeSpan.FromSeconds(10));
        await Assert.That(ReferenceEquals(preparation, shell.Publisher.Preparation)).IsTrue();
        await Assert.That(shell.Publisher.SelectedArtifactIds.Count).IsEqualTo(1);
        await Assert.That(shell.Publisher.SelectedArtifactIds[0]).IsEqualTo("config");
        await Assert.That(shell.Publisher.FileName).IsEqualTo("chosen.json");
        if (pending) await Assert.That(accepted).IsFalse();
        await Assert.That(shell.Workspace.Form!.ImportComparison).IsNull();
        await Assert.That(shell.ImportReview.CanFillImportFields).IsFalse();
        await Assert.That(bytes.All(value => value == 0)).IsTrue();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task SelectionUsesSingleOverrideOrDeclaredBundleNamesAndConsumesReadiness(bool multiple)
    {
        if (!OperatingSystem.IsLinux()) return;
        string directory = Directory.CreateTempSubdirectory("configurator-output-review-").FullName;
        try
        {
            using var shell = DesktopTestContext.Root(outputDirectory: directory);
            shell.Loader.LoadDefinition(DesktopApplicationTests.Definition());
            if (multiple) shell.Publisher.SelectArtifact("environment", true);
            shell.Publisher.FileName = "chosen.json";
            shell.Publisher.ArtifactId = "yaml";
            await Assert.That(shell.Publisher.FileName).IsEqualTo("chosen.json");
            await Assert.That(shell.Publisher.SelectedArtifactIds[0]).IsEqualTo("config");
            string unevaluated = shell.Publisher.ArtifactSelections.Single(item => item.Id == "config").PreparationStatus;
            await Assert.That(await shell.Publisher.PrepareSelectionAsync()).IsEqualTo(DesktopPreparationOutcome.Prepared);
            await Assert.That(shell.Publisher.ArtifactSelections.Single(item => item.Id == "config").PreparationStatus)
                .IsNotEqualTo(unevaluated);
            await Assert.That(Directory.GetFiles(directory)).IsEmpty();
            ProtectedArtifactPublicationReceipt? receipt = await shell.Publisher.SaveSelectionAsync();
            await Assert.That(receipt).IsNotNull();
            await Assert.That(receipt!.IsSettled).IsTrue();
            await Assert.That(receipt.Outcomes.Count).IsEqualTo(multiple ? 2 : 1);
            await Assert.That(receipt.Outcomes.All(outcome => outcome.Result is { Succeeded: true })).IsTrue();
            await Assert.That(File.Exists(Path.Combine(directory, multiple ? "service.json" : "chosen.json"))).IsTrue();
            await Assert.That(File.Exists(Path.Combine(directory, "service.env"))).IsEqualTo(multiple);
            await Assert.That(shell.Publisher.CanSaveSelection).IsFalse();
            await Assert.That(shell.Publisher.ArtifactSelections.Single(item => item.Id == "config").PreparationStatus)
                .IsEqualTo(unevaluated);
            await Assert.That(await shell.Publisher.SaveSelectionAsync()).IsNull();
            await Assert.That(shell.Publisher.PublicationHistory.Count).IsEqualTo(1);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Test]
    public async Task EmptyExportSelectionCannotPrepareOrGrantSaveFromReviewStage()
    {
        using var shell = DesktopTestContext.Root();
        shell.Loader.LoadDefinition(DesktopApplicationTests.Definition());
        shell.Publisher.SelectArtifact("config", false);
        shell.Navigation.ReviewCommand.Execute(null);
        await Assert.That(shell.Publisher.SelectedArtifactIds.Count).IsEqualTo(0);
        await Assert.That(shell.Publisher.CanPrepare).IsFalse();
        await Assert.That(shell.Publisher.CanSave).IsFalse();
        await Assert.That(shell.Publisher.ArtifactId).IsEqualTo("config");
    }

    [Test]
    [NotInParallel]
    public async Task ReviewExposesOnePreparationAndOneDeliberateSaveForSelectedOutputs()
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(DesktopOutputReviewTests));
        try
        {
            var result = await session.Dispatch(() =>
            {
                using var shell = DesktopTestContext.Root();
                var window = new MainWindow { DataContext = shell };
                try
                {
                    window.Show();
                    shell.Loader.LoadDefinition(DesktopApplicationTests.Definition());
                    shell.Navigation.ReviewCommand.Execute(null);
                    window.UpdateLayout();
                    Dispatcher.UIThread.RunJobs();
                    return (prepare: window.FindNamedControl<Button>("PrepareSelection") is { IsVisible: true },
                        save: window.FindNamedControl<Button>("SaveSelection") is { IsEffectivelyEnabled: false },
                        declaredName: window.GetLogicalDescendants().OfType<TextBlock>().Any(control =>
                            control.DataContext is ArtifactSelectionViewModel { Id: "config" } &&
                            control.Text == "service.json" && control.IsEffectivelyVisible),
                        format: window.GetLogicalDescendants().OfType<TextBlock>().Any(control =>
                            control.DataContext is ArtifactSelectionViewModel { Id: "config" } &&
                            control.Text == "json" && control.IsEffectivelyVisible),
                        selected: shell.Publisher.SelectedArtifactIds.ToArray());
                }
                finally { window.Close(); }
            }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));
            await Assert.That(result.prepare).IsTrue();
            await Assert.That(result.save).IsTrue();
            await Assert.That(result.declaredName).IsTrue();
            await Assert.That(result.format).IsTrue();
            await Assert.That(result.selected.Length).IsEqualTo(1);
            await Assert.That(result.selected[0]).IsEqualTo("config");
        }
        finally { await Task.Run(session.Dispose); }
    }
}
