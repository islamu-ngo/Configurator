namespace Configurator.Core.Composition;

public sealed class CompositionCompiler
{
    private readonly CompositionLimits _limits;

    public CompositionCompiler() : this(CompositionLimits.Default) { }

    public CompositionCompiler(CompositionLimits limits)
    {
        ArgumentNullException.ThrowIfNull(limits);
        limits.Validate();
        _limits = limits with { };
    }

    public ValueTask<CompositionResult> CompileAsync(
        CompositionSource source, CancellationToken cancellationToken = default) =>
        CompileAsync([source], cancellationToken);

    /// <summary>Compiles ordered, path-free fragments under one aggregate semantic budget.</summary>
    public ValueTask<CompositionResult> CompileAsync(
        IEnumerable<CompositionSource> sources,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sources);
        return ValueTask.FromResult(Compile(sources, cancellationToken));
    }

    private CompositionResult Compile(IEnumerable<CompositionSource> sources, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var budget = new CompositionBudget(_limits);
            var fragments = new List<CompositionMap>();
            int bytes = 0;
            foreach (CompositionSource source in sources)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ReadOnlyMemory<byte> input = source switch
                {
                    CompositionJsonSource json => json.Bytes,
                    CompositionYamlSource yaml => yaml.Bytes,
                    _ => throw new CompositionException(CompositionFailureCode.InvalidSource)
                };
                bytes = checked(bytes + input.Length);
                if (bytes > _limits.AggregateSourceBytes)
                    throw new CompositionException(CompositionFailureCode.LimitExceeded);
                fragments.Add(source is CompositionJsonSource
                    ? CompositionNormalizer.ParseJson(input, _limits, cancellationToken, budget)
                    : CompositionYamlParser.Parse(input, _limits, cancellationToken, budget));
            }
            CompositionMap normalized = CompositionNormalizer.Merge(fragments, _limits, cancellationToken);
            if (normalized.Entries.Count > _limits.MappingEntries)
                throw new CompositionException(CompositionFailureCode.LimitExceeded);
            cancellationToken.ThrowIfCancellationRequested();
            byte[] normalizedJson = CompositionNormalizer.WriteJson(normalized, cancellationToken);
            if (normalizedJson.Length > _limits.AggregateSourceBytes)
                throw new CompositionException(CompositionFailureCode.LimitExceeded);
            CompositionResult result = CompositionResult.Success(normalizedJson);
            cancellationToken.ThrowIfCancellationRequested();
            return result;
        }
        catch (OperationCanceledException)
        {
            return CompositionResult.Failed(CompositionFailureCode.Cancelled);
        }
        catch (CompositionException exception)
        {
            return CompositionResult.Failed(exception.Code);
        }
        catch (OverflowException)
        {
            return CompositionResult.Failed(CompositionFailureCode.LimitExceeded);
        }
        catch (ArgumentException)
        {
            return CompositionResult.Failed(CompositionFailureCode.InvalidSource);
        }
    }

}
