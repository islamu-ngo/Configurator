namespace Configurator.Terminal;

using System.Drawing;
using System.Text;
using global::Terminal.Gui.App;
using global::Terminal.Gui.Drawing;
using global::Terminal.Gui.Input;
using global::Terminal.Gui.ViewBase;
using global::Terminal.Gui.Views;

/// <summary>Modal keyboard shortcut reference accessible via '?' or 'F1'.</summary>
internal sealed class TerminalShortcutsDialog : Dialog
{
    private readonly View _body;
    private readonly Label _content;
    private readonly Button _close;

    internal TerminalShortcutsDialog(string locale, IApplication? application = null)
    {
        Title = TerminalStrings.Get("ShortcutsTitle", locale);
        X = Pos.Center();
        Y = Pos.Center();
        Width = Dim.Percent(85);
        Height = Dim.Percent(85);

        string reference = TerminalStrings.Get("ShortcutsReference", locale);
        int lineCount = Math.Max(1, reference.Split('\n').Length);

        _body = new View
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(2),
            CanFocus = true
        };
        _body.ViewportSettings |= ViewportSettingsFlags.HasVerticalScrollBar;
        _body.SetContentSize(new Size(68, lineCount + 1));

        _content = new Label
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Auto(DimAutoStyle.Text, 1),
            Text = reference,
            HotKeySpecifier = (Rune)0xffff
        };
        _body.Add(_content);

        _body.KeyDown += (_, key) =>
        {
            if (key == Key.CursorDown || key == new Key('j')) { _body.ScrollVertical(1); key.Handled = true; }
            else if (key == Key.CursorUp || key == new Key('k')) { _body.ScrollVertical(-1); key.Handled = true; }
            else if (key == Key.PageDown) { _body.ScrollVertical(Math.Max(1, _body.Viewport.Height - 1)); key.Handled = true; }
            else if (key == Key.PageUp) { _body.ScrollVertical(-Math.Max(1, _body.Viewport.Height - 1)); key.Handled = true; }
        };

        _close = new Button { Text = TerminalStrings.Get("Close", locale), IsDefault = true };
        _close.Accepting += (_, args) =>
        {
            args.Handled = true;
            application?.RequestStop(this);
        };

        Add(_body);
        AddButton(_close);
    }
}
