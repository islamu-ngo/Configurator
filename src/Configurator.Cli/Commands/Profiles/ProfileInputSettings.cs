using Configurator.Core.Profiles;
using Spectre.Console.Cli;

namespace Configurator.Cli.Commands;

internal class ProfileCredentialSettings : CliCommandSettings
{
    [CommandOption("--passphrase-stdin")]
    public bool PassphraseStdin { get; set; }
    [CommandOption("--passphrase-file <PATH>")]
    public string? PassphraseFile { get; set; }
    [CommandOption("--device")]
    public bool Device { get; set; }

    protected override string? ValidateValues() => base.ValidateValues()
        ?? (PassphraseStdin && PassphraseFile is not null ? "passphrase-input-conflict" : null)
        ?? (PassphraseFile is "" or "-" ? "passphrase-file-invalid" : null);

    public override CliCommand Bind(string family, string operation, CliMode invocationMode) =>
        base.Bind(family, operation, invocationMode) with
        { PassphraseInput = PassphraseStdin ? "-" : PassphraseFile, Device = Device };
}

internal class ProfileInputSettings : ProfileCredentialSettings
{
    [CommandOption("--store <PATH>")]
    public string? Store { get; set; }
    [CommandOption("--input <PATH>")]
    public string? Input { get; set; }
    [CommandOption("--profile-id <GUID>")]
    public Guid? ProfileId { get; set; }
    [CommandOption("--snapshot-id <GUID>")]
    public Guid? SnapshotId { get; set; }
    [CommandOption("--recipient <INDEX>")]
    public int RecipientIndex { get; set; }

    protected override string? ValidateRequired() => base.ValidateRequired()
        ?? (Input is null && Store is null ? "profile-input-required" : null);

    protected override string? ValidateValues() => base.ValidateValues()
        ?? (Input is not null && Store is not null ? "profile-input-conflict" : null)
        ?? (Input is "" or "-" || Store is "" or "-" ? "profile-local-input-required" : null)
        ?? (Store is not null && (ProfileId is null || SnapshotId is null) ? "profile-identity-required" : null)
        ?? (ProfileId == Guid.Empty || SnapshotId == Guid.Empty ? "profile-identity-invalid" : null)
        ?? (RecipientIndex is < 0 or >= ProfileContainerPolicy.MaximumRecipients ? "recipient-invalid" : null);

    public override CliCommand Bind(string family, string operation, CliMode invocationMode) =>
        base.Bind(family, operation, invocationMode) with
        { Store = Store, Input = Input, ProfileId = ProfileId, SnapshotId = SnapshotId, RecipientIndex = RecipientIndex };
}

internal class ProfileUnlockSettings : ProfileInputSettings
{
    protected virtual bool RequiresUnlock => true;
    protected override string? ValidateValues() => base.ValidateValues()
        ?? (Device && (PassphraseStdin || PassphraseFile is not null) ? "passphrase-input-conflict" : null)
        ?? (RequiresUnlock && !Device && !PassphraseStdin && PassphraseFile is null ? "passphrase-input-required" : null)
        ?? (Device && Store is null ? "device-store-required" : null);
}

internal class ProfilePlanSettings : ProfileUnlockSettings
{
    [CommandOption("--comparison")]
    public bool Comparison { get; set; }
    [CommandOption("--definition <PATH>")]
    public string? Definition { get; set; }
    [CommandOption("--source-definition <PATH>")]
    public string? SourceDefinition { get; set; }
    [CommandOption("--baseline <PATH>")]
    public string? Baseline { get; set; }
    protected override bool RequiresUnlock => !Comparison;

    protected override string? ValidateRequired() => base.ValidateRequired()
        ?? (string.IsNullOrEmpty(Definition) ? "definition-required" : null);
    protected override string? ValidateValues() => base.ValidateValues()
        ?? (Comparison && (Device || PassphraseStdin || PassphraseFile is not null) ? "profile-credentials-inapplicable" : null)
        ?? (Baseline is "" or "-" ? "baseline-local-file-required" : null)
        ?? (Definition == "-" || SourceDefinition is "" or "-" ? "definition-local-file-required" : null);
    public override CliCommand Bind(string family, string operation, CliMode invocationMode) =>
        base.Bind(family, operation, invocationMode) with
        {
            Definition = Definition, SourceDefinition = SourceDefinition, Baseline = Baseline,
            ProfileMode = Comparison ? "compare-only" : null
        };
}

internal sealed class ProfileAdoptSettings : ProfilePlanSettings
{
    [CommandOption("--artifact <ID>")]
    public string? Artifact { get; set; }
    [CommandOption("--answers <PATH>")]
    public string? Answers { get; set; }
    [CommandOption("--output <PATH>")]
    public string? Output { get; set; }

    protected override string? ValidateRequired() => base.ValidateRequired()
        ?? (string.IsNullOrEmpty(Artifact) ? "artifact-required" : null)
        ?? (string.IsNullOrEmpty(Output) ? "output-required" : null);
    protected override string? ValidateValues() => base.ValidateValues()
        ?? (Comparison ? "comparison-not-restorable" : null)
        ?? (Baseline is not null ? "baseline-inapplicable" : null)
        ?? (Output == "-" ? "restricted-stdout" : null)
        ?? (!IsIdentifier(Artifact!) ? "artifact-invalid" : null)
        ?? ValidateStdin(Answers, PassphraseStdin ? "-" : null);
    public override CliCommand Bind(string family, string operation, CliMode invocationMode) =>
        base.Bind(family, operation, invocationMode) with { Artifact = Artifact, Answers = Answers, Output = Output };
}

internal sealed class ProfileRecoverySettings : ProfileUnlockSettings
{
    [CommandOption("--expected-digest <SHA256>")]
    public string? ExpectedDigest { get; set; }
    [CommandOption("--device-recipient-id <GUID>")]
    public Guid? DeviceRecipientId { get; set; }
    protected override string? ValidateRequired() => base.ValidateRequired()
        ?? (Store is null ? "recovery-store-required" : null)
        ?? (ExpectedDigest is null ? "recovery-reference-required" : null);
    protected override string? ValidateValues() => base.ValidateValues()
        ?? (Device ? "independent-recovery-required" : null)
        ?? (DeviceRecipientId == Guid.Empty ? "device-recipient-invalid" : null)
        ?? (!IsDigest(ExpectedDigest!) ? "recovery-digest-invalid" : null);
    public override CliCommand Bind(string family, string operation, CliMode invocationMode) =>
        base.Bind(family, operation, invocationMode) with
        { RecoveryDigest = ExpectedDigest, DeviceRecipientId = DeviceRecipientId };
}

internal sealed class ProfileListSettings : CliCommandSettings
{
    [CommandOption("--store <PATH>")]
    public string? Store { get; set; }
    [CommandOption("--offset <COUNT>")]
    public int Offset { get; set; }
    [CommandOption("--limit <COUNT>")]
    public int Limit { get; set; } = 16;
    protected override string? ValidateRequired() => string.IsNullOrEmpty(Store) ? "store-required" : null;
    protected override string? ValidateValues() => base.ValidateValues()
        ?? (Store == "-" ? "store-local-required" : null)
        ?? (Offset is < 0 or > 4096 || Limit is < 1 or > 32 ? "profile-page-invalid" : null);
    public override CliCommand Bind(string family, string operation, CliMode invocationMode) =>
        base.Bind(family, operation, invocationMode) with
        { Store = Store, ProfileListOffset = Offset, ProfileListLimit = Limit };
}
