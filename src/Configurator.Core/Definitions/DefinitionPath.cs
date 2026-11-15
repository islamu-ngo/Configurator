namespace Configurator.Core.Definitions;

using System.Globalization;
using Configurator.Core.Composition;

/// <summary>A bounded, ordinal JSON Pointer. Diagnostic text deliberately omits its potentially private tokens.</summary>
public sealed record DefinitionPath
{
    private static readonly DefinitionPath s_empty = new("", []);

    private readonly string? _jsonPointer;
    private readonly int _hashCode;

    private DefinitionPath(string? jsonPointer, string[] segments)
    {
        _jsonPointer = jsonPointer;
        Segments = Array.AsReadOnly(segments);
        var hash = new HashCode();
        foreach (string segment in segments) hash.Add(segment, StringComparer.Ordinal);
        _hashCode = hash.ToHashCode();
    }

    public static DefinitionPath Parse(string jsonPointer)
    {
        ArgumentNullException.ThrowIfNull(jsonPointer);
        DefinitionProfile.ValidateScalar(jsonPointer);
        if (jsonPointer.Length == 0) return s_empty;
        if (jsonPointer[0] != '/') throw new DefinitionException("path-invalid");
        string[] segments = jsonPointer[1..].Split('/');
        if (segments.Length > CompositionLimits.DefaultNestingDepth)
            throw new DefinitionException("path-limit");
        for (int index = 0; index < segments.Length; index++)
        {
            string token = segments[index];
            for (int position = 0; position < token.Length; position++)
                if (token[position] == '~' && (++position >= token.Length || token[position] is not ('0' or '1')))
                    throw new DefinitionException("path-invalid");
            segments[index] = token.Replace("~1", "/", StringComparison.Ordinal)
                .Replace("~0", "~", StringComparison.Ordinal);
        }
        return new(jsonPointer, segments);
    }

    public static DefinitionPath FromSegments(IEnumerable<string> segments)
    {
        ArgumentNullException.ThrowIfNull(segments);
        if (segments is IReadOnlyList<string> list)
        {
            if (list.Count == 0) return s_empty;
            if (list.Count > CompositionLimits.DefaultNestingDepth)
                throw new DefinitionException("path-limit");
            string[] copy = new string[list.Count];
            int totalCharacters = 0;
            for (int index = 0; index < list.Count; index++)
            {
                string segment = list[index];
                ArgumentNullException.ThrowIfNull(segment);
                DefinitionProfile.ValidateScalar(segment);
                totalCharacters += segment.Length + 1;
                foreach (char character in segment)
                    if (character is '~' or '/') totalCharacters++;
                if (totalCharacters > CompositionLimits.DefaultScalarCharacters)
                    throw new DefinitionException("path-limit");
                copy[index] = segment;
            }
            return new(null, copy);
        }

        var snapshot = new List<string>();
        int characters = 0;
        foreach (string segment in segments)
        {
            if (snapshot.Count == CompositionLimits.DefaultNestingDepth)
                throw new DefinitionException("path-limit");
            ArgumentNullException.ThrowIfNull(segment);
            DefinitionProfile.ValidateScalar(segment);
            characters += segment.Length + 1;
            foreach (char character in segment)
                if (character is '~' or '/') characters++;
            if (characters > CompositionLimits.DefaultScalarCharacters)
                throw new DefinitionException("path-limit");
            snapshot.Add(segment);
        }
        return snapshot.Count == 0 ? s_empty : new(null, snapshot.ToArray());
    }

    public IReadOnlyList<string> Segments { get; }
    public string ToPointer()
    {
        if (_jsonPointer is not null) return _jsonPointer;
        if (Segments.Count == 0) return "";
        if (Segments.Count == 1 && !Segments[0].Contains('~') && !Segments[0].Contains('/'))
            return "/" + Segments[0];
        return "/" + string.Join("/", Segments.Select(segment => segment.Replace("~", "~0", StringComparison.Ordinal)
            .Replace("/", "~1", StringComparison.Ordinal)));
    }

    public bool TryGetArrayIndex(int segment, out int index)
    {
        index = 0;
        if (segment < 0 || segment >= Segments.Count) return false;
        string token = Segments[segment];
        return token.Length > 0 && (token.Length == 1 || token[0] != '0')
            && token.All(char.IsAsciiDigit)
            && int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out index);
    }

    public bool Equals(DefinitionPath? other)
    {
        if (ReferenceEquals(this, other)) return true;
        if (other is null || _hashCode != other._hashCode || Segments.Count != other.Segments.Count) return false;
        for (int index = 0; index < Segments.Count; index++)
            if (!string.Equals(Segments[index], other.Segments[index], StringComparison.Ordinal))
                return false;
        return true;
    }
    public override int GetHashCode() => _hashCode;
    public override string ToString() => nameof(DefinitionPath);
}
