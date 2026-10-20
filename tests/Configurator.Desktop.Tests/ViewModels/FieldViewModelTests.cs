namespace Configurator.Desktop.Tests.ViewModels;

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Configurator.Core.Definitions;
using Configurator.Desktop.ViewModels.Fields;
using Configurator.Presentation;

public sealed class FieldViewModelTests
{
    [Test]
    public async Task OptionalAdvancedSecretDoesNotOpenBecauseItIsProtected()
    {
        using var form = Form("""
            {"id":"token","title":"Token","type":"string","sensitivity":"secret","disclosure":"advanced",
             "consequence":"Acquire a token only if this optional integration is needed."}
            """);
        var fields = new FormFieldFactory().Create(form);
        try
        {
            await Assert.That(fields.Single().IsVisible).IsFalse();
            await Assert.That(fields.Single().RequiresMasking).IsTrue();
            long revision = form.InputRevision;
            form.SetAllSectionsExpanded(true);
            await Assert.That(fields.Single().IsVisible).IsTrue();
            form.SetAllSectionsExpanded(false);
            await Assert.That(fields.Single().IsVisible).IsFalse();
            await Assert.That(form.Snapshot().Values.ContainsKey("token")).IsFalse();
            await Assert.That(form.InputRevision).IsEqualTo(revision);
        }
        finally { Dispose(fields); }
    }

    [Test]
    public async Task LiteralNestedPathsFollowSurvivingListMembershipForCorrections()
    {
        using var form = Form("""
            {"id":"rows.list","title":"Rows","type":"list","itemType":"object","sensitivity":"public","disclosure":"essential",
             "properties":[{"id":"value.part","title":"Value","type":"string","maxLength":2,"sensitivity":"secret","disclosure":"advanced"}]}
            """);
        var fields = new FormFieldFactory().Create(form);
        try
        {
            var list = (ListFieldViewModel)fields.Single();
            list.Text = """[{"value.part":"ok"},{"value.part":"ok"}]""";
            var first = (ObjectFieldViewModel)list.Items[0];
            var second = (ObjectFieldViewModel)list.Items[1];
            var firstValue = first.Children.Single();
            var secondValue = second.Children.Single();
            await Assert.That(firstValue.IsVisible).IsFalse();
            await Assert.That(secondValue.IsVisible).IsFalse();
            secondValue.Text = "invalid";
            await Assert.That(firstValue.IsVisible).IsFalse();
            await Assert.That(secondValue.IsVisible).IsTrue();
            list.RemoveItemCommand.Execute(first);
            await Assert.That(ReferenceEquals(list.Items.Single(), second)).IsTrue();
            await Assert.That(secondValue.IsVisible).IsTrue();
            await Assert.That(form.Snapshot().Values["rows.list"].GetArrayLength()).IsEqualTo(1);
            secondValue.Text = "ok";
            await Assert.That(secondValue.IsVisible).IsFalse();
            long revision = form.InputRevision;
            form.SetAllSectionsExpanded(true);
            await Assert.That(secondValue.IsVisible).IsTrue();
            form.SetAllSectionsExpanded(false);
            await Assert.That(secondValue.IsVisible).IsFalse();
            await Assert.That(form.InputRevision).IsEqualTo(revision);
            await Assert.That(ReferenceEquals(second.Children.Single(), secondValue)).IsTrue();
        }
        finally { Dispose(fields); }
    }

    [Test]
    public async Task EmptyEnumSelectionSurvivesRefreshAndClearMeansOmission()
    {
        using var form = Form("""
            {"id":"value","title":"Value","type":"enum","choices":["","other"],"sensitivity":"public","disclosure":"guided"}
            """);
        var fields = new FormFieldFactory().Create(form);
        try
        {
            var field = (EnumFieldViewModel)fields.Single();
            long before = form.InputRevision;
            form.SetAllSectionsExpanded(true);
            field.Refresh();
            await Assert.That(form.InputRevision).IsEqualTo(before);
            await Assert.That(form.Snapshot().Values.ContainsKey("value")).IsFalse();
            field.SelectedIndex = 2;
            field.SelectedIndex = 1;
            await Assert.That(form.Snapshot().Values["value"].GetRawText()).IsEqualTo("\"\"");
            before = form.InputRevision;
            form.SetAllSectionsExpanded(false);
            field.Refresh();
            await Assert.That(form.InputRevision).IsEqualTo(before);
            field.ClearCommand.Execute(null);
            await Assert.That(form.Snapshot().Values.ContainsKey("value")).IsFalse();
        }
        finally { Dispose(fields); }
    }

