namespace Configurator.Desktop.ViewModels.Fields;

using CommunityToolkit.Mvvm.ComponentModel;

public sealed class EnumChoice : ObservableObject
{
    private string _label;
    internal EnumChoice(int index, string label) { Index = index; _label = label; }
    public int Index { get; }
    public string Label => _label;
    internal void UpdateLabel(string label) => SetProperty(ref _label, label, nameof(Label));
}
