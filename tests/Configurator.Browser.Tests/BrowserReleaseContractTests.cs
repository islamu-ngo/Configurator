namespace Configurator.Browser.Tests;

using System.Reflection;
using Configurator.Browser;

public sealed class BrowserReleaseContractTests
{
    [Test]
    public async Task EnabledBrowserRuntimeExcludesConnectedAndTelemetryProducts()
    {
        Assembly assembly = typeof(BrowserOfflinePolicy).Assembly;
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
    public async Task BundledDefinitionsRemainDataOnlyWithCanonicalRevisions()
    {
        BrowserDefinitionRegistry registry = BrowserDefinitionRegistry.LoadBundled();
        await Assert.That(registry.Current.Definitions.Count > 0).IsTrue();
        foreach (var definition in registry.Current.Definitions)
        {
            var readmitted = Configurator.Core.Definitions.SetupDefinitionCodec.ParseJson(definition.CanonicalBytes);
            await Assert.That(readmitted.Digest).IsEqualTo(definition.Digest);
            await Assert.That(readmitted.FamilyId).IsEqualTo(definition.FamilyId);
            await Assert.That(readmitted.Revision).IsEqualTo(definition.Revision);
        }
    }
}
