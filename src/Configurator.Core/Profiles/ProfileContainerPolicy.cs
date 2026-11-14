namespace Configurator.Core.Profiles;

using System.Buffers;
using System.Buffers.Text;
using System.Text;
using System.Text.Json;

public enum ProfileFailureCode
{
    InvalidContainer,
    InvalidPayload,
    AuthenticationFailed,
    LimitExceeded,
    InvalidPassphrase,
    ModeRestricted,
    Busy
}

/// <summary>Value-free failure at the profile boundary; original parser/crypto messages are not retained.</summary>
public sealed class ProfileException(ProfileFailureCode code) : Exception(code.ToString())
{
    public ProfileFailureCode Code { get; } = code;
    public override string ToString() => $"{nameof(ProfileException)}:{Code}";
}

/// <summary>Bounds standard JSON envelopes before either maintained implementation can derive keys.</summary>
public static class ProfileContainerPolicy
{
    public const int MaximumContainerBytes = 4 * 1024 * 1024;
    public const int MaximumPayloadBytes = 2 * 1024 * 1024;
    public const int MaximumRecipients = 8;
    public const int MaximumPassphraseUtf8Bytes = 1024;
    public const int MinimumPassphraseScalars = 15;
    public const int Iterations = 600_000;
    public const string KeyAlgorithm = "PBES2-HS512+A256KW";
    public const string ContentAlgorithm = "A256GCM";
    public static void ValidatePassphrase(ReadOnlySpan<byte> value, bool creating)
    {
        if (value.Length is 0 or > MaximumPassphraseUtf8Bytes)
            throw new ProfileException(ProfileFailureCode.InvalidPassphrase);
        int count = 0;
        while (!value.IsEmpty)
        {
            if (Rune.DecodeFromUtf8(value, out _, out int consumed) != OperationStatus.Done)
                throw new ProfileException(ProfileFailureCode.InvalidPassphrase);
            count++;
            value = value[consumed..];
        }
        if (creating && count < MinimumPassphraseScalars)
            throw new ProfileException(ProfileFailureCode.InvalidPassphrase);
    }

