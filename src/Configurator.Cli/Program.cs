using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Configurator.Artifacts;
using Configurator.Cli;
using Configurator.Core;
using Configurator.Core.Artifacts;
using Microsoft.Win32.SafeHandles;

return await CliProgram.RunAsync(args);

internal static class CliProgram
{
    internal static async Task<int> RunAsync(string[] args)
    {
        using var cancellation = new CancellationTokenSource();
        ConsoleCancelEventHandler cancel = (_, context) =>
        {
            context.Cancel = true;
            cancellation.Cancel();
        };
        // Unix Console signal registration initializes the terminal and emits keypad controls.
        if (OperatingSystem.IsWindows()) Console.CancelKeyPress += cancel;
        try
        {
            using PosixSignalRegistration? interrupt = OperatingSystem.IsWindows() ? null
                : PosixSignalRegistration.Create(PosixSignal.SIGINT, context =>
                {
                    context.Cancel = true;
                    cancellation.Cancel();
                });
            using PosixSignalRegistration? termination = OperatingSystem.IsWindows() ? null
                : PosixSignalRegistration.Create(PosixSignal.SIGTERM, context =>
                {
                    context.Cancel = true;
                    cancellation.Cancel();
                });
            CliMode mode = CliArgumentPreflight.Mode(args);
            // Non-owning handles can hang async pipe disposal after cancellation.
            using SafePipeHandle? inputHandle = OperatingSystem.IsWindows() ? null : new((IntPtr)0, ownsHandle: true);
            using SafePipeHandle? outputHandle = OperatingSystem.IsWindows() ? null : new((IntPtr)1, ownsHandle: true);
            using SafePipeHandle? errorHandle = OperatingSystem.IsWindows() ? null : new((IntPtr)2, ownsHandle: true);
            using SafeFileHandle? outputFileHandle = OperatingSystem.IsWindows() ? null : new((IntPtr)1, ownsHandle: false);
            using SafeFileHandle? errorFileHandle = OperatingSystem.IsWindows() ? null : new((IntPtr)2, ownsHandle: false);
            using Stream standardOutput = OpenStandardStream(1, outputHandle, outputFileHandle);
            using Stream standardError = OpenStandardStream(2, errorHandle, errorFileHandle);
            var writer = new SystemWriter(standardOutput);
            var error = new SystemWriter(standardError);
            var io = new CliIo(new SystemInput(inputHandle), writer, error, new PublicDefinitionFiles(), 65_536, 4 * 1024 * 1024);
            CliInvocation invocation;
            try
            {
                invocation = new(args, mode, io);
            }
            catch (ArgumentException)
            {
                invocation = new(["doctor", mode == CliMode.Machine ? "--machine" : "--text", "--invalid-arguments"], mode, io);
            }
            return (int)await new CliApplication().RunAsync(invocation, cancellation.Token);
        }
        catch (ArgumentException)
        {
            return (int)CliExitCode.Usage;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return (int)CliExitCode.Io;
        }
        catch (Exception)
        {
            return (int)CliExitCode.Internal;
        }
        finally { if (OperatingSystem.IsWindows()) Console.CancelKeyPress -= cancel; }
    }

    private static Stream OpenStandardStream(int descriptor, SafePipeHandle? handle, SafeFileHandle? fileHandle = null)
    {
        if (handle is not null)
        {
            try
            {
                return new NamedPipeClientStream(descriptor == 0 ? PipeDirection.In : PipeDirection.Out,
                isAsync: true, isConnected: true, handle);
            }
            catch (IOException)
            {
                // Console output streams also initialize the terminal driver on their first write.
                if (fileHandle is not null) return new FileStream(fileHandle, FileAccess.Write,
                    bufferSize: 1, isAsync: false);
            }
        }
        return descriptor switch
        {
            0 => Console.OpenStandardInput(),
            1 => Console.OpenStandardOutput(),
            _ => Console.OpenStandardError()
        };
    }

