namespace Configurator.Core.Definitions.Authoring;

/// <summary>
/// Value-safe authoring diagnostic containing only a closed error code, optional known schema path,
/// numeric source coordinates, and optional schema span. Never echoes raw text, unknown keys, or values.
/// </summary>
public sealed record DefinitionAuthoringDiagnostic
{
    public DefinitionAuthoringDiagnostic(
        string code,
        string? schemaPath = null,
        int? line = null,
        int? column = null,
        long? byteOffset = null,
        DefinitionSourceSpan? sourceSpan = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        if (line is { } l) ArgumentOutOfRangeException.ThrowIfLessThan(l, 1);
        if (column is { } c) ArgumentOutOfRangeException.ThrowIfLessThan(c, 1);
        if (byteOffset is { } b) ArgumentOutOfRangeException.ThrowIfNegative(b);

        Code = code;
        SchemaPath = schemaPath;
        Line = line;
        Column = column;
        ByteOffset = byteOffset;
        SourceSpan = sourceSpan;
    }

    public string Code { get; }
    public string? SchemaPath { get; }
    public int? Line { get; }
    public int? Column { get; }
    public long? ByteOffset { get; }
    public DefinitionSourceSpan? SourceSpan { get; }

    public static DefinitionAuthoringDiagnostic FromException(DefinitionException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        int? line = exception.Line ?? exception.SourceSpan?.StartLine;
        int? column = exception.Column ?? exception.SourceSpan?.StartColumn;
        return new DefinitionAuthoringDiagnostic(
            exception.Code,
            exception.SchemaPath,
            line,
            column,
            exception.ByteOffset,
            exception.SourceSpan);
    }
}
