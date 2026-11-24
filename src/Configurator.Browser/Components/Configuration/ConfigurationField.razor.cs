namespace Configurator.Browser.Components.Configuration;

using System.Text.Json;
using Configurator.Browser.Components.Common;
using Configurator.Browser.Resources;
using Configurator.Core.Composition;
using Configurator.Core.Definitions;
using Configurator.Presentation;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using MudBlazor;

public partial class ConfigurationField
{
    [Inject] private IJSRuntime Javascript { get; set; } = null!;
    [Parameter, EditorRequired] public DefinitionFormModel Form { get; set; } = null!;
    [CascadingParameter(Name = "WorkspaceConsentAction")] public EventCallback RequestSecretConsent { get; set; }
    [Parameter, EditorRequired] public DefinitionNode Schema { get; set; } = null!;
    [Parameter, EditorRequired] public ConfigurationMountNode MountNode { get; set; } = null!;
    [Parameter, EditorRequired] public long MountRevision { get; set; } = -1;
    [Parameter, EditorRequired] public IReadOnlyList<string> Path { get; set; } = [];
    [Parameter] public bool LiteralPath { get; set; }
    [CascadingParameter(Name = "OnFieldFocused")] private Action<DefinitionPath>? OnFieldFocused { get; set; }
    [CascadingParameter(Name = "OnWorkspaceKeyDown")] private Func<KeyboardEventArgs, Task>? OnWorkspaceKeyDown { get; set; }
    [CascadingParameter(Name = "SuggestionReviewRequested")] private EventCallback<DefinitionPath> SuggestionReviewRequested { get; set; }
    [Parameter] public DefinitionValidationResult? Validation { get; set; }
    [Parameter] public EventCallback<(string Id, string FieldId, bool Invalid)> ValidationChanged { get; set; }
    [Parameter] public bool InheritedSecret { get; set; }
    [Parameter] public bool? RequiredOverride { get; set; }
    [Parameter] public bool SectionAllRequired { get; set; }
    [Parameter] public string? TitleOverride { get; set; }
    [Parameter] public bool FocusFirstControl { get; set; }
    [Parameter] public long FocusGeneration { get; set; }
    [Parameter] public int FocusRow { get; set; } = -1;
    [Parameter] public IReadOnlyList<string> FocusPath { get; set; } = [];
    private ElementReference _addButton;
    private ElementReference _group;
    private ElementReference _radioGroup;
    private MudSelect<string>? _choiceControl;
    private MudSelect<string>? _variantControl;
    private MudTextField<string>? _mapKeyControl;
    private AppTextField? _textControl;
    private bool _focusedForRequest;
    private long _observedFocusGeneration;
    private int _focusNewRow = -1;
    private bool _focusObjectChildren;
    private bool _focusAdd;
    private bool _focusAfterAction;
    private bool _invalid;
    private bool _disposed;
    private bool _controlMounted;
    private bool _radiosMounted;
    private long _controlGeneration;
    private long _lastInputRevision = -1;
    private DefinitionFormModel? _rootForm;
    private DefinitionFieldInput? _rootInput;
    private DefinitionFormModel? _parameterForm;
    private DefinitionFormProjection? _parameterProjection;
    private long _parameterInputRevision = -1;
    private string? _parameterLocale;
    private bool _suppressParameterRender;
    private DefinitionEditorPresentation? _presentation;
    private bool _actionsMounted;
    private long _actionGeneration;
    private string _newMapKey = "";
    private bool _invalidMapKey;
    private string? _focusMapEntry;
    private long _mapFocusGeneration;
    private string? _pendingFocusIdentity;
    private long _pendingFocusRevision = -1;
    private DefinitionSessionPrivacy _lastPrivacy;
    private bool _secretRevealed;
    private long _secretRevision = -1;

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
            && !Changed(nameof(MountNode), MountNode)
            && !Changed(nameof(MountRevision), MountRevision)
            && !(parameters.TryGetValue<DefinitionNode>(nameof(Schema), out var suppliedSchema) && !ReferenceEquals(Schema, suppliedSchema))
            && !PathChanged(nameof(Path), Path)
            && !Changed(nameof(LiteralPath), LiteralPath)
            && !Changed(nameof(Validation), Validation)
            && !Changed(nameof(ValidationChanged), ValidationChanged)
            && !Changed(nameof(RequestSecretConsent), RequestSecretConsent)
            && !Changed(nameof(InheritedSecret), InheritedSecret)
            && !Changed(nameof(RequiredOverride), RequiredOverride)
            && !Changed(nameof(TitleOverride), TitleOverride)
            && !Changed(nameof(FocusFirstControl), FocusFirstControl)
            && !Changed(nameof(FocusGeneration), FocusGeneration)
            && !Changed(nameof(FocusRow), FocusRow)
            && !PathChanged(nameof(FocusPath), FocusPath)
            && !Changed(nameof(SectionAllRequired), SectionAllRequired);
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

    // Cascading consent callbacks can arrive before the owner reconciles row paths.
    // Only parameters belonging to the current input revision may render that tree.
    protected override bool ShouldRender() => _selfRenderRequested || (!_suppressParameterRender && !_scalarRenderSuppressed && MountRevision == Form.InputRevision);

