using Spectre.Console.Cli;

namespace Configurator.Cli.Commands;

internal abstract class CliCommandSettings : CommandSettings, ICliCommandSettings
{
    [CommandOption("--machine")]
    public bool Machine { get; set; }

    [CommandOption("--text")]
    public bool Text { get; set; }

    public string? ValidateInput() => ValidateRequired() ?? ValidateValues();

    protected virtual string? ValidateRequired() => null;

    protected virtual string? ValidateValues() => Machine && Text ? "mode-conflict" : null;

    public virtual CliCommand Bind(string family, string operation, CliMode invocationMode) =>
        new(family, operation, Machine || invocationMode == CliMode.Machine,
            false, false, null, null, null, null, null, null);

    protected string? ValidateOutput(string? output, bool required = false) =>
        required && string.IsNullOrEmpty(output) ? "output-required"
            : Machine && output == "-" ? "machine-artifact-stdout" : null;

    protected string? ValidateDetail(string? detail, int offset, int limit, string? expectedDigest) =>
        detail is not null && (detail is not ("diagnostics" or "coverage" or "findings") || !Machine) ? "detail-invalid"
            : offset is < 0 or > 4096 ? "detail-offset-invalid"
            : limit is < 1 or > 32 ? "detail-limit-invalid"
            : expectedDigest is not null && !IsDigest(expectedDigest) ? "detail-digest-invalid" : null;

    protected static bool IsIdentifier(string value) =>
        value.Length is > 0 and <= 128 && char.IsAsciiLetter(value[0])
        && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '.' or '_');

    protected static bool IsDigest(string value) =>
        value.Length == 64 && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    protected static string? ValidateInputRevision(string revision) =>
        long.TryParse(revision, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture,
            out long number) && number >= 0 ? null : "input-revision-invalid";

    protected static string? ValidateStdin(params string?[] paths) =>
        paths.Count(path => path == "-") > 1 ? "stdin-conflict" : null;
}

