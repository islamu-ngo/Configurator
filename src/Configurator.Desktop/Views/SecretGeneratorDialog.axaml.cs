namespace Configurator.Desktop.Views;

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Configurator.Desktop.ViewModels;

/// <summary>Modal options dialog for bounded local secret generation preview and explicit apply.</summary>
public sealed partial class SecretGeneratorDialog : Window
{
    private SecretGeneratorDialogViewModel? _attached;
    private bool _closingFromRequest;

    public SecretGeneratorDialog()
    {
        AvaloniaXamlLoader.Load(this);
        DataContextChanged += (_, _) => Reattach();
        KeyDown += OnDialogKeyDown;
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        if (_attached is not null)
        {
            _attached.RequestClose -= OnRequestClose;
            if (!_closingFromRequest)
                _attached.CancelCommand.Execute(null);
            _attached = null;
        }
    }

    private void Reattach()
    {
        if (_attached is not null)
            _attached.RequestClose -= OnRequestClose;

        _attached = DataContext as SecretGeneratorDialogViewModel;
        if (_attached is not null)
            _attached.RequestClose += OnRequestClose;
    }

    private void OnRequestClose(object? sender, bool applied)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => OnRequestClose(sender, applied));
            return;
        }
        if (_closingFromRequest) return;
        _closingFromRequest = true;
        Close(applied);
    }

    private void OnDialogKeyDown(object? sender, KeyEventArgs args)
    {
        if (args.Key == Key.Escape && DataContext is SecretGeneratorDialogViewModel vm)
        {
            args.Handled = true;
            vm.CancelCommand.Execute(null);
        }
    }
}
