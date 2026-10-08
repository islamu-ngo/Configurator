namespace Configurator.Browser.Tests;

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bunit;
using Configurator.Core.Definitions;

public sealed class BrowserSecretBoundaryTests
{
    [Test]
    [Arguments("""{"credential":"private-canary"}""")]
    [Arguments("""{"hostname":"private-canary"}""")]
    [Arguments("""{"unknown":"private-canary"}""")]
    [Arguments("""{"mode":"private-canary"}""")]
    [Arguments("""{"mode":{"secret":"private-canary"}}""")]
    [Arguments("""{"mode":"single","mode":"cluster"}""")]
    [Arguments("""{"mode":"single","replicas":true}""")]
    [Arguments("""{"mode":"cluster","replicas":"true"}""")]
    [Arguments("""{"mode":"cluster","replicas":null}""")]
    public async Task RestrictedUnknownDuplicateAndUnsupportedSelectionsRejectWithoutOutput(string selections)
    {
        BrowserTemplateResult result = BrowserPublicTemplate.Parse(BrowserFixture.Definition, BrowserFixture.Template(selections));
        await Assert.That(result.IsAccepted).IsFalse();
        await Assert.That(result.Answers).IsNull();
        await Assert.That(result.Preview).IsNull();
        await Assert.That(result.DownloadHref).IsNull();
    }

    [Test]
    [Arguments("extra")]
    [Arguments("duplicate")]
    [Arguments("schema")]
    [Arguments("family")]
    [Arguments("revision")]
    [Arguments("digest")]
    [Arguments("missing")]
    [Arguments("wrong-type")]
    public async Task ClosedEnvelopeRejectsUnpinnedAndPrivatePayloads(string mutation)
    {
        string template = Encoding.UTF8.GetString(BrowserFixture.Template());
        JsonNode node = JsonNode.Parse(template)!;
        switch (mutation)
        {
            case "extra": node["private"] = "private-canary"; break;
            case "schema": node["schemaVersion"] = "event-setup-public-manifest/v1"; break;
            case "family": node["familyId"] = "11111111-1111-1111-1111-111111111111"; break;
            case "revision": node["revision"] = 2; break;
            case "digest": node["definitionDigest"] = new string('0', 64); break;
            case "missing": node.AsObject().Remove("revision"); break;
            case "wrong-type": node["revision"] = "private-canary"; break;
        }
        byte[] bytes = mutation == "duplicate"
            ? Encoding.UTF8.GetBytes(template.Insert(1, "\"revision\":1,"))
            : Encoding.UTF8.GetBytes(node.ToJsonString());
        using var context = BrowserFixture.Context();
        var component = context.Render<PublicTemplateEditor>(parameters => parameters.Add(item => item.Definition, BrowserFixture.Definition));
        using var valid = new MemoryStream(BrowserFixture.Template());
        await component.InvokeAsync(() => component.Instance.LoadAsync(valid));
        using var invalid = new MemoryStream(bytes);
        await component.InvokeAsync(() => component.Instance.LoadAsync(invalid));
        await Assert.That(component.FindAll("a[download]")).IsEmpty();
        await Assert.That(component.FindAll("pre")).IsEmpty();
        await Assert.That(component.Find("[role=alert]").GetAttribute("data-code")).IsEqualTo("template-rejected");
        await Assert.That(component.Markup).DoesNotContain("private-canary");
    }

    [Test]
    public async Task AdmissionAndPublicSelectionRespectSensitivityRatherThanFieldTypeAlone()
    {
        JsonNode node = JsonNode.Parse(BrowserFixture.Definition.CanonicalBytes.Span)!;
        node["fields"]![0]!["sensitivity"] = "secret";
        SetupDefinition protectedDefinition = SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(node.ToJsonString()));
        using var context = BrowserFixture.Context();
        var component = context.Render<PublicTemplateEditor>(parameters => parameters.Add(item => item.Definition, protectedDefinition));
        await Assert.That(component.FindAll("select")).IsEmpty();
        await Assert.That(BrowserPublicTemplate.Parse(protectedDefinition,
            BrowserFixture.Template("""{"mode":"single"}""", protectedDefinition)).IsAccepted).IsFalse();
        await Assert.That(protectedDefinition.Digest == BrowserFixture.Definition.Digest).IsFalse();
    }

    [Test]
    public async Task UploadLimitRejectsAbove64KiBAndAcceptsExactBoundary()
    {
        byte[] template = BrowserFixture.Template();
        byte[] exact = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(template)
            + new string(' ', BrowserPublicTemplate.MaximumBytes - template.Length));
        byte[] oversized = [.. exact, (byte)' '];
        await Assert.That(BrowserPublicTemplate.Parse(BrowserFixture.Definition, exact).IsAccepted).IsTrue();
        await Assert.That(BrowserPublicTemplate.Parse(BrowserFixture.Definition, oversized).IsAccepted).IsFalse();
        using var context = BrowserFixture.Context();
        var component = context.Render<PublicTemplateEditor>(parameters => parameters.Add(item => item.Definition, BrowserFixture.Definition));
        using var input = new MemoryStream(oversized);
        await component.InvokeAsync(() => component.Instance.LoadAsync(input));
        await Assert.That(component.FindAll("a[download]")).IsEmpty();
        await Assert.That(component.FindAll("[role=alert]")).Count().IsEqualTo(1);
    }
}
