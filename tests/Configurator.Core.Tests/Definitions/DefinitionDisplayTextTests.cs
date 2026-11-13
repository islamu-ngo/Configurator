using System.Text;
using Configurator.Core.Definitions;
using Configurator.Core.Definitions.Authoring;
using TUnit.Assertions.Enums;

namespace Configurator.Core.Tests.Definitions;

/// <summary>Specifies untitled identifier humanization independently of the implementation.</summary>
public sealed class DefinitionDisplayTextTests
{
    private static readonly string[] ChoiceLabels = ["OpenAI Compatible", ">=1.2"];
    private static readonly string[] ChoiceTokens = ["OPENAI_COMPATIBLE", ">=1.2"];

    [Test]
    [Arguments("SCREAMING_SNAKE_CASE", "Screaming Snake Case")]
    [Arguments("PUBLIC_BASE_URL", "Public Base URL")]
    [Arguments("OPENAI_COMPATIBLE", "OpenAI Compatible")]
    [Arguments("OIDC_JWKS_URI", "OIDC JWKS URI")]
    [Arguments("ASPNETCORE_FORWARDED_HEADERS", "ASP.NET Core Forwarded Headers")]
    [Arguments("POSTGRESQL_CONNECTION_STRING", "PostgreSQL Connection String")]
    [Arguments("S3_COMPATIBLE", "S3 Compatible")]
    [Arguments("LOCAL", "Local")]
    [Arguments("user_id", "User ID")]
    [Arguments("allow_unscanned_documents", "Allow Unscanned Documents")]
    [Arguments("max-connections", "Max Connections")]
    [Arguments("maxConnectionsPerHost", "Max Connections Per Host")]
    [Arguments("PascalCaseName", "Pascal Case Name")]
    [Arguments("HTTPServerTimeout", "HTTP Server Timeout")]
    [Arguments("smtp_tls_mode", "SMTP TLS Mode")]
    public async Task IdentifierStylesBecomeTitleCaseWithCanonicalAcronyms(string identifier, string expected)
    {
        await Assert.That(DefinitionDisplayText.Humanize(identifier)).IsEqualTo(expected);
    }

    [Test]
    [Arguments("env.PUBLIC_BASE_URL", "Public Base URL")]
    [Arguments("Logging__LogLevel__Default", "Logging / Log Level / Default")]
    [Arguments("Kestrel__Endpoints__Http__Url", "Kestrel / Endpoints / HTTP / URL")]
    public async Task ContainerPrefixesAndHierarchySeparatorsAreFormatted(string identifier, string expected)
    {
        await Assert.That(DefinitionDisplayText.Humanize(identifier)).IsEqualTo(expected);
    }

    [Test]
    [Arguments(">=1.2")]
    [Arguments("0.0.0.0/0")]
    [Arguments("/var/lib/app")]
    [Arguments("application/json")]
    [Arguments("1.2.3")]
    [Arguments("v1.2")]
    [Arguments("Already readable text")]
    [Arguments("")]
    public async Task SymbolicLiteralsPassThroughUnchanged(string literal)
    {
        await Assert.That(DefinitionDisplayText.Humanize(literal)).IsEqualTo(literal);
    }

    [Test]
    public async Task ExplicitTextWinsOverHumanization()
    {
        await Assert.That(DefinitionDisplayText.Resolve("Base address", "PUBLIC_BASE_URL")).IsEqualTo("Base address");
        await Assert.That(DefinitionDisplayText.Resolve(null, "PUBLIC_BASE_URL")).IsEqualTo("Public Base URL");
    }

    [Test]
    public async Task AdmittedFieldsExposeDisplayTitlesWhilePreservingWireNames()
    {
        Definition definition = Compile("""
            {"$schema":"urn:islamu:configurator:authoring:v0.0.1",
             "identity":{"familyId":"0d8bd0a4-1a52-4d6c-9fd9-58d1d38bd3c1","revision":1},
             "project":{"id":"display.sample","name":"Display Sample"},"applicationVersions":["1.0"],
             "configuration":{"type":"object","additionalProperties":false,"x-configurator":{"sensitivity":"public"},
               "properties":{
                 "SCREAMING_SNAKE_CASE":{"type":"string","x-configurator":{"sensitivity":"public"}},
                 "EXPLICIT_KEY":{"type":"string","title":"Hand written","x-configurator":{"sensitivity":"public"}},
                 "connection":{"type":"object","additionalProperties":false,"x-configurator":{"sensitivity":"public"},
                   "properties":{"maxConnectionsPerHost":{"type":"integer","x-configurator":{"sensitivity":"public"}}}}}},
             "outputs":{"settings.json":{"format":"json"}}}
            """);

        FieldDefinition untitled = definition.Fields.Single(field => field.Name == "SCREAMING_SNAKE_CASE");
        await Assert.That(untitled.Title).IsEqualTo("Screaming Snake Case");
        await Assert.That(untitled.DisplayTitle).IsEqualTo("Screaming Snake Case");
        await Assert.That(untitled.TechnicalName).IsEqualTo("SCREAMING_SNAKE_CASE");
        await Assert.That(untitled.HasExplicitTitle).IsFalse();

        FieldDefinition titled = definition.Fields.Single(field => field.Name == "EXPLICIT_KEY");
        await Assert.That(titled.Title).IsEqualTo("Hand written");
        await Assert.That(titled.TechnicalName).IsEqualTo("EXPLICIT_KEY");
        await Assert.That(titled.HasExplicitTitle).IsTrue();

        FieldDefinition nested = definition.Fields.Single(field => field.Name == "connection").Properties.Single();
        await Assert.That(nested.Title).IsEqualTo("Max Connections Per Host");
        await Assert.That(nested.TechnicalName).IsEqualTo("maxConnectionsPerHost");
    }

    [Test]
    public async Task UntitledEnumTokensHumanizeWhileSymbolicTokensStayVerbatim()
    {
        Definition definition = Compile("""
            {"$schema":"urn:islamu:configurator:authoring:v0.0.1",
             "identity":{"familyId":"0d8bd0a4-1a52-4d6c-9fd9-58d1d38bd3c1","revision":1},
             "project":{"id":"display.sample","name":"Display Sample"},"applicationVersions":["1.0"],
             "configuration":{"type":"object","additionalProperties":false,"x-configurator":{"sensitivity":"public"},
               "properties":{"provider":{"type":"string","enum":["OPENAI_COMPATIBLE",">=1.2"],
                 "x-configurator":{"sensitivity":"public"}}}},
             "outputs":{"settings.json":{"format":"json"}}}
            """);

        IReadOnlyList<DefinitionChoice> choices = definition.Fields.Single().OptionMetadata;
        await Assert.That(choices.Select(choice => choice.DisplayLabel).ToArray())
            .IsEquivalentTo(ChoiceLabels, CollectionOrdering.Matching);
        await Assert.That(choices.Select(choice => choice.WireToken).ToArray())
            .IsEquivalentTo(ChoiceTokens, CollectionOrdering.Matching);
        await Assert.That(choices.All(choice => !choice.HasExplicitLabel)).IsTrue();
    }

    internal static Definition Compile(string source) =>
        DefinitionAuthoringCompiler.Compile(DefinitionAuthoringCodec.ParseJson(Encoding.UTF8.GetBytes(source)));
}
