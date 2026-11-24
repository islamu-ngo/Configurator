using System.Text;
using System.Text.Json;
using Configurator.Artifacts;
using Configurator.Artifacts.Profiles;
using Configurator.Cli.Commands;
using Configurator.Core.Definitions;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Configurator.Cli;


public sealed class CliApplication
{
    private readonly Func<string, IProfileVaultStore> _profileStore;
    private readonly Func<IOsKeyringProvider> _profileKeyring;
    private readonly Func<bool> _isProtectedWriterAvailable;

    public CliApplication() : this(
        root => new LocalProfileVaultStore(root),
        () => new OsKeyringProvider(),
        () => new ProtectedArtifactWriter().IsAvailable)
    { }

    internal CliApplication(
        Func<string, IProfileVaultStore>? profileStore = null,
        Func<IOsKeyringProvider>? profileKeyring = null,
        Func<bool>? isProtectedWriterAvailable = null)
    {
        _profileStore = profileStore ?? (root => new LocalProfileVaultStore(root));
        _profileKeyring = profileKeyring ?? (() => new OsKeyringProvider());
        _isProtectedWriterAvailable = isProtectedWriterAvailable ?? (() => new ProtectedArtifactWriter().IsAvailable);
    }
    public Task<CliExitCode> RunAsync(CliInvocation invocation, CancellationToken cancellationToken = default) =>
        RunCoreAsync(invocation, null, cancellationToken);

    internal Task<CliExitCode> RejectArgumentsAsync(CliMode mode, CliIo io, CancellationToken cancellationToken) =>
        RunCoreAsync(new CliInvocation([], mode, io), "argument-shape-invalid", cancellationToken);

    private async Task<CliExitCode> RunCoreAsync(CliInvocation invocation, string? argumentError,
        CancellationToken cancellationToken)
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
        CliCommand command = new("doctor", "doctor", invocation.Mode == CliMode.Machine, false, false, null, null, null, null, null, null);
        CliCommandResult result;
        var ledger = new CliOutcomeLedger();
        try
        {
            CliPreflight preflight = CliArgumentPreflight.Inspect(invocation);
            command = preflight.Command;
            cancellationToken.ThrowIfCancellationRequested();
            if (argumentError is not null || preflight.Error is not null)
                result = CliResults.Failure(CliExitCode.Usage, argumentError ?? preflight.Error!);
            else if (command.Version)
                result = CliDiscoveryHandlers.Version(_isProtectedWriterAvailable());
            else
            {
                var runtime = new CliCommandRuntime(this, invocation, ledger);
                CliHelpProvider? helpProvider = command.Machine ? new() : null;
                int exit = await CliCommandRegistry.Create(runtime, console, helpProvider).RunAsync(preflight.Arguments, cancellationToken);
                command = runtime.Command ?? command;
                result = helpProvider?.View is { } help ? CliResults.Success() with { Help = help }
                    : command.Help && command.Machine ? CliResults.Failure(CliExitCode.Internal, "help-unavailable")
                    : runtime.Result ?? (exit == (int)CliExitCode.Cancelled
                        ? Cancelled() : CliResults.Success() with { Exit = (CliExitCode)exit });
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { result = Cancelled(); }
        catch (Exception exception)
        {
            text.GetStringBuilder().Clear();
            result = MapFailure(exception);
        }
        if (result.Exit == CliExitCode.Success && cancellationToken.IsCancellationRequested) result = Cancelled();
        result = ledger.Preserve(result);
        if (result.Exit is not (CliExitCode.Cancelled or CliExitCode.Io or CliExitCode.Internal))
            result = CliMachineOutput.ValidateDetail(command, result);
        using var delivery = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            // No operation is inside this delivery boundary: once a write starts, never emit a replacement receipt.
            if (command.Machine)
                await invocation.Io.Output.WriteAsync("-", CliMachineOutput.Serialize(command, result, invocation.Io.MaximumOutputBytes),
                    invocation.Io.MaximumOutputBytes, delivery.Token);
            else if (result.Exit == CliExitCode.Success && result.RawTextOutput is { } rawText)
            {
                byte[] rawBytes = Encoding.UTF8.GetBytes(rawText);
                await invocation.Io.Output.WriteAsync("-", rawBytes, invocation.Io.MaximumBytes, delivery.Token);
            }
            else
            {
                if (text.GetStringBuilder().Length == 0) CliHumanOutput.Render(console, result, invocation.Culture);
                byte[] bytes = Encoding.UTF8.GetBytes(text.ToString());
                ICliWriter writer = command.Output == "-" || result.Exit != CliExitCode.Success
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
            return CliExitCode.Io;
        }
        return result.Exit;
    }

    internal async Task<CliCommandResult> ExecuteAsync(CliCommand command, CliInvocation invocation,
        CliOutcomeLedger ledger, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if ((command.Detail is not null || command.Offset != 0 || command.Limit != 16 || command.ExpectedDetailDigest is not null)
                && !(command.Family == "definition" && command.Operation is "check" or "lint" or "inspect" or "describe"
                    || command.Family == "artifact" && command.Operation is "validate" or "check-existing" or "compare" or "bundle-check"))
                return CliResults.Failure(CliExitCode.Usage, "detail-read-only-required");
            return command.Family switch
            {
                "info" => CliDiscoveryHandlers.Info(_isProtectedWriterAvailable()),
                "schema" => await CliDiscoveryHandlers.SchemaAsync(command, invocation, ledger, cancellationToken),
                "catalogue" => await CliCatalogueEnvironmentHandlers.CatalogueAsync(command, invocation, ledger, cancellationToken),
                "artifact" => await CliArtifactHandlers.ExecuteAsync(command, invocation, ledger, cancellationToken),
                "definition" => await CliDefinitionHandlers.ExecuteAsync(command, invocation, ledger, cancellationToken),
                "secret" => await CliSecretHandlers.ExecuteAsync(command, invocation, ledger, cancellationToken),
                "profile" => await CliProfileHandlers.ExecuteAsync(command, invocation, ledger,
                    _profileStore, _profileKeyring, cancellationToken),
                "doctor" => CliDiscoveryHandlers.Doctor(command, _isProtectedWriterAvailable()),
                _ => CliResults.Failure(CliExitCode.Usage, "command-unknown")
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return Cancelled(); }
        catch (Exception exception) { return MapFailure(exception); }
    }

    private static CliCommandResult Cancelled() => CliResults.Failure(CliExitCode.Cancelled, "cancelled")
        with
    { Completion = "cancelled" };

    private static CliCommandResult MapFailure(Exception exception) => exception switch
    {
        CommandParseException => CliResults.Failure(CliExitCode.Usage, "arguments-invalid"),
        DefinitionException => CliResults.Failure(CliExitCode.Data, "definition-or-answer-invalid", "/input"),
        JsonException => CliResults.Failure(CliExitCode.Data, "input-document-invalid", "/input"),
        IOException or UnauthorizedAccessException => CliResults.Failure(CliExitCode.Io, "io-failed"),
        _ => CliResults.Failure(CliExitCode.Internal, "internal-failed")
    };
}

