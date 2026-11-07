namespace Configurator.Terminal.Tests;

using System.Text;
using System.Text.Json;
using Configurator.Artifacts;
using Configurator.Core.Definitions;
using Configurator.Presentation;
using Configurator.Terminal;

public sealed class TerminalRecursiveConformanceTests
{
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
    public async Task NativeAdapterPreparesAndPublishesExactCompleteCorpusOrRefuses(string identity)
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
        using var form = new DefinitionFormModel(definition);
        // Fixture setup retains malformed imported values too. The adapter must not silently drop them.
        await Assert.That(form.SetValue(DefinitionPath.FromSegments([]), item.GetProperty("values"))).IsTrue();
        foreach (JsonElement omission in item.GetProperty("omitted").EnumerateArray())
            await Assert.That(form.SetValue(DefinitionPath.Parse(omission.GetString()!), null)).IsTrue();
        var editor = new TerminalFieldEditor(form);
        if (item.GetProperty("definition").GetString() == "synthetic-library")
        {
            await Assert.That(editor.Slots[0].Location.ToPointer()).IsEqualTo("/branches");
            await Assert.That(editor.Slots[0].Title).IsEqualTo("Branches");
            long revision = form.InputRevision;
            await Assert.That(form.SelectLocale("ar")).IsTrue();
            await Assert.That(editor.Slots[0].Title).IsEqualTo("الفروع");
            await Assert.That(form.InputRevision).IsEqualTo(revision);
        }
        else
            await Assert.That(editor.Slots[0].Location.Segments).IsEmpty();
        using JsonDocument expectedDisplay = JsonDocument.Parse(
            item.GetProperty("definition").GetString() == "synthetic-library"
                ? item.GetProperty("values").GetRawText()
                : item.GetProperty("expectedJson").GetString() ?? item.GetProperty("values").GetRawText());
        foreach (JsonProperty property in expectedDisplay.RootElement.EnumerateObject())
        {
            var slot = editor.Slots.SingleOrDefault(candidate => candidate.Location.Segments.SequenceEqual([property.Name]));
            if (slot is null && !item.GetProperty("ready").GetBoolean()) continue;
            await Assert.That(slot).IsNotNull().Because(identity);
            string expectedText = slot!.IsContainer ? "" : property.Value.ValueKind switch
            {
                JsonValueKind.Null => "",
                JsonValueKind.String => property.Value.GetString()!,
                _ => property.Value.GetRawText()
            };
            await Assert.That(TerminalFieldEditor.Read(slot!).Equals(expectedText, StringComparison.Ordinal)).IsTrue();
            if (property.Name == "branches")
                await Assert.That(TerminalFieldEditor.ItemCount(slot!)).IsEqualTo(property.Value.GetArrayLength());
        }
        string directory = Path.Combine(Path.GetTempPath(), "configurator-terminal-corpus-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            using var operation = new TerminalArtifactOperation(form, new ProtectedArtifactWriter(), directory);
            TerminalPreparationOutcome prepared = await operation.PrepareAsync();
            bool ready = item.GetProperty("ready").GetBoolean();
            await Assert.That(prepared.Code).IsEqualTo(ready ? TerminalPreparationCode.Ready : TerminalPreparationCode.Invalid)
                .Because(identity);
            await Assert.That(operation.AdoptPreparation(prepared))
                .IsEqualTo(ready ? TerminalPreparationCode.Ready : TerminalPreparationCode.Invalid);
            var outcome = await operation.ExecuteAsync(CancellationToken.None);
            var result = (TerminalArtifactResult)outcome.CoreResult;
            bool published = ready && OperatingSystem.IsLinux();
            await Assert.That(result.Succeeded).IsEqualTo(published).Because(identity);
            string output = Path.Combine(directory, definition.Artifacts.Single().FileName);
            await Assert.That(File.Exists(output)).IsEqualTo(published).Because(identity);
            if (published)
            {
                string expected = item.GetProperty("expectedJson").GetString()!;
                await Assert.That((await File.ReadAllBytesAsync(output)).SequenceEqual(Encoding.UTF8.GetBytes(expected)))
                    .IsTrue().Because(identity);
                if (OperatingSystem.IsLinux())
                    await Assert.That(File.GetUnixFileMode(output)).IsEqualTo(UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
            else
                await Assert.That(Directory.GetFiles(directory)).IsEmpty();
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static Stream Resource(string name) =>
        typeof(TerminalRecursiveConformanceTests).Assembly.GetManifestResourceStream(name)
        ?? throw new InvalidDataException("Missing conformance resource.");
}
