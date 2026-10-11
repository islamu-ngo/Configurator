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
    private bool _disposed;
    private static readonly IReadOnlyList<DesktopDisplayMode> Modes = Array.AsReadOnly(Enum.GetValues<DesktopDisplayMode>());

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ValidateCommand))]
    private bool _isBusy;
    [ObservableProperty]
    private DesktopDisplayMode _displayMode = DesktopDisplayMode.Guided;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FlowDirection))]
    private bool _isRightToLeft;

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
    public IReadOnlyList<DesktopDisplayMode> DisplayModes => Modes;
    public FlowDirection FlowDirection => IsRightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
    public FieldViewModel? FirstError => Fields.FirstOrDefault(item => item.IsVisible && item.HasError);
    public FieldViewModel? FirstVisible => Fields.FirstOrDefault(item => item.IsVisible);
    public bool CanValidate => !_disposed && !IsBusy && Form is not null;
    public IRelayCommand ValidateCommand { get; }

    partial void OnDisplayModeChanged(DesktopDisplayMode value)
    {
        if (Form is not null)
            Form.DisplayMode = value == DesktopDisplayMode.Advanced ? SetupDisplayMode.Advanced : SetupDisplayMode.Guided;
        RefreshFields();
    }

    private void Replace(DefinitionFormModel? form)
    {
        if (_form is not null) _form.InputChanged -= InputChanged;
        foreach (FieldViewModel field in _fields) field.Dispose();
        _form?.Dispose();
        _form = form;
        if (form is not null)
        {
            form.DisplayMode = DisplayMode == DesktopDisplayMode.Advanced ? SetupDisplayMode.Advanced : SetupDisplayMode.Guided;
            _fields = _factory.Create(form, DisplayMode);
            form.InputChanged += InputChanged;
        }
        else _fields = [];
        OnPropertyChanged(nameof(Form));
        OnPropertyChanged(nameof(Fields));
        RefreshFields();
        ValidateCommand.NotifyCanExecuteChanged();
        _messenger.Send(new FormWorkspaceChangedMessage(form));
    }

    private void InputChanged(object? sender, EventArgs args)
    {
        _messenger.Send(new FormInputsChangedMessage());
        RefreshFields();
    }

    private void RefreshFields()
    {
        foreach (FieldViewModel field in _fields) field.Refresh(DisplayMode);
        OnPropertyChanged(nameof(FirstError));
        OnPropertyChanged(nameof(FirstVisible));
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
            RefreshFields();
            _messenger.Send(new StatusChangedMessage(ready ? "validation-ready" : "validation-invalid:" +
                string.Join(",", result.Diagnostics.Select(item => $"{item.FieldId}:{item.Code}")
                    .Concat(Form.Fields.Where(item => !item.TryValue(out _))
                        .Select(item => $"{item.Definition.Id}:input-invalid")))));
            if (!ready) FirstError?.RequestFocus();
            return ready;
        }
        catch (SetupDefinitionException exception)
        {
            RefreshFields();
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
