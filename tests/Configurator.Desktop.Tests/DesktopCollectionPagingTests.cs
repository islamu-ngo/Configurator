namespace Configurator.Desktop.Tests;

using System.Text;
using System.Text.Json.Nodes;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Configurator.Core.Definitions;
using Configurator.Desktop.ViewModels.Fields;
using Configurator.Presentation;

[NotInParallel]
public sealed class DesktopCollectionPagingTests
{
    [Test]
    public async Task LargeListMountsOnlyTwentyFiveImmediateRows()
    {
        int mounted = await MountedInputs(nested: false);
        await Assert.That(mounted).IsEqualTo(25);
    }

    [Test]
    public async Task UnopenedNestedCollectionsMountNoScalarDescendants()
    {
        int mounted = await MountedInputs(nested: true);
        await Assert.That(mounted).IsEqualTo(0);
    }

    [Test]
    public async Task UnopenedObjectRowsMountNoScalarDescendants()
    {
        int mounted = await MountedInputs(nested: false, objectRows: true);
        await Assert.That(mounted).IsEqualTo(0);
    }

    [Test]
    public async Task LargeMapMountsTwentyFiveRowsAndItsKeyInput()
    {
        int mounted = await MountedInputs(nested: false, map: true);
        await Assert.That(mounted).IsEqualTo(26);
    }

