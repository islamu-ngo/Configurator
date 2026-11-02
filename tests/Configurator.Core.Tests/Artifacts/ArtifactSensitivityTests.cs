using System.Text;
using Configurator.Core;
using Configurator.Core.Dotenv;
using Configurator.Core.Environment;

namespace Configurator.Core.Tests;

public sealed class ArtifactSensitivityTests
{
    [Test]
    public async Task GeneratedEnvironmentAndUnknownKindsHaveNoPublicProjection()
    {
        const string value = "private-value-marker";
        DotenvRenderResult rendered = DotenvCodec.Render(new DotenvDocument(
            [new DotenvEntry("SERVICE_TOKEN", value, DotenvEntryKind.LocalHumanValue, true, DotenvProvenance.UserInput)]), true);
        await Assert.That(rendered.Succeeded).IsTrue();
        await Assert.That(Encoding.UTF8.GetString(rendered.Bytes.Span)).Contains(value);

        foreach (ArtifactKind kind in new[]
        {
            ArtifactKind.Environment, ArtifactKind.Configuration,
            ArtifactKind.OperatorIdentity, ArtifactKind.Unknown, (ArtifactKind)int.MaxValue
        })
        {
            await Assert.That(ArtifactPolicy.Classify(kind)).IsEqualTo(ArtifactSensitivity.Restricted);
            await Assert.That(ArtifactPolicy.TryProjectPublic(kind, rendered.Bytes, out var projection)).IsFalse();
            await Assert.That(projection.IsEmpty).IsTrue();
        }
    }

    [Test]
    public async Task OnlyClosedPublicKindsCanProjectBytes()
    {
        byte[] bytes = "public-template"u8.ToArray();
        foreach (ArtifactKind kind in new[] { ArtifactKind.PublicCatalogue, ArtifactKind.PublicTemplate })
        {
            await Assert.That(ArtifactPolicy.TryProjectPublic(kind, bytes, out var projection)).IsTrue();
            await Assert.That(projection.ToArray()).IsEquivalentTo(bytes);
        }
    }
}
