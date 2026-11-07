namespace Configurator.Desktop.Tests;

using System.Text;
using Configurator.Artifacts;
using Configurator.Core.Definitions;
using Configurator.Desktop.ViewModels;

public sealed class DesktopDeploymentContextTests
{
    [Test]
    public async Task ForgedDestinationCannotImportOrPrepareAnExcludedRecipient()
    {
        using var shell = DesktopTestContext.Root();
        shell.Loader.LoadDefinition(Definition().CanonicalBytes);
        shell.Workspace.SelectedConfigurationContext = shell.Workspace.ConfigurationChoices.Single(choice => choice.ScopeId == "dashboard");
        shell.Publisher.SelectArtifact("worker", true);
        await Assert.That(shell.Publisher.SelectedArtifactIds.Contains("worker")).IsFalse();
        shell.Publisher.ArtifactId = "worker";
        await Assert.That(shell.ImportReview.CanImport).IsFalse();
        await Assert.That(await shell.Publisher.PrepareAsync()).IsEqualTo(DesktopPreparationOutcome.Invalid);
        await Assert.That(shell.Publisher.CanSave).IsFalse();
    }

    [Test]
    public async Task ScopeChangeDuringSaveRetiresPreparationButRetainsEstablishedReceipt()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var shell = DesktopTestContext.Root(save: async (_, _, _) =>
        {
            entered.TrySetResult();
            await release.Task.WaitAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
            return ProtectedArtifactCommitResult.Installed(true);
        }, protectedOutputAvailable: true);
        shell.Loader.LoadDefinition(Definition().CanonicalBytes);
        shell.Publisher.ArtifactId = "worker";
        shell.Workspace.Form!.Fields.Single(field => field.Definition.Id == "workerSecret").Text = "synthetic-worker-only";
        await Assert.That(await shell.Publisher.PrepareAsync()).IsEqualTo(DesktopPreparationOutcome.Prepared);
        Task<ProtectedArtifactCommitResult> saving = shell.Publisher.SaveAsync();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try
        {
            shell.Workspace.SelectedConfigurationContext = shell.Workspace.ConfigurationChoices.Single(choice => choice.ScopeId == "dashboard");
            await Assert.That(shell.Publisher.CanSave).IsFalse();
            await Assert.That(shell.Workspace.Form!.ConfigurationContext?.ScopeId).IsEqualTo("dashboard");
        }
        finally { release.TrySetResult(); }
        await Assert.That((await saving.WaitAsync(TimeSpan.FromSeconds(10))).InstallationEstablished).IsTrue();
        await Assert.That(shell.Publisher.PublicationHistory.Single().Outcomes.Single().ArtifactId).IsEqualTo("worker");
        await Assert.That(shell.Publisher.CanSave).IsFalse();
        await Assert.That(shell.Publisher.ArtifactId).IsEqualTo("dashboard");
    }

    [Test]
    public async Task ForgedPairDoesNotChangeAcceptedAnswersOrRevision()
    {
        using var shell = DesktopTestContext.Root();
        shell.Loader.LoadDefinition(Definition().CanonicalBytes);
        long revision = shell.Workspace.Form!.InputRevision;
        shell.Workspace.SelectedConfigurationContext = shell.Workspace.ConfigurationChoices.Single(choice => choice.ScopeId == "all");
        await Assert.That(shell.Workspace.Form.InputRevision).IsEqualTo(revision);
        shell.Workspace.SelectedConfigurationContext = new ConfigurationContextChoice("separate", "unknown", "Unknown");
        await Assert.That(shell.Workspace.Form.InputRevision).IsEqualTo(revision);
        await Assert.That(shell.Workspace.Form.ConfigurationContext?.ScopeId).IsEqualTo("all");
        shell.Workspace.SelectedConfigurationContext = shell.Workspace.ConfigurationChoices.Single(choice => choice.ScopeId == "dashboard");
        await Assert.That(shell.Workspace.Form.InputRevision).IsEqualTo(revision + 1);
        await Assert.That(shell.Workspace.Form.ConfigurationContext?.ScopeId).IsEqualTo("dashboard");
    }

    [Test]
    public async Task ScopeChangeRejectsLateImportWithoutRestoringAnOldRecipient()
    {
        using var shell = DesktopTestContext.Root();
        shell.Loader.LoadDefinition(Definition().CanonicalBytes);
        shell.Publisher.ArtifactId = "worker";
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<ReadOnlyMemory<byte>>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<bool> import = shell.ImportReview.ImportAsync(_ => { entered.TrySetResult(); return release.Task; });
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        shell.Workspace.SelectedConfigurationContext = shell.Workspace.ConfigurationChoices.Single(choice => choice.ScopeId == "dashboard");
        release.SetResult(Encoding.UTF8.GetBytes("{\"credential\":\"synthetic-late-worker\"}"));
        await Assert.That(await import.WaitAsync(TimeSpan.FromSeconds(10))).IsFalse();
        await Assert.That(shell.Workspace.Form!.ConfigurationContext?.ScopeId).IsEqualTo("dashboard");
        await Assert.That(shell.Workspace.Form.ImportComparison).IsNull();
    }

    [Test]
    public async Task NarrowingRecipientRetiresPreparationAndRemovesIneligibleDestinations()
    {
        using var shell = DesktopTestContext.Root(protectedOutputAvailable: true);
        await Assert.That(shell.Loader.LoadDefinition(Definition().CanonicalBytes)).IsTrue();
        shell.Publisher.ArtifactId = "worker";
        shell.Workspace.Form!.Fields.Single(field => field.Definition.Id == "workerSecret").Text = "synthetic-worker-only";
        await Assert.That(await shell.Publisher.PrepareAsync()).IsEqualTo(DesktopPreparationOutcome.Prepared);
        await Assert.That(shell.Publisher.CanSave).IsTrue();
        shell.Workspace.SelectedConfigurationContext = shell.Workspace.ConfigurationChoices.Single(choice => choice.ScopeId == "dashboard");
        await Assert.That(string.Join(",", shell.Publisher.Artifacts.Select(artifact => artifact.Id))).IsEqualTo("dashboard");
        await Assert.That(shell.Publisher.ArtifactId).IsEqualTo("dashboard");
        await Assert.That(shell.Publisher.CanSave).IsFalse();
        await Assert.That(shell.Publisher.Preparation).IsNull();
    }

    internal static Definition Definition() => DefinitionSourceCodec.ParseJson(Encoding.UTF8.GetBytes("""
        {"$schema":"urn:islamu:configurator:authoring:v0.0.1",
         "identity":{"familyId":"3e534759-0fc0-46cc-920d-43c66313dce6","revision":1},
         "project":{"id":"desktop-scope","name":"Desktop scope"},"applicationVersions":["1"],
         "localization":{"defaultLocale":"en","translations":{"ar":{"direction":"rtl","overrides":{
          "/deployment/recipes/0/title":"منفصل","/deployment/recipes/0/scopes/2/title":"لوحة التحكم"}}}},
         "configuration":{"type":"object","additionalProperties":false,"x-configurator":{"sensitivity":"public"},"required":["recipe","scope","workerSecret","endpoint"],"properties":{
          "recipe":{"type":"string","enum":["separate"],"x-configurator":{"sensitivity":"public","initialValue":"separate"}},
          "scope":{"type":"string","enum":["all","worker","dashboard"],"x-configurator":{"sensitivity":"public","initialValue":"all"}},
          "workerSecret":{"type":"string","x-configurator":{"sensitivity":"secret"}},
          "endpoint":{"type":"string","x-configurator":{"sensitivity":"public","initialValue":"synthetic-endpoint"}}}},
         "deployment":{"recipePointer":"/recipe","scopePointer":"/scope","components":[{"id":"worker","title":"Worker"},{"id":"dashboard","title":"Dashboard"}],
          "recipes":[{"id":"separate","title":"Separate","scopes":[
           {"id":"all","title":"All","componentIds":["worker","dashboard"],"artifactIds":["worker","dashboard"]},
           {"id":"worker","title":"Worker","componentIds":["worker"],"artifactIds":["worker"]},
           {"id":"dashboard","title":"Dashboard","componentIds":["dashboard"],"artifactIds":["dashboard"]}]}]},
         "outputs":{
          "worker.json":{"id":"worker","format":"json","consumerIds":["worker"],"bindings":{"/credential":{"scope":"root","path":"/workerSecret"}}},
          "dashboard.json":{"id":"dashboard","format":"json","consumerIds":["dashboard"],"bindings":{"/endpoint":{"scope":"root","path":"/endpoint"}}}}}
        """));
}
