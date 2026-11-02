namespace Configurator.Terminal;

using global::Terminal.Gui.App;
using global::Terminal.Gui.Drivers;
using global::Terminal.Gui.Input;
using global::Terminal.Gui.Views;

internal sealed class SecretTextField : TextField
{
    private readonly TerminalSecretBuffer _secret;

    internal SecretTextField(TerminalSecretBuffer secret)
    {
        _secret = secret ?? throw new ArgumentNullException(nameof(secret));
        Secret = true;
        foreach (Command command in new[]
        {
            Command.Copy,
            Command.Cut,
            Command.Paste,
            Command.Undo,
            Command.Redo,
            Command.Context,
            Command.CutToEndOfLine,
            Command.CutToStartOfLine
        })
            AddCommand(command, () => BlockSensitiveCommand());
        foreach (Command command in new[]
        {
            Command.KillWordLeft,
            Command.KillWordRight,
            Command.Left,
            Command.Right,
            Command.LeftStart,
            Command.RightEnd,
            Command.WordLeft,
            Command.WordRight,
            Command.SelectAll
        })
            AddCommand(command, () => true);
        AddCommand(Command.DeleteCharLeft, () => RemoveLast());
        AddCommand(Command.DeleteCharRight, () => RemoveLast());
        AddCommand(Command.DeleteAll, () => ClearInput());
    }

    internal event EventHandler? InputRejected;

    internal event EventHandler? SensitiveCommandBlocked;
    internal event EventHandler? SensitiveInputChanged;

    internal bool IsRevealed { get; private set; }

    internal void SetRevealed(bool revealed)
    {
        IsRevealed = revealed;
        Secret = !revealed;
        RefreshMask();
    }

    internal bool TryCopy(IClipboard? clipboard)
    {
        if (_secret.Count == 0 || clipboard is FakeClipboard || clipboard?.IsSupported != true) return false;
        try { return clipboard.TrySetClipboardData(_secret.CopyText()); }
        catch (Exception exception) when (exception is NotSupportedException or IOException
            or System.ComponentModel.Win32Exception or InvalidOperationException)
        { return false; }
    }

    internal bool TryReplaceSensitiveInput(string value)
    {
        if (!_secret.TryReplace(value))
            return false;
        IsRevealed = false;
        Secret = true;
        RefreshMask();
        SensitiveInputChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    protected override bool OnKeyDownNotHandled(Key key)
    {
        if (ReadOnly) return true;
        if (key.IsCtrl || key.IsAlt || string.IsNullOrEmpty(key.AsGrapheme))
            return true;
        if (_secret.TryAppend(key.AsGrapheme))
        {
            RefreshMask();
            SensitiveInputChanged?.Invoke(this, EventArgs.Empty);
        }
        else
            InputRejected?.Invoke(this, EventArgs.Empty);
        return true;
    }

    private bool BlockSensitiveCommand()
    {
        SensitiveCommandBlocked?.Invoke(this, EventArgs.Empty);
        return true;
    }

    private bool RemoveLast()
    {
        if (ReadOnly) return true;
        int previousCount = _secret.Count;
        _secret.RemoveLast();
        RefreshMask();
        if (_secret.Count != previousCount) SensitiveInputChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    private bool ClearInput()
    {
        if (ReadOnly) return true;
        ClearSensitiveState();
        return true;
    }

    private void RefreshMask()
    {
        Text = IsRevealed ? new string(_secret.CopyText().Select(character =>
            char.IsControl(character) ? ' ' : character).ToArray()) : new string('●', _secret.Count);
        ClearHistoryChanges();
    }

    internal void ClearSensitiveState()
    {
        bool changed = _secret.Count > 0;
        _secret.Clear();
        IsRevealed = false;
        Secret = true;
        Text = string.Empty;
        ClearHistoryChanges();
        if (changed) SensitiveInputChanged?.Invoke(this, EventArgs.Empty);
    }
}
