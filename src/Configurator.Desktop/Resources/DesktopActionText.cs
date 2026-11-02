namespace Configurator.Desktop.Resources;

using System.Globalization;
using System.Resources;

/// <summary>Shipped container-action copy selected by the current UI culture, independently of layout direction.</summary>
public static class DesktopActionText
{
    private static readonly ResourceManager Resources = new(typeof(DesktopActionText));

    public static string SupplyLabel => Read(nameof(SupplyLabel));
    public static string SupplyHelp => Read(nameof(SupplyHelp));
    public static string ClearLabel => Read(nameof(ClearLabel));
    public static string ClearHelp => Read(nameof(ClearHelp));
    public static string SuggestionLabel => Read(nameof(SuggestionLabel));
    public static string ResetLabel => Read(nameof(ResetLabel));
    public static string ResetHelp => Read(nameof(ResetHelp));
    public static string NullLabel => Read(nameof(NullLabel));
    public static string NewKeyLabel => Read(nameof(NewKeyLabel));
    public static string AddEntryLabel => Read(nameof(AddEntryLabel));
    public static string LocaleLabel => Read(nameof(LocaleLabel));
    public static string FollowLocaleDirectionLabel => Read(nameof(FollowLocaleDirectionLabel));
    public static string GenerateLabel => Read(nameof(GenerateLabel));
    public static string RegenerateLabel => Read(nameof(RegenerateLabel));
    public static string ShowLabel => Read(nameof(ShowLabel));
    public static string HideLabel => Read(nameof(HideLabel));
    public static string CopySecretLabel => Read(nameof(CopySecretLabel));
    public static string SecretInputLabel => Read(nameof(SecretInputLabel));
    public static string GeneratedPasswordLabel => Read(nameof(GeneratedPasswordLabel));
    public static string CopyGeneratedPasswordLabel => Read(nameof(CopyGeneratedPasswordLabel));
    public static string DismissSecretResultLabel => Read(nameof(DismissSecretResultLabel));
    public static string CancelGenerationLabel => Read(nameof(CancelGenerationLabel));
    public static string SecretOperationMessage(string code) => code switch
    {
        "" => string.Empty,
        "secret-generated" => Read("SecretGenerated"),
        "secret-copied" => Read("SecretCopied"),
        "clipboard-unavailable" => Read("SecretClipboardUnavailable"),
        "secret-cancelled" or "secret-stale" => Read("SecretCancelled"),
        _ => Read("SecretOperationFailed")
    };
    public static string SuggestionName(string title) => string.Format(CultureInfo.CurrentCulture, Read(nameof(SuggestionName)), title);
    public static string ResetName(string title) => string.Format(CultureInfo.CurrentCulture, Read(nameof(ResetName)), title);
    public static string NullName(string title) => string.Format(CultureInfo.CurrentCulture, Read(nameof(NullName)), title);
    public static string AddEntryName(string title) => string.Format(CultureInfo.CurrentCulture, Read(nameof(AddEntryName)), title);
    public static string SupplyName(string title) => string.Format(CultureInfo.CurrentCulture, Read(nameof(SupplyName)), title);
    public static string ClearName(string title) => string.Format(CultureInfo.CurrentCulture, Read(nameof(ClearName)), title);

    private static string Read(string key) => Resources.GetString(key, CultureInfo.CurrentUICulture)!;
}
