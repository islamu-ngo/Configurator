namespace Configurator.Desktop.Views;

using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Configurator.Desktop.ViewModels;

public sealed partial class MainWindow : Window
{
    private bool _allowClose;
    private bool _closed;

    public MainWindow()
    {
        AvaloniaXamlLoader.Load(this);
        Closing += OnClosing;
        Closed += OnClosed;
    }

    private async void OnClosing(object? sender, WindowClosingEventArgs args)
    {
        if (_allowClose || DataContext is not DesktopShellViewModel shell) return;
        args.Cancel = true;
        bool close = await shell.RequestCloseAsync();
        if (!close || _closed || !ReferenceEquals(DataContext, shell)) return;
        _allowClose = true;
        Close();
    }

    private void OnClosed(object? sender, EventArgs args)
    {
        _closed = true;
        Closing -= OnClosing;
        Closed -= OnClosed;
        var shell = DataContext as DesktopShellViewModel;
        DataContext = null;
        shell?.Dispose();
    }
}
