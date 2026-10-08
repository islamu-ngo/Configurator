namespace Configurator.Terminal;

using CommunityToolkit.Mvvm.Messaging;
using Configurator.Core.Definitions;
using Configurator.Presentation;

internal static class SetupTerminalProgram
{
    internal const string Usage = """
        configurator-terminal --definition <local.json|local.yaml>
        configurator-terminal --registry <local-directory> --family <guid> [--app-version <version>]
        No answer values are accepted on the command line. Output is protected and create-only.
        """;

    internal static int Run(string[] arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        if (arguments is ["--help"])
        {
            Console.Out.WriteLine(Usage);
            return 0;
        }
        if (!SetupTerminalDefinitionSelection.TryParse(arguments, out var selection))
            return Fail(64, "terminal-definition-selection-required");
        if (Console.IsInputRedirected || Console.IsOutputRedirected || Console.IsErrorRedirected)
            return Fail(4, "interactive-terminal-required");
        try
        {
            SetupDefinition definition = selection!.Load();
            using var session = new SetupPresentationSession(new StrongReferenceMessenger());
            return new SetupTerminalApplication(session, definition).Run();
        }
        catch (SetupDefinitionException exception) { return Fail(65, exception.Code); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
            or InvalidOperationException or NotSupportedException or ArgumentException)
        {
            return Fail(70, "terminal-start-failed");
        }
    }

    private static int Fail(int exitCode, string diagnostic)
    {
        try { Console.Error.WriteLine(diagnostic); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
        return exitCode;
    }
}
