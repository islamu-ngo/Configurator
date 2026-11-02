namespace Configurator.Core.Definitions;

public sealed class DefinitionException : Exception
{
    internal DefinitionException(string code) : base(code) => Code = code;
    public string Code { get; }
    public string? SchemaPath { get; private init; }
    public DefinitionSourceSpan? SourceSpan { get; private init; }

    internal DefinitionException AtSchemaPath(string knownSchemaPath) => SchemaPath is not null ? this
        : new DefinitionException(Code) { SchemaPath = knownSchemaPath };

    internal DefinitionException WithLocation(DefinitionSourceMap map, string knownSchemaPath)
    {
        string path = SchemaPath ?? knownSchemaPath;
        while (true)
        {
            if (map.TryGetSpan(path, out DefinitionSourceSpan span))
                return new DefinitionException(Code) { SchemaPath = path, SourceSpan = span };
            int separator = path.LastIndexOf('/');
            if (separator < 0) return new DefinitionException(Code);
            path = path[..separator];
        }
    }
}
