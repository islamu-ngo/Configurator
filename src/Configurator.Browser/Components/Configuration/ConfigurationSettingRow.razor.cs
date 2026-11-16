namespace Configurator.Browser.Components.Configuration;

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Configurator.Browser.Resources;
using Configurator.Browser.Services;
using Configurator.Core.Definitions;
using Configurator.Presentation;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using MudBlazor;

/// <summary>
/// Lightweight semantic HTML5 control row for scalar, choice, and toggle settings in continuous views.
/// Retains zero answer buffers: all raw drafts and consent checks remain owned by <see cref="DefinitionFormModel"/>.
/// </summary>
public partial class ConfigurationSettingRow
{
    [Inject] private IJSRuntime Javascript { get; set; } = null!;
    [Inject] private IDialogService Dialogs { get; set; } = null!;
    [Parameter, EditorRequired] public DefinitionFormModel Form { get; set; } = null!;
    [Parameter, EditorRequired] public DefinitionNode Schema { get; set; } = null!;
    [Parameter, EditorRequired] public IReadOnlyList<string> Path { get; set; } = [];
    [Parameter] public string Identity { get; set; } = string.Empty;
    [Parameter] public DefinitionPath Location { get; set; } = default!;
    [Parameter] public DefinitionValidationResult? Validation { get; set; }
    [Parameter] public bool RequiredOverride { get; set; }
    [Parameter] public bool SectionAllRequired { get; set; }
    [Parameter] public bool InheritedSecret { get; set; }
    [Parameter] public string? TitleOverride { get; set; }
    [Parameter] public bool FocusFirstControl { get; set; }
    [Parameter] public long FocusGeneration { get; set; }
    [Parameter] public IReadOnlyList<string> FocusPath { get; set; } = [];
    [CascadingParameter(Name = "WorkspaceConsentAction")] public EventCallback RequestSecretConsent { get; set; }
    [CascadingParameter(Name = "OnFieldFocused")] public Action<DefinitionPath>? OnFieldFocused { get; set; }
    [CascadingParameter(Name = "OnWorkspaceKeyDown")] public Func<KeyboardEventArgs, Task>? OnWorkspaceKeyDown { get; set; }

    private ElementReference _controlElement;
    private DefinitionFieldInput? _rootInput;
    private DefinitionFormModel? _parameterForm;
    private DefinitionFormProjection? _parameterProjection;
    private long _parameterInputRevision = -1;
    private string? _parameterLocale;
    private bool _suppressParameterRender;

    private DefinitionFormModel? _renderedForm;
    private DefinitionScalarFieldStamp _renderedScalarStamp;
    private DefinitionNode? _renderedSchema;
    private DefinitionValidationResult? _renderedValidation;
    private string? _renderedIdentity;
    private DefinitionPath? _renderedLocation;
    private bool _renderedRequiredOverride;
    private bool _renderedSectionAllRequired;
    private bool _renderedInheritedSecret;
    private string? _renderedTitleOverride;
    private bool _renderedFocusFirstControl;
    private long _renderedFocusGeneration;
    private string[] _renderedFocusPath = [];
    private string[] _renderedPath = [];

    private DefinitionFormModel? _verifiedScalarForm;
    private DefinitionValidationResult? _verifiedValidation;
    private long _verifiedScalarStampEpoch = -1;
    private long _verifiedScalarInputRevision = -1;
    private long _verifiedRootScalarEpoch = -1;
    private bool _scalarRenderSuppressed;
    private long _scalarSynchronizedRevision = -1;
    private long _renderedInputRevision = -1;

    private DefinitionEditorPresentation? _presentation;
    private DefinitionSessionPrivacy _lastPrivacy;
    private long _controlGeneration;
    private long _actionGeneration;
    private long _lastInputRevision = -1;
    private long _observedFocusGeneration;
    private bool _focusedForRequest;
    private bool _focusAfterAction;
    private bool _invalid;
    private bool _secretRevealed;
    private long _secretRevision = -1;
    private IDialogReference? _helpDialog;
    private bool _helpOpening;
    private long _helpGeneration;
    private bool _selfRenderRequested;
    private bool _disposed;
    private Func<(IReadOnlyList<string> ArtifactIds, IReadOnlyList<string> BindingKeys)>? _getBindingsDelegate;

