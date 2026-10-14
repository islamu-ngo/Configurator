namespace Configurator.Terminal;

using System.Runtime.InteropServices;

internal sealed class SetupTerminalSignalScope : IDisposable
{
    private readonly List<PosixSignalRegistration> _registrations = [];
    private readonly SetupTerminalSecretBuffer _secret;
    private Action? _stop;
    private int _disposed;
    private int _requested;

    internal SetupTerminalSignalScope(SetupTerminalSecretBuffer secret, Action stop)
    {
        _secret = secret ?? throw new ArgumentNullException(nameof(secret));
        _stop = stop ?? throw new ArgumentNullException(nameof(stop));
        Console.CancelKeyPress += OnCancelKeyPress;
        if (!OperatingSystem.IsWindows())
        {
            Register(PosixSignal.SIGTERM);
            Register(PosixSignal.SIGHUP);
            Register(PosixSignal.SIGQUIT);
            Register(PosixSignal.SIGTSTP);
        }
    }

    private void Register(PosixSignal signal)
    {
        try
        {
            _registrations.Add(PosixSignalRegistration.Create(signal, context =>
            {
                context.Cancel = true;
                RequestStop();
            }));
        }
        catch (PlatformNotSupportedException)
        {
        }
        catch (ArgumentException)
        {
        }
    }

    private void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs args)
    {
        args.Cancel = true;
        RequestStop();
    }

    internal void RequestStop()
    {
        _secret.Clear();
        if (Volatile.Read(ref _disposed) != 0 || Interlocked.Exchange(ref _requested, 1) != 0) return;
        try
        {
            Volatile.Read(ref _stop)?.Invoke();
        }
        catch (Exception exception) when (exception is InvalidOperationException or ObjectDisposedException)
        {
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        Interlocked.Exchange(ref _stop, null);
        Console.CancelKeyPress -= OnCancelKeyPress;
        foreach (PosixSignalRegistration registration in _registrations)
            registration.Dispose();
        _registrations.Clear();
        _secret.Clear();
    }
}
