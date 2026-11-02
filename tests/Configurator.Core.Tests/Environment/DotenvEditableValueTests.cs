using Configurator.Core.Dotenv;
namespace Configurator.Core.Tests.Environment;

using System.Text;
using Configurator.Core.Environment;

public sealed class DotenvEditableValueTests
{
    [Test]
    public async Task EditableEmptyAssignmentsAreValuesRatherThanTemplatePlaceholders()
    {
        var parsed = DotenvCodec.Parse("ONE=\nTWO=\"\"\n"u8.ToArray(), DotenvPurpose.EditableValues);
        await Assert.That(parsed.Succeeded).IsTrue();
        foreach (DotenvEntry entry in parsed.Document!.Entries)
        {
            await Assert.That(entry.Value).IsEqualTo("");
            await Assert.That(entry.Kind).IsEqualTo(DotenvEntryKind.LocalHumanValue);
        }
    }

    [Test]
    public async Task EditableInputNormalizesOneLeadingBomAndCrLf()
    {
        var parsed = DotenvCodec.Parse(Encoding.UTF8.GetBytes("\uFEFF# comment\r\nNAME=\"a#=b\"\r\n"),
            DotenvPurpose.EditableValues);
        await Assert.That(parsed.Succeeded).IsTrue();
        await Assert.That(parsed.Document!.Entries.Single().Value).IsEqualTo("a#=b");
    }

    [Test]
    public async Task TemplatePurposeRetainsEmptyPlaceholdersAndStrictLineEndings()
    {
        var parsed = DotenvCodec.Parse("ONE=\nTWO=\"\"\n"u8.ToArray());
        await Assert.That(parsed.Succeeded).IsTrue();
        foreach (DotenvEntry entry in parsed.Document!.Entries)
        {
            await Assert.That(entry.Value).IsNull();
            await Assert.That(entry.Kind).IsEqualTo(DotenvEntryKind.EmptyPlaceholder);
        }
        await Assert.That(DotenvCodec.Parse("NAME=value\r\n"u8.ToArray()).Succeeded).IsFalse();
        await Assert.That(DotenvCodec.Parse(Encoding.UTF8.GetBytes("\uFEFFNAME=value\n")).Succeeded).IsFalse();
    }

    [Test]
    [Arguments("NAME=a\nNAME=b\n", "dotenv-duplicate-key")]
    [Arguments("NAME=a\nname=b\n", "dotenv-key-case-collision")]
    [Arguments("=value\n", "dotenv-key-invalid")]
    [Arguments("NAME='value'\n", "dotenv-quote-invalid")]
    [Arguments("NAME=value #comment\n", "dotenv-trailing-syntax")]
    [Arguments("NAME=\"$HOME\"\n", "dotenv-expansion-forbidden")]
    [Arguments("NAME=`command`\n", "dotenv-expansion-forbidden")]
    [Arguments("NAME=\"one\ntwo\"\n", "dotenv-multiline-forbidden")]
    [Arguments("NAME=one\rtwo\n", "dotenv-carriage-return-forbidden")]
    [Arguments("\uFEFF\uFEFFNAME=value\n", "dotenv-key-invalid")]
    public async Task UnsupportedSyntaxRejectsWithoutPublishingPartialEntries(string source, string code)
    {
        var parsed = DotenvCodec.Parse(Encoding.UTF8.GetBytes(source), DotenvPurpose.EditableValues);
        await Assert.That(parsed.Succeeded).IsFalse();
        await Assert.That(parsed.Document).IsNull();
        await Assert.That(parsed.Diagnostics.Any(item => item.Code == code)).IsTrue();
        await Assert.That(parsed.ToString()).DoesNotContain(source);
    }

