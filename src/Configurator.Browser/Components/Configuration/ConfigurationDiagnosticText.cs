namespace Configurator.Browser.Components.Configuration;

using System.Text;
using Configurator.Core.Definitions;
using Configurator.Presentation;

internal static class ConfigurationDiagnosticText
{
    internal static string Title(FieldDefinition field) => Title(field.Title, field.Id);
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

    internal static string Describe(string code, string locale) => $"{PresentationStrings.Diagnostic(code, locale)} ({code})";
}
