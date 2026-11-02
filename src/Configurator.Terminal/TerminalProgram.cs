namespace Configurator.Terminal;

using CommunityToolkit.Mvvm.Messaging;
using Configurator.Core.Definitions;
using Configurator.Presentation;

internal static class TerminalProgram
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
        if (!TerminalDefinitionSelection.TryParse(arguments, out var selection))
            return Fail(64, "terminal-definition-selection-required");
        if (Console.IsInputRedirected || Console.IsOutputRedirected || Console.IsErrorRedirected)
            return Fail(4, "interactive-terminal-required");
        try
        {
            Definition definition = selection!.Load();
            using var session = new PresentationSession(new StrongReferenceMessenger());
            return new TerminalApplication(session, definition).Run();
        }
        catch (DefinitionException exception) { return Fail(65, exception.Code); }
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
