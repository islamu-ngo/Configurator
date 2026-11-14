namespace Configurator.Terminal;

using System.ComponentModel;
using System.Globalization;
using System.Text;
using Configurator.Core.Secrets;
using Configurator.Presentation;
using global::Terminal.Gui.App;
using global::Terminal.Gui.Drawing;
using global::Terminal.Gui.Input;
using global::Terminal.Gui.ViewBase;
using global::Terminal.Gui.Views;

/// <summary>
/// Centered 80x24-compatible modal dialog backed by <see cref="DefinitionSecretGeneratorSession"/>.
/// Holds preview bytes only in an explicitly zeroized <see cref="TerminalSecretBuffer"/> and commits only on Apply.
/// </summary>
internal sealed class TerminalSecretGeneratorDialog : Dialog
{
    private readonly DefinitionFormModel _form;
    private readonly DefinitionSecretGeneratorSession _session;
    private readonly IApplication? _application;
    private readonly IClipboard? _clipboard;
    private readonly Action<Action>? _dispatch;
    private readonly TerminalSecretBuffer _previewBuffer;
    private readonly SecretTextField _previewField;
    private readonly Label _boundsLabel;
    private readonly Label _lengthOrBytesLabel;
    private readonly TextField _lengthOrBytesField;
    private readonly Label _forbiddenLabel;
    private readonly TextField _forbiddenField;
    private readonly Button _lowerButton;
    private readonly Button _upperButton;
    private readonly Button _digitsButton;
    private readonly Button _symbolsButton;
    private readonly Label _complianceLabel;
    private readonly Label _previewCaption;
    private readonly Button _previewButton;
    private readonly Button _revealButton;
    private readonly Button _copyButton;
    private readonly Label _statusLabel;
    private readonly Button _applyButton;
    private readonly Button _cancelButton;
    private bool _syncingControls;

