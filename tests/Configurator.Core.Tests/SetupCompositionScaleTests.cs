using Configurator.Core.Composition;

namespace Configurator.Core.Tests;

public sealed class SetupCompositionScaleTests
{
    [Test]
    public async Task AdmissionNeverClampsFallsBackOrAcceptsUnboundEvidence()
    {
        ArtifactDigest evidence = ArtifactDigest.Compute("neutral-profile-evidence"u8);
        var profile = new SetupCompositionScaleProfile(SetupCompositionScaleProfileId.Small,
            "small", SetupCompositionSourceKind.Json, evidence.ToString(), 100);
        var profiles = new SetupCompositionScaleProfiles([profile]);
        await Assert.That(profiles.Admit("small", evidence, 100).Profile).IsSameReferenceAs(profile);
        foreach ((string name, ArtifactDigest digest, int maximum, SetupCompositionScaleAdmissionCode expected) in new[]
        {
            ("unknown", evidence, 100, SetupCompositionScaleAdmissionCode.UnknownProfile),
            ("expanded", evidence, 100, SetupCompositionScaleAdmissionCode.ProfileDisabled),
            ("small", ArtifactDigest.Compute("different"u8), 100, SetupCompositionScaleAdmissionCode.EvidenceMismatch),
            ("small", evidence, 99, SetupCompositionScaleAdmissionCode.TargetIncompatible)
        })
        {
            SetupCompositionScaleAdmission result = profiles.Admit(name, digest, maximum);
            await Assert.That(result.Code).IsEqualTo(expected);
            await Assert.That(result.Profile).IsNull();
            await Assert.That(result.Succeeded).IsFalse();
        }
        await Assert.That(profile.EffectiveLimits).IsEqualTo(SetupCompositionLimits.Default);
    }

    [Test]
    public async Task RuntimeTelemetryContainsOnlyClosedAggregateDimensions()
    {
        var properties = typeof(SetupCompositionScaleTelemetry).GetProperties();
        await Assert.That(properties.Select(property => property.Name)).IsEquivalentTo(
            ["SourceKind", "Profile", "Outcome", "AggregateBytes", "Nodes", "Files", "DurationMicroseconds"]);
        await Assert.That(properties.All(property => property.PropertyType.IsEnum
            || property.PropertyType == typeof(int) || property.PropertyType == typeof(long))).IsTrue();
    }
}
