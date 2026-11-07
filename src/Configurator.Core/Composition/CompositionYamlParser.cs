namespace Configurator.Core.Composition;

using System.Text;
using System.Text.RegularExpressions;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;

internal static partial class CompositionYamlParser
{
    [GeneratedRegex("^-?(0|[1-9][0-9]*)(\\.[0-9]+)?([eE][+-]?[0-9]+)?$", RegexOptions.CultureInvariant)]
    private static partial Regex JsonNumberPattern();
    internal static CompositionMap Parse(
        ReadOnlyMemory<byte> bytes, CompositionLimits limits, CancellationToken cancellationToken,
        CompositionBudget? sharedBudget = null, CompositionKeyPolicy keyPolicy = CompositionKeyPolicy.CollisionSafe)
    {
        if (bytes.IsEmpty)
            throw new CompositionException(CompositionFailureCode.InvalidDocument);
        if (bytes.Length > limits.AggregateSourceBytes)
            throw new CompositionException(CompositionFailureCode.LimitExceeded);

        string text;
        try
        {
            text = new UTF8Encoding(false, true).GetString(bytes.Span);
            if (text.Length > 0 && text[0] == '\uFEFF') text = text[1..];
        }
        catch (DecoderFallbackException)
        {
            throw new CompositionException(CompositionFailureCode.InvalidDocument);
        }

        try
        {
            if (ContainsDirectiveLine(text))
                throw new CompositionException(CompositionFailureCode.UnsupportedYamlGrammar);

            var budget = sharedBudget ?? new CompositionBudget(limits);
            var reader = new EventReader(new Parser(new StringReader(text)), budget, cancellationToken);
            reader.Advance();
            reader.Require<StreamStart>();
            reader.Advance();
            DocumentStart document = reader.Require<DocumentStart>();
            if (document.Version is not null || !HasOnlyStandardTagDirectives(document))
                throw new CompositionException(CompositionFailureCode.UnsupportedYamlGrammar);
            reader.Advance();

            CompositionNode root = ReadNode(reader, new CompositionSourceText(text), budget, limits, 1, keyPolicy, cancellationToken);
            if (root is not CompositionMap map)
                throw new CompositionException(CompositionFailureCode.InvalidDocument);
            reader.Require<DocumentEnd>();
            reader.Advance();
            reader.Require<StreamEnd>();
            if (reader.Advance())
                throw new CompositionException(CompositionFailureCode.InvalidDocument);
            return map;
        }
        catch (CompositionException) { throw; }
        catch (YamlException)
        {
            throw new CompositionException(CompositionFailureCode.InvalidDocument);
        }
    }

    private static bool ContainsDirectiveLine(string text)
    {
        for (int index = 0; index < text.Length; index++)
        {
            if (text[index] == '%' && (index == 0 || text[index - 1] == '\n'
                    || index == 1 && text[0] == '\uFEFF'))
                return true;
        }
        return false;
    }

    private static bool HasOnlyStandardTagDirectives(DocumentStart document)
    {
        if (document.Tags is null || document.Tags.Count != 2)
            return false;
        return document.Tags.Any(static tag => tag.Handle == "!" && tag.Prefix == "!")
            && document.Tags.Any(static tag => tag.Handle == "!!" && tag.Prefix == "tag:yaml.org,2002:");
    }

    private static CompositionNode ReadNode(
        EventReader reader, CompositionSourceText source, CompositionBudget budget, CompositionLimits limits,
        int depth, CompositionKeyPolicy keyPolicy, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        budget.Node(depth);
        int start = checked((int)reader.Current!.Start.Index);

        if (reader.Current is AnchorAlias)
            throw new CompositionException(CompositionFailureCode.UnsupportedYamlGrammar);
        if (reader.Current is NodeEvent nodeEvent
            && (!nodeEvent.Anchor.IsEmpty || !nodeEvent.Tag.IsEmpty))
            throw new CompositionException(CompositionFailureCode.UnsupportedYamlGrammar);

        if (reader.Current is MappingStart)
        {
            reader.Advance();
            var entries = new SortedDictionary<string, CompositionNode>(StringComparer.Ordinal);
            var identities = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            int count = 0;
            while (reader.Current is not MappingEnd)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (checked(++count) > limits.MappingEntries)
                    throw new CompositionException(CompositionFailureCode.LimitExceeded);
                if (reader.Current is not Scalar keyScalar)
                    throw new CompositionException(CompositionFailureCode.InvalidKey);
                RejectNodeAuthority(keyScalar);
                string key = keyScalar.Value;
                if (keyPolicy != CompositionKeyPolicy.Exact && string.IsNullOrEmpty(key) || key == "<<"
                    || ConvertScalar(keyScalar, budget).Kind != CompositionScalarKind.String)
                    throw new CompositionException(key == "<<"
                        ? CompositionFailureCode.UnsupportedYamlGrammar
                        : CompositionFailureCode.InvalidKey);
                reader.Advance();
                CompositionNode value = ReadNode(reader, source, budget, limits, checked(depth + 1), keyPolicy, cancellationToken);
                CompositionNormalizer.AddKey(entries, identities, key, value, keyPolicy);
            }
            int end = checked((int)reader.Current.End.Index);
            reader.Advance();
            return new CompositionMap(entries) { SourceSpan = source.Span(start, end) };
        }

