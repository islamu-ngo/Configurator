namespace Configurator.Terminal;

using System.Collections.ObjectModel;
using Configurator.Presentation;
using global::Terminal.Gui.App;
using global::Terminal.Gui.ViewBase;
using global::Terminal.Gui.Views;

/// <summary>Centered keyboard-navigable choice selector surfacing display labels, wire tokens, explanations and deprecations.</summary>
internal sealed class TerminalChoiceDialog : Dialog
{
    private readonly IReadOnlyList<DefinitionChoicePresentation> _choices;
    private readonly string _locale;
    private readonly ListView _list;
    private readonly Label _detail;
    private readonly Button _apply;
    private readonly Button _cancel;
    private int _highlightedIndex;

    internal TerminalChoiceDialog(IReadOnlyList<DefinitionChoicePresentation> choices, string locale,
        string? currentWireToken = null, IApplication? application = null)
    {
        _choices = choices;
        _locale = locale;
        Title = TerminalStrings.Get("ChoiceDialogTitle", locale);
        X = Pos.Center();
        Y = Pos.Center();
        Width = 68;
        Height = 16;

        _list = new ListView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(4),
            CanFocus = true
        };
        string[] lines = choices.Select(choice => FormatOptionLine(choice, locale)).ToArray();
        _list.SetSource(new ObservableCollection<string>(lines));

        _detail = new Label
        {
            X = 0,
            Y = Pos.Bottom(_list),
            Width = Dim.Fill(),
            Height = 2
        };

        int initialIndex = currentWireToken is null
            ? 0
            : Math.Max(0, choices.ToList().FindIndex(choice =>
                string.Equals(choice.WireToken, currentWireToken, StringComparison.Ordinal)));
        if (lines.Length > 0)
        {
            _list.SelectedItem = initialIndex;
            _highlightedIndex = initialIndex;
        }
        UpdateDetail();

        _list.ValueChanged += (_, _) =>
        {
            if (_list.SelectedItem is { } selected && selected >= 0 && selected < _choices.Count)
            {
                _highlightedIndex = selected;
                UpdateDetail();
            }
        };
        _list.Accepting += (_, args) =>
        {
            args.Handled = true;
            if (_list.SelectedItem is { } selected) SelectIndex(selected);
            if (ConfirmSelection()) application?.RequestStop(this);
        };

        _apply = new Button { Text = TerminalStrings.Get("DialogApply", locale), IsDefault = true };
        _apply.Accepting += (_, args) =>
        {
            args.Handled = true;
            if (ConfirmSelection()) application?.RequestStop(this);
        };
        _cancel = new Button { Text = TerminalStrings.Get("DialogCancel", locale) };
        _cancel.Accepting += (_, args) =>
        {
            args.Handled = true;
            SelectedChoice = null;
            application?.RequestStop(this);
        };

        Add(_list, _detail);
        AddButton(_apply);
        AddButton(_cancel);
    }

    internal DefinitionChoicePresentation? SelectedChoice { get; private set; }

    internal bool SelectIndex(int index)
    {
        if (index < 0 || index >= _choices.Count) return false;
        _highlightedIndex = index;
        _list.SelectedItem = index;
        UpdateDetail();
        return true;
    }

    internal bool ConfirmSelection()
    {
        if (_highlightedIndex < 0 || _highlightedIndex >= _choices.Count) return false;
        SelectedChoice = _choices[_highlightedIndex];
        return true;
    }

    private void UpdateDetail()
    {
        if (_highlightedIndex < 0 || _highlightedIndex >= _choices.Count)
        {
            _detail.Text = string.Empty;
            return;
        }
        DefinitionChoicePresentation item = _choices[_highlightedIndex];
        var parts = new List<string>();
        if (item.HasCopyableWireToken && !string.IsNullOrEmpty(item.WireToken) &&
            !string.Equals(item.WireToken, item.DisplayLabel, StringComparison.Ordinal))
            parts.Add($"[{item.WireToken}]");
        if (item.Explanation is { Length: > 0 } exp) parts.Add(exp);
        if (item.IsDeprecated)
            parts.Add(item.DeprecationGuidance is { Length: > 0 } dep
                ? TerminalStrings.Get("DeprecatedBadge", _locale) + ": " + dep
                : TerminalStrings.Get("DeprecatedBadge", _locale));
        _detail.Text = new string(string.Join(" — ", parts)
            .Select(ch => char.IsControl(ch) ? ' ' : ch).ToArray());
    }

    private static string FormatOptionLine(DefinitionChoicePresentation choice, string locale)
    {
        string text = choice.Label;
        if (choice.HasCopyableWireToken && !string.IsNullOrEmpty(choice.WireToken) &&
            !string.Equals(choice.WireToken, text, StringComparison.Ordinal))
            text += $" ({choice.WireToken})";
        if (choice.IsDeprecated)
            text += " [" + TerminalStrings.Get("DeprecatedBadge", locale) + "]";
        return new(text.Select(ch => char.IsControl(ch) ? ' ' : ch).ToArray());
    }
}
