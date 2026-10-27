namespace Configurator.Browser.Tests;

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AngleSharp.Dom;
using Bunit;
using Configurator.Browser.Services;
using Configurator.Core.Definitions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using MudBlazor;

internal static class BrowserFixture
{
    internal const string Family = "d66d02af-6a66-4396-9717-452e8eb9541b";

    internal static SetupDefinition Definition
    {
        get
        {
            using Stream input = typeof(BrowserFixture).Assembly.GetManifestResourceStream("InteractiveDefinition")!;
            using var bytes = new MemoryStream();
            input.CopyTo(bytes);
            return SetupDefinitionCodec.ParseJson(bytes.ToArray());
        }
    }

    internal static SetupDefinition Revision(long revision, string appVersion = "1.0")
    {
        JsonNode node = JsonNode.Parse(Definition.CanonicalBytes.Span)!;
        node["revision"] = revision;
        node["applicationVersions"] = new JsonArray(appVersion);
        return SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(node.ToJsonString()));
    }

    internal static BunitContext Context(BrowserDefinitionRegistry? registry = null)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddSingleton(registry ?? new BrowserDefinitionRegistry([Definition]));
        context.Services.AddScoped<AppearanceThemeService>();
        context.Services.AddScoped<BrowserNavigationState>();
        context.Services.AddScoped<BrowserOfflinePolicy>();
        context.Services.AddScoped<WorkspaceSearchState>();
        context.Services.AddSingleton(new PopoverHost());
        context.Services.AddMudServices();
        var appearance = context.JSInterop.SetupModule("./browser-appearance.js");
        appearance.Setup<string?>("read", _ => true).SetResult(null);
        appearance.SetupVoid("watch", _ => true).SetVoidResult();
        appearance.SetupVoid("write", _ => true).SetVoidResult();
        appearance.SetupVoid("apply", _ => true).SetVoidResult();
        appearance.SetupVoid("unwatch", _ => true).SetVoidResult();
        var offline = context.JSInterop.SetupModule("./pwa-lifecycle.js");
        offline.Setup<BrowserOfflineState>("initialize", _ => true).SetResult(new BrowserOfflineState(true, true, true));
        offline.Setup<bool>("beginWorkspace", _ => true).SetResult(true);
        offline.SetupVoid("endWorkspace", _ => true).SetVoidResult();
        var interactions = context.JSInterop.SetupModule("./workspace-interactions.js");
        interactions.SetupVoid("watch", _ => true).SetVoidResult();
        interactions.SetupVoid("unwatch", _ => true).SetVoidResult();
        var rendering = context.JSInterop.SetupModule("./configuration-rendering.js");
        rendering.SetupVoid("nextFrame", _ => true).SetVoidResult();
        return context;
    }

    internal static async Task<IElement> OpenOmissionAsync<T>(BunitContext context, IRenderedComponent<T> component, string field)
        where T : class, IComponent
    {
        IRenderedComponent<IComponent> surface = component;
        if (component.FindComponents<MudPopoverProvider>().Count == 0)
        {
            var host = context.Services.GetRequiredService<PopoverHost>();
            surface = host.Provider ??= context.Render<MudPopoverProvider>();
        }
        var editor = component.FindComponents<ConfigurationField>().Single(item => string.Join('.', item.Instance.Path) == field).Instance;
        string identity = editor.Form.GetEditorIdentity(editor.Path);
        await component.Find($"[data-field-actions-id='{identity}'] button").ClickAsync(new MouseEventArgs());
        return surface.Find($"[data-omit-owner='{identity}']");
    }

    internal static async Task OmitAsync<T>(BunitContext context, IRenderedComponent<T> component, string field)
        where T : class, IComponent
    {
        var action = await OpenOmissionAsync(context, component, field);
        await action.ClickAsync(new MouseEventArgs());
    }

    private sealed class PopoverHost
    {
        internal IRenderedComponent<MudPopoverProvider>? Provider { get; set; }
    }

    internal static Task NotifyChoiceAsync<T>(IRenderedComponent<T> component, string field, string value)
        where T : class, IComponent
    {
        var editor = component.FindComponents<ConfigurationField>()
            .Single(item => string.Join('.', item.Instance.Path) == field);
        var choice = editor.FindComponent<MudSelect<string>>();
        return component.InvokeAsync(() => choice.Instance.ValueChanged.InvokeAsync(value));
    }

    internal static byte[] Template(string selections = """{"mode":"single"}""", SetupDefinition? definition = null)
    {
        definition ??= Definition;
        return Encoding.UTF8.GetBytes(
            $$"""
            {"schemaVersion":"configurator-public-template/v1","familyId":"{{definition.FamilyId:D}}",
             "revision":{{definition.Revision}},"definitionDigest":"{{definition.Digest.Value}}",
             "selections":{{selections}}}
            """);
    }

    internal static JsonDocument Download(string href) =>
        JsonDocument.Parse(Convert.FromBase64String(href["data:application/json;base64,".Length..]));
}
