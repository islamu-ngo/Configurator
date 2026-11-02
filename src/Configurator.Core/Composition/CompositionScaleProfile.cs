namespace Configurator.Core.Composition;

using Configurator.Core;

public sealed class CompositionScaleProfile
{
    public CompositionScaleProfile(
        CompositionScaleProfileId id,
        string name,
        CompositionSourceKind sourceKind,
        string evidenceDigest,
        int serializedArtifactBytes)
    {
        if (!Enum.IsDefined(id) || !Enum.IsDefined(sourceKind))
            throw new ArgumentOutOfRangeException(nameof(id));
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(serializedArtifactBytes);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            serializedArtifactBytes, CompositionLimits.DefaultAggregateSourceBytes);
        Id = id;
        Name = name;
        SourceKind = sourceKind;
        EvidenceDigest = ArtifactDigest.Parse(evidenceDigest);
        SerializedArtifactBytes = serializedArtifactBytes;
    }

    public CompositionScaleProfileId Id { get; }
    public string Name { get; }
    public CompositionSourceKind SourceKind { get; }
    public ArtifactDigest EvidenceDigest { get; }
    public int SerializedArtifactBytes { get; }
    public CompositionLimits EffectiveLimits => CompositionLimits.Default;

    public override string ToString() =>
        $"{nameof(CompositionScaleProfile)}:{Name}:{SourceKind}";
}
