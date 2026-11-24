using Spectre.Console.Cli;

namespace Configurator.Cli.Commands;

internal sealed class DoctorSettings : CliCommandSettings
{
    [CommandOption("--require <CAPABILITY>")]
    public string[]? Require { get; set; }

    protected override string? ValidateValues()
    {
        if (base.ValidateValues() is { } baseError) return baseError;
        if (Require is null) return null;
        foreach (string capability in Require)
        {
            if (!string.Equals(capability, "protected-write", StringComparison.Ordinal))
                return "doctor-capability-unknown";
        }
        return null;
    }

    public override CliCommand Bind(string family, string operation, CliMode invocationMode) =>
        base.Bind(family, operation, invocationMode) with
        {
            RequiredCapabilities = Require
        };
}

