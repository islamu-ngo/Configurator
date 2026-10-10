namespace Configurator.Browser.Components.Configuration;

using System.Text;
using Configurator.Core.Definitions;

internal static class ConfigurationDiagnosticText
{
    internal static string Title(SetupFieldDefinition field) => Title(field.Title, field.Id);
    internal static string Title(string title, string? id)
    {
        if (title != id) return title;
        var result = new StringBuilder(title.Length);
        for (int index = 0; index < title.Length; index++)
        {
            char current = title[index];
            if (current is '_' or '-' or '.')
            {
                if (result.Length > 0 && result[^1] != ' ') result.Append(' ');
                continue;
            }
            if (index > 0 && char.IsUpper(current) && (char.IsLower(title[index - 1]) || char.IsDigit(title[index - 1])
                || char.IsUpper(title[index - 1]) && index + 1 < title.Length && char.IsLower(title[index + 1]))
                && result.Length > 0 && result[^1] != ' ') result.Append(' ');
            result.Append(current);
        }
        return result.ToString().Trim();
    }

    internal static string Describe(string code) => (code switch
    {
        "value-required" => "Supply the required value.",
        "value-type-invalid" or "input-type-invalid" => "Use the declared field type.",
        "value-duplicate" => "Each item must have a unique declared key.",
        "value-invalid" or "value-constraint-invalid" => "Check the declared field constraints.",
        "answer-budget-exceeded" => "Reduce or clear the oversized input.",
        "draft-dependency-unresolved" => "Finish the deferred secret locally before evaluating dependent settings.",
        "artifact-omitted" => "This draft omits an artifact with unresolved dependencies.",
        _ => "Check this setting against the declared definition."
    }) + " (" + code + ")";
}
