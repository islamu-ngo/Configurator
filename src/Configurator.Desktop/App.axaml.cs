namespace Configurator.Desktop;

using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CommunityToolkit.Mvvm.Messaging;
using Configurator.Artifacts;
using Configurator.Desktop.Resources;
using Configurator.Desktop.Services;
using Configurator.Desktop.ViewModels;
using Configurator.Desktop.Views;
using Microsoft.Extensions.DependencyInjection;

public sealed partial class App : Application, IDisposable
{
    private ServiceProvider? _services;
    private DesktopTheme? _theme;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (PlatformSettings is { } settings) _theme = new DesktopTheme(this, settings);
            var services = new ServiceCollection();
            if (_theme is not null) services.AddSingleton(_theme);
            services.AddSingleton<IProtectedArtifactWriter, ProtectedArtifactWriter>();
            services.AddSingleton<IMessenger, WeakReferenceMessenger>();
            services.AddSingleton<IStorageDialogService>(provider =>
                new AvaloniaStorageDialogService(() => provider.GetRequiredService<MainWindow>().StorageProvider,
                    () => provider.GetRequiredService<FormWorkspaceViewModel>().Form?.Locale));
            services.AddSingleton<ISecretClipboardService>(provider =>
                new AvaloniaSecretClipboardService(() => provider.GetRequiredService<MainWindow>().Clipboard));
            services.AddSingleton<DefinitionLoaderViewModel>();
            services.AddSingleton<FormWorkspaceViewModel>();
            services.AddSingleton<Func<CancellationToken, ValueTask>>(
                DesktopImportReviewViewModel.YieldDispatcherAsync);
            services.AddSingleton<ArtifactPublisherViewModel>();
            services.AddSingleton<DesktopImportReviewViewModel>();
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
            desktop.Exit += (_, _) => Dispose();
        }

        base.OnFrameworkInitializationCompleted();
    }

    public void Dispose()
    {
        _theme?.Dispose();
        _theme = null;
        _services?.Dispose();
        _services = null;
    }
}
