namespace Configurator.Core.Definitions;

using System.Text.Json;
using System.Security.Cryptography;
using System.Buffers;
using Configurator.Core.Composition;

/// <summary>Admits one private value tree and explicit omissions without retaining its source bytes.</summary>
public static class SetupAnswerSnapshotCodec
{
    public const int ContractVersion = 1;

    /// <summary>Returns caller-owned restricted bytes. Callers retain publication authority and cleanup ownership.</summary>
    public static byte[] Serialize(SetupAnswerSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var buffer = new ArrayBufferWriter<byte>();
        try
        {
            using (var writer = new Utf8JsonWriter(buffer))
            {
                writer.WriteStartObject();
                writer.WriteNumber("answerContractVersion", ContractVersion);
                writer.WriteStartObject("values");
                foreach ((string name, JsonElement value) in snapshot.Values.OrderBy(member => member.Key, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(name);
                    value.WriteTo(writer);
                }
                writer.WriteEndObject();
                writer.WriteStartArray("omitted");
                foreach (DefinitionPath path in snapshot.Omitted.OrderBy(path => path.ToPointer(), StringComparer.Ordinal))
                    writer.WriteStringValue(path.ToPointer());
                writer.WriteEndArray();
                writer.WriteEndObject();
            }
            if (buffer.WrittenCount > SetupCompositionLimits.DefaultAggregateSourceBytes)
                throw new SetupDefinitionException("answer-limit");
            return buffer.WrittenSpan.ToArray();
        }
        finally { buffer.Clear(); }
    }

    public static SetupAnswerSnapshot ParseJson(ReadOnlyMemory<byte> bytes, long inputRevision = 0)
    {
        byte[] canonical = DefinitionProfile.CanonicalizeJson(bytes);
        try
        {
            using JsonDocument document = JsonDocument.Parse(canonical);
            JsonElement root = document.RootElement;
            if (root.EnumerateObject().Any(member => member.Name is not ("answerContractVersion" or "values" or "omitted"))
                || root.GetProperty("answerContractVersion").GetInt32() != ContractVersion)
                throw new SetupDefinitionException("answer-contract");
            JsonElement values = root.GetProperty("values");
            JsonElement omitted = root.GetProperty("omitted");
            if (values.ValueKind != JsonValueKind.Object || omitted.ValueKind != JsonValueKind.Array)
                throw new SetupDefinitionException("answer-invalid");
            return new SetupAnswerSnapshot(inputRevision, values.EnumerateObject().ToDictionary(
                member => member.Name, member => member.Value, StringComparer.Ordinal),
                omitted.EnumerateArray().Select(item => DefinitionPath.Parse(item.GetString()!)));
        }
        catch (SetupDefinitionException) { throw; }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException
            or KeyNotFoundException or ArgumentException or FormatException or OverflowException)
        {
            throw new SetupDefinitionException("answer-invalid");
        }
        finally { CryptographicOperations.ZeroMemory(canonical); }
    }
}
