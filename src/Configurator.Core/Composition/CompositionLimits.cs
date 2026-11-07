namespace Configurator.Core.Composition;

public sealed record CompositionLimits
{
    public const int DefaultAggregateSourceBytes = 4_194_304;
    public const int DefaultYamlDocuments = 1;
    public const int DefaultParserEvents = 131_072;
    public const int DefaultNormalizedNodes = 65_536;
    public const int DefaultNestingDepth = 32;
    public const int DefaultMappingEntries = 4_096;
    public const int DefaultSequenceEntries = 4_096;
    public const int DefaultScalarCharacters = 65_536;
    public const int DefaultAggregateScalarCharacters = 1_048_576;

    public static CompositionLimits Default { get; } = new();

    public int AggregateSourceBytes => DefaultAggregateSourceBytes;
    public int YamlDocuments => DefaultYamlDocuments;
    public int ParserEvents => DefaultParserEvents;
    public int NormalizedNodes => DefaultNormalizedNodes;
    public int NestingDepth => DefaultNestingDepth;
    public int MappingEntries => DefaultMappingEntries;
    public int SequenceEntries => DefaultSequenceEntries;
    public int ScalarCharacters => DefaultScalarCharacters;
    public int AggregateScalarCharacters => DefaultAggregateScalarCharacters;

    internal void Validate()
    {
        if (AggregateSourceBytes <= 0 || YamlDocuments <= 0 || ParserEvents <= 0
            || NormalizedNodes <= 0 || NestingDepth <= 0 || MappingEntries <= 0
            || SequenceEntries <= 0 || ScalarCharacters <= 0
            || AggregateScalarCharacters <= 0)
            throw new ArgumentOutOfRangeException(nameof(CompositionLimits));
    }
}
