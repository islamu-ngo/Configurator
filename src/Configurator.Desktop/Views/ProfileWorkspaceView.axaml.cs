namespace Configurator.Desktop.Views;

using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Configurator.Desktop.ViewModels;
using Configurator.Presentation;

public sealed partial class ProfileWorkspaceView : UserControl
{
    public ProfileWorkspaceView() => AvaloniaXamlLoader.Load(this);

    private string Take(string name)
    {
        TextBox input = this.FindControl<TextBox>(name)!;
        string value = input.Text ?? string.Empty;
        input.Text = string.Empty;
        return value;
    }

    private async void OpenProfile(object? sender, RoutedEventArgs args)
    {
        Take("ProfileConfirmation");
        if (DataContext is DesktopShellViewModel shell)
            await shell.Profiles.OpenAsync(Take("ProfilePassphrase"));
        this.FindControl<TextBox>("ProfilePassphrase")?.Focus();
    }

    private async void SaveProfile(object? sender, RoutedEventArgs args)
    {
        string material = Take("ProfilePassphrase");
        string confirmation = Take("ProfileConfirmation");
        if (DataContext is DesktopShellViewModel shell) await shell.Profiles.SaveAsync(material, confirmation);
        this.FindControl<TextBox>("ProfilePassphrase")?.Focus();
    }

    private async void CompareProfile(object? sender, RoutedEventArgs args)
    {
        if (DataContext is DesktopShellViewModel shell) await shell.Profiles.CompareAsync();
    }

    private async void SaveTarget(object? sender, RoutedEventArgs args)
    {
        if (DataContext is DesktopShellViewModel shell && shell.Publisher.ArtifactId is { } artifact)
            await shell.Profiles.SaveTargetAsync(artifact, shell.Publisher.OutputDirectory, shell.Publisher.FileName);
    }

    private async void CheckRecovery(object? sender, RoutedEventArgs args)
    {
        Take("ProfileConfirmation");
        if (DataContext is DesktopShellViewModel shell)
            await shell.Profiles.CheckRecoveryAsync(Take("ProfilePassphrase"));
        this.FindControl<TextBox>("ProfilePassphrase")?.Focus();
    }

    private void ReplaceRetained(object? sender, RoutedEventArgs args)
    {
        CheckBox confirmation = this.FindControl<CheckBox>("ProfileConfirmReplacement")!;
        if (DataContext is DesktopShellViewModel shell)
            shell.Profiles.Replace(this.FindControl<ListBox>("ProfileRetainedSlots")?.SelectedItem as BlindSecretSlot,
                confirmation.IsChecked == true);
        confirmation.IsChecked = false;
        this.FindControl<ListBox>("ProfileRetainedSlots")?.Focus();
    }
}
