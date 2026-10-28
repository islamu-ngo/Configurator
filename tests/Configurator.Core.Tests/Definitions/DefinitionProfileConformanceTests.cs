using System.Text;
using Configurator.Core.Definitions;

namespace Configurator.Core.Tests.Definitions;

public sealed class DefinitionProfileConformanceTests
{
    [Test]
    public async Task ConcretePointersEscapeOnceAndKeepOrdinalIdentity()
    {
        string[] segments = ["", "a/b", "~1", "a.b", "*", "A", "a", "\u00e9", "e\u0301", "\U0001f600"];
        DefinitionPath path = DefinitionPath.FromSegments(segments);
        segments[0] = "changed";
        const string pointer = "//a~1b/~01/a.b/*/A/a/\u00e9/e\u0301/\U0001f600";
        await Assert.That(path.ToPointer()).IsEqualTo(pointer);
        await Assert.That(path.Equals(DefinitionPath.Parse(pointer))).IsTrue();
        await Assert.That(new HashSet<DefinitionPath> { path }.Contains(DefinitionPath.Parse(pointer))).IsTrue();
        await Assert.That(DefinitionPath.Parse("").Segments.Count).IsEqualTo(0);
        await Assert.That(DefinitionPath.Parse("/").Segments.Count).IsEqualTo(1);
        await Assert.That(DefinitionPath.Parse("/A") == DefinitionPath.Parse("/a")).IsFalse();
        await Assert.That(DefinitionPath.Parse("/\u00e9") == DefinitionPath.Parse("/e\u0301")).IsFalse();
        await Assert.That(DefinitionPath.Parse("/synthetic-private-key").ToString().Contains("synthetic", StringComparison.Ordinal)).IsFalse();
    }

    [Test]
    public async Task PathsRejectMalformedUnicodeEscapesAndResourceOverflow()
    {
        foreach (string pointer in new[] { "not/a/pointer", "/~", "/~2", "/\ud800", "/\udc00",
            new string('/', 33), "/" + new string('x', 65_536) })
            await Assert.That(() => DefinitionPath.Parse(pointer)).Throws<SetupDefinitionException>();
        await Assert.That(() => DefinitionPath.FromSegments(Enumerable.Repeat("x", 33)))
            .Throws<SetupDefinitionException>();
        await Assert.That(DefinitionPath.Parse(new string('/', 32)).Segments.Count).IsEqualTo(32);
    }

    [Test]
    public async Task ArrayIndicesAreCanonicalOnlyWhenExplicitlyRequested()
    {
        DefinitionPath path = DefinitionPath.Parse("/0/12/01/-/-1/2147483648");
        await Assert.That(path.TryGetArrayIndex(0, out int zero) && zero == 0).IsTrue();
        await Assert.That(path.TryGetArrayIndex(1, out int twelve) && twelve == 12).IsTrue();
        for (int index = 2; index < 6; index++)
            await Assert.That(path.TryGetArrayIndex(index, out _)).IsFalse();
    }

    [Test]
    public async Task ExactJsonAndYamlShareIndependentCanonicalBytes()
    {
        const string json = """{"a":-0,"A":1.2300e+2,"":null,"é":"e\u0301","e\u0301":"é","slash/~":["\n","\ud83d\ude00"]}""";
        const string yaml = "\"a\": -0\n\"A\": 1.2300e+2\n\"\": null\n\"é\": \"e\\u0301\"\n\"e\\u0301\": \"é\"\n\"slash/~\": [\"\\n\", \"\\U0001F600\"]";
        const string expected = """{"":null,"A":123,"a":0,"e\u0301":"\u00E9","slash/~":["\n","\uD83D\uDE00"],"\u00E9":"e\u0301"}""";
        byte[] first = DefinitionProfile.CanonicalizeJson(Encoding.UTF8.GetBytes(json));
        await Assert.That(Encoding.UTF8.GetString(first)).IsEqualTo(expected);
        await Assert.That(DefinitionProfile.CanonicalizeYaml(Encoding.UTF8.GetBytes(yaml)).SequenceEqual(first)).IsTrue();
        await Assert.That(DefinitionProfile.CanonicalizeJson(first).SequenceEqual(first)).IsTrue();
        foreach ((string input, string canonical) in new[]
        {
            ("-0.000e-28", "0"), ("1e-28", "0.0000000000000000000000000001"),
            ("1e28", "10000000000000000000000000000"),
            ("1234567890123456789012345678", "1234567890123456789012345678")
        })
            await Assert.That(Encoding.UTF8.GetString(DefinitionProfile.CanonicalizeJson(
                Encoding.UTF8.GetBytes("{\"n\":" + input + "}")))).IsEqualTo("{\"n\":" + canonical + "}");
    }

    [Test]
    public async Task ExactAdmissionRejectsDuplicateInvalidUnicodeAndNumbersWithoutValues()
    {
        foreach (string json in new[]
        {
            """{"synthetic-private-key":1,"synthetic-private-key":2}""",
            """{"x":1,"\u0078":2}""", """{"x":"\ud800"}""", """{"\udc00":0}""",
            """{"x":1e29}""", """{"x":1e-29}""", """{"x":0.00000000000000000000000000001}""",
            """{"x":1.2345678901234567890123456789}""", """{"x":NaN}""",
            """{"x":79228162514264337593543950336}"""
        })
        {
            try
            {
                DefinitionProfile.CanonicalizeJson(Encoding.UTF8.GetBytes(json));
                throw new InvalidOperationException("Expected admission refusal.");
            }
            catch (SetupDefinitionException exception)
            {
                await Assert.That(exception.Message.Contains("synthetic", StringComparison.Ordinal)).IsFalse();
            }
        }
        await Assert.That(() => DefinitionProfile.CanonicalizeJson(new byte[] { 123, 34, 120, 34, 58, 34, 0xFF, 34, 125 }))
            .Throws<SetupDefinitionException>();
        foreach (string yaml in new[] { "\"x\": 1\n\"x\": 2", "\"x\": \"\\uD800\"", "x: &a value\ny: *a", "x: !run value", "x: 1e29" })
            await Assert.That(() => DefinitionProfile.CanonicalizeYaml(Encoding.UTF8.GetBytes(yaml)))
                .Throws<SetupDefinitionException>();
    }

