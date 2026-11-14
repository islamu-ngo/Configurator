namespace Configurator.Terminal;

using System.Globalization;
using System.Resources;
using System.Text.Json;
using Configurator.Artifacts;
using Configurator.Presentation;

internal static class TerminalStrings
{
    private static readonly ResourceManager Resources = new(typeof(TerminalStrings));
    internal static string Get(string key, string locale = "en") => Resources.GetString(key, Culture(locale)) ?? key;

    internal static CultureInfo Culture(string locale)
    {
        try { return CultureInfo.GetCultureInfo(locale); }
        catch (CultureNotFoundException) { return CultureInfo.GetCultureInfo("en"); }
    }

    internal static string FormatHelp(DefinitionEditorPresentation? presentation, JsonElement metadata, string locale = "en")
    {
        string Get(string key) => TerminalStrings.Get(key, locale);
        if (presentation is null) return string.Empty;
        var parts = new List<string>();
        if (presentation.Description is { Length: > 0 } description) parts.Add(description);
        if (presentation.Instructions is { Length: > 0 } instructions) parts.Add(instructions);
        var qualifiers = new List<string>();
        if (presentation.Units is { Length: > 0 } units) qualifiers.Add(Get("UnitsLabel") + ": " + units);
        if (presentation.IfOmittedDisplay is { Length: > 0 } ifOmitted) qualifiers.Add(ifOmitted);
        if (qualifiers.Count > 0) parts.Add(string.Join(" | ", qualifiers));
        if (presentation.IsDeprecated)
        {
            string badge = presentation.DeprecationGuidance is { Length: > 0 } guide
                ? Get("DeprecatedBadge") + ": " + guide
                : Get("DeprecatedBadge");
            parts.Add(badge);
        }
        if (metadata.TryGetProperty("x-configurator", out JsonElement extension) &&
            extension.TryGetProperty("secret", out JsonElement secret))
            parts.Add(Get(secret.GetProperty("acquisition").GetString() switch
            {
                "issuer" => "CredentialIssuer",
                "local" => "CredentialCreate",
                _ => "CredentialExisting"
            }));
        parts.AddRange(presentation.Choices.Where(choice => choice.Explanation is { Length: > 0 } || choice.IsDeprecated)
            .Select(choice => choice.Label
                + (choice.HasCopyableWireToken && choice.WireToken != choice.Label ? $" ({choice.WireToken})" : "")
                + (choice.IsDeprecated && choice.DeprecationGuidance is { Length: > 0 } dep
                    ? $" [{Get("DeprecatedBadge")}: {dep}]" : "")
                + (choice.Explanation is { Length: > 0 } exp ? ": " + exp : "")));
        parts.AddRange(presentation.Links.Select(link => link.Title + ": " + link.Url));
        return string.Join("\n", parts);
    }

    /// <summary>Pages inert public guidance without interpreting markup, links or terminal escape sequences.</summary>
    internal static IReadOnlyList<string> HelpPages(string text, int width, int linesPerPage = 3)
    {
        width = Math.Max(1, width);
        linesPerPage = Math.Max(1, linesPerPage);
        var lines = new List<string>();
        foreach (string paragraph in text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            string safe = Safe(paragraph);
            if (safe.Length == 0) lines.Add("");
            for (int start = 0; start < safe.Length;)
            {
                int remaining = safe.Length - start;
                if (remaining <= width)
                {
                    lines.Add(safe[start..]);
                    break;
                }
                int length = width;
                int breakAt = safe.LastIndexOf(' ', start + width - 1, width);
                if (breakAt > start) length = breakAt - start;
                else if (char.IsHighSurrogate(safe[start + length - 1])) length--;
                if (length <= 0) length = Math.Min(2, remaining);
                lines.Add(safe.Substring(start, length));
                start += length;
                while (start < safe.Length && safe[start] == ' ') start++;
            }
        }
        int pageCount = (lines.Count + linesPerPage - 1) / linesPerPage;
        return Enumerable.Range(0, pageCount)
            .Select(index => string.Join("\n", lines.Skip(index * linesPerPage).Take(linesPerPage))).ToArray();
    }
    internal static string FormatReceipt(ProtectedArtifactPublicationReceipt receipt, int index, string locale = "en")
    {
        string Get(string key) => TerminalStrings.Get(key, locale);
        var result = new TerminalArtifactResult(receipt.Outcomes,
            receipt.CompletionCode ?? ProtectedArtifactBundleCompletionCode.Completed, receipt.CleanupFailed);
        int installed = receipt.Outcomes.Count(item => item.Result is { InstallationEstablished: true });
        string state = receipt.Failed ? Get("PublicationFailed") : receipt.IsUnknown ? Get("PublicationUnknown")
            : !receipt.IsSettled ? Get("PublicationInProgress") : Get(result.Succeeded ? "ProtectedDraftSaved"
                : installed > 0 ? "PartialBundle" : "ProtectedDraftFailed");
        if (receipt.Outcomes.Count > 0)
        {
            index = Math.Clamp(index, 0, receipt.Outcomes.Count - 1);
            state += string.Create(Culture(locale), $" {installed}/{receipt.Outcomes.Count}. {index + 1}/{receipt.Outcomes.Count} ") + FormatOutcome(receipt, index, locale);
        }
        return $"{Get("PublicationAttempt")} {receipt.AttemptId:N}: {state}"
            + (installed > 0 && !result.Succeeded ? " " + Get("ManualRecovery") : string.Empty)
            + (receipt.CleanupFailed ? " " + Get("PublicationCleanupFailed") : string.Empty)
            + (receipt.CompletionCode is { } code && code != ProtectedArtifactBundleCompletionCode.Completed ? " " + Get("Completion" + code) : string.Empty);
    }

    internal static string FormatClosingReceipt(ProtectedArtifactPublicationReceipt receipt, string locale = "en")
    {
        string Get(string key) => TerminalStrings.Get(key, locale);
        string state = receipt.IsUnknown || !receipt.IsSettled ? Get("PublicationUnknown")
            : receipt.Failed ? Get("PublicationFailed") : receipt.CompletionCode is { } code ? Get("Completion" + code) : string.Empty;
        return $"{Get("PublicationAttempt")} {receipt.AttemptId:N}: {state}. "
            + string.Join("; ", Enumerable.Range(0, receipt.Outcomes.Count).Select(index => FormatOutcome(receipt, index, locale)))
            + (receipt.CleanupFailed ? " " + Get("PublicationCleanupFailed") : string.Empty);
    }

    private static string FormatOutcome(ProtectedArtifactPublicationReceipt receipt, int index, string locale)
    {
        string Get(string key) => TerminalStrings.Get(key, locale);
        ProtectedArtifactBundleOutcome outcome = receipt.Outcomes[index];
        if (outcome.Result is not { } result)
            return Safe(outcome.ArtifactId) + ": " + Get(!receipt.IsSettled || receipt.IsUnknown ? "PublicationUnknown" : "NotAttempted");
        return Safe(outcome.ArtifactId) + ": " + Get("Output" + result.Status)
            + (result.InstallationEstablished ? " [" + Get("InstallationEstablished") + "; "
                + Get(result.VerificationSucceeded ? "VerificationSucceeded" : "VerificationFailed")
                + "; " + Get("CurrentIntegrityNotGuaranteed") + "]" : string.Empty)
            + (result.CleanupFailed ? " " + Get("PublicationCleanupFailed") : string.Empty);
    }

    private static string Safe(string text) => new(text.Select(character => char.IsControl(character) ? ' ' : character).ToArray());
}
