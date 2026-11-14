namespace Configurator.Desktop.ViewModels;

using System.ComponentModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Configurator.Core.Definitions;
using Configurator.Desktop.Messages;
using Configurator.Desktop.Services;
using Configurator.Desktop.ViewModels.Fields;
using Configurator.Presentation;

/// <summary>Owns the editable Presentation form and projects its fields without storing duplicate answers.</summary>
public sealed partial class FormWorkspaceViewModel : ObservableObject, IDisposable
{
    private readonly IMessenger _messenger;
    private readonly FormFieldFactory _factory;
    private readonly SynchronizationContext? _uiContext = SynchronizationContext.Current;
    private DefinitionFormModel? _form;
    private IReadOnlyList<FieldViewModel> _fields = [];
    private IReadOnlyList<ExperienceChoiceViewModel> _experiences = [];
    private IReadOnlyList<ConfigurationContextChoice> _configurationChoices = [];
    private WorkspaceDensity _density = WorkspaceDensity.Comfortable;
    private WorkspaceNamingMode _defaultNamingMode = WorkspaceNamingMode.Humanized;
    private string? _experienceLocale;
    private bool _disposed;
    private ExperienceChoiceViewModel[] _outputTasks = [];
    private DefinitionTransitionTransferPlan? _transferReview;
    private DesktopTransferCandidateViewModel[] _transferCandidates = [];
    private string _transferStatus = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ValidateCommand))]
    [NotifyCanExecuteChangedFor(nameof(ApplyTransferCommand))]
    [NotifyCanExecuteChangedFor(nameof(SwitchWithoutCopyCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelTransferCommand))]
    private bool _isBusy;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FlowDirection))]
    [NotifyPropertyChangedFor(nameof(DirectionModeIndex))]
    private bool _isRightToLeft;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FlowDirection))]
    [NotifyPropertyChangedFor(nameof(DirectionModeIndex))]
    private bool _followLocaleDirection = true;
    partial void OnIsRightToLeftChanged(bool value) => FollowLocaleDirection = false;

    public FormWorkspaceViewModel(
        IMessenger messenger,
        ISecretClipboardService? clipboard = null,
        ISecretGeneratorDialogService? secretGeneratorDialog = null)
    {
        _messenger = messenger;
        _factory = new FormFieldFactory(clipboard, secretGeneratorDialog);
        ValidateCommand = new RelayCommand(() => Validate(), () => CanValidate);
        UndoCommand = new RelayCommand(() => Undo(), () => CanUndo);
        RedoCommand = new RelayCommand(() => Redo(), () => CanRedo);
        ToggleNamingModeCommand = new RelayCommand(() => ToggleNamingMode(), () => !_disposed && Form is not null);
        ApplyTransferCommand = new RelayCommand(() => ApplyTransfer(null), static () => false);
        SwitchWithoutCopyCommand = new RelayCommand(() => ApplyTransfer(null, withoutCopy: true), static () => false);
        CancelTransferCommand = new RelayCommand(() => CancelTransfer(null), static () => false);
        messenger.Register<FormWorkspaceViewModel, WorkspaceInvalidatedMessage>(this,
            static (workspace, _) => workspace.RetireTransfer());
        messenger.Register<FormWorkspaceViewModel, DefinitionClearedMessage>(this,
            static (workspace, _) => workspace.Replace(null));
        messenger.Register<FormWorkspaceViewModel, DefinitionLoadedMessage>(this,
            static (workspace, message) =>
            {
                workspace.Replace(new DefinitionFormModel(message.Definition));
            });
        messenger.Register<FormWorkspaceViewModel, OperationStateMessage>(this,
            static (workspace, message) => workspace.IsBusy = message.IsBusy);
        messenger.Register<FormWorkspaceViewModel, FormValidationRequestedMessage>(this,
            static (workspace, message) => message.Reply(workspace.Validate()));
        messenger.Register<FormWorkspaceViewModel, FormFocusRequestedMessage>(this,
            static (workspace, _) => workspace.FirstVisible?.RequestFocus());
    }

    public DefinitionFormModel? Form => _form;
    internal event Action? TransferReviewRequested;
    public IReadOnlyList<ExperienceChoiceViewModel> OutputTasks => _outputTasks;
    public bool HasOutputTasks => OutputTasks.Count > 1;
    public ExperienceChoiceViewModel? OutputTask
    {
        get
        {
            if (Form?.Definition.Presentation?.OutputSelector is not { } selector) return null;
            Form.Validate().TryGetEffectiveValue(selector.Location, out var selected);
            return selected.ValueKind == System.Text.Json.JsonValueKind.String
                ? OutputTasks.FirstOrDefault(task => task.Id == selected.GetString()) : null;
        }
        set
        {
            if (!_disposed && !IsBusy && value is not null && OutputTasks.Contains(value) && value != OutputTask)
                ReviewOutputTask(value.Id);
        }
    }
    public IReadOnlyList<DesktopNextActionViewModel> ProposedNextActions
    {
        get
        {
            if (Form is not { } form) return [];
            long revision = form.InputRevision;
            return form.OutputWorkflow.ProposedNextActions.Select(action =>
                new DesktopNextActionViewModel(action.Title, action.Description, new RelayCommand(() =>
                {
                    if (ReferenceEquals(Form, form) && form.InputRevision == revision &&
                        form.OutputWorkflow.ProposedNextActions.Any(current => current.TargetOption == action.TargetOption))
                        ReviewOutputTask(action.TargetOption);
                }))).ToArray();
        }
    }
    public bool HasNextActions => ProposedNextActions.Count > 0;
    public string OutputStatus => string.Join("\n", Form?.OutputWorkflow.Tasks.Select(task =>
        (OutputTasks.FirstOrDefault(choice => choice.Id == task.Option)?.Title ?? string.Empty) + ": " +
        PresentationStrings.OutputStatus(task, Form.Locale)) ?? []);
    public bool HasTransferReview => _transferReview is not null;
    public IReadOnlyList<DesktopTransferCandidateViewModel> TransferCandidates => _transferCandidates;
    public string TransferStatus => _transferStatus;
    public string TransferApplyLabel => PresentationStrings.Get(
        _transferReview?.TargetOption is null ? "TransferApply" : "TransferApplyAndSwitch", Form?.Locale ?? "en");
    public bool CanSwitchWithoutCopy => _transferReview?.TargetOption is not null;
    public IRelayCommand ApplyTransferCommand { get; private set; }
    public IRelayCommand SwitchWithoutCopyCommand { get; private set; }
    public IRelayCommand CancelTransferCommand { get; private set; }

    private void ReviewOutputTask(string? option)
    {
        if (_disposed || IsBusy || Form is null || option is null) return;
        DefinitionTransitionTransferPlan review = Form.ReviewOutputTransition(option);
        BeginTransfer(review);
        if (TransferCandidates.Count == 0) ApplyTransfer(review, withoutCopy: true);
        OnPropertyChanged(nameof(OutputTask));
    }

    private void ReviewSuggestion(DefinitionPath path)
    {
        if (_disposed || IsBusy || Form is null) return;
        BeginTransfer(Form.ReviewSuggestion(path));
    }

    private void BeginTransfer(DefinitionTransitionTransferPlan review)
    {
        _transferReview = review;
        _transferStatus = string.Empty;
        ApplyTransferCommand = new RelayCommand(() => ApplyTransfer(review), () => !IsBusy && OwnsTransfer(review));
        SwitchWithoutCopyCommand = new RelayCommand(() => ApplyTransfer(review, withoutCopy: true),
            () => !IsBusy && review.TargetOption is not null && OwnsTransfer(review));
        CancelTransferCommand = new RelayCommand(() => CancelTransfer(review), () => OwnsTransfer(review));
        _transferCandidates = review.Candidates.Select(candidate =>
            new DesktopTransferCandidateViewModel(candidate, () =>
                _transferReview == review && Form is { } form
                    ? candidate.RequiresMasking ? PresentationStrings.Get("TransferMasked", form.Locale)
                        : form.ReadTransferPreview(review, candidate.Id)?.ToString()
                            ?? PresentationStrings.Get("TransferUnavailable", form.Locale)
                    : string.Empty,
                () => _transferReview == review && Form is { } form
                    ? (form.GetEditorPresentation(candidate.Source)?.DisplayLabel ?? candidate.SourceTitle) + " → " +
                        (form.GetEditorPresentation(candidate.Target)?.DisplayLabel ?? candidate.TargetTitle) +
                        (candidate.DestinationProvided ? " — " + PresentationStrings.Get("TransferOverwrite", form.Locale) : string.Empty)
                    : string.Empty))
            .ToArray();
        NotifyTransfer();
        if (_transferCandidates.Length > 0 && OwnsTransfer(review))
        {
            TransferReviewRequested?.Invoke();
            _messenger.Send(new FocusRequestedMessage("TransferApply"));
        }
    }

    private bool OwnsTransfer(DefinitionTransitionTransferPlan? review) =>
        !_disposed && Form is not null && review is not null && ReferenceEquals(review, _transferReview);

    private void ApplyTransfer(DefinitionTransitionTransferPlan? review, bool withoutCopy = false)
    {
        if (review is null || !OwnsTransfer(review) || IsBusy || Form is not { } owner) return;
        int[] selected = withoutCopy ? [] : TransferCandidates.Where(candidate => candidate.IsSelected).Select(candidate => candidate.Id).ToArray();
        _transferReview = null;
        _transferCandidates = [];
        DefinitionTransferResult result = owner.ApplyTransfer(review, selected);
        if (_disposed || !ReferenceEquals(owner, Form) || _transferReview is not null) return;
        _transferStatus = result.Succeeded ? string.Empty : PresentationStrings.Get("TransferStale", owner.Locale);
        NotifyTransfer();
        if (result.Succeeded && owner.InputRevision == result.InputRevision) FirstVisible?.RequestFocus();
    }

    private void CancelTransfer(DefinitionTransitionTransferPlan? review)
    {
        if (OwnsTransfer(review)) ClearTransfer();
    }

    private void ClearTransfer()
    {
        _transferReview = null;
        _transferCandidates = [];
        _transferStatus = string.Empty;
        NotifyTransfer();
        OnPropertyChanged(nameof(OutputTask));
    }

    private void RetireTransfer()
    {
        if (_transferReview is null) return;
        _transferReview = null;
        _transferCandidates = [];
        _transferStatus = PresentationStrings.Get("TransferStale", Form?.Locale ?? "en");
        NotifyTransfer();
    }

    private void NotifyTransfer()
    {
        OnPropertyChanged(nameof(ApplyTransferCommand));
        OnPropertyChanged(nameof(SwitchWithoutCopyCommand));
        OnPropertyChanged(nameof(CancelTransferCommand));
        ApplyTransferCommand.NotifyCanExecuteChanged();
        SwitchWithoutCopyCommand.NotifyCanExecuteChanged();
        CancelTransferCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(HasTransferReview));
        OnPropertyChanged(nameof(TransferCandidates));
        OnPropertyChanged(nameof(TransferApplyLabel));
        OnPropertyChanged(nameof(CanSwitchWithoutCopy));
        OnPropertyChanged(nameof(TransferStatus));
    }

    private void WorkflowChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(DefinitionFormModel.OutputWorkflow))
            RunOnUiContext(NotifyOutputWorkflow);
    }

    private void NotifyOutputWorkflow()
    {
        OnPropertyChanged(nameof(OutputTask));
        OnPropertyChanged(nameof(OutputStatus));
        OnPropertyChanged(nameof(ProposedNextActions));
        OnPropertyChanged(nameof(HasNextActions));
    }
    internal event Action<FieldViewModel>? EditorFocusRequested;
    public IReadOnlyList<FieldViewModel> Fields => _fields;
    public IReadOnlyList<ConfigurationContextChoice> ConfigurationChoices => _configurationChoices;
    public bool HasConfigurationContext => Form?.ConfigurationContext is not null;
    public ConfigurationContextChoice? SelectedConfigurationContext
    {
        get => ConfigurationChoices.FirstOrDefault(choice =>
            choice.RecipeId == Form?.ConfigurationContext?.RecipeId && choice.ScopeId == Form?.ConfigurationContext?.ScopeId);
        set
        {
            if (_disposed || value is null || value == SelectedConfigurationContext) return;
            if (!ConfigurationChoices.Contains(value) || Form?.SelectConfigurationContext(value.RecipeId, value.ScopeId) != true)
                OnPropertyChanged(nameof(SelectedConfigurationContext));
        }
    }
    public IReadOnlyList<ExperienceChoiceViewModel> Experiences => _experiences;
    public IReadOnlyList<string> Locales => Form?.Definition.Locales ?? [];
    public Configurator.Desktop.Resources.DesktopStrings Strings => new(Form?.Locale);
    public string LocaleLabel => Strings.LocaleLabel;
    public string FollowLocaleDirectionLabel => Strings.FollowLocaleDirectionLabel;
    public string? SelectedLocale
    {
        get => Form?.Locale;
        set { if (!_disposed && value is not null) Form?.SelectLocale(value); }
    }
    public ExperienceChoiceViewModel? SelectedExperience
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
    public FlowDirection FlowDirection => (FollowLocaleDirection
        ? Form is null ? Strings.Culture.TextInfo.IsRightToLeft : Form.Direction == "rtl"
        : IsRightToLeft)
        ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
    public int DirectionModeIndex
    {
        get => FollowLocaleDirection ? 0 : IsRightToLeft ? 2 : 1;
        set
        {
            if (_disposed || value is < 0 or > 2 || value == DirectionModeIndex) return;
            if (value == 0)
            {
                FollowLocaleDirection = true;
                return;
            }
            FollowLocaleDirection = false;
            IsRightToLeft = value == 2;
            OnPropertyChanged(nameof(DirectionModeIndex));
            OnPropertyChanged(nameof(FlowDirection));
        }
    }
    public FieldViewModel? FirstError => Fields.FirstOrDefault(item => item.IsVisible && item.HasError);
    public FieldViewModel? FirstVisible => Fields.FirstOrDefault(item => item.IsVisible && item.IsRequired)
        ?? Fields.FirstOrDefault(item => item.IsVisible);
    public bool CanValidate => !_disposed && !IsBusy && Form is not null;
    public IRelayCommand ValidateCommand { get; }
    public WorkspaceDensity Density => _density;
    public WorkspaceNamingMode DefaultNamingMode
    {
        get => _defaultNamingMode;
        internal set
        {
            if (_disposed || !Enum.IsDefined(value) || value == _defaultNamingMode) return;
            _defaultNamingMode = value;
            if (Form is not null) Form.SetWorkspaceNamingMode(value);
            NotifyPresentationState();
        }
    }
    public WorkspaceNamingMode NamingMode => Form?.PresentationOptions.NamingMode ?? _defaultNamingMode;
    public bool IsTechnicalNaming => NamingMode == WorkspaceNamingMode.Technical;
    public bool CanUndo => !_disposed && !IsBusy && Form?.CanUndo == true;
    public bool CanRedo => !_disposed && !IsBusy && Form?.CanRedo == true;
    public string UndoLabel => Form?.LastUndoLabel is { Length: > 0 } label
        ? $"{Strings["UndoLabel"]}: {label}" : Strings["UndoLabel"];
    public string RedoLabel => Form?.LastRedoLabel is { Length: > 0 } label
        ? $"{Strings["RedoLabel"]}: {label}" : Strings["RedoLabel"];
    public IRelayCommand UndoCommand { get; }
    public IRelayCommand RedoCommand { get; }
    public IRelayCommand ToggleNamingModeCommand { get; }

    internal void SetDensity(WorkspaceDensity density)
    {
        if (_disposed || !Enum.IsDefined(density) || _density == density) return;
        _density = density;
        Form?.SetWorkspaceDensity(density);
        OnPropertyChanged(nameof(Density));
    }

    internal void RunOnUiContext(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (_disposed) return;
        if (_uiContext is null || ReferenceEquals(SynchronizationContext.Current, _uiContext))
            action();
        else
            _uiContext.Post(_ => { if (!_disposed) action(); }, null);
    }

    public bool Undo() => CanUndo && Form!.Undo();
    public bool Redo() => CanRedo && Form!.Redo();

    public void ToggleNamingMode()
    {
        if (_disposed || Form is null) return;
        WorkspaceNamingMode next = IsTechnicalNaming
            ? WorkspaceNamingMode.Humanized
            : WorkspaceNamingMode.Technical;
        Form.SetWorkspaceNamingMode(next);
        NotifyPresentationState();
    }

    private void Replace(DefinitionFormModel? form)
    {
        if (_form is not null)
        {
            _form.InputChanged -= InputChanged;
            _form.ViewChanged -= ViewChanged;
            _form.PropertyChanged -= WorkflowChanged;
        }
        ClearTransfer();
        _factory.Dispose();
        _form?.Dispose();
        _form = form;
        if (form is not null)
        {
            form.SetWorkspaceDensity(_density);
            form.SetWorkspaceNamingMode(_defaultNamingMode);
        }
        _experiences = form?.DisplayExperiences.Select(experience =>
            new ExperienceChoiceViewModel(experience.Id, experience.Title)).ToArray() ?? [];
        _experienceLocale = form?.Locale;
        _outputTasks = form?.Definition.Presentation?.OutputSelector?.Options.Select(option =>
            new ExperienceChoiceViewModel(option.Value, option.Title)).ToArray() ?? [];
        OnPropertyChanged(nameof(OutputTasks));
        OnPropertyChanged(nameof(HasOutputTasks));
        if (form is not null)
        {
            _fields = _factory.Create(form);
            foreach (FieldViewModel field in _fields)
            {
                field.FocusRequested += RevealEditor;
                field.SuggestionReviewRequested = ReviewSuggestion;
            }
        }
        else _fields = [];
        // Notify ItemsSource lists and defer ViewChanged subscription until after Form replacement
        // so re-entrant section-opened notifications never fire Selected* while selectors still hold empty lists.
        OnPropertyChanged(nameof(Locales));
        OnPropertyChanged(nameof(Experiences));
        OnPropertyChanged(nameof(Form));
        OnPropertyChanged(nameof(Fields));
        OnPropertyChanged(nameof(SelectedExperience));
        OnPropertyChanged(nameof(OptionalEditorsExpanded));
        OnPropertyChanged(nameof(SelectedLocale));
        OnPropertyChanged(nameof(FlowDirection));
        NotifyFieldState();
        NotifyPresentationState();
        if (form is not null)
        {
            form.InputChanged += InputChanged;
            form.ViewChanged += ViewChanged;
            form.PropertyChanged += WorkflowChanged;
        }
        ValidateCommand.NotifyCanExecuteChanged();
        ToggleNamingModeCommand.NotifyCanExecuteChanged();
        _messenger.Send(new FormWorkspaceChangedMessage(form));
    }

    private void InputChanged(object? sender, EventArgs args)
    {
        RetireTransfer();
        _messenger.Send(new FormInputsChangedMessage());
        NotifyFieldState();
        NotifyPresentationState();
    }

    private void NotifyPresentationState()
    {
        OnPropertyChanged(nameof(NamingMode));
        OnPropertyChanged(nameof(IsTechnicalNaming));
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        OnPropertyChanged(nameof(UndoLabel));
        OnPropertyChanged(nameof(RedoLabel));
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
    }

    private void NotifyFieldState()
    {
        RefreshOutputTasks();
        RefreshConfigurationChoices();
        OnPropertyChanged(nameof(Strings));
        OnPropertyChanged(nameof(LocaleLabel));
        OnPropertyChanged(nameof(FollowLocaleDirectionLabel));
        OnPropertyChanged(nameof(FirstError));
        OnPropertyChanged(nameof(FirstVisible));
    }

    private void RefreshOutputTasks()
    {
        if (Form?.Definition.Presentation?.OutputSelector is { } selector)
            for (int index = 0; index < selector.Options.Count; index++)
                _outputTasks[index].RefreshTitle(Form.LocalizedText(
                    DefinitionPath.Parse($"/presentation/outputSelector/options/{index}/title"), selector.Options[index].Title)
                    ?? selector.Options[index].Title);
        NotifyOutputWorkflow();
    }

    private void RefreshConfigurationChoices()
    {
        ConfigurationContextChoice[] choices = Form?.ConfigurationRecipes.SelectMany((recipe, recipeIndex) =>
            recipe.Scopes.Select((scope, scopeIndex) => new ConfigurationContextChoice(recipe.Id, scope.Id,
                Form.LocalizedText(DefinitionPath.Parse($"/deployment/recipes/{recipeIndex}/title"), recipe.Title) + " · " +
                Form.LocalizedText(DefinitionPath.Parse($"/deployment/recipes/{recipeIndex}/scopes/{scopeIndex}/title"), scope.Title))))
            .ToArray() ?? [];
        if (!_configurationChoices.SequenceEqual(choices))
        {
            _configurationChoices = Array.AsReadOnly(choices);
            OnPropertyChanged(nameof(ConfigurationChoices));
        }
        OnPropertyChanged(nameof(HasConfigurationContext));
        OnPropertyChanged(nameof(SelectedConfigurationContext));
    }

    private void ViewChanged(object? sender, EventArgs args)
    {
        if (_experienceLocale != Form?.Locale)
        {
            _experienceLocale = Form?.Locale;
            foreach (DefinitionFormExperience experience in Form?.DisplayExperiences ?? [])
                _experiences.Single(choice => choice.Id == experience.Id).RefreshTitle(experience.Title);
            foreach (DesktopTransferCandidateViewModel candidate in _transferCandidates)
                candidate.RefreshLocalizedText();
            OnPropertyChanged(nameof(TransferApplyLabel));
        }
        OnPropertyChanged(nameof(SelectedLocale));
        OnPropertyChanged(nameof(FlowDirection));
        OnPropertyChanged(nameof(SelectedExperience));
        OnPropertyChanged(nameof(OptionalEditorsExpanded));
        NotifyFieldState();
        NotifyPresentationState();
    }

    public bool Validate()
    {
        if (_disposed || IsBusy) return false;
        if (Form is null)
        {
            _messenger.Send(new StatusChangedMessage("definition-required", Guidance: DesktopStatusGuidance.DefinitionRequired));
            return false;
        }
        try
        {
            foreach (FieldViewModel field in Fields) field.RequestValidationFeedback();
            DefinitionValidationResult result = Form.Validate();
            bool ready = result.IsReady && !Form.HasInputErrors;
            NotifyFieldState();
            _messenger.Send(new StatusChangedMessage(ready ? "validation-ready" : "validation-invalid:" +
                string.Join(",", result.Diagnostics.Select(item => $"{item.FieldId}:{item.Code}")
                    .Concat(Form.Fields.Where(item => item.InputDiagnosticCode is not null || !item.TryValue(out _))
                        .Select(item => $"{item.Definition.Id}:input-invalid"))), Guidance:
                ready ? DesktopStatusGuidance.General : DesktopStatusGuidance.ValidationInvalid));
            if (!ready) FocusFirstError();
            return ready;
        }
        catch (DefinitionException exception)
        {
            NotifyFieldState();
            FocusFirstError();
            _messenger.Send(new StatusChangedMessage($"validation-invalid:{exception.Code}", Guidance: DesktopStatusGuidance.ValidationInvalid));
            return false;
        }
    }

    private void FocusFirstError()
    {
        if (FirstError is not { } field) return;
        EditorFocusRequested?.Invoke(field);
        field.RequestFocus();
    }

    private void RevealEditor(FieldViewModel field) => EditorFocusRequested?.Invoke(field);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Replace(null);
        _messenger.UnregisterAll(this);
    }

    public override string ToString() => nameof(FormWorkspaceViewModel);
}

/// <summary>Retains only a review choice; previews are read from the current shared form on demand.</summary>
public sealed class DesktopTransferCandidateViewModel : ObservableObject
{
    private bool _isSelected;
    private readonly Func<string> _preview;
    private readonly Func<string> _title;

    internal DesktopTransferCandidateViewModel(DefinitionTransferCandidate candidate, Func<string> preview, Func<string> title)
    {
        Id = candidate.Id;
        _isSelected = candidate.SelectedByDefault;
        _preview = preview;
        _title = title;
    }

    public int Id { get; }
    public string Title => _title();
    public string Preview => _preview();
    internal void RefreshLocalizedText()
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Preview));
    }
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}

public sealed record DesktopNextActionViewModel(string Title, string? Description, IRelayCommand ReviewCommand);
