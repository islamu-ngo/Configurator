namespace Configurator.Terminal;

using System.Globalization;
using System.Resources;
using Configurator.Artifacts;

internal static class SetupTerminalText
{
    private static readonly ResourceManager Resources = new(
        "Configurator.Terminal.Resources.SetupTerminalText", typeof(SetupTerminalText).Assembly);
    internal static string Get(string key) => Resources.GetString(key, CultureInfo.CurrentUICulture) ?? key;
    internal static string FormatResult(SetupTerminalArtifactResult result, int index)
    {
        int written = result.Outcomes.Count(item => item.Status == ProtectedArtifactStatus.Written);
        string heading = Get(result.Succeeded ? "ProtectedDraftSaved"
            : written > 0 ? "PartialBundle" : "ProtectedDraftFailed");
        if (result.Outcomes.Count == 0) return heading;
        ProtectedArtifactBundleOutcome outcome = result.Outcomes[index];
        return $"{heading} {written}/{result.Outcomes.Count}. "
            + $"{index + 1}/{result.Outcomes.Count} {outcome.ArtifactId}: "
            + (outcome.Status?.ToString() ?? Get("NotAttempted"))
            + (written > 0 && !result.Succeeded ? " " + Get("ManualRecovery") : string.Empty);
    }
}
