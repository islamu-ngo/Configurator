namespace Configurator.Desktop.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;

public enum DesktopSettingsSectionId
{
    Appearance,
    LanguageAndDirection,
    KeyboardAndDialogs,
    PrivacyAndHost
}

public sealed partial class DesktopSettingsSection : ObservableObject
{
    public DesktopSettingsSection(DesktopSettingsSectionId id, string title, string description)
    {
        Id = id;
        _title = title;
        _description = description;
    }

    public DesktopSettingsSectionId Id { get; }

    [ObservableProperty]
    private string _title;

    [ObservableProperty]
    private string _description;

    public void Refresh(string title, string description)
    {
        Title = title;
        Description = description;
    }
}
