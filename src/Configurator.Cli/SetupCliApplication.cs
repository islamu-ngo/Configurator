using System.Text;
using System.Text.Json;
using Configurator.Cli.Commands;
using Configurator.Core.Definitions;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Configurator.Cli;

public sealed class SetupCliApplication
{
    public async Task<SetupCliExitCode> RunAsync(SetupCliInvocation invocation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        using var text = new StringWriter(System.Globalization.CultureInfo.InvariantCulture);
        IAnsiConsole console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No,
            ColorSystem = ColorSystemSupport.NoColors,
            Interactive = InteractionSupport.No,
            Out = new AnsiConsoleOutput(text)
        });
        SetupCliCommand command = new("doctor", "doctor", invocation.Mode == SetupCliMode.Machine, false, false, null, null, null, null, null, null);
        SetupCliCommandResult result;
        var ledger = new SetupCliOutcomeLedger();
        try
        {
            SetupCliPreflight preflight = SetupCliArgumentPreflight.Inspect(invocation);
            command = preflight.Command;
            cancellationToken.ThrowIfCancellationRequested();
            if (preflight.Error is not null) result = SetupCliResults.Failure(SetupCliExitCode.Usage, preflight.Error);
            else
            {
                var runtime = new SetupCliCommandRuntime(this, invocation, ledger);
                SetupCliHelpProvider? helpProvider = command.Machine ? new() : null;
                int exit = await SetupCliCommandRegistry.Create(runtime, console, helpProvider).RunAsync(preflight.Arguments, cancellationToken);
                command = runtime.Command ?? command;
                result = helpProvider?.View is { } help ? SetupCliResults.Success() with { Help = help }
                    : command.Help && command.Machine ? SetupCliResults.Failure(SetupCliExitCode.Internal, "help-unavailable")
                    : runtime.Result ?? (exit == (int)SetupCliExitCode.Cancelled
                        ? Cancelled() : SetupCliResults.Success() with { Exit = (SetupCliExitCode)exit });
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { result = Cancelled(); }
        catch (Exception exception)
        {
            text.GetStringBuilder().Clear();
            result = MapFailure(exception);
        }
        if (result.Exit == SetupCliExitCode.Success && cancellationToken.IsCancellationRequested) result = Cancelled();
        result = ledger.Preserve(result);
        if (result.Exit is not (SetupCliExitCode.Cancelled or SetupCliExitCode.Io or SetupCliExitCode.Internal))
            result = SetupCliMachineOutput.ValidateDetail(command, result);
        using var delivery = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            // No operation is inside this delivery boundary: once a write starts, never emit a replacement receipt.
            if (command.Machine)
                await invocation.Io.Output.WriteAsync("-", SetupCliMachineOutput.Serialize(command, result, invocation.Io.MaximumOutputBytes),
                    invocation.Io.MaximumOutputBytes, delivery.Token);
            else
            {
                if (text.GetStringBuilder().Length == 0) SetupCliHumanOutput.Render(console, result);
                byte[] bytes = Encoding.UTF8.GetBytes(text.ToString());
                ISetupCliWriter writer = command.Output == "-" || result.Exit != SetupCliExitCode.Success
                    ? invocation.Io.Error : invocation.Io.Output;
                if (bytes.Length > 0) await writer.WriteAsync("-", bytes, invocation.Io.MaximumOutputBytes, delivery.Token);
            }
        }
        catch (Exception)
        {
            try
            {
                await invocation.Io.Error.WriteAsync("-", "configurator: result delivery failed\n"u8.ToArray(),
                invocation.Io.MaximumOutputBytes, delivery.Token);
            }
            catch (Exception) { }
            return SetupCliExitCode.Io;
        }
        return result.Exit;
    }

    internal async Task<SetupCliCommandResult> ExecuteAsync(SetupCliCommand command, SetupCliInvocation invocation,
        SetupCliOutcomeLedger ledger, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if ((command.Detail is not null || command.Offset != 0 || command.Limit != 16 || command.ExpectedDetailDigest is not null)
                && !(command.Family == "definition" && command.Operation is "check" or "lint"
                    || command.Family == "artifact" && command.Operation is "validate" or "check-existing" or "compare" or "bundle-check"))
                return SetupCliResults.Failure(SetupCliExitCode.Usage, "detail-read-only-required");
            return command.Family switch
            {
                "catalogue" => await SetupCliCatalogueEnvironmentHandlers.CatalogueAsync(command, invocation, ledger, cancellationToken),
                "artifact" or "env" => await SetupCliArtifactHandlers.ExecuteAsync(command, invocation, ledger, cancellationToken),
                "definition" => await SetupCliDefinitionHandlers.ExecuteAsync(command, invocation, ledger, cancellationToken),
                "doctor" => SetupCliArtifactHandlers.Doctor(),
                _ => SetupCliResults.Failure(SetupCliExitCode.Usage, "command-unknown")
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return Cancelled(); }
        catch (Exception exception) { return MapFailure(exception); }
    }

    private static SetupCliCommandResult Cancelled() => SetupCliResults.Failure(SetupCliExitCode.Cancelled, "cancelled")
        with
    { Completion = "cancelled" };

    private static SetupCliCommandResult MapFailure(Exception exception) => exception switch
    {
        CommandParseException => SetupCliResults.Failure(SetupCliExitCode.Usage, "arguments-invalid"),
        SetupDefinitionException => SetupCliResults.Failure(SetupCliExitCode.Data, "definition-or-answer-invalid", "/input"),
        JsonException => SetupCliResults.Failure(SetupCliExitCode.Data, "input-document-invalid", "/input"),
        IOException or UnauthorizedAccessException => SetupCliResults.Failure(SetupCliExitCode.Io, "io-failed"),
        _ => SetupCliResults.Failure(SetupCliExitCode.Internal, "internal-failed")
    };
}

internal sealed class SetupCliCommandRuntime(SetupCliApplication application, SetupCliInvocation invocation, SetupCliOutcomeLedger ledger)
{
    internal SetupCliCommandResult? Result { get; private set; }
    internal SetupCliCommand? Command { get; private set; }
    internal async Task<int> ExecuteAsync(string family, string operation, ISetupCliCommandSettings settings,
        IRemainingArguments remaining, CancellationToken cancellationToken)
    {
        if (settings is SetupCliCommandSettings typed && invocation.Mode == SetupCliMode.Machine)
            typed.Machine = true;
        SetupCliCommand bound = settings.Bind(family, operation, invocation.Mode);
        string? error = remaining.Raw.Count > 0 || remaining.Parsed.Count > 0 ? "arguments-invalid" : settings.ValidateInput();
        Command = error is null ? bound
            : new SetupCliCommand(family, operation, bound.Machine,
                false, false, null, null, null, null, null, null);
        Result = error is not null ? SetupCliResults.Failure(SetupCliExitCode.Usage, error)
            : await application.ExecuteAsync(Command, invocation, ledger, cancellationToken);
        return (int)Result.Exit;
    }
}
