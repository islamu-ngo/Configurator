using Configurator.Artifacts;
using Configurator.Cli;
using Configurator.Core;
using Configurator.Core.Artifacts;

return SetupCliProgram.Run(args);

internal static class SetupCliProgram
{
    internal static int Run(string[] args)
    {
        try
        {
            SetupCliMode mode = args.Contains("--machine", StringComparer.Ordinal) ? SetupCliMode.Machine : SetupCliMode.Text;
            using Stream standardOutput = Console.OpenStandardOutput();
            using Stream standardError = Console.OpenStandardError();
            var writer = new SystemWriter(standardOutput);
            var error = new SystemWriter(standardError);
            var io = new SetupCliIo(new SystemInput(), writer, error, 65_536, 4 * 1024 * 1024);
            SetupCliInvocation invocation;
            try
            {
                invocation = new(args, mode, io, new SetupCliEnvironmentPresence(
                    Environment.GetEnvironmentVariables().Keys.Cast<object>().Select(name => name.ToString()!).Where(name => name.Length > 0)));
            }
            catch (ArgumentException)
            {
                invocation = new(["doctor", mode == SetupCliMode.Machine ? "--machine" : "--text", "--invalid-arguments"], mode, io, new([]));
            }
            return (int)new SetupCliApplication().Run(invocation);
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
    }

    private sealed class SystemInput : ISetupCliInput
    {
        public ReadOnlyMemory<byte> Read(string path, int maximumBytes)
        {
            using Stream stream = path == "-"
                ? Console.OpenStandardInput()
                : new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var buffer = new MemoryStream();
            byte[] chunk = new byte[8192];
            int total = 0;
            while (true)
            {
                int read = stream.Read(chunk, 0, Math.Min(chunk.Length, maximumBytes + 1 - total));
                if (read == 0) break;
                total += read;
                if (total > maximumBytes) throw new IOException("input-bound");
                buffer.Write(chunk, 0, read);
            }
            return buffer.ToArray();
        }
    }

    private sealed class SystemWriter(Stream standardOutput) : ISetupCliWriter
    {
        public void Write(string path, ReadOnlyMemory<byte> bytes, int maximumBytes)
        {
            if (bytes.Length > maximumBytes) throw new IOException("output-bound");
            if (path == "-")
            {
                standardOutput.Write(bytes.Span);
                standardOutput.Flush();
                return;
            }
            throw new IOException("protected-output-required");
        }

        public SetupCliPublicationResult WriteArtifact(string path, SetupArtifactKind kind, ReadOnlyMemory<byte> bytes, int maximumBytes)
        {
            if (bytes.Length > maximumBytes) throw new IOException("output-bound");
            if (path == "-")
            {
                ProtectedArtifactWriter.WritePublic(standardOutput, kind, bytes);
                return new([new(ProtectedArtifactStatus.Written)]);
            }
            var writer = new ProtectedArtifactWriter();
            ProtectedArtifactPreparation preparation = writer.PrepareAsync(kind, path, bytes).GetAwaiter().GetResult();
            ProtectedArtifactStatus? status = null;
            bool unknown = false;
            bool cleanupFailed = false;
            try { status = preparation.CommitAsync().GetAwaiter().GetResult(); }
            catch (Exception) { status = preparation.EstablishedStatus; unknown = !status.HasValue; }
            finally
            {
                try { preparation.Dispose(); } catch (Exception) { cleanupFailed = true; }
                cleanupFailed |= preparation.CleanupFailed;
            }
            return new([new(status, unknown)], cleanupFailed);
        }

        public SetupCliPublicationResult WriteBundle(
            IReadOnlyList<DefinitionArtifact> artifacts, string directory, int maximumBytes)
        {
            if (artifacts.Any(item => item.ByteLength > maximumBytes))
                throw new IOException("output-bound");
            ProtectedArtifactBundleResult result = ProtectedArtifactBundlePublisher.PublishAsync(
                new ProtectedArtifactWriter(), artifacts, directory).GetAwaiter().GetResult();
            return new(result.Outcomes.Select(item => new SetupCliPublicationOutcome(item.Status)).ToArray(),
                result.CleanupFailed, result.CompletionCode == ProtectedArtifactBundleCompletionCode.Cancelled);
        }
    }
}
