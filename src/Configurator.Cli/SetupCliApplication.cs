using System.Text;
using System.Text.Json;
using Configurator.Cli.Commands;
using Configurator.Core.Definitions;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Configurator.Cli;

public sealed class SetupCliApplication
{
    public SetupCliExitCode Run(SetupCliInvocation invocation)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        using var text = new StringWriter(System.Globalization.CultureInfo.InvariantCulture);
        IAnsiConsole console = AnsiConsole.Create(new AnsiConsoleSettings
        { Ansi = AnsiSupport.No, ColorSystem = ColorSystemSupport.NoColors, Interactive = InteractionSupport.No,
            Out = new AnsiConsoleOutput(text) });
        SetupCliCommand command = new("doctor", "doctor", invocation.Mode == SetupCliMode.Machine, false, false, null, null, null, null, null, null);
        SetupCliCommandResult result;
        var ledger = new SetupCliOutcomeLedger();
        try
        {
            SetupCliPreflight preflight = SetupCliArgumentPreflight.Inspect(invocation);
            command = preflight.Command;
            if (preflight.Error is not null) result = SetupCliResults.Failure(SetupCliExitCode.Usage, preflight.Error);
            else if (invocation.Environment.Names.Any(SetupCliArgumentPreflight.IsForbiddenName))
                result = SetupCliResults.Failure(SetupCliExitCode.Blocked, "environment-name-blocked");
            else if (command.Help && command.Machine) result = SetupCliResults.Success();
            else
            {
                var runtime = new SetupCliCommandRuntime(this, invocation, ledger);
                int exit = SetupCliCommandRegistry.Create(runtime, console).Run(preflight.Arguments);
                command = runtime.Command ?? command;
                result = runtime.Result ?? SetupCliResults.Success() with { Exit = (SetupCliExitCode)exit };
            }
        }
        catch (Exception exception) { result = MapFailure(exception); }
        result = ledger.Preserve(result);
        result = SetupCliMachineOutput.ValidateDetail(command, result);
        try
        {
            // No operation is inside this delivery boundary: once a write starts, never emit a replacement receipt.
            if (command.Machine)
                invocation.Io.Output.Write("-", SetupCliMachineOutput.Serialize(command, result, invocation.Io.MaximumOutputBytes), invocation.Io.MaximumOutputBytes);
            else
            {
                if (text.GetStringBuilder().Length == 0) SetupCliHumanOutput.Render(console, result);
                byte[] bytes = Encoding.UTF8.GetBytes(text.ToString());
                ISetupCliWriter writer = command.Output == "-" ? invocation.Io.Error : invocation.Io.Output;
                if (bytes.Length > 0) writer.Write("-", bytes, invocation.Io.MaximumOutputBytes);
            }
        }
        catch (Exception)
        {
            try { invocation.Io.Error.Write("-", "configurator: result delivery failed\n"u8.ToArray(), invocation.Io.MaximumOutputBytes); }
            catch (Exception) { }
            return SetupCliExitCode.Io;
        }
        return result.Exit;
    }

    internal SetupCliCommandResult Execute(SetupCliCommand command, SetupCliInvocation invocation, SetupCliOutcomeLedger ledger)
    {
        try
        {
            if ((command.Detail is not null || command.Offset != 0 || command.Limit != 16 || command.ExpectedDetailDigest is not null)
                && !(command.Family == "definition" && command.Operation == "check"
                    || command.Family == "artifact" && command.Operation is "validate" or "check-existing" or "bundle-check"))
                return SetupCliResults.Failure(SetupCliExitCode.Usage, "detail-read-only-required");
            return command.Family switch
            {
                "catalogue" => SetupCliCatalogueEnvironmentHandlers.Catalogue(command, invocation, ledger),
                "artifact" or "env" => SetupCliArtifactHandlers.Execute(command, invocation, ledger),
                "definition" => SetupCliDefinitionHandlers.Execute(command, invocation, ledger),
                "doctor" => SetupCliArtifactHandlers.Doctor(),
                _ => SetupCliResults.Failure(SetupCliExitCode.Usage, "command-unknown")
            };
        }
        catch (Exception exception) { return MapFailure(exception); }
    }

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
    internal int Execute(string family, string operation, ISetupCliCommandSettings settings)
    {
        Command = settings.Bind(family, operation, invocation.Mode);
        Result = application.Execute(Command, invocation, ledger);
        return (int)Result.Exit;
    }
}