    [Test]
    public async Task DefaultEmptyStringAndErasedStringRemainSuppliedUntilClear()
    {
        using var form = Form("""
            {"id":"value","title":"Value","type":"string","default":"","sensitivity":"public","disclosure":"guided"}
            """);
        var fields = new FormFieldFactory().Create(form);
        try
        {
            FieldViewModel field = fields.Single();
            long before = form.InputRevision;
            form.SetAllSectionsExpanded(true);
            field.Refresh();
            await Assert.That(form.InputRevision).IsEqualTo(before);
            await Assert.That(form.Snapshot().Values["value"].GetRawText()).IsEqualTo("\"\"");
            field.Text = "erase";
            field.Text = "";
            await Assert.That(form.Snapshot().Values["value"].GetRawText()).IsEqualTo("\"\"");
            field.ClearCommand.Execute(null);
            await Assert.That(form.Snapshot().Values.ContainsKey("value")).IsFalse();
        }
        finally { Dispose(fields); }
    }

    [Test]
    public async Task FactoryProjectsEveryDeclaredType()
    {
        using var form = Form();
        var fields = new FormFieldFactory().Create(form);
        try
        {
            await Assert.That(fields.Select(field => field.GetType()).ToArray()).IsEquivalentTo(
                new[] { typeof(StringFieldViewModel), typeof(EnumFieldViewModel), typeof(BooleanFieldViewModel),
                    typeof(NumericFieldViewModel), typeof(NumericFieldViewModel), typeof(ObjectFieldViewModel),
                    typeof(ListFieldViewModel), typeof(StringFieldViewModel) });
        }
        finally { Dispose(fields); }
    }

    [Test]
    [Arguments("string")]
    [Arguments("enum")]
    [Arguments("boolean")]
    [Arguments("integer")]
    [Arguments("number")]
    public async Task EveryNonPublicScalarMasksAndSuppressesChoiceDisclosure(string type)
    {
        string extra = type == "enum" ? ""","choices":["synthetic-private-choice","other"]""" : "";
        using var form = Form($$"""
            {"id":"value","title":"Protected","help":"Declared help","type":"{{type}}","sensitivity":"sensitive","disclosure":"guided"{{extra}}}
            """);
        var fields = new FormFieldFactory().Create(form);
        try
        {
            var field = fields.Single();
            field.Text = "synthetic-private-answer";
            await Assert.That(field.RequiresMasking).IsTrue();
            await Assert.That(field.PasswordChar).IsEqualTo('●');
            await Assert.That(field.Name).IsEqualTo("Protected");
            await Assert.That(field.HelpText.Contains("synthetic-private", StringComparison.Ordinal)).IsFalse();
            if (field is EnumFieldViewModel choice)
            {
                await Assert.That(choice.Choices.Count).IsEqualTo(0);
                await Assert.That(choice.SelectedIndex).IsEqualTo(-1);
            }
            if (field is BooleanFieldViewModel boolean)
                await Assert.That(boolean.Value).IsNull();
        }
        finally { Dispose(fields); }
    }

    [Test]
    public async Task SensitivityIsInheritedThroughObjectListRows()
    {
        using var form = Form("""
            {"id":"rows","title":"Rows","type":"list","itemType":"object","sensitivity":"secret","disclosure":"guided",
             "properties":[{"id":"count","title":"Count","type":"integer","sensitivity":"public","disclosure":"guided"}]}
            """);
        var fields = new FormFieldFactory().Create(form);
        try
        {
            var list = (ListFieldViewModel)fields.Single();
            list.AddItemCommand.Execute(null);
            var count = ((ObjectFieldViewModel)list.Items.Single()).Children.Single();
            await Assert.That(count.PasswordChar).IsEqualTo('●');
            count.Text = "42";
            await Assert.That(form.Fields.Single().Text).IsEqualTo("""[{"count":42}]""");
        }
        finally { Dispose(fields); }
    }

