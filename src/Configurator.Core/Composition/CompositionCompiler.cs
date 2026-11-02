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
        CompileAsync(source, CompositionImmediatePublicationCommitBarrier.Instance, cancellationToken);

    public async ValueTask<CompositionResult> CompileAsync(
        CompositionSource source, ICompositionPublicationCommitBarrier publicationCommitBarrier,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(publicationCommitBarrier);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            CompositionMap normalized;
            bool barrierAlreadyApplied = false;
            switch (source)
            {
                case CompositionJsonSource json:
                    normalized = CompositionNormalizer.ParseJson(
                        json.Bytes, _limits, cancellationToken);
                    break;
                case CompositionYamlSource yaml:
                    normalized = CompositionYamlParser.Parse(
                        yaml.Bytes, _limits, cancellationToken);
                    break;
                case CompositionDirectorySource directory:
                    normalized = await CompositionDirectoryReader.ReadAsync(
                        directory, _limits, publicationCommitBarrier, cancellationToken).ConfigureAwait(false);
                    barrierAlreadyApplied = true;
                    break;
                default:
                    throw new CompositionException(CompositionFailureCode.InvalidSource);
            }

            cancellationToken.ThrowIfCancellationRequested();
            byte[] normalizedJson = CompositionNormalizer.WriteJson(normalized, cancellationToken);
            if (normalizedJson.Length > _limits.AggregateSourceBytes)
                throw new CompositionException(CompositionFailureCode.LimitExceeded);
            CompositionResult result = CompositionResult.Success(normalizedJson);
            if (!barrierAlreadyApplied)
                await publicationCommitBarrier.AwaitPublicationCommitAsync(cancellationToken).ConfigureAwait(false);
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
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
            or NotSupportedException or ArgumentException or OverflowException)
        {
            return CompositionResult.Failed(CompositionFailureCode.InvalidSource);
        }
    }

}
