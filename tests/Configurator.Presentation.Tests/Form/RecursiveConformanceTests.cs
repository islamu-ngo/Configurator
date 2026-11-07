namespace Configurator.Presentation.Tests;

using System.Text;
using System.Text.Json;
using Configurator.Core.Artifacts;
using Configurator.Core.Definitions;

public sealed class RecursiveConformanceTests
{
    [Test]
    public async Task PublicRootDocumentsAdoptOnceWithLiteralValuesAndOmissions()
    {
        using Stream casesStream = Resource("Conformance.Cases");
        using JsonDocument cases = JsonDocument.Parse(casesStream);
        foreach (JsonElement item in cases.RootElement.GetProperty("cases").EnumerateArray())
        {
            JsonElement values = item.GetProperty("values");
            // This scenario exercises admissible public documents. Invalid value
            // and secret-bearing imports have their own refusal/custody owners.
            if (item.GetProperty("expectedDraftJson").ValueKind == JsonValueKind.Null ||
                values.TryGetProperty("token", out _)) continue;
            string identity = item.GetProperty("id").GetString()!;
            using Stream definitionStream = Resource(item.GetProperty("definition").GetString() switch
            {
                "root-map" => "Conformance.RootMap",
                "root-variant" => "Conformance.RootVariant",
                "synthetic-library" => "Conformance.Library",
                _ => throw new InvalidDataException("Unknown conformance definition.")
            });
            using var bytes = new MemoryStream();
            definitionStream.CopyTo(bytes);
            using var form = new DefinitionFormModel(DefinitionSourceCodec.ParseJson(bytes.ToArray()));
            using var session = form.OpenDocument();
            byte[] envelope = Encoding.UTF8.GetBytes(
                "{\"answerContractVersion\":\"0.0.1\",\"values\":" + values.GetRawText() +
                ",\"omitted\":" + item.GetProperty("omitted").GetRawText() + "}");
            using var parsed = session.Parse(envelope);
            if (item.TryGetProperty("expectedDocumentCode", out JsonElement documentCode))
            {
                await Assert.That(parsed.Succeeded).IsFalse().Because(identity);
                await Assert.That(parsed.Diagnostics.Select(diagnostic => diagnostic.Code).ToArray())
                    .IsEquivalentTo(new[] { documentCode.GetString()! }).Because(identity);
                await Assert.That(form.InputRevision).IsEqualTo(0);
                await Assert.That(form.Snapshot().Values).IsEmpty();
                continue;
            }
            await Assert.That(parsed.Succeeded).IsTrue().Because(identity);
            long revision = form.InputRevision;
            int changes = 0;
            form.InputChanged += (_, _) => changes++;
            await Assert.That(session.Apply(parsed.Candidate!).Succeeded).IsTrue().Because(identity);
            await Assert.That(form.InputRevision).IsEqualTo(revision + 1).Because(identity);
            await Assert.That(changes).IsEqualTo(1).Because(identity);
            AnswerSnapshot snapshot = form.Snapshot();
            foreach (JsonProperty property in values.EnumerateObject())
            {
                await Assert.That(JsonElement.DeepEquals(snapshot.Values[property.Name], property.Value))
                    .IsTrue().Because(identity);
                await Assert.That(form.AnswerOrigin([property.Name]))
                    .IsEqualTo(DefinitionAnswerOrigin.Explicit).Because(identity);
            }
            await Assert.That(snapshot.Omitted.Select(path => path.ToPointer()).Order(StringComparer.Ordinal).ToArray())
                .IsEquivalentTo(item.GetProperty("omitted").EnumerateArray().Select(path => path.GetString()!).ToArray())
                .Because(identity);
            using DefinitionDraftArtifactResult draft = DefinitionDraftArtifactComposer.Compose(form.Definition, "config", snapshot);
            await Assert.That(draft.Succeeded).IsTrue().Because(identity);
            await Assert.That(Encoding.UTF8.GetString(draft.Artifact!.Bytes.Span))
                .IsEqualTo(item.GetProperty("expectedDraftJson").GetString()).Because(identity);
            await Assert.That(session.Apply(parsed.Candidate!).Succeeded).IsFalse().Because(identity);
            if (item.GetProperty("definition").GetString() == "synthetic-library")
            {
                await Assert.That(form.LocalizedText(DefinitionPath.Parse("/configuration/title"), "Library branches"))
                    .IsEqualTo("Library branches");
                long beforeLocale = form.InputRevision;
                await Assert.That(form.SelectLocale("ar")).IsTrue();
                await Assert.That(form.Direction).IsEqualTo("rtl");
                await Assert.That(form.LocalizedText(DefinitionPath.Parse("/configuration/title"), "Library branches"))
                    .IsEqualTo("فروع المكتبة");
                await Assert.That(form.LocalizedText(DefinitionPath.Parse(
                    "/configuration/properties/branches/items/properties/loanDays/title"), "Loan period"))
                    .IsEqualTo("مدة الإعارة");
                await Assert.That(form.InputRevision).IsEqualTo(beforeLocale);
                await Assert.That(changes).IsEqualTo(1);
                using DefinitionDraftArtifactResult localized = DefinitionDraftArtifactComposer.Compose(form.Definition, "config", form.Snapshot());
                await Assert.That(Encoding.UTF8.GetString(localized.Artifact!.Bytes.Span))
                    .IsEqualTo(item.GetProperty("expectedDraftJson").GetString());
                await Assert.That(form.SelectLocale("en")).IsTrue();
                await Assert.That(form.Direction).IsEqualTo("ltr");
                await Assert.That(form.InputRevision).IsEqualTo(beforeLocale);
            }
        }
    }

    private static Stream Resource(string name) =>
        typeof(RecursiveConformanceTests).Assembly.GetManifestResourceStream(name)
        ?? throw new InvalidDataException("Missing conformance resource.");
}
