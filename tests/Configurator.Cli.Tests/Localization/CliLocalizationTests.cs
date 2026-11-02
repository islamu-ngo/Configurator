using System.Collections;
using System.Globalization;
using System.Resources;
using System.Text;
using Configurator.Cli.Resources;
using Spectre.Console;

namespace Configurator.Cli.Tests;

public sealed class CliLocalizationTests
{
    [Test]
    public async Task IndependentInvocationsUseTheirCapturedCulture()
    {
        var culture = new CultureInfo("ar");
        var files = new CliFixture();
        var invocation = new CliInvocation(["doctor"], CliMode.Text,
            new(files, files, files, CliFixture.RejectingAuthoringFiles, 65_536, 4 * 1024 * 1024), culture);
        culture.NumberFormat.NumberDecimalSeparator = "!";

        Task<string> english = RenderAsync(CultureInfo.GetCultureInfo("en"));
        Task<string> arabic = RenderAsync(invocation.Culture);
        string[] results = await Task.WhenAll(english, arabic);

        await Assert.That(results[0]).Contains(CliStrings.Get("Status", CultureInfo.GetCultureInfo("en")));
        await Assert.That(results[1]).Contains(CliStrings.Get("Status", CultureInfo.GetCultureInfo("ar")));
        await Assert.That(results[1]).IsNotEqualTo(results[0]);
        await Assert.That(invocation.Culture.IsReadOnly).IsTrue();
        await Assert.That(invocation.Culture.NumberFormat.NumberDecimalSeparator).IsNotEqualTo("!");
        await Assert.That(await RenderAsync(CultureInfo.GetCultureInfo("fr"))).IsEqualTo(results[0]);
        await new CliApplication().RunAsync(invocation);
        await Assert.That(Encoding.UTF8.GetString(files.StandardOutput))
            .Contains(CliStrings.Get("Scope", invocation.Culture));
    }

    [Test]
    public async Task ArabicCatalogHasEveryNeutralKeyAndMatchingFormatArguments()
    {
        var manager = new ResourceManager(typeof(CliStrings));
        ResourceSet neutral = manager.GetResourceSet(CultureInfo.InvariantCulture, true, false)!;
        ResourceSet arabic = manager.GetResourceSet(CultureInfo.GetCultureInfo("ar"), true, false)!;
        await Assert.That(arabic).IsNotNull();
        foreach (DictionaryEntry entry in neutral)
        {
            string key = (string)entry.Key;
            string? translated = arabic.GetString(key);
            await Assert.That(translated).IsNotNull();
            await Assert.That(string.IsNullOrWhiteSpace(translated)).IsFalse();
            await Assert.That(CompositeFormat.Parse(translated!).MinimumArgumentCount)
                .IsEqualTo(CompositeFormat.Parse((string)entry.Value!).MinimumArgumentCount);
        }
    }

    [Test]
    public async Task MachineEnvelopeIsIndependentOfInvocationCulture()
    {
        async Task<byte[]> RunAsync(string locale)
        {
            var files = new CliFixture();
            var invocation = new CliInvocation(["doctor", "--machine"], CliMode.Machine,
                new(files, files, files, CliFixture.RejectingAuthoringFiles, 65_536, 4 * 1024 * 1024),
                CultureInfo.GetCultureInfo(locale));
            await new CliApplication().RunAsync(invocation);
            return files.StandardOutput;
        }
        byte[][] results = await Task.WhenAll(RunAsync("en"), RunAsync("ar"));
        await Assert.That(results[0].SequenceEqual(results[1])).IsTrue();
        await Assert.That(CliMachineContractVerifier.Validate(results[0])).IsEmpty();
    }

    private static Task<string> RenderAsync(CultureInfo culture) => Task.Run(() =>
    {
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        IAnsiConsole console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No,
            ColorSystem = ColorSystemSupport.NoColors,
            Interactive = InteractionSupport.No,
            Out = new AnsiConsoleOutput(writer)
        });
        console.Profile.Width = 180;
        CliHumanOutput.Render(console, CliResults.Failure(CliExitCode.Usage, "arguments-invalid"), culture);
        return writer.ToString();
    });
}
