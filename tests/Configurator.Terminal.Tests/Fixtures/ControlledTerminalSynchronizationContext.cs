namespace Configurator.Terminal.Tests;

using System.Collections.Concurrent;

internal sealed class ControlledTerminalSynchronizationContext : SynchronizationContext, IDisposable
{
    private readonly BlockingCollection<Action> _callbacks = [];
    private readonly Thread _thread;
    private int _recovering;

    internal ControlledTerminalSynchronizationContext()
    {
        _thread = new Thread(Run) { IsBackground = true, Name = "Terminal test owner" };
        _thread.Start();
    }

    internal int OwnerThreadId => _thread.ManagedThreadId;

    internal void Dispatch(Action callback)
    {
        if (Environment.CurrentManagedThreadId == OwnerThreadId) callback();
        else Post(_ => callback(), null);
    }

    public override void Post(SendOrPostCallback callback, object? state)
    {
        if (Volatile.Read(ref _recovering) != 0) ThreadPool.QueueUserWorkItem(_ => callback(state));
        else _callbacks.Add(() => callback(state));
    }

    internal void RecoverBlockedCallbacks()
    {
        Volatile.Write(ref _recovering, 1);
        while (_callbacks.TryTake(out Action? callback)) callback();
    }

    internal void ResumeOwnerDispatch() => Volatile.Write(ref _recovering, 0);

    internal Task InvokeAsync(Action action)
    {
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _callbacks.Add(() =>
        {
            try { action(); completed.SetResult(); }
            catch (Exception exception) { completed.SetException(exception); }
        });
        return completed.Task;
    }

    private void Run()
    {
        SetSynchronizationContext(this);
        foreach (Action callback in _callbacks.GetConsumingEnumerable()) callback();
    }

    public void Dispose()
    {
        _callbacks.CompleteAdding();
        if (!_thread.Join(TimeSpan.FromSeconds(5))) throw new TimeoutException("terminal-test-owner-not-retired");
        _callbacks.Dispose();
    }
}
