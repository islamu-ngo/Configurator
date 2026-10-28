namespace Configurator.Desktop.ViewModels;

using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Configurator.Core.Definitions;
using Configurator.Desktop.Messages;
using Configurator.Desktop.ViewModels.Fields;
using Configurator.Presentation;

/// <summary>Owns the editable Presentation form and projects its fields without storing duplicate answers.</summary>
public sealed partial class FormWorkspaceViewModel : ObservableObject, IDisposable
{
    private readonly IMessenger _messenger;
    private readonly FormFieldFactory _factory = new();
    private DefinitionFormModel? _form;
    private IReadOnlyList<FieldViewModel> _fields = [];
    private IReadOnlyList<DefinitionFormExperience> _experiences = [];
    private string? _experienceLocale;
    private bool _disposed;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ValidateCommand))]
    private bool _isBusy;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FlowDirection))]
    private bool _isRightToLeft;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FlowDirection))]
    private bool _followLocaleDirection = true;
    partial void OnIsRightToLeftChanged(bool value) => FollowLocaleDirection = false;

    public FormWorkspaceViewModel(IMessenger messenger)
    {
        _messenger = messenger;
        ValidateCommand = new RelayCommand(() => Validate(), () => CanValidate);
        messenger.Register<FormWorkspaceViewModel, DefinitionClearedMessage>(this,
            static (workspace, _) => workspace.Replace(null));
        messenger.Register<FormWorkspaceViewModel, DefinitionLoadedMessage>(this,
            static (workspace, message) => workspace.Replace(new DefinitionFormModel(message.Definition)));
        messenger.Register<FormWorkspaceViewModel, OperationStateMessage>(this,
            static (workspace, message) => workspace.IsBusy = message.IsBusy);
        messenger.Register<FormWorkspaceViewModel, FormValidationRequestedMessage>(this,
            static (workspace, message) => message.Reply(workspace.Validate()));
        messenger.Register<FormWorkspaceViewModel, FormFocusRequestedMessage>(this,
            static (workspace, _) => workspace.FirstVisible?.RequestFocus());
    }

    public DefinitionFormModel? Form => _form;
    public IReadOnlyList<FieldViewModel> Fields => _fields;
    public IReadOnlyList<DefinitionFormExperience> Experiences => _experiences;
    public IReadOnlyList<string> Locales => Form?.Definition.Locales ?? [];
    public string LocaleLabel => Configurator.Desktop.Resources.DesktopActionText.LocaleLabel;
    public string FollowLocaleDirectionLabel => Configurator.Desktop.Resources.DesktopActionText.FollowLocaleDirectionLabel;
    public string? SelectedLocale
    {
        get => Form?.Locale;
        set { if (!_disposed && value is not null) Form?.SelectLocale(value); }
    }
    public DefinitionFormExperience? SelectedExperience
    {
        get => Experiences.FirstOrDefault(experience => experience.Id == Form?.SelectedExperienceId);
        set
        {
            if (!_disposed && value is not null) Form?.SelectExperience(value.Id);
        }
    }
    public bool OptionalEditorsExpanded
    {
        get => Form?.OptionalEditorsExpanded ?? false;
        set
        {
            if (!_disposed) Form?.SetAllSectionsExpanded(value);
        }
    }
    public FlowDirection FlowDirection => (FollowLocaleDirection ? Form?.Direction == "rtl" : IsRightToLeft)
        ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
    public FieldViewModel? FirstError => Fields.FirstOrDefault(item => item.IsVisible && item.HasError);
    public FieldViewModel? FirstVisible => Fields.FirstOrDefault(item => item.IsVisible && item.IsRequired)
        ?? Fields.FirstOrDefault(item => item.IsVisible);
    public bool CanValidate => !_disposed && !IsBusy && Form is not null;
    public IRelayCommand ValidateCommand { get; }

    private void Replace(DefinitionFormModel? form)
    {
        if (_form is not null)
        {
            _form.InputChanged -= InputChanged;
            _form.ViewChanged -= ViewChanged;
        }
        _factory.Dispose();
        _form?.Dispose();
        _form = form;
        _experiences = form?.DisplayExperiences ?? [];
        _experienceLocale = form?.Locale;
        if (form is not null)
        {
            _fields = _factory.Create(form);
            form.InputChanged += InputChanged;
            form.ViewChanged += ViewChanged;
        }
        else _fields = [];
        OnPropertyChanged(nameof(Form));
        OnPropertyChanged(nameof(Fields));
        OnPropertyChanged(nameof(Experiences));
        OnPropertyChanged(nameof(SelectedExperience));
        OnPropertyChanged(nameof(OptionalEditorsExpanded));
        OnPropertyChanged(nameof(Locales));
        OnPropertyChanged(nameof(SelectedLocale));
        OnPropertyChanged(nameof(FlowDirection));
        NotifyFieldState();
        ValidateCommand.NotifyCanExecuteChanged();
        _messenger.Send(new FormWorkspaceChangedMessage(form));
    }

    private void InputChanged(object? sender, EventArgs args)
    {
        _messenger.Send(new FormInputsChangedMessage());
        NotifyFieldState();
    }

    private void NotifyFieldState()
    {
        OnPropertyChanged(nameof(FirstError));
        OnPropertyChanged(nameof(FirstVisible));
    }

    private void ViewChanged(object? sender, EventArgs args)
    {
        if (_experienceLocale != Form?.Locale)
        {
            _experienceLocale = Form?.Locale;
            _experiences = Form?.DisplayExperiences ?? [];
            OnPropertyChanged(nameof(Experiences));
        }
        OnPropertyChanged(nameof(SelectedLocale));
        OnPropertyChanged(nameof(FlowDirection));
        OnPropertyChanged(nameof(SelectedExperience));
        OnPropertyChanged(nameof(OptionalEditorsExpanded));
        NotifyFieldState();
    }

    public bool Validate()
    {
        if (_disposed || IsBusy) return false;
        if (Form is null)
        {
            _messenger.Send(new StatusChangedMessage("definition-required"));
            return false;
        }
        try
        {
            DefinitionValidationResult result = Form.Validate();
            bool ready = result.IsReady && !Form.HasInputErrors;
            NotifyFieldState();
            _messenger.Send(new StatusChangedMessage(ready ? "validation-ready" : "validation-invalid:" +
                string.Join(",", result.Diagnostics.Select(item => $"{item.FieldId}:{item.Code}")
                    .Concat(Form.Fields.Where(item => item.InputDiagnosticCode is not null || !item.TryValue(out _))
                        .Select(item => $"{item.Definition.Id}:input-invalid")))));
            if (!ready) FirstError?.RequestFocus();
            return ready;
        }
        catch (SetupDefinitionException exception)
        {
            NotifyFieldState();
            FirstError?.RequestFocus();
            _messenger.Send(new StatusChangedMessage($"validation-invalid:{exception.Code}"));
            return false;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Replace(null);
        _messenger.UnregisterAll(this);
    }

    public override string ToString() => nameof(FormWorkspaceViewModel);
}
