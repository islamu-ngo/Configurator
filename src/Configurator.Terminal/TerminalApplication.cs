namespace Configurator.Terminal;

using Configurator.Artifacts;
using Configurator.Core.Definitions;
using Configurator.Presentation;
using global::Terminal.Gui.App;

internal sealed class TerminalApplication(PresentationSession session, Definition definition)
{
    internal int Run()
    {
        if (!WorkspaceId.TryCreate("definition", out WorkspaceId id))
            throw new InvalidOperationException("terminal-workspace-id-invalid");
        using var form = new DefinitionFormModel(definition);
        using var secret = new TerminalSecretBuffer(262144, urlSafeOnly: false);
#if CONFIGURATOR_NATIVE_IMPORT_PROBE
        TerminalPartialSaveProbe? partialSave = TerminalPartialSaveProbe.Bind(definition);
        using var operation = new TerminalArtifactOperation(form, new ProtectedArtifactWriter(), Directory.GetCurrentDirectory(),
            outcomeObserver: partialSave is null ? null : partialSave.ObserveOutcome);
#else
        using var operation = new TerminalArtifactOperation(form, new ProtectedArtifactWriter(), Directory.GetCurrentDirectory());
#endif
        using var workspace = session.CreateWorkspace(id, operation);
        workspace.Activate();
#if CONFIGURATOR_NATIVE_IMPORT_PROBE
        Configurator.Engineering.MeasureNativePreparation.ObserveLoopCadence(
            Application.MaximumIterationsPerSecond, Application.DefaultMaximumIterationsPerSecond);
#endif
        // Dispatch and drawing can occupy separate iterations before terminal output completes.
        Application.MaximumIterationsPerSecond = 120;
        using IApplication app = Application.Create();
        app.Init();
        using var window = new TerminalWindow(app, workspace, operation, secret);
#if CONFIGURATOR_NATIVE_IMPORT_PROBE
        partialSave?.Attach(window);
#endif
        using var signals = new TerminalSignalScope(secret, window.RequestStopFromSignal);
        app.Run(window);
        return window.ExitCode;
    }
}
