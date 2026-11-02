namespace Configurator.Core.Definitions;

using System.Text.Json;
using Configurator.Core.Composition;

/// <summary>One combined traversal budget, independent of parser admission and display paging.</summary>
internal sealed class DefinitionImportWork(CancellationToken cancellationToken,
    Func<CancellationToken, ValueTask>? cooperativeYield = null)
{
    private const int YieldQuantum = 1024;
    internal CancellationToken CancellationToken => cancellationToken;
    internal Func<CancellationToken, ValueTask>? CooperativeYield => cooperativeYield;
    internal static T CompleteSynchronously<T>(ValueTask<T> operation)
    {
        if (!operation.IsCompleted) throw new InvalidOperationException("synchronous-operation-incomplete");
        return operation.GetAwaiter().GetResult();
    }

    internal int Units { get; private set; }
    internal int InverseNodes { get; private set; }
    internal int ValidationNodes { get; private set; }
    internal async ValueTask VisitAsync()
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Units == CompositionLimits.DefaultParserEvents) throw new DefinitionImportLimitException();
        Units++;
        if (cooperativeYield is not null && Units % YieldQuantum == 0)
        {
            await cooperativeYield(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
        }
    }
    internal async ValueTask InverseAsync()
    {
        await VisitAsync();
        if (InverseNodes == CompositionLimits.DefaultNormalizedNodes) throw new DefinitionImportLimitException();
        InverseNodes++;
    }
    internal async ValueTask ValidationAsync()
    {
        await VisitAsync();
        if (ValidationNodes == CompositionLimits.DefaultNormalizedNodes) throw new DefinitionImportLimitException();
        ValidationNodes++;
        // Validation resolves paths, dependencies and constraints in addition to visiting each node.
        if (cooperativeYield is not null && ValidationNodes % 256 == 0)
        {
            await cooperativeYield(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
        }
    }
}
