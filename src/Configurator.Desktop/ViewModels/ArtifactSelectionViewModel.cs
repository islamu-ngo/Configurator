namespace Configurator.Desktop.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using Configurator.Desktop.Resources;

/// <summary>Represents one declared bundle selection; the publisher owns its membership.</summary>
public sealed class ArtifactSelectionViewModel(string id, string title, string locale, bool selected, Action<bool> select)
    : ObservableObject
{
    private bool _isSelected = selected;
    private DesktopStrings _strings = new(locale);
    public string Id { get; } = id;
    public string Title { get; } = title;
    public string AutomationName => _strings.Format("IncludeArtifact", Title);
    internal void SetLocale(string locale)
    {
        _strings = new(locale);
        OnPropertyChanged(nameof(AutomationName));
    }
    public bool IsSelected
    {
        get => _isSelected;
        set { if (SetProperty(ref _isSelected, value)) select(value); }
    }
    internal void SetSelected(bool value) => SetProperty(ref _isSelected, value, nameof(IsSelected));
}