    private string HelpButtonTitle => ConfigurationFieldHelp.FormatHelpTitle(Form.Locale, Title);

    private string FieldKey => string.Join('.', Path);
    private string FieldPathLabel => string.Join(" / ", Path);
    private string ControlId => string.IsNullOrEmpty(Identity) ? "row-" + FieldKey : Identity;
    private DefinitionPath EditorPath => Location;
    private string Ui(string key) => BrowserStrings.Get(key, Form.Locale);
    private string UiFormat(string key, params object[] values) =>
        string.Format(CultureInfo.GetCultureInfo(Form.Locale), Ui(key), values);
    private Func<(IReadOnlyList<string> ArtifactIds, IReadOnlyList<string> BindingKeys)> GetBindingsDelegate =>
        _getBindingsDelegate ??= () => Form.GetEditorBindings(Path);

    private DefinitionFieldValidation CorrectionFacts => Form.GetFieldValidation(EditorPath);
    private bool CanEdit => CorrectionFacts.CanEdit;
    private bool Required => RequiredOverride;
    private bool SecretsEnabled => Form.SessionPrivacy == DefinitionSessionPrivacy.BrowserSecretsEnabled;
    private bool IsSecret => InheritedSecret || DefinitionSensitivityPolicy.DeclaresSecret(Schema)
        || (Path.Count == 1 && DefinitionSensitivityPolicy.DeclaresSecret(Form.Definition.Configuration));
    private bool IsMasked => CorrectionFacts.RequiresMasking;
    private bool IsMultilineSecret => Schema.Secret is { Kind: DefinitionSecretKind.PrivateKey, Encoding: DefinitionSecretEncoding.Pkcs8Pem };
    private bool IsRetainedByProfile => Form.BrowserProfile?.RetainedPaths.Any(retained =>
        retained.Segments.Count == 0 || EditorPath.Segments.Count == 0 ||
        retained.Segments[0] == EditorPath.Segments[0]) == true;

    private string Title => TitleOverride ?? _presentation?.Title
        ?? (Schema.Keywords.TryGetProperty("title", out var value) ? value.GetString()! : Ui("FieldItem"));
    private string? Help => _presentation?.Instructions ?? _presentation?.Description
        ?? (Schema.Keywords.TryGetProperty("description", out var value) ? value.GetString() : null);

    private bool HasChoices => Schema.Keywords.TryGetProperty("enum", out _);
    private JsonElement Choices => Schema.Keywords.GetProperty("enum");
    private bool UseSwitch => !IsMasked && !HasChoices && Schema.JsonType == "boolean"
        && _presentation?.Widget == DefinitionFieldWidget.Switch;
    private bool UseSlider => !IsMasked && Schema.JsonType is "integer" or "number"
        && _presentation?.Widget == DefinitionFieldWidget.Slider && TryGetSliderBounds(out _, out _, out _);
    private bool IsMultilineWidget => !IsMasked && Schema.JsonType == "string"
        && _presentation?.Widget is DefinitionFieldWidget.MultiLine or DefinitionFieldWidget.Markdown or DefinitionFieldWidget.Code;
    private int ResolvedRows => _presentation?.Rows is > 0 and <= 64 ? _presentation.Rows.Value : 4;
    private bool IsDynamicPrivateMapEntry => IsSecret && Path.Count > 1 && Form.GetEditorSchema(EditorPath)?.Keywords is not null &&
        _presentation?.HasCopyableWireToken == false && Path[^1] == TitleOverride;

    private string InputModeToken => Schema.JsonType switch
    {
        "integer" => "numeric",
        "number" => "decimal",
        _ => "text"
    };

    private JsonElement? Value =>
        Form.Validate().TryGetEffectiveValue(EditorPath, out var effective) ? effective : null;

    private string TextFor(bool canEdit) => !canEdit ? string.Empty : Path.Count == 1 && _rootInput is not null
        ? _rootInput.Text
        : Form.ReadDraftText(EditorPath) ?? (Value is { } value ? Display(value) : string.Empty);