    [Test]
    public async Task ExactAdmissionBoundsBytesDepthContainersScalarsAndAggregateWork()
    {
        foreach (string json in new[]
        {
            "{\"x\":\"" + new string('x', 65_537) + "\"}",
            "{\"" + new string('x', 65_537) + "\":0}",
            "{\"x\":" + new string('[', 32) + "0" + new string(']', 32) + "}",
            "{\"x\":[" + string.Join(",", Enumerable.Repeat("0", 4097)) + "]}",
            "{" + string.Join(",", Enumerable.Range(0, 4097).Select(index => "\"" + index + "\":0")) + "}",
            "{\"x\":[" + string.Join(",", Enumerable.Repeat("\"" + new string('x', 65_536) + "\"", 17)) + "]}",
            "{\"x\":[" + string.Join(",", Enumerable.Repeat("[" + string.Join(",", Enumerable.Repeat("0", 4096)) + "]", 16)) + "]}"
        })
            await Assert.That(() => DefinitionProfile.CanonicalizeJson(Encoding.UTF8.GetBytes(json)))
                .Throws<SetupDefinitionException>();
        await Assert.That(() => DefinitionProfile.CanonicalizeJson(new byte[4_194_305]))
            .Throws<SetupDefinitionException>();
        await Assert.That(() => DefinitionProfile.CanonicalizeYaml(new byte[4_194_305]))
            .Throws<SetupDefinitionException>();
    }

    [Test]
    public async Task RecursiveNodesSnapshotInputsAndProvideConsumedStructuralEquality()
    {
        var leaf = new DefinitionNode(DefinitionNodeType.Text, nullable: true);
        var properties = new Dictionary<string, DefinitionNode>(StringComparer.Ordinal) { ["A"] = leaf, ["a"] = leaf };
        var required = new List<string> { "A" };
        var first = new DefinitionNode(DefinitionNodeType.Mapping, properties: properties, required: required,
            additionalProperties: new DefinitionNode(DefinitionNodeType.Sequence, items: leaf));
        properties.Clear();
        required[0] = "a";
        var equal = new DefinitionNode(DefinitionNodeType.Mapping,
            properties: new Dictionary<string, DefinitionNode> { ["a"] = leaf, ["A"] = leaf },
            required: ["A"], additionalProperties: new DefinitionNode(DefinitionNodeType.Sequence,
                items: new DefinitionNode(DefinitionNodeType.Text, nullable: true)));
        await Assert.That(first.Properties.Count).IsEqualTo(2);
        await Assert.That(first.Required[0]).IsEqualTo("A");
        await Assert.That(first.Equals(equal)).IsTrue();
        await Assert.That(new HashSet<DefinitionNode> { first }.Contains(equal)).IsTrue();
        await Assert.That(first == new DefinitionNode(DefinitionNodeType.Mapping,
            properties: equal.Properties, required: ["a"], additionalProperties: equal.AdditionalProperties)).IsFalse();
        await Assert.That(leaf == new DefinitionNode(DefinitionNodeType.Text)).IsFalse();
        await Assert.That((leaf with { }).Equals(leaf)).IsTrue();
    }

    [Test]
    public async Task RecursiveNodesRejectInvalidShapesAndBoundExpandedTrees()
    {
        await Assert.That(() => new DefinitionNode(DefinitionNodeType.Sequence)).Throws<SetupDefinitionException>();
        await Assert.That(() => new DefinitionNode(DefinitionNodeType.Text, items: new DefinitionNode(DefinitionNodeType.Boolean)))
            .Throws<SetupDefinitionException>();
        await Assert.That(() => new DefinitionNode(DefinitionNodeType.Mapping, required: ["missing"]))
            .Throws<SetupDefinitionException>();
        await Assert.That(() => new DefinitionNode(DefinitionNodeType.Mapping,
            properties: new Dictionary<string, DefinitionNode> { ["\ud800"] = new(DefinitionNodeType.Text) }))
            .Throws<SetupDefinitionException>();
        DefinitionNode node = new(DefinitionNodeType.Text);
        for (int depth = 1; depth < 32; depth++) node = new DefinitionNode(DefinitionNodeType.Sequence, items: node);
        DefinitionNode deepest = node;
        await Assert.That(() => new DefinitionNode(DefinitionNodeType.Sequence, items: deepest)).Throws<SetupDefinitionException>();
        DefinitionNode repeated = new(DefinitionNodeType.Text);
        for (int depth = 0; depth < 4; depth++)
            repeated = new DefinitionNode(DefinitionNodeType.Mapping,
                properties: Enumerable.Range(0, 10).Select(index => KeyValuePair.Create(index.ToString(System.Globalization.CultureInfo.InvariantCulture), repeated)));
        DefinitionNode expanded = repeated;
        await Assert.That(() => new DefinitionNode(DefinitionNodeType.Mapping,
            properties: Enumerable.Range(0, 10).Select(index => KeyValuePair.Create(index.ToString(System.Globalization.CultureInfo.InvariantCulture), expanded))))
            .Throws<SetupDefinitionException>();
    }
}
