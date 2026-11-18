namespace Configurator.Desktop.Views;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Configurator.Desktop.ViewModels;
using Configurator.Desktop.ViewModels.Fields;

public sealed partial class MainWindow : Window
{
    private bool _allowClose;
    private bool _closed;
    private DesktopShellViewModel? _attachedShell;

    public MainWindow()
    {
        AvaloniaXamlLoader.Load(this);
        AddHandler(InputElement.KeyDownEvent, OnUnhandledKeyDown, RoutingStrategies.Bubble, handledEventsToo: false);
        AddHandler(InputElement.GotFocusEvent, OnWorkspaceGotFocus, RoutingStrategies.Bubble, handledEventsToo: true);
        Closing += OnClosing;
        Closed += OnClosed;
        DataContextChanged += (_, _) => BindNavigationViewport();
#if CONFIGURATOR_NATIVE_IMPORT_PROBE
        Opened += (_, _) => Configurator.Engineering.MeasureNativeImportComparison.Attach(this);
#endif
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if ((change.Property == BoundsProperty || change.Property == WidthProperty) &&
            DataContext is DesktopShellViewModel shell)
        {
            double width = Bounds.Width > 0 ? Bounds.Width : Width;
            if (!double.IsNaN(width) && width > 0)
                shell.Navigation.UpdateViewportWidth(width);
        }
    }

    private void BindNavigationViewport()
    {
        if (_attachedShell is not null)
            _attachedShell.Navigation.AllSectionsScrollRequested -= OnAllSectionsScrollRequested;
        _attachedShell = DataContext as DesktopShellViewModel;
        if (_attachedShell is null) return;
        _attachedShell.Navigation.AllSectionsScrollRequested += OnAllSectionsScrollRequested;
        double width = Bounds.Width > 0 ? Bounds.Width : Width;
        if (!double.IsNaN(width) && width > 0)
            _attachedShell.Navigation.UpdateViewportWidth(width);
    }

    private void OnStartDockDragDelta(object? sender, VectorEventArgs args)
    {
        if (DataContext is not DesktopShellViewModel shell) return;
        bool isRtl = FlowDirection == Avalonia.Media.FlowDirection.RightToLeft;
        shell.Navigation.ResizeStartPane(args.Vector.X, isRtl);
    }

    private void OnStartDockKeyDown(object? sender, KeyEventArgs args)
    {
        if (DataContext is not DesktopShellViewModel shell) return;
        bool isRtl = FlowDirection == Avalonia.Media.FlowDirection.RightToLeft;
        if (args.Key == Key.Left)
        {
            shell.Navigation.ResizeStartPane(-16, isRtl);
            args.Handled = true;
        }
        else if (args.Key == Key.Right)
        {
            shell.Navigation.ResizeStartPane(16, isRtl);
            args.Handled = true;
        }
        else if (args.Key == Key.Home)
        {
            shell.Navigation.ResetStartPaneWidth();
            args.Handled = true;
        }
    }

    private void OnStartDockDoubleTapped(object? sender, TappedEventArgs args)
    {
        if (DataContext is DesktopShellViewModel shell)
            shell.Navigation.ResetStartPaneWidth();
    }

    private void OnEndDockKeyDown(object? sender, KeyEventArgs args)
    {
        if (DataContext is not DesktopShellViewModel shell) return;
        bool isRtl = FlowDirection == Avalonia.Media.FlowDirection.RightToLeft;
        if (args.Key == Key.Left)
        {
            shell.Navigation.ResizeEndPane(-16, isRtl);
            args.Handled = true;
        }
        else if (args.Key == Key.Right)
        {
            shell.Navigation.ResizeEndPane(16, isRtl);
            args.Handled = true;
        }
        else if (args.Key == Key.Home)
        {
            shell.Navigation.ResetEndPaneWidth();
            args.Handled = true;
        }
    }

    private void OnEndDockDoubleTapped(object? sender, TappedEventArgs args)
    {
        if (DataContext is DesktopShellViewModel shell)
            shell.Navigation.ResetEndPaneWidth();
    }

    private void OnAllSectionsScrollRequested(FieldViewModel field) =>
        Dispatcher.UIThread.Post(() =>
        {
            if (!_closed)
                this.FindControl<ListBox>("AllSectionsFieldsList")?.ScrollIntoView(field);
        });

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
        if (args.Key == Key.F7 && args.KeyModifiers == KeyModifiers.None)
        {
            if (shell.Navigation.JumpNextErrorCommand.CanExecute(null))
            {
                shell.Navigation.JumpNextErrorCommand.Execute(null);
                args.Handled = true;
            }
            return;
        }
        if (args.Key == Key.F8 && args.KeyModifiers == KeyModifiers.None)
        {
            if (shell.Navigation.JumpNextRequiredCommand.CanExecute(null))
            {
                shell.Navigation.JumpNextRequiredCommand.Execute(null);
                args.Handled = true;
            }
            return;
        }
        if (args.Key == Key.F9 && args.KeyModifiers == KeyModifiers.None)
        {
            if (shell.Navigation.JumpNextUnvisitedCommand.CanExecute(null))
            {
                shell.Navigation.JumpNextUnvisitedCommand.Execute(null);
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
            else if (shell.Loader.HasStoreDetails)
            {
                shell.Loader.CloseStoreDetailsCommand.Execute(null);
            }
            else if (shell.Navigation.IsDiscoveryOpen)
            {
                shell.Navigation.CloseCommandPaletteCommand.Execute(null);
            }
            else if (shell.Navigation.IsDocumentOpen)
            {
                shell.Navigation.CloseDocumentDrawerCommand.Execute(null);
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
        if (_attachedShell is not null)
        {
            _attachedShell.Navigation.AllSectionsScrollRequested -= OnAllSectionsScrollRequested;
            _attachedShell = null;
        }
        var shell = DataContext as DesktopShellViewModel;
        DataContext = null;
        shell?.Dispose();
    }
}
