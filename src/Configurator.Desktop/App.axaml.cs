namespace Configurator.Desktop;

using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Configurator.Desktop.ViewModels;
using Configurator.Desktop.Views;

public sealed partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var shell = new SetupDesktopShellViewModel();
            desktop.MainWindow = new MainWindow
            {
                DataContext = shell
            };
            string[] args = desktop.Args ?? [];
            if (args.Length is 1 or 2)
            {
                shell.DefinitionPath = args[0];
                if (args.Length == 2) shell.OutputDirectory = args[1];
                desktop.MainWindow.Opened += async (_, _) => await shell.LoadDefinitionAsync();
            }
            desktop.Exit += (_, _) => shell.Dispose();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
