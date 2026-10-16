using Spectre.Console;

namespace Configurator.Cli;

internal static class SetupCliHumanOutput
{
    internal static void Render(IAnsiConsole console, SetupCliCommandResult result)
    {
        ArgumentNullException.ThrowIfNull(console);
        ArgumentNullException.ThrowIfNull(result);

        var grid = new Grid();
        grid.AddColumn();
        grid.AddColumn();
        grid.AddRow(new Text("Status"), new Text(SetupCliResults.Lower(result.Exit)));
        grid.AddRow(new Text("Scope"), new Text("Local draft or export; no platform changes were applied."));
        if (result.Artifacts.Count > 0)
            grid.AddRow(new Text("Artifacts"), new Text(string.Join(", ", result.Artifacts.Select(artifact => artifact.Kind))));
        if (result.Diagnostics.Count > 0)
            grid.AddRow(new Text("Diagnostics"), new Text(string.Join(", ", result.Diagnostics.Select(diagnostic => diagnostic.Code))));
        if (result.Readiness.State != "ready")
            grid.AddRow(new Text("Readiness"), new Text(result.Readiness.State));
        console.Write(new Panel(grid).Header("islamu-configurator"));
    }
}
