namespace Configurator.Browser.Tests;

using System.Reflection;
using Configurator.Browser;

public sealed class BrowserReleaseContractTests
{
    [Test]
    public async Task EnabledBrowserRuntimeExcludesConnectedAndTelemetryProducts()
    {
        Assembly assembly = typeof(BrowserPublicTemplate).Assembly;
        string[] forbidden =
        [
            "Configurator.Presentation.SetupLive",
            "Configurator.Artifacts",
            "Event",
            "Explore",
            "ApplicationInsights",
            "OpenTelemetry",
            "Yarp",
            "Authentication"
        ];
        string[] references = assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .ToArray();

        await Assert.That(references.Any(reference => forbidden.Any(term =>
            reference.Contains(term, StringComparison.OrdinalIgnoreCase)))).IsFalse();
    }

    [Test]
    public async Task BrowserPublicSurfaceExposesNoConnectionOrStorageContract()
    {
        string[] forbiddenTerms =
        [
            "HttpClient", "Token", "Login", "Identity", "Storage",
            "Telemetry", "ServiceWorker", "InstanceUrl", "ApiKey"
        ];
        Type[] publicTypes = typeof(BrowserPublicTemplate).Assembly.GetExportedTypes();
        string[] publicMembers = publicTypes
            .SelectMany(type => type.GetMembers(BindingFlags.Public
                | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Select(member => $"{member.DeclaringType?.FullName}.{member.Name}")
            .ToArray();

        await Assert.That(publicMembers.Any(member => forbiddenTerms.Any(term =>
            member.Contains(term, StringComparison.OrdinalIgnoreCase)))).IsFalse();
    }
}
