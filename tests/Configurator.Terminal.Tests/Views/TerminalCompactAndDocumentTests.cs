namespace Configurator.Terminal.Tests;

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CommunityToolkit.Mvvm.Messaging;
using Configurator.Core.Definitions;
using Configurator.Presentation;
using Configurator.Terminal;
using global::Terminal.Gui.App;
using global::Terminal.Gui.Input;
using global::Terminal.Gui.ViewBase;
using global::Terminal.Gui.Views;

public sealed class TerminalCompactAndDocumentTests
{
    private static Definition RichDefinition()
    {
        JsonObject source = JsonNode.Parse("""
            {"$schema":"urn:islamu:configurator:authoring:v0.0.1",
             "identity":{"familyId":"5c11a8f3-8d95-4b23-b1dc-8e2f6308339d","revision":1},
             "project":{"id":"terminal.rich","name":"Terminal Rich","publisher":"example"},
             "applicationVersions":["1.0"],"entryPoints":["setup"],
             "localization":{"defaultLocale":"en","translations":{"ar":{"direction":"rtl","overrides":{}}}},
             "configuration":{"type":"object","additionalProperties":false,
               "required":["serviceName","apiKey"],
               "x-configurator":{"sensitivity":"public"},
               "properties":{
                 "serviceName":{"title":"Service name","type":"string","default":"catalog",
                   "x-configurator":{"sensitivity":"public","detail":"basic","defaultText":"catalog"}},
                 "delivery":{"title":"Delivery mode","type":"string","enum":["sync","async"],
                   "x-configurator":{"sensitivity":"public","detail":"basic","choices":[
                     {"value":"sync","label":"Synchronous HTTP","explanation":"Blocks caller until processed"},
                     {"value":"async","label":"Background Queue","explanation":"Queues work asynchronously","deprecated":true,"deprecationMessage":"Use webhook v2"}
                   ]}},
                 "tlsEnabled":{"title":"Enable TLS","type":"boolean",
                   "x-configurator":{"sensitivity":"public","detail":"basic","booleanLabels":{"true":"TLS On","false":"TLS Off"}}},
                 "secretMode":{"title":"Protected mode","type":"string","enum":["strict","relaxed"],
                   "x-configurator":{"sensitivity":"secret","detail":"basic"}},
                 "apiKey":{"title":"API key","type":"string",
                   "x-configurator":{"sensitivity":"secret","detail":"basic"}},
                 "privateHeaders":{"title":"Private headers","type":"object",
                   "additionalProperties":{"type":"string","x-configurator":{"sensitivity":"secret"}},
                   "x-configurator":{"sensitivity":"secret","detail":"basic"}}
               }},
             "presentation":{"defaultJourney":"guided","navigation":"sections",
               "sections":[{"id":"general","title":"General","pointers":["/serviceName","/delivery","/tlsEnabled","/secretMode","/apiKey","/privateHeaders"]}],
               "journeys":[{"id":"guided","title":"Guided","sections":["general"]}]},
             "outputs":{"config.json":{"id":"config","format":"json","targetUpdateMode":"create","restartRequirement":"process",
               "projection":{"op":"object","members":{
                 "service":{"op":"copy","reference":{"scope":"root","path":"/serviceName"}},
                 "delivery":{"op":"copy","reference":{"scope":"root","path":"/delivery"}},
                 "tls":{"op":"copy","reference":{"scope":"root","path":"/tlsEnabled"}},
                 "key":{"op":"copy","reference":{"scope":"root","path":"/apiKey"}}}}}}}
            """)!.AsObject();
        return DefinitionSourceCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString()));
    }

    private static readonly string[] ExpectedBooleanLabels = ["TLS Off", "TLS On"];
    private static readonly string[] ExpectedBooleanTokens = ["false", "true"];

    [Test]
    public async Task FieldEditorAndChoiceDialogUseLocalizedChoicePresentationWithoutLeakingSecretChoices()
    {
        using var form = new DefinitionFormModel(RichDefinition());
        var root = new TerminalRootEditor(form).Slots;

        var deliverySlot = root.Single(slot => slot.Path is ["delivery"]);
        IReadOnlyList<DefinitionChoicePresentation> deliveryChoices = TerminalFieldEditor.ChoicePresentations(deliverySlot);
        await Assert.That(deliveryChoices.Count).IsEqualTo(2);
        await Assert.That(deliveryChoices[0].DisplayLabel).IsEqualTo("Synchronous HTTP");
        await Assert.That(deliveryChoices[0].WireToken).IsEqualTo("sync");
        await Assert.That(deliveryChoices[0].Explanation).IsEqualTo("Blocks caller until processed");
        await Assert.That(deliveryChoices[1].IsDeprecated).IsTrue();
        await Assert.That(deliveryChoices[1].DeprecationGuidance).IsEqualTo("Use webhook v2");

        var boolSlot = root.Single(slot => slot.Path is ["tlsEnabled"]);
        IReadOnlyList<DefinitionChoicePresentation> boolChoices = TerminalFieldEditor.ChoicePresentations(boolSlot);
        await Assert.That(boolChoices.Select(choice => choice.DisplayLabel).ToArray())
            .IsEquivalentTo(ExpectedBooleanLabels);
        await Assert.That(boolChoices.Select(choice => choice.WireToken).ToArray())
            .IsEquivalentTo(ExpectedBooleanTokens);

        var secretEnumSlot = root.Single(slot => slot.Path is ["secretMode"]);
        await Assert.That(TerminalFieldEditor.ChoicePresentations(secretEnumSlot)).IsEmpty();
        await Assert.That(TerminalFieldEditor.Choices(secretEnumSlot)).IsEmpty();

        using var application = Application.Create();
        application.Init("dotnet");
        using var dialog = new TerminalChoiceDialog(deliveryChoices, form.Locale, currentWireToken: "sync");
        await Assert.That(dialog.X).IsEqualTo(Pos.Center());
        await Assert.That(dialog.Y).IsEqualTo(Pos.Center());
        dialog.SelectIndex(1);
        dialog.ConfirmSelection();
        await Assert.That(dialog.SelectedChoice?.WireToken).IsEqualTo("async");
        await Assert.That(dialog.SelectedChoice?.DisplayLabel).IsEqualTo("Background Queue");
    }

    [Test]
    public async Task CompactTableViewVirtualizesCompactPropertyRowsAndMasksPrivateValues()
    {
        using var form = new DefinitionFormModel(RichDefinition());
        form.SetValue(["serviceName"], JsonSerializer.SerializeToElement("orders-api"));
        form.SetValue(["apiKey"], JsonSerializer.SerializeToElement("super-secret-api-key"));
        form.SetValue(["privateHeaders"], JsonSerializer.SerializeToElement(
            new Dictionary<string, string> { ["internal-header-name"] = "super-secret-header" }));

        using var table = new TerminalCompactTableView(form);
        ITableSource source = table.Table ?? throw new InvalidOperationException("compact-table-source-missing");
        await Assert.That(source.Rows).IsGreaterThan(0);
        await Assert.That(source.Columns).IsGreaterThanOrEqualTo(4);

        var allCells = new StringBuilder();
        for (int r = 0; r < source.Rows; r++)
            for (int c = 0; c < source.Columns; c++)
                allCells.Append(source[r, c]?.ToString()).Append('\n');
        string dumped = allCells.ToString();
        await Assert.That(dumped).Contains("orders-api");
        await Assert.That(dumped).DoesNotContain("super-secret-api-key");
        await Assert.That(dumped).DoesNotContain("internal-header-name");
        await Assert.That(dumped).DoesNotContain("super-secret-header");

        await Assert.That(table.SelectRowByPath(DefinitionPath.FromSegments(["serviceName"]))).IsTrue();
        await Assert.That(table.TryEditSelected("catalog-v2")).IsTrue();
        await Assert.That(form.Snapshot().Values["serviceName"].GetString()).IsEqualTo("catalog-v2");

        await Assert.That(table.TryOmitSelected()).IsTrue();
        await Assert.That(form.Snapshot().Values.ContainsKey("serviceName")).IsFalse();
    }

    [Test]
    public async Task DocumentDialogEnforcesByteLimitDiffPreviewStaleRevisionAndBufferZeroing()
    {
        using var form = new DefinitionFormModel(RichDefinition());
        form.SetValue(["serviceName"], JsonSerializer.SerializeToElement("initial-svc"));
        form.SetValue(["apiKey"], JsonSerializer.SerializeToElement("untouched-secret-value"));

        using var dialog = new TerminalDocumentDialog(form, DefinitionDocumentRepresentation.Canonical);
        await Assert.That(dialog.X).IsEqualTo(Pos.Center());
        await Assert.That(dialog.Y).IsEqualTo(Pos.Center());

        byte[] oversized = new byte[(256 * 1024) + 1];
        Array.Fill(oversized, (byte)'a');
        DefinitionDocumentParseResult tooLarge = dialog.TryPreparePreview(oversized);
        await Assert.That(tooLarge.Succeeded).IsFalse();
        await Assert.That(tooLarge.Diagnostics.Any(d => d.Code == "document-limit")).IsTrue();

        byte[] updatedDoc = Encoding.UTF8.GetBytes("""{"answerContractVersion":"0.0.1","values":{"serviceName":"edited-svc","tlsEnabled":true},"omitted":[]}""");
        DefinitionDocumentParseResult preview = dialog.TryPreparePreview(updatedDoc);
        await Assert.That(preview.Succeeded).IsTrue();
        await Assert.That(dialog.PreviewChanges.Any(change => change.Path == DefinitionPath.FromSegments(["serviceName"]))).IsTrue();

        // Advance underlying form revision -> apply must fail closed with document-stale!
        form.SetValue(["delivery"], JsonSerializer.SerializeToElement("async"));
        DefinitionDocumentApplyResult stale = dialog.ApplyPrepared();
        await Assert.That(stale.Succeeded).IsFalse();
        await Assert.That(stale.Code).IsEqualTo("document-stale");

        // Reopen fresh dialog, prepare valid diff, and verify apply + zeroization + no OSC52.
        using var fresh = new TerminalDocumentDialog(form, DefinitionDocumentRepresentation.Canonical);
        DefinitionDocumentParseResult freshPreview = fresh.TryPreparePreview(updatedDoc);
        await Assert.That(freshPreview.Succeeded).IsTrue();
        DefinitionDocumentApplyResult applied = fresh.ApplyPrepared();
        await Assert.That(applied.Succeeded).IsTrue();
        await Assert.That(form.Snapshot().Values["serviceName"].GetString()).IsEqualTo("edited-svc");
        await Assert.That(form.Snapshot().Values["apiKey"].GetString()).IsEqualTo("untouched-secret-value");
        await Assert.That(fresh.BufferByteLength).IsEqualTo(0);
        await Assert.That(fresh.ExportAllVisibleText()).DoesNotContain("\x1b]52;");
        await Assert.That(fresh.ExportAllVisibleText()).DoesNotContain("untouched-secret-value");
    }

    [Test]
    public async Task DiscoveryDialogFiltersFieldsAndJumpsToMatchingField()
    {
        using var form = new DefinitionFormModel(RichDefinition());
        form.SetValue(["serviceName"], JsonSerializer.SerializeToElement("catalog-api"));

        using var dialog = new TerminalDiscoveryDialog(form);
        await Assert.That(dialog.X).IsEqualTo(Pos.Center());
        await Assert.That(dialog.Y).IsEqualTo(Pos.Center());

        dialog.SetSearch("tls", DefinitionDiscoveryFilter.MissingRequired);
        await Assert.That(dialog.Matches.Count).IsEqualTo(0);

        dialog.SetSearch("tls", DefinitionDiscoveryFilter.All);
        await Assert.That(dialog.Matches.Any(match => match.Entry.FieldId == "tlsEnabled")).IsTrue();

        await Assert.That(dialog.ActivateMatch(0)).IsTrue();
        await Assert.That(dialog.SelectedPath).IsEqualTo(DefinitionPath.FromSegments(["tlsEnabled"]));
        await Assert.That(form.IsEditorVisible(["tlsEnabled"])).IsTrue();
    }
}
