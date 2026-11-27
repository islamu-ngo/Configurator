namespace Configurator.Browser.Tests;

using System.Text;
using System.Text.Json.Nodes;
using Bunit;
using Configurator.Browser.Layout;
using Configurator.Core.Definitions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;

public sealed class BrowserSecretBoundaryTests
{
    [Test]
    public async Task RevokePreservesOrdinarySiblingAndRejectsRetiredCallback()
    {
        JsonNode source = JsonNode.Parse(BrowserFixture.Definition.CanonicalBytes.Span)!;
        BrowserFixture.WithField(source, "profile", """
            {"type":"object","title":"Profile","additionalProperties":false,"x-configurator":{"sensitivity":"public","detail":"standard"},"properties":{
              "name":{"type":"string","x-configurator":{"sensitivity":"public","detail":"standard"}},
              "token":{"type":"string","x-configurator":{"sensitivity":"secret","detail":"standard"}}}}
            """);
        Definition definition = DefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString()));
        await using var context = BrowserFixture.Context();
        context.ComponentFactories.Add<ConfigurationField, BrowserCredentialEntryTests.EmittedInputField>();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, definition));
        await component.Find("[data-field='profile.name'] input").InputAsync(new ChangeEventArgs { Value = "ordinary-sibling" });
        await BrowserFixture.SetSecretConsentAsync(component, true);
        await component.Find("[data-field='profile.token'] input").InputAsync(new ChangeEventArgs { Value = "synthetic-retired-secret" });
        var token = component.FindComponents<BrowserCredentialEntryTests.EmittedInputField>()
            .Single(item => item.Instance.Path.SequenceEqual(["profile", "token"]));
        object callback = token.Instance.CaptureInput();
        var form = token.Instance.Form;
        await BrowserFixture.SetSecretConsentAsync(component, false);
        await Assert.That(form.Snapshot().Values["profile"].GetRawText()).IsEqualTo("{\"name\":\"ordinary-sibling\"}");
        await BrowserFixture.SetSecretConsentAsync(component, true);
        long revision = form.InputRevision;
        await component.InvokeAsync(() => callback switch
        {
            EventCallback<ChangeEventArgs> typed => typed.InvokeAsync(new ChangeEventArgs { Value = "synthetic-late-secret" }),
            EventCallback untyped => untyped.InvokeAsync(new ChangeEventArgs { Value = "synthetic-late-secret" }),
            MulticastDelegate handler => new EventCallbackWorkItem(handler).InvokeAsync(new ChangeEventArgs { Value = "synthetic-late-secret" }),
            _ => throw new InvalidOperationException("input-handler-missing")
        });
        await Assert.That(form.InputRevision).IsEqualTo(revision);
        await Assert.That(form.Snapshot().Values["profile"].GetRawText()).IsEqualTo("{\"name\":\"ordinary-sibling\"}");
        await Assert.That(component.Find("[data-field='profile.token'] input").GetAttribute("value")).IsEqualTo("");
    }

    [Test]
    public async Task NestedSecretDenialKeepsOrdinarySiblingAndStartsReplacementWithoutConsent()
    {
        JsonNode source = JsonNode.Parse(BrowserFixture.Definition.CanonicalBytes.Span)!;
        BrowserFixture.WithField(source, "profile", """
            {"type":"object","title":"Profile","additionalProperties":false,"x-configurator":{"sensitivity":"public","detail":"standard"},"properties":{
              "name":{"type":"string","title":"Name","x-configurator":{"sensitivity":"public","detail":"standard"}},
              "token":{"type":"string","title":"Token","x-configurator":{"sensitivity":"secret","detail":"standard"}}}}
            """);
        Definition definition = DefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString()));
        await using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, definition));
        component.Find("[data-field='profile.name'] input").Input("ordinary-sibling");
        await Assert.That(component.FindAll("[data-field='profile.token'] input")).IsEmpty();
        await BrowserFixture.SetSecretConsentAsync(component, true);
        component.Find("[data-field='profile.token'] input").Input("nested-revoked-canary");
        await BrowserFixture.SetSecretConsentAsync(component, false);
        var form = component.FindComponents<ConfigurationField>()[0].Instance.Form;
        await Assert.That(form.Snapshot().Values["profile"].GetRawText()).IsEqualTo("{\"name\":\"ordinary-sibling\"}");
        await Assert.That(component.FindAll("[data-field='profile.token'] input")).IsEmpty();
        await Assert.That(component.Markup).DoesNotContain("nested-revoked-canary");
        component.Render(parameters => parameters.Add(item => item.Definition, BrowserFixture.Revision(2)));
        await Assert.That(component.Find("[data-workspace-consent-chip]").GetAttribute("data-consent-active")).IsEqualTo("false");
        await Assert.That(component.FindAll("[data-field='credential'] input")).IsEmpty();
    }

    [Test]
    public async Task SecretEntryDefaultsOffAndConsentRevocationRetainsOrdinarySettings()
    {
        await using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, BrowserFixture.Definition));
        await Assert.That(component.FindAll("[data-field='credential'] input")).IsEmpty();
        component.Find("[data-field='hostname'] input").Input("example.test");
        await BrowserFixture.SetSecretConsentAsync(component, true);
        component.Find("[data-field='credential'] input").Input("synthetic-private-canary");
        await Assert.That(component.Find("[data-field='credential'] input").GetAttribute("type")).IsEqualTo("password");
        await BrowserFixture.SetSecretConsentAsync(component, false);
        await Assert.That(component.FindAll("[data-field='credential'] input")).IsEmpty();
        await Assert.That(component.Find("[data-field='hostname'] input").GetAttribute("value")).IsEqualTo("example.test");
        await Assert.That(component.Markup).DoesNotContain("synthetic-private-canary");
        await Assert.That(context.JSInterop.Invocations.Any(call => call.Arguments.Any(arg => arg?.ToString()?.Contains("synthetic-private-canary", StringComparison.Ordinal) == true))).IsFalse();
    }

    [Test]
    public async Task SecretBearingImportRejectsBeforeConsentWithoutEchoingValues()
    {
        await using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, BrowserFixture.Definition));
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("""{"mode":"single","hostname":"example.test","credential":"synthetic-private-canary"}"""));
        await component.InvokeAsync(() => component.Instance.LoadAsync(stream));
        await Assert.That(stream.Position).IsEqualTo(0);
        await Assert.That(component.Find("[role=status]").GetAttribute("data-code")).IsEqualTo("import-secret-consent-required");
        await Assert.That(component.Markup).DoesNotContain("synthetic-private-canary");
        await Assert.That(component.Find("[data-field='hostname'] input").GetAttribute("value")).IsEqualTo("");
    }

    [Test]
    [Arguments("en", "ltr")]
    [Arguments("ar", "rtl")]
    public async Task SecretConsentAndHelpSurfaceHostedCodeCustodyRiskAndAlternativesInEnglishAndArabic(string locale, string direction)
    {
        Definition bilingual = DefinitionSourceCodec.ParseJson("""
            {"$schema":"urn:islamu:configurator:authoring:v0.0.1",
             "identity":{"familyId":"88c4268a-1f9f-4e64-a3ea-4c4b3fb2d0a1","revision":1},
             "project":{"id":"bilingual-custody","name":"Bilingual Custody"},"applicationVersions":["1"],
             "configuration":{"type":"object","additionalProperties":false,"required":["token"],
               "x-configurator":{"sensitivity":"public"},"properties":{
                 "token":{"type":"string","title":"API Token","x-configurator":{"sensitivity":"secret"}}}},
             "localization":{"defaultLocale":"en","translations":{"ar":{"direction":"rtl","overrides":{}}}},
             "outputs":{"config.json":{"id":"config","format":"json"}}}
            """u8.ToArray());
        await using var context = BrowserFixture.Context(new BrowserDefinitionRegistry([bilingual]));
        var editor = context.Render<ConfigurationEditor>(parameters => parameters
            .Add(item => item.Definition, bilingual)
            .Add(item => item.InitialLocale, locale));

        await BrowserFixture.OpenWorkspaceUtilityAsync(editor, "trust-privacy");
        await Assert.That(editor.Find("#secret-consent").HasAttribute("checked")).IsFalse();
        var consentDisclosure = editor.Find("[data-secret-consent] [data-hosted-code-custody]");
        await Assert.That(string.IsNullOrWhiteSpace(consentDisclosure.TextContent)).IsFalse();
        var helpLink = editor.Find("[data-secret-consent] a[href='help#custody']");
        await Assert.That(string.IsNullOrWhiteSpace(helpLink.TextContent)).IsFalse();

        var help = context.Render<Pages.Help>(parameters => parameters.AddCascadingValue((locale, direction)));
        await Assert.That(help.FindAll("section#custody")).Count().IsEqualTo(1);
        var helpDisclosure = help.Find("section#custody [data-hosted-code-custody]");
        await Assert.That(string.IsNullOrWhiteSpace(helpDisclosure.TextContent)).IsFalse();

        if (locale == "ar")
        {
            await Assert.That(consentDisclosure.TextContent.Any(ch => ch is >= '\u0600' and <= '\u06ff')).IsTrue();
            await Assert.That(helpDisclosure.TextContent.Any(ch => ch is >= '\u0600' and <= '\u06ff')).IsTrue();
        }
        else
        {
            await Assert.That(consentDisclosure.TextContent).Contains("extensions");
            await Assert.That(helpDisclosure.TextContent).Contains("SecretKit");
        }
    }

    [Test]
    [Arguments("""{"unknown":"private-canary"}""")]
    [Arguments("""{"mode":"private-canary"}""")]
    [Arguments("""{"mode":{"secret":"private-canary"}}""")]
    [Arguments("""{"mode":"single","mode":"cluster"}""")]
    [Arguments("""{"mode":"single","replicas":true}""")]
    [Arguments("""{"mode":"cluster","replicas":"private-canary"}""")]
    [Arguments("""{"mode":"cluster","replicas":null}""")]
    public async Task UnknownDuplicateInactiveAndWrongTypedImportedValuesNeverPublishAnswersOrOutput(string text)
    {
        await using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, BrowserFixture.Definition));
        await BrowserFixture.SetSecretConsentAsync(component, true);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));
        await component.InvokeAsync(() => component.Instance.LoadAsync(stream));
        await Assert.That(component.Find("[role=status]").GetAttribute("data-code")).IsNotEqualTo("imported");
        await Assert.That(component.FindAll(".configuration-editor__artifact")).IsEmpty();
        await Assert.That(component.Markup).DoesNotContain("private-canary");
        var mode = component.FindComponents<ConfigurationField>().Single(item => item.Instance.Path.SequenceEqual(["mode"]));
        await Assert.That(mode.FindComponent<MudBlazor.MudSelect<string>>().Instance.Value).IsEqualTo("");
    }

    [Test]
    public async Task RevocationRetiresRetainedComparisonAndReconsentCannotRestoreOrAdoptIt()
    {
        await using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, BrowserFixture.Definition));
        await BrowserFixture.SetSecretConsentAsync(component, true);
        await BrowserFixture.OpenWorkspaceUtilityAsync(component, "import");
        using var stream = new MemoryStream("""{"mode":"single","hostname":"compared.test","credential":"synthetic-retained-secret"}"""u8.ToArray());
        await component.Find("#import-purpose").ChangeAsync(new ChangeEventArgs { Value = "compare" });
        await component.InvokeAsync(() => component.Instance.LoadAsync(stream));
        var review = component.FindComponent<ConfigurationImportReview>().Instance;
        var form = review.Form;
        var report = form.ImportComparison!;
        var adoption = review.FillFields;
        await Assert.That(report).IsNotNull();
        await BrowserFixture.SetSecretConsentAsync(component, false);
        await Assert.That(form.ImportComparison).IsNull();
        await Assert.That(component.FindAll("[data-import-comparison]")).IsEmpty();
        await BrowserFixture.SetSecretConsentAsync(component, true);
        long revision = form.InputRevision;
        await component.InvokeAsync(() => adoption.InvokeAsync(report));
        await Assert.That(form.InputRevision).IsEqualTo(revision);
        await Assert.That(form.Snapshot().Values.Count).IsEqualTo(0);
        await Assert.That(form.ImportComparison).IsNull();
        await Assert.That(component.Markup).DoesNotContain("synthetic-retained-secret");
        await Assert.That(context.JSInterop.Invocations.Any(call => call.Arguments.Any(argument => argument is byte[]))).IsFalse();
    }

    [Test]
    public async Task SecretContainerMasksEveryDescendantScalarAfterConsent()
    {
        JsonNode definition = JsonNode.Parse(BrowserFixture.Definition.CanonicalBytes.Span)!;
        BrowserFixture.WithField(definition, "private", """
            {"type":"object","title":"Private settings","additionalProperties":false,"x-configurator":{"sensitivity":"secret","detail":"standard"},"properties":{
              "enabled":{"type":"boolean","title":"Enabled","x-configurator":{"sensitivity":"public","detail":"standard"}},
              "count":{"type":"integer","title":"Count","x-configurator":{"sensitivity":"public","detail":"standard"}},
              "mode":{"type":"string","title":"Mode","enum":["single","cluster"],"x-configurator":{"sensitivity":"public","detail":"standard"}}}}
            """, "schema.integer");
        Definition admitted = DefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(definition.ToJsonString()));
        await using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, admitted));
        await Assert.That(component.FindAll("[data-field='private'] input")).IsEmpty();
        await BrowserFixture.SetSecretConsentAsync(component, true);
        await Assert.That(component.FindAll("[data-field='private'] input")).Count().IsEqualTo(3);
        await Assert.That(component.Find("[data-field='private.enabled'] input").GetAttribute("type")).IsEqualTo("password");
        await Assert.That(component.Find("[data-field='private.count'] input").GetAttribute("type")).IsEqualTo("password");
        var choice = component.FindComponents<ConfigurationField>().Single(field => field.Instance.Path.SequenceEqual(["private", "mode"]));
        var form = choice.Instance.Form;
        await component.InvokeAsync(() => choice.FindComponent<MudSelect<string>>().Instance.ValueChanged.InvokeAsync("1"));
        await Assert.That(form.Snapshot().Values["private"].GetProperty("mode").GetString()).IsEqualTo("cluster");
        await Assert.That(component.FindComponents<ConfigurationField>().Single(field => field.Instance.Path.SequenceEqual(["private", "mode"]))
            .FindComponent<MudSelect<string>>().Instance.Value).IsEqualTo("");
        await Assert.That(component.FindAll("[data-field='private.mode'] .configuration-field__choice").Count).IsEqualTo(1);
    }

    [Test]
    public async Task UnmountedScrolledSectionRetainsUnparsedDraftsAndWipesRevokedSecretsAgainstStaleCallbacks()
    {
        var properties = new JsonObject
        {
            ["unparsedPort"] = JsonNode.Parse("""{"type":"integer","title":"Unparsed Port","x-configurator":{"sensitivity":"public","detail":"basic"}}""")
        };
        var sections = new JsonArray();
        var sectionIds = new JsonArray();
        for (int s = 0; s < 12; s++)
        {
            string secId = $"sec{s}";
            sectionIds.Add(secId);
            var pointers = new JsonArray();
            if (s == 0) pointers.Add("/unparsedPort");
            for (int f = 0; f < 6; f++)
            {
                string name = s == 11 && f == 0 ? "deepSecret" : $"sec{s}_f{f}";
                bool secret = s == 11 && f == 0;
                properties[name] = new JsonObject
                {
                    ["type"] = "string",
                    ["title"] = secret ? "Deep Secret" : $"Setting {s}-{f}",
                    ["x-configurator"] = secret
                        ? new JsonObject
                        {
                            ["sensitivity"] = "secret",
                            ["detail"] = "basic",
                            ["secret"] = new JsonObject { ["kind"] = "password", ["acquisition"] = "local" }
                        }
                        : new JsonObject { ["sensitivity"] = "public", ["detail"] = "basic" }
                };
                pointers.Add("/" + name);
            }
            sections.Add(new JsonObject { ["id"] = secId, ["title"] = $"Section {s}", ["detail"] = "basic", ["pointers"] = pointers });
        }
        var doc = new JsonObject
        {
            ["schemaVersion"] = "0.0.1",
            ["profile"] = "bounded-v0.0.1",
            ["familyId"] = "f26b4e93-6e21-4c9a-a2a0-4810a1a0f202",
            ["revision"] = 1,
            ["project"] = new JsonObject { ["id"] = "custody-unmount-test", ["publisher"] = "example", ["name"] = "Custody Unmount Test" },
            ["applicationVersions"] = new JsonArray("1.0.0"),
            ["entryPoints"] = new JsonArray("setup"),
            ["locales"] = new JsonArray("en"),
            ["configuration"] = new JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = false,
                ["x-configurator"] = new JsonObject { ["sensitivity"] = "public" },
                ["required"] = new JsonArray("deepSecret"),
                ["properties"] = properties
            },
            ["presentation"] = new JsonObject
            {
                ["defaultJourney"] = "default",
                ["navigation"] = "sections",
                ["sections"] = sections,
                ["journeys"] = new JsonArray(new JsonObject { ["id"] = "default", ["title"] = "Default", ["sections"] = sectionIds })
            },
            ["artifacts"] = JsonNode.Parse("""[{"id":"out","format":"json","fileName":"out.json","targetUpdateMode":"create","restartRequirement":"none","projection":{"op":"literal","value":{}}}]"""),
            ["requiredCapabilities"] = new JsonArray("projection.literal", "schema.integer", "schema.object", "schema.secret", "schema.string")
        };
        Definition definition = DefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(doc.ToJsonString()));
        await using var context = BrowserFixture.Context();
        context.ComponentFactories.Add<ConfigurationSettingRow, EmittedSettingRow>();
        var component = BrowserFixture.RenderWorkspace(context, definition);
        var panel = component.FindComponent<ConfigurationConfigurePanel>();
        var form = component.FindComponents<ConfigurationField>()[0].Instance.Form;

        await component.Find("[data-layout-mode='all-sections']").ClickAsync(new MouseEventArgs());

        // Enter an unparsed invalid numeric draft in section 0, then scroll window to section 11.
        await component.Find("[data-field='unparsedPort'] input").InputAsync(new ChangeEventArgs { Value = "99999-draft" });
        await panel.InvokeAsync(() => panel.Instance.OnScrollspySectionChangedAsync("sec11"));
        await Assert.That(component.FindAll("[data-section-panel='sec0'] input")).IsEmpty();

        // Enable consent and enter a secret in section 11 while sec0 is unmounted, capturing its @oninput callback.
        await BrowserFixture.SetSecretConsentAsync(component, true);
        await component.Find("[data-field='deepSecret'] input").InputAsync(new ChangeEventArgs { Value = "synthetic-unmounted-secret-s04" });
        object staleDeepCallback = component.FindComponents<EmittedSettingRow>()
            .Single(row => row.Instance.Path.SequenceEqual(["deepSecret"]))
            .Instance.CaptureInput();

        // Scroll back to sec0 so sec11 unmounts, verifying sec0 still holds the exact unparsed draft.
        await panel.InvokeAsync(() => panel.Instance.OnScrollspySectionChangedAsync("sec0"));
        await Assert.That(component.FindAll("[data-section-panel='sec11'] input")).IsEmpty();
        await Assert.That(component.Find("[data-field='unparsedPort'] input").GetAttribute("value")).IsEqualTo("99999-draft");

        // Revoke consent while sec11 is unmounted, fire stale input callback against retired control, then verify zero resurrection.
        await BrowserFixture.SetSecretConsentAsync(component, false);
        long revisionAfterRevoke = form.InputRevision;
        await component.InvokeAsync(() => staleDeepCallback switch
        {
            EventCallback<ChangeEventArgs> typed => typed.InvokeAsync(new ChangeEventArgs { Value = "synthetic-late-stale-secret" }),
            EventCallback untyped => untyped.InvokeAsync(new ChangeEventArgs { Value = "synthetic-late-stale-secret" }),
            MulticastDelegate handler => new EventCallbackWorkItem(handler).InvokeAsync(new ChangeEventArgs { Value = "synthetic-late-stale-secret" }),
            _ => throw new InvalidOperationException("input-handler-missing")
        });
        await Assert.That(form.InputRevision).IsEqualTo(revisionAfterRevoke);
        await Assert.That(component.Markup).DoesNotContain("synthetic-unmounted-secret-s04");
        await Assert.That(component.Markup).DoesNotContain("synthetic-late-stale-secret");

        await panel.InvokeAsync(() => panel.Instance.OnScrollspySectionChangedAsync("sec11"));
        await Assert.That(component.FindAll("[data-field='deepSecret'] [data-secret-denied]").Count).IsEqualTo(1);
        await Assert.That(component.FindAll("[data-field='deepSecret'] input")).IsEmpty();

        // Search must never expose secret value or leak matches across revoked state.
        await component.InvokeAsync(() => form.SetDiscovery("synthetic-unmounted-secret-s04"));
        await Assert.That(form.Projection.Matches).IsEmpty();
    }

    [Test]
    public async Task InPlaceSettingsPreservesActiveSecretConsentAndEnteredSecretsWhileReturnRestoresFocusToEditor()
    {
        await using BunitContext context = BrowserFixture.Context();
        var exports = context.JSInterop.SetupModule("./js/browser-artifacts.js");
        exports.SetupVoid("open", _ => true).SetVoidResult();
        exports.Setup<bool>("copy", _ => true).SetResult(true);
        exports.SetupVoid("invalidate", _ => true).SetVoidResult();
        context.Services.GetRequiredService<NavigationManager>()
            .NavigateTo($"launch/{BrowserFixture.Family}?digest={BrowserFixture.Definition.Digest.Value}");
        IRenderedComponent<App> component = context.Render<App>();

        await BrowserFixture.SetSecretConsentAsync(component, true);
        await component.Find("[data-field='credential'] input").InputAsync(new ChangeEventArgs { Value = "synthetic-in-place-secret" });

        await component.Find("header [data-action='open-settings']").ClickAsync(new MouseEventArgs());
        await Assert.That(component.FindAll("[data-action='return-to-configuration']")).Count().IsEqualTo(1);
        await Assert.That(component.Find("[data-settings-workspace]").TextContent).DoesNotContain("synthetic-in-place-secret");
        await Assert.That(exports.Invocations.Any(call => call.Identifier == "invalidate")).IsFalse();

        await component.Find("[data-action='return-to-configuration']").ClickAsync(new MouseEventArgs());
        await Assert.That(component.Find("#secret-consent").HasAttribute("checked")).IsTrue();
        await Assert.That(component.Find("[data-field='credential'] input").GetAttribute("value")).IsEqualTo("synthetic-in-place-secret");
    }

    public sealed class EmittedSettingRow : ConfigurationSettingRow
    {
        public object CaptureInput()
        {
            using var builder = new Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder();
            BuildRenderTree(builder);
            var frames = builder.GetFrames();
            return frames.Array.Take(frames.Count).Single(frame =>
                frame.FrameType == Microsoft.AspNetCore.Components.RenderTree.RenderTreeFrameType.Attribute &&
                frame.AttributeName == "oninput").AttributeValue;
        }
    }
}
