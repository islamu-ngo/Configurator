namespace Configurator.Desktop.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using Configurator.Core.Definitions;
using Configurator.Desktop.Resources;

/// <summary>Represents one eligible output choice; the publisher owns export membership.</summary>
public sealed class ArtifactSelectionViewModel(ArtifactDefinition artifact, string locale, bool selected,
    Action<bool> select, Func<string> preparationStatus)
    : ObservableObject
{
    private bool _isSelected = selected;
    private DesktopStrings _strings = new(locale);
    public string Id => artifact.Id;
    public string Title => artifact.Title;
    public string DeclaredFileName => artifact.FileName;
    public string Format => artifact.Format.ToToken();
    public string Consequences => _strings.Format("ArtifactConsequences", Format,
        artifact.ApplySemantics.ToToken(), artifact.Restart.ToToken());
    public string PreparationStatus => preparationStatus();
    public string AutomationName => _strings.Format("IncludeArtifact", Title);
    internal void SetLocale(string locale)
    {
        _strings = new(locale);
        OnPropertyChanged(nameof(AutomationName));
        OnPropertyChanged(nameof(Consequences));
        RefreshPreparation();
    }
    internal void RefreshPreparation() => OnPropertyChanged(nameof(PreparationStatus));
    public bool IsSelected
    {
        get => _isSelected;
        set { if (SetProperty(ref _isSelected, value)) select(value); }
    }
    internal void SetSelected(bool value) => SetProperty(ref _isSelected, value, nameof(IsSelected));
}
