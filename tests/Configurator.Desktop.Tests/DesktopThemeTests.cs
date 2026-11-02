namespace Configurator.Desktop.Tests;

using Avalonia;
using Avalonia.Headless;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using Configurator.Desktop.Resources;

public sealed class DesktopThemeTests
{
    public static AppBuilder BuildAvaloniaApp() => DesktopApplicationTests.BuildAvaloniaApp();

    [Test]
    [NotInParallel]
    public async Task NativeContrastChangesApplyUntilTheSubscriptionIsRetired()
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(DesktopThemeTests));
        try
        {
            var result = await session.Dispatch(() =>
            {
                var application = Application.Current!;
                Action<PlatformColorValues>? notify = null;
                bool unsubscribed = false;
                using var theme = new DesktopTheme(application, changed =>
                {
                    notify = changed;
                    return () => unsubscribed = true;
                });
                notify!(new PlatformColorValues { ContrastPreference = ColorContrastPreference.High });
                bool high = application.RequestedThemeVariant == DesktopTheme.HighContrast;
                notify(new PlatformColorValues { ContrastPreference = ColorContrastPreference.NoPreference });
                bool followsSystem = application.RequestedThemeVariant == ThemeVariant.Default;
                // Hold the dispatcher until the platform thread has queued its update.
                var publication = new Thread(() => notify(new PlatformColorValues
                { ContrastPreference = ColorContrastPreference.High }));
                publication.Start();
                bool queued = publication.Join(TimeSpan.FromSeconds(10));
                theme.Dispose();
                notify(new PlatformColorValues { ContrastPreference = ColorContrastPreference.High });
                Dispatcher.UIThread.RunJobs();
                return (high, followsSystem, queued, unsubscribed, retired: application.RequestedThemeVariant == ThemeVariant.Default);
            }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));
            await Assert.That(result.high).IsTrue();
            await Assert.That(result.followsSystem).IsTrue();
            await Assert.That(result.queued).IsTrue();
            await Assert.That(result.unsubscribed).IsTrue();
            await Assert.That(result.retired).IsTrue();
        }
        finally { await Task.Run(session.Dispose); }
    }

}
