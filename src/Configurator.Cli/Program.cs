using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Configurator.Artifacts;
using Configurator.Cli;
using Configurator.Core;
using Configurator.Core.Artifacts;
using Microsoft.Win32.SafeHandles;

return await SetupCliProgram.RunAsync(args);

internal static class SetupCliProgram
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
            SetupCliMode mode = SetupCliArgumentPreflight.Mode(args);
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
            var io = new SetupCliIo(new SystemInput(inputHandle), writer, error, 65_536, 4 * 1024 * 1024);
            SetupCliInvocation invocation;
            try
            {
                invocation = new(args, mode, io);
            }
            catch (ArgumentException)
            {
                invocation = new(["doctor", mode == SetupCliMode.Machine ? "--machine" : "--text", "--invalid-arguments"], mode, io);
            }
            return (int)await new SetupCliApplication().RunAsync(invocation, cancellation.Token);
        }
        catch (ArgumentException)
        {
            return (int)SetupCliExitCode.Usage;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return (int)SetupCliExitCode.Io;
        }
        catch (Exception)
        {
            return (int)SetupCliExitCode.Internal;
        }
        finally { if (OperatingSystem.IsWindows()) Console.CancelKeyPress -= cancel; }
    }

    private static Stream OpenStandardStream(int descriptor, SafePipeHandle? handle, SafeFileHandle? fileHandle = null)
    {
        if (handle is not null)
        {
            try { return new NamedPipeClientStream(descriptor == 0 ? PipeDirection.In : PipeDirection.Out,
                isAsync: true, isConnected: true, handle); }
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
        public async ValueTask<SetupCliInputBuffer> ReadAsync(string path, int maximumBytes, CancellationToken cancellationToken)
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
                    if (read == 0) return new SetupCliInputBuffer(bytes, total);
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

    private sealed class SystemWriter(Stream standardOutput) : ISetupCliWriter
    {
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

        public async ValueTask<SetupCliPublicationResult> WriteArtifactAsync(string path, SetupArtifactKind kind,
            ReadOnlyMemory<byte> bytes, int maximumBytes, CancellationToken cancellationToken)
        {
            if (bytes.Length > maximumBytes) throw new IOException("output-bound");
            if (path == "-")
            {
                if (!SetupArtifactPolicy.TryProjectPublic(kind, bytes, out ReadOnlyMemory<byte> projection))
                    throw new IOException("protected-output-required");
                await WriteAsync(path, projection, maximumBytes, cancellationToken);
                return new([new(ProtectedArtifactStatus.Written)]);
            }
            var writer = new ProtectedArtifactWriter();
            ProtectedArtifactPreparation preparation;
            try { preparation = await writer.PrepareAsync(kind, path, bytes, cancellationToken); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return new([new(null)], Cancelled: true);
            }
            ProtectedArtifactStatus? status = null;
            bool unknown = false;
            bool cleanupFailed = false;
            bool cancelled = false;
            try { status = await preparation.CommitAsync(cancellationToken); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                status = preparation.EstablishedStatus;
                cancelled = true;
            }
            catch (Exception) { status = preparation.EstablishedStatus; unknown = !status.HasValue; }
            finally
            {
                try { preparation.Dispose(); } catch (Exception) { cleanupFailed = true; }
                cleanupFailed |= preparation.CleanupFailed;
            }
            return new([new(status, unknown)], cleanupFailed, cancelled || cancellationToken.IsCancellationRequested);
        }

        public async ValueTask<SetupCliPublicationResult> WriteBundleAsync(
            IReadOnlyList<DefinitionArtifact> artifacts, string directory, int maximumBytes, CancellationToken cancellationToken)
        {
            if (artifacts.Any(item => item.ByteLength > maximumBytes))
                throw new IOException("output-bound");
            ProtectedArtifactBundleResult result = await ProtectedArtifactBundlePublisher.PublishAsync(
                new ProtectedArtifactWriter(), artifacts, directory, cancellationToken: cancellationToken);
            return new(result.Outcomes.Select(item => new SetupCliPublicationOutcome(item.Status)).ToArray(),
                result.CleanupFailed, result.CompletionCode == ProtectedArtifactBundleCompletionCode.Cancelled);
        }
    }
}
