namespace Configurator.Core.Definitions;

using System.Collections.Frozen;
using Configurator.Core.Composition;

/// <summary>A definition-only span. Lines and columns are one-based; the end is exclusive.</summary>
public readonly record struct DefinitionSourceSpan
{
    public DefinitionSourceSpan(int documentIndex, int startLine, int startColumn, int endLine, int endColumn)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(documentIndex);
        ArgumentOutOfRangeException.ThrowIfLessThan(startLine, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(startColumn, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(endLine, startLine);
        ArgumentOutOfRangeException.ThrowIfLessThan(endColumn, 1);
        if (endLine == startLine && endColumn < startColumn)
            throw new ArgumentOutOfRangeException(nameof(endColumn));
        DocumentIndex = documentIndex;
        StartLine = startLine;
        StartColumn = startColumn;
        EndLine = endLine;
        EndColumn = endColumn;
    }

    public int DocumentIndex { get; }
    public int StartLine { get; }
    public int StartColumn { get; }
    public int EndLine { get; }
    public int EndColumn { get; }
}
