namespace Configurator.Terminal;

using System.Globalization;
using System.Resources;
using Configurator.Artifacts;

internal static class SetupTerminalText
{
    private static readonly ResourceManager Resources = new(
        "Configurator.Terminal.Resources.SetupTerminalText", typeof(SetupTerminalText).Assembly);
    internal static string Get(string key) => Resources.GetString(key, CultureInfo.CurrentUICulture) ?? key;
    internal static string FormatResult(SetupTerminalArtifactResult result) =>
        Get(result.Status == ProtectedArtifactStatus.Written ? "ProtectedDraftSaved" : "ProtectedDraftFailed")
        + " " + result.Status + (result.Digest is null ? string.Empty : " " + result.Digest);
}
