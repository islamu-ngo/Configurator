using System.Text;
using System.Text.Json.Nodes;
using Configurator.Core.Composition;
using Configurator.Core.Definitions;

namespace Configurator.Core.Tests.Definitions;

public sealed class DefinitionBoundaryTests
{
    [Test]
    public async Task SourceByteCeilingAcceptsExactlyFourMebibytesAndRejectsOneMore()
    {
        string padded = DefinitionFixture.Json.PadRight(SetupCompositionLimits.DefaultAggregateSourceBytes, ' ');
        await Assert.That(DefinitionFixture.Parse(padded).FamilyId).IsEqualTo(DefinitionFixture.Parse().FamilyId);
        await Assert.That(() => DefinitionFixture.Parse(padded + " ")).Throws<SetupDefinitionException>();
    }

    [Test]
    public async Task InvalidShapesAndCyclicDependenciesAreRejectedBeforeSessionUse()
    {
        foreach (string change in new[]
        {
            "\"x-configurator\":{\"applicableWhen\":{\"op\":\"exists\",\"reference\":{\"scope\":\"root\",\"path\":\"/missing\"}}}",
            "\"x-configurator\":{\"applicableWhen\":{\"op\":\"equals\"}}",
            "\"x-configurator\":{\"applicableWhen\":{\"op\":\"not\",\"operands\":[]}}",
            "\"x-configurator\":{\"applicableWhen\":{\"op\":\"all\",\"operands\":[{\"op\":\"exists\",\"reference\":{\"scope\":\"root\",\"path\":\"/service.name\"}}]}}",
            "\"validator\":\"remote-schema\"",
            "\"properties\":[]",
            "\"maxItems\":5"
        })
        {
            JsonNode invalid = JsonNode.Parse(DefinitionFixture.Json)!;
            foreach ((string name, JsonNode? value) in JsonNode.Parse("{" + change + "}")!.AsObject())
                invalid["configuration"]!["properties"]!["service.name"]![name] = value?.DeepClone();
            await Assert.That(() => DefinitionFixture.Parse(invalid.ToJsonString())).Throws<SetupDefinitionException>();
        }
        JsonNode root = JsonNode.Parse(DefinitionFixture.Json)!;
        JsonNode field = root["configuration"]!["properties"]!["service.name"]!;
        field["x-configurator"]!["applicableWhen"] = JsonNode.Parse(
            """{"op":"exists","reference":{"scope":"root","path":"/second"}}""");
        root["configuration"]!["properties"]!["second"] = JsonNode.Parse("""
            {"type":"string","title":"Second","x-configurator":{"sensitivity":"public",
              "applicableWhen":{"op":"exists","reference":{"scope":"root","path":"/service.name"}}}}
            """);
        await Assert.That(() => DefinitionFixture.Parse(root.ToJsonString())).Throws<SetupDefinitionException>();
    }

    [Test]
    public async Task CanonicalNumericProfileRejectsRoundingAndNonfiniteRepresentations()
    {
        foreach (string number in new[] { "1e999", "0.00000000000000000000000000001", "12345678901234567890123456789.5" })
            await Assert.That(() => DefinitionFixture.Parse(DefinitionFixture.Json.Replace(
                "\"maxLength\":80", "\"maxLength\":80,\"default\":" + number, StringComparison.Ordinal)))
                .Throws<SetupDefinitionException>();
    }

    [Test]
    public async Task NeutralCompilerRetainsCollisionGrammarAndCancellationGuards()
    {
        var compiler = new SetupCompositionCompiler();
        foreach ((string json, SetupCompositionFailureCode code) in new[]
        {
            ("{\"a\":1,\"a\":2}", SetupCompositionFailureCode.DuplicateKey),
            ("{\"a\":1,\"A\":2}", SetupCompositionFailureCode.KeyCollision),
            ("{\"é\":1,\"e\\u0301\":2}", SetupCompositionFailureCode.KeyCollision),
            ("[]", SetupCompositionFailureCode.InvalidDocument)
        })
        {
            var result = await compiler.CompileAsync(new SetupCompositionJsonSource(Encoding.UTF8.GetBytes(json)));
            await Assert.That(result.Failure.Code).IsEqualTo(code);
            await Assert.That(result.SerializedBytes.IsEmpty).IsTrue();
        }
        foreach (string yaml in new[] { "a: &ref value\nb: *ref", "a: !tag value", "a: 1\n---\nb: 2", "a: 1,2" })
            await Assert.That((await compiler.CompileAsync(new SetupCompositionYamlSource(Encoding.UTF8.GetBytes(yaml)))).Succeeded)
                .IsFalse();
        using var cancellation = new CancellationTokenSource();
        var cancelled = await compiler.CompileAsync(new SetupCompositionJsonSource("{\"a\":1}"u8.ToArray()),
            new CancelAtCommit(cancellation), cancellation.Token);
        await Assert.That(cancelled.Failure.Code).IsEqualTo(SetupCompositionFailureCode.Cancelled);
        await Assert.That(cancelled.SerializedBytes.IsEmpty).IsTrue();
        var valid = await compiler.CompileAsync(new SetupCompositionJsonSource("{\"b\":2,\"a\":1}"u8.ToArray()));
        await Assert.That(Encoding.UTF8.GetString(valid.SerializedBytes.Span)).IsEqualTo("{\"a\":1,\"b\":2}");
    }

    private sealed class CancelAtCommit(CancellationTokenSource cancellation) : ISetupCompositionPublicationCommitBarrier
    {
        public ValueTask AwaitPublicationCommitAsync(CancellationToken cancellationToken)
        {
            cancellation.Cancel();
            return ValueTask.CompletedTask;
        }
    }
}
