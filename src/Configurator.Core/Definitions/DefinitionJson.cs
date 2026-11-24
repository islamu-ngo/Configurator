namespace Configurator.Core.Definitions;

using System.Buffers;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Configurator.Core.Composition;

/// <summary>
/// Owned byte buffer writer that cryptographically zeros superseded backing arrays on growth and on disposal.
/// </summary>
internal sealed class ZeroingByteBufferWriter : IBufferWriter<byte>, IDisposable
{
    private byte[] _buffer;
    private int _index;
    private bool _disposed;

    internal ZeroingByteBufferWriter(int initialCapacity = 1024)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(initialCapacity);
        _buffer = new byte[initialCapacity];
    }

    internal int WrittenCount => _index;
    internal ReadOnlySpan<byte> WrittenSpan => _buffer.AsSpan(0, _index);
    internal ReadOnlyMemory<byte> WrittenMemory => _buffer.AsMemory(0, _index);

    public void Advance(int count)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (count < 0 || _index + count > _buffer.Length)
            throw new ArgumentOutOfRangeException(nameof(count));
        _index += count;
    }

    public Memory<byte> GetMemory(int sizeHint = 0)
    {
        EnsureCapacity(sizeHint);
        return _buffer.AsMemory(_index);
    }

    public Span<byte> GetSpan(int sizeHint = 0)
    {
        EnsureCapacity(sizeHint);
        return _buffer.AsSpan(_index);
    }

    private void EnsureCapacity(int sizeHint)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentOutOfRangeException.ThrowIfNegative(sizeHint);
        if (sizeHint == 0) sizeHint = 1;
        int available = _buffer.Length - _index;
        if (sizeHint <= available) return;

        int required = checked(_index + sizeHint);
        int nextSize = _buffer.Length;
        while (nextSize < required)
        {
            nextSize = nextSize > int.MaxValue / 2 ? required : nextSize * 2;
        }

        byte[] next = new byte[nextSize];
        _buffer.AsSpan(0, _index).CopyTo(next);
        CryptographicOperations.ZeroMemory(_buffer.AsSpan(0, _index));
        _buffer = next;
    }

    public void Dispose()
    {
        if (_disposed) return;
        CryptographicOperations.ZeroMemory(_buffer.AsSpan(0, _index));
        _index = 0;
        _disposed = true;
    }
}

internal static class DefinitionJson
{
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    internal static byte[] Canonicalize(JsonElement value, CompositionBudget? budget = null,
        CompositionKeyPolicy keyPolicy = CompositionKeyPolicy.CollisionSafe, int initialDepth = 1)
    {
        ValueTask<byte[]> operation = CanonicalizeCoreAsync(value, null, budget, keyPolicy, initialDepth, default);
        if (!operation.IsCompleted) throw new InvalidOperationException("synchronous-operation-incomplete");
        return operation.GetAwaiter().GetResult();
    }