    internal TerminalSecretGeneratorDialog(
        DefinitionFormModel form,
        DefinitionSecretGeneratorSession session,
        IApplication? application = null,
        IClipboard? clipboard = null,
        Action<Action>? dispatch = null)
    {
        ArgumentNullException.ThrowIfNull(form);
        ArgumentNullException.ThrowIfNull(session);
        _form = form;
        _session = session;
        _application = application;
        _clipboard = clipboard;
        _dispatch = dispatch;
        _previewBuffer = new TerminalSecretBuffer(16384, urlSafeOnly: false);

        Title = TerminalStrings.Get("SecretGeneratorTitle", form.Locale);
        X = Pos.Center();
        Y = Pos.Center();
        Width = 72;
        Height = 19;

        _boundsLabel = new Label { X = 0, Y = 0, Width = Dim.Fill(), Height = 1 };

        SecretGenerationBounds bounds = _session.Bounds;
        bool passwordBased = bounds.IsPasswordBased;
        string sizeKey = passwordBased ? "SecretLengthLabel" : "SecretBytesLabel";
        _lengthOrBytesLabel = new Label
        {
            X = 0,
            Y = 1,
            Width = 10,
            Height = 1,
            Text = TerminalStrings.Get(sizeKey, form.Locale)
        };
        _lengthOrBytesField = new TextField
        {
            X = 11,
            Y = 1,
            Width = 8,
            Height = 1,
            Visible = bounds.SupportsOptions,
            Enabled = bounds.SupportsOptions
        };
        _lengthOrBytesLabel.Visible = bounds.SupportsOptions;

        _forbiddenLabel = new Label
        {
            X = 21,
            Y = 1,
            Width = 10,
            Height = 1,
            Text = TerminalStrings.Get("SecretForbiddenLabel", form.Locale),
            Visible = passwordBased && bounds.SupportsOptions
        };
        _forbiddenField = new TextField
        {
            X = 32,
            Y = 1,
            Width = Dim.Fill(),
            Height = 1,
            Visible = passwordBased && bounds.SupportsOptions,
            Enabled = passwordBased && bounds.SupportsOptions
        };

        _lowerButton = CreateToggle(0, 2, () => ToggleClass(lower: true));
        _upperButton = CreateToggle(17, 2, () => ToggleClass(upper: true));
        _digitsButton = CreateToggle(34, 2, () => ToggleClass(digits: true));
        _symbolsButton = CreateToggle(51, 2, () => ToggleClass(symbols: true));
        _lowerButton.Visible = passwordBased && bounds.AllowsLowercaseToggle;
        _upperButton.Visible = passwordBased && bounds.AllowsUppercaseToggle;
        _digitsButton.Visible = passwordBased && bounds.AllowsDigitsToggle;
        _symbolsButton.Visible = passwordBased && bounds.AllowsSymbolsToggle;

        _complianceLabel = new Label { X = 0, Y = 3, Width = Dim.Fill(), Height = 2 };
        _previewCaption = new Label
        {
            X = 0,
            Y = 5,
            Width = Dim.Fill(),
            Height = 1,
            Text = TerminalStrings.Get("SecretPreviewLabel", form.Locale)
        };
        _previewField = new SecretTextField(_previewBuffer)
        {
            X = 0,
            Y = 6,
            Width = Dim.Fill(),
            Height = 1,
            ReadOnly = true
        };

        _previewButton = CreateBodyButton("SecretPreviewAction", 0, 8, () => _ = PreviewCandidateAsync());
        _revealButton = CreateBodyButton("ShowSecret", 16, 8, TogglePreviewVisibility);
        _copyButton = CreateBodyButton("CopySecret", 32, 8, CopyPreviewToClipboard);

        _statusLabel = new Label { X = 0, Y = 10, Width = Dim.Fill(), Height = 2 };

        _applyButton = new Button { Text = TerminalStrings.Get("DialogApply", form.Locale), IsDefault = true };
        _applyButton.Accepting += (_, args) =>
        {
            args.Handled = true;
            _ = ApplyAndCloseAsync();
        };
        _cancelButton = new Button { Text = TerminalStrings.Get("DialogCancel", form.Locale) };
        _cancelButton.Accepting += (_, args) =>
        {
            args.Handled = true;
            CancelAndClear();
            _application?.RequestStop(this);
        };

        Add(_boundsLabel, _lengthOrBytesLabel, _lengthOrBytesField, _forbiddenLabel, _forbiddenField,
            _lowerButton, _upperButton, _digitsButton, _symbolsButton,
            _complianceLabel, _previewCaption, _previewField,
            _previewButton, _revealButton, _copyButton, _statusLabel);
        AddButton(_applyButton);
        AddButton(_cancelButton);

        _lengthOrBytesField.ValueChanged += (_, _) => OnNumericOrForbiddenChanged();
        _forbiddenField.ValueChanged += (_, _) => OnNumericOrForbiddenChanged();
        _session.PropertyChanged += SessionChanged;

        SyncFromSession();
    }

    internal TerminalSecretBuffer PreviewBuffer => _previewBuffer;
    internal bool CanGenerate => _session.CanGenerate;
    internal bool CanApply => _session.CanApply;

    internal void ApplyOptions(SecretGenerationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _previewField.ClearSensitiveState();
        _session.UpdateOptions(options);
        SyncFromSession();
    }

    internal async Task<bool> PreviewCandidateAsync(CancellationToken cancellationToken = default)
    {
        _previewField.ClearSensitiveState();
        DefinitionSecretOperationResult result = await _session.GeneratePreviewAsync(cancellationToken).ConfigureAwait(false);
        DispatchUi(() =>
        {
            _previewField.ClearSensitiveState();
            if (result.Succeeded && _session.HasCandidate)
            {
                string? value = _session.CopyPreview();
                if (!string.IsNullOrEmpty(value)) _previewField.TryReplaceSensitiveInput(value);
                _statusLabel.Text = TerminalStrings.Get("SecretGenerated", _form.Locale);
            }
            else
            {
                _statusLabel.Text = Sanitize(TerminalStrings.Get("SecretGenerationFailed", _form.Locale) + " " + result.Code);
            }
            SyncFromSession();
        });
        return result.Succeeded;
    }

