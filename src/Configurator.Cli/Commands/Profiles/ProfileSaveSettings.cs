using Configurator.Core.Profiles;
using Spectre.Console.Cli;

namespace Configurator.Cli.Commands;

internal sealed class ProfileSaveSettings : ProfileCredentialSettings
{
    [CommandOption("--store <PATH>")]
    public string? Store { get; set; }
    [CommandOption("--definition <PATH>")]
    public string? Definition { get; set; }
    [CommandOption("--input <PATH>")]
    public string? Input { get; set; }
    [CommandOption("--mode <MODE>")]
    public string? Mode { get; set; }
    [CommandOption("--environment-context <CONTEXT>")]
    public string? EnvironmentContext { get; set; }
    [CommandOption("--confirm-passphrase-stdin")]
    public bool ConfirmPassphraseStdin { get; set; }
    [CommandOption("--confirm-passphrase-file <PATH>")]
    public string? ConfirmPassphraseFile { get; set; }
    [CommandOption("--artifact <ID>")]
    public string? Artifact { get; set; }
    [CommandOption("--output <PATH>")]
    public string? Output { get; set; }
    [CommandOption("--acknowledge-device-only")]
    public bool AcknowledgeDeviceOnly { get; set; }
    [CommandOption("--cipher <ID>")]
    public string? Cipher { get; set; }
    [CommandOption("--kdf <ID>")]
    public string? Kdf { get; set; }

    protected override string? ValidateRequired() => base.ValidateRequired()
        ?? (string.IsNullOrEmpty(Definition) ? "definition-required" : null)
        ?? (string.IsNullOrEmpty(Input) ? "input-required" : null)
        ?? (string.IsNullOrEmpty(Mode) ? "profile-mode-required" : null)
        ?? (Mode is "import-upgrade-only" or "inspectable-encrypted" && EnvironmentContext is null
            ? "environment-context-required" : null)
        ?? (Mode != "unencrypted-target" && string.IsNullOrEmpty(Store) ? "store-required" : null)
        ?? (Mode == "unencrypted-target" && (string.IsNullOrEmpty(Artifact) || string.IsNullOrEmpty(Output))
            ? "restricted-target-required" : null);

    protected override string? ValidateValues() => base.ValidateValues()
        ?? (Cipher is not null && Cipher != ProfileContainerPolicy.ContentAlgorithm
            || Kdf is not null && Kdf != ProfileContainerPolicy.KeyAlgorithm ? "profile-policy-unsupported" : null)
        ?? (Mode is not ("compare-only" or "import-upgrade-only" or "inspectable-encrypted" or "unencrypted-target")
            ? "profile-mode-invalid" : null)
        ?? (EnvironmentContext is { Length: 0 or > 256 } ? "environment-context-invalid" : null)
        ?? (Definition == "-" || Store == "-" ? "profile-local-input-required" : null)
        ?? (Output == "-" ? "restricted-stdout" : null)
        ?? (Mode != "unencrypted-target" && (Output is not null || Artifact is not null) ? "profile-target-inapplicable" : null)
        ?? (Mode == "unencrypted-target" && Store is not null ? "profile-store-inapplicable" : null)
        ?? (Artifact is not null && !IsIdentifier(Artifact) ? "artifact-invalid" : null)
        ?? (ConfirmPassphraseStdin && ConfirmPassphraseFile is not null ? "passphrase-confirmation-conflict" : null)
        ?? (ConfirmPassphraseFile is "" or "-" ? "passphrase-confirmation-file-invalid" : null)
        ?? ValidateStdin(Input, PassphraseStdin ? "-" : null, ConfirmPassphraseStdin ? "-" : null)
        ?? (Mode is "compare-only" or "unencrypted-target" && (Device || PassphraseStdin || PassphraseFile is not null)
            ? "profile-credentials-inapplicable" : null)
        ?? (Mode is "import-upgrade-only" or "inspectable-encrypted" && !Device && !PassphraseStdin && PassphraseFile is null
            ? "passphrase-input-required" : null)
        ?? ((PassphraseStdin || PassphraseFile is not null) && !ConfirmPassphraseStdin && ConfirmPassphraseFile is null
            ? "passphrase-confirmation-required" : null)
        ?? (!PassphraseStdin && PassphraseFile is null && (ConfirmPassphraseStdin || ConfirmPassphraseFile is not null)
            ? "passphrase-confirmation-inapplicable" : null)
        ?? (Device && !PassphraseStdin && PassphraseFile is null && !AcknowledgeDeviceOnly
            ? "device-only-acknowledgement-required" : null);

    public override CliCommand Bind(string family, string operation, CliMode invocationMode) =>
        base.Bind(family, operation, invocationMode) with
        {
            Store = Store, Definition = Definition, Input = Input, ProfileMode = Mode,
            Artifact = Artifact, Output = Output, AcknowledgeDeviceOnly = AcknowledgeDeviceOnly,
            EnvironmentContext = EnvironmentContext,
            PassphraseConfirmationInput = ConfirmPassphraseStdin ? "-" : ConfirmPassphraseFile
        };
}
