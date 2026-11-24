namespace Configurator.Core.Definitions.Authoring;

using System.Text.Json.Nodes;

/// <summary>Generates the unversioned 0.0.1 JSON Schema for bounded data-only synthetic conformance fixture suites.</summary>
public static class DefinitionFixtureSchema
{
    public static string Generate() => DefinitionSchema.Render(Create());

    private static JsonObject Create()
    {
        var answerEnvelope = new JsonObject
        {
            ["type"] = "object",
            ["additionalProperties"] = false,
            ["required"] = new JsonArray("answerContractVersion", "values"),
            ["properties"] = new JsonObject
            {
                ["answerContractVersion"] = new JsonObject
                {
                    ["const"] = DefinitionFixtureDocument.ContractVersion,
                    ["description"] = "Synthetic answer envelope contract version."
                },
                ["values"] = new JsonObject
                {
                    ["type"] = "object",
                    ["description"] = "Synthetic answer values keyed by root configuration field."
                },
                ["omitted"] = new JsonObject
                {
                    ["type"] = "array",
                    ["uniqueItems"] = true,
                    ["default"] = new JsonArray(),
                    ["items"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["maxLength"] = 65536,
                        ["pattern"] = "^(?:/(?:[^~]|~[01])*)*$"
                    }
                }
            }
        };

        var artifactExpectation = new JsonObject
        {
            ["type"] = "object",
            ["additionalProperties"] = false,
            ["required"] = new JsonArray("id"),
            ["properties"] = new JsonObject
            {
                ["id"] = new JsonObject
                {
                    ["type"] = "string",
                    ["minLength"] = 1,
                    ["maxLength"] = DefinitionFixtureDocument.MaximumCaseIdLength,
                    ["description"] = "Declared artifact identifier."
                },
                ["fileName"] = new JsonObject
                {
                    ["type"] = "string",
                    ["minLength"] = 1,
                    ["maxLength"] = DefinitionFixtureDocument.MaximumCaseIdLength,
                    ["description"] = "Expected artifact file name."
                },
                ["contentUtf8"] = new JsonObject
                {
                    ["type"] = "string",
                    ["description"] = "Expected literal UTF-8 artifact content. Mutually exclusive with contentBase64."
                },
                ["contentBase64"] = new JsonObject
                {
                    ["type"] = "string",
                    ["description"] = "Expected base64-encoded artifact bytes. Mutually exclusive with contentUtf8."
                },
                ["sha256"] = new JsonObject
                {
                    ["type"] = "string",
                    ["pattern"] = "^[0-9a-f]{64}$",
                    ["description"] = "Expected lowercase hexadecimal SHA-256 digest."
                },
                ["deferred"] = new JsonObject
                {
                    ["type"] = "boolean",
                    ["description"] = "For draft cases, whether this artifact is expected to carry deferred secret completions."
                }
            }
        };

        var expectation = new JsonObject
        {
            ["type"] = "object",
            ["additionalProperties"] = false,
            ["minProperties"] = 1,
            ["properties"] = new JsonObject
            {
                ["ready"] = new JsonObject
                {
                    ["type"] = "boolean",
                    ["description"] = "Expected preparation readiness."
                },
                ["refusal"] = new JsonObject
                {
                    ["type"] = "string",
                    ["minLength"] = 1,
                    ["maxLength"] = DefinitionFixtureDocument.MaximumCaseIdLength,
                    ["description"] = "Expected closed refusal or diagnostic code."
                },
                ["diagnosticCodes"] = new JsonObject
                {
                    ["type"] = "array",
                    ["maxItems"] = DefinitionFixtureDocument.MaximumDiagnosticCodesPerCase,
                    ["items"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["minLength"] = 1,
                        ["maxLength"] = DefinitionFixtureDocument.MaximumCaseIdLength
                    },
                    ["description"] = "Closed diagnostic codes expected in the result."
                },
                ["artifacts"] = new JsonObject
                {
                    ["type"] = "array",
                    ["maxItems"] = DefinitionFixtureDocument.MaximumArtifactExpectationsPerCase,
                    ["items"] = artifactExpectation,
                    ["description"] = "Expected prepared artifacts for prepare cases."
                },
                ["importCompatibility"] = new JsonObject
                {
                    ["type"] = "string",
                    ["enum"] = new JsonArray("compatible", "changes-required", "incomplete", "unsupported", "invalid-input"),
                    ["description"] = "Expected inverse import compatibility classification for import cases."
                },
                ["importReady"] = new JsonObject
                {
                    ["type"] = "boolean",
                    ["description"] = "Expected post-import readiness for import cases."
                }
            }
        };

        var fixtureCase = new JsonObject
        {
            ["type"] = "object",
            ["additionalProperties"] = false,
            ["required"] = new JsonArray("id", "kind", "expect"),
            ["properties"] = new JsonObject
            {
                ["id"] = new JsonObject
                {
                    ["type"] = "string",
                    ["minLength"] = 1,
                    ["maxLength"] = DefinitionFixtureDocument.MaximumCaseIdLength,
                    ["pattern"] = "^[a-z0-9][a-z0-9._-]{0,127}$",
                    ["description"] = "Unique synthetic fixture case identifier."
                },
                ["description"] = new JsonObject
                {
                    ["type"] = "string",
                    ["minLength"] = 1,
                    ["maxLength"] = DefinitionFixtureDocument.MaximumDescriptionLength,
                    ["description"] = "Public description of the synthetic case."
                },
                ["kind"] = new JsonObject
                {
                    ["type"] = "string",
                    ["enum"] = new JsonArray("prepare", "import"),
                    ["description"] = "Offline engine flow exercised by this synthetic case."
                },
                ["draft"] = new JsonObject
                {
                    ["type"] = "boolean",
                    ["default"] = false,
                    ["description"] = "When true on a prepare case, composes a secret-deferred draft bundle."
                },
                ["artifactId"] = new JsonObject
                {
                    ["type"] = "string",
                    ["minLength"] = 1,
                    ["maxLength"] = DefinitionFixtureDocument.MaximumCaseIdLength,
                    ["description"] = "Artifact identifier to inspect (required for import; optional single-artifact selection for prepare)."
                },
                ["answers"] = answerEnvelope,
                ["contextAnswers"] = answerEnvelope.DeepClone(),
                ["contentUtf8"] = new JsonObject
                {
                    ["type"] = "string",
                    ["description"] = "Synthetic UTF-8 artifact input for import cases. Mutually exclusive with contentBase64."
                },
                ["contentBase64"] = new JsonObject
                {
                    ["type"] = "string",
                    ["description"] = "Synthetic base64-encoded artifact input for import cases. Mutually exclusive with contentUtf8."
                },
                ["expect"] = expectation
            }
        };

        return new JsonObject
        {
            ["$schema"] = "https://json-schema.org/draft/2020-12/schema",
            ["$id"] = DefinitionFixtureDocument.SchemaId,
            ["title"] = "Configurator synthetic conformance fixtures " + DefinitionFixtureDocument.ContractVersion,
            ["type"] = "object",
            ["additionalProperties"] = false,
            ["required"] = new JsonArray("fixtureContractVersion", "cases"),
            ["properties"] = new JsonObject
            {
                ["$schema"] = new JsonObject
                {
                    ["const"] = DefinitionFixtureDocument.SchemaId,
                    ["description"] = "Optional schema identifier for editor validation."
                },
                ["fixtureContractVersion"] = new JsonObject
                {
                    ["const"] = DefinitionFixtureDocument.ContractVersion,
                    ["description"] = "Synthetic conformance fixture contract version."
                },
                ["cases"] = new JsonObject
                {
                    ["type"] = "array",
                    ["minItems"] = 1,
                    ["maxItems"] = DefinitionFixtureDocument.MaximumCases,
                    ["items"] = fixtureCase,
                    ["description"] = "Non-empty list of data-only synthetic conformance cases."
                }
            }
        };
    }
}
