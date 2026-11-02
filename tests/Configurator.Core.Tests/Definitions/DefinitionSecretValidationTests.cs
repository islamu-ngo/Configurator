namespace Configurator.Core.Tests.Definitions;

using System.Text;
using System.Text.Json;
using Configurator.Core.Definitions;

public sealed class DefinitionSecretValidationTests
{
    [Test]
    public async Task WeakPasswordFailsCompleteAndDraftWithSafeCorrection()
    {
        using JsonDocument policy = JsonDocument.Parse("""{"kind":"password","acquisition":"existing","constraints":{"minSymbols":1}}""");
        string source = DefinitionFixture.Json.Replace("\"sensitivity\":\"public\",\"detail\":\"advanced\"",
            "\"secret\":" + policy.RootElement.GetRawText(), StringComparison.Ordinal);
        SetupDefinition definition = DefinitionFixture.Parse(source);
        SetupAnswerSnapshot answers = DefinitionFixture.Answers("""{"service.name":"SyntheticPassword123"}""");
        foreach (DefinitionValidationResult result in new[]
        {
            DefinitionValidator.Validate(definition, answers),
            DefinitionValidator.ValidateDraft(definition, answers)
        })
        {
            await Assert.That(result.Diagnostics.Any(item => item.Code == "secret-symbol-required")).IsTrue();
            await Assert.That(result.Diagnostics.Single(item => item.Code == "secret-symbol-required").Location!.InputRevision)
                .IsEqualTo(answers.InputRevision);
            await Assert.That(string.Join("", result.Diagnostics).Contains("SyntheticPassword123", StringComparison.Ordinal)).IsFalse();
        }
        using DefinitionImportAssessment imported = DefinitionImport.Inspect(definition, "config",
            Encoding.UTF8.GetBytes("""{"name":"SyntheticPassword123"}"""));
        await Assert.That(imported.Report.Findings.Any(item => item.Code == "secret-symbol-required"
            && item.Kind == DefinitionImportFindingKind.InvalidSuppliedValue)).IsTrue();
        await Assert.That(imported.AssessAutofill(DefinitionFixture.Answers("{}")).Succeeded).IsFalse();
    }

    [Test]
    public async Task MissingSecretDefersInDraftButNullAndEnteredInvalidRemainDistinct()
    {
        string source = DefinitionFixture.Json.Replace("\"sensitivity\":\"public\",\"detail\":\"advanced\"",
            "\"secret\":{\"kind\":\"password\",\"acquisition\":\"existing\",\"constraints\":{\"minSymbols\":1}}",
            StringComparison.Ordinal);
        SetupDefinition definition = DefinitionFixture.Parse(source);
        DefinitionValidationResult missing = DefinitionValidator.ValidateDraft(definition, DefinitionFixture.Answers("{}"));
        DefinitionValidationResult suppliedNull = DefinitionValidator.ValidateDraft(definition,
            DefinitionFixture.Answers("""{"service.name":null}"""));
        await Assert.That(missing.Diagnostics.Any(item => item.Code.StartsWith("secret-", StringComparison.Ordinal))).IsFalse();
        await Assert.That(missing.Nodes.Single().IsMissing).IsTrue();
        await Assert.That(suppliedNull.Diagnostics.Any(item => item.Code == "value-invalid")).IsTrue();
        await Assert.That(suppliedNull.Diagnostics.Any(item => item.Code.StartsWith("secret-", StringComparison.Ordinal))).IsFalse();
    }
}
