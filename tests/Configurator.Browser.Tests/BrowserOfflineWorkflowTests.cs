namespace Configurator.Browser.Tests;

using Bunit;
using Configurator.Browser.Pages;
using Configurator.Core.Definitions;
using Configurator.Presentation;

public sealed class BrowserOfflineWorkflowTests
{
    [Test]
    public async Task DisconnectedRetainedDefinitionReopensWithPinnedIdentityAndEmptyAnswers()
    {
        using var context = new BunitContext();
        var module = context.JSInterop.SetupModule("./pwa-lifecycle.js");
        module.Setup<BrowserOfflineState>("initialize").SetResult(new(true, true, false));
        module.Setup<byte[]?>("readDefinition", BrowserFixture.Definition.Digest.Value)
            .SetResult(BrowserFixture.Definition.CanonicalBytes.ToArray());
        await using var policy = new BrowserOfflinePolicy(context.JSInterop.JSRuntime);
        await policy.InitializeAsync();
        SetupDefinition? retained = await policy.ReadRetainedDefinitionAsync(BrowserFixture.Definition.Digest.Value);
        await Assert.That(retained).IsNotNull();
        await Assert.That(retained!.Digest).IsEqualTo(BrowserFixture.Definition.Digest);
        using var form = new DefinitionFormModel(retained, DefinitionSessionPrivacy.BrowserNoSecrets);
        await Assert.That(form.Snapshot().Values).IsEmpty();
        await Assert.That(policy.CanFetchDefinition).IsFalse();
    }

    [Test]
    public async Task CatalogueLinksEachCompleteAdmittedDefinitionByItsExactDigest()
    {
        using var context = BrowserFixture.Context();
        var component = context.Render<Catalogue>();
        await Assert.That(component.FindAll("h1")).Count().IsEqualTo(1);
        await Assert.That(component.Find(".definition-list a").GetAttribute("href"))
            .IsEqualTo($"launch/{BrowserFixture.Family}?digest={BrowserFixture.Definition.Digest.Value}");
        await Assert.That(BrowserFixture.Definition.Fields.Any(field => field.Sensitivity == SetupFieldSensitivity.Secret)).IsTrue();
    }
}
