namespace Configurator.Browser.Tests;

using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Bunit;
using Configurator.Browser.Components.Configuration;
using Configurator.Core.Definitions;
using Configurator.Presentation;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

public sealed class BrowserPreparationYieldFencingTests
{
    [Test]
    public async Task MidYieldSecretConsentRevocationCancelsPreparationAndPublishesNothing()
    {
        await using var context = BrowserFixture.Context();
        var renderingGate = new RenderingYieldGate(context.JSInterop.JSRuntime);
        context.Services.AddSingleton<IJSRuntime>(renderingGate);

        Definition definition = BuildNearLimitSecretDefinition(300);
        var component = context.Render<ConfigurationEditor>(parameters =>
            parameters.Add(item => item.Definition, definition));

        await BrowserFixture.SetSecretConsentAsync(component, true);
        var form = component.FindComponent<ConfigurationField>().Instance.Form;
        await component.InvokeAsync(() =>
        {
            form.EditValue(["hostname"], "yield-fence.example.invalid");
            form.EditValue(["credential"], "synthetic-yield-revoke-canary");
            for (int index = 0; index < 300; index++)
                form.EditValue(["settings", "item" + index.ToString(CultureInfo.InvariantCulture).PadLeft(4, '0')], "val");
        });

        await BrowserFixture.SelectPanelAsync(component, "prepare");

        renderingGate.HoldNextTask = true;
        Task preparing = component.Find("#prepare-complete").ClickAsync(new MouseEventArgs());

        await renderingGate.NextTaskEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(preparing.IsCompleted).IsFalse();

        await BrowserFixture.SetSecretConsentAsync(component, false);
        renderingGate.ReleaseNextTask.TrySetResult();
        await preparing.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.That(component.FindAll("[data-prepared-file]")).IsEmpty();
        await Assert.That(component.FindAll(".configuration-editor__artifact")).IsEmpty();
        await Assert.That(form.SessionPrivacy).IsEqualTo(DefinitionSessionPrivacy.BrowserNoSecrets);
        await Assert.That(form.Snapshot().Values.ContainsKey("credential")).IsFalse();
        await Assert.That(component.Markup).DoesNotContain("synthetic-yield-revoke-canary");
        await Assert.That(component.Find("[role=status]").GetAttribute("data-code")).IsNotEqualTo("preparation-ready");
    }

    [Test]
    public async Task MidYieldAnswerEditInvalidatesStalePreparation()
    {
        await using var context = BrowserFixture.Context();
        var renderingGate = new RenderingYieldGate(context.JSInterop.JSRuntime);
        context.Services.AddSingleton<IJSRuntime>(renderingGate);

        var component = context.Render<ConfigurationEditor>(parameters =>
            parameters.Add(item => item.Definition, BuildNearLimitSecretDefinition(300)));

        await BrowserFixture.SetSecretConsentAsync(component, true);
        var form = component.FindComponent<ConfigurationField>().Instance.Form;
        await component.InvokeAsync(() =>
        {
            form.EditValue(["hostname"], "before-edit.example.invalid");
            form.EditValue(["credential"], "synthetic-edit-canary");
            for (int index = 0; index < 300; index++)
                form.EditValue(["settings", "item" + index.ToString(CultureInfo.InvariantCulture).PadLeft(4, '0')], "val");
        });

        await BrowserFixture.SelectPanelAsync(component, "prepare");

        renderingGate.HoldNextTask = true;
        Task preparing = component.Find("#prepare-complete").ClickAsync(new MouseEventArgs());

        await renderingGate.NextTaskEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await component.InvokeAsync(() => form.EditValue(["hostname"], "after-edit.example.invalid"));
        renderingGate.ReleaseNextTask.TrySetResult();
        await preparing.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.That(component.FindAll("[data-prepared-file]")).IsEmpty();
        await Assert.That(component.FindAll(".configuration-editor__artifact")).IsEmpty();
        await Assert.That(component.Find("[role=status]").GetAttribute("data-code")).IsNotEqualTo("preparation-ready");
    }

