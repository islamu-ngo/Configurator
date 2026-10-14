namespace Configurator.Terminal;

using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Configurator.Core.Composition;
using Configurator.Core.Definitions;
using Configurator.Presentation;
using global::Terminal.Gui.App;

internal sealed record SetupTerminalImportResult(bool IsCurrent, bool Succeeded, string Code);

/// <summary>Owns one physical read until cleanup, and admits its bytes only on the current UI owner.</summary>
internal sealed class SetupTerminalImportOperation : IDisposable
{
    private readonly object _gate = new();
    private readonly DefinitionFormModel _form;
    private readonly Func<string, CancellationToken, Task<ReadOnlyMemory<byte>>> _read;
    private readonly Action<Action> _dispatch;
    private Attempt? _active;
    private long _generation;
    private bool _disposed;
    private Task _completion = Task.CompletedTask;

    internal SetupTerminalImportOperation(DefinitionFormModel form, Action<Action> dispatch,
        Func<string, CancellationToken, Task<ReadOnlyMemory<byte>>>? read = null)
    {
        _form = form;
        _dispatch = dispatch;
        _read = read ?? ReadFileAsync;
    }

    internal event EventHandler<SetupTerminalImportResult>? Settled;
    internal bool IsBusy { get { lock (_gate) return _active is not null; } }
    internal Task Completion { get { lock (_gate) return _completion; } }

    internal bool Start(string path, string artifactId)
    {
        lock (_gate)
        {
            if (_disposed || _active is not null) return false;
            var attempt = new Attempt(++_generation, artifactId, _form.InputRevision, _form.Definition);
            _active = attempt;
            _completion = RunAsync(attempt, path);
            Observe(_completion);
            return true;
        }
    }

    internal Task RequestCancellationAsync()
    {
        lock (_gate)
        {
            _generation++;
            if (_active is not { } attempt || attempt.Retiring) return Task.CompletedTask;
            if (!attempt.Source.IsCancellationRequested)
            {
                attempt.Cancellation = attempt.Source.CancelAsync();
                Observe(attempt.Cancellation);
            }
            if (!attempt.Applying) attempt.Dispatched.TrySetResult();
            return attempt.Cancellation;
        }
    }

    private async Task RunAsync(Attempt attempt, string path)
    {
        var result = new SetupTerminalImportResult(true, false, "import-failed");
        try
        {
            // File open/stat and injected native reads may block before their first asynchronous suspension.
            attempt.Bytes = await Task.Run(() => _read(path, attempt.Token), attempt.Token).ConfigureAwait(false);
            if (attempt.Bytes.Length is < 1 or > SetupCompositionLimits.DefaultAggregateSourceBytes)
                throw new InvalidDataException();
            lock (_gate)
            {
                if (!IsCurrent(attempt))
                {
                    result = result with { IsCurrent = false };
                    return;
                }
            }
            if (TryDispatch(() => Observe(ApplyAsync(attempt))))
                await attempt.Dispatched.Task.ConfigureAwait(false);
            result = attempt.Result;
        }
        catch (OperationCanceledException) when (attempt.Token.IsCancellationRequested) { }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException
            or ArgumentException or NotSupportedException or SetupDefinitionException)
        {
            lock (_gate)
                result = new(IsCurrent(attempt), false, exception is InvalidDataException ? "import-size-invalid"
                    : exception is UnauthorizedAccessException ? "import-access-denied" : "import-read-failed");
        }
        finally
        {
            Clear(attempt.Bytes);
            attempt.Bytes = ReadOnlyMemory<byte>.Empty;
            Task cancellation;
            lock (_gate)
            {
                attempt.Retiring = true;
                cancellation = attempt.Cancellation;
            }
            try { await cancellation.ConfigureAwait(false); }
            finally
            {
                attempt.Source.Dispose();
                lock (_gate)
                    if (ReferenceEquals(_active, attempt)) _active = null;
                SetupTerminalImportResult settled = result;
                TryDispatch(() =>
                {
                    lock (_gate)
                    {
                        if (_disposed) return;
                        if (attempt.Generation != _generation) settled = settled with { IsCurrent = false };
                    }
                    Settled?.Invoke(this, settled);
                });
            }
        }
    }

    private async Task ApplyAsync(Attempt attempt)
    {
        Exception? failure = null;
        try
        {
            lock (_gate)
            {
                if (!IsCurrent(attempt)) return;
                attempt.Applying = true;
            }
            DefinitionImportResult imported = await _form.ImportAsync(attempt.ArtifactId,
                _ => Task.FromResult(attempt.Bytes), attempt.Token);
            attempt.Result = new(true, imported.Succeeded, imported.Code ?? "import-ready");
        }
        catch (Exception exception)
        {
            failure = exception;
            throw;
        }
        finally
        {
            lock (_gate)
            {
                attempt.Applying = false;
                if (failure is null) attempt.Dispatched.TrySetResult();
                else attempt.Dispatched.TrySetException(failure);
            }
        }
    }

    private bool IsCurrent(Attempt attempt) => !_disposed && ReferenceEquals(_active, attempt)
        && attempt.Generation == _generation && !attempt.Token.IsCancellationRequested
        && attempt.InputRevision == _form.InputRevision && attempt.Definition.Digest == _form.Definition.Digest;

    private bool TryDispatch(Action callback)
    {
        lock (_gate)
            if (_disposed) return false;
        try { _dispatch(callback); return true; }
        catch (Exception exception) when (exception is NotInitializedException or ObjectDisposedException) { return false; }
    }

    internal static async Task<ReadOnlyMemory<byte>> ReadFileAsync(string path, CancellationToken token)
    {
        byte[] bytes = [];
        try
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
                4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
            if (stream.Length is < 1 or > SetupCompositionLimits.DefaultAggregateSourceBytes)
                throw new InvalidDataException();
            bytes = new byte[(int)stream.Length];
            await stream.ReadExactlyAsync(bytes, token).ConfigureAwait(false);
            byte[] extra = new byte[1];
            try
            {
                if (await stream.ReadAsync(extra, token).ConfigureAwait(false) != 0) throw new InvalidDataException();
            }
            finally { CryptographicOperations.ZeroMemory(extra); }
            return bytes;
        }
        catch
        {
            CryptographicOperations.ZeroMemory(bytes);
            throw;
        }
    }

    private static void Clear(ReadOnlyMemory<byte> bytes)
    {
        if (MemoryMarshal.TryGetArray(bytes, out ArraySegment<byte> storage))
            CryptographicOperations.ZeroMemory(storage.AsSpan());
    }

    private static void Observe(Task task) => _ = task.ContinueWith(completed => _ = completed.Exception,
        CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
        TaskScheduler.Default);

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            RequestCancellationAsync();
            _disposed = true;
            Settled = null;
        }
    }

    private sealed class Attempt
    {
        internal Attempt(long generation, string artifactId, long revision, SetupDefinition definition)
        {
            Generation = generation;
            ArtifactId = artifactId;
            InputRevision = revision;
            Definition = definition;
            Token = Source.Token;
        }
        internal long Generation { get; }
        internal string ArtifactId { get; }
        internal long InputRevision { get; }
        internal SetupDefinition Definition { get; }
        internal CancellationTokenSource Source { get; } = new();
        internal CancellationToken Token { get; }
        internal Task Cancellation { get; set; } = Task.CompletedTask;
        internal bool Retiring { get; set; }
        internal bool Applying { get; set; }
        internal ReadOnlyMemory<byte> Bytes { get; set; }
        internal TaskCompletionSource Dispatched { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal SetupTerminalImportResult Result { get; set; } = new(false, false, "import-stale");
    }
}
