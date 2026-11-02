namespace Configurator.Terminal;

using Configurator.Presentation;
using global::Terminal.Gui.App;

internal sealed partial class TerminalWindow
{
    internal IApplication ProbeApplication => _application;
    internal DefinitionFormModel ProbeForm => _operation.Form;

    internal void SelectProbeArtifact(string artifact) => _operation.ArtifactId = artifact;

    internal Task StartProbeImport(string path)
    {
        _importPath.Text = path;
        StartImport(true);
        return _importOperation.Completion;
    }

    internal void NextProbeFinding() => NextComparisonFinding();
    internal void ClearProbeState() => ClearPrivateState();
    internal void CancelProbeImport() => CancelImport();

    internal void ClearProbeHistory(string mode)
    {
        if (mode is "importPath" or "all") _importPath.ClearHistoryChanges();
        if (mode is "value" or "all") _value.ClearHistoryChanges();
        if (mode is "status" or "all") _status.ClearHistoryChanges();
    }
}
