namespace Configurator.Core.Tests.Definitions;

using Configurator.Core.Definitions;

public sealed class DefinitionDiagnosticPathTests
{
    [Test]
    public async Task ContainerNodeCannotDemoteNestedSecretSensitivity()
    {
        System.Text.Json.Nodes.JsonObject source = System.Text.Json.Nodes.JsonNode.Parse(Definition().CanonicalBytes.Span)!.AsObject();
        source["fields"]![1]!["properties"]![0]!["sensitivity"] = "secret";
        DefinitionValidationResult result = DefinitionValidator.Validate(DefinitionFixture.Parse(source.ToJsonString()),
            DefinitionFixture.Answers("""{"rows":[],"options":{"dotted.name":1}}"""));

        await Assert.That(result.Nodes.Single(node => node.FieldId == "options" && node.RelativePath.Count == 0).Sensitivity)
            .IsEqualTo(SetupFieldSensitivity.Secret);
    }

    [Test]
    public async Task DiagnosticEqualityUsesLiteralSegmentValues()
    {
        var first = new DefinitionDiagnostic("value-invalid", "rows", ["1", "name"]);
        var equal = new DefinitionDiagnostic("value-invalid", "rows", ["1", "name"]);
        var distinct = new DefinitionDiagnostic("value-invalid", "rows", ["1.name"]);
        await Assert.That(first).IsEqualTo(equal);
        await Assert.That(first.GetHashCode()).IsEqualTo(equal.GetHashCode());
        await Assert.That(first).IsNotEqualTo(distinct);
    }

    [Test]
    public async Task DuplicateRowKeepsRootIdentityAndIdentifiesItsDeclaredKey()
    {
        DefinitionValidationResult result = DefinitionValidator.Validate(Definition(),
            DefinitionFixture.Answers("""
            {"rows":[{"name":"synthetic-private-row"},{"name":"synthetic-private-row"}],"options":{"dotted.name":1}}
            """));

        DefinitionDiagnostic diagnostic = result.Diagnostics.Single();
        await Assert.That(diagnostic.Code).IsEqualTo("value-duplicate");
        await Assert.That(diagnostic.FieldId).IsEqualTo("rows");
        await Assert.That(diagnostic.RelativePath.SequenceEqual(["1", "name"])).IsTrue();
        await Assert.That(diagnostic.ToString()).DoesNotContain("synthetic-private-row");
    }

    [Test]
    public async Task LiteralDottedPropertyIsOneCorrectionSegment()
    {
        DefinitionValidationResult result = DefinitionValidator.Validate(Definition(),
            DefinitionFixture.Answers("""
            {"rows":[],"options":{"dotted.name":"synthetic-private-value"}}
            """));

        DefinitionDiagnostic diagnostic = result.Diagnostics.Single();
        await Assert.That(diagnostic.FieldId).IsEqualTo("options");
        await Assert.That(diagnostic.RelativePath.SequenceEqual(["dotted.name"])).IsTrue();
        await Assert.That(diagnostic.ToString()).DoesNotContain("synthetic-private-value");
    }

    [Test]
    public async Task UnknownAnswerPropertiesNeverBecomeDiagnosticPathSegments()
    {
        DefinitionValidationResult result = DefinitionValidator.Validate(Definition(),
            DefinitionFixture.Answers("""
            {"rows":[],"options":{"dotted.name":1,"synthetic-private-key":"synthetic-private-value"}}
            """));

        DefinitionDiagnostic diagnostic = result.Diagnostics.Single();
        await Assert.That(diagnostic.FieldId).IsEqualTo("options");
        await Assert.That(diagnostic.RelativePath.Count).IsEqualTo(0);
        await Assert.That(diagnostic.ToString()).DoesNotContain("synthetic-private");
    }

    [Test]
    public async Task RequiredNestedEmptyStringCannotClaimReadiness()
    {
        DefinitionValidationResult result = DefinitionValidator.Validate(Definition(),
            DefinitionFixture.Answers("""
            {"rows":[{"name":""}],"options":{"dotted.name":1}}
            """));

        await Assert.That(result.IsReady).IsFalse();
        DefinitionDiagnostic diagnostic = result.Diagnostics.Single();
        await Assert.That(diagnostic.FieldId).IsEqualTo("rows");
        await Assert.That(diagnostic.RelativePath.SequenceEqual(["0", "name"])).IsTrue();
        await Assert.That(diagnostic.Code).IsEqualTo("value-required");
    }

    [Test]
    public async Task DiagnosticSnapshotsCallerOwnedPathSegments()
    {
        string[] source = ["1", "name"];
        var diagnostic = new DefinitionDiagnostic("value-invalid", "rows", source);
        source[0] = "changed";

        await Assert.That(diagnostic.RelativePath.SequenceEqual(["1", "name"])).IsTrue();
    }

    private static SetupDefinition Definition() => DefinitionFixture.Parse("""
    {"schemaVersion":1,"familyId":"0d9f1857-93ad-4b7d-8be1-bd9e7223c587","revision":1,
     "applicationId":"example","applicationVersions":["1"],"publisher":"example","title":"Paths",
     "entryPoints":["setup"],"locales":["en"],
     "fields":[
       {"id":"rows","type":"list","itemType":"object","maxItems":3,"uniqueBy":["name"],
        "title":"Rows","sensitivity":"public","disclosure":"advanced","properties":[
          {"id":"name","type":"string","required":true,"title":"Name","sensitivity":"public","disclosure":"guided"}]},
       {"id":"options","type":"object","title":"Options","sensitivity":"public","disclosure":"guided","properties":[
         {"id":"dotted.name","type":"integer","required":true,"title":"Number","sensitivity":"public","disclosure":"guided"}]}],
     "artifacts":[{"id":"config","format":"json","fileName":"service.json",
       "applySemantics":"create","restart":"process","projection":{"op":"literal","value":{}}}]}
    """);
}
