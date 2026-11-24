namespace Configurator.Core.Composition;

using System.Text;
using System.Text.RegularExpressions;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;

internal static partial class CompositionYamlParser
{
    [GeneratedRegex("^-?(0|[1-9][0-9]*)(\\.[0-9]+)?([eE][+-]?[0-9]+)?$", RegexOptions.CultureInvariant)]
    private static partial Regex JsonNumberPattern();
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    internal static CompositionMap Parse(
        ReadOnlyMemory<byte> bytes, CompositionLimits limits, CancellationToken cancellationToken,
        CompositionBudget? sharedBudget = null, CompositionKeyPolicy keyPolicy = CompositionKeyPolicy.CollisionSafe)
    {
        if (bytes.IsEmpty)
            throw new CompositionException(CompositionFailureCode.InvalidDocument, new CompositionSourceLocation(1, 1, 0));
        if (bytes.Length > limits.AggregateSourceBytes)
            throw new CompositionException(CompositionFailureCode.LimitExceeded);

        ReadOnlySpan<byte> originalBytes = bytes.Span;
        int bomBytes = CompositionSourceLocation.Utf8BomLength(originalBytes);
        ReadOnlySpan<byte> contentSpan = bomBytes > 0 ? originalBytes[bomBytes..] : originalBytes;

        string text;
        try
        {
            text = StrictUtf8.GetString(contentSpan);
        }
        catch (DecoderFallbackException)
        {
            throw new CompositionException(
                CompositionFailureCode.InvalidDocument,
                CompositionSourceLocation.FromInvalidUtf8(originalBytes, bomBytes, standaloneCrBreaksLine: true));
        }

        try
        {
            int directiveIndex = FindDirectiveLineIndex(text);
            if (directiveIndex >= 0)
                throw new CompositionException(
                    CompositionFailureCode.UnsupportedYamlGrammar,
                    CompositionSourceLocation.FromTextCharIndex(text, bomBytes, directiveIndex));

            var budget = sharedBudget ?? new CompositionBudget(limits);
            var reader = new EventReader(new Parser(new StringReader(text)), budget, text, bomBytes, cancellationToken);
            reader.Advance();
            reader.Require<StreamStart>();
            reader.Advance();
            DocumentStart document = reader.Require<DocumentStart>();
            if (document.Version is not null || !HasOnlyStandardTagDirectives(document))
                throw new CompositionException(
                    CompositionFailureCode.UnsupportedYamlGrammar,
                    CompositionSourceLocation.FromTextCharIndex(text, bomBytes, document.Start.Index));
            reader.Advance();

            long rootStart = reader.Current?.Start.Index ?? 0;
            CompositionNode root = ReadNode(reader, new CompositionSourceText(text), text, bomBytes, budget, limits, 1, keyPolicy, cancellationToken);
            if (root is not CompositionMap map)
                throw new CompositionException(
                    CompositionFailureCode.InvalidDocument,
                    CompositionSourceLocation.FromTextCharIndex(text, bomBytes, rootStart));
            reader.Require<DocumentEnd>();
            reader.Advance();
            reader.Require<StreamEnd>();
            if (reader.Advance())
                throw new CompositionException(
                    CompositionFailureCode.InvalidDocument,
                    CompositionSourceLocation.FromTextCharIndex(text, bomBytes, reader.Current?.Start.Index ?? text.Length));
            return map;
        }
        catch (CompositionException) { throw; }
        catch (YamlException exception)
        {
            throw new CompositionException(
                CompositionFailureCode.InvalidDocument,
                CompositionSourceLocation.FromTextCharIndex(text, bomBytes, exception.Start.Index));
        }
    }

    private static int FindDirectiveLineIndex(string text)
    {
        for (int index = 0; index < text.Length; index++)
        {
            if (text[index] == '%' && (index == 0 || text[index - 1] is '\n' or '\r'))
                return index;
        }
        return -1;
    }

    private static bool HasOnlyStandardTagDirectives(DocumentStart document)
    {
        if (document.Tags is null || document.Tags.Count != 2)
            return false;
        return document.Tags.Any(static tag => tag.Handle == "!" && tag.Prefix == "!")
            && document.Tags.Any(static tag => tag.Handle == "!!" && tag.Prefix == "tag:yaml.org,2002:");
    }

