namespace Configurator.Core.Tests.Definitions;

using System.Text;
using System.Text.Json;
using Configurator.Core.Definitions;
using Configurator.Core.Definitions.Authoring;

public sealed class DefinitionAuthoringDiagnosticTests
{
    private static readonly byte[] Utf8Bom = [0xEF, 0xBB, 0xBF];

    [Test]
    public async Task MalformedJsonWithBomCrlfArabicAndSupplementaryUnicodeReportsExactCoordinates()
    {
        const string line1 = "{";
        const string line2Prefix = "  \"العربية\U0001F600\": ";
        const string text = line1 + "\r\n" + line2Prefix + "@SYNTH_CANARY_JSON_TOKEN\r\n}";
        byte[] rawBytes = CombineBomAndUtf8(text);

        // Independent coordinate specification:
        // BOM = 3 bytes; Line 1 = "{\r\n" (3 bytes); Line 2 prefix = 2 spaces + '"' + 7 Arabic (14B) + U+1F600 (4B) + '"' + ':' + ' '
        int expectedLine = 2;
        int expectedColumn = CountUnicodeScalars(line2Prefix) + 1; // 1-based Unicode scalar column (15, NOT UTF-16 16 or UTF-8 byte 25)
        long expectedByteOffset = Utf8Bom.Length + Encoding.UTF8.GetByteCount(line1 + "\r\n" + line2Prefix);

        DefinitionException? failure = null;
        try { _ = DefinitionAuthoringCodec.ParseJson(rawBytes); }
        catch (DefinitionException exception) { failure = exception; }

        await Assert.That(failure).IsNotNull();
        DefinitionAuthoringDiagnostic diagnostic = failure!.ToAuthoringDiagnostic();
        await Assert.That(diagnostic.Code).IsEqualTo("document-invalid");
        await Assert.That(diagnostic.Line).IsEqualTo(expectedLine);
        await Assert.That(diagnostic.Column).IsEqualTo(expectedColumn);
        await Assert.That(diagnostic.ByteOffset).IsEqualTo(expectedByteOffset);
        await Assert.That(failure.Line).IsEqualTo(expectedLine);
        await Assert.That(failure.Column).IsEqualTo(expectedColumn);
        await Assert.That(failure.ByteOffset).IsEqualTo(expectedByteOffset);
        await AssertDoesNotLeak(failure, diagnostic, "SYNTH_CANARY_JSON_TOKEN", "العربية");
    }

    [Test]
    public async Task MalformedYamlWithBomCrlfArabicAndSupplementaryUnicodeReportsExactCoordinates()
    {
        const string line1 = "project: \"العربية\U0001F600\"";
        const string line2Prefix = "broken: \"العربية\U0001F600\" ";
        const string text = line1 + "\r\n" + line2Prefix + "SYNTH_CANARY_YAML_TAIL\r\n";
        byte[] rawBytes = CombineBomAndUtf8(text);

        int expectedLine = 2;
        int expectedColumn = CountUnicodeScalars(line2Prefix) + 1;
        long expectedByteOffset = Utf8Bom.Length + Encoding.UTF8.GetByteCount(line1 + "\r\n" + line2Prefix);

        DefinitionException? failure = null;
        try { _ = DefinitionAuthoringCodec.ParseYaml(rawBytes); }
        catch (DefinitionException exception) { failure = exception; }

        await Assert.That(failure).IsNotNull();
        DefinitionAuthoringDiagnostic diagnostic = failure!.ToAuthoringDiagnostic();
        await Assert.That(diagnostic.Code).IsEqualTo("document-invalid");
        await Assert.That(diagnostic.Line).IsEqualTo(expectedLine);
        await Assert.That(diagnostic.Column).IsEqualTo(expectedColumn);
        await Assert.That(diagnostic.ByteOffset).IsEqualTo(expectedByteOffset);
        await AssertDoesNotLeak(failure, diagnostic, "SYNTH_CANARY_YAML_TAIL", "العربية");
    }

