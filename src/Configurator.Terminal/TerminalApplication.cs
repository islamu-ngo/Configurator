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
        using var operation = new TerminalArtifactOperation(form, new ProtectedArtifactWriter(), Directory.GetCurrentDirectory());
        using var workspace = session.CreateWorkspace(id, operation);
        workspace.Activate();
        using IApplication app = Application.Create();
        app.Init();
        using var window = new TerminalWindow(app, workspace, operation, secret);
        using var signals = new TerminalSignalScope(secret, window.RequestStopFromSignal);
        app.Run(window);
        return window.ExitCode;
    }
}
