namespace Configurator.Core.Composition;

using System.Buffers;
using System.Text;
using System.Text.Json;
using Configurator.Core.Definitions;

internal sealed class CompositionException : Exception
{
    internal CompositionException(CompositionFailureCode code) : base("Setup composition failed.") => Code = code;
    internal CompositionFailureCode Code { get; }
}

internal abstract record CompositionNode
{
    internal DefinitionSourceSpan? SourceSpan { get; init; }
}
internal sealed record CompositionMap(IReadOnlyDictionary<string, CompositionNode> Entries) : CompositionNode;
internal sealed record CompositionSequence(IReadOnlyList<CompositionNode> Entries) : CompositionNode;
internal sealed record CompositionScalar(CompositionScalarKind Kind, string? Value) : CompositionNode;

internal enum CompositionScalarKind
{
    String,
    Integer,
    Boolean,
    Null,
    JsonNumber
}

internal enum CompositionKeyPolicy { CollisionSafe, Exact }

internal sealed class CompositionBudget
{
    private readonly CompositionLimits _limits;
    private int _nodes;
    private int _scalarCharacters;

    internal CompositionBudget(CompositionLimits limits) => _limits = limits;
    internal int Nodes => _nodes;

    internal void Node(int depth)
    {
        if (depth > _limits.NestingDepth)
            throw new CompositionException(CompositionFailureCode.LimitExceeded);
        try { _nodes = checked(_nodes + 1); }
        catch (OverflowException) { throw new CompositionException(CompositionFailureCode.LimitExceeded); }
        if (_nodes > _limits.NormalizedNodes)
            throw new CompositionException(CompositionFailureCode.LimitExceeded);
    }

    internal void Scalar(string? value)
    {
        int length = value?.Length ?? 0;
        if (length > _limits.ScalarCharacters)
            throw new CompositionException(CompositionFailureCode.LimitExceeded);
        try { _scalarCharacters = checked(_scalarCharacters + length); }
        catch (OverflowException) { throw new CompositionException(CompositionFailureCode.LimitExceeded); }
        if (_scalarCharacters > _limits.AggregateScalarCharacters)
            throw new CompositionException(CompositionFailureCode.LimitExceeded);
    }
}