    private string SwitchAriaChecked => Value switch
    {
        { ValueKind: JsonValueKind.True } => "true",
        { ValueKind: JsonValueKind.False } => "false",
        _ => "mixed"
    };

    private string SwitchLabelText => Value switch
    {
        { ValueKind: JsonValueKind.True } => _presentation?.BooleanLabels?.TrueLabel ?? Ui("Yes"),
        { ValueKind: JsonValueKind.False } => _presentation?.BooleanLabels?.FalseLabel ?? Ui("No"),
        _ => Ui("SwitchUnset")
    };

    private string Selection
    {
        get
        {
            if (Value is not { } value) return string.Empty;
            if (!HasChoices) return Display(value);
            int index = 0;
            foreach (JsonElement choice in Choices.EnumerateArray())
            {
                if (JsonElement.DeepEquals(choice, value)) return ChoiceIdentity(index);
                index++;
            }
            return string.Empty;
        }
    }

    private string SelectionLabel(string? identity)
    {
        if (string.IsNullOrEmpty(identity)) return Ui(IsMasked && HasChoices
            ? Value is null ? "MaskedChoiceAbsent" : "MaskedChoiceReplace" : "NotSupplied");
        if (!HasChoices) return Ui(identity == "true" ? "Yes" : "No");
        int index = int.Parse(identity, CultureInfo.InvariantCulture);
        return ChoiceLabel(index, Choices[index]);
    }

    private static string ChoiceIdentity(int index) => index.ToString(CultureInfo.InvariantCulture);
    private string ChoiceLabel(int index, JsonElement value)
    {
        string label = _presentation?.Choices[index].Label ?? Display(value);
        return label.Length == 0 ? Ui("EmptyString") : label;
    }

    private static string Display(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString()!,
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        JsonValueKind.Null => string.Empty,
        _ => value.GetRawText()
    };

    private DefinitionAnswerOrigin CompactOrigin => Form.AnswerOrigin(Path);
    private string CompactFieldOrigin => CompactOrigin.ToString().ToLowerInvariant();
    private string CompactFieldState
    {
        get
        {
            if (_invalid || Error.Length > 0) return "invalid";
            if (IsSecret && !CanEdit) return "secret-locked";
            if (Required && Value is null) return "missing-required";
            if (Value is not null && CompactOrigin == DefinitionAnswerOrigin.Defaulted) return "defaulted";
            if (Value is not null) return "supplied";
            return "omitted";
        }
    }

    private string CompactStatusLabel => CompactFieldState switch
    {
        "invalid" => Ui("CompactStateInvalid"),
        "secret-locked" => Ui("CompactStateSecretLocked"),
        "missing-required" => Ui("CompactStateMissingRequired"),
        "defaulted" => Ui("CompactStateDefaulted"),
        "supplied" => UiFormat("CompactStateSupplied", CompactFieldOrigin),
        _ => Ui("CompactStateOmitted")
    };

    private bool HasHelp(bool isMasked) =>
        !string.IsNullOrWhiteSpace(Help) || InputPurposeHelp is not null || (isMasked && IsMultilineSecret);

    private bool HasError(string error) => _invalid || error.Length > 0;
    private string ErrorMessage(string error) => _invalid ? Ui("FieldInputTypeInvalid") : error;

    private string? BuildDescribedBy(bool isMasked, string error)
    {
        var ids = new List<string>(3);
        if (HasHelp(isMasked)) ids.Add(ControlId + "-help");
        if (HasError(error)) ids.Add(ControlId + "-error");
        if (Schema.Secret is not null && CanEdit) ids.Add(ControlId + "-rules");
        return ids.Count == 0 ? null : string.Join(' ', ids);
    }

    private string Error
    {
        get
        {
            if (Schema.Secret is not null)
                return string.Join(" ", Form.GetSecretValidation(EditorPath, Validation is not null).Failures.Select(item => item.Message));
            if (Validation is null && !Form.HasInputError(EditorPath)) return string.Empty;
            string fieldId = Path.Count > 0 ? Path[0] : string.Empty;
            IEnumerable<DefinitionDiagnostic> validationDiagnostics = Validation?.DiagnosticsForField(fieldId) ?? [];
            IEnumerable<DefinitionDiagnostic> inputDiagnostics = Form.HasInputError(EditorPath) ? Form.InputDiagnostics : [];
            var codes = validationDiagnostics.Concat(inputDiagnostics).Where(MatchesDiagnostic)
                .Select(diagnostic => diagnostic.Code).Distinct(StringComparer.Ordinal);
            return string.Join(" ", codes.Append(PresentationStrings.FieldCorrection(CorrectionFacts, Form.Locale)));
        }
    }