    [Test]
    public async Task EffectiveContainerSensitivityMasksPublicSiblingsAndSuppressesRecommendations()
    {
        using var form = Form("""
            {"id":"owner","title":"Owner","type":"object","sensitivity":"public","disclosure":"guided",
             "properties":[
               {"id":"name","title":"Name","type":"string","sensitivity":"sensitive","disclosure":"guided"},
               {"id":"age","title":"Age","type":"integer","recommended":7,"sensitivity":"public","disclosure":"guided"},
               {"id":"mode","title":"Mode","type":"enum","choices":["first","second"],"sensitivity":"public","disclosure":"guided"}]}
            """);
        var fields = new FormFieldFactory().Create(form);
        try
        {
            var owner = (ObjectFieldViewModel)fields.Single();
            await Assert.That(owner.RequiresMasking).IsTrue();
            await Assert.That(owner.Children.All(field => field.PasswordChar == '●')).IsTrue();
            var age = owner.Children.Single(field => field.Id == "age");
            await Assert.That(age.UseRecommendedCommand.CanExecute(null)).IsFalse();
            await Assert.That(((EnumFieldViewModel)owner.Children.Single(field => field.Id == "mode")).Choices.Count).IsEqualTo(0);
        }
        finally { Dispose(fields); }
    }

    [Test]
    public async Task ScalarEditsImportsAndActionsSynchronizeBothDirections()
    {
        using var form = Form("""
            {"id":"value","title":"Value","type":"integer","sensitivity":"public","disclosure":"guided",
             "minimum":1,"maximum":9,"recommended":4}
            """);
        var fields = new FormFieldFactory().Create(form);
        try
        {
            var field = fields.Single();
            field.Text = "3";
            await Assert.That(form.Fields.Single().Text).IsEqualTo("3");
            form.Fields.Single().Text = "7";
            await Assert.That(field.Text).IsEqualTo("7");
            field.UseRecommendedCommand.Execute(null);
            await Assert.That(form.Fields.Single().Text).IsEqualTo("4");
            field.ClearCommand.Execute(null);
            await Assert.That(form.Fields.Single().Text).IsEmpty();
            await Assert.That(field.ConstraintsSummary).Contains("minimum: 1");
            await Assert.That(field.ConstraintsSummary).Contains("maximum: 9");
            field.RequestFocus();
            await Assert.That(field.FocusRequestVersion > 0).IsTrue();
        }
        finally { Dispose(fields); }
    }

    [Test]
    public async Task ExpansionRestoresAuthoredDefaultsAndKeepsActiveCorrectionsReachable()
    {
        using var form = Form();
        var fields = new FormFieldFactory().Create(form);
        try
        {
            var note = fields.Single(field => field.Id == "note");
            await Assert.That(note.IsVisible).IsFalse();
            await Assert.That(fields.Single(field => field.Id == "mode").IsVisible).IsTrue();
            await Assert.That(fields.Single(field => field.Id == "owner").IsVisible).IsTrue();
            form.Fields.Single(input => input.Definition.Id == "note").Text = "too-long-note";
            note.Refresh();
            await Assert.That(note.HasError).IsTrue();
            await Assert.That(note.IsVisible).IsTrue();
            await Assert.That(note.ErrorText.Contains("too-long-note", StringComparison.Ordinal)).IsFalse();
            note.ClearCommand.Execute(null);
            await Assert.That(note.HasError).IsFalse();
            long revision = form.InputRevision;
            form.SetAllSectionsExpanded(true);
            await Assert.That(note.IsVisible).IsTrue();
            form.SetAllSectionsExpanded(false);
            await Assert.That(note.IsVisible).IsFalse();
            await Assert.That(form.InputRevision).IsEqualTo(revision);
            await Assert.That(ReferenceEquals(note, fields.Single(field => field.Id == "note"))).IsTrue();
        }
        finally { Dispose(fields); }
    }

