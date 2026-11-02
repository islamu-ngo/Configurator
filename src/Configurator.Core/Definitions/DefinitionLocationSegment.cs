namespace Configurator.Core.Definitions;

using System.Collections.Frozen;
using Configurator.Core.Composition;

/// <summary>A declared property or numeric position; dynamic map keys never enter this model.</summary>
public sealed record DefinitionLocationSegment
{
    private DefinitionLocationSegment(DefinitionLocationSegmentKind kind, string? propertyName, int? index)
    {
        Kind = kind;
        PropertyName = propertyName;
        Index = index;
    }

    public DefinitionLocationSegmentKind Kind { get; }
    public string? PropertyName { get; }
    public int? Index { get; }

    public static DefinitionLocationSegment Property(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        DefinitionProfile.ValidateScalar(name);
        return new(DefinitionLocationSegmentKind.Property, name, null);
    }

    public static DefinitionLocationSegment ArrayRow(int index) => Indexed(DefinitionLocationSegmentKind.ArrayRow, index);
    public static DefinitionLocationSegment MapEntry(int index) => Indexed(DefinitionLocationSegmentKind.MapEntry, index);

    private static DefinitionLocationSegment Indexed(DefinitionLocationSegmentKind kind, int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, kind == DefinitionLocationSegmentKind.MapEntry
            ? CompositionLimits.DefaultMappingEntries : CompositionLimits.DefaultSequenceEntries);
        return new(kind, null, index);
    }

    public override string ToString() => Kind.ToString();
}
