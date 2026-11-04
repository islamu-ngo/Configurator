namespace Configurator.Core.Tests.Dotenv;

using System.Text;
using Configurator.Core.Dotenv;

public sealed class EnvironmentDestinationDialectTests
{
    [Test]
    public async Task DockerAssignmentsPreserveLiteralQuotesWithoutAddingShellEscapes()
    {
        var document = new DotenvDocument([new("MESSAGE", "a\"b", DotenvEntryKind.LocalHumanValue,
            true, DotenvProvenance.UserInput)]);
        var rendered = DotenvCodec.Render(document, true, EnvironmentDestinationDialect.DockerRun);
        await Assert.That(rendered.Succeeded).IsTrue();
        await Assert.That(Encoding.UTF8.GetString(rendered.Bytes.Span)).IsEqualTo("MESSAGE=a\"b\n");
    }

    [Test]
    [Arguments(EnvironmentDestinationDialect.DockerRun, "MESSAGE=  مرحبا #$HOME; &|<>`'\"\\=  \n")]
    [Arguments(EnvironmentDestinationDialect.Compose, "MESSAGE=\"  مرحبا #$$HOME; &|<>`'\\\"\\\\=  \"\n")]
    public async Task DestinationBytesPreservePunctuationUnicodeAndWhitespace(
        EnvironmentDestinationDialect dialect, string expected)
    {
        const string literal = "  مرحبا #$HOME; &|<>`'\"\\=  ";
        var document = new DotenvDocument([new("MESSAGE", literal, DotenvEntryKind.LocalHumanValue,
            true, DotenvProvenance.UserInput)]);
        var rendered = DotenvCodec.Render(document, true, dialect);
        await Assert.That(rendered.Succeeded).IsTrue();
        await Assert.That(Encoding.UTF8.GetString(rendered.Bytes.Span)).IsEqualTo(expected);
        var parsed = DotenvCodec.Parse(Encoding.UTF8.GetBytes(expected), dialect);
        await Assert.That(parsed.Succeeded).IsTrue();
        await Assert.That(parsed.Document!.Entries.Single().Value).IsEqualTo(literal);
    }

    [Test]
    [Arguments(EnvironmentDestinationDialect.DockerRun, "_Empty=\n")]
    [Arguments(EnvironmentDestinationDialect.Compose, "_Empty=\"\"\n")]
    public async Task EmptyIsAnAssignmentAndBareHostInheritanceIsRejected(
        EnvironmentDestinationDialect dialect, string expected)
    {
        var document = new DotenvDocument([new("_Empty", "", DotenvEntryKind.LocalHumanValue,
            true, DotenvProvenance.UserInput)]);
        var rendered = DotenvCodec.Render(document, true, dialect);
        await Assert.That(rendered.Succeeded).IsTrue();
        await Assert.That(Encoding.UTF8.GetString(rendered.Bytes.Span)).IsEqualTo(expected);
        var parsed = DotenvCodec.Parse(Encoding.UTF8.GetBytes(expected), dialect);
        await Assert.That(parsed.Document!.Entries.Single().Value).IsEqualTo("");
        await Assert.That(parsed.Document.Entries.Single().Kind).IsEqualTo(DotenvEntryKind.LocalHumanValue);
        await Assert.That(DotenvCodec.Parse("HOST_VALUE\n"u8.ToArray(), dialect).Document).IsNull();
    }

    [Test]
    [Arguments(EnvironmentDestinationDialect.DockerRun)]
    [Arguments(EnvironmentDestinationDialect.Compose)]
    public async Task UnsupportedControlsAndCollisionsPublishNoPartialDocument(EnvironmentDestinationDialect dialect)
    {
        foreach (string literal in new[] { "one\ntwo", "one\rtwo", "one\0two", "one\ttwo", "\uD800" })
        {
            var document = new DotenvDocument([new("VALUE", literal, DotenvEntryKind.LocalHumanValue,
                true, DotenvProvenance.UserInput)]);
            var result = DotenvCodec.Render(document, true, dialect);
            await Assert.That(result.Succeeded).IsFalse();
            await Assert.That(result.Bytes.Length).IsEqualTo(0);
            await Assert.That(string.Join("", result.Diagnostics)).DoesNotContain(literal);
        }
        var parsed = DotenvCodec.Parse("Value=one\nVALUE=two\n"u8.ToArray(), dialect);
        await Assert.That(parsed.Document).IsNull();
        await Assert.That(parsed.Diagnostics.Any(item => item.Code == "dotenv-key-case-collision")).IsTrue();
    }

