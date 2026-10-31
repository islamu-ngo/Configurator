namespace Configurator.Core.Definitions;

using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Configurator.Core.Composition;

internal static class DefinitionJson
{
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
        budget ??= new CompositionBudget(SetupCompositionLimits.Default);
        int normalizedNodes = 0;
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
            await WriteAsync(writer, value, initialDepth);
        if (buffer.WrittenCount > SetupCompositionLimits.DefaultAggregateSourceBytes)
            throw new SetupDefinitionException("document-limit");
        return buffer.WrittenSpan.ToArray();

        async ValueTask WriteAsync(Utf8JsonWriter writer, JsonElement value, int depth)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try { budget.Node(depth); }
            catch (SetupCompositionException) { throw new SetupDefinitionException("document-limit"); }
            if (++normalizedNodes % 1024 == 0 && cooperativeYield is not null)
            {
                await cooperativeYield(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
            }
            switch (value.ValueKind)
            {
                case JsonValueKind.Object:
                    JsonProperty[] properties = value.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal).ToArray();
                    if (properties.Length > SetupCompositionLimits.DefaultMappingEntries)
                        throw new SetupDefinitionException("document-limit");
                    var keys = new HashSet<string>(keyPolicy == CompositionKeyPolicy.Exact
                        ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase);
                    writer.WriteStartObject();
                    foreach (JsonProperty property in properties)
                    {
                        if (!keys.Add(keyPolicy == CompositionKeyPolicy.Exact
                            ? property.Name : property.Name.Normalize(NormalizationForm.FormC)))
                            throw new SetupDefinitionException("duplicate-member");
                        Scalar(property.Name, budget);
                        writer.WritePropertyName(property.Name);
                        await WriteAsync(writer, property.Value, depth + 1);
                    }
                    writer.WriteEndObject();
                    break;
                case JsonValueKind.Array:
                    if (value.GetArrayLength() > SetupCompositionLimits.DefaultSequenceEntries)
                        throw new SetupDefinitionException("document-limit");
                    writer.WriteStartArray();
                    foreach (JsonElement item in value.EnumerateArray()) await WriteAsync(writer, item, depth + 1);
                    writer.WriteEndArray();
                    break;
                case JsonValueKind.String:
                    string text = value.GetString()!;
                    Scalar(text, budget);
                    writer.WriteStringValue(text);
                    break;
                case JsonValueKind.Number:
                    writer.WriteRawValue(Number(value, keyPolicy));
                    break;
                case JsonValueKind.True: writer.WriteBooleanValue(true); break;
                case JsonValueKind.False: writer.WriteBooleanValue(false); break;
                case JsonValueKind.Null: writer.WriteNullValue(); break;
                default: throw new SetupDefinitionException("value-invalid");
            }
        }
    }

    private static string Number(JsonElement value, CompositionKeyPolicy keyPolicy)
    {
        string text = value.GetRawText();
        string[] parts = text.Split(['e', 'E']);
        string digits = parts[0].Replace(".", "", StringComparison.Ordinal).TrimStart('-').TrimStart('0').TrimEnd('0');
        if (digits.Length > 28 || parts.Length > 1 &&
            (!int.TryParse(parts[1], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int exponent)
             || exponent is < -28 or > 28)
            || !decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal number))
            throw new SetupDefinitionException("number-range");
        // Refuse precision loss at the decimal scale boundary, including underflow.
        int decimalPoint = parts[0].IndexOf('.', StringComparison.Ordinal);
        int scale = decimalPoint < 0 ? 0 : parts[0].TrimEnd('0').Length - decimalPoint - 1;
        int power = parts.Length > 1 ? int.Parse(parts[1], CultureInfo.InvariantCulture) : 0;
        if (scale - power > 28 && digits.Length > 0)
            throw new SetupDefinitionException("number-range");
        return number == 0 ? "0" : number.ToString(keyPolicy == CompositionKeyPolicy.Exact
            ? "0.############################" : "G29", CultureInfo.InvariantCulture);
    }

    private static void Scalar(string text, CompositionBudget budget)
    {
        try
        {
            _ = new UTF8Encoding(false, true).GetByteCount(text);
            budget.Scalar(text);
        }
        catch (Exception exception) when (exception is EncoderFallbackException or SetupCompositionException)
        {
            throw new SetupDefinitionException("scalar-invalid");
        }
    }
}
