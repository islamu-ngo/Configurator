namespace Configurator.Desktop.Views;

using System.ComponentModel;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Configurator.Core.Definitions;
using Configurator.Desktop.ViewModels;
using Configurator.Presentation;

public sealed partial class MainWindow : Window
{
    private SetupDesktopShellViewModel? _shell;
    private DefinitionFormModel? _form;
    private readonly List<(DefinitionFieldInput Input, FieldEditor Editor)> _editors = [];

    public MainWindow()
    {
        AvaloniaXamlLoader.Load(this);
        DataContextChanged += (_, _) => Attach();
        KeyDown += ShortcutPressed;
        Opened += (_, _) => this.GetControl<TextBox>("DefinitionPath").Focus();
        Closed += (_, _) => Detach();
    }

    private void Attach()
    {
        Detach();
        _shell = DataContext as SetupDesktopShellViewModel;
        if (_shell is not null)
        {
            _shell.PropertyChanged += ShellChanged;
            this.GetControl<ComboBox>("DisplayMode").SelectedIndex = (int)_shell.DisplayMode;
        }
        BuildFields();
    }
    private void Detach()
    {
        if (_shell is not null) _shell.PropertyChanged -= ShellChanged;
        if (_form is not null) _form.InputChanged -= InputsChanged;
        _shell = null;
        _form = null;
    }
    private void ShellChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(SetupDesktopShellViewModel.Form)) BuildFields();
        if (args.PropertyName == nameof(SetupDesktopShellViewModel.DisplayMode)) RefreshFields();
    }
    private void BuildFields()
    {
        if (_form is not null) _form.InputChanged -= InputsChanged;
        _form = _shell?.Form;
        _editors.Clear();
        var panel = this.GetControl<StackPanel>("Fields");
        panel.Children.Clear();
        if (_form is not null)
        {
            foreach (var input in _form.Fields)
            {
                input.TryValue(out var initial);
                var editor = new FieldEditor(input.Definition.Constraints, input.RequiresMasking, initial)
                { Name = "Field_" + input.Definition.Id.Replace('.', '_') };
                editor.Changed += (_, _) => input.Text = editor.AnswerText;
                var clear = new Button { Content = "_Clear field" };
                clear.Click += (_, _) =>
                {
                    editor.Clear();
                    input.Text = string.Empty;
                    editor.FocusInput();
                };
                editor.Children.Add(clear);
                _editors.Add((input, editor));
                panel.Children.Add(editor);
            }
            _form.InputChanged += InputsChanged;
        }
        var artifacts = this.GetControl<ComboBox>("Artifact");
        artifacts.ItemsSource = _shell?.Artifacts;
        artifacts.SelectedItem = _shell?.Artifacts.FirstOrDefault(item => item.Id == _shell.ArtifactId);
        var selections = this.GetControl<WrapPanel>("ArtifactSelections");
        selections.Children.Clear();
        if (_shell is not null)
            foreach (SetupArtifactDefinition artifact in _shell.Artifacts)
            {
                var choice = new CheckBox
                {
                    Content = artifact.Id,
                    IsChecked = _shell.SelectedArtifactIds.Contains(artifact.Id)
                };
                AutomationProperties.SetName(choice, $"Include {artifact.Id} in bundle");
                choice.Click += (_, _) => _shell.SelectArtifact(artifact.Id, choice.IsChecked == true);
                selections.Children.Add(choice);
            }
        RefreshFields();
    }
    private void InputsChanged(object? sender, EventArgs args) => RefreshFields();
    private void RefreshFields()
    {
        if (_form is null || _shell is null) return;
        try
        {
            var validation = _form.Validate();
            foreach (var (input, editor) in _editors)
            {
                var state = validation.Fields.Single(item => item.Id == input.Definition.Id);
                bool invalid = !input.TryValue(out _);
                bool essential = state.IsRequired || state.HasError || invalid ||
                    input.RequiresMasking || input.Definition.Consequence is not null ||
                    input.Definition.Disclosure == SetupFieldDisclosure.Essential;
                editor.IsVisible = state.IsActive && (_shell.DisplayMode switch
                {
                    DesktopDisplayMode.Essential => essential,
                    DesktopDisplayMode.Guided => state.IsVisible(SetupDisplayMode.Guided) || invalid,
                    _ => true
                });
                editor.SetError(state.HasError || invalid, state.IsRequired);
            }
        }
        catch (SetupDefinitionException)
        {
            foreach (var (_, editor) in _editors) editor.SetError(true, false);
        }
    }
    private void FocusFirstError()
    {
        _editors.FirstOrDefault(item => item.Editor.IsVisible && item.Editor.HasError).Editor?.FocusInput();
    }
    private async void LoadClicked(object? sender, RoutedEventArgs args)
    {
        if (_shell is not null && await _shell.LoadDefinitionAsync())
            _editors.FirstOrDefault(item => item.Editor.IsVisible).Editor?.FocusInput();
    }
    private void ValidateClicked(object? sender, RoutedEventArgs args)
    {
        _shell?.Validate();
        RefreshFields();
        FocusFirstError();
    }
    private async void PrepareClicked(object? sender, RoutedEventArgs args)
    {
        if (_shell is null) return;
        if (!await _shell.PrepareAsync()) FocusFirstError();
    }
    private async void SaveClicked(object? sender, RoutedEventArgs args)
    {
        if (_shell is not null) await _shell.SaveAsync();
    }
    private async void ImportClicked(object? sender, RoutedEventArgs args)
    {
        if (_shell is null) return;
        if (await _shell.ImportFileAsync())
            _editors.FirstOrDefault(item => item.Editor.IsVisible).Editor?.FocusInput();
    }
    private void PrepareBundleClicked(object? sender, RoutedEventArgs args)
    {
        if (_shell is not null && !_shell.PrepareBundle()) FocusFirstError();
    }
    private async void SaveBundleClicked(object? sender, RoutedEventArgs args)
    {
        if (_shell is not null) await _shell.SaveBundleAsync();
    }
    private void CancelClicked(object? sender, RoutedEventArgs args) => _shell?.Cancel();
    private void ArtifactChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (_shell is not null && sender is ComboBox { SelectedItem: SetupArtifactDefinition artifact })
            _shell.ArtifactId = artifact.Id;
    }
    private void DisplayModeChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (_shell is not null && sender is ComboBox { SelectedIndex: >= 0 } combo)
            _shell.DisplayMode = (DesktopDisplayMode)combo.SelectedIndex;
    }
    private void DirectionClicked(object? sender, RoutedEventArgs args) =>
        FlowDirection = sender is CheckBox { IsChecked: true } ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
    private async void ShortcutPressed(object? sender, KeyEventArgs args)
    {
        if (args.Key == Key.O && args.KeyModifiers == KeyModifiers.Control)
        {
            this.GetControl<TextBox>("DefinitionPath").Focus();
            args.Handled = true;
        }
        else if (args.Key == Key.Enter && args.KeyModifiers == KeyModifiers.Control && _shell is not null)
        {
            args.Handled = true;
            if (!await _shell.PrepareAsync()) FocusFirstError();
        }
        else if (args.Key == Key.Escape)
        {
            _shell?.Cancel();
            args.Handled = true;
        }
    }
}
