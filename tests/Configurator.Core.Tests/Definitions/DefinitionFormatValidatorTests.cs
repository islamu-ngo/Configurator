using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Configurator.Core.Definitions;
using Configurator.Core.Artifacts;
using Configurator.Core.Definitions.Authoring;

namespace Configurator.Core.Tests.Definitions;

/// <summary>
/// Specifies the asserted-by-default domain formats and the sensitivity floors of non-value field metadata.
/// Expected verdicts come from the cited grammars (RFC 1123, RFC 4291, RFC 3986, RFC 5646, SemVer 2.0.0,
/// RFC 3339 Appendix A, the OCI distribution reference grammar and POSIX cron), not from the implementation.
/// </summary>
public sealed class DefinitionFormatValidatorTests
{
    private const string FamilyId = "8a3c3d62-71d4-4f43-9d0e-6a6f4a1f7b21";

    [Test]
    [Arguments("hostname", "api.example.org")]
    [Arguments("hostname", "localhost")]
    [Arguments("hostname", "a-b.example.")]
    [Arguments("ipv4", "192.168.0.1")]
    [Arguments("ipv4", "0.0.0.0")]
    [Arguments("ipv6", "::1")]
    [Arguments("ipv6", "2001:db8::8a2e:370:7334")]
    [Arguments("ipv6", "::ffff:192.0.2.128")]
    [Arguments("ip-address", "10.0.0.1")]
    [Arguments("ip-address", "fe80::1")]
    [Arguments("cidr", "0.0.0.0/0")]
    [Arguments("cidr", "10.0.0.0/8")]
    [Arguments("cidr", "2001:db8::/32")]
    [Arguments("uri", "urn:isbn:0451450523")]
    [Arguments("uri", "mailto:ops@example.org")]
    [Arguments("uri", "https://example.org/a?b=c#d")]
    [Arguments("http-url", "http://localhost:8080/health")]
    [Arguments("http-url", "https://[::1]:8443/")]
    [Arguments("https-url", "https://example.org")]
    [Arguments("https-url", "https://example.org:443/path?q=1")]
    [Arguments("origin", "https://example.org")]
    [Arguments("origin", "http://localhost:5000")]
    [Arguments("email-address", "first.last@example.org")]
    [Arguments("slug", "event-2026")]
    [Arguments("slug", "a")]
    [Arguments("env-key", "PUBLIC_BASE_URL")]
    [Arguments("env-key", "_private")]
    [Arguments("bcp47-language-tag", "en")]
    [Arguments("bcp47-language-tag", "ar-EG")]
    [Arguments("bcp47-language-tag", "zh-Hant-TW")]
    [Arguments("bcp47-language-tag", "es-419")]
    [Arguments("iso3166-alpha2", "BE")]
    [Arguments("uuid", "69cdb122-41d2-4fab-9b82-7929a889c726")]
    [Arguments("semver", "1.2.3")]
    [Arguments("semver", "1.0.0-alpha.1+build.5")]
    [Arguments("semver", "0.0.1")]
    [Arguments("duration", "P1D")]
    [Arguments("duration", "PT30S")]
    [Arguments("duration", "P1Y2M3DT4H5M6S")]
    [Arguments("duration", "P2W")]
    [Arguments("byte-size", "512MiB")]
    [Arguments("byte-size", "1.5GB")]
    [Arguments("byte-size", "1024")]
    [Arguments("unix-path", "/var/lib/app")]
    [Arguments("unix-path", "/")]
    [Arguments("unix-path", "/srv/data/")]
    [Arguments("relative-path", "config/app.json")]
    [Arguments("relative-path", "./data")]
    [Arguments("oci-image", "nginx")]
    [Arguments("oci-image", "ghcr.io/islamu/event-api:1.2.3")]
    [Arguments("oci-image", "localhost:5000/team/app@sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef")]
    [Arguments("sha256-hex", "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef")]
    [Arguments("cron-5", "*/15 * * * *")]
    [Arguments("cron-5", "0 3 * * MON-FRI")]
    [Arguments("cron-5", "5,10 0-6/2 1 JAN,jul 0")]
    public async Task ValidValuesSatisfyTheirFormat(string format, string value)
    {
        await Assert.That(DefinitionFormatValidator.IsValid(format, value)).IsTrue();
    }

