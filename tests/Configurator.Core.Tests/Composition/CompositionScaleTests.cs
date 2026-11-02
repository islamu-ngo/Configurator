using Configurator.Core.Composition;

namespace Configurator.Core.Tests;

public sealed class CompositionScaleTests
{
    [Test]
    public async Task AdmissionNeverClampsFallsBackOrAcceptsUnboundEvidence()
    {
        ArtifactDigest evidence = ArtifactDigest.Compute("neutral-profile-evidence"u8);
        var profile = new CompositionScaleProfile(CompositionScaleProfileId.Small,
            "small", CompositionSourceKind.Json, evidence.ToString(), 100);
        var profiles = new CompositionScaleProfiles([profile]);
        await Assert.That(profiles.Admit("small", evidence, 100).Profile).IsSameReferenceAs(profile);
        foreach ((string name, ArtifactDigest digest, int maximum, CompositionScaleAdmissionCode expected) in new[]
        {
            ("unknown", evidence, 100, CompositionScaleAdmissionCode.UnknownProfile),
            ("expanded", evidence, 100, CompositionScaleAdmissionCode.ProfileDisabled),
            ("small", ArtifactDigest.Compute("different"u8), 100, CompositionScaleAdmissionCode.EvidenceMismatch),
            ("small", evidence, 99, CompositionScaleAdmissionCode.TargetIncompatible)
        })
        {
            CompositionScaleAdmission result = profiles.Admit(name, digest, maximum);
            await Assert.That(result.Code).IsEqualTo(expected);
            await Assert.That(result.Profile).IsNull();
            await Assert.That(result.Succeeded).IsFalse();
        }
        await Assert.That(profile.EffectiveLimits).IsEqualTo(CompositionLimits.Default);
    }

    [Test]
    public async Task RuntimeTelemetryContainsOnlyClosedAggregateDimensions()
    {
        var properties = typeof(CompositionScaleTelemetry).GetProperties();
        await Assert.That(properties.Select(property => property.Name)).IsEquivalentTo(
            ["SourceKind", "Profile", "Outcome", "AggregateBytes", "Nodes", "Files", "DurationMicroseconds"]);
        await Assert.That(properties.All(property => property.PropertyType.IsEnum
            || property.PropertyType == typeof(int) || property.PropertyType == typeof(long))).IsTrue();
    }
}
