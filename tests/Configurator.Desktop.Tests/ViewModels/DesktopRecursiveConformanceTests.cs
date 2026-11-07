namespace Configurator.Desktop.Tests.ViewModels;

using Configurator.Core.Definitions;
using Configurator.Artifacts;
using Configurator.Desktop.ViewModels;
using Configurator.Desktop.ViewModels.Fields;
using Configurator.Presentation;
using System.Text;
using System.Text.Json;

public sealed class DesktopRecursiveConformanceTests
{
    [Test]
    [Arguments("Conformance.RootMap")]
    [Arguments("Conformance.RootVariant")]
    public async Task RootControlsExistBeforeAnswersAreCollected(string resource)
    {
        using Stream source = typeof(DesktopRecursiveConformanceTests).Assembly
            .GetManifestResourceStream(resource)!;
        using var bytes = new MemoryStream();
        source.CopyTo(bytes);
        using var form = new DefinitionFormModel(DefinitionSourceCodec.ParseJson(bytes.ToArray()));
        using var factory = new FormFieldFactory();
        IReadOnlyList<FieldViewModel> controls = factory.Create(form);
        await Assert.That(controls.Count).IsEqualTo(1);
        await Assert.That(((RootFieldViewModel)controls.Single()).Editor.ClearCommand.CanExecute(null)).IsFalse();
        await Assert.That(form.InputRevision).IsEqualTo(0);
        await Assert.That(form.Snapshot().Values.Count).IsEqualTo(0);
    }

    [Test]
    [Arguments("literal-map-keys-and-null")]
    [Arguments("empty-map-requires-entry")]
    [Arguments("map-rejects-unlisted-integer")]
    [Arguments("map-rejects-string-instead-of-integer")]
    [Arguments("map-retains-ordinal-case-distinction")]
    [Arguments("variant-applies-initialization-without-suggestion")]
    [Arguments("variant-omission-suppresses-initialization")]
    [Arguments("variant-null-is-present")]
    [Arguments("variant-missing-secret-is-draft-only")]
    [Arguments("variant-supplied-synthetic-secret-is-complete")]
    [Arguments("variant-missing-ordinary-input-is-not-a-draft")]
    [Arguments("variant-rejects-unselected-branch-payload")]
    [Arguments("library-item-scope-excludes-inactive-invalid-value")]
    [Arguments("library-two-active-branches-retain-distinct-periods")]
    [Arguments("library-inactive-required-period-is-absent")]
    [Arguments("library-active-period-is-required")]
    [Arguments("library-active-period-rejects-string")]
    [Arguments("library-inactive-first-row-does-not-shift-item-scope")]
    [Arguments("library-active-period-rejects-unlisted-integer")]
    public async Task NativeProjectionAndPublicationMatchLiteralCorpus(string identity)
    {
        using Stream casesStream = Resource("Conformance.Cases");
        using JsonDocument cases = JsonDocument.Parse(casesStream);
        JsonElement item = cases.RootElement.GetProperty("cases").EnumerateArray()
            .Single(candidate => candidate.GetProperty("id").GetString() == identity);
        using Stream definitionStream = Resource(item.GetProperty("definition").GetString() switch
        {
            "root-map" => "Conformance.RootMap",
            "root-variant" => "Conformance.RootVariant",
            "synthetic-library" => "Conformance.Library",
            _ => throw new InvalidDataException("Unknown conformance definition.")
        });
        using var bytes = new MemoryStream();
        definitionStream.CopyTo(bytes);
        Definition definition = DefinitionSourceCodec.ParseJson(bytes.ToArray());
        byte[]? published = null;
        using var shell = DesktopTestContext.Root((_, output, _) =>
        {
            published = output.ToArray();
            return Task.FromResult(ProtectedArtifactCommitResult.Installed(true));
        });
        await Assert.That(shell.Loader.LoadDefinition(definition.CanonicalBytes)).IsTrue();
        DefinitionFormModel form = shell.Workspace.Form!;
        await Assert.That(form.SetValue(DefinitionPath.FromSegments([]), item.GetProperty("values"))).IsTrue();
        foreach (JsonElement omitted in item.GetProperty("omitted").EnumerateArray())
            await Assert.That(form.SetValue(DefinitionPath.Parse(omitted.GetString()!), null)).IsTrue();
        FieldViewModel field = shell.Workspace.Fields.Single();
        if (item.GetProperty("definition").GetString() == "synthetic-library")
        {
            long revision = form.InputRevision;
            shell.Workspace.SelectedLocale = "ar";
            await Assert.That(shell.Workspace.FlowDirection).IsEqualTo(Avalonia.Media.FlowDirection.RightToLeft);
            await Assert.That(form.InputRevision).IsEqualTo(revision);
            await Assert.That(shell.Workspace.Fields.Single().Title).IsEqualTo("الفروع");
            var branches = (ListFieldViewModel)shell.Workspace.Fields.Single();
            var firstBranch = (ObjectFieldViewModel)branches.Items[0];
            await Assert.That(firstBranch.Children.Single(child => child is EnumFieldViewModel).Title)
                .IsEqualTo("مدة الإعارة");
        }
        bool ready = item.GetProperty("ready").GetBoolean();
        if (field is RootFieldViewModel root)
            await Assert.That(root.Editor.IsVisible).IsTrue();
        await Assert.That(field.HasError).IsEqualTo(!ready).Because(identity);
        shell.Publisher.ArtifactId = "config";
        DesktopPreparationOutcome outcome = await shell.Publisher.PrepareAsync();
        await Assert.That(outcome).IsEqualTo(ready
            ? DesktopPreparationOutcome.Prepared : DesktopPreparationOutcome.Invalid).Because(identity);
        if (ready)
        {
            ProtectedArtifactCommitResult result = await shell.Publisher.SaveAsync();
            await Assert.That(result.VerificationSucceeded).IsTrue();
            await Assert.That(published is not null &&
                published.AsSpan().SequenceEqual(Encoding.UTF8.GetBytes(item.GetProperty("expectedJson").GetString()!)))
                .IsTrue().Because(identity);
        }
        else
        {
            await Assert.That(shell.Publisher.CanSave).IsFalse();
            await Assert.That(published).IsNull();
        }
    }

    private static Stream Resource(string name) =>
        typeof(DesktopRecursiveConformanceTests).Assembly.GetManifestResourceStream(name)
        ?? throw new InvalidDataException("Missing conformance resource.");
}