    [Test]
    [Arguments("hostname", "-bad.example")]
    [Arguments("hostname", "bad..example")]
    [Arguments("hostname", "under_score.example")]
    [Arguments("ipv4", "256.0.0.1")]
    [Arguments("ipv4", "01.2.3.4")]
    [Arguments("ipv4", "1.2.3")]
    [Arguments("ipv6", "1::2::3")]
    [Arguments("ipv6", "12345::")]
    [Arguments("ipv6", "1:2:3:4:5:6:7:8:9")]
    [Arguments("ip-address", "example.org")]
    [Arguments("cidr", "10.0.0.0/33")]
    [Arguments("cidr", "10.0.0.0")]
    [Arguments("cidr", "::/129")]
    [Arguments("uri", "relative/path")]
    [Arguments("uri", "https://exa mple.org")]
    [Arguments("http-url", "ftp://example.org")]
    [Arguments("http-url", "https://user:pass@example.org")]
    [Arguments("https-url", "http://plain.example")]
    [Arguments("https-url", "https://")]
    [Arguments("origin", "https://example.org/")]
    [Arguments("origin", "https://example.org/path")]
    [Arguments("origin", "https://example.org:70000")]
    [Arguments("email-address", "no-at-sign.example.org")]
    [Arguments("email-address", ".lead@example.org")]
    [Arguments("slug", "Bad_Slug!")]
    [Arguments("slug", "trailing-")]
    [Arguments("slug", "double--dash")]
    [Arguments("env-key", "1STARTS_WITH_DIGIT")]
    [Arguments("env-key", "HAS-DASH")]
    [Arguments("bcp47-language-tag", "e")]
    [Arguments("bcp47-language-tag", "en--US")]
    [Arguments("bcp47-language-tag", "toolongsubtag")]
    [Arguments("iso3166-alpha2", "be")]
    [Arguments("iso3166-alpha2", "BEL")]
    [Arguments("uuid", "69cdb12241d24fab9b827929a889c726")]
    [Arguments("semver", "01.2.3")]
    [Arguments("semver", "v1.2.3")]
    [Arguments("semver", "1.2")]
    [Arguments("semver", "1.2.3-01")]
    [Arguments("duration", "30s")]
    [Arguments("duration", "P")]
    [Arguments("duration", "PT")]
    [Arguments("duration", "P1W2D")]
    [Arguments("byte-size", "12 parsecs")]
    [Arguments("byte-size", "-5MB")]
    [Arguments("byte-size", "MB")]
    [Arguments("unix-path", "relative/path")]
    [Arguments("unix-path", "/etc/../passwd")]
    [Arguments("unix-path", "/double//slash")]
    [Arguments("relative-path", "../../etc/passwd")]
    [Arguments("relative-path", "/absolute")]
    [Arguments("relative-path", "a/../b")]
    [Arguments("oci-image", "UPPER/case")]
    [Arguments("oci-image", "app:")]
    [Arguments("oci-image", "app@sha256:short")]
    [Arguments("sha256-hex", "0123456789ABCDEF0123456789abcdef0123456789abcdef0123456789abcdef")]
    [Arguments("sha256-hex", "abc")]
    [Arguments("cron-5", "* * * *")]
    [Arguments("cron-5", "60 * * * *")]
    [Arguments("cron-5", "@daily")]
    [Arguments("cron-5", "*/0 * * * *")]
    public async Task InvalidValuesFailTheirFormat(string format, string value)
    {
        await Assert.That(DefinitionFormatValidator.IsValid(format, value)).IsFalse();
    }

