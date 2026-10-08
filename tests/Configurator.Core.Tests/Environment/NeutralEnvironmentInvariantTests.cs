using System.Text;
using Configurator.Core.Environment;

namespace Configurator.Core.Tests.EnvironmentTests;

public sealed class NeutralEnvironmentInvariantTests
{
    [Test]
    public async Task OriginalDialectRejectionVectorsStillExerciseTheProductionParser()
    {
        foreach (DotenvRejectionFixture fixture in EnvironmentContractExpectedVectors.DotenvRejections)
        {
            DotenvParseResult result = DotenvCodec.Parse(Encoding.UTF8.GetBytes(fixture.Text));
            await Assert.That(result.Diagnostics.Select(item => item.Code)).Contains(fixture.ExpectedCode);
            await Assert.That(result.Document).IsNull();
            await Assert.That(string.Join("", result.Diagnostics).Contains(fixture.Text, StringComparison.Ordinal)).IsFalse();
        }
        await Assert.That(DotenvCodec.Parse(new byte[] { 0x4b, 0x3d, 0xff }).Diagnostics.Select(item => item.Code))
            .Contains("dotenv-utf8-invalid");
    }

    [Test]
    public async Task QuotingEscapingUnicodeAndCommentsRoundTripToPinnedBytes()
    {
        const string expected = "HASH_TEXT=\"value # text\"\nQUOTE_TEXT=\"a\\\"b\\\\c\"\nUNICODE_NAME=\"سلام\"\n";
        var parsed = DotenvCodec.Parse(Encoding.UTF8.GetBytes(
            "# ignored\nUNICODE_NAME=\"سلام\"\nHASH_TEXT=\"value # text\"\nQUOTE_TEXT=\"a\\\"b\\\\c\"\n"));
        var rendered = DotenvCodec.Render(parsed.Document!, true);
        await Assert.That(rendered.Succeeded).IsTrue();
        await Assert.That(Encoding.UTF8.GetString(rendered.Bytes.Span)).IsEqualTo(expected);
        var second = DotenvCodec.Render(DotenvCodec.Parse(rendered.Bytes).Document!, true);
        await Assert.That(second.Bytes.ToArray()).IsEquivalentTo(rendered.Bytes.ToArray());
    }

    [Test]
    public async Task FileLineKeyValueAndEntryCountBoundsRemainEnforced()
    {
        foreach ((string input, string code) in new[]
        {
            ("SAFE_KEY=" + new string('a', DotenvCodec.MaximumFileUtf8Bytes) + "\n", "dotenv-file-too-large"),
            ("SAFE_KEY=" + new string('a', DotenvCodec.MaximumLineUtf8Bytes) + "\n", "dotenv-line-too-large"),
            (new string('A', DotenvCodec.MaximumKeyCharacters + 1) + "=\n", "dotenv-key-invalid"),
            ("SAFE_KEY=" + new string('a', DotenvCodec.MaximumValueUtf8Bytes + 1) + "\n", "dotenv-value-too-large"),
            (string.Concat(Enumerable.Range(0, DotenvCodec.MaximumEntryCount + 1).Select(i => $"KEY_{i}=\n")), "dotenv-count-exceeded")
        })
            await Assert.That(DotenvCodec.Parse(Encoding.UTF8.GetBytes(input)).Diagnostics.Select(item => item.Code)).Contains(code);
    }

    [Test]
    public async Task RendererRejectsDuplicateInvalidUnicodeAndInvalidProvenance()
    {
        foreach (DotenvDocument document in new[]
        {
            new DotenvDocument([Entry("PUBLIC_REQUIRED", "a"), Entry("PUBLIC_REQUIRED", "b")]),
            new DotenvDocument([Entry("PUBLIC_REQUIRED", "\uD800")]),
            new DotenvDocument([null!]),
            new DotenvDocument([new("PUBLIC_REQUIRED", "a", (DotenvEntryKind)999, false, DotenvProvenance.UserInput)])
        })
        {
            var result = DotenvCodec.Render(document, true);
            await Assert.That(result.Succeeded).IsFalse();
            await Assert.That(result.Bytes.IsEmpty).IsTrue();
        }
    }

