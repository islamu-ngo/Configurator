namespace Configurator.Desktop.Tests.ViewModels;

using Configurator.Core.Definitions;
using Configurator.Desktop.ViewModels.Fields;
using Configurator.Presentation;
using System.Text.Json;

public sealed class DefinitionV2EditingTests
{
    [Test]
    public async Task MapEntriesKeepLiteralIdentityAndSafeCorrectionFactsAcrossRemoval()
    {
        using var form = new DefinitionFormModel(Definition());
        using var factory = new FormFieldFactory();
        var map = (MapFieldViewModel)factory.Create(form).Single(field => field.Id == "named");
        map.NewKey = "literal/key";
        map.AddEntryCommand.Execute(null);
        FieldViewModel first = map.Entries.Single();
        await Assert.That(first.HasError).IsTrue();
        first.Text = "5";
        await Assert.That(first.HasError).IsFalse();
        map.NewKey = "LITERAL/key";
        map.AddEntryCommand.Execute(null);
        FieldViewModel removed = map.Entries.Single(entry => entry.Id == "LITERAL/key");
        map.RemoveEntryCommand.Execute(removed);
        long revision = form.InputRevision;
        removed.Text = "99";
        await Assert.That(form.InputRevision).IsEqualTo(revision);
        await Assert.That(ReferenceEquals(first, map.Entries.Single())).IsTrue();
        await Assert.That(form.Snapshot().Values["named"].GetProperty("literal/key").GetInt32()).IsEqualTo(5);
        map.NewKey = "private-draft";
        factory.Dispose();
        await Assert.That(map.NewKey).IsEqualTo("");
        await Assert.That(map.AddEntryCommand.CanExecute(null)).IsFalse();
    }

    [Test]
    public async Task ReselectingProviderKeepsValuesButSwitchingBranchRetiresOldEditors()
    {
        using var form = new DefinitionFormModel(Definition());
        using var factory = new FormFieldFactory();
        var provider = (VariantFieldViewModel)factory.Create(form).Single(field => field.Id == "provider");
        provider.SelectedChoice = provider.Choices[0];
        FieldViewModel count = provider.Children.Single(child => child.Id == "count");
        count.Text = "3";
        provider.SelectedChoice = provider.Choices[0];
        await Assert.That(form.Snapshot().Values["provider"].GetProperty("count").GetInt32()).IsEqualTo(3);
        provider.SelectedChoice = provider.Choices[1];
        long revision = form.InputRevision;
        count.Text = "9";
        await Assert.That(form.InputRevision).IsEqualTo(revision);
        await Assert.That(form.Snapshot().Values["provider"].GetRawText()).IsEqualTo("""{"kind":"second"}""");
    }

    [Test]
    public async Task NativeLabelsAndTypedValuesUseSharedLocaleAndRequiredFacts()
    {
        using var form = new DefinitionFormModel(Definition());
        using var factory = new FormFieldFactory();
        IReadOnlyList<FieldViewModel> fields = factory.Create(form);
        var choice = (EnumFieldViewModel)fields.Single(field => field.Id == "choice");
        var settings = (ObjectFieldViewModel)fields.Single(field => field.Id == "settings");
        FieldViewModel value = settings.Children.Single(child => child.Id == "value");
        await Assert.That(choice.Choices[1].Label).IsEqualTo(choice.Choices[2].Label);
        await Assert.That(choice.Choices[1].Index).IsNotEqualTo(choice.Choices[2].Index);
        await Assert.That(value.IsRequired).IsFalse();
        form.EditValue(["settings", "enabled"], "true");
        await Assert.That(value.IsRequired).IsTrue();
        await Assert.That(value.HasError).IsTrue();
        choice.SetNullCommand.Execute(null);
        await Assert.That(form.Snapshot().Values["choice"].ValueKind).IsEqualTo(JsonValueKind.Null);
        choice.ResetCommand.Execute(null);
        choice.ApplySuggestionCommand.Execute(null);
        await Assert.That(form.Snapshot().Values["choice"].GetInt32()).IsEqualTo(2);
        long before = form.InputRevision;
        form.SelectLocale("ar");
        await Assert.That(choice.Choices[1].Label).IsEqualTo("Translated equal");
        await Assert.That(form.InputRevision).IsEqualTo(before);
        await Assert.That(form.Snapshot().Values["choice"].GetInt32()).IsEqualTo(2);
    }

