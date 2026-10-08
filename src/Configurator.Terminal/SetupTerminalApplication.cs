namespace Configurator.Terminal;

using Configurator.Artifacts;
using Configurator.Core.Definitions;
using Configurator.Presentation;
using global::Terminal.Gui.App;

internal sealed class SetupTerminalApplication(SetupPresentationSession session, SetupDefinition definition)
{
    internal int Run()
    {
        if (!SetupWorkspaceId.TryCreate("definition", out SetupWorkspaceId id))
            throw new InvalidOperationException("terminal-workspace-id-invalid");
        using var form = new DefinitionFormModel(definition);
        using var secret = new SetupTerminalSecretBuffer(262144, urlSafeOnly: false);
        using var operation = new SetupTerminalArtifactOperation(form, new ProtectedArtifactWriter(), Directory.GetCurrentDirectory());
        using var workspace = session.CreateWorkspace(id, operation);
        workspace.Activate();
        using IApplication app = Application.Create();
        app.Init();
        using var window = new SetupTerminalWindow(app, workspace, operation, secret);
        using var signals = new SetupTerminalSignalScope(secret, () =>
        {
            workspace.Deactivate();
            operation.ClearPrivateState();
            app.Invoke(_ => window.RequestStopFromSignal());
        });
        app.Run(window);
        return window.ExitCode;
    }
}
