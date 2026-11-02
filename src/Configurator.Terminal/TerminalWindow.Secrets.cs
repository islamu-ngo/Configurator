namespace Configurator.Terminal;

using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Configurator.Core.Definitions;
using Configurator.Presentation;
using global::Terminal.Gui.App;
using global::Terminal.Gui.ViewBase;
using global::Terminal.Gui.Views;

internal sealed partial class TerminalWindow
{
    private Task _secretCompletion = Task.CompletedTask;
    private readonly CancellationTokenSource _secretCancellation = new();
    private Dialog? _secretDialog;
    private SecretTextField? _dialogSecret;
    internal Task SecretCompletion => _secretCompletion;

    internal void ToggleSecretVisibility()
    {
        if (!_masked.Visible || _secret.Count == 0) return;
        _loading = true;
        try { _masked.SetRevealed(!_masked.IsRevealed); }
        finally { _loading = false; }
        _revealSecret.Text = TerminalText.Get(_masked.IsRevealed ? "HideSecret" : "ShowSecret");
        _masked.SetFocus();
    }

    internal void CopySecret()
    {
        if (!_masked.Visible) return;
        Status(_masked.TryCopy(NativeClipboard()) ? "SecretCopied" : "SecretClipboardUnavailable");
    }

    private IClipboard? NativeClipboard() => OperatingSystem.IsLinux() &&
        string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY"))
        ? null : _application.Clipboard;

    private void ClearSecretDialog()
    {
        _dialogSecret?.ClearSensitiveState();
        if (_secretDialog is { } dialog) _application.RequestStop(dialog);
    }

    private void GenerateSecret()
    {
        if (_slot is null || !CommitField()) return;
        string[] path = TerminalFieldEditor.EditPath(_slot, _itemIndex);
        if (!_operation.Form.CanGenerateSecret(path)) return;
        DefinitionSecretSpecification? policy = (_slot.IsList ? _slot.Schema.Items : _slot.Schema)?.Secret;
        char[] input = [];
        if (policy is { Kind: DefinitionSecretKind.PasswordHash or DefinitionSecretKind.Digest, Generation.Input: DefinitionSecretInput.Entered })
        {
            using var buffer = new TerminalSecretBuffer(65536, urlSafeOnly: false);
            using var dialog = new Dialog { Title = TerminalText.Get("SecretDerivationInput"), Width = Dim.Percent(90), Height = 9 };
            var field = new SecretTextField(buffer) { X = 1, Y = 1, Width = Dim.Fill(2) };
            bool accepted = false;
            var accept = Button("GenerateSecret", 1, 3, () => { accepted = true; _application.RequestStop(dialog); });
            var cancel = Button("Close", 49, 3, () => _application.RequestStop(dialog));
            var notice = new Label { X = 1, Y = 5, Width = Dim.Fill(2), Height = 2 };
            field.InputRejected += (_, _) => notice.Text = TerminalText.Get("SecretInputLimit");
            Button? reveal = null;
            reveal = Button("ShowSecret", 25, 3, () =>
            {
                field.SetRevealed(!field.IsRevealed);
                reveal!.Text = TerminalText.Get(field.IsRevealed ? "HideSecret" : "ShowSecret");
            });
            dialog.Add(field, accept, reveal, cancel, notice);
            _secretDialog = dialog;
            _dialogSecret = field;
            try { _application.Run(dialog); }
            finally { _secretDialog = null; _dialogSecret = null; }
            if (!accepted) return;
            input = buffer.CopyCharacters();
        }
        _secretCompletion = GenerateSecretAsync(path, input);
        Observe(_secretCompletion);
    }

    internal async Task GenerateSecretAsync(IReadOnlyList<string> path, char[]? input = null)
    {
        input ??= [];
        try
        {
            Status("SecretGenerating");
            DefinitionSecretOperationResult result = await _operation.Form.GenerateSecretAsync(path, input,
                cancellationToken: _secretCancellation.Token);
            long revision = _operation.Form.InputRevision;
            DispatchToOwner(window =>
            {
                if (Volatile.Read(ref window._closeRequested) != 0 ||
                    window._operation.Form.InputRevision != revision || window._operation.Form.SecretOperation.IsRunning) return;
                window.ShowField();
                window.Status(result.Succeeded ? "SecretGenerated" : "SecretGenerationFailed");
                if (!result.Succeeded) window._status.Text += " " + result.Code;
                if (result.Succeeded && result.HasGeneratedPassword) window.ShowGeneratedPassword();
                window.FocusInput();
            });
        }
        finally { CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(input.AsSpan())); }
    }

    private void ShowGeneratedPassword()
    {
        string? password = _operation.Form.CopyGeneratedPassword();
        if (password is null) return;
        using var buffer = new TerminalSecretBuffer(4096, urlSafeOnly: false);
        using var dialog = new Dialog { Title = TerminalText.Get("GeneratedPassword"), Width = Dim.Percent(90), Height = 10 };
        var field = new SecretTextField(buffer) { X = 1, Y = 1, Width = Dim.Fill(2), ReadOnly = true };
        field.TryReplaceSensitiveInput(password);
        var notice = new Label { X = 1, Y = 5, Width = Dim.Fill(2), Height = 2, Text = TerminalText.Get("GeneratedPasswordNotice") };
        Button? reveal = null;
        reveal = Button("ShowSecret", 1, 3, () =>
        {
            field.SetRevealed(!field.IsRevealed);
            reveal!.Text = TerminalText.Get(field.IsRevealed ? "HideSecret" : "ShowSecret");
        });
        var copy = Button("CopySecret", 25, 3, () => notice.Text = TerminalText.Get(
            field.TryCopy(NativeClipboard()) ? "SecretCopied" : "SecretClipboardUnavailable"));
        var close = Button("Close", 49, 3, () => _application.RequestStop(dialog));
        dialog.Add(field, reveal, copy, close, notice);
        _secretDialog = dialog;
        _dialogSecret = field;
        try { _application.Run(dialog); }
        finally
        {
            field.ClearSensitiveState();
            _secretDialog = null;
            _dialogSecret = null;
            _operation.Form.DismissSecretResult();
        }
    }
}
