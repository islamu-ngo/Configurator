using System.Collections.ObjectModel;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Configurator.Artifacts;
using Configurator.Core;
using Configurator.Core.Artifacts;
using Configurator.Core.Composition;
using Configurator.Core.Definitions;
using Configurator.Core.Dotenv;
using Configurator.Core.Environment;

namespace Configurator.Cli;

internal static class CliIoOperations
{
    internal static async ValueTask<CliInputBuffer> ReadAsync(CliInvocation invocation, string path,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CliInputBuffer buffer = await invocation.Io.Input.ReadAsync(path, invocation.Io.MaximumBytes, cancellationToken);
        if (buffer.Bytes.Length > invocation.Io.MaximumBytes || cancellationToken.IsCancellationRequested)
        {
            buffer.Dispose();
            cancellationToken.ThrowIfCancellationRequested();
            throw new IOException("input-bound");
        }
        return buffer;
    }

    internal static async ValueTask<Definition> DefinitionAsync(CliInvocation invocation, string path,
        CancellationToken cancellationToken)
    {
        using CliInputBuffer buffer = await ReadAsync(invocation, path, cancellationToken);
        return Path.GetExtension(path).ToLowerInvariant() is ".yaml" or ".yml"
            ? DefinitionSourceCodec.ParseYaml(buffer.Bytes) : DefinitionSourceCodec.ParseJson(buffer.Bytes);
    }

    internal static async ValueTask<AnswerSnapshot> AnswersAsync(CliInvocation invocation, string path,
        string revision, CancellationToken cancellationToken)
    {
        using CliInputBuffer buffer = await ReadAsync(invocation, path, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        AnswerSnapshot snapshot = AnswerSnapshotCodec.ParseJson(buffer.Bytes, long.Parse(revision, CultureInfo.InvariantCulture));
        cancellationToken.ThrowIfCancellationRequested();
        return snapshot;
    }
}
