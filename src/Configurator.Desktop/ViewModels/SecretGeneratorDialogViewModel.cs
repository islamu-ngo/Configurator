namespace Configurator.Desktop.ViewModels;

using System.ComponentModel;
using System.Globalization;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Configurator.Core.Secrets;
using Configurator.Desktop.Resources;
using Configurator.Desktop.Services;
using Configurator.Presentation;

/// <summary>Drives bounded local secret-generation options, entropy evaluation, and explicit-apply preview state.</summary>
public sealed class SecretGeneratorDialogViewModel : ObservableObject, IDisposable
{
    private readonly DefinitionFormModel _form;
    private readonly ISecretClipboardService? _clipboard;
    private string _lengthText;
    private bool _includeLowercase;
    private bool _includeUppercase;
    private bool _includeDigits;
    private bool _includeSymbols;
    private string _additionalForbiddenCharacters = string.Empty;
    private string _previewText = string.Empty;
    private bool _isPreviewRevealed;
    private bool _isApplying;
    private string? _clipboardNotice;
    private bool _closeRequested;
    private bool _disposed;

    public SecretGeneratorDialogViewModel(
        DefinitionFormModel form,
        DefinitionSecretGeneratorSession session,
        DesktopStrings strings,
        ISecretClipboardService? clipboard = null)
    {
        ArgumentNullException.ThrowIfNull(form);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(strings);

        _form = form;
        Session = session;
        Strings = strings;
        _clipboard = clipboard;

        Bounds = session.Bounds;
        _lengthText = (Bounds.IsPasswordBased
            ? session.Options.Length ?? Bounds.DefaultLength
            : session.Options.Bytes ?? Bounds.DefaultBytes)
            .ToString(CultureInfo.InvariantCulture);
        _includeLowercase = session.Options.IncludeLowercase ?? false;
        _includeUppercase = session.Options.IncludeUppercase ?? false;
        _includeDigits = session.Options.IncludeDigits ?? false;
        _includeSymbols = session.Options.IncludeSymbols ?? false;
        _additionalForbiddenCharacters = session.Options.AdditionalForbiddenCharacters;

        GeneratePreviewCommand = new AsyncRelayCommand(GeneratePreviewAsync, () => CanGenerate);
        CopyPreviewCommand = new AsyncRelayCommand(CopyPreviewAsync, () => HasPreview);
        ApplyCommand = new AsyncRelayCommand(ApplyAsync, () => CanApply);
        ToggleRevealPreviewCommand = new RelayCommand(ToggleRevealPreview);
        CancelCommand = new RelayCommand(Cancel);

        Session.PropertyChanged += OnSessionChanged;
        _form.InputChanged += OnFormInputChanged;
    }

    public DefinitionSecretGeneratorSession Session { get; }
    public SecretGenerationBounds Bounds { get; }
    public DesktopStrings Strings { get; }
    public DefinitionSecretOperationResult? LastResult { get; private set; }

    public event EventHandler<bool>? RequestClose;

    public IAsyncRelayCommand GeneratePreviewCommand { get; }
    public IAsyncRelayCommand CopyPreviewCommand { get; }
    public IAsyncRelayCommand ApplyCommand { get; }
    public IRelayCommand ToggleRevealPreviewCommand { get; }
    public IRelayCommand CancelCommand { get; }

    public FlowDirection FlowDirection =>
        PresentationStrings.Culture(_form.Locale).TextInfo.IsRightToLeft
            ? FlowDirection.RightToLeft
            : FlowDirection.LeftToRight;

    public string Title => Strings["SecretGeneratorTitle"];
    public string PrivacyNote => Strings["SecretGeneratorPrivacyNote"];
    public bool IsPasswordBased => Bounds.IsPasswordBased;
    public bool IsByteTokenBased => Bounds.IsByteTokenBased;
    public bool ShowsByteLengthInput => Bounds.IsByteTokenBased && Bounds.MaxBytes > Bounds.MinBytes;
    public bool ShowLowercaseOption => Bounds.AllowsLowercaseToggle || Bounds.RequireLowercase;
    public bool ShowUppercaseOption => Bounds.AllowsUppercaseToggle || Bounds.RequireUppercase;
    public bool ShowDigitsOption => Bounds.AllowsDigitsToggle || Bounds.RequireDigits;
    public bool ShowSymbolsOption => Bounds.AllowsSymbolsToggle || Bounds.RequireSymbols;

