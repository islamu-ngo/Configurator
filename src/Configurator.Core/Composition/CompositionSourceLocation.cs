namespace Configurator.Core.Composition;

using System.Buffers;
using System.Text;

/// <summary>Value-safe parser coordinate in original raw UTF-8 source bytes.</summary>
internal readonly record struct CompositionSourceLocation
{
    private static ReadOnlySpan<byte> Utf8Bom => [0xEF, 0xBB, 0xBF];

    internal CompositionSourceLocation(int line, int column, long byteOffset)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(line, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(column, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(byteOffset);
        Line = line;
        Column = column;
        ByteOffset = byteOffset;
    }

    internal int Line { get; }
    internal int Column { get; }
    internal long ByteOffset { get; }

    internal static int Utf8BomLength(ReadOnlySpan<byte> originalBytes) =>
        originalBytes.StartsWith(Utf8Bom) ? Utf8Bom.Length : 0;

    internal static CompositionSourceLocation FromUtf8ByteOffset(
        ReadOnlySpan<byte> originalBytes, int bomBytes, long contentByteOffset, bool standaloneCrBreaksLine)
    {
        int clampedContent = (int)Math.Clamp(contentByteOffset, 0, Math.Max(0, originalBytes.Length - bomBytes));
        int target = bomBytes + clampedContent;
        int line = 1;
        int column = 1;
        int index = bomBytes;

        while (index < target)
        {
            byte current = originalBytes[index];
            if (current == (byte)'\n')
            {
                line++;
                column = 1;
                index++;
                continue;
            }

            if (current == (byte)'\r')
            {
                if (index + 1 < originalBytes.Length && originalBytes[index + 1] == (byte)'\n')
                {
                    if (index + 1 < target)
                    {
                        line++;
                        column = 1;
                        index += 2;
                        continue;
                    }

                    column++;
                    index++;
                    continue;
                }

                if (standaloneCrBreaksLine)
                {
                    line++;
                    column = 1;
                    index++;
                    continue;
                }

                column++;
                index++;
                continue;
            }

            OperationStatus status = Rune.DecodeFromUtf8(originalBytes[index..target], out _, out int bytesConsumed);
            if (status == OperationStatus.Done && bytesConsumed > 0)
            {
                column++;
                index += bytesConsumed;
            }
            else
            {
                column++;
                index++;
            }
        }

        return new CompositionSourceLocation(line, column, target);
    }

    internal static CompositionSourceLocation FromJsonLineAndBytePosition(
        ReadOnlySpan<byte> originalBytes, int bomBytes, long? zeroBasedLine, long? zeroBasedBytePositionInLine,
        long fallbackContentByteOffset)
    {
        if (zeroBasedLine is { } targetLine and >= 0 && zeroBasedBytePositionInLine is { } byteInLine and >= 0)
        {
            ReadOnlySpan<byte> content = originalBytes[bomBytes..];
            long currentLine = 0;
            int lineStart = 0;
            for (int index = 0; index < content.Length && currentLine < targetLine; index++)
            {
                if (content[index] == (byte)'\n')
                {
                    currentLine++;
                    lineStart = index + 1;
                }
            }

            if (currentLine == targetLine)
            {
                long contentOffset = Math.Min((long)lineStart + byteInLine, content.Length);
                return FromUtf8ByteOffset(originalBytes, bomBytes, contentOffset, standaloneCrBreaksLine: false);
            }
        }

        return FromUtf8ByteOffset(originalBytes, bomBytes, fallbackContentByteOffset, standaloneCrBreaksLine: false);
    }

    internal static CompositionSourceLocation FromTextCharIndex(string textWithoutBom, int bomBytes, long charIndex)
    {
        int target = (int)Math.Clamp(charIndex, 0, textWithoutBom.Length);
        int line = 1;
        int column = 1;
        long byteOffset = bomBytes;
        int index = 0;

        while (index < target)
        {
            char current = textWithoutBom[index];
            if (current == '\n')
            {
                line++;
                column = 1;
                byteOffset++;
                index++;
                continue;
            }

            if (current == '\r')
            {
                if (index + 1 < textWithoutBom.Length && textWithoutBom[index + 1] == '\n')
                {
                    if (index + 1 < target)
                    {
                        line++;
                        column = 1;
                        byteOffset += 2;
                        index += 2;
                        continue;
                    }

                    column++;
                    byteOffset++;
                    index++;
                    continue;
                }

                line++;
                column = 1;
                byteOffset++;
                index++;
                continue;
            }

            OperationStatus status = Rune.DecodeFromUtf16(textWithoutBom.AsSpan(index, target - index), out Rune rune, out int charsConsumed);
            if (status == OperationStatus.Done && charsConsumed > 0)
            {
                column++;
                byteOffset += rune.Utf8SequenceLength;
                index += charsConsumed;
            }
            else
            {
                column++;
                byteOffset++;
                index++;
            }
        }

        return new CompositionSourceLocation(line, column, byteOffset);
    }

    internal static CompositionSourceLocation FromInvalidUtf8(
        ReadOnlySpan<byte> originalBytes, int bomBytes, bool standaloneCrBreaksLine)
    {
        int line = 1;
        int column = 1;
        int index = bomBytes;

        while (index < originalBytes.Length)
        {
            byte current = originalBytes[index];
            if (current == (byte)'\n')
            {
                line++;
                column = 1;
                index++;
                continue;
            }

            if (current == (byte)'\r')
            {
                if (index + 1 < originalBytes.Length && originalBytes[index + 1] == (byte)'\n')
                {
                    line++;
                    column = 1;
                    index += 2;
                    continue;
                }

                if (standaloneCrBreaksLine)
                {
                    line++;
                    column = 1;
                    index++;
                    continue;
                }

                column++;
                index++;
                continue;
            }

            OperationStatus status = Rune.DecodeFromUtf8(originalBytes[index..], out _, out int bytesConsumed);
            if (status != OperationStatus.Done || bytesConsumed <= 0)
                return new CompositionSourceLocation(line, column, index);

            column++;
            index += bytesConsumed;
        }

        return new CompositionSourceLocation(line, column, originalBytes.Length);
    }
}
