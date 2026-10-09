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
            var invocation = new SetupCliInvocation(
                args,
                mode,
                io,
                new SetupCliEnvironmentPresence(
                    Environment.GetEnvironmentVariables().Keys.Cast<object>()
                        .Select(name => name.ToString()!)
                        .Where(name => name.Length > 0)));
            return (int)new SetupCliApplication().Run(invocation);
        }
        catch (ArgumentException)
        {
            return WriteFallback(args, SetupCliExitCode.Usage, "argument-shape-invalid");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return WriteFallback(args, SetupCliExitCode.Io, "io-failed");
        }
        catch (Exception)
        {
            return WriteFallback(args, SetupCliExitCode.Internal, "internal-failed");
        }
    }

    private static int WriteFallback(string[] args, SetupCliExitCode exit, string code)
    {
        bool machine = args.Contains("--machine", StringComparer.Ordinal);
        try
        {
            using Stream stream = machine ? Console.OpenStandardOutput() : Console.OpenStandardError();
            byte[] bytes = machine ? SetupCliMachineOutput.Fallback(exit, code)
                : System.Text.Encoding.UTF8.GetBytes($"{exit.ToString().ToLowerInvariant()}-error $.arguments\n");
            stream.Write(bytes);
            stream.Flush();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
        return (int)exit;
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

        public void WriteArtifact(string path, SetupArtifactKind kind, ReadOnlyMemory<byte> bytes, int maximumBytes)
        {
            if (bytes.Length > maximumBytes) throw new IOException("output-bound");
            if (path == "-")
            {
                ProtectedArtifactWriter.WritePublic(standardOutput, kind, bytes);
                return;
            }
            var writer = new ProtectedArtifactWriter();
            using ProtectedArtifactPreparation preparation = writer
                .PrepareAsync(kind, path, bytes, CancellationToken.None).GetAwaiter().GetResult();
            if (preparation.CommitAsync().GetAwaiter().GetResult() != ProtectedArtifactStatus.Written)
                throw new IOException("protected-output-unavailable");
        }

        public IReadOnlyList<ProtectedArtifactStatus?> WriteBundle(
            IReadOnlyList<DefinitionArtifact> artifacts, string directory, int maximumBytes)
        {
            if (artifacts.Any(item => item.ByteLength > maximumBytes))
                throw new IOException("output-bound");
            ProtectedArtifactBundleResult result = ProtectedArtifactBundlePublisher.PublishAsync(
                new ProtectedArtifactWriter(), artifacts, directory).GetAwaiter().GetResult();
            return result.Outcomes.Select(item => item.Status).ToArray();
        }
    }
}