internal static class CompositionNormalizer
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    internal static CompositionMap ParseJson(
        ReadOnlyMemory<byte> bytes, CompositionLimits limits, CancellationToken cancellationToken,
        CompositionBudget? sharedBudget = null, CompositionKeyPolicy keyPolicy = CompositionKeyPolicy.CollisionSafe)
    {
        if (bytes.IsEmpty)
            throw new CompositionException(CompositionFailureCode.InvalidDocument);
        if (bytes.Length > limits.AggregateSourceBytes)
            throw new CompositionException(CompositionFailureCode.LimitExceeded);

        if (bytes.Span.StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }))
            bytes = bytes[3..];

        try
        {
            _ = StrictUtf8.GetCharCount(bytes.Span);
            var reader = new Utf8JsonReader(bytes.Span, new JsonReaderOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = limits.NestingDepth
            });
            var budget = sharedBudget ?? new CompositionBudget(limits);
            var source = new CompositionSourceText(StrictUtf8.GetString(bytes.Span));
            if (!reader.Read()) throw new CompositionException(CompositionFailureCode.InvalidDocument);
            CompositionNode root = FromJson(ref reader, bytes.Span, source, 1, budget, limits, keyPolicy, cancellationToken);
            if (reader.Read()) throw new CompositionException(CompositionFailureCode.InvalidDocument);
            return root as CompositionMap
                ?? throw new CompositionException(CompositionFailureCode.InvalidDocument);
        }
        catch (CompositionException) { throw; }
        catch (JsonException)
        {
            throw new CompositionException(CompositionFailureCode.InvalidDocument);
        }
        catch (DecoderFallbackException)
        {
            throw new CompositionException(CompositionFailureCode.InvalidDocument);
        }
    }

    internal static CompositionMap Merge(
        IEnumerable<CompositionMap> fragments, CompositionLimits limits,
        CancellationToken cancellationToken)
    {
        var merged = new SortedDictionary<string, CompositionNode>(StringComparer.Ordinal);
        var identities = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (CompositionMap fragment in fragments)
        {
            cancellationToken.ThrowIfCancellationRequested();
            MergeInto(merged, identities, fragment.Entries, limits, cancellationToken);
        }
        return new CompositionMap(merged);
    }

    internal static byte[] WriteJson(CompositionMap root, CancellationToken cancellationToken)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = false }))
        {
            WriteNode(writer, root, cancellationToken);
        }
        return buffer.WrittenSpan.ToArray();
    }

    private static CompositionNode FromJson(
        ref Utf8JsonReader reader, ReadOnlySpan<byte> bytes, CompositionSourceText source,
        int depth, CompositionBudget budget, CompositionLimits limits,
        CompositionKeyPolicy keyPolicy, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        budget.Node(depth);
        int start = source.CharacterOffset(bytes, checked((int)reader.TokenStartIndex));
        CompositionNode result;
        switch (reader.TokenType)
        {
            case JsonTokenType.StartObject:
                {
                    var entries = new SortedDictionary<string, CompositionNode>(StringComparer.Ordinal);
                    var identities = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    int count = 0;
                    while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                    {
                        if (reader.TokenType != JsonTokenType.PropertyName)
                            throw new CompositionException(CompositionFailureCode.InvalidDocument);
                        if (checked(++count) > limits.MappingEntries)
                            throw new CompositionException(CompositionFailureCode.LimitExceeded);
                        string name = reader.GetString()!;
                        if (keyPolicy == CompositionKeyPolicy.Exact) budget.Scalar(name);
                        if (!reader.Read()) throw new CompositionException(CompositionFailureCode.InvalidDocument);
                        AddKey(entries, identities, name,
                            FromJson(ref reader, bytes, source, checked(depth + 1), budget, limits, keyPolicy, cancellationToken), keyPolicy);
                    }
                    if (reader.TokenType != JsonTokenType.EndObject)
                        throw new CompositionException(CompositionFailureCode.InvalidDocument);
                    result = new CompositionMap(entries);
                    break;
                }
            case JsonTokenType.StartArray:
                {
                    var entries = new List<CompositionNode>();
                    while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                    {
                        if (checked(entries.Count + 1) > limits.SequenceEntries)
                            throw new CompositionException(CompositionFailureCode.LimitExceeded);
                        entries.Add(FromJson(ref reader, bytes, source, checked(depth + 1), budget, limits, keyPolicy, cancellationToken));
                    }
                    if (reader.TokenType != JsonTokenType.EndArray)
                        throw new CompositionException(CompositionFailureCode.InvalidDocument);
                    result = new CompositionSequence(entries.AsReadOnly());
                    break;
                }
            case JsonTokenType.String:
                {
                    string value = reader.GetString()!;
                    budget.Scalar(value);
                    if (keyPolicy == CompositionKeyPolicy.Exact) ValidateExactText(value);
                    result = new CompositionScalar(CompositionScalarKind.String, value);
                    break;
                }
            case JsonTokenType.Number:
                {
                    string value = StrictUtf8.GetString(reader.ValueSpan);
                    budget.Scalar(value);
                    result = new CompositionScalar(CompositionScalarKind.JsonNumber, value);
                    break;
                }
            case JsonTokenType.True:
                budget.Scalar("true");
                result = new CompositionScalar(CompositionScalarKind.Boolean, "true");
                break;
            case JsonTokenType.False:
                budget.Scalar("false");
                result = new CompositionScalar(CompositionScalarKind.Boolean, "false");
                break;
            case JsonTokenType.Null:
                result = new CompositionScalar(CompositionScalarKind.Null, null);
                break;
            default:
                throw new CompositionException(CompositionFailureCode.InvalidDocument);
        }
        int end = checked((int)reader.BytesConsumed);
        return result with { SourceSpan = source.Span(start, source.CharacterOffset(bytes, end)) };
    }

    internal static void AddKey(
        SortedDictionary<string, CompositionNode> entries, Dictionary<string, string> identities,
        string key, CompositionNode value, CompositionKeyPolicy keyPolicy = CompositionKeyPolicy.CollisionSafe)
    {
        if (keyPolicy != CompositionKeyPolicy.Exact && string.IsNullOrEmpty(key))
            throw new CompositionException(CompositionFailureCode.InvalidKey);
        if (entries.ContainsKey(key))
            throw new CompositionException(CompositionFailureCode.DuplicateKey);
        if (keyPolicy == CompositionKeyPolicy.Exact)
        {
            ValidateExactText(key);
            entries.Add(key, value);
            return;
        }

        string normalized = key.Normalize(NormalizationForm.FormC);
        if (identities.TryGetValue(normalized, out _))
            throw new CompositionException(CompositionFailureCode.KeyCollision);
        identities.Add(normalized, key);
        entries.Add(key, value);
    }

    internal static void ValidateExactText(string text)
    {
        try { _ = StrictUtf8.GetByteCount(text); }
        catch (EncoderFallbackException)
        {
            throw new CompositionException(CompositionFailureCode.InvalidScalar);
        }
    }

    private static void MergeInto(
        SortedDictionary<string, CompositionNode> target, Dictionary<string, string> identities,
        IReadOnlyDictionary<string, CompositionNode> source, CompositionLimits limits,
        CancellationToken cancellationToken)
    {
        foreach ((string key, CompositionNode incoming) in source)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!target.TryGetValue(key, out CompositionNode? existing))
            {
                AddKey(target, identities, key, incoming);
                continue;
            }

            if (existing is CompositionMap existingMap && incoming is CompositionMap incomingMap)
            {
                var nested = new SortedDictionary<string, CompositionNode>(StringComparer.Ordinal);
                foreach ((string nestedKey, CompositionNode nestedValue) in existingMap.Entries)
                    nested.Add(nestedKey, nestedValue);
                var nestedIdentities = nested.Keys.ToDictionary(
                    static key => key.Normalize(NormalizationForm.FormC), static key => key,
                    StringComparer.OrdinalIgnoreCase);
                MergeInto(nested, nestedIdentities, incomingMap.Entries, limits, cancellationToken);
                if (nested.Count > limits.MappingEntries)
                    throw new CompositionException(CompositionFailureCode.LimitExceeded);
                target[key] = new CompositionMap(nested);
                continue;
            }

            throw new CompositionException(CompositionFailureCode.SourceConflict);
        }
    }

    private static void WriteNode(Utf8JsonWriter writer, CompositionNode node, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        switch (node)
        {
            case CompositionMap map:
                writer.WriteStartObject();
                foreach ((string key, CompositionNode value) in map.Entries)
                {
                    writer.WritePropertyName(key);
                    WriteNode(writer, value, cancellationToken);
                }
                writer.WriteEndObject();
                break;
            case CompositionSequence sequence:
                writer.WriteStartArray();
                foreach (CompositionNode value in sequence.Entries)
                    WriteNode(writer, value, cancellationToken);
                writer.WriteEndArray();
                break;
            case CompositionScalar { Kind: CompositionScalarKind.String } scalar:
                writer.WriteStringValue(scalar.Value);
                break;
            case CompositionScalar { Kind: CompositionScalarKind.Integer or CompositionScalarKind.JsonNumber } scalar:
                writer.WriteRawValue(scalar.Value!, skipInputValidation: false);
                break;
            case CompositionScalar { Kind: CompositionScalarKind.Boolean } scalar:
                writer.WriteBooleanValue(string.Equals(scalar.Value, "true", StringComparison.Ordinal));
                break;
            case CompositionScalar { Kind: CompositionScalarKind.Null }:
                writer.WriteNullValue();
                break;
            default:
                throw new CompositionException(CompositionFailureCode.InvalidDocument);
        }
    }
}

/// <summary>Indexes parser offsets only; it does not interpret document syntax.</summary>
internal sealed class CompositionSourceText
{
    private readonly int[] _lineStarts;
    private int _byteOffset;
    private int _characterOffset;

    internal CompositionSourceText(string text)
    {
        var starts = new List<int> { 0 };
        for (int index = 0; index < text.Length; index++)
            if (text[index] == '\n') starts.Add(index + 1);
        _lineStarts = starts.ToArray();
    }

    internal int CharacterOffset(ReadOnlySpan<byte> bytes, int offset)
    {
        _characterOffset += Encoding.UTF8.GetCharCount(bytes[_byteOffset..offset]);
        _byteOffset = offset;
        return _characterOffset;
    }

    internal DefinitionSourceSpan Span(int start, int end)
    {
        int startLine = Array.BinarySearch(_lineStarts, start);
        if (startLine < 0) startLine = ~startLine - 1;
        int endLine = Array.BinarySearch(_lineStarts, end);
        if (endLine < 0) endLine = ~endLine - 1;
        return new DefinitionSourceSpan(0, startLine + 1, start - _lineStarts[startLine] + 1,
            endLine + 1, end - _lineStarts[endLine] + 1);
    }
}
