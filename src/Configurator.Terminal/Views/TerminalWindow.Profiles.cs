namespace Configurator.Terminal;

internal sealed partial class TerminalWindow
{
    private TerminalProfileDialog? _profileDialog;

    private void OpenProfiles()
    {
        if (_tooSmall || _workspace.IsBusy || _importOperation.IsBusy ||
            _operation.IsPublishing || _preparing || !CommitField()) return;
        using var dialog = new TerminalProfileDialog(_operation.Form, _application, dispatch: _dispatch);
        _profileDialog = dialog;
        try { _application.Run(dialog); }
        finally { dialog.Retire(); _profileDialog = null; }
        ShowField(preserveLocation: true);
        if (dialog.TargetOutputRequested) SetStage(TerminalWorkbenchStage.FilesAndSave);
        FocusInput();
    }
}