    [Test]
    public async Task YamlAnchorWithBomCrlfAndUnicodeReportsExactCoordinatesWithoutLeakingAnchorCanary()
    {
        const string line1 = "project: \"العربية\U0001F600\"";
        const string line2Prefix = "secret: ";
        const string text = line1 + "\r\n" + line2Prefix + "&SYNTH_CANARY_ANCHOR value\r\n";
        byte[] rawBytes = CombineBomAndUtf8(text);

        int expectedLine = 2;
        int expectedColumn = CountUnicodeScalars(line2Prefix) + 1;
        long expectedByteOffset = Utf8Bom.Length + Encoding.UTF8.GetByteCount(line1 + "\r\n" + line2Prefix);

        DefinitionException? failure = null;
        try { _ = DefinitionAuthoringCodec.ParseYaml(rawBytes); }
        catch (DefinitionException exception) { failure = exception; }

        await Assert.That(failure).IsNotNull();
        DefinitionAuthoringDiagnostic diagnostic = failure!.ToAuthoringDiagnostic();
        await Assert.That(diagnostic.Code).IsEqualTo("document-invalid");
        await Assert.That(diagnostic.Line).IsEqualTo(expectedLine);
        await Assert.That(diagnostic.Column).IsEqualTo(expectedColumn);
        await Assert.That(diagnostic.ByteOffset).IsEqualTo(expectedByteOffset);
        await AssertDoesNotLeak(failure, diagnostic, "SYNTH_CANARY_ANCHOR");
    }

    [Test]
    public async Task DuplicateKeysInJsonAndYamlReportSecondKeyCoordinatesWithoutLeakingCanaries()
    {
        const string jsonLine1 = "{";
        const string jsonLine2 = "  \"SYNTH_DUP_KEY_CANARY\": \"العربية\U0001F600\",";
        const string jsonLine3Prefix = "  ";
        const string jsonText = jsonLine1 + "\r\n" + jsonLine2 + "\r\n" + jsonLine3Prefix + "\"SYNTH_DUP_KEY_CANARY\": 2\r\n}";
        byte[] jsonBytes = CombineBomAndUtf8(jsonText);

        long expectedJsonOffset = Utf8Bom.Length + Encoding.UTF8.GetByteCount(jsonLine1 + "\r\n" + jsonLine2 + "\r\n" + jsonLine3Prefix);

        DefinitionException? jsonFailure = null;
        try { _ = DefinitionAuthoringCodec.ParseJson(jsonBytes); }
        catch (DefinitionException exception) { jsonFailure = exception; }

        await Assert.That(jsonFailure).IsNotNull();
        DefinitionAuthoringDiagnostic jsonDiagnostic = jsonFailure!.ToAuthoringDiagnostic();
        await Assert.That(jsonDiagnostic.Code).IsEqualTo("document-invalid");
        await Assert.That(jsonDiagnostic.Line).IsEqualTo(3);
        await Assert.That(jsonDiagnostic.Column).IsEqualTo(3);
        await Assert.That(jsonDiagnostic.ByteOffset).IsEqualTo(expectedJsonOffset);
        await AssertDoesNotLeak(jsonFailure, jsonDiagnostic, "SYNTH_DUP_KEY_CANARY", "العربية");

        const string yamlLine1 = "SYNTH_DUP_YAML_CANARY: \"العربية\U0001F600\"";
        const string yamlLine2 = "SYNTH_DUP_YAML_CANARY: 2";
        byte[] yamlBytes = CombineBomAndUtf8(yamlLine1 + "\r\n" + yamlLine2 + "\r\n");

        long expectedYamlOffset = Utf8Bom.Length + Encoding.UTF8.GetByteCount(yamlLine1 + "\r\n");

        DefinitionException? yamlFailure = null;
        try { _ = DefinitionAuthoringCodec.ParseYaml(yamlBytes); }
        catch (DefinitionException exception) { yamlFailure = exception; }

        await Assert.That(yamlFailure).IsNotNull();
        DefinitionAuthoringDiagnostic yamlDiagnostic = yamlFailure!.ToAuthoringDiagnostic();
        await Assert.That(yamlDiagnostic.Code).IsEqualTo("document-invalid");
        await Assert.That(yamlDiagnostic.Line).IsEqualTo(2);
        await Assert.That(yamlDiagnostic.Column).IsEqualTo(1);
        await Assert.That(yamlDiagnostic.ByteOffset).IsEqualTo(expectedYamlOffset);
        await AssertDoesNotLeak(yamlFailure, yamlDiagnostic, "SYNTH_DUP_YAML_CANARY", "العربية");
    }

