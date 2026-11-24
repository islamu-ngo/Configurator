namespace Configurator.Core.Composition;

using System.Buffers;
using System.Text;
using System.Text.Json;
using Configurator.Core.Definitions;

internal static class CompositionNormalizer
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    internal static CompositionMap ParseJson(
        ReadOnlyMemory<byte> bytes, CompositionLimits limits, CancellationToken cancellationToken,
        CompositionBudget? sharedBudget = null, CompositionKeyPolicy keyPolicy = CompositionKeyPolicy.CollisionSafe)
    {
        if (bytes.IsEmpty)
            throw new CompositionException(CompositionFailureCode.InvalidDocument, new CompositionSourceLocation(1, 1, 0));
        if (bytes.Length > limits.AggregateSourceBytes)
            throw new CompositionException(CompositionFailureCode.LimitExceeded);

        ReadOnlySpan<byte> originalBytes = bytes.Span;
        int bomBytes = CompositionSourceLocation.Utf8BomLength(originalBytes);
        ReadOnlyMemory<byte> contentMemory = bomBytes > 0 ? bytes[bomBytes..] : bytes;
        ReadOnlySpan<byte> contentSpan = contentMemory.Span;

        try
        {
            _ = StrictUtf8.GetCharCount(contentSpan);
        }
        catch (DecoderFallbackException)
        {
            throw new CompositionException(
                CompositionFailureCode.InvalidDocument,
                CompositionSourceLocation.FromInvalidUtf8(originalBytes, bomBytes, standaloneCrBreaksLine: false));
        }

        long lastContentOffset = 0;
        try
        {
            var reader = new Utf8JsonReader(contentSpan, new JsonReaderOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = limits.NestingDepth
            });
            var budget = sharedBudget ?? new CompositionBudget(limits);
            var source = new CompositionSourceText(StrictUtf8.GetString(contentSpan));
            if (!reader.Read())
                throw new CompositionException(
                    CompositionFailureCode.InvalidDocument,
                    CompositionSourceLocation.FromUtf8ByteOffset(originalBytes, bomBytes, contentSpan.Length, standaloneCrBreaksLine: false));
            long rootStart = reader.TokenStartIndex;
            lastContentOffset = rootStart;
            CompositionNode root = FromJson(
                ref reader, originalBytes, bomBytes, contentSpan, source, 1, budget, limits, keyPolicy, cancellationToken);
            lastContentOffset = reader.BytesConsumed;
            if (reader.Read())
                throw new CompositionException(
                    CompositionFailureCode.InvalidDocument,
                    CompositionSourceLocation.FromUtf8ByteOffset(originalBytes, bomBytes, reader.TokenStartIndex, standaloneCrBreaksLine: false));
            return root as CompositionMap
                ?? throw new CompositionException(
                    CompositionFailureCode.InvalidDocument,
                    CompositionSourceLocation.FromUtf8ByteOffset(originalBytes, bomBytes, rootStart, standaloneCrBreaksLine: false));
        }
        catch (CompositionException) { throw; }
        catch (JsonException exception)
        {
            throw new CompositionException(
                CompositionFailureCode.InvalidDocument,
                CompositionSourceLocation.FromJsonLineAndBytePosition(
                    originalBytes, bomBytes, exception.LineNumber, exception.BytePositionInLine, lastContentOffset));
        }
        catch (DecoderFallbackException)
        {
            throw new CompositionException(
                CompositionFailureCode.InvalidDocument,
                CompositionSourceLocation.FromInvalidUtf8(originalBytes, bomBytes, standaloneCrBreaksLine: false));
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
        ref Utf8JsonReader reader, ReadOnlySpan<byte> originalBytes, int bomBytes,
        ReadOnlySpan<byte> bytes, CompositionSourceText source,
        int depth, CompositionBudget budget, CompositionLimits limits,
        CompositionKeyPolicy keyPolicy, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        budget.Node(depth);
        long tokenStartIndex = reader.TokenStartIndex;
        int start = source.CharacterOffset(bytes, checked((int)tokenStartIndex));
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
                        long propertyStartIndex = reader.TokenStartIndex;
                        if (reader.TokenType != JsonTokenType.PropertyName)
                            throw new CompositionException(
                                CompositionFailureCode.InvalidDocument,
                                CompositionSourceLocation.FromUtf8ByteOffset(originalBytes, bomBytes, propertyStartIndex, standaloneCrBreaksLine: false));
                        if (checked(++count) > limits.MappingEntries)
                            throw new CompositionException(
                                CompositionFailureCode.LimitExceeded,
                                CompositionSourceLocation.FromUtf8ByteOffset(originalBytes, bomBytes, propertyStartIndex, standaloneCrBreaksLine: false));
                        string name = reader.GetString()!;
                        if (keyPolicy == CompositionKeyPolicy.Exact) budget.Scalar(name);
                        if (!reader.Read())
                            throw new CompositionException(
                                CompositionFailureCode.InvalidDocument,
                                CompositionSourceLocation.FromUtf8ByteOffset(originalBytes, bomBytes, reader.BytesConsumed, standaloneCrBreaksLine: false));
                        CompositionNode child = FromJson(
                            ref reader, originalBytes, bomBytes, bytes, source, checked(depth + 1), budget, limits, keyPolicy, cancellationToken);
                        AddJsonKey(entries, identities, name, child, keyPolicy, originalBytes, bomBytes, propertyStartIndex);
                    }
                    if (reader.TokenType != JsonTokenType.EndObject)
                        throw new CompositionException(
                            CompositionFailureCode.InvalidDocument,
                            CompositionSourceLocation.FromUtf8ByteOffset(originalBytes, bomBytes, reader.BytesConsumed, standaloneCrBreaksLine: false));
                    result = new CompositionMap(entries);
                    break;
                }
            case JsonTokenType.StartArray:
                {
                    var entries = new List<CompositionNode>();
                    while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                    {
                        if (checked(entries.Count + 1) > limits.SequenceEntries)
                            throw new CompositionException(
                                CompositionFailureCode.LimitExceeded,
                                CompositionSourceLocation.FromUtf8ByteOffset(originalBytes, bomBytes, reader.TokenStartIndex, standaloneCrBreaksLine: false));
                        entries.Add(FromJson(
                            ref reader, originalBytes, bomBytes, bytes, source, checked(depth + 1), budget, limits, keyPolicy, cancellationToken));
                    }
                    if (reader.TokenType != JsonTokenType.EndArray)
                        throw new CompositionException(
                            CompositionFailureCode.InvalidDocument,
                            CompositionSourceLocation.FromUtf8ByteOffset(originalBytes, bomBytes, reader.BytesConsumed, standaloneCrBreaksLine: false));
                    result = new CompositionSequence(entries.AsReadOnly());
                    break;
                }
            case JsonTokenType.String:
                {
                    string value = reader.GetString()!;
                    budget.Scalar(value);
                    if (keyPolicy == CompositionKeyPolicy.Exact)
                        ValidateJsonExactText(value, originalBytes, bomBytes, tokenStartIndex);
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
                throw new CompositionException(
                    CompositionFailureCode.InvalidDocument,
                    CompositionSourceLocation.FromUtf8ByteOffset(originalBytes, bomBytes, tokenStartIndex, standaloneCrBreaksLine: false));
        }
        int end = checked((int)reader.BytesConsumed);
        return result with { SourceSpan = source.Span(start, source.CharacterOffset(bytes, end)) };
    }

    private static void AddJsonKey(
        SortedDictionary<string, CompositionNode> entries, Dictionary<string, string> identities,
        string key, CompositionNode value, CompositionKeyPolicy keyPolicy,
        ReadOnlySpan<byte> originalBytes, int bomBytes, long keyByteOffset)
    {
        if (keyPolicy != CompositionKeyPolicy.Exact && string.IsNullOrEmpty(key))
            throw new CompositionException(
                CompositionFailureCode.InvalidKey,
                CompositionSourceLocation.FromUtf8ByteOffset(originalBytes, bomBytes, keyByteOffset, standaloneCrBreaksLine: false));
        if (entries.ContainsKey(key))
            throw new CompositionException(
                CompositionFailureCode.DuplicateKey,
                CompositionSourceLocation.FromUtf8ByteOffset(originalBytes, bomBytes, keyByteOffset, standaloneCrBreaksLine: false));
        if (keyPolicy == CompositionKeyPolicy.Exact)
        {
            ValidateJsonExactText(key, originalBytes, bomBytes, keyByteOffset);
            entries.Add(key, value);
            return;
        }

        string normalized = key.Normalize(NormalizationForm.FormC);
        if (identities.TryGetValue(normalized, out _))
            throw new CompositionException(
                CompositionFailureCode.KeyCollision,
                CompositionSourceLocation.FromUtf8ByteOffset(originalBytes, bomBytes, keyByteOffset, standaloneCrBreaksLine: false));
        identities.Add(normalized, key);
        entries.Add(key, value);
    }

    private static void ValidateJsonExactText(
        string text, ReadOnlySpan<byte> originalBytes, int bomBytes, long tokenByteOffset)
    {
        try { _ = StrictUtf8.GetByteCount(text); }
        catch (EncoderFallbackException)
        {
            throw new CompositionException(
                CompositionFailureCode.InvalidScalar,
                CompositionSourceLocation.FromUtf8ByteOffset(originalBytes, bomBytes, tokenByteOffset, standaloneCrBreaksLine: false));
        }
    }

    internal static void AddKey(
        SortedDictionary<string, CompositionNode> entries, Dictionary<string, string> identities,
        string key, CompositionNode value, CompositionKeyPolicy keyPolicy = CompositionKeyPolicy.CollisionSafe,
        string? yamlText = null, int bomBytes = 0, long yamlCharIndex = -1)
    {
        if (keyPolicy != CompositionKeyPolicy.Exact && string.IsNullOrEmpty(key))
            throw new CompositionException(CompositionFailureCode.InvalidKey, ResolveYamlLocation(yamlText, bomBytes, yamlCharIndex));
        if (entries.ContainsKey(key))
            throw new CompositionException(CompositionFailureCode.DuplicateKey, ResolveYamlLocation(yamlText, bomBytes, yamlCharIndex));
        if (keyPolicy == CompositionKeyPolicy.Exact)
        {
            ValidateExactText(key, yamlText, bomBytes, yamlCharIndex);
            entries.Add(key, value);
            return;
        }

        string normalized = key.Normalize(NormalizationForm.FormC);
        if (identities.TryGetValue(normalized, out _))
            throw new CompositionException(CompositionFailureCode.KeyCollision, ResolveYamlLocation(yamlText, bomBytes, yamlCharIndex));
        identities.Add(normalized, key);
        entries.Add(key, value);
    }

    internal static void ValidateExactText(string text, string? yamlText = null, int bomBytes = 0, long yamlCharIndex = -1)
    {
        try { _ = StrictUtf8.GetByteCount(text); }
        catch (EncoderFallbackException)
        {
            throw new CompositionException(CompositionFailureCode.InvalidScalar, ResolveYamlLocation(yamlText, bomBytes, yamlCharIndex));
        }
    }

    private static CompositionSourceLocation? ResolveYamlLocation(string? yamlText, int bomBytes, long yamlCharIndex) =>
        yamlText is not null && yamlCharIndex >= 0
            ? CompositionSourceLocation.FromTextCharIndex(yamlText, bomBytes, yamlCharIndex)
            : null;

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
