namespace Configurator.Desktop.Views;

using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Configurator.Desktop.ViewModels;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        AvaloniaXamlLoader.Load(this);
        Closed += (_, _) => (DataContext as DesktopShellViewModel)?.Dispose();
    }
}
