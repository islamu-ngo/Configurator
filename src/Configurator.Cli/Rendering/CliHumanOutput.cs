using System.Globalization;
using System.Text.Json;
using Configurator.Cli.Resources;
using Spectre.Console;

namespace Configurator.Cli;

internal static class CliHumanOutput
{
    internal static void Render(IAnsiConsole console, CliCommandResult result, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(console);
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(culture);
        string TextFor(string key) => CliStrings.Get(key, culture);
        string State(string token) => CliStrings.State(token, culture);

        if (result.Schema is { } schema)
        {
            console.Write(new Text(schema.Document.GetRawText() + "\n"));
            return;
        }
        var grid = new Grid();
        grid.AddColumn();
        grid.AddColumn();
        grid.AddRow(new Text(TextFor("Status")), new Text(State(CliResults.Lower(result.Exit))));
        grid.AddRow(new Text(TextFor("Scope")), new Text(TextFor("ScopeDescription")));
        if (result.Assessment is { } assessment)
        {
            grid.AddRow(new Text(TextFor("AssessmentExecution")), new Text(State(result.Completion)));
            grid.AddRow(new Text(TextFor("Compatibility")), new Text(State(assessment.Compatibility)));
            grid.AddRow(new Text(TextFor("Coverage")), new Text(State(assessment.Coverage)));
            grid.AddRow(new Text(TextFor("FindingInventory")), new Text(CliStrings.Format("RetainedInventory", culture, State(assessment.Inventory), assessment.RetainedCount)));
            grid.AddRow(new Text(TextFor("Autofill")), new Text(State(assessment.Autofill)));
            grid.AddRow(new Text(TextFor("Preparation")), new Text(State("not-evaluated")));
            grid.AddRow(new Text(TextFor("TargetAcceptance")), new Text(State("unassessed")));
            foreach (CliMachineFinding finding in assessment.Findings.Take(16))
                grid.AddRow(new Text(State(finding.Classification)), new Text(finding.Code));
            if (assessment.Findings.Count > 16)
                grid.AddRow(new Text(TextFor("MoreFindings")), new Text(TextFor("MoreFindingsGuidance")));
        }
        if (result.Artifacts.Count > 0)
            grid.AddRow(new Text(TextFor("Artifacts")), new Text(string.Join(", ", result.Artifacts.Select(artifact => artifact.Kind))));
        foreach (CliMachineArtifact artifact in result.Artifacts.Where(artifact =>
            artifact.Id is "generated-password-output" or "answer-output"))
            grid.AddRow(new Text(artifact.Id), new Text(CliStrings.Format("PublicationOutcome", culture, State(artifact.WriteStatus), State(artifact.Installation), State(artifact.Verification))));
        if (result.Exit != CliExitCode.Success && result.Artifacts.Any(artifact =>
            artifact.Id == "generated-password-output" && artifact.Installation == "established"))
            grid.AddRow(new Text(TextFor("Recovery")), new Text(TextFor("RecoveryGuidance")));
        if (result.Diagnostics.Count > 0)
        {
            foreach (CliMachineDiagnostic diagnostic in result.Diagnostics.Take(16).Select(CliMachineOutput.Sanitize))
            {
                string location = diagnostic.Source is { } source
                    ? string.Create(culture, $"{source.SchemaPath} ({source.StartLine}:{source.StartColumn}-{source.EndLine}:{source.EndColumn})")
                    : diagnostic.Correction is { } correction
                        ? JsonSerializer.Serialize(correction, CliJsonContext.Default.CliMachineCorrectionLocation)
                        : diagnostic.Path;
                grid.AddRow(new Text(State(diagnostic.Severity) + ": " + diagnostic.Code), new Text(location));
            }
            if (result.Diagnostics.Count > 16)
                grid.AddRow(new Text(TextFor("MoreDiagnostics")), new Text(TextFor("MoreDiagnosticsGuidance")));
        }
        if (result.Readiness.State != "ready")
            grid.AddRow(new Text(TextFor("Readiness")), new Text(State(result.Readiness.State)));
        console.Write(new Panel(grid).Header("islamu-configurator"));
    }
}
