using System.Text.Json;
using Spectre.Console;

namespace Configurator.Cli;

internal static class SetupCliHumanOutput
{
    internal static void Render(IAnsiConsole console, SetupCliCommandResult result)
    {
        ArgumentNullException.ThrowIfNull(console);
        ArgumentNullException.ThrowIfNull(result);

        if (result.Schema is { } schema)
        {
            console.Write(new Text(schema.Document.GetRawText() + "\n"));
            return;
        }
        var grid = new Grid();
        grid.AddColumn();
        grid.AddColumn();
        grid.AddRow(new Text("Status"), new Text(SetupCliResults.Lower(result.Exit)));
        grid.AddRow(new Text("Scope"), new Text("Local draft or export; no platform changes were applied."));
        if (result.Artifacts.Count > 0)
            grid.AddRow(new Text("Artifacts"), new Text(string.Join(", ", result.Artifacts.Select(artifact => artifact.Kind))));
        if (result.Diagnostics.Count > 0)
        {
            foreach (SetupCliMachineDiagnostic diagnostic in result.Diagnostics.Take(16).Select(SetupCliMachineOutput.Sanitize))
            {
                string location = diagnostic.Source is { } source
                    ? $"{source.SchemaPath} ({source.StartLine}:{source.StartColumn}-{source.EndLine}:{source.EndColumn})"
                    : diagnostic.Correction is { } correction
                        ? JsonSerializer.Serialize(correction, SetupCliJsonContext.Default.SetupCliMachineCorrectionLocation)
                        : diagnostic.Path;
                grid.AddRow(new Text(diagnostic.Severity + ": " + diagnostic.Code), new Text(location));
            }
            if (result.Diagnostics.Count > 16)
                grid.AddRow(new Text("More diagnostics"), new Text("Use --machine --detail diagnostics with bounded continuation."));
        }
        if (result.Readiness.State != "ready")
            grid.AddRow(new Text("Readiness"), new Text(result.Readiness.State));
        console.Write(new Panel(grid).Header("islamu-configurator"));
    }
}