        if (reader.Current is SequenceStart)
        {
            reader.Advance();
            var entries = new List<CompositionNode>();
            while (reader.Current is not SequenceEnd)
            {
                if (checked(entries.Count + 1) > limits.SequenceEntries)
                    throw new CompositionException(CompositionFailureCode.LimitExceeded);
                entries.Add(ReadNode(reader, source, budget, limits, checked(depth + 1), keyPolicy, cancellationToken));
            }
            int end = checked((int)reader.Current.End.Index);
            reader.Advance();
            return new CompositionSequence(entries.AsReadOnly()) { SourceSpan = source.Span(start, end) };
        }

        if (reader.Current is Scalar scalar)
        {
            RejectNodeAuthority(scalar);
            CompositionScalar result = ConvertScalar(scalar, budget);
            if (keyPolicy == CompositionKeyPolicy.Exact && result.Kind == CompositionScalarKind.String)
                CompositionNormalizer.ValidateExactText(result.Value!);
            reader.Advance();
            return result with { SourceSpan = source.Span(start, checked((int)scalar.End.Index)) };
        }

        throw new CompositionException(CompositionFailureCode.UnsupportedYamlGrammar);
    }

    private static void RejectNodeAuthority(NodeEvent node)
    {
        if (!node.Anchor.IsEmpty || !node.Tag.IsEmpty)
            throw new CompositionException(CompositionFailureCode.UnsupportedYamlGrammar);
    }

    private static CompositionScalar ConvertScalar(Scalar scalar, CompositionBudget budget)
    {
        string value = scalar.Value;
        budget.Scalar(value);
        if (scalar.Style != ScalarStyle.Plain)
            return new CompositionScalar(CompositionScalarKind.String, value);

        if (value.Length == 0)
            throw new CompositionException(CompositionFailureCode.InvalidScalar);

        if (value == "true" || value == "false")
            return new CompositionScalar(CompositionScalarKind.Boolean, value);
        if (value == "null")
            return new CompositionScalar(CompositionScalarKind.Null, null);
        if (JsonNumberPattern().IsMatch(value))
            return new CompositionScalar(CompositionScalarKind.JsonNumber, value);
        if (IsAmbiguousPlainScalar(value))
            throw new CompositionException(CompositionFailureCode.InvalidScalar);
        return new CompositionScalar(CompositionScalarKind.String, value);
    }

    private static bool IsAmbiguousPlainScalar(string value)
    {
        string folded = value.ToLowerInvariant();
        if (folded is "true" or "false" or "null" or "yes" or "no" or "on" or "off"
            or "~" or ".nan" or ".inf" or "+.inf" or "-.inf")
            return true;
        if (value.Length == 0 || value[0] is not ('+' or '-' or '.' or >= '0' and <= '9'))
            return false;
        return value.All(static character =>
            char.IsDigit(character) || character is '+' or '-' or '.' or ',' or '_' or ':'
                or 'e' or 'E' or 'x' or 'X' or 'o' or 'O');
    }

    private sealed class EventReader
    {
        private readonly IParser _parser;
        private readonly CompositionBudget _budget;
        private readonly CancellationToken _cancellationToken;

        internal EventReader(IParser parser, CompositionBudget budget, CancellationToken cancellationToken)
        {
            _parser = parser;
            _budget = budget;
            _cancellationToken = cancellationToken;
        }

        internal ParsingEvent? Current => _parser.Current;

        internal bool Advance()
        {
            _cancellationToken.ThrowIfCancellationRequested();
            if (!_parser.MoveNext())
                return false;
            _budget.ParserEvent();
            return true;
        }

        internal T Require<T>() where T : ParsingEvent =>
            Current as T ?? throw new CompositionException(CompositionFailureCode.InvalidDocument);
    }
}
