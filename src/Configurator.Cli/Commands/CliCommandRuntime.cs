using System.Text;
using System.Text.Json;
using Configurator.Cli.Commands;
using Configurator.Core.Definitions;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Configurator.Cli;

internal sealed class CliCommandRuntime(CliApplication application, CliInvocation invocation, CliOutcomeLedger ledger)
{
    internal CliCommandResult? Result { get; private set; }
    internal CliCommand? Command { get; private set; }
    internal async Task<int> ExecuteAsync(string family, string operation, ICliCommandSettings settings,
        IRemainingArguments remaining, CancellationToken cancellationToken)
    {
        if (settings is CliCommandSettings typed && invocation.Mode == CliMode.Machine)
            typed.Machine = true;
        CliCommand bound = settings.Bind(family, operation, invocation.Mode);
        string? error = remaining.Raw.Count > 0 || remaining.Parsed.Count > 0 ? "arguments-invalid" : settings.ValidateInput();
        Command = error is null ? bound
            : new CliCommand(family, operation, bound.Machine,
                false, false, null, null, null, null, null, null);
        Result = error is not null ? CliResults.Failure(CliExitCode.Usage, error)
            : await application.ExecuteAsync(Command, invocation, ledger, cancellationToken);
        return (int)Result.Exit;
    }
}