    [Test]
    public async Task EveryAssertedFormatIsRegistered()
    {
        string[] expected =
        [
            "bcp47-language-tag", "byte-size", "cidr", "cron-5", "duration", "email-address", "env-key",
            "hostname", "http-url", "https-url", "ip-address", "ipv4", "ipv6", "iso3166-alpha2", "oci-image",
            "origin", "relative-path", "semver", "sha256-hex", "slug", "unix-path", "uri", "uuid"
        ];
        await Assert.That(DefinitionFormatValidator.Formats.Order(StringComparer.Ordinal).ToArray())
            .IsEquivalentTo(expected, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task PathologicalNearMissInputsCompleteInLinearTime()
    {
        string[] samples =
        [
            new string('a', 65535) + "!", string.Concat(Enumerable.Repeat("a.", 32767)) + "-",
            string.Concat(Enumerable.Repeat("1:", 32767)) + "x", "P" + new string('9', 65534) + "X",
            "/" + string.Concat(Enumerable.Repeat("a/", 32766)) + "..", string.Concat(Enumerable.Repeat("a-", 32767)) + "-",
            "https://" + new string('a', 65520) + ".", string.Concat(Enumerable.Repeat("1,", 32767)) + " * * * *"
        ];
        var clock = Stopwatch.StartNew();
        foreach (string format in DefinitionFormatValidator.Formats)
            foreach (string sample in samples)
                _ = DefinitionFormatValidator.IsValid(format, sample);
        clock.Stop();
        await Assert.That(clock.Elapsed).IsLessThan(TimeSpan.FromSeconds(5));
    }

    [Test]
    public async Task KnownFormatsValidateAnswersAutomaticallyWithValueFreeDiagnostics()
    {
        Definition definition = Compile(Field("""{"type":"string","format":"https-url","x-configurator":{"sensitivity":"public"}}"""));
        const string invalid = "http://plain.example";
        DefinitionValidationResult result = DefinitionValidator.Validate(definition, Answers("""{"endpoint":"http://plain.example"}"""));
        DefinitionValidationResult valid = DefinitionValidator.Validate(definition, Answers("""{"endpoint":"https://plain.example"}"""));

        await Assert.That(result.Diagnostics.Select(diagnostic => diagnostic.Code)).Contains("format-invalid");
        await Assert.That(string.Join("|", result.Diagnostics.Select(diagnostic => diagnostic.ToString()))).DoesNotContain(invalid);
        await Assert.That(valid.Diagnostics.Any(diagnostic => diagnostic.Code == "format-invalid")).IsFalse();
    }

    [Test]
    [Arguments("""{"type":"string","format":"hostname","x-configurator":{"sensitivity":"public","assertFormat":true}}""", "member-unsupported")]
    [Arguments("""{"type":"string","pattern":"^a+$","x-configurator":{"sensitivity":"public"}}""", "member-unsupported")]
    [Arguments("""{"type":"string","format":"date-time","x-configurator":{"sensitivity":"public"}}""", "format-unsupported")]
    [Arguments("""{"type":"integer","format":"hostname","x-configurator":{"sensitivity":"public"}}""", "constraint-context")]
    public async Task RetiredOrUnboundedFormatSemanticsRejectAtAuthoring(string field, string code)
    {
        await Assert.That(Failure(() => Compile(Field(field)))).IsEqualTo(code);
    }

    [Test]
    public async Task DefaultTextAndPlaceholderNeverMaterializeIntoAnswersOrArtifacts()
    {
        Definition definition = Compile(Field("""
            {"type":"integer","minimum":1,"x-configurator":{"sensitivity":"public","placeholder":"30",
              "defaultText":"Target uses 30 seconds when omitted","units":"seconds"}}
            """));
        FieldDefinition field = definition.Fields.Single();
        DefinitionValidationResult result = DefinitionValidator.Validate(definition, Answers("{}"));

        await Assert.That(field.PresentationMetadata.Placeholder).IsEqualTo("30");
        await Assert.That(field.PresentationMetadata.DefaultText).IsEqualTo("Target uses 30 seconds when omitted");
        await Assert.That(field.Default).IsNull();
        await Assert.That(field.InitialValue).IsNull();
        DefinitionArtifactResult artifact = DefinitionArtifactComposer.Compose(definition, definition.Artifacts.Single().Id, Answers("{}"));
        await Assert.That(result.IsReady).IsTrue();
        await Assert.That(artifact.Succeeded).IsTrue();
        await Assert.That(Encoding.UTF8.GetString(artifact.Artifact!.Bytes.Span)).IsEqualTo("{}");
        await Assert.That(result.TryGetEffectiveValue(DefinitionPath.Parse("/endpoint"), out _)).IsFalse();
    }

    [Test]
    [Arguments("""{"type":"string","examples":["sample-token"],"x-configurator":{"sensitivity":"secret"}}""", "public-value-protected")]
    [Arguments("""{"type":"string","default":"sample-token","x-configurator":{"sensitivity":"secret"}}""", "public-value-protected")]
    [Arguments("""{"type":"string","x-configurator":{"sensitivity":"secret","initialValue":"sample-token"}}""", "public-value-invalid")]
    [Arguments("""{"type":"string","x-configurator":{"sensitivity":"secret","placeholder":"sk-live-0000"}}""", "public-value-protected")]
    [Arguments("""{"type":"string","x-configurator":{"sensitivity":"sensitive","placeholder":"db-password"}}""", "public-value-protected")]
    [Arguments("""{"type":"string","x-configurator":{"sensitivity":"public","placeholder":"<script>"}}""", "text-invalid")]
    [Arguments("""{"type":"string","x-configurator":{"sensitivity":"public","defaultText":"Uses \u0007 bell"}}""", "text-invalid")]
    [Arguments("""{"type":"string","deprecated":true,"x-configurator":{"sensitivity":"public","deprecationMessage":"Use <b>new</b>"}}""", "text-invalid")]
    [Arguments("""{"type":"string","x-configurator":{"sensitivity":"public","deprecationMessage":"Not deprecated"}}""", "deprecation-invalid")]
    public async Task SensitivityFloorRejectsExemplarsAndMarkupWithValueFreeCodes(string field, string code)
    {
        DefinitionException? failure = null;
        try { _ = Compile(Field(field)); }
        catch (DefinitionException exception) { failure = exception; }
        await Assert.That(failure?.Code).IsEqualTo(code);
        await Assert.That(failure!.ToString()).DoesNotContain("sk-live-0000");
        await Assert.That(failure.ToString()).DoesNotContain("sample-token");
    }

    private static string Field(string schema) => """
        {"$schema":"urn:islamu:configurator:authoring:v0.0.1",
         "identity":{"familyId":"
        """ + FamilyId + """
        ","revision":1},
         "project":{"id":"formats.sample","name":"Formats Sample"},"applicationVersions":["1.0"],
         "outputs":{"settings.json":{"format":"json"}},
         "configuration":{"type":"object","additionalProperties":false,"x-configurator":{"sensitivity":"public"},
           "properties":{"endpoint":
        """ + schema + "}}}";

    private static Definition Compile(string source) =>
        DefinitionAuthoringCompiler.Compile(DefinitionAuthoringCodec.ParseJson(Encoding.UTF8.GetBytes(source)));

    private static AnswerSnapshot Answers(string values) => AnswerSnapshotCodec.ParseJson(Encoding.UTF8.GetBytes(
        JsonNode.Parse("""{"answerContractVersion":"0.0.1","omitted":[]}""")!.AsObject()
            .Also(root => root["values"] = JsonNode.Parse(values)).ToJsonString()));

    private static string? Failure(Func<Definition> action)
    {
        try { _ = action(); }
        catch (DefinitionException exception) { return exception.Code; }
        return null;
    }
}

internal static class JsonObjectExtensions
{
    internal static JsonObject Also(this JsonObject node, Action<JsonObject> change)
    {
        change(node);
        return node;
    }
}
