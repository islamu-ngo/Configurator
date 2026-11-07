namespace Configurator.Desktop.Views;

using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Configurator.Desktop.ViewModels;

public sealed partial class MainWindow : Window
{
    private bool _allowClose;
    private bool _closed;

    public MainWindow()
    {
        AvaloniaXamlLoader.Load(this);
        AddHandler(InputElement.KeyDownEvent, OnUnhandledKeyDown, RoutingStrategies.Bubble, handledEventsToo: false);
        Closing += OnClosing;
        Closed += OnClosed;
#if CONFIGURATOR_NATIVE_IMPORT_PROBE
        Opened += (_, _) => Configurator.Engineering.MeasureNativeImportComparison.Attach(this);
#endif
    }

    private void OnUnhandledKeyDown(object? sender, KeyEventArgs args)
    {
        if (DataContext is not DesktopShellViewModel shell) return;
        if (args.Key == Key.Escape && args.KeyModifiers == KeyModifiers.None)
        {
            Popup? popup = OpenedPopups.Count > 0 ? OpenedPopups[^1] : null;
            if (popup is not null)
            {
                while (popup.OpenedPopups.Count > 0) popup = popup.OpenedPopups[^1];
                popup.Close();
            }
            else shell.CancelCommand.Execute(null);
            args.Handled = true;
            return;
        }
        var modifiers = Avalonia.Application.Current!.PlatformSettings!.HotkeyConfiguration.CommandModifiers;
        if (args.KeyModifiers != modifiers) return;
        var command = args.Key switch
        {
            Key.O => (System.Windows.Input.ICommand)shell.FocusDefinitionPathCommand,
            Key.Enter => shell.ReviewAndPrepareCommand,
            _ => null
        };
        if (command?.CanExecute(null) != true) return;
        command.Execute(null);
        args.Handled = true;
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
        RemoveHandler(InputElement.KeyDownEvent, OnUnhandledKeyDown);
        var shell = DataContext as DesktopShellViewModel;
        DataContext = null;
        shell?.Dispose();
    }
}