    [Test]
    public async Task StructuredEditingPreservesTypedValuesDefaultsAndOptionalOmission()
    {
        using var form = Form("""
            {"id":"settings","title":"Settings","type":"object","sensitivity":"public","disclosure":"guided",
             "properties":[
               {"id":"count","title":"Count","type":"integer","default":2,"sensitivity":"public","disclosure":"guided"},
               {"id":"mode","title":"Mode","type":"enum","choices":["one","two"],"sensitivity":"public","disclosure":"guided"},
               {"id":"note","title":"Note","type":"string","sensitivity":"public","disclosure":"guided"},
               {"id":"nested","title":"Nested","type":"object","required":true,"sensitivity":"public","disclosure":"guided","properties":[]}]}
            """);
        var fields = new FormFieldFactory().Create(form);
        try
        {
            var obj = (ObjectFieldViewModel)fields.Single();
            await Assert.That(form.Fields.Single().Text).IsEmpty();
            obj.Children.Single(field => field.Id == "mode").Text = "two";
            using var json = JsonDocument.Parse(form.Fields.Single().Text);
            await Assert.That(json.RootElement.GetProperty("count").GetInt32()).IsEqualTo(2);
            await Assert.That(json.RootElement.GetProperty("mode").GetString()).IsEqualTo("two");
            await Assert.That(json.RootElement.TryGetProperty("note", out _)).IsFalse();
            await Assert.That(json.RootElement.GetProperty("nested").ValueKind).IsEqualTo(JsonValueKind.Object);
            form.Fields.Single().Text = """{"count":8,"note":"imported","nested":{}}""";
            await Assert.That(obj.Children.Single(field => field.Id == "count").Text).IsEqualTo("8");
            await Assert.That(obj.Children.Single(field => field.Id == "note").Text).IsEqualTo("imported");
            obj.Children.Single(field => field.Id == "count").Text = "invalid-number";
            await Assert.That(form.HasInputErrors).IsTrue();
            obj.ClearCommand.Execute(null);
            await Assert.That(form.Fields.Single().Text).IsEmpty();
            await Assert.That(obj.Children.Single(field => field.Id == "note").Text).IsEmpty();
        }
        finally { Dispose(fields); }
    }

    [Test]
    public async Task BoundedRowsPreserveDefaultsAndEmptyJsonValuesOnRemoval()
    {
        using var form = Form("""
            {"id":"rows","title":"Rows","type":"list","itemType":"object","maxItems":2,"sensitivity":"public","disclosure":"guided",
             "properties":[
               {"id":"enabled","title":"Enabled","type":"boolean","default":false,"sensitivity":"public","disclosure":"guided"},
               {"id":"aliases","title":"Aliases","type":"list","itemType":"string","required":true,"sensitivity":"public","disclosure":"guided"}]}
            """);
        var fields = new FormFieldFactory().Create(form);
        try
        {
            var list = (ListFieldViewModel)fields.Single();
            list.AddItemCommand.Execute(null);
            var first = list.Items.Single();
            var aliases = (ListFieldViewModel)((ObjectFieldViewModel)first).Children.Single(field => field.Id == "aliases");
            aliases.AddItemCommand.Execute(null);
            await Assert.That(form.Fields.Single().Text).IsEqualTo("""[{"enabled":false,"aliases":[""]}]""");
            list.AddItemCommand.Execute(null);
            await Assert.That(list.AddItemCommand.CanExecute(null)).IsFalse();
            list.AddItemCommand.Execute(null);
            await Assert.That(list.Items.Count).IsEqualTo(2);
            int focusBeforeRemoval = list.FocusRequestVersion;
            list.RemoveItemCommand.Execute(first);
            await Assert.That(list.FocusRequestVersion > focusBeforeRemoval).IsTrue();
            first.Text = "ignored";
            await Assert.That(form.Fields.Single().Text).IsEqualTo("""[{"enabled":false,"aliases":[]}]""");
            list.RemoveItemCommand.Execute(list.Items.Single());
            await Assert.That(form.Fields.Single().Text).IsEqualTo("[]");
            list.ClearCommand.Execute(null);
            await Assert.That(form.Fields.Single().Text).IsEmpty();
        }
        finally { Dispose(fields); }
    }