    [Test]
    public async Task MidYieldComponentDisposalCancelsPreparationWithoutExceptions()
    {
        await using var context = BrowserFixture.Context();
        var renderingGate = new RenderingYieldGate(context.JSInterop.JSRuntime);
        context.Services.AddSingleton<IJSRuntime>(renderingGate);

        var component = context.Render<ConfigurationEditor>(parameters =>
            parameters.Add(item => item.Definition, BuildNearLimitSecretDefinition(300)));

        await BrowserFixture.SetSecretConsentAsync(component, true);
        var form = component.FindComponent<ConfigurationField>().Instance.Form;
        await component.InvokeAsync(() =>
        {
            form.EditValue(["hostname"], "dispose-fence.example.invalid");
            form.EditValue(["credential"], "synthetic-dispose-canary");
            for (int index = 0; index < 300; index++)
                form.EditValue(["settings", "item" + index.ToString(CultureInfo.InvariantCulture).PadLeft(4, '0')], "val");
        });

        await BrowserFixture.SelectPanelAsync(component, "prepare");

        renderingGate.HoldNextTask = true;
        Task preparing = component.Find("#prepare-complete").ClickAsync(new MouseEventArgs());

        await renderingGate.NextTaskEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await component.Instance.DisposeAsync();
        renderingGate.ReleaseNextTask.TrySetResult();
        await preparing.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Test]
    public async Task LargeFormReviewHydratesEffectsAsynchronouslyAndCancelsCleanlyOnReturnToConfigure()
    {
        await using var context = BrowserFixture.Context();
        var renderingGate = new RenderingYieldGate(context.JSInterop.JSRuntime);
        context.Services.AddSingleton<IJSRuntime>(renderingGate);

        Definition largeDefinition = BuildLargeFlatDefinition(160);
        var component = context.Render<ConfigurationEditor>(parameters =>
            parameters.Add(item => item.Definition, largeDefinition));

        // Hold frame-2 hydration before navigating to Review.
        renderingGate.HoldNextFrame = true;
        await BrowserFixture.SelectPanelAsync(component, "review");

        await renderingGate.NextFrameEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Frame 1 renders summary readiness & optional-missing count immediately without waiting for effects hydration.
        await Assert.That(component.Find("[data-review-readiness]").GetAttribute("data-review-readiness")).IsNotNull();
        await Assert.That(component.Find("[data-review-optional-missing]").GetAttribute("data-review-optional-missing")).IsEqualTo("150");
        await Assert.That(component.Find("[data-review-effects]").GetAttribute("data-review-effects-hydrated")).IsEqualTo("false");

        // Navigate back to Configure BEFORE frame-2 hydration fires; releasing the frame must cancel cleanly.
        await BrowserFixture.SelectPanelAsync(component, "configure");
        renderingGate.ReleaseNextFrame.TrySetResult();
        await component.InvokeAsync(() => Task.CompletedTask);
        await Assert.That(component.FindAll("[data-workflow-panel='review']")).IsEmpty();

        // Navigate to Review again and let frame-2 hydration complete normally.
        renderingGate.ResetFrameGate(hold: true);
        await BrowserFixture.SelectPanelAsync(component, "review");
        await renderingGate.NextFrameEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        renderingGate.ReleaseNextFrame.TrySetResult();
        component.WaitForAssertion(() =>
        {
            if (component.Find("[data-review-effects]").GetAttribute("data-review-effects-hydrated") != "true")
                throw new InvalidOperationException("review-effects-not-hydrated");
        }, TimeSpan.FromSeconds(5));
        await Assert.That(component.Find("[data-review-count]").GetAttribute("data-review-count")).IsEqualTo("10");
    }

