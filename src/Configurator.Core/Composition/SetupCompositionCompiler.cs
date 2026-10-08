namespace Configurator.Core.Composition;

public sealed class SetupCompositionCompiler
{
    private readonly SetupCompositionLimits _limits;

    public SetupCompositionCompiler() : this(SetupCompositionLimits.Default) { }

    public SetupCompositionCompiler(SetupCompositionLimits limits)
    {
        ArgumentNullException.ThrowIfNull(limits);
        limits.Validate();
        _limits = limits with { };
    }

    public ValueTask<SetupCompositionResult> CompileAsync(
        SetupCompositionSource source, CancellationToken cancellationToken = default) =>
        CompileAsync(source, SetupCompositionImmediatePublicationCommitBarrier.Instance, cancellationToken);

    public async ValueTask<SetupCompositionResult> CompileAsync(
        SetupCompositionSource source, ISetupCompositionPublicationCommitBarrier publicationCommitBarrier,
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
                case SetupCompositionJsonSource json:
                    normalized = SetupCompositionNormalizer.ParseJson(
                        json.Bytes, _limits, cancellationToken);
                    break;
                case SetupCompositionYamlSource yaml:
                    normalized = SetupCompositionYamlParser.Parse(
                        yaml.Bytes, _limits, cancellationToken);
                    break;
                case SetupCompositionDirectorySource directory:
                    normalized = await SetupCompositionDirectoryReader.ReadAsync(
                        directory, _limits, publicationCommitBarrier, cancellationToken).ConfigureAwait(false);
                    barrierAlreadyApplied = true;
                    break;
                default:
                    throw new SetupCompositionException(SetupCompositionFailureCode.InvalidSource);
            }

            cancellationToken.ThrowIfCancellationRequested();
            byte[] normalizedJson = SetupCompositionNormalizer.WriteJson(normalized, cancellationToken);
            if (normalizedJson.Length > _limits.AggregateSourceBytes)
                throw new SetupCompositionException(SetupCompositionFailureCode.LimitExceeded);
            SetupCompositionResult result = SetupCompositionResult.Success(normalizedJson);
            if (!barrierAlreadyApplied)
                await publicationCommitBarrier.AwaitPublicationCommitAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return result;
        }
        catch (OperationCanceledException)
        {
            return SetupCompositionResult.Failed(SetupCompositionFailureCode.Cancelled);
        }
        catch (SetupCompositionException exception)
        {
            return SetupCompositionResult.Failed(exception.Code);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
            or NotSupportedException or ArgumentException or OverflowException)
        {
            return SetupCompositionResult.Failed(SetupCompositionFailureCode.InvalidSource);
        }
    }

}
