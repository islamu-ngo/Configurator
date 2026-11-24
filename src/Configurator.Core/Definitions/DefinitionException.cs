namespace Configurator.Core.Definitions;

using Configurator.Core.Composition;
using Configurator.Core.Definitions.Authoring;

public sealed class DefinitionException : Exception
{
    internal DefinitionException(string code) : base(code) => Code = code;
    public string Code { get; }
    public string? SchemaPath { get; private init; }
    public DefinitionSourceSpan? SourceSpan { get; private init; }
    public int? Line { get; private init; }
    public int? Column { get; private init; }
    public long? ByteOffset { get; private init; }

    public static DefinitionException RegistryLimit() => new("registry-limit");

    public static DefinitionException RevisionConflict() => new("revision-conflict");

    public DefinitionAuthoringDiagnostic ToAuthoringDiagnostic() =>
        DefinitionAuthoringDiagnostic.FromException(this);

    internal static DefinitionException AtLocation(string code, CompositionSourceLocation? location) =>
        location is { } loc
            ? new DefinitionException(code) { Line = loc.Line, Column = loc.Column, ByteOffset = loc.ByteOffset }
            : new DefinitionException(code);

    internal DefinitionException AtSchemaPath(string knownSchemaPath) => SchemaPath is not null ? this
        : new DefinitionException(Code)
        {
            SchemaPath = knownSchemaPath,
            SourceSpan = SourceSpan,
            Line = Line,
            Column = Column,
            ByteOffset = ByteOffset
        };

    internal DefinitionException WithLocation(DefinitionSourceMap map, string knownSchemaPath)
    {
        string path = SchemaPath ?? knownSchemaPath;
        while (true)
        {
            if (map.TryGetSpan(path, out DefinitionSourceSpan span))
                return new DefinitionException(Code)
                {
                    SchemaPath = path,
                    SourceSpan = span,
                    Line = Line ?? span.StartLine,
                    Column = Column ?? span.StartColumn,
                    ByteOffset = ByteOffset
                };
            int separator = path.LastIndexOf('/');
            if (separator < 0)
                return new DefinitionException(Code)
                {
                    Line = Line,
                    Column = Column,
                    ByteOffset = ByteOffset
                };
            path = path[..separator];
        }
    }
}
