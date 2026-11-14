namespace Configurator.Terminal;

using System.Collections.ObjectModel;
using Configurator.Core.Definitions;
using Configurator.Presentation;
using global::Terminal.Gui.App;
using global::Terminal.Gui.ViewBase;
using global::Terminal.Gui.Views;

/// <summary>Centered search and filter dialog backed by DefinitionDiscovery.Search without exposing answers.</summary>
internal sealed class TerminalDiscoveryDialog : Dialog
{
    private readonly DefinitionFormModel _form;
    private readonly IApplication? _application;
    private readonly TextField _queryField;
    private readonly Button _filterButton;
    private readonly ListView _resultsList;
    private readonly Button _jump;
    private readonly Button _cancel;
    private DefinitionDiscoveryFilter _filter = DefinitionDiscoveryFilter.All;
    private IReadOnlyList<DefinitionDiscoveryMatch> _matches = [];

    internal TerminalDiscoveryDialog(DefinitionFormModel form, IApplication? application = null)
    {
        _form = form;
        _application = application;
        Title = TerminalStrings.Get("DiscoveryTitle", form.Locale);
        X = Pos.Center();
        Y = Pos.Center();
        Width = 70;
        Height = 18;

        _queryField = new TextField
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(22),
            Height = 1
        };
        _filterButton = new Button
        {
            X = Pos.Right(_queryField) + 1,
            Y = 0,
            Width = 20,
            Text = $"{TerminalStrings.Get("DialogFilter", form.Locale)}: {_filter}"
        };
        _filterButton.Accepting += (_, args) =>
        {
            args.Handled = true;
            CycleFilter();
        };
        _resultsList = new ListView
        {
            X = 0,
            Y = 2,
            Width = Dim.Fill(),
            Height = Dim.Fill(3),
            CanFocus = true
        };
        _queryField.ValueChanged += (_, _) => SetSearch(_queryField.Text, _filter);
        _resultsList.Accepting += (_, args) =>
        {
            args.Handled = true;
            if (_resultsList.SelectedItem is { } selected) ActivateMatch(selected);
        };

        _jump = new Button { Text = TerminalStrings.Get("DialogJump", form.Locale), IsDefault = true };
        _jump.Accepting += (_, args) =>
        {
            args.Handled = true;
            ActivateMatch(_resultsList.SelectedItem ?? 0);
        };
        _cancel = new Button { Text = TerminalStrings.Get("DialogCancel", form.Locale) };
        _cancel.Accepting += (_, args) =>
        {
            args.Handled = true;
            SelectedPath = null;
            _application?.RequestStop(this);
        };

        Add(_queryField, _filterButton, _resultsList);
        AddButton(_jump);
        AddButton(_cancel);
        SetSearch(string.Empty, DefinitionDiscoveryFilter.All);
    }

    internal IReadOnlyList<DefinitionDiscoveryMatch> Matches => _matches;
    internal DefinitionPath? SelectedPath { get; private set; }

    internal void SetSearch(string query, DefinitionDiscoveryFilter filter = DefinitionDiscoveryFilter.All)
    {
        _filter = filter;
        _filterButton.Text = $"{TerminalStrings.Get("DialogFilter", _form.Locale)}: {_filter}";
        string bounded = query.Length > 256 ? query[..256] : query;
        DefinitionDiscoverySnapshot snapshot = DefinitionDiscovery.Search(
            _form.Projection.Entries,
            new DefinitionDiscoverySelection(bounded, includeInactive: false, filter: filter));
        _matches = snapshot.Matches;

        string[] items = _matches.Select(match =>
        {
            DefinitionDiscoveryEntry e = match.Entry;
            string flag = e.NeedsAttention ? "[!]" : e.IsRequired && !e.IsReady ? "[*]" : "[ ]";
            string raw = $"{flag} {e.SectionTitle} / {e.Title}";
            return new string(raw.Select(ch => char.IsControl(ch) ? ' ' : ch).ToArray());
        }).ToArray();
        _resultsList.SetSource(new ObservableCollection<string>(items));
        if (items.Length > 0) _resultsList.SelectedItem = 0;
    }

    internal bool ActivateMatch(int index)
    {
        if (index < 0 || index >= _matches.Count) return false;
        DefinitionDiscoveryEntry entry = _matches[index].Entry;
        DefinitionPath literal = DefinitionPath.FromSegments([entry.FieldId, .. entry.RelativePath]);
        if (!_form.FocusEditor(literal, preserveDiscovery: true)) return false;
        SelectedPath = literal;
        _application?.RequestStop(this);
        return true;
    }

    private void CycleFilter()
    {
        DefinitionDiscoveryFilter[] all = Enum.GetValues<DefinitionDiscoveryFilter>();
        int next = (Array.IndexOf(all, _filter) + 1) % all.Length;
        SetSearch(_queryField.Text, all[next]);
    }
}
