namespace Configurator.Desktop.Tests;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Configurator.Core.Definitions;
using Configurator.Desktop.Views;

public sealed class DesktopDiscoveryTests
{
    public static AppBuilder BuildAvaloniaApp() => DesktopApplicationTests.BuildAvaloniaApp();

    [Test]
    public async Task DiscoveryExcludesAnswersAndPrivateKeysAndPreservesPreparation()
    {
        using var shell = DesktopTestContext.Root();
        shell.Loader.LoadDefinition(ViewModels.DefinitionEditingTests.Definition().CanonicalBytes);
        var form = shell.Workspace.Form!;
        shell.Workspace.Fields.Single(field => field.Id == "text").Text = "synthetic-answer-custody-9d02";
        form.SetValue(DefinitionPath.Parse("/named"), JsonSerializer.SerializeToElement(
            new Dictionary<string, int> { ["synthetic-private-key-9d02"] = 4 }));
        await shell.Publisher.PrepareSelectionAsync();
        var preparation = shell.Publisher.Preparation;
        long revision = form.InputRevision;
        foreach (string query in new[] { "synthetic-answer-custody-9d02", "synthetic-private-key-9d02" })
        {
            shell.Navigation.SearchText = query;
            await Assert.That(shell.Navigation.DiscoveryTotal).IsEqualTo(0);
            await Assert.That(shell.Navigation.DiscoveryResults.Count).IsEqualTo(0);
        }
        await Assert.That(form.InputRevision).IsEqualTo(revision);
        await Assert.That(preparation).IsNotNull();
        await Assert.That(shell.Publisher.Preparation).IsSameReferenceAs(preparation);
        await Assert.That(form.Snapshot().Values["named"].GetProperty("synthetic-private-key-9d02").GetInt32()).IsEqualTo(4);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task QueryLimitsGiveSafeFeedbackWithoutChangingAcceptedDiscovery(bool terms)
    {
        using var shell = DesktopTestContext.Root();
        shell.Loader.LoadDefinition(ViewModels.DefinitionEditingTests.Definition().CanonicalBytes);
        shell.Navigation.SearchText = "text";
        long revision = shell.Workspace.Form!.InputRevision;
        shell.Navigation.SearchText = terms ? string.Join(" ", Enumerable.Repeat("term", 17)) : new string('x', 257);
        await Assert.That(shell.Navigation.DiscoveryGuidance).IsNotEmpty();
        await Assert.That(shell.Navigation.DiscoveryResults.Count).IsEqualTo(0);
        await Assert.That(shell.Workspace.Form.DiscoveryQuery).IsEqualTo("text");
        await Assert.That(shell.Workspace.Form.InputRevision).IsEqualTo(revision);
    }

    [Test]
    public async Task InactiveResultsExplainApplicabilityWithoutChangingControllers()
    {
        using var shell = DesktopTestContext.Root();
        shell.Loader.LoadDefinition(DesktopApplicationTests.Definition());
        shell.Navigation.IncludeInactive = true;
        shell.Navigation.SearchText = "Port";
        var result = shell.Navigation.DiscoveryResults.Single(match => match.Entry.FieldId == "port");
        long revision = shell.Workspace.Form!.InputRevision;
        shell.Navigation.SelectedDiscoveryResult = result;
        await Assert.That(result.Entry.IsActive).IsFalse();
        await Assert.That(shell.Navigation.DiscoveryGuidance).IsNotEmpty();
        await Assert.That(shell.Workspace.Form.InputRevision).IsEqualTo(revision);
        await Assert.That(shell.Workspace.Form.Fields.Single(field => field.Definition.Id == "mode").Text).IsEqualTo("local");
        await Assert.That(shell.Workspace.Form.Snapshot().Values.ContainsKey("mode")).IsFalse();
    }

    [Test]
    public async Task DiscoveryPagesPublicMetadataAndDoesNotSupplyAbsentContainers()
    {
        JsonObject source = JsonNode.Parse("""
            {"$schema":"urn:islamu:configurator:authoring:v0.0.1",
             "identity":{"familyId":"06d0cbe3-673e-4a34-8520-e9452d293ee2","revision":1},
             "project":{"id":"discovery-pages","name":"Discovery pages"},"applicationVersions":["1"],
             "configuration":{"type":"object","additionalProperties":false,
               "x-configurator":{"sensitivity":"public"}},
             "outputs":{"config.json":{"format":"json"}}}
            """)!.AsObject();
        var properties = new JsonObject();
        for (int index = 0; index < 31; index++)
            properties["setting" + index] = new JsonObject
            {
                ["type"] = "string", ["title"] = "Searchable setting " + index,
                ["x-configurator"] = new JsonObject { ["sensitivity"] = "public", ["detail"] = "basic" }
            };
        source["configuration"]!["properties"] = properties;
        using var shell = DesktopTestContext.Root();
        shell.Loader.LoadDefinition(Encoding.UTF8.GetBytes(source.ToJsonString()));
        shell.Navigation.SearchText = "Searchable";
        await Assert.That(shell.Navigation.DiscoveryTotal).IsEqualTo(31);
        await Assert.That(shell.Navigation.DiscoveryResults.Count).IsEqualTo(25);
        shell.Navigation.NextDiscoveryPageCommand.Execute(null);
        await Assert.That(shell.Navigation.DiscoveryResults.Count).IsEqualTo(6);
        var retired = shell.Navigation.DiscoveryResults[0];
        shell.Loader.LoadDefinition(ViewModels.DefinitionEditingTests.Definition().CanonicalBytes);
        shell.Navigation.SelectedDiscoveryResult = retired;
        await Assert.That(shell.Navigation.SelectedDiscoveryResult).IsNull();
        shell.Navigation.SearchText = "named";
        shell.Navigation.SelectedDiscoveryResult = shell.Navigation.DiscoveryResults
            .Single(match => match.Entry.FieldId == "named" && match.Entry.RelativePath.Count == 0);
        await Assert.That(shell.Workspace.Form!.InputRevision).IsEqualTo(0);
        await Assert.That(shell.Workspace.Form.Snapshot().Values.ContainsKey("named")).IsFalse();
    }

    [Test]
    [NotInParallel]
    public async Task ConfigurationExposesMetadataSearchWithoutChangingAnswers()
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(DesktopDiscoveryTests));
        try
        {
            var result = await session.Dispatch(() =>
            {
                using var shell = DesktopTestContext.Root();
                var window = new MainWindow { DataContext = shell };
                try
                {
                    window.Show();
                    shell.Loader.LoadDefinition(ViewModels.DefinitionEditingTests.Definition().CanonicalBytes);
                    long revision = shell.Workspace.Form!.InputRevision;
                    window.UpdateLayout();
                    Dispatcher.UIThread.RunJobs();
                    var search = window.FindNamedControl<TextBox>("ConfigurationSearch");
                    return (present: search is not null, revision: shell.Workspace.Form.InputRevision == revision);
                }
                finally { window.Close(); }
            }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));
            await Assert.That(result.present).IsTrue();
            await Assert.That(result.revision).IsTrue();
        }
        finally { await Task.Run(session.Dispose); }
    }
}
