namespace Configurator.Presentation.Tests;

using System.Collections;
using System.Globalization;
using System.Resources;

public sealed class PresentationLocalizationTests
{
    [Test]
    public async Task EveryShippedResourceHasBothLanguagesAndValidSubstitutions()
    {
        var assembly = typeof(PresentationStrings).Assembly;
        using var english = new ResourceReader(assembly.GetManifestResourceStream("Configurator.Presentation.Resources.PresentationStrings.resources")!);
        using var arabic = new ResourceReader(assembly.GetManifestResourceStream("Configurator.Presentation.Resources.PresentationStrings.ar.resources")!);
        var englishKeys = english.Cast<DictionaryEntry>().Select(entry => (string)entry.Key).Order().ToArray();
        var arabicKeys = arabic.Cast<DictionaryEntry>().Select(entry => (string)entry.Key).Order().ToArray();
        await Assert.That(englishKeys.SequenceEqual(arabicKeys)).IsTrue();
        await Assert.That(englishKeys.Length).IsGreaterThan(0);
        foreach (string key in englishKeys)
            foreach (string locale in new[] { "en", "ar" })
                await Assert.That(PresentationStrings.Format(key, locale, 12, 64).Length).IsGreaterThan(0);
    }

    [Test]
    public async Task UnsupportedLocaleFallsBackAndDiagnosticDoesNotEchoUnknownInput()
    {
        await Assert.That(PresentationStrings.Get("Configure", "fr-FR")).IsEqualTo(PresentationStrings.Get("Configure", "en"));
        await Assert.That(PresentationStrings.Get("Configure", "invalid_locale_!")).IsEqualTo(PresentationStrings.Get("Configure", "en"));
        await Assert.That(PresentationStrings.Get("Configure", "ar-SA")).IsEqualTo(PresentationStrings.Get("Configure", "ar"));
        await Assert.That(PresentationStrings.Diagnostic("synthetic-private-value", "ar").Contains("synthetic-private-value", StringComparison.Ordinal)).IsFalse();
    }

    [Test]
    public async Task IndependentWorkspacesRefreshGuidanceWithoutChangingAnswersOrProcessCulture()
    {
        CultureInfo hostCulture = CultureInfo.CurrentCulture;
        CultureInfo hostUiCulture = CultureInfo.CurrentUICulture;
        using var english = new DefinitionFormModel(DefinitionSecretOperationTests.Definition());
        using var arabic = new DefinitionFormModel(DefinitionSecretOperationTests.Definition());
        string[] path = [english.Fields.Single().Definition.Id];
        english.SelectLocale("en");
        arabic.SelectLocale("ar");
        string englishMessage = english.GetSecretValidation(path).Requirements[0].Message;
        string arabicMessage = arabic.GetSecretValidation(path).Requirements[0].Message;
        await Assert.That(arabicMessage).IsNotEqualTo(englishMessage);
        await Assert.That(arabic.DisplaySections[0].Title).IsNotEqualTo(english.DisplaySections[0].Title);
        arabic.EditValue(path, "synthetic-locale-fixture");
        string? answer = arabic.Snapshot().Values[path[0]].GetString();
        long revision = arabic.InputRevision;
        bool displayNotified = false;
        arabic.PropertyChanged += (_, change) => displayNotified |= change.PropertyName == nameof(DefinitionFormModel.DisplaySections);
        arabic.SelectLocale("en");
        await Assert.That(arabic.GetSecretValidation(path).Requirements[0].Message).IsEqualTo(englishMessage);
        await Assert.That(english.GetSecretValidation(path).Requirements[0].Message).IsEqualTo(englishMessage);
        await Assert.That(arabic.InputRevision).IsEqualTo(revision);
        await Assert.That(arabic.Snapshot().Values[path[0]].GetString()).IsEqualTo(answer);
        await Assert.That(displayNotified).IsTrue();
        await Assert.That(CultureInfo.CurrentCulture).IsEqualTo(hostCulture);
        await Assert.That(CultureInfo.CurrentUICulture).IsEqualTo(hostUiCulture);
    }
}
