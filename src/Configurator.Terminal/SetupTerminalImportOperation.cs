namespace Configurator.Terminal;

using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Configurator.Artifacts;
using Configurator.Core.Composition;
using Configurator.Core.Definitions;
using Configurator.Presentation;
using global::Terminal.Gui.App;

internal sealed record SetupTerminalImportResult(bool IsCurrent, bool Succeeded, string Code)
{
    internal bool IsComparison { get; init; }
}

/// <summary>Owns physical input and cooperative comparison or adoption until cleanup on the current UI owner.</summary>
internal sealed class SetupTerminalImportOperation : IDisposable
{
    private readonly object _gate = new();
    private readonly DefinitionFormModel _form;
    private readonly Func<string, CancellationToken, Task<ReadOnlyMemory<byte>>>? _read;
    private readonly Func<string, Stream> _openRead;
    private readonly Action<Action> _dispatch;
    private readonly Func<CancellationToken, ValueTask> _cooperativeYield;
    private Attempt? _active;
    private long _generation;
    private bool _disposed;
    private Task _completion = Task.CompletedTask;

    internal SetupTerminalImportOperation(DefinitionFormModel form, Action<Action> dispatch,
        Func<string, CancellationToken, Task<ReadOnlyMemory<byte>>>? read = null,
        Func<string, Stream>? openRead = null,
        Func<CancellationToken, ValueTask>? cooperativeYield = null)
    {
        _form = form;
        _dispatch = dispatch;
        _read = read;
        _cooperativeYield = cooperativeYield ?? (_ => ValueTask.CompletedTask);
        _openRead = openRead ?? (path => new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            4096, FileOptions.Asynchronous | FileOptions.SequentialScan));
    }

    internal bool StartAdoption(string artifactId)
    {
        lock (_gate)
        {
            if (_disposed || _active is not null) return false;
            var attempt = new Attempt(++_generation, artifactId, _form.InputRevision, _form.Definition,
                compare: false, adoption: true);
            _active = attempt;
            _completion = RunAsync(attempt, null);
            Observe(_completion);
            return true;
        }
    }

    internal event EventHandler<SetupTerminalImportResult>? Settled;
    internal bool IsBusy { get { lock (_gate) return _active is not null; } }
    internal Task Completion { get { lock (_gate) return _completion; } }

    internal bool Start(string path, string artifactId, bool compare = false)
    {
        lock (_gate)
        {
            if (_disposed || _active is not null) return false;
            _form.ClearImportComparison();
            var attempt = new Attempt(++_generation, artifactId, _form.InputRevision, _form.Definition, compare);
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
            _form.ClearImportComparison();
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

    private async Task RunAsync(Attempt attempt, string? path)
    {
        var result = new SetupTerminalImportResult(true, false, "import-failed");
        try
        {
            if (!attempt.Adoption)
            {
                if (_read is null)
                {
                    attempt.Input = await BoundedFileInput.ReadAsync(() => _openRead(path!),
                        SetupCompositionLimits.DefaultAggregateSourceBytes, attempt.Token).ConfigureAwait(false);
                    attempt.Bytes = attempt.Input.Bytes;
                }
                else
                {
                    // Injected external readers may also block before their first asynchronous suspension.
                    attempt.Bytes = await Task.Run(() => _read(path!, attempt.Token), attempt.Token).ConfigureAwait(false);
                }
                if (attempt.Bytes.Length is < 1 or > SetupCompositionLimits.DefaultAggregateSourceBytes)
                    throw new InvalidDataException();
            }
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
            attempt.Input?.Dispose();
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
            if (!attempt.Adoption)
            {
                DefinitionImportComparisonResult compared = await _form.CompareAsync(attempt.ArtifactId,
                    _ => Task.FromResult(attempt.Bytes), _cooperativeYield, attempt.Token);
                attempt.Result = new(true, compared.Succeeded, compared.Code) { IsComparison = attempt.Compare };
                if (!compared.Succeeded || attempt.Compare) return;
            }
            DefinitionImportResult imported = await _form.AdoptImportComparisonAsync(_cooperativeYield, attempt.Token);
            attempt.Result = new(true, imported.Succeeded, imported.Code);
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
        internal Attempt(long generation, string artifactId, long revision, SetupDefinition definition, bool compare,
            bool adoption = false)
        {
            Generation = generation;
            ArtifactId = artifactId;
            InputRevision = revision;
            Definition = definition;
            Compare = compare;
            Adoption = adoption;
            Token = Source.Token;
        }
        internal long Generation { get; }
        internal string ArtifactId { get; }
        internal long InputRevision { get; }
        internal SetupDefinition Definition { get; }
        internal bool Compare { get; }
        internal bool Adoption { get; }
        internal CancellationTokenSource Source { get; } = new();
        internal CancellationToken Token { get; }
        internal Task Cancellation { get; set; } = Task.CompletedTask;
        internal BoundedFileInput? Input { get; set; }
        internal bool Retiring { get; set; }
        internal bool Applying { get; set; }
        internal ReadOnlyMemory<byte> Bytes { get; set; }
        internal TaskCompletionSource Dispatched { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal SetupTerminalImportResult Result { get; set; } = new(false, false, "import-stale");
    }
}