    internal static ValueTask<byte[]> CanonicalizeAsync(JsonElement value,
        Func<CancellationToken, ValueTask> cooperativeYield, CompositionBudget? budget = null,
        CompositionKeyPolicy keyPolicy = CompositionKeyPolicy.CollisionSafe, int initialDepth = 1,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cooperativeYield);
        return CanonicalizeCoreAsync(value, cooperativeYield, budget, keyPolicy, initialDepth, cancellationToken);
    }

    private static async ValueTask<byte[]> CanonicalizeCoreAsync(JsonElement value,
        Func<CancellationToken, ValueTask>? cooperativeYield, CompositionBudget? budget,
        CompositionKeyPolicy keyPolicy, int initialDepth, CancellationToken cancellationToken)
    {
        budget ??= new CompositionBudget(CompositionLimits.Default);
        int normalizedNodes = 0;
        using var buffer = new ZeroingByteBufferWriter();
        using (var writer = new Utf8JsonWriter(buffer))
            await WriteAsync(writer, value, initialDepth);
        if (buffer.WrittenCount > CompositionLimits.DefaultAggregateSourceBytes)
            throw new DefinitionException("document-limit");
        return buffer.WrittenSpan.ToArray();

        async ValueTask WriteAsync(Utf8JsonWriter writer, JsonElement element, int depth)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try { budget.Node(depth); }
            catch (CompositionException) { throw new DefinitionException("document-limit"); }
            if (++normalizedNodes % 1024 == 0 && cooperativeYield is not null)
            {
                await cooperativeYield(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
            }
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    if (keyPolicy == CompositionKeyPolicy.Exact && IsStrictlyOrdinalSorted(element, out int propertyCount))
                    {
                        if (propertyCount > CompositionLimits.DefaultMappingEntries)
                            throw new DefinitionException("document-limit");
                        writer.WriteStartObject();
                        foreach (JsonProperty property in element.EnumerateObject())
                        {
                            Scalar(property.Name, budget);
                            writer.WritePropertyName(property.Name);
                            await WriteAsync(writer, property.Value, depth + 1);
                        }
                        writer.WriteEndObject();
                        break;
                    }
                    JsonProperty[] properties = element.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal).ToArray();
                    if (properties.Length > CompositionLimits.DefaultMappingEntries)
                        throw new DefinitionException("document-limit");
                    var keys = new HashSet<string>(keyPolicy == CompositionKeyPolicy.Exact
                        ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase);
                    writer.WriteStartObject();
                    foreach (JsonProperty property in properties)
                    {
                        if (!keys.Add(keyPolicy == CompositionKeyPolicy.Exact
                            ? property.Name : property.Name.Normalize(NormalizationForm.FormC)))
                            throw new DefinitionException("duplicate-member");
                        Scalar(property.Name, budget);
                        writer.WritePropertyName(property.Name);
                        await WriteAsync(writer, property.Value, depth + 1);
                    }
                    writer.WriteEndObject();
                    break;
                case JsonValueKind.Array:
                    if (element.GetArrayLength() > CompositionLimits.DefaultSequenceEntries)
                        throw new DefinitionException("document-limit");
                    writer.WriteStartArray();
                    foreach (JsonElement item in element.EnumerateArray()) await WriteAsync(writer, item, depth + 1);
                    writer.WriteEndArray();
                    break;
                case JsonValueKind.String:
                    string text = element.GetString()!;
                    Scalar(text, budget);
                    writer.WriteStringValue(text);
                    break;
                case JsonValueKind.Number:
                    writer.WriteRawValue(CanonicalizeNumber(element, keyPolicy));
                    break;
                case JsonValueKind.True: writer.WriteBooleanValue(true); break;
                case JsonValueKind.False: writer.WriteBooleanValue(false); break;
                case JsonValueKind.Null: writer.WriteNullValue(); break;
                default: throw new DefinitionException("value-invalid");
            }
        }
    }

    internal static bool IsStrictlyOrdinalSorted(JsonElement objectElement, out int count)
    {
        count = 0;
        string? previous = null;
        foreach (JsonProperty property in objectElement.EnumerateObject())
        {
            count++;
            if (previous is not null && string.CompareOrdinal(previous, property.Name) >= 0)
                return false;
            previous = property.Name;
        }
        return true;
    }

    internal static string CanonicalizeNumber(JsonElement value, CompositionKeyPolicy keyPolicy)
    {
        string text = value.GetRawText();
        string[] parts = text.Split(['e', 'E']);
        string digits = parts[0].Replace(".", "", StringComparison.Ordinal).TrimStart('-').TrimStart('0').TrimEnd('0');
        if (digits.Length > 28 || parts.Length > 1 &&
            (!int.TryParse(parts[1], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int exponent)
             || exponent is < -28 or > 28)
            || !decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal number))
            throw new DefinitionException("number-range");
        // Refuse precision loss at the decimal scale boundary, including underflow.
        int decimalPoint = parts[0].IndexOf('.', StringComparison.Ordinal);
        int scale = decimalPoint < 0 ? 0 : parts[0].TrimEnd('0').Length - decimalPoint - 1;
        int power = parts.Length > 1 ? int.Parse(parts[1], CultureInfo.InvariantCulture) : 0;
        if (scale - power > 28 && digits.Length > 0)
            throw new DefinitionException("number-range");
        return number == 0 ? "0" : number.ToString(keyPolicy == CompositionKeyPolicy.Exact
            ? "0.############################" : "G29", CultureInfo.InvariantCulture);
    }

    internal static void ValidateUtf8Scalar(string text)
    {
        try
        {
            _ = StrictUtf8.GetByteCount(text);
        }
        catch (EncoderFallbackException)
        {
            throw new DefinitionException("scalar-invalid");
        }
    }

    private static void Scalar(string text, CompositionBudget budget)
    {
        try
        {
            _ = StrictUtf8.GetByteCount(text);
            budget.Scalar(text);
        }
        catch (Exception exception) when (exception is EncoderFallbackException or CompositionException)
        {
            throw new DefinitionException("scalar-invalid");
        }
    }
}
