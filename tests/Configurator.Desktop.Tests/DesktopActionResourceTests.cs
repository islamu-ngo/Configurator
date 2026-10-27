namespace Configurator.Desktop.Tests;

using System.Globalization;
using System.Resources;
using Configurator.Desktop.ViewModels.Fields;

public sealed class DesktopActionResourceTests
{
    [Test]
    public async Task ArabicActionsExistInTheirExactShippedResourceSet()
    {
        var resources = new ResourceManager("Configurator.Desktop.Resources.DesktopActionText",
            typeof(FieldViewModel).Assembly);
        try
        {
            using ResourceSet? arabic = resources.GetResourceSet(CultureInfo.GetCultureInfo("ar"),
                createIfNotExists: true, tryParents: false);
            await Assert.That(arabic).IsNotNull();
            foreach (string key in new[] { "SupplyLabel", "SupplyName", "SupplyHelp", "ClearLabel", "ClearName", "ClearHelp" })
            {
                string? text = arabic!.GetString(key);
                await Assert.That(string.IsNullOrWhiteSpace(text)).IsFalse();
                await Assert.That(text!.Any(character => character is >= '\u0600' and <= '\u06ff')).IsTrue();
            }
        }
        finally { resources.ReleaseAllResources(); }
    }
}