    public string LengthFieldLabel => Bounds.IsPasswordBased
        ? $"{Strings["SecretLengthLabel"]} ({Bounds.MinLength}–{Bounds.MaxLength})"
        : $"{Strings["SecretByteLengthLabel"]} ({Bounds.MinBytes}–{Bounds.MaxBytes})";

    public string EntropyText => string.Format(
        PresentationStrings.Culture(_form.Locale),
        Strings["SecretEntropyLabel"],
        Session.EstimatedEntropyBits);

    public string? ErrorText => Session.ErrorCode is { } code ? FormatError(code) : null;
    public bool HasError => !string.IsNullOrEmpty(ErrorText);

    public bool HasPreview => !_disposed && Session.HasCandidate && _previewText.Length > 0;
    public bool CanGenerate => !_disposed && Session.CanGenerate && !Session.IsBusy;
    public bool CanApply => !_disposed && Session.CanApply && !Session.IsBusy && !_isApplying;
    public bool HasClipboardNotice => !string.IsNullOrEmpty(_clipboardNotice);
    public string RevealButtonLabel => _isPreviewRevealed ? Strings.HideLabel : Strings.ShowLabel;
    public char PreviewPasswordChar => _isPreviewRevealed ? '\0' : '•';

    public string LengthText
    {
        get => _lengthText;
        set
        {
            if (_disposed || !SetProperty(ref _lengthText, value)) return;
            ClearPreviewState();
            int parsed = int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int numeric)
                ? numeric
                : 0;
            Session.UpdateOptions(Bounds.IsPasswordBased
                ? Session.Options with { Length = parsed }
                : Session.Options with { Bytes = parsed });
            NotifySessionDerivedState();
        }
    }

    public bool IncludeLowercase
    {
        get => _includeLowercase;
        set
        {
            if (_disposed || !Bounds.AllowsLowercaseToggle || !SetProperty(ref _includeLowercase, value)) return;
            ApplyOptionsUpdate(Session.Options with { IncludeLowercase = value });
        }
    }

    public bool IncludeUppercase
    {
        get => _includeUppercase;
        set
        {
            if (_disposed || !Bounds.AllowsUppercaseToggle || !SetProperty(ref _includeUppercase, value)) return;
            ApplyOptionsUpdate(Session.Options with { IncludeUppercase = value });
        }
    }

    public bool IncludeDigits
    {
        get => _includeDigits;
        set
        {
            if (_disposed || !Bounds.AllowsDigitsToggle || !SetProperty(ref _includeDigits, value)) return;
            ApplyOptionsUpdate(Session.Options with { IncludeDigits = value });
        }
    }

    public bool IncludeSymbols
    {
        get => _includeSymbols;
        set
        {
            if (_disposed || !Bounds.AllowsSymbolsToggle || !SetProperty(ref _includeSymbols, value)) return;
            ApplyOptionsUpdate(Session.Options with { IncludeSymbols = value });
        }
    }

    public string AdditionalForbiddenCharacters
    {
        get => _additionalForbiddenCharacters;
        set
        {
            string normalized = value ?? string.Empty;
            if (_disposed || !Bounds.IsPasswordBased || !SetProperty(ref _additionalForbiddenCharacters, normalized)) return;
            ApplyOptionsUpdate(Session.Options with { AdditionalForbiddenCharacters = normalized });
        }
    }

    public string PreviewText => _previewText;

    public bool IsPreviewRevealed
    {
        get => _isPreviewRevealed;
        private set
        {
            if (SetProperty(ref _isPreviewRevealed, value))
            {
                OnPropertyChanged(nameof(RevealButtonLabel));
                OnPropertyChanged(nameof(PreviewPasswordChar));
            }
        }
    }

    public string? ClipboardNotice
    {
        get => _clipboardNotice;
        private set
        {
            if (SetProperty(ref _clipboardNotice, value))
                OnPropertyChanged(nameof(HasClipboardNotice));
        }
    }

    public async Task<DefinitionSecretOperationResult> GeneratePreviewAsync()
    {
        if (_disposed || !Session.CanGenerate)
            return new(false, Session.ErrorCode ?? "secret-generation-unavailable");

        ClearPreviewState();
        NotifySessionDerivedState();

        DefinitionSecretOperationResult result = await Session.GeneratePreviewAsync();
        LastResult = result;
        if (!_disposed && result.Succeeded)
        {
            _previewText = Session.CopyPreview() ?? string.Empty;
            OnPropertyChanged(nameof(PreviewText));
        }
        NotifySessionDerivedState();
        return result;
    }

    public async Task<DefinitionSecretOperationResult> ApplyAsync()
    {
        if (_disposed || _isApplying || !Session.CanApply)
            return new(false, Session.ErrorCode ?? "secret-generation-unavailable");

        _isApplying = true;
        NotifySessionDerivedState();
        try
        {
            DefinitionSecretOperationResult result = await Session.ApplyAsync();
            LastResult = result;
            if (!_disposed && result.Succeeded)
            {
                ClearPreviewState();
                RaiseClose(true);
            }
            return result;
        }
        finally
        {
            _isApplying = false;
            NotifySessionDerivedState();
        }
    }

    private async Task CopyPreviewAsync()
    {
        if (_disposed || !Session.HasCandidate || _clipboard is null) return;
        string? candidate = Session.CopyPreview();
        if (string.IsNullOrEmpty(candidate)) return;
        bool copied = await _clipboard.CopyAsync(candidate);
        if (!_disposed)
            ClipboardNotice = copied ? Strings["SecretCopied"] : Strings["SecretClipboardUnavailable"];
    }

    private void ToggleRevealPreview()
    {
        if (_disposed || !HasPreview) return;
        IsPreviewRevealed = !IsPreviewRevealed;
    }

    private void Cancel()
    {
        ClearPreviewState();
        if (!_disposed)
        {
            Session.Cancel();
            NotifySessionDerivedState();
        }
        RaiseClose(false);
    }

    private void ApplyOptionsUpdate(SecretGenerationOptions updated)
    {
        ClearPreviewState();
        Session.UpdateOptions(updated);
        NotifySessionDerivedState();
    }

    private void ClearPreviewState()
    {
        if (_previewText.Length > 0)
        {
            _previewText = string.Empty;
            OnPropertyChanged(nameof(PreviewText));
        }
        IsPreviewRevealed = false;
        ClipboardNotice = null;
        OnPropertyChanged(nameof(HasPreview));
    }

    private void OnSessionChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (_disposed) return;
        if (Session.IsStale && !_isApplying)
        {
            ClearPreviewState();
            RaiseClose(false);
        }
        NotifySessionDerivedState();
    }

    private void OnFormInputChanged(object? sender, EventArgs args)
    {
        if (_disposed) return;
        if (_form.SessionPrivacy == DefinitionSessionPrivacy.BrowserNoSecrets)
        {
            ClearPreviewState();
            Session.Cancel();
            RaiseClose(false);
        }
        NotifySessionDerivedState();
    }

    private void NotifySessionDerivedState()
    {
        OnPropertyChanged(nameof(EntropyText));
        OnPropertyChanged(nameof(ErrorText));
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(HasPreview));
        OnPropertyChanged(nameof(CanGenerate));
        OnPropertyChanged(nameof(CanApply));
        GeneratePreviewCommand.NotifyCanExecuteChanged();
        CopyPreviewCommand.NotifyCanExecuteChanged();
        ApplyCommand.NotifyCanExecuteChanged();
    }

    private void RaiseClose(bool result)
    {
        if (_closeRequested) return;
        _closeRequested = true;
        RequestClose?.Invoke(this, result);
    }

    private string FormatError(string code) => code switch
    {
        "secret-options-out-of-bounds" => PresentationStrings.Get("SecretOptionsOutOfBounds", _form.Locale),
        "secret-generation-policy-invalid" => PresentationStrings.Get("SecretGenerationPolicyInvalid", _form.Locale),
        "secret-generation-entropy-insufficient" => PresentationStrings.Get("SecretGenerationEntropyInsufficient", _form.Locale),
        "secret-cancelled" => Strings["SecretCancelled"],
        _ => Strings["SecretOperationFailed"]
    };

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        ClearPreviewState();
        Session.PropertyChanged -= OnSessionChanged;
        _form.InputChanged -= OnFormInputChanged;
        Session.Dispose();
    }
}
