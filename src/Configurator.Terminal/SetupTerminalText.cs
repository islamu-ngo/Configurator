namespace Configurator.Terminal;

using System.Globalization;
using System.Resources;
using Configurator.Artifacts;

internal static class SetupTerminalText
{
    private static readonly ResourceManager Resources = new(
        "Configurator.Terminal.Resources.SetupTerminalText", typeof(SetupTerminalText).Assembly);
    internal static string Get(string key) => Resources.GetString(key, CultureInfo.CurrentUICulture) ?? key;
    internal static string FormatReceipt(ProtectedArtifactPublicationReceipt receipt, int index)
    {
        var result = new SetupTerminalArtifactResult(receipt.Outcomes,
            receipt.CompletionCode ?? ProtectedArtifactBundleCompletionCode.Completed, receipt.CleanupFailed);
        int installed = receipt.Outcomes.Count(item => item.Result is { InstallationEstablished: true });
        string state = receipt.Failed ? Get("PublicationFailed") : receipt.IsUnknown ? Get("PublicationUnknown")
            : !receipt.IsSettled ? Get("PublicationInProgress") : Get(result.Succeeded ? "ProtectedDraftSaved"
                : installed > 0 ? "PartialBundle" : "ProtectedDraftFailed");
        if (receipt.Outcomes.Count > 0)
        {
            index = Math.Clamp(index, 0, receipt.Outcomes.Count - 1);
            state += $" {installed}/{receipt.Outcomes.Count}. {index + 1}/{receipt.Outcomes.Count} " + FormatOutcome(receipt, index);
        }
        return $"{Get("PublicationAttempt")} {receipt.AttemptId:N}: {state}"
            + (receipt.Failed ? " terminal-operation-failed" : string.Empty)
            + (installed > 0 && !result.Succeeded ? " " + Get("ManualRecovery") : string.Empty)
            + (receipt.CleanupFailed ? " " + Get("PublicationCleanupFailed") : string.Empty)
            + (receipt.CompletionCode is { } code && code != ProtectedArtifactBundleCompletionCode.Completed ? " " + code : string.Empty);
    }

    internal static string FormatClosingReceipt(ProtectedArtifactPublicationReceipt receipt)
    {
        string state = receipt.IsUnknown || !receipt.IsSettled ? Get("PublicationUnknown")
            : receipt.Failed ? Get("PublicationFailed") : receipt.CompletionCode?.ToString() ?? string.Empty;
        return $"{Get("PublicationAttempt")} {receipt.AttemptId:N}: {state}. "
            + string.Join("; ", Enumerable.Range(0, receipt.Outcomes.Count).Select(index => FormatOutcome(receipt, index)))
            + (receipt.CleanupFailed ? " " + Get("PublicationCleanupFailed") : string.Empty);
    }

    private static string FormatOutcome(ProtectedArtifactPublicationReceipt receipt, int index)
    {
        ProtectedArtifactBundleOutcome outcome = receipt.Outcomes[index];
        if (outcome.Result is not { } result)
            return Safe(outcome.ArtifactId) + ": " + Get(!receipt.IsSettled || receipt.IsUnknown ? "PublicationUnknown" : "NotAttempted");
        return Safe(outcome.ArtifactId) + ": " + result.Status
            + (result.InstallationEstablished ? " [installation-established;"
                + (result.VerificationSucceeded ? "verification-succeeded" : "verification-failed")
                + ";current-integrity-not-guaranteed]" : string.Empty)
            + (result.CleanupFailed ? " " + Get("PublicationCleanupFailed") : string.Empty);
    }

    private static string Safe(string text) => new(text.Select(character => char.IsControl(character) ? ' ' : character).ToArray());
}
