namespace Configurator.Core.Composition;

using System.Buffers;
using System.Text;
using System.Text.Json;
using Configurator.Core.Definitions;

internal sealed class CompositionBudget
{
    private readonly CompositionLimits _limits;
    private int _nodes;
    private int _scalarCharacters;
    private int _parserEvents;

    internal CompositionBudget(CompositionLimits limits) => _limits = limits;
    internal int Nodes => _nodes;

    internal void ParserEvent()
    {
        try { _parserEvents = checked(_parserEvents + 1); }
        catch (OverflowException) { throw new CompositionException(CompositionFailureCode.LimitExceeded); }
        if (_parserEvents > _limits.ParserEvents)
            throw new CompositionException(CompositionFailureCode.LimitExceeded);
    }

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