    [Test]
    public async Task RemovingAnEarlierListRowKeepsDraftIdentityAndClampsThePage()
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(DesktopApplicationTests));
        try
        {
            var result = await session.Dispatch(() =>
            {
                using var form = CreateForm();
                using var factory = new FormFieldFactory();
                var list = (ListFieldViewModel)factory.Create(form).Single();
                FieldViewModel survivor = list.Items[50];
                string identity = form.GetEditorIdentity(["rows", "50"]);
                survivor.Text = "unfinished-number";
                var window = new Window { Content = new ContentControl { Content = list } };
                try
                {
                    window.Show();
                    window.UpdateLayout();
                    Dispatcher.UIThread.RunJobs();
                    Button next = window.GetLogicalDescendants().OfType<Button>().Single(button =>
                        AutomationProperties.GetName(button) == list.Strings["NextPage"]);
                    next.Command!.Execute(next.CommandParameter);
                    next.Command.Execute(next.CommandParameter);
                    window.UpdateLayout();
                    Dispatcher.UIThread.RunJobs();
                    int lastPageRows = window.GetLogicalDescendants().OfType<TextBox>()
                        .Count(control => control.IsEffectivelyVisible && control.DataContext is FieldViewModel);
                    list.RemoveItemCommand.Execute(list.Items[0]);
                    window.UpdateLayout();
                    Dispatcher.UIThread.RunJobs();
                    int clampedRows = window.GetLogicalDescendants().OfType<TextBox>()
                        .Count(control => control.IsEffectivelyVisible && control.DataContext is FieldViewModel);
                    bool pageText = window.GetLogicalDescendants().OfType<TextBlock>().Any(block =>
                        block.Text == list.Strings.Format("CollectionPageFormat", 26, 50, 50, 2, 2));
                    bool mounted = window.GetLogicalDescendants().OfType<TextBox>().Any(control =>
                        control.IsEffectivelyVisible && ReferenceEquals(control.DataContext, survivor));
                    return (lastPageRows, clampedRows, pageText, mounted,
                        same: ReferenceEquals(list.Items[49], survivor),
                        identity: form.GetEditorIdentity(["rows", "49"]) == identity,
                        draft: form.ReadDraftText(["rows", "49"]) == "unfinished-number");
                }
                finally { window.Close(); }
            }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30)).ConfigureAwait(false);
            await Assert.That(result.lastPageRows).IsEqualTo(1);
            await Assert.That(result.clampedRows).IsEqualTo(25);
            await Assert.That(result.pageText).IsTrue();
            await Assert.That(result.mounted).IsTrue();
            await Assert.That(result.same).IsTrue();
            await Assert.That(result.identity).IsTrue();
            await Assert.That(result.draft).IsTrue();
        }
        finally { await Task.Run(session.Dispose).ConfigureAwait(false); }
    }

    [Test]
    public async Task RequestingAnOffPageEditorRevealsItsExactCollectionPage()
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(DesktopApplicationTests));
        try
        {
            var result = await session.Dispatch(() =>
            {
                using var form = CreateForm();
                using var factory = new FormFieldFactory();
                var list = (ListFieldViewModel)factory.Create(form).Single();
                FieldViewModel target = list.Items[50];
                var window = new Window { Content = new ContentControl { Content = list } };
                try
                {
                    window.Show();
                    window.UpdateLayout();
                    Dispatcher.UIThread.RunJobs();
                    int initialRows = window.GetLogicalDescendants().OfType<TextBox>()
                        .Count(control => control.IsEffectivelyVisible && control.DataContext is FieldViewModel);
                    target.RequestFocus();
                    window.UpdateLayout();
                    Dispatcher.UIThread.RunJobs();
                    TextBox? editor = window.GetLogicalDescendants().OfType<TextBox>().SingleOrDefault(control =>
                        control.IsEffectivelyVisible && ReferenceEquals(control.DataContext, target));
                    bool pageText = window.GetLogicalDescendants().OfType<TextBlock>().Any(block =>
                        block.Text == list.Strings.Format("CollectionPageFormat", 51, 51, 51, 3, 3));
                    return (initialRows, pageText, mounted: editor is not null, focused: editor?.IsFocused == true);
                }
                finally { window.Close(); }
            }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30)).ConfigureAwait(false);
            await Assert.That(result.initialRows).IsEqualTo(25);
            await Assert.That(result.pageText).IsTrue();
            await Assert.That(result.mounted).IsTrue();
            await Assert.That(result.focused).IsTrue();
        }
        finally { await Task.Run(session.Dispose).ConfigureAwait(false); }
    }

    private static async Task<int> MountedInputs(bool nested, bool map = false, bool objectRows = false)
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(DesktopApplicationTests));
        try
        {
            return await session.Dispatch(() =>
            {
                using var form = CreateForm(nested, map, objectRows);
                using var factory = new FormFieldFactory();
                var window = new Window { Content = new ContentControl { Content = factory.Create(form).Single() } };
                try
                {
                    window.Show();
                    window.UpdateLayout();
                    Dispatcher.UIThread.RunJobs();
                    return window.GetLogicalDescendants().OfType<TextBox>().Count();
                }
                finally { window.Close(); }
            }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30)).ConfigureAwait(false);
        }
        finally { await Task.Run(session.Dispose).ConfigureAwait(false); }
    }

    internal static DefinitionFormModel CreateForm(bool nested = false, bool map = false, bool objectRows = false)
    {
        var source = JsonNode.Parse("""
            {"$schema":"urn:islamu:configurator:authoring:v0.0.1",
             "identity":{"familyId":"28a8b036-d638-44e2-9242-c51b14a24a35","revision":1},
             "project":{"id":"collection-paging","name":"Collection paging"},"applicationVersions":["1"],
             "outputs":{"config.json":{"id":"config","format":"json"}}}
            """)!.AsObject();
        JsonNode item = JsonNode.Parse("""{"type":"integer","x-configurator":{"sensitivity":"public"}}""")!;
        if (nested)
            item = new JsonObject { ["type"] = "array", ["items"] = item,
                ["x-configurator"] = new JsonObject { ["sensitivity"] = "public" } };
        if (objectRows)
        {
            var properties = new JsonObject();
            for (int index = 0; index < 25; index++)
                properties["value" + index] = item.DeepClone();
            item = new JsonObject
            {
                ["type"] = "object", ["additionalProperties"] = false, ["properties"] = properties,
                ["x-configurator"] = new JsonObject { ["sensitivity"] = "public" }
            };
        }
        var values = new JsonArray();
        for (int index = 0; index < (nested ? 25 : 51); index++)
        {
            if (objectRows)
            {
                var row = new JsonObject();
                for (int value = 0; value < 25; value++) row["value" + value] = value;
                values.Add(row);
            }
            else values.Add(nested ? new JsonArray(Enumerable.Range(0, 25).Select(value => (JsonNode?)JsonValue.Create(value)).ToArray()) : JsonValue.Create(index));
        }
        JsonNode initial = values;
        if (map)
        {
            var entries = new JsonObject();
            for (int index = 0; index < 51; index++) entries["key" + index] = index;
            initial = entries;
        }
        source["configuration"] = new JsonObject
        {
            ["type"] = "object", ["additionalProperties"] = false,
            ["x-configurator"] = new JsonObject { ["sensitivity"] = "public" },
            ["properties"] = new JsonObject
            {
                ["rows"] = new JsonObject
                {
                    ["type"] = map ? "object" : "array",
                    [map ? "additionalProperties" : "items"] = item,
                    ["x-configurator"] = new JsonObject
                    {
                        ["sensitivity"] = "public", ["detail"] = "basic", ["initialValue"] = initial
                    }
                }
            }
        };
        return new DefinitionFormModel(DefinitionSourceCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString())));
    }
}
