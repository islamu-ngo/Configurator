namespace Configurator.Core.Definitions;

using System.Globalization;
using Configurator.Core.Composition;

/// <summary>A bounded, ordinal JSON Pointer. Diagnostic text deliberately omits its potentially private tokens.</summary>
public sealed record DefinitionPath
{
    private readonly string _jsonPointer;

    private DefinitionPath(string jsonPointer, string[] segments)
    {
        _jsonPointer = jsonPointer;
        Segments = Array.AsReadOnly(segments);
    }

    public static DefinitionPath Parse(string jsonPointer)
    {
        ArgumentNullException.ThrowIfNull(jsonPointer);
        DefinitionProfile.ValidateScalar(jsonPointer);
        if (jsonPointer.Length == 0) return new("", []);
        if (jsonPointer[0] != '/') throw new SetupDefinitionException("path-invalid");
        string[] segments = jsonPointer[1..].Split('/');
        if (segments.Length > SetupCompositionLimits.DefaultNestingDepth)
            throw new SetupDefinitionException("path-limit");
        for (int index = 0; index < segments.Length; index++)
        {
            string token = segments[index];
            for (int position = 0; position < token.Length; position++)
                if (token[position] == '~' && (++position >= token.Length || token[position] is not ('0' or '1')))
                    throw new SetupDefinitionException("path-invalid");
            segments[index] = token.Replace("~1", "/", StringComparison.Ordinal)
                .Replace("~0", "~", StringComparison.Ordinal);
        }
        return new(jsonPointer, segments);
    }

    public static DefinitionPath FromSegments(IEnumerable<string> segments)
    {
        ArgumentNullException.ThrowIfNull(segments);
        var snapshot = new List<string>();
        int characters = 0;
        foreach (string segment in segments)
        {
            if (snapshot.Count == SetupCompositionLimits.DefaultNestingDepth)
                throw new SetupDefinitionException("path-limit");
            ArgumentNullException.ThrowIfNull(segment);
            DefinitionProfile.ValidateScalar(segment);
            string escaped = segment.Replace("~", "~0", StringComparison.Ordinal)
                .Replace("/", "~1", StringComparison.Ordinal);
            characters += escaped.Length + 1;
            if (characters > SetupCompositionLimits.DefaultScalarCharacters)
                throw new SetupDefinitionException("path-limit");
            snapshot.Add(escaped);
        }
        return Parse(snapshot.Count == 0 ? "" : "/" + string.Join("/", snapshot));
    }

    public IReadOnlyList<string> Segments { get; }
    public string ToPointer() => _jsonPointer;

    public bool TryGetArrayIndex(int segment, out int index)
    {
        index = 0;
        if (segment < 0 || segment >= Segments.Count) return false;
        string token = Segments[segment];
        return token.Length > 0 && (token.Length == 1 || token[0] != '0')
            && token.All(char.IsAsciiDigit)
            && int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out index);
    }

    public bool Equals(DefinitionPath? other) =>
        other is not null && StringComparer.Ordinal.Equals(_jsonPointer, other._jsonPointer);
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(_jsonPointer);
    public override string ToString() => nameof(DefinitionPath);
}
