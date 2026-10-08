using System.Text;
using Configurator.Core;
using Configurator.Core.Environment;

namespace Configurator.Core.Tests;

public sealed class SetupArtifactSensitivityTests
{
    [Test]
    public async Task GeneratedEnvironmentAndUnknownKindsHaveNoPublicProjection()
    {
        const string value = "private-value-marker";
        DotenvRenderResult rendered = DotenvCodec.Render(new DotenvDocument(
            [new DotenvEntry("SERVICE_TOKEN", value, DotenvEntryKind.LocalHumanValue, true, DotenvProvenance.UserInput)]), true);
        await Assert.That(rendered.Succeeded).IsTrue();
        await Assert.That(Encoding.UTF8.GetString(rendered.Bytes.Span)).Contains(value);

        foreach (SetupArtifactKind kind in new[]
        {
            SetupArtifactKind.Environment, SetupArtifactKind.Configuration,
            SetupArtifactKind.OperatorIdentity, SetupArtifactKind.Unknown, (SetupArtifactKind)int.MaxValue
        })
        {
            await Assert.That(SetupArtifactPolicy.Classify(kind)).IsEqualTo(SetupArtifactSensitivity.Restricted);
            await Assert.That(SetupArtifactPolicy.TryProjectPublic(kind, rendered.Bytes, out var projection)).IsFalse();
            await Assert.That(projection.IsEmpty).IsTrue();
        }
    }

    [Test]
    public async Task OnlyClosedPublicKindsCanProjectBytes()
    {
        byte[] bytes = "public-template"u8.ToArray();
        foreach (SetupArtifactKind kind in new[] { SetupArtifactKind.PublicCatalogue, SetupArtifactKind.PublicTemplate })
        {
            await Assert.That(SetupArtifactPolicy.TryProjectPublic(kind, bytes, out var projection)).IsTrue();
            await Assert.That(projection.ToArray()).IsEquivalentTo(bytes);
        }
    }
}
