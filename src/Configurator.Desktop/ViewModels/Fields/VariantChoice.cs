namespace Configurator.Desktop.ViewModels.Fields;

using CommunityToolkit.Mvvm.ComponentModel;

public sealed class VariantChoice : ObservableObject
{
    private string _title = "";
    internal VariantChoice(int index) => Index = index;
    public int Index { get; }
    public string Title => _title;
    internal void UpdateTitle(string title) => SetProperty(ref _title, title, nameof(Title));
}
