namespace Configurator.Browser.Tests;

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bunit;
using Configurator.Core.Definitions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;

public sealed class BrowserImportComparisonTests
{
    [Test]
    [Arguments("""{"obsolete-private-name":"synthetic-unknown-secret"}""")]
    [Arguments("""{"obsolete-private-name":"synthetic-malformed-secret""")]
    public async Task PublicOnlyDefinitionCannotConsumeUnknownOrMalformedInputWithoutConsent(string text)
    {
        JsonNode source = JsonNode.Parse(BrowserFixture.Definition.CanonicalBytes.Span)!;
        source["configuration"]!["properties"]!["credential"]!["x-configurator"]!["sensitivity"] = "public";
        SetupDefinition definition = SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString()));
        await using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, definition));
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));

        await component.InvokeAsync(() => component.Instance.LoadAsync(stream));

        await Assert.That(stream.Position).IsEqualTo(0);
        await Assert.That(component.Find("[role=status]").GetAttribute("data-code")).IsEqualTo("import-secret-consent-required");
        await Assert.That(component.Markup).DoesNotContain("obsolete-private-name");
        await Assert.That(component.Markup).DoesNotContain("synthetic-");
        var form = component.FindComponents<ConfigurationField>()[0].Instance.Form;
        await Assert.That(form.ImportComparison).IsNull();
        await Assert.That(form.Snapshot().Values.Count).IsEqualTo(0);
    }

    [Test]
    public async Task DefaultComparisonPreservesAnswersRevisionAndPreparedOutput()
    {
        await using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, BrowserFixture.Definition));
        await component.Find("#secret-consent").ChangeAsync(new ChangeEventArgs { Value = true });
        await BrowserFixture.NotifyChoiceAsync(component, "mode", "single");
        await component.Find("[data-field='hostname'] input").InputAsync(new ChangeEventArgs { Value = "workspace.test" });
        await component.Find("#prepare-draft").ClickAsync(new MouseEventArgs());
        var form = component.FindComponents<ConfigurationField>()[0].Instance.Form;
        long revision = form.InputRevision;
        using var stream = new MemoryStream("""{"mode":"single","hostname":"file.test","credential":"synthetic-compared-secret"}"""u8.ToArray());

        await component.InvokeAsync(() => component.Instance.LoadAsync(stream));

        await Assert.That(form.ImportComparison).IsNotNull();
        await Assert.That(form.InputRevision).IsEqualTo(revision);
        await Assert.That(form.Snapshot().Values["hostname"].GetString()).IsEqualTo("workspace.test");
        await Assert.That(form.Snapshot().Values.ContainsKey("credential")).IsFalse();
        await Assert.That(component.FindAll(".configuration-editor__artifact").Count).IsEqualTo(1);
        await Assert.That(component.Markup).DoesNotContain("synthetic-compared-secret");
        await Assert.That(component.Markup).DoesNotContain("file.test");
        await Assert.That(context.JSInterop.Invocations.Any(call =>
            call.Arguments.Any(argument => argument is byte[]))).IsFalse();
    }

    [Test]
    public async Task DefaultComparisonDoesNotCommitInvalidDraftOrRefreshItsControl()
    {
        await using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, BrowserFixture.Definition));
        await component.Find("#secret-consent").ChangeAsync(new ChangeEventArgs { Value = true });
        await component.Find("[data-field='hostname'] input").InputAsync(new ChangeEventArgs { Value = "invalid draft" });
        var form = component.FindComponents<ConfigurationField>()[0].Instance.Form;
        long revision = form.InputRevision;
        string[] diagnostics = form.InputDiagnostics.Select(item => item.Code).ToArray();
        using var stream = new MemoryStream("""{"mode":"single","hostname":"file.test"}"""u8.ToArray());

        await component.InvokeAsync(() => component.Instance.LoadAsync(stream));

        await Assert.That(form.ImportComparison).IsNotNull();
        await Assert.That(form.InputRevision).IsEqualTo(revision);
        await Assert.That(component.Find("[data-field='hostname'] input").GetAttribute("value")).IsEqualTo("invalid draft");
        await Assert.That(form.InputDiagnostics.Select(item => item.Code).SequenceEqual(diagnostics)).IsTrue();
    }

    [Test]
    [Arguments("compare")]
    [Arguments("fill")]
    public async Task FillingRequiresAnExplicitPurposeOrAdoptionAndUsesOneMutation(string purpose)
    {
        await using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, BrowserFixture.Definition));
        await Assert.That(component.Find("#import-purpose").GetAttribute("value")).IsEqualTo("compare");
        await component.Find("#secret-consent").ChangeAsync(new ChangeEventArgs { Value = true });
        if (purpose == "fill")
            await component.Find("#import-purpose").ChangeAsync(new ChangeEventArgs { Value = purpose });
        var form = component.FindComponents<ConfigurationField>()[0].Instance.Form;
        long revision = form.InputRevision;
        using var stream = new MemoryStream("""{"mode":"single","hostname":"adopted.test","credential":"synthetic-adopted-secret"}"""u8.ToArray());
        await component.InvokeAsync(() => component.Instance.LoadAsync(stream));
        if (purpose == "compare")
        {
            await Assert.That(form.InputRevision).IsEqualTo(revision);
            await Assert.That(form.Snapshot().Values.Count).IsEqualTo(0);
            await component.Find("[data-action='adopt-import-comparison']").ClickAsync(new MouseEventArgs());
        }
        await Assert.That(form.InputRevision).IsEqualTo(revision + 1);
        await Assert.That(form.Snapshot().Values["hostname"].GetString()).IsEqualTo("adopted.test");
        await Assert.That(form.Projection.Entries.Single(entry => entry.FieldId == "hostname").Origin)
            .IsEqualTo(Configurator.Presentation.DefinitionAnswerOrigin.Imported);
        await Assert.That(form.ImportComparison).IsNull();
        await Assert.That(component.Find("[role=status]").GetAttribute("data-code")).IsEqualTo("imported");
    }

    [Test]
    public async Task ComparisonPublicationAndRetirementUseOnlyDedicatedNotifications()
    {
        await using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, BrowserFixture.Definition));
        await component.Find("#secret-consent").ChangeAsync(new ChangeEventArgs { Value = true });
        var form = component.FindComponents<ConfigurationField>()[0].Instance.Form;
        int inputs = 0, views = 0, comparisons = 0;
        form.InputChanged += (_, _) => inputs++;
        form.ViewChanged += (_, _) => views++;
        form.ImportComparisonChanged += (_, _) => comparisons++;
        using var stream = new MemoryStream("""{"mode":"single","hostname":"compared.test"}"""u8.ToArray());
        await component.InvokeAsync(() => component.Instance.LoadAsync(stream));
        await Assert.That(component.FindAll("[data-import-comparison]").Count).IsEqualTo(1);
        await component.Find("[data-action='clear-import-comparison']").ClickAsync(new MouseEventArgs());
        await Assert.That(component.FindAll("[data-import-comparison]")).IsEmpty();
        await Assert.That(inputs).IsEqualTo(0);
        await Assert.That(views).IsEqualTo(0);
        await Assert.That(comparisons).IsEqualTo(2);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task UnknownContentOrStructuralMismatchRefusesWholeFileWithoutLeakingPrivateNames(bool structural)
    {
        JsonNode source = JsonNode.Parse(BrowserFixture.Definition.CanonicalBytes.Span)!;
        source["artifacts"]![0]!["projection"]!["members"]!["version"] = JsonNode.Parse("""{"op":"literal","value":2}""");
        BrowserFixture.SetCapabilities(source, source["requiredCapabilities"]!.AsArray()
            .Select(item => item!.GetValue<string>()).Append("projection.literal").ToArray());
        SetupDefinition definition = SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString()));
        await using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, definition));
        await component.Find("#secret-consent").ChangeAsync(new ChangeEventArgs { Value = true });
        var form = component.FindComponents<ConfigurationField>()[0].Instance.Form;
        string text = structural
            ? """{"version":1,"mode":"single","hostname":"compared.test","credential":"synthetic-private-value"}"""
            : """{"version":2,"mode":"single","hostname":"compared.test","credential":"synthetic-private-value","obsolete-private-name":{"token":"synthetic-unknown-value"}}""";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));
        await component.InvokeAsync(() => component.Instance.LoadAsync(stream));
        await Assert.That(form.ImportComparison!.Compatibility).IsEqualTo(DefinitionImportCompatibility.ChangesRequired);
        await Assert.That(form.ImportComparison.AutofillEligible).IsFalse();
        await Assert.That(component.Find("[data-action='adopt-import-comparison']").HasAttribute("disabled")).IsTrue();
        await Assert.That(component.FindAll("[data-import-severity='Blocking']").Count).IsGreaterThan(0);
        await Assert.That(form.Snapshot().Values.Count).IsEqualTo(0);
        await Assert.That(component.Find("[data-import-comparison]").QuerySelectorAll("[aria-invalid='true']").Length).IsEqualTo(0);
        await Assert.That(component.Markup).DoesNotContain("obsolete-private-name");
        await Assert.That(component.Markup).DoesNotContain("synthetic-private-value");
        await Assert.That(component.Markup).DoesNotContain("synthetic-unknown-value");
        await Assert.That(context.JSInterop.Invocations.Any(call => call.Arguments.Any(argument => argument is byte[]))).IsFalse();
    }

    [Test]
    public async Task RepeatedAndMapFindingsExplainOnlySchemaWhileRetainingDirtyWorkspaceIdentity()
    {
        SetupDefinition definition = Definition("""
            {"type":"object","additionalProperties":false,"x-configurator":{"sensitivity":"public"},"properties":{
              "rows":{"type":"array","title":"Rows","x-configurator":{"sensitivity":"public","detail":"basic"},
                "items":{"type":"object","additionalProperties":false,"x-configurator":{"sensitivity":"public"},"properties":{
                  "name":{"type":"string","title":"Name","x-configurator":{"sensitivity":"public"}},
                  "port":{"type":"integer","title":"Port","x-configurator":{"sensitivity":"public"}}}}},
              "settings":{"type":"object","title":"Settings","x-configurator":{"sensitivity":"public","detail":"basic"},
                "additionalProperties":{"type":"string","x-configurator":{"sensitivity":"public"}}}}}
            """, """
            {"op":"object","members":{"rows":{"op":"copy","reference":{"scope":"root","path":"/rows"}},
             "settings":{"op":"copy","reference":{"scope":"root","path":"/settings"}}}}
            """, "schema.object", "schema.array", "schema.string", "schema.integer", "schema.map", "projection.object", "projection.copy");
        await using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, definition));
        await component.Find("#secret-consent").ChangeAsync(new ChangeEventArgs { Value = true });
        var form = component.FindComponent<ConfigurationImportReview>().Instance.Form;
        using var rows = JsonDocument.Parse("""[{"name":"workspace-B","port":2},{"name":"workspace-A","port":1}]""");
        using var map = JsonDocument.Parse("""{"current.key":"workspace"}""");
        await component.InvokeAsync(() =>
        {
            form.SetValue(["rows"], rows.RootElement);
            form.SetValue(["settings"], map.RootElement);
            form.EditValue(["rows", "0", "port"], "invalid draft");
            form.SetDiscovery("retained-query");
        });
        long revision = form.InputRevision;
        string rowIdentity = form.GetEditorIdentity(["rows", "0", "name"]);
        string mapIdentity = form.GetEditorIdentity(["settings", "current.key"]);
        string before = form.Snapshot().Values["rows"].GetRawText();
        using var input = new MemoryStream("""{"rows":[{"name":42,"port":1}],"settings":{"imported-private-name":false}}"""u8.ToArray());
        await component.InvokeAsync(() => component.Instance.LoadAsync(input));
        int javascriptCalls = context.JSInterop.Invocations.Count;
        await component.Find("[data-import-schema='rows']").ClickAsync(new MouseEventArgs());
        await component.Find("[data-import-schema='settings']").ClickAsync(new MouseEventArgs());
        await Assert.That(component.Find("#import-schema-explanation").TextContent).DoesNotContain("imported-private-name");
        await Assert.That(form.GetEditorIdentity(["rows", "0", "name"])).IsEqualTo(rowIdentity);
        await Assert.That(form.GetEditorIdentity(["settings", "current.key"])).IsEqualTo(mapIdentity);
        await Assert.That(form.InputRevision).IsEqualTo(revision);
        await Assert.That(form.Snapshot().Values["rows"].GetRawText()).IsEqualTo(before);
        await Assert.That(form.HasInputErrors).IsTrue();
        await Assert.That(form.DiscoveryQuery).IsEqualTo("retained-query");
        await Assert.That(context.JSInterop.Invocations.Count).IsEqualTo(javascriptCalls);
        await Assert.That(component.Find("[data-import-comparison]").TextContent).DoesNotContain("imported-private-name");
        await Assert.That(component.Find("[data-import-comparison]").TextContent).DoesNotContain("workspace-B");
    }

    [Test]
    public async Task ThreeThousandFindingsRemainReachableThroughBoundedRenderingPages()
    {
        SetupDefinition definition = Definition("""
            {"type":"object","additionalProperties":false,"x-configurator":{"sensitivity":"public"},"properties":{
              "items":{"type":"array","title":"Items","x-configurator":{"sensitivity":"public","detail":"basic"},
                "items":{"type":"string","maxLength":1,"x-configurator":{"sensitivity":"public"}}}}}
            """, """{"op":"object","members":{"items":{"op":"copy","reference":{"scope":"root","path":"/items"}}}}""",
            "schema.object", "schema.array", "schema.string", "schema.bounds", "projection.object", "projection.copy");
        await using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, definition));
        await component.Find("#secret-consent").ChangeAsync(new ChangeEventArgs { Value = true });
        string input = new JsonObject
        {
            ["items"] = new JsonArray(Enumerable.Range(0, 3000)
            .Select(_ => (JsonNode?)JsonValue.Create("xx")).ToArray())
        }.ToJsonString();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(input));
        await component.InvokeAsync(() => component.Instance.LoadAsync(stream));
        var report = component.FindComponent<ConfigurationImportReview>().Instance.Form.ImportComparison!;
        await Assert.That(report.InventoryComplete).IsTrue();
        await Assert.That(report.TotalFindingCount).IsEqualTo(3000);
        int rendered = 0;
        for (int page = 1; page <= 60; page++)
        {
            await Assert.That(component.Find("[data-import-page]").GetAttribute("data-import-page")).IsEqualTo(page.ToString(System.Globalization.CultureInfo.InvariantCulture));
            await Assert.That(component.FindAll("[data-import-finding]").Count).IsEqualTo(50);
            rendered += component.FindAll("[data-import-finding]").Count;
            if (page < 60)
                await component.Find("[data-action='import-next']").ClickAsync(new MouseEventArgs());
        }
        await Assert.That(rendered).IsEqualTo(3000);
        await Assert.That(component.Find("[data-action='import-next']").HasAttribute("disabled")).IsTrue();
        await Assert.That(ReferenceEquals(component.FindComponent<ConfigurationImportReview>().Instance.Form.ImportComparison, report)).IsTrue();
    }

    [Test]
    public async Task ActualFileHandlerChecksConsentBeforeOpeningAndRetiresDisposalFailure()
    {
        await using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, BrowserFixture.Definition));
        using var stream = new CapturingStream("""{"mode":"single","hostname":"file.test","credential":"synthetic-disposal-secret"}"""u8.ToArray())
        {
            FailDisposal = true
        };
        var file = new LocalFile(stream);
        await component.InvokeAsync(() => component.FindComponent<InputFile>().Instance.OnChange
            .InvokeAsync(new InputFileChangeEventArgs([file])));
        await Assert.That(file.Opened).IsFalse();
        await component.Find("#secret-consent").ChangeAsync(new ChangeEventArgs { Value = true });
        await component.InvokeAsync(() => component.FindComponent<InputFile>().Instance.OnChange
            .InvokeAsync(new InputFileChangeEventArgs([file])));
        var form = component.FindComponent<ConfigurationImportReview>().Instance.Form;
        await Assert.That(file.Opened).IsTrue();
        await Assert.That(stream.Disposed).IsTrue();
        await Assert.That(stream.Captured.ToArray().All(value => value == 0)).IsTrue();
        await Assert.That(form.ImportComparison).IsNull();
        await Assert.That(form.Snapshot().Values.Count).IsEqualTo(0);
        await Assert.That(component.Find("[role=status]").GetAttribute("data-code")).IsEqualTo("import-read-failed");
        await Assert.That(component.Markup).DoesNotContain("synthetic-disposal-secret");
    }

    [Test]
    public async Task AcquisitionStopsAtOneMiBPlusSentinelAndClearsItsOwnedBuffer()
    {
        await using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, BrowserFixture.Definition));
        await component.Find("#secret-consent").ChangeAsync(new ChangeEventArgs { Value = true });
        using var stream = new CapturingStream(Enumerable.Repeat((byte)'x', 1024 * 1024 + 2).ToArray());
        await component.InvokeAsync(() => component.Instance.LoadAsync(stream));
        await Assert.That(stream.Position).IsEqualTo(1024 * 1024 + 1);
        await Assert.That(stream.Captured.ToArray().All(value => value == 0)).IsTrue();
        await Assert.That(component.Find("[role=status]").GetAttribute("data-code")).IsEqualTo("import-too-large");
        await Assert.That(component.FindComponent<ConfigurationImportReview>().Instance.Form.ImportComparison).IsNull();
    }

    private static SetupDefinition Definition(string configuration, string projection, params string[] capabilities)
    {
        JsonObject source = JsonNode.Parse(BrowserFixture.Definition.CanonicalBytes.Span)!.AsObject();
        source["configuration"] = JsonNode.Parse(configuration);
        source["artifacts"]![0]!["projection"] = JsonNode.Parse(projection);
        source.Remove("presentation");
        BrowserFixture.SetCapabilities(source, capabilities);
        return SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString()));
    }

    private sealed class CapturingStream(byte[] bytes) : MemoryStream(bytes)
    {
        internal Memory<byte> Captured { get; private set; }
        internal bool FailDisposal { get; init; }
        internal bool Disposed { get; private set; }
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (Captured.IsEmpty) Captured = buffer;
            return base.ReadAsync(buffer, cancellationToken);
        }
        public override async ValueTask DisposeAsync()
        {
            Disposed = true;
            await base.DisposeAsync();
            if (FailDisposal) throw new IOException("synthetic-disposal-secret");
        }
    }

    private sealed class LocalFile(CapturingStream stream) : IBrowserFile
    {
        internal bool Opened { get; private set; }
        public string Name => "synthetic-private-file-name.json";
        public DateTimeOffset LastModified => DateTimeOffset.UnixEpoch;
        public long Size => stream.Length;
        public string ContentType => "application/json";
        public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default)
        {
            Opened = true;
            return stream;
        }
    }
}