    private DefinitionPath EditorLocation(IReadOnlyList<string> path) => DefinitionPath.FromSegments(LiteralPath ? path
        : new[] { Form.GetFieldEditor(path[0]).Input.Definition.Name }.Concat(path.Skip(1)));
    private DefinitionPath EditorPath => MountNode.Location;
    private DefinitionFieldValidation CorrectionFacts => Form.GetFieldValidation(EditorPath);
    private bool CanEdit => CorrectionFacts.CanEdit;
    private bool IsRetainedByProfile => Form.BrowserProfile?.RetainedPaths.Any(retained =>
        retained.Segments.Count == 0 || EditorPath.Segments.Count == 0 ||
        retained.Segments[0] == EditorPath.Segments[0]) == true;
    private bool IsVisible => Form.IsEditorVisible(EditorPath);
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
    private string ControlId => MountNode.Identity;
    private string Ui(string key) => BrowserStrings.Get(key, Form.Locale);
    private string UiFormat(string key, params object[] values) =>
        string.Format(System.Globalization.CultureInfo.GetCultureInfo(Form.Locale), Ui(key), values);
    private string Type => Schema.JsonType;
    private string Title => TitleOverride ?? _presentation?.Title ?? (Schema.Keywords.TryGetProperty("title", out var value) ? value.GetString()! : Ui("FieldItem"));
    private string? Help => _presentation?.Instructions ?? _presentation?.Description
        ?? (Schema.Keywords.TryGetProperty("description", out var value) ? value.GetString() : null);
    private DefinitionNode ObjectSchema => Value is { } value ? DefinitionValidator.SelectVariant(Schema, value) ?? Schema : Schema;
    private string VariantSelection
    {
        get
        {
            if (Value is not { } value || DefinitionValidator.SelectVariant(Schema, value) is not { } selected) return "";
            for (int index = 0; index < Schema.Variants.Count; index++)
                if (ReferenceEquals(Schema.Variants[index], selected)) return ChoiceIdentity(index);
            return "";
        }
    }
    private bool HasHelp(bool isMasked) => !string.IsNullOrWhiteSpace(Help) || IsMultilineSecret
        || InputPurposeHelp is not null || isMasked && Type == "boolean";
    private bool Required => Path.Count == 0 || (Form.GetEditorState(EditorPath)?.IsRequired ?? RequiredOverride ?? false);
    private bool HasOptionalChildren => ObjectSchema.HasOptionalDescendant;
    private bool OptionalEditorsExpanded => Form.IsEditorExpanded(EditorPath);
    private void ExpandEditor() => Form.SetEditorExpanded(EditorPath, true);
    private void ToggleOptionalEditors() => Form.SetEditorExpanded(EditorPath, !OptionalEditorsExpanded);
    private bool HasChoices => Schema.Keywords.TryGetProperty("enum", out _);
    private JsonElement Choices => Schema.Keywords.GetProperty("enum");
    private bool UseSwitch => !IsMasked && !HasChoices && Type == "boolean"
        && _presentation?.Widget == DefinitionFieldWidget.Switch;
    private bool UseRadios => !IsMasked && (
        (!HasChoices && Type == "boolean" && _presentation?.Widget != DefinitionFieldWidget.Switch) ||
        (HasChoices && _presentation?.Widget == DefinitionFieldWidget.Radio));
    private bool UseSlider => !IsMasked && Type is "integer" or "number"
        && _presentation?.Widget == DefinitionFieldWidget.Slider && TryGetSliderBounds(out _, out _, out _);
    private bool IsMultilineWidget => !IsMasked && Type == "string"
        && _presentation?.Widget is DefinitionFieldWidget.MultiLine or DefinitionFieldWidget.Markdown or DefinitionFieldWidget.Code;
    private int ResolvedRows => _presentation?.Rows is > 0 and <= 64 ? _presentation.Rows.Value : 8;
    private bool IsDynamicPrivateMapEntry => IsSecret && Path.Count > 1 && Form.GetEditorSchema(EditorPath)?.Keywords is not null &&
        _presentation?.HasCopyableWireToken == false && Path[^1] == TitleOverride;
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
    private bool TryGetSliderBounds(out double min, out double max, out double step)
    {
        min = 0; max = 100; step = Type == "integer" ? 1 : 0.1;
        bool hasMin = Schema.Keywords.TryGetProperty("minimum", out var minEl) && minEl.TryGetDouble(out min);
        bool hasMax = Schema.Keywords.TryGetProperty("maximum", out var maxEl) && maxEl.TryGetDouble(out max);
        if (!hasMin || !hasMax || max <= min) return false;
        if (Schema.Keywords.TryGetProperty("multipleOf", out var stepEl) && stepEl.TryGetDouble(out double declared) && declared > 0)
            step = declared;
        return true;
    }
    private string? ArrayItemTitle(int index)
    {
        if (_presentation?.ItemTitlePointer is not { } pointer) return null;
        if (Value is not { ValueKind: JsonValueKind.Array } array || index < 0 || index >= array.GetArrayLength()) return null;
        JsonElement current = array[index];
        foreach (string segment in DefinitionPath.Parse(pointer).Segments)
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out JsonElement next)) return null;
            current = next;
        }
        if (current.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined or JsonValueKind.Object or JsonValueKind.Array) return null;
        string text = Display(current);
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }
    private bool IsDuplicateArrayItem(int row)
    {
        if (_presentation?.UniqueBy is not { Count: > 0 } keys) return false;
        if (Value is not { ValueKind: JsonValueKind.Array } array) return false;
        int len = array.GetArrayLength();
        if (row < 0 || row >= len) return false;
        string? KeyFor(int idx)
        {
            JsonElement item = array[idx];
            if (item.ValueKind != JsonValueKind.Object) return null;
            var parts = new string[keys.Count];
            for (int k = 0; k < keys.Count; k++)
            {
                JsonElement cur = item;
                foreach (string seg in keys[k].Segments)
                {
                    if (cur.ValueKind != JsonValueKind.Object || !cur.TryGetProperty(seg, out JsonElement nxt)) return null;
                    cur = nxt;
                }
                if (cur.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return null;
                parts[k] = cur.ValueKind == JsonValueKind.String ? cur.GetString()! : cur.GetRawText();
            }
            return string.Join('\u001F', parts);
        }
        string? target = KeyFor(row);
        if (target is null) return false;
        int matches = 0;
        for (int i = 0; i < len; i++)
            if (string.Equals(KeyFor(i), target, StringComparison.Ordinal) && ++matches > 1) return true;
        return false;
    }
    private void NotifyFocused() => OnFieldFocused?.Invoke(EditorPath);
    private Dictionary<string, object> BuildInputAttributes(bool canEdit, bool hasError)
    {
        var attributes = new Dictionary<string, object>
        {
            ["name"] = ControlId,
            ["autocomplete"] = Autocomplete(canEdit),
            ["spellcheck"] = "false",
            ["aria-invalid"] = hasError.ToString().ToLowerInvariant(),
            ["aria-describedby"] = ControlId + "-help " + ControlId + "-error" + (Schema.Secret is null ? "" : " " + ControlId + "-rules")
        };
        if (!IsMasked && !string.IsNullOrWhiteSpace(_presentation?.Placeholder))
            attributes["placeholder"] = _presentation.Placeholder;
        if (!IsMasked && _presentation?.Widget is DefinitionFieldWidget.MultiLine or DefinitionFieldWidget.Markdown or DefinitionFieldWidget.Code)
        {
            attributes["data-widget"] = _presentation.Widget switch
            {
                DefinitionFieldWidget.MultiLine => "multiline",
                DefinitionFieldWidget.Markdown => "markdown",
                DefinitionFieldWidget.Code => "code",
                _ => "auto"
            };
            if (_presentation.Widget == DefinitionFieldWidget.Code && !string.IsNullOrWhiteSpace(_presentation.Syntax))
                attributes["data-syntax"] = _presentation.Syntax;
        }
        return attributes;
    }
    private async Task CopyWireKeyAsync()
    {
        if (_disposed || _presentation is not { HasCopyableWireToken: true } presentation) return;
        await using var export = new Configurator.Browser.Services.BrowserArtifactExport(Javascript);
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(presentation.WireToken);
        try
        {
            export.SetOutput(bytes);
            await export.CopyAsync();
        }
        finally
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(bytes);
        }
    }
    private static string ChoiceIdentity(int index) => index.ToString(System.Globalization.CultureInfo.InvariantCulture);
    private string ChoiceLabel(int index, JsonElement value)
    {
        string label = _presentation?.Choices[index].Label ?? Display(value);
        return label.Length == 0 ? Ui("EmptyString") : label;
    }
    private string SelectionLabel(string? identity)
    {
        if (string.IsNullOrEmpty(identity)) return Ui(IsMasked && HasChoices
            ? Value is null ? "MaskedChoiceAbsent" : "MaskedChoiceReplace" : "NotSupplied");
        if (!HasChoices) return Ui(identity == "true" ? "Yes" : "No");
        int index = int.Parse(identity, System.Globalization.CultureInfo.InvariantCulture);
        return ChoiceLabel(index, Choices[index]);
    }
    private string Selection
    {
        get
        {
            if (Value is not { } value) return "";
            if (!HasChoices) return Display(value);
            int index = 0;
            foreach (JsonElement choice in Choices.EnumerateArray())
            {
                if (JsonElement.DeepEquals(choice, value)) return ChoiceIdentity(index);
                index++;
            }
            return "";
        }
    }
    private static bool DeclaresSecret(DefinitionNode schema) => DefinitionSensitivityPolicy.DeclaresSecret(schema);
    private bool IsSecret => InheritedSecret || DeclaresSecret(Schema)
        || Path.Count == 1 && DeclaresSecret(Form.Definition.Configuration);
    private bool IsMultilineSecret => Schema.Secret is { Kind: DefinitionSecretKind.PrivateKey, Encoding: DefinitionSecretEncoding.Pkcs8Pem };
    private bool SecretsEnabled => Form.SessionPrivacy == DefinitionSessionPrivacy.BrowserSecretsEnabled;
    private bool IsMasked => CorrectionFacts.RequiresMasking;
    private int MaximumItems => Schema.Keywords.TryGetProperty("maxItems", out var value) ? value.GetInt32() : CompositionLimits.DefaultSequenceEntries;
    private int Count => Value is { ValueKind: JsonValueKind.Array } value ? value.GetArrayLength() : 0;
    private string Text => TextFor(CanEdit);
    private string TextFor(bool canEdit) => !canEdit ? string.Empty : !LiteralPath && Path.Count == 1
        ? _rootInput!.Text
        : Form.ReadDraftText(EditorPath) ?? (Value is { } value ? Display(value) : string.Empty);
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
    private DefinitionNode ItemSchema => Form.GetListItemMetadata(EditorPath);
    private JsonElement? Value
    {
        get
        {
            if (LiteralPath)
                return Form.Validate().TryGetEffectiveValue(EditorPath, out var effective) ? effective : null;
            var input = _rootInput!;
            if (!input.TryValue(out JsonElement? supplied) || supplied is not { } value) return null;
            foreach (string segment in Path.Skip(1))
            {
                if (value.ValueKind == JsonValueKind.Object && value.TryGetProperty(segment, out var child)) value = child;
                else if (value.ValueKind == JsonValueKind.Array && int.TryParse(segment, out int index) && index >= 0 && index < value.GetArrayLength()) value = value[index];
                else return null;
            }
            return value;
        }
    }
    private bool _scalarRenderSuppressed;
    private bool _selfRenderRequested;
    private long _verifiedScalarInputRevision = -1;
    private long _verifiedScalarStampEpoch = -1;
    private DefinitionFormModel? _verifiedScalarForm;
    private DefinitionValidationResult? _verifiedScalarValidation;
    private long _verifiedRootScalarEpoch = -1;
    private long _renderedInputRevision = -1;
    private long _scalarSynchronizedRevision = -1;
    private DefinitionFormModel? _renderedScalarForm;
    private DefinitionScalarFieldStamp _renderedScalarStamp;
    private DefinitionNode? _renderedSchema;
    private DefinitionValidationResult? _renderedValidation;
    private string? _renderedMountIdentity;
    private DefinitionPath? _renderedMountLocation;
    private bool _renderedMountAdmitted;
    private bool _renderedSectionAllRequired;
    private bool? _renderedRequiredOverride;
    private bool _renderedInheritedSecret;
    private string? _renderedTitleOverride;
    private bool _renderedFocusFirstControl;
    private long _renderedFocusGeneration;
    private string[] _renderedFocusPath = [];
    private string[] _renderedPath = [];
    private string _fieldPathKey = string.Empty;
    private Dictionary<string, string[]>? _childPaths;
    private string FieldPathKey => _renderedPath.Length == Path.Count && _fieldPathKey.Length > 0 ? _fieldPathKey : string.Join('.', Path);
    private IReadOnlyList<string> CurrentPathSnapshot => _renderedPath.Length == Path.Count ? _renderedPath : Path.ToArray();

    private string[] ChildPath(string segment)
    {
        if (_renderedPath.Length == Path.Count)
        {
            _childPaths ??= new Dictionary<string, string[]>(StringComparer.Ordinal);
            if (_childPaths.TryGetValue(segment, out string[]? cached)) return cached;
            var cachedNext = new string[_renderedPath.Length + 1];
            for (int index = 0; index < _renderedPath.Length; index++) cachedNext[index] = _renderedPath[index];
            cachedNext[_renderedPath.Length] = segment;
            _childPaths[segment] = cachedNext;
            return cachedNext;
        }
        var next = new string[Path.Count + 1];
        for (int index = 0; index < Path.Count; index++) next[index] = Path[index];
        next[Path.Count] = segment;
        return next;
    }
    private Func<(IReadOnlyList<string> ArtifactIds, IReadOnlyList<string> BindingKeys)>? _getBindingsDelegate;
    private Func<(IReadOnlyList<string> ArtifactIds, IReadOnlyList<string> BindingKeys)> GetBindingsDelegate =>
        _getBindingsDelegate ??= InspectorBindings;

    private bool CanSkipScalarRender()
    {
        if (!_renderedScalarStamp.SupportsSkip || _selfRenderRequested || LiteralPath || Path.Count == 0 ||
            Schema.Secret is not null || HasChoices ||
            MountNode is null || !MountNode.Admitted || MountNode.Children.Count > 0 ||
            !ReferenceEquals(_renderedScalarForm, Form) ||
            !ReferenceEquals(_renderedSchema, Schema) ||
            _renderedMountIdentity != MountNode.Identity ||
            _renderedMountAdmitted != MountNode.Admitted ||
            !Equals(_renderedMountLocation, MountNode.Location) ||
            _renderedSectionAllRequired != SectionAllRequired ||
            _renderedRequiredOverride != RequiredOverride ||
            _renderedInheritedSecret != InheritedSecret ||
            !string.Equals(_renderedTitleOverride, TitleOverride, StringComparison.Ordinal) ||
            _renderedFocusFirstControl != FocusFirstControl ||
            PathChangedFrom(_renderedPath, Path))
            return false;
        if (FocusFirstControl && (_renderedFocusGeneration != FocusGeneration || PathChangedFrom(_renderedFocusPath, FocusPath)))
        {
            _verifiedScalarForm = null;
            return false;
        }
        if (Schema.Secret is not null && (_renderedValidation is null) != (Validation is null))
        {
            _verifiedScalarForm = null;
            return false;
        }
        if (ReferenceEquals(_verifiedScalarForm, Form) &&
            ReferenceEquals(_verifiedScalarValidation, Validation))
        {
            if (Path.Count == 1 && _verifiedRootScalarEpoch > 0 && Form.GetRootScalarEpoch(Path[0]) == _verifiedRootScalarEpoch)
                return true;
            if (Form.ScalarStampEpoch == _verifiedScalarStampEpoch &&
                Form.InputRevision == _verifiedScalarInputRevision)
                return true;
        }
        if (!ReferenceEquals(_renderedValidation, Validation) &&
            !SameDiagnosticsForField(Path[0], _renderedValidation, Validation))
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
            _verifiedScalarValidation = Validation;
            return true;
        }
        _verifiedScalarForm = null;
        return false;
    }

    internal static bool PathChangedFrom(string[] previous, IReadOnlyList<string> current)
    {
        if (previous.Length != current.Count) return true;
        for (int i = 0; i < previous.Length; i++)
            if (!string.Equals(previous[i], current[i], StringComparison.Ordinal)) return true;
        return false;
    }

    internal static bool SameDiagnosticsForField(string fieldId, DefinitionValidationResult? previous, DefinitionValidationResult? current)
    {
        IReadOnlyList<DefinitionDiagnostic> a = previous?.DiagnosticsForField(fieldId) ?? [];
        IReadOnlyList<DefinitionDiagnostic> b = current?.DiagnosticsForField(fieldId) ?? [];
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++)
        {
            DefinitionDiagnostic left = a[i];
            DefinitionDiagnostic right = b[i];
            if (!string.Equals(left.Code, right.Code, StringComparison.Ordinal) ||
                !string.Equals(left.FieldId, right.FieldId, StringComparison.Ordinal) ||
                !SameStringList(left.RelativePath, right.RelativePath) ||
                !SameLocationSegments(left.Location, right.Location))
                return false;
        }
        return true;
    }

    private static bool SameStringList(IReadOnlyList<string> left, IReadOnlyList<string> right)
    {
        if (ReferenceEquals(left, right)) return true;
        if (left.Count != right.Count) return false;
        for (int i = 0; i < left.Count; i++)
        {
            if (!string.Equals(left[i], right[i], StringComparison.Ordinal))
                return false;
        }
        return true;
    }

    private static bool SameLocationSegments(DefinitionCorrectionLocation? left, DefinitionCorrectionLocation? right)
    {
        if (ReferenceEquals(left, right)) return true;
        if (left is null || right is null) return false;
        IReadOnlyList<DefinitionLocationSegment> a = left.Segments;
        IReadOnlyList<DefinitionLocationSegment> b = right.Segments;
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++)
        {
            if (!a[i].Equals(b[i]))
                return false;
        }
        return true;
    }

    private bool IsSynchronizedRevision(long expectedRevision) =>
        Form.InputRevision == expectedRevision ||
        (_scalarRenderSuppressed && expectedRevision == _renderedInputRevision && Form.InputRevision == _scalarSynchronizedRevision);

    protected override void OnParametersSet()
    {
        // A field first mounted by a stale plan still renders once; bind its root input before deferring the rest.
        if (!LiteralPath && _rootInput is null) _rootInput = Form.GetFieldEditor(Path[0]).Input;
        if (MountRevision != Form.InputRevision)
        {
            _scalarRenderSuppressed = false;
            _scalarSynchronizedRevision = -1;
            return;
        }
        if (_suppressParameterRender)
            return;
        if (CanSkipScalarRender())
        {
            _scalarRenderSuppressed = true;
            _scalarSynchronizedRevision = Form.InputRevision;
            return;
        }
        _scalarRenderSuppressed = false;
        if (_pendingFocusIdentity is not null && (_pendingFocusRevision != Form.InputRevision ||
            !ReferenceEquals(_rootForm, Form) || _lastPrivacy != Form.SessionPrivacy ||
            !ConfigurationMountPlan.Descendants([MountNode]).Any(node => node.Identity == _pendingFocusIdentity)))
            ClearPendingFocus();
        if (!ReferenceEquals(_rootForm, Form) || _secretRevision != Form.InputRevision || _lastPrivacy != Form.SessionPrivacy)
        {
            if (_secretRevealed && IsMultilineSecret) ++_controlGeneration;
            _secretRevealed = false;
        }
        _secretRevision = Form.InputRevision;
        _presentation = Form.GetEditorPresentation(EditorPath);
        var correctionFacts = CorrectionFacts;
        bool canEdit = correctionFacts.CanEdit;
        bool visible = IsVisible;
        bool actionsMounted = visible && (canEdit || correctionFacts.CanReset);
        if (_actionsMounted != actionsMounted)
        {
            _actionsMounted = actionsMounted;
            ++_actionGeneration;
        }
        if (!actionsMounted || !ReferenceEquals(_rootForm, Form) || _lastPrivacy != Form.SessionPrivacy)
        {
            _newMapKey = "";
            _focusMapEntry = null;
        }
        _lastPrivacy = Form.SessionPrivacy;
        bool controlMounted = visible && canEdit && Type is not ("object" or "array");
        bool radiosMounted = controlMounted && !correctionFacts.RequiresMasking
            && !HasChoices && Type == "boolean";
        if (_controlMounted != controlMounted || _radiosMounted != radiosMounted)
        {
            _controlMounted = controlMounted;
            _radiosMounted = radiosMounted;
            ++_controlGeneration;
        }
        if (!LiteralPath && (!ReferenceEquals(_rootForm, Form) || _rootInput?.Definition.Id != Path[0]))
        {
            _rootForm = Form;
            _rootInput = Form.GetFieldEditor(Path[0]).Input;
        }
        if (!FocusFirstControl || _observedFocusGeneration != FocusGeneration) _focusedForRequest = false;
        _observedFocusGeneration = FocusGeneration;
        _rootForm = Form;
        bool invalid = canEdit && Form.HasInputError(EditorPath);
        if (_invalid != invalid)
        {
            _invalid = invalid;
            _ = NotifyValidityAsync();
        }
        _renderedInputRevision = Form.InputRevision;
        _verifiedScalarForm = Form;
        _verifiedScalarStampEpoch = Form.ScalarStampEpoch;
        _verifiedScalarInputRevision = _renderedInputRevision;
        _verifiedRootScalarEpoch = !LiteralPath && Path.Count == 1 ? Form.GetRootScalarEpoch(Path[0]) : -1L;
        _verifiedScalarValidation = Validation;
        _scalarSynchronizedRevision = Form.InputRevision;
        _renderedScalarForm = Form;
        _renderedSchema = Schema;
        _renderedValidation = Validation;
        _renderedMountIdentity = MountNode.Identity;
        _renderedMountLocation = MountNode.Location;
        _renderedMountAdmitted = MountNode.Admitted;
        _renderedSectionAllRequired = SectionAllRequired;
        _renderedRequiredOverride = RequiredOverride;
        _renderedInheritedSecret = InheritedSecret;
        _renderedTitleOverride = TitleOverride;
        _renderedFocusFirstControl = FocusFirstControl;
        _renderedFocusGeneration = FocusGeneration;
        _renderedFocusPath = FocusFirstControl ? (FocusPath.Count == 0 ? [] : FocusPath.ToArray()) : [];
        if (_renderedPath.Length != Path.Count || PathChangedFrom(_renderedPath, Path))
        {
            _renderedPath = Path.ToArray();
            _fieldPathKey = string.Join('.', _renderedPath);
            _childPaths?.Clear();
        }
        _renderedScalarStamp = !LiteralPath && MountNode.Admitted && MountNode.Children.Count == 0 &&
            Type is not ("object" or "array")
            ? Form.GetScalarFieldRenderStamp(Path, InheritedSecret)
            : default;
    }
    private bool Edit(ChangeEventArgs args, DefinitionFormModel expectedForm, IReadOnlyList<string> expectedPath,
        long expectedRevision, long expectedControlGeneration)
    {
        if (_disposed || !_controlMounted || _controlGeneration != expectedControlGeneration || !ReferenceEquals(Form, expectedForm)
            || !IsSynchronizedRevision(expectedRevision) || !Path.SequenceEqual(expectedPath)) return false;
        string selection = args.Value?.ToString() ?? "";
        if (selection.Length == 0) { Omit(); return true; }
        if (HasChoices)
        {
            if (!int.TryParse(selection, out int index) || index < 0 ||
                index >= Choices.GetArrayLength()) return false;
            _invalid = !Form.SetValue(EditorPath, Choices[index]);
            if (IsMasked)
            {
                ++_controlGeneration;
                _focusAfterAction = true;
            }
            _ = NotifyValidityAsync();
            return true;
        }
        EditText(selection);
        return true;
    }
    private void EditText(string? suppliedText)
    {
        if (!CanEdit) return;
        _selfRenderRequested = true;
        string text = suppliedText ?? string.Empty;
        _invalid = !Form.EditValue(EditorPath, text);
        _ = NotifyValidityAsync();
    }
    private bool EditText(string? text, DefinitionFormModel expectedForm, IReadOnlyList<string> expectedPath, long expectedRevision,
        long expectedControlGeneration, bool changeOnly = false)
    {
        if (_disposed || !_controlMounted || _controlGeneration != expectedControlGeneration || !ReferenceEquals(Form, expectedForm) || !IsSynchronizedRevision(expectedRevision)
            || !Path.SequenceEqual(expectedPath)) return false;
        if (changeOnly && _lastInputRevision == expectedRevision && Text == text) return false;
        bool keepMultilineVisible = IsMultilineSecret && _secretRevealed;
        EditText(text);
        if (keepMultilineVisible) { _secretRevealed = true; _secretRevision = Form.InputRevision; }
        _lastInputRevision = changeOnly ? -1 : Form.InputRevision;
        return true;
    }
    private Task NotifyValidityAsync() => ValidationChanged.InvokeAsync((ControlId, Path.Count == 0 ? "" : Path[0], _invalid));
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
    private (IReadOnlyList<string> ArtifactIds, IReadOnlyList<string> BindingKeys) InspectorBindings()
    {
        if (!LiteralPath) return Form.GetEditorBindings(Path);
        DefinitionNodeState? state = Form.GetEditorState(EditorPath);
        return state is null || state.FieldId.Length == 0
            ? ([], [])
            : Form.GetEditorBindings([state.FieldId, .. state.RelativePath]);
    }
    private void Omit()
    {
        if (!Form.EditValue(EditorPath, null)) return;
        _selfRenderRequested = true;
        _invalid = false;
        _focusAfterAction = true;
        _ = NotifyValidityAsync();
    }
    private void Add()
    {
        _focusNewRow = Count;
        if (!Form.AddListItem(EditorPath)) _focusNewRow = -1;
        else RememberPendingFocus(DefinitionPath.FromSegments([.. EditorPath.Segments,
            _focusNewRow.ToString(System.Globalization.CultureInfo.InvariantCulture)]));
    }
    private bool IsCurrentAction(DefinitionFormModel form, IReadOnlyList<string> path, long revision, long generation, bool requiresEdit = true) =>
        !_disposed && _actionsMounted && generation == _actionGeneration && ReferenceEquals(Form, form) &&
        IsSynchronizedRevision(revision) && Path.SequenceEqual(path) && (!requiresEdit || CanEdit);

    private void OmitCurrent(DefinitionFormModel form, IReadOnlyList<string> path, long revision, long generation)
    {
        if (IsCurrentAction(form, path, revision, generation)) Omit();
    }
    private void UpdateMapKey(string? key, DefinitionFormModel form, IReadOnlyList<string> path, long revision, long generation)
    {
        if (!IsCurrentAction(form, path, revision, generation)) return;
        _newMapKey = key ?? "";
        _invalidMapKey = false;
    }

    private void Reset(DefinitionFormModel form, IReadOnlyList<string> path, long revision, long generation)
    {
        if (IsCurrentAction(form, path, revision, generation, requiresEdit: false) && CorrectionFacts.CanReset)
        {
            Form.ResetValue(EditorPath);
            _selfRenderRequested = true;
            _invalid = false;
            _ = NotifyValidityAsync();
        }
    }
    private Task Suggest(DefinitionFormModel form, IReadOnlyList<string> path, long revision, long generation)
    {
        if (!IsCurrentAction(form, path, revision, generation) || !CorrectionFacts.CanApplySuggestion) return Task.CompletedTask;
        if (Schema.PresentationMetadata.SuggestedSource is not null)
        {
            if (!SuggestionReviewRequested.HasDelegate) throw new InvalidOperationException("suggestion-review-owner-missing");
            return SuggestionReviewRequested.InvokeAsync(EditorPath);
        }
        Form.ApplySuggestion(EditorPath);
        _selfRenderRequested = true;
        _invalid = false;
        _ = NotifyValidityAsync();
        return Task.CompletedTask;
    }
    private void SupplyNull(DefinitionFormModel form, IReadOnlyList<string> path, long revision, long generation)
    {
        if (IsCurrentAction(form, path, revision, generation) && CorrectionFacts.CanSetNull)
        {
            Form.SetNull(EditorPath);
            _selfRenderRequested = true;
            _invalid = false;
            _ = NotifyValidityAsync();
        }
    }
    private void ChooseVariant(string? choice, DefinitionFormModel form, IReadOnlyList<string> path, long revision, long generation)
    {
        if (IsCurrentAction(form, path, revision, generation) &&
            int.TryParse(choice, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int index))
        {
            Form.SelectVariant(EditorPath, index);
            _ = NotifyValidityAsync();
        }
    }
    private void AddMapEntry(DefinitionFormModel form, IReadOnlyList<string> path, long revision, long generation)
    {
        if (!IsCurrentAction(form, path, revision, generation)) return;
        _invalidMapKey = !Form.AddMapEntry(EditorPath, _newMapKey);
        if (!_invalidMapKey)
        {
            _focusMapEntry = _newMapKey;
            ++_mapFocusGeneration;
            RememberPendingFocus(DefinitionPath.FromSegments([.. EditorPath.Segments, _newMapKey]));
            _newMapKey = "";
        }
        _ = NotifyValidityAsync();
    }
    private void RemoveMapEntry(string name, DefinitionFormModel form, IReadOnlyList<string> path, long revision, long generation)
    {
        if (IsCurrentAction(form, path, revision, generation))
        {
            Form.EditValue(DefinitionPath.FromSegments([.. EditorPath.Segments, name]), null);
            _focusMapEntry = null;
            _ = NotifyValidityAsync();
        }
    }
    private void SupplyObject()
    {
        _focusObjectChildren = true;
        if (!Form.SupplyObject(EditorPath)) _focusObjectChildren = false;
        else RememberPendingFocus(EditorPath);
    }
    private void SupplyList() { if (Form.SupplyList(EditorPath)) _focusAdd = true; }
    private void Remove(int index)
    {
        if (!Form.RemoveListItem(EditorPath, index)) return;
        _focusAdd = true;
    }
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        _selfRenderRequested = false;
        bool pendingFocusMounted = PendingFocusTarget is { Admitted: true };
        if (_focusAfterAction)
        {
            _focusAfterAction = false;
            if (CanEdit && IsVisible)
            {
                if (Type is "object" or "array") await _group.FocusAsync(preventScroll: true);
                else await FocusScalarAsync(preventScroll: true);
            }
        }
        if (_focusObjectChildren && pendingFocusMounted && !CanFocus(Schema, Path))
            await _group.FocusAsync(preventScroll: true);
        if (pendingFocusMounted) ClearPendingFocus();
        if (FocusFirstControl && !_focusedForRequest && CanEdit && Type == "object" && FocusPath.Count == Path.Count)
        {
            _focusedForRequest = true;
            if (_variantControl is not null) await _variantControl.FocusAsync();
            else if (_mapKeyControl is not null) await _mapKeyControl.FocusAsync();
            else await _group.FocusAsync();
        }
        if (FocusFirstControl && !_focusedForRequest && CanEdit && Type != "object" && (Type != "array" || Count == 0))
        {
            _focusedForRequest = true;
            if (Type == "array") await _addButton.FocusAsync();
            else await FocusScalarAsync();
        }
        if (!_focusAdd) return;
        _focusAdd = false;
        await _addButton.FocusAsync(preventScroll: true);
    }
    private async Task FocusScalarAsync(bool preventScroll = false)
    {
        if (UseRadios)
        {
            DefinitionFormModel form = Form;
            long revision = form.InputRevision;
            long generation = _controlGeneration;
            long focusGeneration = FocusGeneration;
            string controlId = ControlId;
            ElementReference element = _radioGroup;
            await using IJSObjectReference module =
                await Javascript.InvokeAsync<IJSObjectReference>("import", "./js/configuration-rendering.js");
            if (_disposed || !_controlMounted || generation != _controlGeneration || focusGeneration != FocusGeneration
                || !ReferenceEquals(form, Form) || revision != form.InputRevision || ControlId != controlId || !UseRadios
                || !IsVisible || !CanEdit) return;
            await module.InvokeVoidAsync("focusRadio", element, preventScroll);
        }
        else if (_textControl is not null && !HasChoices && (Type != "boolean" || IsMasked))
            await _textControl.FocusAsync();
        else if (_choiceControl is not null) await _choiceControl.FocusAsync();
    }
    private bool CanFocus(DefinitionNode schema, IReadOnlyList<string> path)
    {
        if (FocusPath.Count > 0 && !FocusPath.Take(path.Count).SequenceEqual(path)) return false;
        if (!Form.GetFieldValidation(EditorLocation(path)).CanEdit) return false;
        if (schema.Type != DefinitionNodeType.Mapping) return true;
        return schema.Properties.Any(child => CanFocus(child.Value, path.Append(child.Key).ToArray()));
    }
    private void RememberPendingFocus(DefinitionPath location)
    {
        _pendingFocusIdentity = Form.GetEditorIdentity(location);
        _pendingFocusRevision = Form.InputRevision;
    }
    private ConfigurationMountNode? PendingFocusTarget
    {
        get
        {
            if (string.IsNullOrEmpty(_pendingFocusIdentity)) return null;
            ConfigurationMountNode? owner = ConfigurationMountPlan.Descendants([MountNode])
                .FirstOrDefault(node => node.Identity == _pendingFocusIdentity);
            return owner is null ? null : ConfigurationMountPlan.Descendants([owner]).FirstOrDefault(node =>
                Form.IsEditorVisible(node.Location) && Form.GetFieldValidation(node.Location).CanEdit &&
                Form.GetEditorSchema(node.Location)?.Type is not (DefinitionNodeType.Mapping or DefinitionNodeType.Sequence)) ?? owner;
        }
    }
    private IReadOnlyList<string> PendingFocusPath => PendingFocusTarget is { } target
        ? LiteralPath ? target.Location.Segments : new[] { Path[0] }.Concat(target.Location.Segments.Skip(1)).ToArray()
        : [];
    private void ClearPendingFocus()
    {
        _pendingFocusIdentity = null;
        _focusNewRow = -1;
        _focusObjectChildren = false;
        _focusMapEntry = null;
    }
    private static string Display(JsonElement value) => value.ValueKind == JsonValueKind.String ? value.GetString()! : value.GetRawText();

    private Task HandleInputKeyDownAsync(KeyboardEventArgs args) =>
        !args.CtrlKey && !args.MetaKey && !args.AltKey && args.Key is "/" or "?"
            ? Task.CompletedTask
            : (OnWorkspaceKeyDown?.Invoke(args) ?? Task.CompletedTask);

    public void Dispose()
    {
        _disposed = true;
        _newMapKey = "";
        ClearPendingFocus();
        if (_invalid) { _invalid = false; _ = NotifyValidityAsync(); }
        GC.SuppressFinalize(this);
    }
}
