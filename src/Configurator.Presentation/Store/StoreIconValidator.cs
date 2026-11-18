namespace Configurator.Presentation.Store;

using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;

/// <summary>Validates and decodes bounded offline PNG data URIs for store project cards.</summary>
public static class StoreIconValidator
{
    public const string PngDataUriPrefix = "data:image/png;base64,";
    public const int MaximumDecodedBytes = 12 * 1024;
    public const int MinimumDimension = 16;
    public const int MaximumDimension = 256;
    public const int MaximumDataUriLength = 22 + ((MaximumDecodedBytes + 2) / 3 * 4);

    private static ReadOnlySpan<byte> PngSignature => [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A];

    public static bool TryDecodePngDataUri(string? dataUri, [NotNullWhen(true)] out byte[]? pngBytes)
    {
        pngBytes = null;
        if (string.IsNullOrEmpty(dataUri)
            || dataUri.Length > MaximumDataUriLength
            || !dataUri.StartsWith(PngDataUriPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        ReadOnlySpan<char> base64 = dataUri.AsSpan(PngDataUriPrefix.Length);
        if (base64.Length < 44 || base64.Length % 4 != 0)
            return false;

        int padding = 0;
        for (int index = 0; index < base64.Length; index++)
        {
            char character = base64[index];
            if (character == '=')
            {
                if (index < base64.Length - 2)
                    return false;
                padding++;
                continue;
            }

            if (padding > 0 || !IsBase64DataChar(character))
                return false;
        }

        int maximumOutputLength = (base64.Length / 4) * 3;
        if (maximumOutputLength - padding > MaximumDecodedBytes)
            return false;

        byte[] buffer = new byte[maximumOutputLength];
        if (!Convert.TryFromBase64Chars(base64, buffer, out int bytesWritten))
            return false;

        if (bytesWritten != maximumOutputLength - padding)
            return false;

        ReadOnlySpan<byte> decoded = buffer.AsSpan(0, bytesWritten);
        if (!IsValidSquarePngHeader(decoded))
            return false;

        pngBytes = bytesWritten == buffer.Length ? buffer : decoded.ToArray();
        return true;
    }

    public static bool IsValidSquarePngHeader(ReadOnlySpan<byte> bytes)
    {
        // Minimum PNG header: 8-byte signature + 4-byte length + 4-byte "IHDR" + 13-byte IHDR + 4-byte CRC = 33 bytes.
        if (bytes.Length < 33 || bytes.Length > MaximumDecodedBytes)
            return false;

        if (!bytes[..8].SequenceEqual(PngSignature))
            return false;

        if (BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(8, 4)) != 13)
            return false;

        if (bytes[12] != (byte)'I' || bytes[13] != (byte)'H' || bytes[14] != (byte)'D' || bytes[15] != (byte)'R')
            return false;

        int width = BinaryPrimitives.ReadInt32BigEndian(bytes.Slice(16, 4));
        int height = BinaryPrimitives.ReadInt32BigEndian(bytes.Slice(20, 4));
        if (width < MinimumDimension || width > MaximumDimension || height != width)
            return false;

        byte bitDepth = bytes[24];
        byte colorType = bytes[25];
        byte compression = bytes[26];
        byte filter = bytes[27];
        byte interlace = bytes[28];

        return bitDepth is 1 or 2 or 4 or 8 or 16
            && colorType is 0 or 2 or 3 or 4 or 6
            && compression == 0
            && filter == 0
            && interlace is 0 or 1;
    }

    private static bool IsBase64DataChar(char character) =>
        character is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '+' or '/';
}