    private static Definition BuildNearLimitSecretDefinition(int nestedCount)
    {
        var nestedProperties = new JsonObject();
        var nestedRequired = new JsonArray();
        for (int index = 0; index < nestedCount; index++)
        {
            string name = "item" + index.ToString(CultureInfo.InvariantCulture).PadLeft(4, '0');
            nestedProperties[name] = new JsonObject { ["type"] = "string", ["minLength"] = 1 };
            nestedRequired.Add(name);
        }

        var root = new JsonObject
        {
            ["$schema"] = "urn:islamu:configurator:authoring:v0.0.1",
            ["identity"] = new JsonObject { ["familyId"] = "ea050007-4ef0-4c80-9f10-000000000300", ["revision"] = 1 },
            ["project"] = new JsonObject { ["id"] = "example.yield", ["name"] = "Yield Fence", ["publisher"] = "example" },
            ["applicationVersions"] = new JsonArray("1.0"),
            ["entryPoints"] = new JsonArray("setup"),
            ["configuration"] = new JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = false,
                ["required"] = new JsonArray("hostname", "credential", "settings"),
                ["x-configurator"] = new JsonObject { ["sensitivity"] = "public" },
                ["properties"] = new JsonObject
                {
                    ["hostname"] = new JsonObject { ["type"] = "string", ["minLength"] = 1 },
                    ["credential"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["minLength"] = 8,
                        ["x-configurator"] = new JsonObject { ["sensitivity"] = "secret" }
                    },
                    ["settings"] = new JsonObject
                    {
                        ["type"] = "object",
                        ["additionalProperties"] = false,
                        ["required"] = nestedRequired,
                        ["properties"] = nestedProperties
                    }
                }
            },
            ["outputs"] = new JsonObject
            {
                ["bundle.json"] = new JsonObject
                {
                    ["id"] = "bundle",
                    ["format"] = "json",
                    ["targetUpdateMode"] = "create",
                    ["restartRequirement"] = "none",
                    ["projection"] = new JsonObject
                    {
                        ["op"] = "object",
                        ["members"] = new JsonObject
                        {
                            ["hostname"] = new JsonObject
                            {
                                ["op"] = "copy",
                                ["reference"] = new JsonObject { ["scope"] = "root", ["path"] = "/hostname" }
                            },
                            ["credential"] = new JsonObject
                            {
                                ["op"] = "copy",
                                ["reference"] = new JsonObject { ["scope"] = "root", ["path"] = "/credential" }
                            },
                            ["settings"] = new JsonObject
                            {
                                ["op"] = "copy",
                                ["reference"] = new JsonObject { ["scope"] = "root", ["path"] = "/settings" }
                            }
                        }
                    }
                }
            }
        };
        return DefinitionSourceCodec.ParseJson(Encoding.UTF8.GetBytes(root.ToJsonString()));
    }

    private static Definition BuildLargeFlatDefinition(int count)
    {
        var properties = new JsonObject();
        var required = new JsonArray();
        var members = new JsonObject();
        for (int index = 0; index < count; index++)
        {
            string name = "field" + index.ToString(CultureInfo.InvariantCulture).PadLeft(4, '0');
            properties[name] = new JsonObject { ["type"] = "string", ["minLength"] = 1 };
            if (index < 10) required.Add(name);
            members[name] = new JsonObject
            {
                ["op"] = "copy",
                ["reference"] = new JsonObject { ["scope"] = "root", ["path"] = "/" + name }
            };
        }
        var root = new JsonObject
        {
            ["$schema"] = "urn:islamu:configurator:authoring:v0.0.1",
            ["identity"] = new JsonObject { ["familyId"] = "ea050007-4ef0-4c80-9f10-000000000160", ["revision"] = 1 },
            ["project"] = new JsonObject { ["id"] = "example.large", ["name"] = "Large", ["publisher"] = "example" },
            ["applicationVersions"] = new JsonArray("1.0"),
            ["entryPoints"] = new JsonArray("setup"),
            ["configuration"] = new JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = false,
                ["required"] = required,
                ["x-configurator"] = new JsonObject { ["sensitivity"] = "public" },
                ["properties"] = properties
            },
            ["outputs"] = new JsonObject
            {
                ["config.json"] = new JsonObject
                {
                    ["id"] = "config",
                    ["format"] = "json",
                    ["targetUpdateMode"] = "create",
                    ["restartRequirement"] = "none",
                    ["projection"] = new JsonObject { ["op"] = "object", ["members"] = members }
                }
            }
        };
        return DefinitionSourceCodec.ParseJson(Encoding.UTF8.GetBytes(root.ToJsonString()));
    }

    private sealed class RenderingYieldGate(IJSRuntime fallback) : IJSRuntime
    {
        internal bool HoldNextTask { get; set; }
        internal bool HoldNextFrame { get; set; }
        internal TaskCompletionSource NextTaskEntered { get; private set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource ReleaseNextTask { get; private set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource NextFrameEntered { get; private set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource ReleaseNextFrame { get; private set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal void ResetFrameGate(bool hold)
        {
            HoldNextFrame = hold;
            NextFrameEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
            ReleaseNextFrame = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public async ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            if (identifier == "import" && args?.FirstOrDefault()?.ToString() == "./js/configuration-rendering.js")
            {
                IJSObjectReference inner = await fallback.InvokeAsync<IJSObjectReference>(identifier, cancellationToken, args);
                return (TValue)(object)new GatedRenderingModule(this, inner);
            }
            return await fallback.InvokeAsync<TValue>(identifier, cancellationToken, args);
        }

        private sealed class GatedRenderingModule(RenderingYieldGate gate, IJSObjectReference inner) : IJSObjectReference
        {
            public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
                InvokeAsync<TValue>(identifier, CancellationToken.None, args);

            public async ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            {
                if (identifier == "nextTask" && gate.HoldNextTask)
                {
                    gate.NextTaskEntered.TrySetResult();
                    await gate.ReleaseNextTask.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
                    return default!;
                }
                if (identifier == "nextFrame" && gate.HoldNextFrame)
                {
                    gate.NextFrameEntered.TrySetResult();
                    await gate.ReleaseNextFrame.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
                    return default!;
                }
                return await inner.InvokeAsync<TValue>(identifier, cancellationToken, args);
            }

            public ValueTask DisposeAsync() => inner.DisposeAsync();
        }
    }
}
