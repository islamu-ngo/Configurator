namespace Configurator.Browser.Tests;

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bunit;
using Configurator.Browser.Services;
using Configurator.Core.Definitions;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

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
        context.Services.AddMudServices();
        var appearance = context.JSInterop.SetupModule("./browser-appearance.js");
        appearance.Setup<string?>("read", _ => true).SetResult(null);
        appearance.SetupVoid("watch", _ => true).SetVoidResult();
        appearance.SetupVoid("write", _ => true).SetVoidResult();
        appearance.SetupVoid("apply", _ => true).SetVoidResult();
        appearance.SetupVoid("unwatch", _ => true).SetVoidResult();
        return context;
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
