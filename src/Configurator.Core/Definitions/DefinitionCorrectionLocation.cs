namespace Configurator.Core.Definitions;

using System.Collections.Frozen;
using Configurator.Core.Composition;

/// <summary>Correction coordinates are meaningful only for the answer revision that produced them.</summary>
public sealed class DefinitionCorrectionLocation : IEquatable<DefinitionCorrectionLocation>
{
    public DefinitionCorrectionLocation(long inputRevision, IReadOnlyList<DefinitionLocationSegment> segments)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(inputRevision);
        ArgumentNullException.ThrowIfNull(segments);
        if (segments.Count > CompositionLimits.DefaultNestingDepth)
            throw new ArgumentOutOfRangeException(nameof(segments));
        if (segments.Any(segment => segment is null))
            throw new ArgumentException("Location segments must be present.", nameof(segments));
        InputRevision = inputRevision;
        Segments = Array.AsReadOnly(segments.ToArray());
    }

    public long InputRevision { get; }
    public IReadOnlyList<DefinitionLocationSegment> Segments { get; }
    public bool Equals(DefinitionCorrectionLocation? other) => other is not null
        && InputRevision == other.InputRevision && Segments.SequenceEqual(other.Segments);
    public override bool Equals(object? obj) => obj is DefinitionCorrectionLocation other && Equals(other);
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(InputRevision);
        foreach (DefinitionLocationSegment segment in Segments) hash.Add(segment);
        return hash.ToHashCode();
    }
    public override string ToString() => $"{nameof(DefinitionCorrectionLocation)}:Revision={InputRevision}:Count={Segments.Count}";
}