    private bool MatchesDiagnostic(DefinitionDiagnostic diagnostic)
    {
        if (diagnostic.Location is { } location)
            return Form.TryResolveCorrectionPath(diagnostic.FieldId, location, out var resolved) &&
                resolved.Segments.Take(EditorPath.Segments.Count).SequenceEqual(EditorPath.Segments);
        var state = Form.GetEditorState(EditorPath);
        return state is not null && state.FieldId == diagnostic.FieldId &&
            diagnostic.RelativePath.Take(state.RelativePath.Count).SequenceEqual(state.RelativePath);
    }

    private string Autocomplete(bool canEdit) => canEdit && Schema.Secret is { Kind: DefinitionSecretKind.Password } secret
        ? secret.Acquisition switch
        {
            DefinitionSecretAcquisition.Local => "section-" + ControlId + " new-password",
            DefinitionSecretAcquisition.Existing => "section-" + ControlId + " current-password",
            _ => "off"
        } : "off";

    private string? InputPurposeHelp => Schema.Secret is { } secret
        ? secret.Acquisition switch
        {
            DefinitionSecretAcquisition.Issuer => Ui("CredentialIssuerHelp"),
            DefinitionSecretAcquisition.Local => Ui("CredentialCreateHelp"),
            _ => Ui("CredentialExistingHelp")
        }
        : null;

    private bool TryGetSliderBounds(out double min, out double max, out double step)
    {
        min = 0; max = 100; step = Schema.JsonType == "integer" ? 1 : 0.1;
        bool hasMin = Schema.Keywords.TryGetProperty("minimum", out var minEl) && minEl.TryGetDouble(out min);
        bool hasMax = Schema.Keywords.TryGetProperty("maximum", out var maxEl) && maxEl.TryGetDouble(out max);
        if (!hasMin || !hasMax || max <= min) return false;
        if (Schema.Keywords.TryGetProperty("multipleOf", out var stepEl) && stepEl.TryGetDouble(out double declared) && declared > 0)
            step = declared;
        return true;
    }

    public override Task SetParametersAsync(ParameterView parameters)
    {
        bool Changed<T>(string name, T current) =>
            parameters.TryGetValue<T>(name, out var supplied) && !EqualityComparer<T>.Default.Equals(current, supplied);
        bool PathChanged(string name, IReadOnlyList<string> current) =>
            parameters.TryGetValue<IReadOnlyList<string>>(name, out var supplied) && !current.SequenceEqual(supplied);

        DefinitionFormModel form = parameters.TryGetValue<DefinitionFormModel>(nameof(Form), out var suppliedForm)
            ? suppliedForm : Form;
        _suppressParameterRender = ReferenceEquals(_parameterForm, form)
            && _parameterInputRevision == form.InputRevision
            && _parameterLocale == form.Locale
            && ReferenceEquals(_parameterProjection, form.Projection)
            && !Changed(nameof(Identity), Identity)
            && !(parameters.TryGetValue<DefinitionNode>(nameof(Schema), out var suppliedSchema) && !ReferenceEquals(Schema, suppliedSchema))
            && !PathChanged(nameof(Path), Path)
            && !Changed(nameof(Location), Location)
            && !Changed(nameof(Validation), Validation)
            && !Changed(nameof(RequiredOverride), RequiredOverride)
            && !Changed(nameof(SectionAllRequired), SectionAllRequired)
            && !Changed(nameof(InheritedSecret), InheritedSecret)
            && !Changed(nameof(TitleOverride), TitleOverride)
            && !Changed(nameof(FocusFirstControl), FocusFirstControl)
            && !Changed(nameof(FocusGeneration), FocusGeneration)
            && !PathChanged(nameof(FocusPath), FocusPath);
        try
        {
            Task updating = base.SetParametersAsync(parameters);
            _parameterForm = form;
            _parameterInputRevision = form.InputRevision;
            _parameterProjection = form.Projection;
            _parameterLocale = form.Locale;
            return updating;
        }
        finally
        {
            _suppressParameterRender = false;
        }
    }

