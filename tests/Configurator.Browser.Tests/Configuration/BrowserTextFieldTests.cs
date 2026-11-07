namespace Configurator.Browser.Tests;

using Bunit;
using Configurator.Browser.Components.Common;
using MudBlazor;

public sealed class BrowserTextFieldTests
{
    [Test]
    [Arguments("value")]
    [Arguments("identity")]
    [Arguments("masked")]
    [Arguments("multiline")]
    [Arguments("read-only")]
    [Arguments("input-mode")]
    [Arguments("disabled")]
    [Arguments("required")]
    [Arguments("error")]
    [Arguments("error-text")]
    [Arguments("attribute-added")]
    [Arguments("attribute-removed")]
    [Arguments("attribute-changed")]
    [Arguments("attributes-cleared")]
    public async Task ChangedParametersRefreshTheVisibleControl(string change)
    {
        await using var context = BrowserFixture.Context();
        var attributes = new Dictionary<string, object> { ["data-hint"] = "before" };
        var component = context.Render<AppTextField>(parameters => parameters
            .Add(item => item.InputId, "before-id")
            .Add(item => item.Value, "before")
            .Add(item => item.Error, change == "error-text")
            .Add(item => item.ErrorText, "before-error")
            .Add(item => item.InputAttributes, attributes));

        component.Render(parameters =>
        {
            switch (change)
            {
                case "value": parameters.Add(item => item.Value, "after"); break;
                case "identity": parameters.Add(item => item.InputId, "after-id"); break;
                case "masked": parameters.Add(item => item.Masked, true); break;
                case "multiline": parameters.Add(item => item.Multiline, true); break;
                case "read-only": parameters.Add(item => item.ReadOnly, true); break;
                case "input-mode": parameters.Add(item => item.InputMode, InputMode.numeric); break;
                case "disabled": parameters.Add(item => item.Disabled, true); break;
                case "required": parameters.Add(item => item.Required, true); break;
                case "error": parameters.Add(item => item.Error, true); break;
                case "error-text": parameters.Add(item => item.ErrorText, "after-error"); break;
                case "attribute-added":
                    parameters.Add(item => item.InputAttributes,
                        new Dictionary<string, object> { ["data-hint"] = "before", ["data-added"] = "after" });
                    break;
                case "attribute-removed":
                    parameters.Add(item => item.InputAttributes, new Dictionary<string, object>());
                    break;
                case "attribute-changed":
                    parameters.Add(item => item.InputAttributes, new Dictionary<string, object> { ["data-hint"] = "after" });
                    break;
                case "attributes-cleared": parameters.Add(item => item.InputAttributes, null); break;
                default: throw new ArgumentOutOfRangeException(nameof(change));
            }
        });

        switch (change)
        {
            case "value":
                await Assert.That(component.Find("input").GetAttribute("value")).IsEqualTo("after");
                break;
            case "identity":
                await Assert.That(component.Find("input").Id).IsEqualTo("after-id");
                break;
            case "masked":
                await Assert.That(component.Find("input").GetAttribute("type")).IsEqualTo("password");
                break;
            case "multiline":
                await Assert.That(component.FindAll("input")).IsEmpty();
                await Assert.That(component.Find("textarea").GetAttribute("value")).IsEqualTo("before");
                break;
            case "read-only":
                await Assert.That(component.Find("input").HasAttribute("readonly")).IsTrue();
                break;
            case "input-mode":
                await Assert.That(component.Find("input").GetAttribute("inputmode")).IsEqualTo("numeric");
                break;
            case "disabled":
                await Assert.That(component.Find("input").HasAttribute("disabled")).IsTrue();
                break;
            case "required":
                await Assert.That(component.Find("input").GetAttribute("aria-required")).IsEqualTo("true");
                break;
            case "error":
                await Assert.That(component.Find("#before-id-error").HasAttribute("hidden")).IsFalse();
                break;
            case "error-text":
                await Assert.That(component.Find("#before-id-error").TextContent).IsEqualTo("after-error");
                break;
            case "attribute-added":
                await Assert.That(component.Find("[data-added]").GetAttribute("data-added")).IsEqualTo("after");
                break;
            case "attribute-removed":
            case "attributes-cleared":
                await Assert.That(component.FindAll("[data-hint]")).IsEmpty();
                break;
            case "attribute-changed":
                await Assert.That(component.Find("[data-hint]").GetAttribute("data-hint")).IsEqualTo("after");
                break;
            default: throw new ArgumentOutOfRangeException(nameof(change));
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ReusedAttributeDictionaryChangesRemainVisible(bool replaceWithEquivalentFirst)
    {
        await using var context = BrowserFixture.Context();
        var attributes = new Dictionary<string, object> { ["data-hint"] = "before" };
        var component = context.Render<AppTextField>(parameters => parameters
            .Add(item => item.InputId, "mutable-attributes")
            .Add(item => item.InputAttributes, attributes));
        if (replaceWithEquivalentFirst)
        {
            attributes = new Dictionary<string, object> { ["data-hint"] = "before" };
            component.Render(parameters => parameters.Add(item => item.InputAttributes, attributes));
        }

        attributes["data-hint"] = "after";
        component.Render(parameters => parameters.Add(item => item.InputAttributes, attributes));

        await Assert.That(component.Find("[data-hint]").GetAttribute("data-hint")).IsEqualTo("after");
    }
}
