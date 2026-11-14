namespace Configurator.Terminal;

using System.Collections.ObjectModel;
using Configurator.Presentation;
using global::Terminal.Gui.App;
using global::Terminal.Gui.ViewBase;
using global::Terminal.Gui.Views;

/// <summary>Displays localized section progress, error/missing badges and jumps to any selected section.</summary>
internal sealed class TerminalSectionNavigator : FrameView
{
    private readonly DefinitionFormModel _form;
    private readonly ListView _list;
    private readonly List<SectionStatusProjection> _sections = [];
    private Action<string>? _onSectionSelected;

    internal TerminalSectionNavigator(DefinitionFormModel form, Action<string>? onSectionSelected = null)
    {
        _form = form;
        _onSectionSelected = onSectionSelected;
        Width = 32;
        Height = Dim.Fill(3);
        Title = TerminalStrings.Get("SectionNavigatorTitle", form.Locale);
        CanFocus = true;

        _list = new ListView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            CanFocus = true
        };
        _list.Accepting += (_, args) =>
        {
            args.Handled = true;
            if (_list.SelectedItem is { } selected) ActivateIndex(selected);
        };
        Add(_list);
        Refresh();
    }

    internal IReadOnlyList<SectionStatusProjection> Sections => _sections;

    internal void SetOnSectionSelected(Action<string> onSectionSelected) => _onSectionSelected = onSectionSelected;

    internal void Refresh()
    {
        Title = TerminalStrings.Get("SectionNavigatorTitle", _form.Locale);
        _sections.Clear();
        DefinitionFormProjection projection = _form.Projection;
        foreach (DefinitionFormSection section in projection.Sections)
        {
            if (projection.SectionStatuses.TryGetValue(section.Id, out SectionStatusProjection? status))
                _sections.Add(status);
        }

        string[] items = _sections.Select(status => FormatSectionLine(status, _form.Locale)).ToArray();
        _list.SetSource(new ObservableCollection<string>(items));

        string? currentSectionId = projection.Navigation.CurrentSectionId;
        int currentIndex = currentSectionId is null
            ? 0
            : _sections.FindIndex(item => string.Equals(item.SectionId, currentSectionId, StringComparison.Ordinal));
        if (currentIndex >= 0 && items.Length > 0)
            _list.SelectedItem = currentIndex;
    }

    internal bool ActivateIndex(int index)
    {
        if (index < 0 || index >= _sections.Count) return false;
        string sectionId = _sections[index].SectionId;
        _onSectionSelected?.Invoke(sectionId);
        return true;
    }

    internal static string FormatSectionLine(SectionStatusProjection status, string locale)
    {
        string icon = status.ConfigurationStatus == SectionConfigurationStatus.OutsideSelectedScope ? "[-]"
            : status.Progress.Invalid > 0 ? "[!]"
            : status.Progress.Missing > 0 || status.Progress.Deferred > 0 ? "[*]"
            : status.Coverage == SectionReviewCoverage.Reviewed || (status.Progress.Required > 0 && status.Progress.Ready == status.Progress.Required) ? "[v]"
            : status.Coverage == SectionReviewCoverage.Opened ? "[o]"
            : "[ ]";
        string counts = status.Progress.Required > 0
            ? string.Create(TerminalStrings.Culture(locale), $" ({status.Progress.Ready}/{status.Progress.Required})")
            : string.Empty;
        string optional = status.HasHiddenOptional
            ? string.Create(TerminalStrings.Culture(locale), $" +{status.AvailableOptionalCount}")
            : string.Empty;
        string cleanTitle = new(status.Title.Select(character => char.IsControl(character) ? ' ' : character).ToArray());
        return $"{icon} {cleanTitle}{counts}{optional}";
    }

    internal static string? ShowModal(IApplication application, DefinitionFormModel form, Action<string> onSectionSelected)
    {
        string? chosen = null;
        using var dialog = new Dialog
        {
            Title = TerminalStrings.Get("SectionNavigatorTitle", form.Locale),
            X = Pos.Center(),
            Y = Pos.Center(),
            Width = 54,
            Height = 16
        };
        using var navigator = new TerminalSectionNavigator(form, sectionId =>
        {
            chosen = sectionId;
            onSectionSelected(sectionId);
            application.RequestStop(dialog);
        });
        navigator.X = 0;
        navigator.Y = 0;
        navigator.Width = Dim.Fill();
        navigator.Height = Dim.Fill(2);

        using var jump = new Button { Text = TerminalStrings.Get("DialogJump", form.Locale), IsDefault = true };
        jump.Accepting += (_, args) =>
        {
            args.Handled = true;
            navigator.ActivateIndex(navigator._list.SelectedItem ?? -1);
        };
        using var cancel = new Button { Text = TerminalStrings.Get("DialogCancel", form.Locale) };
        cancel.Accepting += (_, args) =>
        {
            args.Handled = true;
            application.RequestStop(dialog);
        };
        dialog.Add(navigator);
        dialog.AddButton(jump);
        dialog.AddButton(cancel);
        application.Run(dialog);
        return chosen;
    }
}