    protected override bool ShouldRender() => _selfRenderRequested || (!_suppressParameterRender && !_scalarRenderSuppressed);

    protected override void OnParametersSet()
    {
        if (Path.Count == 1 && _rootInput is null)
            _rootInput = Form.GetFieldEditor(Path[0]).Input;
        if (_suppressParameterRender) return;

        if (CanSkipScalarRender())
        {
            _scalarRenderSuppressed = true;
            _scalarSynchronizedRevision = Form.InputRevision;
            return;
        }
        _scalarRenderSuppressed = false;

        if (!ReferenceEquals(_renderedForm, Form) || _lastPrivacy != Form.SessionPrivacy)
        {
            ++_controlGeneration;
            ++_actionGeneration;
            ++_helpGeneration;
            _helpDialog?.Close();
            _helpDialog = null;
            _invalid = false;
            _secretRevealed = false;
            _rootInput = Path.Count == 1 ? Form.GetFieldEditor(Path[0]).Input : null;
        }
        else if (_secretRevision != Form.InputRevision && _secretRevealed && IsMultilineSecret)
        {
            ++_controlGeneration;
            _secretRevealed = false;
        }

        _secretRevision = Form.InputRevision;
        _presentation = Form.GetEditorPresentation(EditorPath);
        _lastPrivacy = Form.SessionPrivacy;
    }

    private bool CanSkipScalarRender()
    {
        if (!_renderedScalarStamp.SupportsSkip || _selfRenderRequested || Path.Count == 0 ||
            Schema.Secret is not null ||
            !ReferenceEquals(_renderedForm, Form) ||
            !ReferenceEquals(_renderedSchema, Schema) ||
            _renderedIdentity != Identity ||
            !Equals(_renderedLocation, Location) ||
            _renderedRequiredOverride != RequiredOverride ||
            _renderedSectionAllRequired != SectionAllRequired ||
            _renderedInheritedSecret != InheritedSecret ||
            !string.Equals(_renderedTitleOverride, TitleOverride, StringComparison.Ordinal) ||
            _renderedFocusFirstControl != FocusFirstControl ||
            ConfigurationField.PathChangedFrom(_renderedPath, Path))
            return false;
        if (FocusFirstControl && (_renderedFocusGeneration != FocusGeneration ||
            ConfigurationField.PathChangedFrom(_renderedFocusPath, FocusPath)))
        {
            _verifiedScalarForm = null;
            return false;
        }
        if (ReferenceEquals(_verifiedScalarForm, Form) && ReferenceEquals(_verifiedValidation, Validation))
        {
            if (Path.Count == 1 && _verifiedRootScalarEpoch > 0 && Form.GetRootScalarEpoch(Path[0]) == _verifiedRootScalarEpoch)
                return true;
            if (Form.ScalarStampEpoch == _verifiedScalarStampEpoch && Form.InputRevision == _verifiedScalarInputRevision)
                return true;
        }
        if (!ReferenceEquals(_renderedValidation, Validation) &&
            !ConfigurationField.SameDiagnosticsForField(Path[0], _renderedValidation, Validation))
        {
            _verifiedScalarForm = null;
            return false;
        }
        DefinitionScalarFieldStamp next = Form.GetScalarFieldRenderStamp(Path, InheritedSecret);
        if (next.SupportsSkip && next.Equals(_renderedScalarStamp))
        {
            _verifiedScalarForm = Form;
            _verifiedScalarStampEpoch = Form.ScalarStampEpoch;
            _verifiedScalarInputRevision = Form.InputRevision;
            _verifiedRootScalarEpoch = Path.Count == 1 ? Form.GetRootScalarEpoch(Path[0]) : -1L;
            _verifiedValidation = Validation;
            return true;
        }
        _verifiedScalarForm = null;
        return false;
    }

