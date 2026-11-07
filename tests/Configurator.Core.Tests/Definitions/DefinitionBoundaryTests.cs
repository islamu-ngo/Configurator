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
        string padded = DefinitionFixture.Json.PadRight(CompositionLimits.DefaultAggregateSourceBytes, ' ');
        await Assert.That(DefinitionFixture.Parse(padded).FamilyId).IsEqualTo(DefinitionFixture.Parse().FamilyId);
        await Assert.That(() => DefinitionFixture.Parse(padded + " ")).Throws<DefinitionException>();
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
            await Assert.That(() => DefinitionFixture.Parse(invalid.ToJsonString())).Throws<DefinitionException>();
        }
        JsonNode root = JsonNode.Parse(DefinitionFixture.Json)!;
        JsonNode field = root["configuration"]!["properties"]!["service.name"]!;
        field["x-configurator"]!["applicableWhen"] = JsonNode.Parse(
            """{"op":"exists","reference":{"scope":"root","path":"/second"}}""");
        root["configuration"]!["properties"]!["second"] = JsonNode.Parse("""
            {"type":"string","title":"Second","x-configurator":{"sensitivity":"public",
              "applicableWhen":{"op":"exists","reference":{"scope":"root","path":"/service.name"}}}}
            """);
        await Assert.That(() => DefinitionFixture.Parse(root.ToJsonString())).Throws<DefinitionException>();
    }

    [Test]
    public async Task CanonicalNumericProfileRejectsRoundingAndNonfiniteRepresentations()
    {
        foreach (string number in new[] { "1e999", "0.00000000000000000000000000001", "12345678901234567890123456789.5" })
            await Assert.That(() => DefinitionFixture.Parse(DefinitionFixture.Json.Replace(
                "\"maxLength\":80", "\"maxLength\":80,\"default\":" + number, StringComparison.Ordinal)))
                .Throws<DefinitionException>();
    }

    [Test]
    public async Task NeutralCompilerRetainsCollisionGrammarAndCancellationGuards()
    {
        var compiler = new CompositionCompiler();
        foreach ((string json, CompositionFailureCode code) in new[]
        {
            ("{\"a\":1,\"a\":2}", CompositionFailureCode.DuplicateKey),
            ("{\"a\":1,\"A\":2}", CompositionFailureCode.KeyCollision),
            ("{\"é\":1,\"e\\u0301\":2}", CompositionFailureCode.KeyCollision),
            ("[]", CompositionFailureCode.InvalidDocument)
        })
        {
            var result = await compiler.CompileAsync(new CompositionJsonSource(Encoding.UTF8.GetBytes(json)));
            await Assert.That(result.Failure.Code).IsEqualTo(code);
            await Assert.That(result.SerializedBytes.IsEmpty).IsTrue();
        }
        foreach (string yaml in new[] { "a: &ref value\nb: *ref", "a: !tag value", "a: 1\n---\nb: 2", "a: 1,2" })
            await Assert.That((await compiler.CompileAsync(new CompositionYamlSource(Encoding.UTF8.GetBytes(yaml)))).Succeeded)
                .IsFalse();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var cancelled = await compiler.CompileAsync(new CompositionJsonSource("{\"a\":1}"u8.ToArray()),
            cancellation.Token);
        await Assert.That(cancelled.Failure.Code).IsEqualTo(CompositionFailureCode.Cancelled);
        await Assert.That(cancelled.SerializedBytes.IsEmpty).IsTrue();
        var valid = await compiler.CompileAsync(new CompositionJsonSource("{\"b\":2,\"a\":1}"u8.ToArray()));
        await Assert.That(Encoding.UTF8.GetString(valid.SerializedBytes.Span)).IsEqualTo("{\"a\":1,\"b\":2}");
    }

    [Test]
    [Arguments("bytes")]
    [Arguments("nodes")]
    [Arguments("scalars")]
    [Arguments("merge")]
    public async Task FragmentCeilingsAreAggregateRatherThanPerSource(string ceiling)
    {
        var accepted = new List<CompositionSource>();
        var rejected = new List<CompositionSource>();
        switch (ceiling)
        {
            case "bytes":
                string half = "{}".PadRight(2097152);
                accepted.Add(Json(half));
                accepted.Add(Json(half));
                rejected.Add(Json(half));
                rejected.Add(Json(half + " "));
                break;
            case "nodes":
                for (int group = 0; group < 16; group++)
                {
                    string prefix = "{\"group" + group + "\":[";
                    string entries = string.Join(',', Enumerable.Repeat("null", 4094));
                    accepted.Add(Json(prefix + entries + "]}"));
                    rejected.Add(Json(prefix + entries + (group == 15 ? ",null" : "") + "]}"));
                }
                break;
            case "scalars":
                for (int group = 0; group < 16; group++)
                {
                    CompositionSource fragment = Json("{\"group" + group + "\":\"" + new string('x', 65536) + "\"}");
                    accepted.Add(fragment);
                    rejected.Add(fragment);
                }
                rejected.Add(Json("{\"extra\":\"x\"}"));
                break;
            case "merge":
                for (int group = 0; group < 2; group++)
                {
                    string members = string.Join(',', Enumerable.Range(group * 2048, 2048)
                        .Select(index => "\"key" + index + "\":null"));
                    accepted.Add(Json("{" + members + "}"));
                    rejected.Add(Json("{" + members + (group == 1 ? ",\"extra\":null" : "") + "}"));
                }
                break;
        }
        var compiler = new CompositionCompiler();
        CompositionResult success = await compiler.CompileAsync(accepted);
        CompositionResult failure = await compiler.CompileAsync(rejected);
        await Assert.That(success.Succeeded).IsTrue();
        await Assert.That(failure.Failure.Code).IsEqualTo(CompositionFailureCode.LimitExceeded);
        await Assert.That(failure.SerializedBytes.IsEmpty).IsTrue();
    }

    [Test]
    public async Task FragmentYamlEventBudgetsAreIndependentAndDoNotResetAtJson()
    {
        var sources = new List<CompositionSource>();
        for (int group = 0; group < 16; group++)
        {
            var yaml = new StringBuilder("group" + group + ":\n");
            for (int entry = 0; entry < 2727; entry++)
                yaml.Append("  item").Append(entry).Append(": {}\n");
            yaml.Append("  last: value\n");
            sources.Add(new CompositionYamlSource(Encoding.UTF8.GetBytes(yaml.ToString())));
            sources.Add(Json("{}"));
        }
        var compiler = new CompositionCompiler();
        CompositionResult[] results = await Task.WhenAll(Enumerable.Range(0, 2)
            .Select(_ => Task.Run(async () => await compiler.CompileAsync(sources))));
        foreach (CompositionResult result in results)
            await Assert.That(result.Succeeded).IsTrue();
        sources.Add(new CompositionYamlSource("extra: value"u8.ToArray()));
        CompositionResult rejected = await compiler.CompileAsync(sources);
        await Assert.That(rejected.Failure.Code).IsEqualTo(CompositionFailureCode.LimitExceeded);
        await Assert.That(rejected.SerializedBytes.IsEmpty).IsTrue();
        await Assert.That((await compiler.CompileAsync(Json("{\"fresh\":1}"))).Succeeded).IsTrue();
    }

    [Test]
    public async Task OrderedMixedFragmentsMergeWithLiteralDeterministicOutput()
    {
        CompositionResult result = await new CompositionCompiler().CompileAsync(
        [
            new CompositionYamlSource("nested:\n  first: value\n"u8.ToArray()),
            Json("{\"nested\":{\"second\":2},\"z\":true}")
        ]);
        await Assert.That(result.Succeeded).IsTrue();
        await Assert.That(Encoding.UTF8.GetString(result.SerializedBytes.Span))
            .IsEqualTo("{\"nested\":{\"first\":\"value\",\"second\":2},\"z\":true}");
        CompositionResult collision = await new CompositionCompiler().CompileAsync(
            [Json("{\"a\":1}"), Json("{\"a\":2}")]);
        await Assert.That(collision.Failure.Code).IsEqualTo(CompositionFailureCode.SourceConflict);
        await Assert.That(collision.SerializedBytes.IsEmpty).IsTrue();
    }

    private static CompositionJsonSource Json(string text) => new(Encoding.UTF8.GetBytes(text));

}
