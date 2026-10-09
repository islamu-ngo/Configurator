namespace Configurator.Browser.Tests;

using System.Text;
using System.Text.Json;
using Bunit;

public sealed class BrowserConfigurationWorkflowTests
{
    [Test]
    public async Task DraftOrdinaryMissingValueHasConnectedErrorWhileSecretDeferralRemainsValid()
    {
        using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, BrowserFixture.Definition));
        component.Find("#choice-mode").Change("single");
        component.Find("#prepare-draft").Click();
        await Assert.That(component.Find("#choice-hostname").GetAttribute("aria-invalid")).IsEqualTo("true");
        await Assert.That(component.Find("#choice-hostname").GetAttribute("aria-describedby")).Contains("choice-hostname-error");
        await Assert.That(component.Find("#choice-hostname-error").TextContent).Contains("value-required");
        await Assert.That(component.Find("#choice-credential").GetAttribute("aria-invalid")).IsEqualTo("false");
        component.Find("#choice-hostname").Input("example.test");
        component.Find("#prepare-draft").Click();
        await Assert.That(component.FindAll(".configuration-editor__artifact")).Count().IsEqualTo(1);
    }

    [Test]
    public async Task DraftAndCompleteUseSharedProjectionAndOnlyExplicitCopyExposesBytes()
    {
        using var context = BrowserFixture.Context();
        var module = context.JSInterop.SetupModule("./browser-artifacts.js");
        module.SetupVoid("open", _ => true).SetVoidResult();
        module.Setup<bool>("copy", _ => true).SetResult(true);
        module.SetupVoid("invalidate", _ => true).SetVoidResult();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, BrowserFixture.Definition));
        component.Find("#choice-mode").Change("single");
        component.Find("#choice-hostname").Input("example.test");
        component.Find("#prepare-complete").Click();
        await Assert.That(component.FindAll(".configuration-editor__artifact")).IsEmpty();
        component.Find("#prepare-draft").Click();
        await Assert.That(component.FindAll(".configuration-editor__artifact")).Count().IsEqualTo(1);
        await Assert.That(component.Markup).Contains("Draft");
        await Assert.That(module.Invocations.Count).IsEqualTo(0);
        component.Find(".configuration-editor__artifact button").Click();
        byte[] draft = (byte[])module.Invocations.Single(call => call.Identifier == "open").Arguments[2]!;
        using JsonDocument draftDocument = JsonDocument.Parse(draft);
        await Assert.That(draftDocument.RootElement.GetProperty("hostname").GetString()).IsEqualTo("example.test");
        await Assert.That(draftDocument.RootElement.GetProperty("credential").GetString()).IsEqualTo("");
        component.Find("#secret-consent").Change(true);
        component.Find("#choice-credential").Input("synthetic-secret-canary");
        component.Find("#prepare-complete").Click();
        await Assert.That(component.FindAll(".configuration-editor__artifact")).Count().IsEqualTo(1);
        await Assert.That(component.Find("pre").TextContent).IsEqualTo("");
        component.Find(".configuration-editor__artifact button").Click();
        byte[] complete = (byte[])module.Invocations.Last(call => call.Identifier == "open").Arguments[2]!;
        using JsonDocument completeDocument = JsonDocument.Parse(complete);
        await Assert.That(completeDocument.RootElement.GetProperty("credential").GetString()).IsEqualTo("synthetic-secret-canary");
        component.Find("#choice-hostname").Input("changed.test");
        await Assert.That(component.FindAll(".configuration-editor__artifact")).IsEmpty();
        await Assert.That(module.Invocations.Any(call => call.Identifier == "invalidate")).IsTrue();
    }

    [Test]
    public async Task ClipboardFailureProvidesValueSafeDownloadAlternative()
    {
        using var context = BrowserFixture.Context();
        var module = context.JSInterop.SetupModule("./browser-artifacts.js");
        module.Setup<bool>("copy", _ => true).SetResult(false);
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, BrowserFixture.Definition));
        component.Find("#choice-mode").Change("single");
        component.Find("#choice-hostname").Input("example.test");
        component.Find("#prepare-draft").Click();
        component.Find(".configuration-editor__artifact button").Click();
        await Assert.That(component.Find("[role=status]").TextContent).Contains("Download");
        await Assert.That(component.Find("[role=status]").TextContent).DoesNotContain("example.test");
    }
}