    protected override void OnAfterRender(bool firstRender)
    {
        _renderedForm = Form;
        _renderedSchema = Schema;
        _renderedValidation = Validation;
        _renderedIdentity = Identity;
        _renderedLocation = Location;
        _renderedRequiredOverride = RequiredOverride;
        _renderedSectionAllRequired = SectionAllRequired;
        _renderedInheritedSecret = InheritedSecret;
        _renderedTitleOverride = TitleOverride;
        _renderedFocusFirstControl = FocusFirstControl;
        _renderedFocusGeneration = FocusGeneration;
        _renderedFocusPath = FocusPath.ToArray();
        _renderedPath = Path.ToArray();
        _renderedInputRevision = Form.InputRevision;
        _renderedScalarStamp = Path.Count > 0 ? Form.GetScalarFieldRenderStamp(Path, InheritedSecret) : default;
        if (_renderedScalarStamp.SupportsSkip)
        {
            _verifiedScalarForm = Form;
            _verifiedScalarStampEpoch = Form.ScalarStampEpoch;
            _verifiedScalarInputRevision = Form.InputRevision;
            _verifiedRootScalarEpoch = Path.Count == 1 ? Form.GetRootScalarEpoch(Path[0]) : -1L;
            _verifiedValidation = Validation;
        }
        else
        {
            _verifiedScalarForm = null;
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        _selfRenderRequested = false;
        if (_focusAfterAction)
        {
            _focusAfterAction = false;
            if (CanEdit && !string.IsNullOrEmpty(_controlElement.Id))
                await _controlElement.FocusAsync(preventScroll: true);
        }
        if (!FocusFirstControl)
        {
            _focusedForRequest = false;
            return;
        }
        if (_observedFocusGeneration != FocusGeneration)
        {
            _focusedForRequest = false;
            _observedFocusGeneration = FocusGeneration;
        }
        if (_focusedForRequest || !CanEdit || string.IsNullOrEmpty(_controlElement.Id)) return;
        _focusedForRequest = true;
        await _controlElement.FocusAsync(preventScroll: false);
    }

    private bool IsSynchronizedRevision(long expectedRevision) =>
        Form.InputRevision == expectedRevision ||
        (_scalarRenderSuppressed && expectedRevision == _renderedInputRevision && Form.InputRevision == _scalarSynchronizedRevision);

    private bool IsCurrentAction(DefinitionFormModel form, IReadOnlyList<string> path, long revision, long generation, bool requiresEdit = true) =>
        !_disposed && generation == _actionGeneration && ReferenceEquals(Form, form) &&
        IsSynchronizedRevision(revision) && Path.SequenceEqual(path) && (!requiresEdit || CanEdit);

    private void EditScalarText(string? text, DefinitionFormModel expectedForm, IReadOnlyList<string> expectedPath,
        long expectedRevision, long expectedControlGeneration, bool changeOnly)
    {
        if (_disposed || expectedControlGeneration != _controlGeneration || !ReferenceEquals(Form, expectedForm) ||
            !IsSynchronizedRevision(expectedRevision) || !Path.SequenceEqual(expectedPath) || !CanEdit)
            return;
        string normalized = text ?? string.Empty;
        if (changeOnly && _lastInputRevision == expectedRevision && TextFor(CanEdit) == normalized) return;
        _selfRenderRequested = true;
        bool keepMultilineVisible = IsMultilineSecret && _secretRevealed;
        _invalid = !Form.EditValue(EditorPath, normalized);
        if (keepMultilineVisible) { _secretRevealed = true; _secretRevision = Form.InputRevision; }
        _lastInputRevision = changeOnly ? -1 : Form.InputRevision;
    }

    private void EditSelect(ChangeEventArgs args, DefinitionFormModel expectedForm, IReadOnlyList<string> expectedPath,
        long expectedRevision, long expectedControlGeneration)
    {
        if (_disposed || expectedControlGeneration != _controlGeneration || !ReferenceEquals(Form, expectedForm) ||
            !IsSynchronizedRevision(expectedRevision) || !Path.SequenceEqual(expectedPath) || !CanEdit)
            return;
        string selection = args.Value?.ToString() ?? string.Empty;
        _selfRenderRequested = true;
        if (selection.Length == 0)
        {
            _invalid = !Form.EditValue(EditorPath, null);
            return;
        }
        if (HasChoices)
        {
            if (!int.TryParse(selection, NumberStyles.None, CultureInfo.InvariantCulture, out int index) ||
                index < 0 || index >= Choices.GetArrayLength())
                return;
            _invalid = !Form.SetValue(EditorPath, Choices[index]);
            if (IsMasked)
            {
                ++_controlGeneration;
                _focusAfterAction = true;
            }
            return;
        }
        _invalid = !Form.EditValue(EditorPath, selection);
    }

    private void ToggleSwitch(DefinitionFormModel expectedForm, IReadOnlyList<string> expectedPath,
        long expectedRevision, long expectedControlGeneration)
    {
        if (_disposed || expectedControlGeneration != _controlGeneration || !ReferenceEquals(Form, expectedForm) ||
            !IsSynchronizedRevision(expectedRevision) || !Path.SequenceEqual(expectedPath) || !CanEdit)
            return;
        string next = Value is { ValueKind: JsonValueKind.True } ? "false" : "true";
        _selfRenderRequested = true;
        _invalid = !Form.EditValue(EditorPath, next);
    }

    private void OmitCurrent(DefinitionFormModel form, IReadOnlyList<string> path, long revision, long generation)
    {
        if (!IsCurrentAction(form, path, revision, generation)) return;
        if (!Form.EditValue(EditorPath, null)) return;
        _selfRenderRequested = true;
        _invalid = false;
        _focusAfterAction = true;
    }

    private void Reset(DefinitionFormModel form, IReadOnlyList<string> path, long revision, long generation)
    {
        if (!IsCurrentAction(form, path, revision, generation, requiresEdit: false) || !CorrectionFacts.CanReset) return;
        Form.ResetValue(EditorPath);
        _selfRenderRequested = true;
        _invalid = false;
    }

    private void SetSecretRevealed(bool revealed)
    {
        _selfRenderRequested = true;
        if (IsMultilineSecret && revealed != _secretRevealed)
        {
            ++_controlGeneration;
            if (revealed) _focusAfterAction = true;
        }
        _secretRevealed = revealed;
    }

    private Task FocusControlAsync() => !CanEdit || string.IsNullOrEmpty(_controlElement.Id)
        ? Task.CompletedTask : _controlElement.FocusAsync(preventScroll: true).AsTask();

    private Task HandleControlKeyDownAsync(KeyboardEventArgs args) =>
        !args.CtrlKey && !args.MetaKey && !args.AltKey && args.Key is "/" or "?"
            ? Task.CompletedTask
            : (OnWorkspaceKeyDown?.Invoke(args) ?? Task.CompletedTask);

    private void NotifyFocused() => OnFieldFocused?.Invoke(EditorPath);

    private async Task CopyWireKeyAsync()
    {
        if (_disposed || _presentation is not { HasCopyableWireToken: true } presentation) return;
        await using var export = new BrowserArtifactExport(Javascript);
        byte[] bytes = Encoding.UTF8.GetBytes(presentation.WireToken);
        try
        {
            export.SetOutput(bytes);
            await export.CopyAsync();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    private async Task OpenHelpAsync()
    {
        if (_disposed || _helpOpening) return;
        _helpOpening = true;
        long generation = _helpGeneration;
        try
        {
            var dialog = await ConfigurationFieldHelp.OpenInspectorAsync(
                Dialogs, Schema.Keywords, Form.Locale, _presentation, Title, FieldPathLabel,
                null, null, null, GetBindingsDelegate);
            if (_disposed || generation != _helpGeneration) dialog.Close();
            else _helpDialog = dialog;
        }
        finally
        {
            _helpOpening = false;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        ++_controlGeneration;
        ++_actionGeneration;
        ++_helpGeneration;
        _helpDialog?.Close();
        _helpDialog = null;
        _invalid = false;
        _secretRevealed = false;
        _rootInput = null;
        _verifiedScalarForm = null;
        _renderedForm = null;
        _controlElement = default;
        GC.SuppressFinalize(this);
    }
}
