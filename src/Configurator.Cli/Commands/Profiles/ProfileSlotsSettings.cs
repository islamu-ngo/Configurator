using Spectre.Console.Cli;

namespace Configurator.Cli.Commands;

internal sealed class ProfileSlotsSettings : ProfileInputSettings
{
    [CommandOption("--action <ACTION>")]
    public string Action { get; set; } = "inspect";
    [CommandOption("--new-passphrase-stdin")]
    public bool NewPassphraseStdin { get; set; }
    [CommandOption("--new-passphrase-file <PATH>")]
    public string? NewPassphraseFile { get; set; }
    [CommandOption("--confirm-passphrase-stdin")]
    public bool ConfirmPassphraseStdin { get; set; }
    [CommandOption("--confirm-passphrase-file <PATH>")]
    public string? ConfirmPassphraseFile { get; set; }
    [CommandOption("--acknowledge-device-only")]
    public bool AcknowledgeDeviceOnly { get; set; }

    protected override string? ValidateValues() => base.ValidateValues()
        ?? (Action is not ("inspect" or "reseal") ? "profile-action-invalid" : null)
        ?? (NewPassphraseStdin && NewPassphraseFile is not null ? "passphrase-input-conflict" : null)
        ?? (NewPassphraseFile is "" or "-" ? "passphrase-file-invalid" : null)
        ?? (ConfirmPassphraseStdin && ConfirmPassphraseFile is not null ? "passphrase-confirmation-conflict" : null)
        ?? (ConfirmPassphraseFile is "" or "-" ? "passphrase-confirmation-file-invalid" : null)
        ?? ValidateStdin(PassphraseStdin ? "-" : null, NewPassphraseStdin ? "-" : null,
            ConfirmPassphraseStdin ? "-" : null)
        ?? (Action == "inspect" && (PassphraseStdin || PassphraseFile is not null || Device
            || NewPassphraseStdin || NewPassphraseFile is not null || ConfirmPassphraseStdin
            || ConfirmPassphraseFile is not null) ? "profile-credentials-inapplicable" : null)
        ?? (Action == "reseal" && Store is null ? "profile-store-required" : null)
        ?? (Action == "reseal" && !Device && !PassphraseStdin && PassphraseFile is null ? "passphrase-input-required" : null)
        ?? (Action == "reseal" && Device && (PassphraseStdin || PassphraseFile is not null) ? "passphrase-input-conflict" : null)
        ?? (Action == "reseal" && !NewPassphraseStdin && NewPassphraseFile is null ? "new-passphrase-input-required" : null)
        ?? (Action == "reseal" && !ConfirmPassphraseStdin && ConfirmPassphraseFile is null
            ? "passphrase-confirmation-required" : null);

    public override CliCommand Bind(string family, string operation, CliMode invocationMode) =>
        base.Bind(family, operation, invocationMode) with
        {
            ProfileAction = Action, NewPassphraseInput = NewPassphraseStdin ? "-" : NewPassphraseFile,
            AcknowledgeDeviceOnly = AcknowledgeDeviceOnly,
            PassphraseConfirmationInput = ConfirmPassphraseStdin ? "-" : ConfirmPassphraseFile
        };
}
