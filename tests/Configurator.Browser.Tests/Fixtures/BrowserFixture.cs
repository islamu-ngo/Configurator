namespace Configurator.Browser.Tests;

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AngleSharp.Dom;
using Bunit;
using Configurator.Browser.Services;
using Configurator.Core.Definitions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Sections;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;

internal static class BrowserFixture
{
    internal const string Family = "d66d02af-6a66-4396-9717-452e8eb9541b";

    internal static Definition Definition
    {
        get
        {
            using Stream input = typeof(BrowserFixture).Assembly.GetManifestResourceStream("InteractiveDefinition")!;
            using var bytes = new MemoryStream();
            input.CopyTo(bytes);
            return DefinitionCodec.ParseJson(bytes.ToArray());
        }
    }

    internal static Definition Revision(long revision, string appVersion = "1.0")
    {
        JsonNode node = JsonNode.Parse(Definition.CanonicalBytes.Span)!;
        node["revision"] = revision;
        node["applicationVersions"] = new JsonArray(appVersion);
        return DefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(node.ToJsonString()));
    }

    internal static void WithField(JsonNode definition, string name, string schema, params string[] capabilities)
    {
        definition["configuration"]!["properties"]![name] = JsonNode.Parse(schema);
        SetCapabilities(definition, definition["requiredCapabilities"]!.AsArray()
            .Select(item => item!.GetValue<string>()).Concat(capabilities).ToArray());
    }

    internal static void SetCapabilities(JsonNode definition, params string[] capabilities) =>
        definition["requiredCapabilities"] = new JsonArray(capabilities.Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal).Select(value => (JsonNode?)JsonValue.Create(value)).ToArray());

    internal static BunitContext Context(BrowserDefinitionRegistry? registry = null)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddSingleton(registry ?? new BrowserDefinitionRegistry([Definition]));
        context.Services.AddScoped<AppearanceThemeService>();
        context.Services.AddScoped<BrowserNavigationState>();
        context.Services.AddScoped<BrowserOfflinePolicy>();
        context.Services.AddScoped<DefinitionStoreCatalog>();
        context.Services.AddScoped<WorkspaceSearchState>();
        context.Services.AddSingleton(new PopoverHost());
        context.Services.AddSingleton(new SectionNavigationHost());
        context.Services.AddMudServices();
        var appearance = context.JSInterop.SetupModule("./js/browser-appearance.js");
        appearance.Setup<string?>("read", _ => true).SetResult(null);
        appearance.SetupVoid("watch", _ => true).SetVoidResult();
        appearance.SetupVoid("write", _ => true).SetVoidResult();
        appearance.SetupVoid("apply", _ => true).SetVoidResult();
        appearance.SetupVoid("unwatch", _ => true).SetVoidResult();
        var offline = context.JSInterop.SetupModule("./pwa-lifecycle.js");
        offline.Setup<BrowserOfflineState>("initialize", _ => true).SetResult(new BrowserOfflineState(true, true, true));
        offline.Setup<bool>("beginWorkspace", _ => true).SetResult(true);
        offline.SetupVoid("endWorkspace", _ => true).SetVoidResult();
        var store = context.JSInterop.SetupModule("./js/browser-store.js");
        store.Setup<byte[]>("readAsset", _ => true).SetResult([]);
        var interactions = context.JSInterop.SetupModule("./js/workspace-interactions.js");
        interactions.SetupVoid("watch", _ => true).SetVoidResult();
        interactions.SetupVoid("unwatch", _ => true).SetVoidResult();
        var rendering = context.JSInterop.SetupModule("./js/configuration-rendering.js");
        rendering.SetupVoid("nextFrame", _ => true).SetVoidResult();
        rendering.SetupVoid("nextTask", _ => true).SetVoidResult();
        return context;
    }

    internal static async Task SelectPanelAsync<T>(IRenderedComponent<T> component, string panel)
        where T : class, IComponent
    {
        await component.InvokeAsync(async () =>
        {
            IElement action = component.Find($"[data-workflow-step='{panel}']");
            if (action.GetAttribute("aria-current") != "step") await action.ClickAsync(new MouseEventArgs());
        });
    }

    internal static async Task PrepareAsync<T>(IRenderedComponent<T> component, bool draft)
        where T : class, IComponent
    {
        await SelectPanelAsync(component, "prepare");
        await component.Find(draft ? "#prepare-draft" : "#prepare-complete").ClickAsync(new MouseEventArgs());
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

    internal static IRenderedComponent<IComponent> SectionNavigation<T>(BunitContext context, IRenderedComponent<T> component)
        where T : class, IComponent
    {
        if (component.FindComponents<SectionOutlet>().Count > 0) return component;
        var host = context.Services.GetRequiredService<SectionNavigationHost>();
        return host.Outlet ??= context.Render<SectionOutlet>(parameters =>
            parameters.Add(item => item.SectionName, "configuration-sections"));
    }

    private sealed class SectionNavigationHost
    {
        internal IRenderedComponent<SectionOutlet>? Outlet { get; set; }
    }

    internal static Task NotifyChoiceAsync<T>(IRenderedComponent<T> component, string field, string value)
        where T : class, IComponent
    {
        IElement? output = component.FindAll("#configuration-output").SingleOrDefault();
        if (output?.GetAttribute("data-output-field") == field)
        {
            IElement option = output.QuerySelectorAll("option").Single(item => item.GetAttribute("data-output-value") == value);
            return output.ChangeAsync(new ChangeEventArgs { Value = option.GetAttribute("value") });
        }
        var editor = component.FindComponents<ConfigurationField>()
            .Single(item => string.Join('.', item.Instance.Path) == field);
        string selection = value;
        // Resolve canonical declared values, never translated labels or ordinal aliases.
        if (editor.Instance.Schema.Keywords.TryGetProperty("enum", out JsonElement choices))
        {
            int index = choices.EnumerateArray().Select((item, ordinal) => (item, ordinal)).Single(candidate =>
                candidate.item.ValueKind == JsonValueKind.String ? candidate.item.GetString() == value
                    : candidate.item.GetRawText() == value).ordinal;
            selection = index.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        var radios = editor.FindComponents<MudRadioGroup<string>>().SingleOrDefault();
        if (radios is not null)
            return component.InvokeAsync(() => radios.Instance.ValueChanged.InvokeAsync(selection));
        var choice = editor.FindComponent<MudSelect<string>>();
        return component.InvokeAsync(() => choice.Instance.ValueChanged.InvokeAsync(selection));
    }

    internal static JsonDocument Download(string href) =>
        JsonDocument.Parse(Convert.FromBase64String(href["data:application/json;base64,".Length..]));
}
