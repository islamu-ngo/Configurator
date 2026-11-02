namespace Configurator.Desktop.ViewModels.Fields;

using CommunityToolkit.Mvvm.Input;
using Configurator.Core.Definitions;
using Configurator.Desktop.Resources;

public sealed class StringFieldViewModel : FieldViewModel
{
    private bool _isRevealed;
    private bool _passwordRevealed;
    private bool _showErrors;
    private long _inputRevision = -1;
    private string _secretInput = string.Empty;
    private string _operationStatus = string.Empty;

    internal StringFieldViewModel(DefinitionNode schema, string name, bool required,
        FieldViewModel? parent, bool listItem, bool inheritedMasking = false)
        : base(schema, name, required, parent, listItem, inheritedMasking)
    {
        GenerateCommand = new AsyncRelayCommand(GenerateAsync, () => !IsDisposed && Form.CanGenerateSecret(FullPath()));
        CancelGenerationCommand = new RelayCommand(() => GenerateCommand.Cancel());
        RevealCommand = new RelayCommand(() => { if (IsDisposed) return; _isRevealed = !_isRevealed; NotifySecretState(); },
            () => !IsDisposed && RequiresMasking && Text.Length > 0);
        CopyCommand = new AsyncRelayCommand(() => CopyAsync(false), () => !IsDisposed && RequiresMasking && Text.Length > 0);
        CopyGeneratedPasswordCommand = new AsyncRelayCommand(() => CopyAsync(true), () => HasGeneratedPassword);
        RevealGeneratedPasswordCommand = new RelayCommand(() => { if (!HasGeneratedPassword) return; _passwordRevealed = !_passwordRevealed; NotifySecretState(); },
            () => HasGeneratedPassword);
        DismissSecretResultCommand = new RelayCommand(() =>
        {
            if (IsDisposed) return;
            Form.DismissSecretResult();
            _passwordRevealed = false;
            NotifySecretState();
        });
    }
    public int MaxLength => 65_536;
    public bool AcceptsReturn => Schema.Secret is { Kind: "private-key", Encoding: "pkcs8-pem" };
    public bool HasSecretPolicy => Schema.Secret is not null;
    public bool HasSecretGeneration => Schema.Secret is { Acquisition: "local", Generation: not null };
    public bool RequiresSecretInput => Schema.Secret is { Kind: "password-hash" or "digest", Generation.Input: "entered" };
    public bool IsRevealed => _isRevealed;
    public override char PasswordChar => RequiresMasking && !_isRevealed ? '●' : '\0';
    public string GenerateLabel => Text.Length == 0 ? DesktopActionText.GenerateLabel : DesktopActionText.RegenerateLabel;
    public string RevealLabel => _isRevealed ? DesktopActionText.HideLabel : DesktopActionText.ShowLabel;
    public string CopyLabel => DesktopActionText.CopySecretLabel;
    public string SecretInputLabel => DesktopActionText.SecretInputLabel;
    public string GeneratedPasswordLabel => DesktopActionText.GeneratedPasswordLabel;
    public string CopyGeneratedPasswordLabel => DesktopActionText.CopyGeneratedPasswordLabel;
    public string RevealGeneratedPasswordLabel => _passwordRevealed ? DesktopActionText.HideLabel : DesktopActionText.ShowLabel;
    public string DismissSecretResultLabel => DesktopActionText.DismissSecretResultLabel;
    public bool HasGeneratedPassword => !IsDisposed && Form.SecretOperation.HasGeneratedPassword &&
        Form.SecretOperation.EditorIdentity == Form.GetEditorIdentity(FullPath());
    public string GeneratedPassword => HasGeneratedPassword ? Form.CopyGeneratedPassword() ?? string.Empty : string.Empty;
    public char GeneratedPasswordChar => _passwordRevealed ? '\0' : '●';
    public string OperationStatus => _operationStatus;
    public string OperationMessage => DesktopActionText.SecretOperationMessage(_operationStatus);
    public string CancelGenerationLabel => DesktopActionText.CancelGenerationLabel;
    public string GenerateName => $"{GenerateLabel}: {Title}";
    public string RevealName => $"{RevealLabel}: {Title}";
    public string CopyName => $"{CopyLabel}: {Title}";
    public string CopyGeneratedPasswordName => $"{CopyGeneratedPasswordLabel}: {Title}";
    public string RevealGeneratedPasswordName => $"{RevealGeneratedPasswordLabel}: {GeneratedPasswordLabel}, {Title}";
    public string DismissSecretResultName => $"{DismissSecretResultLabel}: {Title}";
    public string SecretRequirements => IsDisposed || !HasSecretPolicy ? string.Empty
        : string.Join(Environment.NewLine, Form.GetSecretValidation(FullPath()).Requirements.Select(rule => rule.Message));
    public string InputHelpText => string.Join(Environment.NewLine,
        new[] { HelpText, SecretRequirements, ErrorText }.Where(text => !string.IsNullOrWhiteSpace(text)));
    public override string ErrorText
    {
        get
        {
            if (IsDisposed || !HasSecretPolicy) return base.ErrorText;
            var validation = Form.GetSecretValidation(FullPath(), _showErrors);
            return validation.ShowErrors
                ? string.Join(Environment.NewLine, validation.Failures.Select(rule => rule.Message)) : string.Empty;
        }
    }
    public string SecretInput
    {
        get => _secretInput;
        set { if (!IsDisposed) SetProperty(ref _secretInput, value ?? string.Empty); }
    }
    public IAsyncRelayCommand GenerateCommand { get; }
    public IRelayCommand CancelGenerationCommand { get; }
    public IRelayCommand RevealCommand { get; }
    public IAsyncRelayCommand CopyCommand { get; }
    public IAsyncRelayCommand CopyGeneratedPasswordCommand { get; }
    public IRelayCommand RevealGeneratedPasswordCommand { get; }
    public IRelayCommand DismissSecretResultCommand { get; }