    private static CompositionNode ReadNode(
        EventReader reader, CompositionSourceText source, string text, int bomBytes, CompositionBudget budget, CompositionLimits limits,
        int depth, CompositionKeyPolicy keyPolicy, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        budget.Node(depth);
        long startIndex = reader.Current!.Start.Index;
        int start = checked((int)startIndex);

        if (reader.Current is AnchorAlias)
            throw new CompositionException(
                CompositionFailureCode.UnsupportedYamlGrammar,
                CompositionSourceLocation.FromTextCharIndex(text, bomBytes, startIndex));
        if (reader.Current is NodeEvent nodeEvent
            && (!nodeEvent.Anchor.IsEmpty || !nodeEvent.Tag.IsEmpty))
            throw new CompositionException(
                CompositionFailureCode.UnsupportedYamlGrammar,
                CompositionSourceLocation.FromTextCharIndex(text, bomBytes, startIndex));

        if (reader.Current is MappingStart)
        {
            reader.Advance();
            var entries = new SortedDictionary<string, CompositionNode>(StringComparer.Ordinal);
            var identities = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            int count = 0;
            while (reader.Current is not MappingEnd)
            {
                cancellationToken.ThrowIfCancellationRequested();
                long entryIndex = reader.Current?.Start.Index ?? text.Length;
                if (checked(++count) > limits.MappingEntries)
                    throw new CompositionException(
                        CompositionFailureCode.LimitExceeded,
                        CompositionSourceLocation.FromTextCharIndex(text, bomBytes, entryIndex));
                if (reader.Current is not Scalar keyScalar)
                    throw new CompositionException(
                        CompositionFailureCode.InvalidKey,
                        CompositionSourceLocation.FromTextCharIndex(text, bomBytes, entryIndex));
                RejectNodeAuthority(keyScalar, text, bomBytes);
                string key = keyScalar.Value;
                if (keyPolicy != CompositionKeyPolicy.Exact && string.IsNullOrEmpty(key) || key == "<<"
                    || ConvertScalar(keyScalar, budget, text, bomBytes).Kind != CompositionScalarKind.String)
                    throw new CompositionException(
                        key == "<<" ? CompositionFailureCode.UnsupportedYamlGrammar : CompositionFailureCode.InvalidKey,
                        CompositionSourceLocation.FromTextCharIndex(text, bomBytes, keyScalar.Start.Index));
                reader.Advance();
                CompositionNode value = ReadNode(reader, source, text, bomBytes, budget, limits, checked(depth + 1), keyPolicy, cancellationToken);
                CompositionNormalizer.AddKey(entries, identities, key, value, keyPolicy, text, bomBytes, keyScalar.Start.Index);
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
                    throw new CompositionException(
                        CompositionFailureCode.LimitExceeded,
                        CompositionSourceLocation.FromTextCharIndex(text, bomBytes, reader.Current?.Start.Index ?? text.Length));
                entries.Add(ReadNode(reader, source, text, bomBytes, budget, limits, checked(depth + 1), keyPolicy, cancellationToken));
            }
            int end = checked((int)reader.Current.End.Index);
            reader.Advance();
            return new CompositionSequence(entries.AsReadOnly()) { SourceSpan = source.Span(start, end) };
        }

        if (reader.Current is Scalar scalar)
        {
            RejectNodeAuthority(scalar, text, bomBytes);
            CompositionScalar result = ConvertScalar(scalar, budget, text, bomBytes);
            if (keyPolicy == CompositionKeyPolicy.Exact && result.Kind == CompositionScalarKind.String)
                CompositionNormalizer.ValidateExactText(result.Value!, text, bomBytes, scalar.Start.Index);
            reader.Advance();
            return result with { SourceSpan = source.Span(start, checked((int)scalar.End.Index)) };
        }

        throw new CompositionException(
            CompositionFailureCode.UnsupportedYamlGrammar,
            CompositionSourceLocation.FromTextCharIndex(text, bomBytes, startIndex));
    }

    private static void RejectNodeAuthority(NodeEvent node, string text, int bomBytes)
    {
        if (!node.Anchor.IsEmpty || !node.Tag.IsEmpty)
            throw new CompositionException(
                CompositionFailureCode.UnsupportedYamlGrammar,
                CompositionSourceLocation.FromTextCharIndex(text, bomBytes, node.Start.Index));
    }

    private static CompositionScalar ConvertScalar(Scalar scalar, CompositionBudget budget, string text, int bomBytes)
    {
        string value = scalar.Value;
        budget.Scalar(value);
        if (scalar.Style != ScalarStyle.Plain)
            return new CompositionScalar(CompositionScalarKind.String, value);

        if (value.Length == 0)
            throw new CompositionException(
                CompositionFailureCode.InvalidScalar,
                CompositionSourceLocation.FromTextCharIndex(text, bomBytes, scalar.Start.Index));

        if (value == "true" || value == "false")
            return new CompositionScalar(CompositionScalarKind.Boolean, value);
        if (value == "null")
            return new CompositionScalar(CompositionScalarKind.Null, null);
        if (JsonNumberPattern().IsMatch(value))
            return new CompositionScalar(CompositionScalarKind.JsonNumber, value);
        if (IsAmbiguousPlainScalar(value))
            throw new CompositionException(
                CompositionFailureCode.InvalidScalar,
                CompositionSourceLocation.FromTextCharIndex(text, bomBytes, scalar.Start.Index));
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
        private readonly string _text;
        private readonly int _bomBytes;
        private readonly CancellationToken _cancellationToken;

        internal EventReader(IParser parser, CompositionBudget budget, string text, int bomBytes, CancellationToken cancellationToken)
        {
            _parser = parser;
            _budget = budget;
            _text = text;
            _bomBytes = bomBytes;
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
            Current as T ?? throw new CompositionException(
                CompositionFailureCode.InvalidDocument,
                CompositionSourceLocation.FromTextCharIndex(_text, _bomBytes, Current?.Start.Index ?? _text.Length));
    }
}
