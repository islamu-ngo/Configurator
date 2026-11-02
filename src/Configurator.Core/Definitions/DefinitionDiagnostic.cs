namespace Configurator.Core.Definitions;

public sealed record DefinitionDiagnostic(string Code, string FieldId)
{
    public DefinitionDiagnostic(string code, string fieldId, DefinitionCorrectionLocation location)
        : this(code, fieldId, location.Segments.SelectMany(segment =>
            segment.Kind == DefinitionLocationSegmentKind.MapEntry
                ? new[] { "@entry", segment.Index!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) }
                : new[] { segment.PropertyName
                    ?? segment.Index!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) }).ToArray())
    {
        Location = location;
    }

    public DefinitionDiagnostic(string code, string fieldId, IReadOnlyList<string> relativePath)
        : this(code, fieldId)
    {
        ArgumentNullException.ThrowIfNull(relativePath);
        RelativePath = Array.AsReadOnly(relativePath.ToArray());
    }

    public IReadOnlyList<string> RelativePath { get; } = Array.Empty<string>();
    public DefinitionCorrectionLocation? Location { get; }
    public bool Equals(DefinitionDiagnostic? other) => other is not null &&
        Code == other.Code && FieldId == other.FieldId && RelativePath.SequenceEqual(other.RelativePath)
        && Equals(Location, other.Location);
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Code, StringComparer.Ordinal);
        hash.Add(FieldId, StringComparer.Ordinal);
        foreach (string segment in RelativePath) hash.Add(segment, StringComparer.Ordinal);
        hash.Add(Location);
        return hash.ToHashCode();
    }
    public override string ToString() => $"{Code}:{FieldId}";
}
