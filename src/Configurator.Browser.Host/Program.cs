namespace Configurator.Browser.Host;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        if (args is ["--healthcheck"])
            return await HealthCheckProbe.RunAsync(CancellationToken.None);

        await using WebApplication app = BrowserHostApplication.Create(args);
        await app.RunAsync();
        return 0;
    }
}
