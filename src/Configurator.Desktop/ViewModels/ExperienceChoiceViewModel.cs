namespace Configurator.Desktop.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;

/// <summary>Keeps selection identity stable while a workspace refreshes translated labels.</summary>
public sealed class ExperienceChoiceViewModel(string id, string title) : ObservableObject
{
    private string _title = title;
    public string Id { get; } = id;
    public string Title => _title;
    internal void RefreshTitle(string title) => SetProperty(ref _title, title, nameof(Title));
}
