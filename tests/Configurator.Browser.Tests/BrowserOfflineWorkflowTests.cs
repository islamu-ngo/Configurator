namespace Configurator.Browser.Tests;

using System.Text;
using System.Text.Json;
using Bunit;
using Configurator.Browser.Pages;
using Configurator.Core.Definitions;

public sealed class BrowserOfflineWorkflowTests
{
    [Test]
    public async Task PublicTemplateRoundTripsPinnedIdentityAndChoices()
    {
        SetupDefinition definition = BrowserFixture.Definition;
        BrowserTemplateResult result = BrowserPublicTemplate.Parse(definition, BrowserFixture.Template());
        await Assert.That(result.IsAccepted).IsTrue();
        using JsonDocument downloaded = BrowserFixture.Download(result.DownloadHref!);
        await Assert.That(downloaded.RootElement.GetProperty("schemaVersion").GetString()).IsEqualTo(BrowserPublicTemplate.SchemaVersion);
        await Assert.That(downloaded.RootElement.GetProperty("definitionDigest").GetString()).IsEqualTo(definition.Digest.Value);
        await Assert.That(downloaded.RootElement.GetProperty("revision").GetInt64()).IsEqualTo(definition.Revision);
        await Assert.That(downloaded.RootElement.GetProperty("selections").GetProperty("mode").GetString()).IsEqualTo("single");
        await Assert.That(BrowserPublicTemplate.Parse(definition, Encoding.UTF8.GetBytes(result.Preview!)).IsAccepted).IsTrue();
        await Assert.That(DefinitionValidator.Validate(definition, result.Answers!).IsReady).IsFalse();
    }

    [Test]
    public async Task RendererAllowsOnlyActivePublicChoicesAndExportsPartialTemplate()
    {
        using var context = BrowserFixture.Context();
        var component = context.Render<PublicTemplateEditor>(parameters => parameters.Add(item => item.Definition, BrowserFixture.Definition));
        await Assert.That(component.FindAll("select")).Count().IsEqualTo(1);
        await Assert.That(component.FindAll("[data-field=credential]")).IsEmpty();
        await Assert.That(component.FindAll("[data-field=hostname] input")).IsEmpty();
        await Assert.That(component.Find("label[for=choice-mode]").TextContent.Length > 0).IsTrue();
        component.Find("#choice-mode").Change("cluster");
        await Assert.That(component.FindAll("select")).Count().IsEqualTo(2);
        component.Find("#choice-replicas").Change("false");
        component.Find("button").Click();
        using JsonDocument template = BrowserFixture.Download(component.Find("a[download]").GetAttribute("href")!);
        await Assert.That(template.RootElement.GetProperty("selections").GetProperty("replicas").GetBoolean()).IsFalse();
        await Assert.That(component.Find("[data-native-required]").GetAttribute("data-native-required")).IsEqualTo("2");
        await Assert.That(component.FindAll("[role=status]")).Count().IsEqualTo(1);
    }

    [Test]
    public async Task ChangingActivationRemovesDormantSelectionsAndInvalidatesPreview()
    {
        using var context = BrowserFixture.Context();
        var component = context.Render<PublicTemplateEditor>(parameters => parameters.Add(item => item.Definition, BrowserFixture.Definition));
        component.Find("#choice-mode").Change("cluster");
        component.Find("#choice-replicas").Change("true");
        component.Find("button").Click();
        component.Find("#choice-mode").Change("single");
        await Assert.That(component.FindAll("a[download]")).IsEmpty();
        await Assert.That(component.FindAll("pre")).IsEmpty();
        component.Find("button").Click();
        using JsonDocument template = BrowserFixture.Download(component.Find("a[download]").GetAttribute("href")!);
        await Assert.That(template.RootElement.GetProperty("selections").EnumerateObject().Select(item => item.Name))
            .IsEquivalentTo(["mode"]);
    }

    [Test]
    public async Task ClearRemovesSelectionsAndPreview()
    {
        using var context = BrowserFixture.Context();
        var component = context.Render<PublicTemplateEditor>(parameters => parameters.Add(item => item.Definition, BrowserFixture.Definition));
        using var input = new MemoryStream(BrowserFixture.Template());
        await component.InvokeAsync(() => component.Instance.LoadAsync(input));
        await Assert.That(component.FindAll("a[download]")).Count().IsEqualTo(1);
        component.FindAll("button")[1].Click();
        await Assert.That(component.FindAll("a[download]")).IsEmpty();
        component.Find("button").Click();
        using JsonDocument template = BrowserFixture.Download(component.Find("a[download]").GetAttribute("href")!);
        await Assert.That(template.RootElement.GetProperty("selections").EnumerateObject()).IsEmpty();
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