    [Test]
    public async Task AdversarialCanariesInUnknownKeysInvalidScalarsAndInvalidUtf8NeverLeak()
    {
        // Unknown authoring root member containing secret canary
        string unknownMemberSource = DefinitionAuthoringTests.Source.Replace(
            "\"applicationVersions\"",
            "\"SYNTH_UNKNOWN_KEY_CANARY\":\"SYNTH_UNKNOWN_VAL_CANARY\",\"applicationVersions\"",
            StringComparison.Ordinal);
        DefinitionException? unknownFailure = null;
        try { _ = DefinitionAuthoringCompiler.Compile(DefinitionAuthoringCodec.ParseJson(Encoding.UTF8.GetBytes(unknownMemberSource))); }
        catch (DefinitionException exception) { unknownFailure = exception; }
        await Assert.That(unknownFailure).IsNotNull();
        await AssertDoesNotLeak(unknownFailure!, unknownFailure!.ToAuthoringDiagnostic(),
            "SYNTH_UNKNOWN_KEY_CANARY", "SYNTH_UNKNOWN_VAL_CANARY");

        // Invalid ambiguous YAML plain scalar adjacent to secret canary key
        byte[] invalidScalarYaml = CombineBomAndUtf8("SYNTH_SCALAR_KEY_CANARY: yes\r\n");
        DefinitionException? scalarFailure = null;
        try { _ = DefinitionAuthoringCodec.ParseYaml(invalidScalarYaml); }
        catch (DefinitionException exception) { scalarFailure = exception; }
        await Assert.That(scalarFailure).IsNotNull();
        DefinitionAuthoringDiagnostic scalarDiagnostic = scalarFailure!.ToAuthoringDiagnostic();
        await Assert.That(scalarDiagnostic.Line).IsEqualTo(1);
        await Assert.That(scalarDiagnostic.Column).IsEqualTo("SYNTH_SCALAR_KEY_CANARY: ".Length + 1);
        await AssertDoesNotLeak(scalarFailure, scalarDiagnostic, "SYNTH_SCALAR_KEY_CANARY", "yes");

        // Invalid UTF-8 sequence after BOM + ASCII prefix
        byte[] prefix = Encoding.UTF8.GetBytes("{\"SYNTH_UTF8_CANARY\":\"");
        byte[] invalidUtf8 = [.. Utf8Bom, .. prefix, 0xFF, 0xFE, (byte)'"', (byte)'}'];
        DefinitionException? utf8Failure = null;
        try { _ = DefinitionAuthoringCodec.ParseJson(invalidUtf8); }
        catch (DefinitionException exception) { utf8Failure = exception; }
        await Assert.That(utf8Failure).IsNotNull();
        DefinitionAuthoringDiagnostic utf8Diagnostic = utf8Failure!.ToAuthoringDiagnostic();
        await Assert.That(utf8Diagnostic.Line).IsEqualTo(1);
        await Assert.That(utf8Diagnostic.Column).IsEqualTo(prefix.Length + 1);
        await Assert.That(utf8Diagnostic.ByteOffset).IsEqualTo(Utf8Bom.Length + prefix.Length);
        await AssertDoesNotLeak(utf8Failure, utf8Diagnostic, "SYNTH_UTF8_CANARY");
    }

    [Test]
    public async Task TryParseReturnsStructuredValueSafeDiagnosticWithoutThrowing()
    {
        byte[] malformed = CombineBomAndUtf8("{\r\n  \"a\": ]\r\n}");
        bool parsed = DefinitionAuthoringCodec.TryParseJson(malformed, out DefinitionAuthoringDocument? document,
            out DefinitionAuthoringDiagnostic? diagnostic);

        await Assert.That(parsed).IsFalse();
        await Assert.That(document).IsNull();
        await Assert.That(diagnostic).IsNotNull();
        await Assert.That(diagnostic!.Code).IsEqualTo("document-invalid");
        await Assert.That(diagnostic.Line).IsEqualTo(2);
        await Assert.That(diagnostic.Column).IsEqualTo(8);
        await Assert.That(diagnostic.ByteOffset).IsEqualTo(Utf8Bom.Length + Encoding.UTF8.GetByteCount("{\r\n  \"a\": "));
    }

    private static byte[] CombineBomAndUtf8(string text)
    {
        byte[] content = Encoding.UTF8.GetBytes(text);
        byte[] combined = new byte[Utf8Bom.Length + content.Length];
        Utf8Bom.CopyTo(combined, 0);
        content.CopyTo(combined, Utf8Bom.Length);
        return combined;
    }

    private static int CountUnicodeScalars(string text)
    {
        int count = 0;
        foreach (Rune _ in text.EnumerateRunes())
            count++;
        return count;
    }

    private static async Task AssertDoesNotLeak(DefinitionException exception, DefinitionAuthoringDiagnostic diagnostic,
        params string[] canaries)
    {
        string serialized = JsonSerializer.Serialize(diagnostic);
        string exceptionText = exception.ToString();
        string message = exception.Message;
        foreach (string canary in canaries)
        {
            await Assert.That(serialized).DoesNotContain(canary);
            await Assert.That(exceptionText).DoesNotContain(canary);
            await Assert.That(message).DoesNotContain(canary);
        }
        await Assert.That(exception.InnerException).IsNull();
    }
}
