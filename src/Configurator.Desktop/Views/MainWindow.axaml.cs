namespace Configurator.Desktop.Views;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using Configurator.Desktop.ViewModels;
using Configurator.Desktop.ViewModels.Fields;

public sealed partial class MainWindow : Window
{
    private bool _allowClose;
    private bool _closed;

    public MainWindow()
    {
        AvaloniaXamlLoader.Load(this);
        AddHandler(InputElement.KeyDownEvent, OnUnhandledKeyDown, RoutingStrategies.Bubble, handledEventsToo: false);
        AddHandler(InputElement.GotFocusEvent, OnWorkspaceGotFocus, RoutingStrategies.Bubble, handledEventsToo: true);
        Closing += OnClosing;
        Closed += OnClosed;
#if CONFIGURATOR_NATIVE_IMPORT_PROBE
        Opened += (_, _) => Configurator.Engineering.MeasureNativeImportComparison.Attach(this);
#endif
    }

    private void OnWorkspaceGotFocus(object? sender, FocusChangedEventArgs args)
    {
        if (_closed || DataContext is not DesktopShellViewModel shell) return;
        if (args.Source is Visual visual && visual.GetVisualAncestors().OfType<DesktopInspectorView>().Any())
            return;
        if ((args.Source as StyledElement)?.DataContext is FieldViewModel field)
            shell.Inspector.InspectField(field);
    }

    private void OnUnhandledKeyDown(object? sender, KeyEventArgs args)
    {
        if (DataContext is not DesktopShellViewModel shell) return;
        if (args.Key == Key.F2 && args.KeyModifiers == KeyModifiers.None)
        {
            if (shell.ToggleInspectorCommand.CanExecute(null))
            {
                shell.ToggleInspectorCommand.Execute(null);
                args.Handled = true;
            }
            return;
        }
        if (args.Key == Key.Escape && args.KeyModifiers == KeyModifiers.None)
        {
            Popup? popup = OpenedPopups.Count > 0 ? OpenedPopups[^1] : null;
            if (popup is not null)
            {
                while (popup.OpenedPopups.Count > 0) popup = popup.OpenedPopups[^1];
                popup.Close();
            }
            else if (shell.Navigation.IsCommandPaletteOpen)
            {
                shell.Navigation.CloseCommandPaletteCommand.Execute(null);
            }
            else shell.CancelCommand.Execute(null);
            args.Handled = true;
            return;
        }
        if (args.Key == Key.OemQuestion && args.KeyModifiers == KeyModifiers.Shift &&
            FocusManager?.GetFocusedElement() is not TextBox)
        {
            shell.OpenShortcutsHelpCommand.Execute(null);
            args.Handled = true;
            return;
        }
        var modifiers = Avalonia.Application.Current!.PlatformSettings!.HotkeyConfiguration.CommandModifiers;
        if (args.KeyModifiers == (modifiers | KeyModifiers.Shift))
        {
            if (args.Key == Key.Z && shell.Workspace.RedoCommand.CanExecute(null))
            {
                shell.Workspace.RedoCommand.Execute(null);
                args.Handled = true;
            }
            else if (args.Key == Key.K && shell.Workspace.ToggleNamingModeCommand.CanExecute(null))
            {
                shell.Workspace.ToggleNamingModeCommand.Execute(null);
                args.Handled = true;
            }
            else if (args.Key == Key.E && shell.OpenDocumentModeCommand.CanExecute(null))
            {
                shell.OpenDocumentModeCommand.Execute(null);
                args.Handled = true;
            }
            return;
        }
        if (args.KeyModifiers != modifiers) return;
        var command = args.Key switch
        {
            Key.O => (System.Windows.Input.ICommand)shell.FocusDefinitionPathCommand,
            Key.K => shell.OpenCommandPaletteCommand,
            Key.Enter => shell.ReviewAndPrepareCommand,
            Key.Z => shell.Workspace.UndoCommand,
            Key.Y => shell.Workspace.RedoCommand,
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
        RemoveHandler(InputElement.GotFocusEvent, OnWorkspaceGotFocus);
        var shell = DataContext as DesktopShellViewModel;
        DataContext = null;
        shell?.Dispose();
    }
}
