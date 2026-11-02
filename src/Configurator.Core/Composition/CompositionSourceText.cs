namespace Configurator.Core.Composition;

using System.Buffers;
using System.Text;
using System.Text.Json;
using Configurator.Core.Definitions;

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