    [Test]
    public async Task DisposalDetachesInputAndPreventsRetiredEdits()
    {
        using var form = Form();
        var fields = new FormFieldFactory().Create(form);
        var name = fields.Single(field => field.Id == "name");
        Dispose(fields);
        form.Fields[0].Text = "later";
        name.Text = "retired";
        name.ClearCommand.Execute(null);
        await Assert.That(form.Fields[0].Text).IsEqualTo("later");
    }

    [Test]
    public async Task OptionalEssentialFieldStaysVisibleAndReportsStringBounds()
    {
        using var form = Form("""
            {"id":"note","title":"Note","type":"string","sensitivity":"public","disclosure":"essential","minLength":1,"maxLength":8}
            """);
        var fields = new FormFieldFactory().Create(form);
        try
        {
            var note = fields.Single();
            await Assert.That(note.IsVisible).IsTrue();
            await Assert.That(note.ConstraintsSummary).Contains("minLength: 1");
            await Assert.That(note.ConstraintsSummary).Contains("maxLength: 8");
        }
        finally { Dispose(fields); }
    }

    [Test]
    public async Task ListRemovalRespectsDeclaredMinimumAndRejectsForeignRows()
    {
        using var form = Form("""
            {"id":"rows","title":"Rows","type":"list","itemType":"integer","minItems":1,"maxItems":2,"sensitivity":"public","disclosure":"guided"}
            """);
        var fields = new FormFieldFactory().Create(form);
        try
        {
            var list = (ListFieldViewModel)fields.Single();
            list.AddItemCommand.Execute(null);
            var first = list.Items.Single();
            await Assert.That(list.RemoveItemCommand.CanExecute(first)).IsFalse();
            list.RemoveItemCommand.Execute(first);
            await Assert.That(form.Fields.Single().Text).IsEqualTo("[0]");
            list.AddItemCommand.Execute(null);
            await Assert.That(list.RemoveItemCommand.CanExecute(first)).IsTrue();
            list.RemoveItemCommand.Execute(first);
            await Assert.That(list.RemoveItemCommand.CanExecute(first)).IsFalse();
            await Assert.That(list.Items.Count).IsEqualTo(1);
            list.ClearCommand.Execute(null);
            await Assert.That(form.Fields.Single().Text).IsEmpty();
        }
        finally { Dispose(fields); }
    }