    private async Task GenerateAsync(CancellationToken cancellationToken)
    {
        if (IsDisposed) return;
        _isRevealed = false;
        _passwordRevealed = false;
        string input = SecretInput;
        SecretInput = string.Empty;
        try
        {
            var result = await Form.GenerateSecretAsync(FullPath(), input.AsMemory(), cancellationToken: cancellationToken);
            if (IsDisposed) return;
            _operationStatus = result.Code;
            RequestFocus();
        }
        finally { if (!IsDisposed) NotifySecretState(); }
    }

    private async Task CopyAsync(bool originalPassword)
    {
        if (IsDisposed || originalPassword && !HasGeneratedPassword || !originalPassword && !RequiresMasking) return;
        string? value = originalPassword ? Form.CopyGeneratedPassword() : Text;
        bool copied = value is not null && SecretClipboard is { } clipboard && await clipboard.CopyAsync(value);
        if (IsDisposed) return;
        _operationStatus = copied ? "secret-copied" : "clipboard-unavailable";
        OnPropertyChanged(nameof(OperationStatus));
        OnPropertyChanged(nameof(OperationMessage));
    }

    protected override void NotifyValue()
    {
        if (_inputRevision != Form.InputRevision)
        {
            _inputRevision = Form.InputRevision;
            SecretInput = string.Empty;
            _operationStatus = string.Empty;
        }
        _isRevealed = false;
        _passwordRevealed = false;
        NotifySecretState();
    }

    protected override void OnRefreshed() => NotifyValue();

    public override void RequestFocus()
    {
        _showErrors = true;
        OnPropertyChanged(nameof(ErrorText));
        OnPropertyChanged(nameof(InputHelpText));
        base.RequestFocus();
    }

    private void NotifySecretState()
    {
        OnPropertyChanged(nameof(IsRevealed));
        OnPropertyChanged(nameof(PasswordChar));
        OnPropertyChanged(nameof(GenerateLabel));
        OnPropertyChanged(nameof(RevealLabel));
        OnPropertyChanged(nameof(CopyLabel));
        OnPropertyChanged(nameof(HasGeneratedPassword));
        OnPropertyChanged(nameof(GeneratedPassword));
        OnPropertyChanged(nameof(GeneratedPasswordChar));
        OnPropertyChanged(nameof(RevealGeneratedPasswordLabel));
        OnPropertyChanged(nameof(OperationStatus));
        OnPropertyChanged(nameof(OperationMessage));
        OnPropertyChanged(nameof(ErrorText));
        OnPropertyChanged(nameof(SecretRequirements));
        OnPropertyChanged(nameof(InputHelpText));
        OnPropertyChanged(nameof(GenerateName));
        OnPropertyChanged(nameof(RevealName));
        OnPropertyChanged(nameof(CopyName));
        OnPropertyChanged(nameof(CopyGeneratedPasswordName));
        OnPropertyChanged(nameof(RevealGeneratedPasswordName));
        OnPropertyChanged(nameof(DismissSecretResultName));
        GenerateCommand.NotifyCanExecuteChanged();
        RevealCommand.NotifyCanExecuteChanged();
        CopyCommand.NotifyCanExecuteChanged();
        CopyGeneratedPasswordCommand.NotifyCanExecuteChanged();
        RevealGeneratedPasswordCommand.NotifyCanExecuteChanged();
    }

    public override void Dispose()
    {
        GenerateCommand.Cancel();
        SecretInput = string.Empty;
        _isRevealed = false;
        _passwordRevealed = false;
        base.Dispose();
        OnPropertyChanged(nameof(Text));
        NotifySecretState();
    }
}