    internal async Task<bool> ApplyCandidateAsync(CancellationToken cancellationToken = default)
    {
        DefinitionSecretOperationResult result = await _session.ApplyAsync(cancellationToken).ConfigureAwait(false);
        DispatchUi(() =>
        {
            _previewField.ClearSensitiveState();
            _statusLabel.Text = Sanitize(result.Succeeded
                ? TerminalStrings.Get("SecretApplied", _form.Locale)
                : TerminalStrings.Get("SecretGenerationFailed", _form.Locale) + " " + result.Code);
            SyncFromSession();
        });
        return result.Succeeded;
    }

    internal void CancelAndClear()
    {
        _previewField.ClearSensitiveState();
        _session.Cancel();
        _statusLabel.Text = TerminalStrings.Get("Cancelled", _form.Locale);
        SyncFromSession();
    }

    internal string ExportAllVisibleText()
    {
        var sb = new StringBuilder();
        CollectVisibleText(this, sb);
        return sb.ToString();
    }

    protected override bool OnKeyDown(Key key)
    {
        if (key == Key.Esc)
        {
            CancelAndClear();
            _application?.RequestStop(this);
            return true;
        }
        return base.OnKeyDown(key);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _session.PropertyChanged -= SessionChanged;
            _previewField.ClearSensitiveState();
            _previewBuffer.Dispose();
        }
        base.Dispose(disposing);
    }

    private Button CreateToggle(int x, int y, Action action)
    {
        var button = new Button
        {
            X = x,
            Y = y,
            Width = 16,
            Height = 1,
            BorderStyle = LineStyle.None,
            ShadowStyle = null
        };
        button.Accepting += (_, args) =>
        {
            args.Handled = true;
            action();
        };
        return button;
    }

    private Button CreateBodyButton(string key, int x, int y, Action action)
    {
        var button = new Button
        {
            X = x,
            Y = y,
            Width = 15,
            Height = 1,
            Text = TerminalStrings.Get(key, _form.Locale),
            BorderStyle = LineStyle.None,
            ShadowStyle = null
        };
        button.Accepting += (_, args) =>
        {
            args.Handled = true;
            action();
        };
        return button;
    }

    private void ToggleClass(bool lower = false, bool upper = false, bool digits = false, bool symbols = false)
    {
        SecretGenerationOptions current = _session.Options;
        ApplyOptions(current with
        {
            IncludeLowercase = lower ? !(current.IncludeLowercase ?? true) : current.IncludeLowercase,
            IncludeUppercase = upper ? !(current.IncludeUppercase ?? true) : current.IncludeUppercase,
            IncludeDigits = digits ? !(current.IncludeDigits ?? true) : current.IncludeDigits,
            IncludeSymbols = symbols ? !(current.IncludeSymbols ?? true) : current.IncludeSymbols
        });
    }

    private void OnNumericOrForbiddenChanged()
    {
        if (_syncingControls || !_session.Bounds.SupportsOptions) return;
        SecretGenerationOptions current = _session.Options;
        if (_session.Bounds.IsPasswordBased)
        {
            if (!int.TryParse(_lengthOrBytesField.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int len))
            {
                _statusLabel.Text = TerminalStrings.Get("SecretInvalidBounds", _form.Locale);
                return;
            }
            ApplyOptions(current with { Length = len, AdditionalForbiddenCharacters = _forbiddenField.Text });
        }
        else if (_session.Bounds.IsByteTokenBased)
        {
            if (!int.TryParse(_lengthOrBytesField.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int bytes))
            {
                _statusLabel.Text = TerminalStrings.Get("SecretInvalidBounds", _form.Locale);
                return;
            }
            ApplyOptions(current with { Bytes = bytes });
        }
    }

    private async Task ApplyAndCloseAsync()
    {
        if (await ApplyCandidateAsync().ConfigureAwait(false))
            DispatchUi(() => _application?.RequestStop(this));
    }

    private void TogglePreviewVisibility()
    {
        if (_previewBuffer.Count == 0) return;
        _previewField.SetRevealed(!_previewField.IsRevealed);
        _revealButton.Text = TerminalStrings.Get(_previewField.IsRevealed ? "HideSecret" : "ShowSecret", _form.Locale);
    }

    private void CopyPreviewToClipboard()
    {
        IClipboard? clipboard = OperatingSystem.IsLinux() && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY"))
            ? null
            : _clipboard ?? _application?.Clipboard;
        _statusLabel.Text = TerminalStrings.Get(
            _previewField.TryCopy(clipboard) ? "SecretCopied" : "SecretClipboardUnavailable",
            _form.Locale);
    }

    private void SessionChanged(object? sender, PropertyChangedEventArgs args) =>
        DispatchUi(SyncFromSession);

    private void SyncFromSession()
    {
        _syncingControls = true;
        try
        {
            SecretGenerationBounds bounds = _session.Bounds;
            SecretGenerationOptions opts = _session.Options;
            CultureInfo culture = TerminalStrings.Culture(_form.Locale);

            if (bounds.IsPasswordBased)
            {
                _boundsLabel.Text = Sanitize(string.Format(culture, "{0} [{1}..{2}]",
                    bounds.Kind.ToString(), bounds.MinLength, bounds.MaxLength));
                string sizeText = (opts.Length ?? bounds.DefaultLength).ToString(CultureInfo.InvariantCulture);
                if (_lengthOrBytesField.Text != sizeText) _lengthOrBytesField.Text = sizeText;
                if (_forbiddenField.Text != opts.AdditionalForbiddenCharacters) _forbiddenField.Text = opts.AdditionalForbiddenCharacters;
                _lowerButton.Text = ToggleText(opts.IncludeLowercase ?? true, "SecretLowerLabel");
                _upperButton.Text = ToggleText(opts.IncludeUppercase ?? true, "SecretUpperLabel");
                _digitsButton.Text = ToggleText(opts.IncludeDigits ?? true, "SecretDigitsLabel");
                _symbolsButton.Text = ToggleText(opts.IncludeSymbols ?? true, "SecretSymbolsLabel");
            }
            else if (bounds.IsByteTokenBased)
            {
                _boundsLabel.Text = Sanitize(string.Format(culture, "{0} [{1}..{2}]",
                    bounds.Kind.ToString(), bounds.MinBytes, bounds.MaxBytes));
                string sizeText = (opts.Bytes ?? bounds.DefaultBytes).ToString(CultureInfo.InvariantCulture);
                if (_lengthOrBytesField.Text != sizeText) _lengthOrBytesField.Text = sizeText;
            }
            else
            {
                _boundsLabel.Text = Sanitize(bounds.Kind.ToString());
            }

            string policyText = _session.MeetsPolicy
                ? TerminalStrings.Get("SecretEntropyReady", _form.Locale)
                : !_session.MeetsEntropyFloor
                    ? TerminalStrings.Get("SecretEntropyLow", _form.Locale)
                    : TerminalStrings.Get("SecretInvalidBounds", _form.Locale);
            _complianceLabel.Text = Sanitize(string.Format(culture, "{0:F1} bits | {1}", _session.EstimatedEntropyBits, policyText));

            _previewButton.Enabled = _session.CanGenerate;
            _revealButton.Enabled = _previewBuffer.Count > 0;
            _revealButton.Text = TerminalStrings.Get(_previewField.IsRevealed ? "HideSecret" : "ShowSecret", _form.Locale);
            _copyButton.Enabled = _previewBuffer.Count > 0;
            _applyButton.Enabled = _session.CanApply;
        }
        finally
        {
            _syncingControls = false;
        }
    }

    private string ToggleText(bool enabled, string key) =>
        Sanitize((enabled ? "[x] " : "[ ] ") + TerminalStrings.Get(key, _form.Locale));

    private void DispatchUi(Action action)
    {
        if (_dispatch is not null)
        {
            _dispatch(action);
            return;
        }
        if (_application is { Initialized: true })
        {
            _application.Invoke(action);
            return;
        }
        action();
    }

    private static string Sanitize(string text) => new(text.Select(c => char.IsControl(c) ? ' ' : c).ToArray());

    private static void CollectVisibleText(View root, StringBuilder sb)
    {
        foreach (View child in root.SubViews)
        {
            if (child is Label or Button or TextField && child is not SecretTextField)
                sb.AppendLine(child.Text);
            if (child.SubViews.Count > 0)
                CollectVisibleText(child, sb);
        }
    }
}
