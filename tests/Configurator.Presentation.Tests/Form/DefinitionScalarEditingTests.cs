namespace Configurator.Presentation.Tests;

using System.Text.Json;

public sealed class DefinitionScalarEditingTests
{
    [Test]
    [Arguments("string", "", "\"\"")]
    [Arguments("boolean", "", null)]
    [Arguments("integer", "", null)]
    [Arguments("number", "", null)]
    [Arguments("string", "", "\"\"")]
    [Arguments("string", "  ", "\"  \"")]
    [Arguments("string", "null", "\"null\"")]
    [Arguments("boolean", " TRUE ", "true")]
    [Arguments("integer", " -42 ", "-42")]
    [Arguments("integer", "9223372036854775807", "9223372036854775807")]
    [Arguments("number", "1.25e2", "125")]
    [Arguments("string", "alpha", "\"alpha\"")]
    [Arguments("boolean", "true", "true")]
    [Arguments("integer", "2", "2")]
    public async Task ScalarTextKeepsDeclaredTypeAndExplicitAbsence(string type, string text, string? expected)
    {
        await Assert.That(DefinitionScalarEditing.TryParse(type, text, default, out JsonElement? value)).IsTrue();
        await Assert.That(value?.GetRawText()).IsEqualTo(expected);
    }

    [Test]
    [Arguments("boolean", "null")]
    [Arguments("boolean", "0")]
    [Arguments("integer", " ")]
    [Arguments("integer", "1.0")]
    [Arguments("integer", "9223372036854775808")]
    [Arguments("number", "NaN")]
    [Arguments("number", "Infinity")]
    [Arguments("number", "1,25")]
    [Arguments("number", "79228162514264337593543950336")]
    [Arguments("enum", "Alpha")]
    [Arguments("enum", "null")]
    [Arguments("object", "{}")]
    [Arguments("list", "[]")]
    [Arguments("array", "[]")]
    public async Task RejectedScalarTextProducesNoReplacementValue(string type, string text)
    {
        await Assert.That(DefinitionScalarEditing.TryParse(type, text, default, out JsonElement? value)).IsFalse();
        await Assert.That(value).IsNull();
    }

    [Test]
    public async Task ScalarTextLimitRejectsOversizedDraftWithoutTruncatingAcceptedText()
    {
        string maximum = new('a', 65_536);
        await Assert.That(DefinitionScalarEditing.TryParse("string", maximum, default, out JsonElement? accepted)).IsTrue();
        await Assert.That(accepted!.Value.GetString()).IsEqualTo(maximum);
        await Assert.That(DefinitionScalarEditing.TryParse("string", maximum + "b", default, out JsonElement? rejected)).IsFalse();
        await Assert.That(rejected).IsNull();
    }
}
