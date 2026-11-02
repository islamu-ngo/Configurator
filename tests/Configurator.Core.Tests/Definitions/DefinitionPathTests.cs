namespace Configurator.Core.Tests.Definitions;

using Configurator.Core.Definitions;

public sealed class DefinitionPathTests
{
    [Test]
    public async Task StructuralKeysKeepLiteralSegmentsDistinctEvenWithHashCollisions()
    {
        string[][] segments = [[], [""], ["/"], ["~"], ["~1"], ["0"], ["00"], ["a/b"], ["a", "b"]];
        string[] pointers = ["", "/", "/~1", "/~0", "/~01", "/0", "/00", "/a~1b", "/a/b"];
        var values = new Dictionary<DefinitionPath, int>(new CollidingPathComparer());
        for (int index = 0; index < segments.Length; index++)
            values.Add(DefinitionPath.FromSegments(segments[index]), index);

        await Assert.That(values.Count).IsEqualTo(9);
        for (int index = 0; index < pointers.Length; index++)
        {
            DefinitionPath parsed = DefinitionPath.Parse(pointers[index]);
            DefinitionPath copied = DefinitionPath.FromSegments(segments[index].ToList());
            await Assert.That(values[parsed]).IsEqualTo(index);
            await Assert.That(copied.Equals(parsed)).IsTrue();
            await Assert.That(copied.GetHashCode()).IsEqualTo(parsed.GetHashCode());
            await Assert.That(copied.ToPointer()).IsEqualTo(pointers[index]);
        }
    }

    [Test]
    public async Task DictionaryIdentitySurvivesCallerCollectionMutation()
    {
        var segments = new List<string> { "a/b", "~", "0" };
        DefinitionPath key = DefinitionPath.FromSegments(segments);
        var values = new Dictionary<DefinitionPath, string> { [key] = "retained" };
        int hash = key.GetHashCode();
        segments[0] = "replacement";
        segments.Clear();

        await Assert.That(values[DefinitionPath.Parse("/a~1b/~0/0")]).IsEqualTo("retained");
        await Assert.That(key.GetHashCode()).IsEqualTo(hash);
        string[] expected = ["a/b", "~", "0"];
        await Assert.That(key.Segments.SequenceEqual(expected)).IsTrue();
    }

    [Test]
    public async Task SegmentConstructionPreservesEscapedPointerLengthLimit()
    {
        DefinitionPath boundary = DefinitionPath.FromSegments([new string('/', 32_767) + "x"]);
        await Assert.That(boundary.ToPointer().Length).IsEqualTo(65_536);
        await Assert.That(() => DefinitionPath.FromSegments([new string('/', 32_768)]))
            .Throws<DefinitionException>();
    }

    private sealed class CollidingPathComparer : IEqualityComparer<DefinitionPath>
    {
        public bool Equals(DefinitionPath? left, DefinitionPath? right) => left == right;
        public int GetHashCode(DefinitionPath path) => 0;
    }
}
