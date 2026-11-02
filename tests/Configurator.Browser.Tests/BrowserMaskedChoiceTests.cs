namespace Configurator.Browser.Tests;

using System.Text;
using System.Text.Json.Nodes;
using Bunit;
using Configurator.Core.Definitions;
using Configurator.Presentation;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;

public sealed class BrowserMaskedChoiceTests
{
    [Test]
    [Arguments("sensitive")]
    [Arguments("secret")]
    public async Task MaskedChoicesOfferReplacementsWithoutRetainingTheSelectedAnswer(string sensitivity)
    {
        JsonNode source = JsonNode.Parse(BrowserFixture.Definition.CanonicalBytes.Span)!;
        BrowserFixture.WithField(source, "protectedChoice", $$$"""
            {"type":"string","enum":["alpha","beta"],"title":"Protected choice",
             "x-configurator":{"sensitivity":"{{{sensitivity}}}","detail":"basic"}}
            """);
        source["configuration"]!["required"]!.AsArray().Add("protectedChoice");
        var definition = DefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString()));
        await using var context = BrowserFixture.Context();
        using var form = new DefinitionFormModel(definition, DefinitionSessionPrivacy.BrowserSecretsEnabled);
        form.EditValue(["protectedChoice"], "beta");
        var component = context.Render<ConfigurationField>(parameters => parameters
            .Add(item => item.Form, form).Add(item => item.Path, ["protectedChoice"])
            .Add(item => item.Schema, definition.Configuration.Properties["protectedChoice"]));
        var chooser = component.FindComponent<MudSelect<string>>().Instance;
        await Assert.That(component.Find("input").GetAttribute("aria-required")).IsEqualTo("true");
        await component.Find("input").BlurAsync(new FocusEventArgs());
        await Assert.That(component.Find("input").GetAttribute("aria-invalid")).IsEqualTo("false");
        await Assert.That(chooser.Value).IsEqualTo("");
        await Assert.That(component.FindAll("input").Any(input => input.GetAttribute("value") is "beta" or "1")).IsFalse();
        var retired = chooser.ValueChanged;
        await component.InvokeAsync(() => retired.InvokeAsync("1"));
        long revision = form.InputRevision;
        await Assert.That(ReferenceEquals(component.FindComponent<MudSelect<string>>().Instance, chooser)).IsFalse();
        await Assert.That(component.FindComponent<MudSelect<string>>().Instance.Value).IsEqualTo("");
        await component.InvokeAsync(() => retired.InvokeAsync("0"));
        await Assert.That(form.InputRevision).IsEqualTo(revision);
        await Assert.That(form.Snapshot().Values["protectedChoice"].GetString()).IsEqualTo("beta");
        await component.InvokeAsync(() => component.FindComponent<MudSelect<string>>().Instance.ValueChanged.InvokeAsync("0"));
        await Assert.That(form.Snapshot().Values["protectedChoice"].GetString()).IsEqualTo("alpha");
        await Assert.That(component.FindComponent<MudSelect<string>>().Instance.Value).IsEqualTo("");
        await Assert.That(component.FindAll("input").Any(input => input.GetAttribute("value") is "alpha" or "0")).IsFalse();
        if (sensitivity == "secret")
        {
            var beforeRevoke = component.FindComponent<MudSelect<string>>().Instance.ValueChanged;
            await component.InvokeAsync(() => form.SetSecretEntryEnabled(false));
            component.Render();
            await Assert.That(component.FindComponents<MudSelect<string>>()).IsEmpty();
            await component.InvokeAsync(() => beforeRevoke.InvokeAsync("1"));
            await Assert.That(form.Snapshot().Values.ContainsKey("protectedChoice")).IsFalse();
        }
    }
}
