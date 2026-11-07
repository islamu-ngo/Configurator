namespace Configurator.Browser.Tests;

using System.Text;
using System.Text.Json;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

public sealed class BrowserRecursiveConformanceTests
{
    [Test]
    public async Task LiteralCorpusImportsAndPreparesThroughTheRenderedWorkspace()
    {
        using Stream input = BrowserRootEditingTests.Resource("Conformance.Cases");
        using JsonDocument corpus = JsonDocument.Parse(input);
        foreach (JsonElement item in corpus.RootElement.GetProperty("cases").EnumerateArray())
        {
            string identity = item.GetProperty("id").GetString()!;
            string resource = item.GetProperty("definition").GetString() switch
            {
                "root-map" => "Conformance.RootMap",
                "root-variant" => "Conformance.RootVariant",
                "synthetic-library" => "Conformance.Library",
                _ => throw new InvalidDataException("Unknown conformance definition.")
            };
            await using var context = BrowserFixture.Context();
            var root = context.Render<ConfigurationEditor>(parameters => parameters.Add(component => component.Definition, BrowserRootEditingTests.Definition(resource)));
            string? observed = null;
            var module = context.JSInterop.SetupModule("./js/browser-artifacts.js");
            module.SetupVoid("register", _ => true).SetVoidResult();
            module.SetupVoid("open", invocation =>
            {
                observed = Encoding.UTF8.GetString((byte[])invocation.Arguments[2]!);
                return true;
            }).SetVoidResult();
            module.Setup<bool>("reveal", _ => true).SetResult(true);
            var form = root.FindComponents<ConfigurationField>()[0].Instance.Form;
            await root.Find("#secret-consent").ChangeAsync(new ChangeEventArgs { Value = true });
            using var source = new MemoryStream(Encoding.UTF8.GetBytes(item.GetProperty("values").GetRawText()));
            if (resource == "Conformance.Library")
            {
                // The scoped output discards branch identity and is not an answer import format.
                await root.InvokeAsync(async () =>
                    await Assert.That(form.SetValue(Configurator.Core.Definitions.DefinitionPath.FromSegments([]),
                        item.GetProperty("values"))).IsTrue().Because(identity));
            }
            else
                await root.InvokeAsync(() => root.Instance.LoadAsync(source));
            foreach (JsonElement omitted in item.GetProperty("omitted").EnumerateArray())
                await BrowserRootEditingTests.Action(root, Configurator.Core.Definitions.DefinitionPath.Parse(omitted.GetString()!).Segments.ToArray(),
                    actions => actions.OnOmit);
            if (resource == "Conformance.Library")
            {
                long revision = form.InputRevision;
                await root.InvokeAsync(() => form.SelectLocale("ar"));
                await Assert.That(form.Direction).IsEqualTo("rtl");
                await Assert.That(form.InputRevision).IsEqualTo(revision);
                await Assert.That(root.FindAll("*").Any(element =>
                    element.TextContent.Contains("تفعيل الإعارة", StringComparison.Ordinal))).IsTrue().Because(identity);
                await Assert.That(root.FindAll("*").Any(element =>
                    element.TextContent.Contains("الفروع", StringComparison.Ordinal))).IsTrue().Because(identity);
            }
            await Assert.That(form.Validate().IsReady).IsEqualTo(item.GetProperty("ready").GetBoolean()).Because(identity);
            await Assert.That(context.JSInterop.Invocations.Any(call => call.Arguments.Any(argument => argument is byte[]))).IsFalse().Because(identity);
            await CheckOutput(false, "expectedJson");
            await BrowserFixture.SelectPanelAsync(root, "configure");
            await CheckOutput(true, "expectedDraftJson");

            async Task CheckOutput(bool draft, string expectedProperty)
            {
                observed = null;
                await BrowserFixture.PrepareAsync(root, draft);
                string? expected = item.GetProperty(expectedProperty).GetString();
                await Assert.That(root.FindAll("[data-prepared-file]").Count).IsEqualTo(expected is null ? 0 : 1).Because(identity);
                if (expected is null) return;
                await root.Find("[data-action='reveal-file']").ClickAsync(new MouseEventArgs());
                await Assert.That(observed).IsEqualTo(expected).Because(identity);
            }
        }
    }
}