    [Test]
    public async Task ComposeImportRefusesUnresolvedInterpolationAndPreservesSingleQuotedLiterals()
    {
        foreach (string source in new[] { "KEY=$HOST\n", "KEY=\"${HOST}\"\n", "KEY=\"unclosed\n", "KEY=\"line\\nfeed\"\n" })
            await Assert.That(DotenvCodec.Parse(Encoding.UTF8.GetBytes(source), EnvironmentDestinationDialect.Compose)
                .Document).IsNull();
        var parsed = DotenvCodec.Parse("KEY='$HOST # literal'\n"u8.ToArray(), EnvironmentDestinationDialect.Compose);
        await Assert.That(parsed.Document!.Entries.Single().Value).IsEqualTo("$HOST # literal");
    }

    [Test]
    [Arguments(EnvironmentDestinationDialect.DockerRun)]
    [Arguments(EnvironmentDestinationDialect.Compose)]
    public async Task DestinationCodecRetainsBudgetsAndCancellation(EnvironmentDestinationDialect dialect)
    {
        await Assert.That(DotenvCodec.Parse(new byte[DotenvCodec.MaximumFileUtf8Bytes + 1], dialect).Document).IsNull();
        var document = new DotenvDocument([new("KEY", new string('a', DotenvCodec.MaximumValueUtf8Bytes + 1),
            DotenvEntryKind.LocalHumanValue, true, DotenvProvenance.UserInput)]);
        await Assert.That(DotenvCodec.Render(document, true, dialect).Succeeded).IsFalse();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.That(() => DotenvCodec.Parse("KEY=value\n"u8.ToArray(), dialect,
            cancellationToken: cancellation.Token)).Throws<OperationCanceledException>();
    }

    [Test]
    public async Task ComposeEscapingCountsTowardTheEncodedLineLimit()
    {
        foreach ((int count, bool accepted) in new[] { (8190, true), (8191, false) })
        {
            var document = new DotenvDocument([new("K", new string('\\', count),
                DotenvEntryKind.LocalHumanValue, true, DotenvProvenance.UserInput)]);
            var rendered = DotenvCodec.Render(document, true, EnvironmentDestinationDialect.Compose);
            await Assert.That(rendered.Succeeded).IsEqualTo(accepted);
            if (accepted)
                await Assert.That(DotenvCodec.Parse(rendered.Bytes, EnvironmentDestinationDialect.Compose).Succeeded).IsTrue();
            else await Assert.That(rendered.Bytes.Length).IsEqualTo(0);
        }
    }

    [Test]
    public async Task DockerFileLimitAcceptsTheBoundaryAndRejectsAnExtraByteWithoutPartialOutput()
    {
        var entries = Enumerable.Range(0, 127).Select(index => new DotenvEntry($"K{index:D3}",
            new string('a', 8192), DotenvEntryKind.LocalHumanValue, true, DotenvProvenance.UserInput)).ToList();
        entries.Add(new("K127", new string('b', 7424), DotenvEntryKind.LocalHumanValue,
            true, DotenvProvenance.UserInput));
        var exact = DotenvCodec.Render(new(entries), true, EnvironmentDestinationDialect.DockerRun);
        await Assert.That(exact.Succeeded).IsTrue();
        await Assert.That(exact.Bytes.Length).IsEqualTo(1_048_576);
        entries[^1] = new("K127", new string('b', 7425), DotenvEntryKind.LocalHumanValue,
            true, DotenvProvenance.UserInput);
        var tooLarge = DotenvCodec.Render(new(entries), true, EnvironmentDestinationDialect.DockerRun);
        await Assert.That(tooLarge.Succeeded).IsFalse();
        await Assert.That(tooLarge.Bytes.Length).IsEqualTo(0);
    }
}
