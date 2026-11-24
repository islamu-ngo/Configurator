namespace Configurator.Core.Tests.Definitions;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Configurator.Core.Definitions;
using Configurator.Core.Definitions.Authoring;

public sealed class DefinitionFixtureRunnerTests
{
    [Test]
    public async Task AdversarialExecutableNetworkAndUnknownKeywordsRejectFailClosedWithoutEchoingValues()
    {
        foreach (string forbiddenField in new[] { "command", "shell", "url", "script", "exec" })
        {
            string json = $$$"""
                {
                  "fixtureContractVersion": "0.0.1",
                  "cases": [
                    {
                      "id": "bad-case",
                      "kind": "prepare",
                      "{{{forbiddenField}}}": "SYNTH_EXEC_CANARY_{{{forbiddenField}}}",
                      "answers": {"answerContractVersion":"0.0.1","values":{"SERVICE_TOKEN":"tok"}},
                      "expect": {"ready": true}
                    }
                  ]
                }
                """;
            DefinitionException? failure = null;
            try { _ = DefinitionFixtureDocument.ParseJson(Encoding.UTF8.GetBytes(json)); }
            catch (DefinitionException exception) { failure = exception; }

            await Assert.That(failure).IsNotNull();
            await Assert.That(failure!.Code).IsEqualTo("fixture-member");
            await Assert.That(failure.ToString()).DoesNotContain("SYNTH_EXEC_CANARY");
            await Assert.That(failure.ToString()).DoesNotContain(forbiddenField);
        }
    }

    [Test]
    public async Task VacuousZeroCaseAndOversizedCaseCountAndOversizedDocumentRejectFailClosed()
    {
        const string emptyCases = """{"fixtureContractVersion":"0.0.1","cases":[]}""";
        DefinitionException? emptyFailure = null;
        try { _ = DefinitionFixtureDocument.ParseJson(Encoding.UTF8.GetBytes(emptyCases)); }
        catch (DefinitionException exception) { emptyFailure = exception; }
        await Assert.That(emptyFailure).IsNotNull();
        await Assert.That(emptyFailure!.Code).IsEqualTo("fixture-empty");

        var cases = new JsonArray();
        for (int index = 0; index < 65; index++)
        {
            cases.Add(new JsonObject
            {
                ["id"] = $"case-{index}",
                ["kind"] = "prepare",
                ["answers"] = new JsonObject
                {
                    ["answerContractVersion"] = "0.0.1",
                    ["values"] = new JsonObject { ["SERVICE_TOKEN"] = "tok" }
                },
                ["expect"] = new JsonObject { ["ready"] = true }
            });
        }
        byte[] tooManyCases = Encoding.UTF8.GetBytes(new JsonObject
        {
            ["fixtureContractVersion"] = "0.0.1",
            ["cases"] = cases
        }.ToJsonString());
        DefinitionException? limitFailure = null;
        try { _ = DefinitionFixtureDocument.ParseJson(tooManyCases); }
        catch (DefinitionException exception) { limitFailure = exception; }
        await Assert.That(limitFailure).IsNotNull();
        await Assert.That(limitFailure!.Code).IsEqualTo("fixture-limit");

        byte[] oversizedDoc = new byte[(4 * 1024 * 1024) + 16];
        DefinitionException? sizeFailure = null;
        try { _ = DefinitionFixtureDocument.ParseJson(oversizedDoc); }
        catch (DefinitionException exception) { sizeFailure = exception; }
        await Assert.That(sizeFailure).IsNotNull();
        await Assert.That(sizeFailure!.Code).IsEqualTo("document-limit");
    }

