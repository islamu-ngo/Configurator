namespace Configurator.Browser.Components.Configuration;

using Configurator.Presentation;

internal static class ConfigurationDiagnosticText
{
    internal static string Describe(string code, string locale) => $"{PresentationStrings.Diagnostic(code, locale)} ({code})";
}
