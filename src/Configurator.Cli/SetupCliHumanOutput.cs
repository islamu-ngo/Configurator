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
        if (result.Assessment is { } assessment)
        {
            grid.AddRow(new Text("Assessment execution"), new Text(result.Completion));
            grid.AddRow(new Text("Compatibility"), new Text(assessment.Compatibility));
            grid.AddRow(new Text("Coverage"), new Text(assessment.Coverage));
            grid.AddRow(new Text("Finding inventory"), new Text(assessment.Inventory + "; retained " + assessment.RetainedCount));
            grid.AddRow(new Text("Autofill"), new Text(assessment.Autofill));
            grid.AddRow(new Text("Preparation"), new Text("not-evaluated"));
            grid.AddRow(new Text("Target acceptance"), new Text("unassessed"));
            foreach (SetupCliMachineFinding finding in assessment.Findings.Take(16))
                grid.AddRow(new Text(finding.Classification), new Text(finding.Code));
            if (assessment.Findings.Count > 16)
                grid.AddRow(new Text("More findings"), new Text("Use --machine --detail findings with nextOffset and the expected detail digest."));
        }
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