    /// <summary>Returns only the unauthenticated recipient count; routing never grants authority.</summary>
    public static int Admit(ReadOnlyMemory<byte> encoded)
    {
        if (encoded.Length is 0 or > MaximumContainerBytes)
            throw new ProfileException(ProfileFailureCode.LimitExceeded);
        try
        {
            using JsonDocument document = JsonDocument.Parse(encoded, new JsonDocumentOptions
            {
                AllowDuplicateProperties = false,
                MaxDepth = 8
            });
            CheckBudget(document.RootElement);
            JsonElement root = document.RootElement;
            bool general = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("recipients", out _);
            Exact(root, general
                ? ["protected", "recipients", "iv", "ciphertext", "tag"]
                : ["protected", "header", "encrypted_key", "iv", "ciphertext", "tag"]);
            byte[] protectedBytes = Decode(root.GetProperty("protected"), 2, 2048);
            using JsonDocument headerDocument = JsonDocument.Parse(protectedBytes, new JsonDocumentOptions
            {
                AllowDuplicateProperties = false,
                MaxDepth = 8
            });
            JsonElement header = headerDocument.RootElement;
            CheckBudget(header);
            if (header.ValueKind != JsonValueKind.Object ||
                header.EnumerateObject().Any(property => property.Name is not ("enc" or "typ" or "alg" or "p2c" or "p2s")) ||
                header.GetProperty("enc").GetString() != ContentAlgorithm ||
                header.GetProperty("typ").GetString() != "cfgvault")
                throw new ProfileException(ProfileFailureCode.InvalidContainer);
            _ = Decode(root.GetProperty("iv"), 12, 12);
            _ = Decode(root.GetProperty("tag"), 16, 16);
            _ = Decode(root.GetProperty("ciphertext"), 1, MaximumPayloadBytes);
            JsonElement recipients = general ? root.GetProperty("recipients") : default;
            if (general && (recipients.ValueKind != JsonValueKind.Array ||
                recipients.GetArrayLength() is < 1 or > MaximumRecipients))
                throw new ProfileException(ProfileFailureCode.InvalidContainer);
            int count = general ? recipients.GetArrayLength() : 1;
            for (int index = 0; index < count; index++)
            {
                JsonElement recipient = general ? recipients[index] : root;
                if (general) Exact(recipient, ["header", "encrypted_key"]);
                _ = Decode(recipient.GetProperty("encrypted_key"), 40, 40);
                JsonElement local = recipient.GetProperty("header");
                if (local.ValueKind != JsonValueKind.Object)
                    throw new ProfileException(ProfileFailureCode.InvalidContainer);
                var merged = header.EnumerateObject().ToDictionary(property => property.Name,
                    property => property.Value, StringComparer.Ordinal);
                foreach (JsonProperty property in local.EnumerateObject())
                    if (!merged.TryAdd(property.Name, property.Value))
                        throw new ProfileException(ProfileFailureCode.InvalidContainer);
                if (merged.Count is < 5 or > 6 ||
                    merged.Keys.Any(key => key is not ("enc" or "typ" or "alg" or "p2c" or "p2s" or "kid")) ||
                    merged["alg"].GetString() != KeyAlgorithm ||
                    merged["p2c"].ValueKind != JsonValueKind.Number ||
                    merged["p2c"].GetRawText() != "600000" ||
                    (merged.TryGetValue("kid", out JsonElement kid) &&
                        (kid.ValueKind != JsonValueKind.String || kid.GetString()!.Length > 64)))
                    throw new ProfileException(ProfileFailureCode.InvalidContainer);
                _ = Decode(merged["p2s"], 12, 64);
            }
            return count;
        }
        catch (ProfileException) { throw; }
        catch (Exception exception) when (exception is JsonException or FormatException or InvalidOperationException
            or KeyNotFoundException or ArgumentException)
        {
            throw new ProfileException(ProfileFailureCode.InvalidContainer);
        }
    }

    private static byte[] Decode(JsonElement element, int minimum, int maximum)
    {
        if (element.ValueKind != JsonValueKind.String)
            throw new ProfileException(ProfileFailureCode.InvalidContainer);
        string text = element.GetString()!;
        if (text.Length > (maximum + 2) / 3 * 4)
            throw new ProfileException(ProfileFailureCode.LimitExceeded);
        byte[] decoded = Base64Url.DecodeFromChars(text);
        if (decoded.Length < minimum || decoded.Length > maximum ||
            Base64Url.EncodeToString(decoded) != text)
            throw new ProfileException(ProfileFailureCode.InvalidContainer);
        return decoded;
    }

    private static void Exact(JsonElement value, string[] names)
    {
        if (value.ValueKind != JsonValueKind.Object ||
            value.EnumerateObject().Count() != names.Length ||
            value.EnumerateObject().Any(property => !names.Contains(property.Name, StringComparer.Ordinal)))
            throw new ProfileException(ProfileFailureCode.InvalidContainer);
    }

    private static void CheckBudget(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            int count = 0;
            foreach (JsonProperty property in value.EnumerateObject())
            {
                if (++count > 16 || property.Name.Length > 64)
                    throw new ProfileException(ProfileFailureCode.LimitExceeded);
                CheckBudget(property.Value);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            if (value.GetArrayLength() > MaximumRecipients)
                throw new ProfileException(ProfileFailureCode.LimitExceeded);
            foreach (JsonElement child in value.EnumerateArray()) CheckBudget(child);
        }
        else if (value.ValueKind == JsonValueKind.String &&
            value.GetString()!.Length > 3 * 1024 * 1024)
            throw new ProfileException(ProfileFailureCode.LimitExceeded);
    }
}
