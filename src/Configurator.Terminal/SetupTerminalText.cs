namespace Configurator.Terminal;

using System.Globalization;
using System.Resources;
using System.Text.Json;
using Configurator.Artifacts;
using Configurator.Presentation;

internal static class SetupTerminalText
{
    private static readonly ResourceManager Resources = new(
        "Configurator.Terminal.Resources.SetupTerminalText", typeof(SetupTerminalText).Assembly);
    internal static string Get(string key) => Resources.GetString(key, CultureInfo.CurrentUICulture) ?? key;

    internal static string FormatHelp(DefinitionEditorPresentation? presentation, JsonElement metadata)
    {
        if (presentation is null) return string.Empty;
        var parts = new List<string>();
        if (presentation.Description is { Length: > 0 } description) parts.Add(description);
        if (presentation.Instructions is { Length: > 0 } instructions) parts.Add(instructions);
        if (presentation.Units is { Length: > 0 } units) parts.Add(units);
        if (metadata.TryGetProperty("x-configurator", out JsonElement extension) &&
            extension.TryGetProperty("credential", out JsonElement credential))
            parts.Add(Get(credential.GetProperty("acquisition").GetString() switch
            {
                "issuer" => "CredentialIssuer",
                "create" => "CredentialCreate",
                _ => "CredentialExisting"
            }));
        parts.AddRange(presentation.Choices.Where(choice => choice.Explanation is { Length: > 0 })
            .Select(choice => choice.Label + ": " + choice.Explanation));
        parts.AddRange(presentation.Links.Select(link => link.Title + ": " + link.Url));
        return string.Join("\n", parts);
    }

    /// <summary>Pages inert public guidance without interpreting markup, links or terminal escape sequences.</summary>
    internal static IReadOnlyList<string> HelpPages(string text, int width)
    {
        width = Math.Max(1, width);
        var lines = new List<string>();
        foreach (string paragraph in text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            string safe = new(paragraph.Select(character => char.IsControl(character) ? ' ' : character).ToArray());
            if (safe.Length == 0) lines.Add("");
            for (int start = 0; start < safe.Length;)
            {
                int length = Math.Min(width, safe.Length - start);
                if (start + length < safe.Length && char.IsHighSurrogate(safe[start + length - 1])) length--;
                if (length == 0) length = Math.Min(2, safe.Length - start);
                lines.Add(safe.Substring(start, length));
                start += length;
            }
        }
        return Enumerable.Range(0, (lines.Count + 1) / 2)
            .Select(index => string.Join("\n", lines.Skip(index * 2).Take(2))).ToArray();
    }
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
