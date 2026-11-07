using Configurator.Core.Composition;

namespace Configurator.Artifacts.Composition;

/// <summary>Acquires a native directory, privately compiles it, and rechecks it after one barrier.</summary>
public sealed class CompositionDirectoryCompiler
{
    public ValueTask<CompositionResult> CompileAsync(
        string rootDirectory, CancellationToken cancellationToken = default) =>
        CompileAsync(rootDirectory, CompositionImmediatePublicationCommitBarrier.Instance, cancellationToken);

    public async ValueTask<CompositionResult> CompileAsync(
        string rootDirectory, ICompositionPublicationCommitBarrier barrier,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(barrier);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await CompositionDirectoryReader.ReadAsync(
                rootDirectory, new CompositionDirectoryLimits(), barrier, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return CompositionResult.Failed(CompositionFailureCode.Cancelled);
        }
        catch (CompositionException exception)
        {
            return CompositionResult.Failed(exception.Code);
        }
        catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException)
        {
            return CompositionResult.Failed(CompositionFailureCode.UnsupportedFilesystem);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
            or ArgumentException or NotSupportedException or System.Runtime.InteropServices.ExternalException)
        {
            return CompositionResult.Failed(CompositionFailureCode.InvalidSource);
        }
        catch (OverflowException)
        {
            return CompositionResult.Failed(CompositionFailureCode.LimitExceeded);
        }
    }
}

internal sealed class CompositionException(CompositionFailureCode code) : Exception
{
    internal CompositionFailureCode Code { get; } = code;
}