    [Test]
    public async Task SecretCompositionCannotDemoteProtectedInputOrSelectDuplicates()
    {
        EnvironmentCatalogue catalogue = Catalogue();
        var context = new EnvironmentActivationContext("single", ["service"], []);
        DotenvEntry[] inputs = [Entry("PUBLIC_REQUIRED", "public"), Entry("SERVICE_TOKEN", "private")];
        var noSecrets = DotenvComposer.ComposeNoSecrets(catalogue, context, inputs);
        await Assert.That(noSecrets.Readiness.State).IsEqualTo(DotenvReadinessState.Blocked);
        await Assert.That(noSecrets.Document.Entries.Single(item => item.Key == "SERVICE_TOKEN").Value).IsNull();
        var withSecrets = DotenvComposer.ComposeWithSecrets(catalogue, context, inputs);
        await Assert.That(withSecrets.Readiness.State).IsEqualTo(DotenvReadinessState.Ready);
        await Assert.That(withSecrets.Document.Entries.Single(item => item.Key == "SERVICE_TOKEN").IsSecret).IsTrue();
        var duplicate = DotenvComposer.ComposeWithSecrets(catalogue, context, inputs.Append(Entry("SERVICE_TOKEN", "second")));
        await Assert.That(duplicate.Readiness.State).IsEqualTo(DotenvReadinessState.Blocked);
        await Assert.That(duplicate.Document.Entries.Single(item => item.Key == "SERVICE_TOKEN").Value).IsNull();
        var parsed = DotenvCodec.Parse("SERVICE_TOKEN=unclassified\n"u8.ToArray());
        await Assert.That(DotenvReadiness.Evaluate(catalogue, context, parsed.Document!).State)
            .IsEqualTo(DotenvReadinessState.Blocked);
    }

    [Test]
    public async Task CatalogueStillRejectsCyclesUnknownActivationAndSecretDefaultDemotion()
    {
        var cycle = new EnvironmentActivationGraph(["single"], ["service"], [],
            [new("loop", EnvironmentActivationExpression.Feature("loop"))]);
        await Assert.That(EnvironmentCatalogue.Create(Catalogue().Definitions, cycle, ["SERVICE_TOKEN"]).Catalogue).IsNull();
        var unknown = new EnvironmentActivationGraph(["single"], [], [], []);
        await Assert.That(EnvironmentCatalogue.Create(Catalogue().Definitions, unknown, ["SERVICE_TOKEN"]).Catalogue).IsNull();
        await Assert.That(EnvironmentCatalogue.Create(Catalogue().Definitions,
            new(["single"], ["service"], [], []), []).Catalogue).IsNull();
        await Assert.That(Catalogue().Lookup("SERVICE_TOKEN__0")).IsNull();
    }

    private static DotenvEntry Entry(string key, string value) =>
        new(key, value, DotenvEntryKind.LocalHumanValue, false, DotenvProvenance.UserInput);

    private static EnvironmentCatalogue Catalogue()
    {
        var graph = new EnvironmentActivationGraph(["single"], ["service"], [], []);
        var active = EnvironmentActivationExpression.Capability("service");
        var documentation = new EnvironmentDocumentationMetadata("title", "help", "service");
        return EnvironmentCatalogue.Create(
        [
            new("PUBLIC_REQUIRED", EnvironmentVariableCategory.Platform, EnvironmentVariableSensitivity.Public,
                EnvironmentVariableRequirement.Required, null, 1, active, "text",
                new(EnvironmentGenerationSurface.Dotenv, 1, null, false), EnvironmentRestartBehavior.Process, documentation),
            new("SERVICE_TOKEN", EnvironmentVariableCategory.Security, EnvironmentVariableSensitivity.Secret,
                EnvironmentVariableRequirement.Required, null, 2, active, "text",
                new(EnvironmentGenerationSurface.Dotenv, 2, null, false), EnvironmentRestartBehavior.Process, documentation)
        ], graph, ["SERVICE_TOKEN"]).Catalogue!;
    }
}
