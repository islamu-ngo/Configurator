namespace Configurator.DependencyProbes.DependencyInjection;

using Microsoft.Extensions.DependencyInjection;

internal static class ProbeComposition
{
    internal static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddTransient<ProbeService>();
        return services.BuildServiceProvider();
    }
}

internal sealed class ProbeService;