    [Test]
    [NotInParallel]
    public async Task CompiledTemplatesRecursivelyMaskAllScalarsAndSynchronizeActualControls()
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(DesktopApplicationTests));
        try
        {
            var result = await session.Dispatch(() =>
            {
                using var form = Form("""
                    {"id":"settings","title":"Settings","type":"object","sensitivity":"secret","disclosure":"guided",
                     "properties":[
                       {"id":"name","title":"Name","type":"string","sensitivity":"public","disclosure":"guided"},
                       {"id":"mode","title":"Mode","type":"enum","choices":["synthetic-choice","other"],"sensitivity":"public","disclosure":"guided"},
                       {"id":"enabled","title":"Enabled","type":"boolean","sensitivity":"public","disclosure":"guided"},
                       {"id":"count","title":"Count","type":"integer","sensitivity":"public","disclosure":"guided"},
                       {"id":"ratio","title":"Ratio","type":"number","sensitivity":"public","disclosure":"guided"},
                       {"id":"aliases","title":"Aliases","type":"list","itemType":"string","sensitivity":"public","disclosure":"guided"}]}
                    """);
                form.Fields.Single().Text = """
                    {"name":"synthetic-answer","mode":"synthetic-choice","enabled":true,"count":42,"ratio":0.5}
                    """;
                var fields = new FormFieldFactory().Create(form);
                var window = new Window { Content = new ContentControl { Content = fields.Single() } };
                try
                {
                    window.Show();
                    window.UpdateLayout();
                    Dispatcher.UIThread.RunJobs();
                    var inputs = window.GetLogicalDescendants().OfType<TextBox>().Where(control => control.IsEffectivelyVisible).ToArray();
                    bool masked = inputs.Length == 5 && inputs.All(control => control.PasswordChar == '●');
                    bool named = inputs.All(control => !string.IsNullOrEmpty(AutomationProperties.GetName(control)) &&
                        !(AutomationProperties.GetHelpText(control) ?? "").Contains("synthetic", StringComparison.Ordinal));
                    bool choicesHidden = window.GetLogicalDescendants().OfType<ComboBox>()
                        .All(control => !control.IsEffectivelyVisible && !control.Items.Cast<object>().Any());
                    inputs.Single(control => AutomationProperties.GetName(control) == "Count").Text = "43";
                    Dispatcher.UIThread.RunJobs();
                    using var changed = JsonDocument.Parse(form.Fields.Single().Text);
                    bool synchronized = changed.RootElement.GetProperty("count").GetInt32() == 43;
                    var list = (ListFieldViewModel)((ObjectFieldViewModel)fields.Single()).Children.Single(field => field.Id == "aliases");
                    list.AddItemCommand.Execute(null);
                    window.UpdateLayout();
                    Dispatcher.UIThread.RunJobs();
                    var row = window.GetLogicalDescendants().OfType<TextBox>().Single(control => AutomationProperties.GetName(control) == "Aliases item");
                    list.RequestFocus();
                    Dispatcher.UIThread.RunJobs();
                    bool focus = row.IsFocused;
                    row.Text = "synthetic-alias";
                    Dispatcher.UIThread.RunJobs();
                    bool recursive = row.PasswordChar == '●' && form.Fields.Single().Text.Contains("synthetic-alias", StringComparison.Ordinal);
                    list.Items.Single().ClearCommand.Execute(null);
                    using var cleared = JsonDocument.Parse(form.Fields.Single().Text);
                    recursive &= cleared.RootElement.GetProperty("aliases")[0].GetString() == "";
                    var add = window.GetLogicalDescendants().OfType<Button>()
                        .Single(control => AutomationProperties.GetName(control) == "Add item to Aliases");
                    bool actionNames = window.GetLogicalDescendants().OfType<Button>()
                        .Any(control => AutomationProperties.GetName(control) == "Remove item from Aliases") &&
                        window.GetLogicalDescendants().OfType<Button>()
                        .Any(control => AutomationProperties.GetName(control) == "Clear Settings");
                    list.RemoveItemCommand.Execute(list.Items.Single());
                    Dispatcher.UIThread.RunJobs();
                    focus &= add.IsFocused;
                    fields.Single().ClearCommand.Execute(null);
                    Dispatcher.UIThread.RunJobs();
                    focus &= inputs.Single(control => AutomationProperties.GetName(control) == "Name").IsFocused;
                    return (masked, named, choicesHidden, synchronized, recursive, actionNames, focus);
                }
                finally { window.Close(); Dispose(fields); }
            }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30)).ConfigureAwait(false);
            await Assert.That(result.masked).IsTrue();
            await Assert.That(result.named).IsTrue();
            await Assert.That(result.choicesHidden).IsTrue();
            await Assert.That(result.synchronized).IsTrue();
            await Assert.That(result.recursive).IsTrue();
            await Assert.That(result.actionNames).IsTrue();
            await Assert.That(result.focus).IsTrue();
        }
        finally { await Task.Run(session.Dispose).ConfigureAwait(false); }
    }

    private static DefinitionFormModel Form(string? field = null)
    {
        JsonObject source = JsonNode.Parse(DesktopApplicationTests.Definition())!.AsObject();
        if (field is not null)
        {
            source["fields"] = new JsonArray(JsonNode.Parse(field));
            source["artifacts"] = JsonNode.Parse("""
                [{"id":"config","format":"json","fileName":"config.json","applySemantics":"create","restart":"process",
                  "projection":{"op":"literal","value":{}}}]
                """);
        }
        return new DefinitionFormModel(SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString())));
    }

    private static void Dispose(IEnumerable<FieldViewModel> fields)
    {
        foreach (var field in fields) field.Dispose();
    }
}