    [Test]
    public async Task ValidSuiteBindsDigestEngineAndScopeAndSeparatesExpectedRefusalFromFailure()
    {
        Definition definition = DefinitionAuthoringTests.Compile();
        string artifactId = definition.Artifacts.Single().Id;
        const string expectedEnv = "SERVICE_PORT=8080\nSERVICE_TOKEN=synthetic-fixture-token\n";
        string expectedSha256 = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(expectedEnv)));

        string suiteJson = $$"""
            {
              "fixtureContractVersion": "0.0.1",
              "cases": [
                {
                  "id": "ready-output",
                  "kind": "prepare",
                  "answers": {
                    "answerContractVersion": "0.0.1",
                    "values": { "SERVICE_TOKEN": "synthetic-fixture-token" }
                  },
                  "expect": {
                    "ready": true,
                    "artifacts": [
                      {
                        "id": "{{artifactId}}",
                        "fileName": ".env",
                        "contentUtf8": {{JsonSerializer.Serialize(expectedEnv)}},
                        "sha256": "{{expectedSha256}}"
                      }
                    ]
                  }
                },
                {
                  "id": "expected-missing-token-refusal",
                  "kind": "prepare",
                  "answers": {
                    "answerContractVersion": "0.0.1",
                    "values": {}
                  },
                  "expect": {
                    "ready": false,
                    "refusal": "validation-failed",
                    "diagnosticCodes": ["value-required"]
                  }
                },
                {
                  "id": "inverse-import-check",
                  "kind": "import",
                  "artifactId": "{{artifactId}}",
                  "contentUtf8": {{JsonSerializer.Serialize(expectedEnv)}},
                  "expect": {
                    "importCompatibility": "compatible",
                    "importReady": true
                  }
                }
              ]
            }
            """;

        DefinitionFixtureDocument suite = DefinitionFixtureDocument.ParseJson(Encoding.UTF8.GetBytes(suiteJson));
        DefinitionFixtureRunReport report = DefinitionFixtureRunner.Run(definition, suite);

        await Assert.That(report.Passed).IsTrue();
        await Assert.That(report.DefinitionDigest).IsEqualTo(definition.Digest.Value);
        await Assert.That(report.EngineVersion).IsEqualTo(ConfiguratorVersion.Current);
        await Assert.That(report.Scope).IsEqualTo(DefinitionFixtureRunner.ConformanceScope);
        await Assert.That(report.TotalCases).IsEqualTo(3);
        await Assert.That(report.PassedCases).IsEqualTo(3);
        await Assert.That(report.FailedCases).IsEqualTo(0);
        await Assert.That(report.ExpectedRefusalCases).IsEqualTo(1);
        await Assert.That(report.Results[0].Outcome).IsEqualTo(DefinitionFixtureCaseOutcome.Passed);
        await Assert.That(report.Results[1].Outcome).IsEqualTo(DefinitionFixtureCaseOutcome.PassedExpectedRefusal);
        await Assert.That(report.Results[1].ExpectedRefusal).IsTrue();
        await Assert.That(report.Results[2].Outcome).IsEqualTo(DefinitionFixtureCaseOutcome.Passed);
    }

    [Test]
    public async Task MismatchedExpectedBytesFailsCaseWithoutLeakingAnswerValues()
    {
        Definition definition = DefinitionAuthoringTests.Compile();
        string artifactId = definition.Artifacts.Single().Id;
        string suiteJson = $$"""
            {
              "fixtureContractVersion": "0.0.1",
              "cases": [
                {
                  "id": "wrong-bytes",
                  "kind": "prepare",
                  "answers": {
                    "answerContractVersion": "0.0.1",
                    "values": { "SERVICE_TOKEN": "SYNTH_SECRET_VALUE_CANARY_999" }
                  },
                  "expect": {
                    "ready": true,
                    "artifacts": [
                      {
                        "id": "{{artifactId}}",
                        "contentUtf8": "SERVICE_PORT=9999\n"
                      }
                    ]
                  }
                }
              ]
            }
            """;

        DefinitionFixtureDocument suite = DefinitionFixtureDocument.ParseJson(Encoding.UTF8.GetBytes(suiteJson));
        DefinitionFixtureRunReport report = DefinitionFixtureRunner.Run(definition, suite);

        await Assert.That(report.Passed).IsFalse();
        await Assert.That(report.FailedCases).IsEqualTo(1);
        await Assert.That(report.Results[0].Outcome).IsEqualTo(DefinitionFixtureCaseOutcome.Failed);
        await Assert.That(report.Results[0].FailureCode).IsEqualTo("artifact-content-mismatch");
        await Assert.That(JsonSerializer.Serialize(report)).DoesNotContain("SYNTH_SECRET_VALUE_CANARY_999");
    }
}