    [Test]
    public async Task RepeatedDeliberateScalarEditStillRetiresPriorOperationAuthority()
    {
        using var form = new DefinitionFormModel(Definition());
        using var factory = new FormFieldFactory();
        var text = (StringFieldViewModel)factory.Create(form).Single(field => field.Id == "text");
        text.Text = "same";
        long before = form.InputRevision;
        text.Text = "same";
        await Assert.That(form.InputRevision).IsEqualTo(before + 1);
        await Assert.That(form.Snapshot().Values["text"].GetString()).IsEqualTo("same");
    }

    [Test]
    public async Task RepeatedTypedChoiceIsIntentEvenWhenTheInitializedValueAlreadyMatches()
    {
        using var form = new DefinitionFormModel(Definition());
        using var factory = new FormFieldFactory();
        var choice = (EnumFieldViewModel)factory.Create(form).Single(field => field.Id == "choice");
        long before = form.InputRevision;
        choice.SelectedIndex = 1;
        await Assert.That(form.InputRevision).IsEqualTo(before + 1);
        await Assert.That(form.Snapshot().Values["choice"].GetInt32()).IsEqualTo(1);
    }

    internal static SetupDefinition Definition() => SetupDefinitionSourceCodec.ParseJson("""
        {"$schema":"urn:islamu:configurator:authoring:v2",
         "identity":{"familyId":"380d5cdd-e1a1-4ddd-91c1-05f42486c0e8","revision":1},
         "project":{"id":"desktop-typed","name":"Desktop typed"},"applicationVersions":["1"],
         "configuration":{"type":"object","additionalProperties":false,"x-configurator":{"sensitivity":"public"},"properties":{
           "text":{"type":"string","x-configurator":{"sensitivity":"public","detail":"basic"}},
           "settings":{"type":"object","additionalProperties":false,"x-configurator":{"sensitivity":"public","detail":"basic","initialValue":{}},"properties":{
             "enabled":{"type":"boolean","x-configurator":{"sensitivity":"public","detail":"basic","initialValue":false}},
             "value":{"type":"integer","x-configurator":{"sensitivity":"public","detail":"basic"}}},
             "if":{"properties":{"enabled":{"const":true}},"required":["enabled"]},"then":{"required":["value"]}},
           "named":{"type":"object","x-configurator":{"sensitivity":"public","detail":"basic"},
             "additionalProperties":{"type":"integer","minimum":1,"x-configurator":{"sensitivity":"public","detail":"basic"}}},
           "provider":{"type":"object","x-configurator":{"sensitivity":"public","detail":"basic"},"oneOf":[
             {"type":"object","additionalProperties":false,"required":["kind"],"x-configurator":{"sensitivity":"public"},"properties":{
               "kind":{"type":"string","const":"first","x-configurator":{"sensitivity":"public","detail":"basic"}},"count":{"type":"integer","x-configurator":{"sensitivity":"public","detail":"basic"}}}},
             {"type":"object","additionalProperties":false,"required":["kind"],"x-configurator":{"sensitivity":"public"},"properties":{
               "kind":{"type":"string","const":"second","x-configurator":{"sensitivity":"public","detail":"basic"}},"other":{"type":"string","x-configurator":{"sensitivity":"public","detail":"basic"}}}}]},
           "choice":{"type":["integer","null"],"enum":[1,2,null],"x-configurator":{"sensitivity":"public","detail":"basic",
             "initialValue":1,"suggestedValue":2,"choices":[{"value":1,"label":"Equal"},{"value":2,"label":"Equal"},{"value":null,"label":"Null"}]}}
         }},
         "localization":{"defaultLocale":"en","translations":{"ar":{"direction":"rtl","overrides":{
           "/configuration/properties/choice/x-configurator/choices/0/label":"Translated equal"}}}},
         "outputs":{"config.json":{"id":"config","format":"json"}}}
        """u8.ToArray());
}
