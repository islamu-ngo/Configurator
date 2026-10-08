namespace Configurator.DependencyProbes.CommunityToolkit;

using global::CommunityToolkit.Mvvm.ComponentModel;
using global::CommunityToolkit.Mvvm.Input;

internal sealed partial class ProbeViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isReady;

    [RelayCommand]
    private void ToggleReady() => IsReady = !IsReady;
}
