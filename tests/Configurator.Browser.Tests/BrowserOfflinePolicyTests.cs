namespace Configurator.Browser.Tests;

using Bunit;

public sealed class BrowserOfflinePolicyTests
{
    [Test]
    public async Task IncompleteInstallationCannotStartAnswerCustodyOrClaimOfflineReadiness()
    {
        using var context = new BunitContext();
        var module = context.JSInterop.SetupModule("./pwa-lifecycle.js");
        module.Setup<BrowserOfflineState>("initialize").SetResult(new(false, false, true));
        await using var policy = new BrowserOfflinePolicy(context.JSInterop.JSRuntime);
        await policy.InitializeAsync();
        await Assert.That(policy.OfflineReady).IsFalse();
        await Assert.That(await policy.BeginWorkspaceAsync()).IsFalse();
        await Assert.That(policy.IsWorkspaceActive).IsFalse();
    }

    [Test]
    public async Task LiveAnswersRejectRemoteSourcesAndDefinitionRetentionUntilCleared()
    {
        using var context = new BunitContext();
        var module = context.JSInterop.SetupModule("./pwa-lifecycle.js");
        module.Setup<BrowserOfflineState>("initialize").SetResult(new(true, true, true));
        module.Setup<bool>("beginWorkspace", 1L).SetResult(true);
        module.SetupVoid("endWorkspace", 2L).SetVoidResult();
        await using var policy = new BrowserOfflinePolicy(context.JSInterop.JSRuntime);
        await policy.InitializeAsync();
        await Assert.That(await policy.BeginWorkspaceAsync()).IsTrue();
        await Assert.That(policy.CanFetchDefinition).IsFalse();
        await Assert.That(await policy.PrepareOfflineAsync(BrowserFixture.Definition, true)).IsFalse();
        await policy.EndWorkspaceAsync();
        await Assert.That(policy.CanFetchDefinition).IsTrue();
    }

    [Test]
    public async Task DisconnectedEntryRejectsRemoteSourcesAndRetentionRequiresExplicitPublicConfirmation()
    {
        using var context = new BunitContext();
        var module = context.JSInterop.SetupModule("./pwa-lifecycle.js");
        module.Setup<BrowserOfflineState>("initialize").SetResult(new(true, true, false));
        await using var policy = new BrowserOfflinePolicy(context.JSInterop.JSRuntime);
        await policy.InitializeAsync();
        await Assert.That(policy.CanFetchDefinition).IsFalse();
        await Assert.That(await policy.PrepareOfflineAsync(BrowserFixture.Definition, false)).IsFalse();
    }

    [Test]
    public async Task RetainedDefinitionIsReadmittedAndDigestBoundBeforeReopening()
    {
        using var context = new BunitContext();
        var module = context.JSInterop.SetupModule("./pwa-lifecycle.js");
        module.Setup<BrowserOfflineState>("initialize").SetResult(new(true, true, false));
        module.Setup<byte[]?>("readDefinition", BrowserFixture.Definition.Digest.Value)
            .SetResult(BrowserFixture.Revision(2).CanonicalBytes.ToArray());
        await using var policy = new BrowserOfflinePolicy(context.JSInterop.JSRuntime);
        await policy.InitializeAsync();
        await Assert.That(await policy.ReadRetainedDefinitionAsync(BrowserFixture.Definition.Digest.Value)).IsNull();
    }

    [Test]
    public async Task ClearWhileBeginningCannotResurrectLiveCustody()
    {
        using var context = new BunitContext();
        var module = context.JSInterop.SetupModule("./pwa-lifecycle.js");
        module.Setup<BrowserOfflineState>("initialize").SetResult(new(true, true, true));
        var beginning = module.Setup<bool>("beginWorkspace", 1L);
        module.SetupVoid("endWorkspace", 2L).SetVoidResult();
        await using var policy = new BrowserOfflinePolicy(context.JSInterop.JSRuntime);
        await policy.InitializeAsync();
        ValueTask<bool> start = policy.BeginWorkspaceAsync();
        await Assert.That(policy.CanFetchDefinition).IsFalse();
        await policy.EndWorkspaceAsync();
        beginning.SetResult(true);
        await Assert.That(await start).IsFalse();
        await Assert.That(policy.IsWorkspaceActive).IsFalse();
    }

    [Test]
    public async Task DisposingAReplacedEditorCannotReleaseTheCurrentEditorsCustody()
    {
        using var context = new BunitContext();
        var module = context.JSInterop.SetupModule("./pwa-lifecycle.js");
        module.Setup<BrowserOfflineState>("initialize").SetResult(new(true, true, true));
        module.Setup<bool>("beginWorkspace", 1L).SetResult(true);
        module.Setup<bool>("beginWorkspace", 2L).SetResult(true);
        module.SetupVoid("endWorkspace", 3L).SetVoidResult();
        await using var policy = new BrowserOfflinePolicy(context.JSInterop.JSRuntime);
        await policy.InitializeAsync();
        await Assert.That(await policy.BeginWorkspaceAsync()).IsTrue();
        long replacedEditor = policy.WorkspaceGeneration;
        await Assert.That(await policy.BeginWorkspaceAsync()).IsTrue();
        await policy.EndWorkspaceAsync(replacedEditor);
        await Assert.That(policy.IsWorkspaceActive).IsTrue();
        await Assert.That(policy.CanFetchDefinition).IsFalse();
    }
}
