namespace Configurator.Desktop.Resources;

using System.Globalization;
using System.Resources;

/// <summary>Provider-owned text bound to one workspace locale, without changing process culture.</summary>
public sealed class DesktopStrings
{
    private static readonly ResourceManager Resources = new(typeof(DesktopStrings));
    public DesktopStrings(string? locale)
    {
        try { Culture = CultureInfo.GetCultureInfo(locale ?? "en"); }
        catch (CultureNotFoundException) { Culture = CultureInfo.GetCultureInfo("en"); }
    }
    public CultureInfo Culture { get; }
    public string this[string key] => Read(key);
    public string Format(string key, params object?[] arguments) => string.Format(Culture, Read(key), arguments);

    public string SupplyLabel => Read(nameof(SupplyLabel));
    public string SupplyHelp => Read(nameof(SupplyHelp));
    public string ClearLabel => Read(nameof(ClearLabel));
    public string ClearHelp => Read(nameof(ClearHelp));
    public string SuggestionLabel => Read(nameof(SuggestionLabel));
    public string ResetLabel => Read(nameof(ResetLabel));
    public string ResetHelp => Read(nameof(ResetHelp));
    public string NullLabel => Read(nameof(NullLabel));
    public string NewKeyLabel => Read(nameof(NewKeyLabel));
    public string AddEntryLabel => Read(nameof(AddEntryLabel));
    public string LocaleLabel => Read(nameof(LocaleLabel));
    public string FollowLocaleDirectionLabel => Read(nameof(FollowLocaleDirectionLabel));
    public string GenerateLabel => Read(nameof(GenerateLabel));
    public string RegenerateLabel => Read(nameof(RegenerateLabel));
    public string ShowLabel => Read(nameof(ShowLabel));
    public string HideLabel => Read(nameof(HideLabel));
    public string CopySecretLabel => Read(nameof(CopySecretLabel));
    public string SecretInputLabel => Read(nameof(SecretInputLabel));
    public string GeneratedPasswordLabel => Read(nameof(GeneratedPasswordLabel));
    public string CopyGeneratedPasswordLabel => Read(nameof(CopyGeneratedPasswordLabel));
    public string DismissSecretResultLabel => Read(nameof(DismissSecretResultLabel));
    public string CancelGenerationLabel => Read(nameof(CancelGenerationLabel));
    public string SecretOperationMessage(string code) => code switch
    {
        "" => string.Empty,
        "secret-generated" => Read("SecretGenerated"),
        "secret-copied" => Read("SecretCopied"),
        "clipboard-unavailable" => Read("SecretClipboardUnavailable"),
        "secret-cancelled" or "secret-stale" => Read("SecretCancelled"),
        _ => Read("SecretOperationFailed")
    };
    public string SuggestionName(string title) => string.Format(Culture, Read(nameof(SuggestionName)), title);
    public string ResetName(string title) => string.Format(Culture, Read(nameof(ResetName)), title);
    public string NullName(string title) => string.Format(Culture, Read(nameof(NullName)), title);
    public string AddEntryName(string title) => string.Format(Culture, Read(nameof(AddEntryName)), title);
    public string SupplyName(string title) => string.Format(Culture, Read(nameof(SupplyName)), title);
    public string ClearName(string title) => string.Format(Culture, Read(nameof(ClearName)), title);

    public string Read(string key) => Resources.GetString(key, Culture) ?? key;
}
