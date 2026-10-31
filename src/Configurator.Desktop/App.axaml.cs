namespace Configurator.Desktop;

using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CommunityToolkit.Mvvm.Messaging;
using Configurator.Artifacts;
using Configurator.Desktop.Services;
using Configurator.Desktop.ViewModels;
using Configurator.Desktop.Views;
using Microsoft.Extensions.DependencyInjection;

public sealed partial class App : Application
{
    private ServiceProvider? _services;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var services = new ServiceCollection();
            services.AddSingleton<IProtectedArtifactWriter, ProtectedArtifactWriter>();
            services.AddSingleton<IMessenger, WeakReferenceMessenger>();
            services.AddSingleton<IStorageDialogService>(provider =>
                new AvaloniaStorageDialogService(() => provider.GetRequiredService<MainWindow>().StorageProvider));
            services.AddSingleton<DefinitionLoaderViewModel>();
            services.AddSingleton<FormWorkspaceViewModel>();
            services.AddSingleton<Func<CancellationToken, ValueTask>>(
                ArtifactPublisherViewModel.YieldDispatcherAsync);
            services.AddSingleton<ArtifactPublisherViewModel>();
            services.AddSingleton<DesktopShellViewModel>();
            services.AddSingleton(provider => new MainWindow
            {
                DataContext = provider.GetRequiredService<DesktopShellViewModel>()
            });
            _services = services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true
            });
            desktop.MainWindow = _services.GetRequiredService<MainWindow>();
            var shell = _services.GetRequiredService<DesktopShellViewModel>();
            string[] args = desktop.Args ?? [];
            if (args.Length is 1 or 2)
            {
                shell.Loader.DefinitionPath = args[0];
                if (args.Length == 2) shell.Publisher.OutputDirectory = args[1];
                desktop.MainWindow.Opened += async (_, _) => await shell.Loader.LoadDefinitionAsync();
            }
            desktop.Exit += (_, _) =>
            {
                _services.Dispose();
                _services = null;
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