    private sealed class SystemInput(SafePipeHandle? standardInput) : ISetupCliInput
    {
        public async ValueTask<CliInputBuffer> ReadAsync(string path, int maximumBytes, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (maximumBytes is < 1 or > 4 * 1024 * 1024) throw new IOException("input-bound");
            if (path == "-" && !Console.IsInputRedirected) throw new IOException("stdin-redirect-required");
            byte[] bytes = new byte[maximumBytes + 1];
            try
            {
                await using Stream stream = path == "-"
                    ? OpenStandardStream(0, standardInput)
                    : new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 8192,
                        FileOptions.Asynchronous | FileOptions.SequentialScan);
                int total = 0;
                while (true)
                {
                    int read = await stream.ReadAsync(bytes.AsMemory(total, Math.Min(8192, bytes.Length - total)), cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    if (read == 0) return new CliInputBuffer(bytes, total);
                    total += read;
                    if (total > maximumBytes) throw new IOException("input-bound");
                }
            }
            catch
            {
                CryptographicOperations.ZeroMemory(bytes);
                throw;
            }
        }
    }

    internal sealed class SystemWriter(Stream standardOutput, IProtectedArtifactWriter? protectedWriter = null) : ISetupCliWriter
    {
        private readonly IProtectedArtifactWriter _protectedWriter = protectedWriter ?? new ProtectedArtifactWriter();

        public async ValueTask<ProtectedArtifactStatus?> CheckArtifactDestinationAsync(string path, CancellationToken cancellationToken)
        {
            // Exercise protected admission with inert bytes, then discard the unpublished preparation.
            using ProtectedArtifactPreparation preparation = await _protectedWriter.PrepareAsync(
                ArtifactKind.Configuration, path, new byte[] { 0 }, cancellationToken);
            return preparation.RejectionStatus;
        }

        public async ValueTask WriteAsync(string path, ReadOnlyMemory<byte> bytes, int maximumBytes, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (bytes.Length > maximumBytes) throw new IOException("output-bound");
            if (path == "-")
            {
                await standardOutput.WriteAsync(bytes, cancellationToken);
                await standardOutput.FlushAsync(cancellationToken);
                return;
            }
            throw new IOException("protected-output-required");
        }

        public async ValueTask<CliPublicationResult> WriteArtifactAsync(string path, ArtifactKind kind,
            ReadOnlyMemory<byte> bytes, int maximumBytes, CancellationToken cancellationToken)
        {
            if (bytes.Length > maximumBytes) throw new IOException("output-bound");
            if (path == "-")
            {
                if (!ArtifactPolicy.TryProjectPublic(kind, bytes, out ReadOnlyMemory<byte> projection))
                    throw new IOException("protected-output-required");
                await WriteAsync(path, projection, maximumBytes, cancellationToken);
                return new([new(null, DeliveredToStandardOutput: true)]);
            }
            ProtectedArtifactPreparation preparation;
            try { preparation = await _protectedWriter.PrepareAsync(kind, path, bytes, cancellationToken); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return new([new(null)], Cancelled: true);
            }
            ProtectedArtifactCommitResult? result = null;
            bool unknown = false;
            bool cleanupFailed = false;
            bool cancelled = false;
            try { result = await preparation.CommitAsync(cancellationToken); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                result = preparation.EstablishedResult;
                cancelled = true;
            }
            catch (Exception) { result = preparation.EstablishedResult; unknown = result is null; }
            finally
            {
                try { preparation.Dispose(); } catch (Exception) { cleanupFailed = true; }
                cleanupFailed |= preparation.CleanupFailed;
            }
            return new([new(result, unknown)], cleanupFailed || result?.CleanupFailed == true,
                cancelled || cancellationToken.IsCancellationRequested);
        }

        public async ValueTask<CliPublicationResult> WriteBundleAsync(
            IReadOnlyList<DefinitionArtifact> artifacts, string directory, int maximumBytes,
            Action<string, CliPublicationOutcome> outcomeObserver, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(outcomeObserver);
            if (artifacts.Any(item => item.ByteLength > maximumBytes))
                throw new IOException("output-bound");
            ProtectedArtifactBundleResult result = await ProtectedArtifactBundlePublisher.PublishAsync(
                _protectedWriter, artifacts, directory,
                outcome => outcomeObserver(outcome.ArtifactId, new(outcome.Result)), cancellationToken);
            return new(result.Outcomes.Select(item => new CliPublicationOutcome(item.Result)).ToArray(),
                result.CleanupFailed, result.CompletionCode == ProtectedArtifactBundleCompletionCode.Cancelled);
        }
    }
}