    [Test]
    public async Task SourceCoordinatesSurviveNormalizationAndEditableRenderingUsesLfWithoutBom()
    {
        var parsed = DotenvCodec.Parse(Encoding.UTF8.GetBytes("\uFEFF# comment\r\n\r\nFIRST=\r\nSECOND=\"a\\\"b\\\\c#=\"\r\n"),
            DotenvPurpose.EditableValues);
        await Assert.That(parsed.Succeeded).IsTrue();
        await Assert.That(parsed.EntryLocations.SequenceEqual([
            new DotenvSourceLocation(3, 7), new DotenvSourceLocation(4, 8)])).IsTrue();
        var rendered = DotenvCodec.Render(parsed.Document!, true, DotenvPurpose.EditableValues);
        await Assert.That(rendered.Succeeded).IsTrue();
        await Assert.That(Encoding.UTF8.GetString(rendered.Bytes.Span))
            .IsEqualTo("FIRST=\nSECOND=\"a\\\"b\\\\c#=\"\n");
        var templateRender = DotenvCodec.Render(parsed.Document!, true);
        await Assert.That(templateRender.Succeeded).IsFalse();
        var rejected = DotenvCodec.Parse(Encoding.UTF8.GetBytes("\uFEFF# comment\r\n\r\nNAME=$VALUE\r\n"),
            DotenvPurpose.EditableValues);
        await Assert.That(rejected.DiagnosticLocations.Single()).IsEqualTo(new DotenvSourceLocation(3, 1));
        await Assert.That(string.Join("", rejected.Diagnostics)).DoesNotContain("$VALUE");
    }

    [Test]
    public async Task DiagnosticRetentionIsBoundedAndReportsTruncationWithoutPartialDocument()
    {
        var parsed = DotenvCodec.Parse(Encoding.UTF8.GetBytes(string.Concat(
            Enumerable.Repeat("invalid-private-sentinel\n", 150))), DotenvPurpose.EditableValues);
        await Assert.That(parsed.Document).IsNull();
        await Assert.That(parsed.Diagnostics.Count).IsEqualTo(100);
        await Assert.That(parsed.DiagnosticLocations.Count).IsEqualTo(100);
        await Assert.That(parsed.DiagnosticsTruncated).IsTrue();
        await Assert.That(string.Join("", parsed.Diagnostics)).DoesNotContain("private-sentinel");
        var exact = DotenvCodec.Parse(Encoding.UTF8.GetBytes(string.Concat(
            Enumerable.Repeat("invalid\n", 100))), DotenvPurpose.EditableValues);
        await Assert.That(exact.DiagnosticsTruncated).IsFalse();
    }

    [Test]
    public async Task EditablePurposeKeepsAllExistingInputBudgets()
    {
        foreach ((byte[] input, string code) in new[]
        {
            (new byte[DotenvCodec.MaximumFileUtf8Bytes + 1], "dotenv-file-too-large"),
            (Encoding.UTF8.GetBytes("KEY=" + new string('a', DotenvCodec.MaximumLineUtf8Bytes)), "dotenv-line-too-large"),
            (Encoding.UTF8.GetBytes(new string('A', DotenvCodec.MaximumKeyCharacters + 1) + "="), "dotenv-key-invalid"),
            (Encoding.UTF8.GetBytes("KEY=" + new string('a', DotenvCodec.MaximumValueUtf8Bytes + 1)), "dotenv-value-too-large"),
            (Encoding.UTF8.GetBytes(string.Concat(Enumerable.Range(0, DotenvCodec.MaximumEntryCount + 1)
                .Select(index => $"KEY_{index}=\n"))), "dotenv-count-exceeded"),
            (new byte[] { 0x4B, 0x3D, 0xFF }, "dotenv-utf8-invalid")
        })
        {
            var parsed = DotenvCodec.Parse(input, DotenvPurpose.EditableValues);
            await Assert.That(parsed.Succeeded).IsFalse();
            await Assert.That(parsed.Document).IsNull();
            await Assert.That(parsed.Diagnostics.Any(item => item.Code == code)).IsTrue();
        }
        var bounded = DotenvCodec.Parse(Encoding.UTF8.GetBytes("KEY=" + new string('a', DotenvCodec.MaximumValueUtf8Bytes)),
            DotenvPurpose.EditableValues);
        await Assert.That(bounded.Succeeded).IsTrue();
    }

    [Test]
    public async Task CanceledAcquisitionNeverReturnsAnEditableDocument()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.That(() => DotenvCodec.Parse("KEY=value\n"u8.ToArray(), DotenvPurpose.EditableValues,
            cancellation.Token)).Throws<OperationCanceledException>();
    }
}
