namespace Configurator.Browser.Components.Configuration;

using System.Globalization;
using System.Text;
using Configurator.Browser.Resources;
using Configurator.Core.Definitions;
using Configurator.Presentation;
using Microsoft.AspNetCore.Components;

public sealed record CompactSectionLayout(
    int[] Ungrouped,
    Dictionary<string, int[]> Groups,
    int[] All,
    HashSet<string> FieldIds);

public partial class ConfigurationCompactSection
{
    [Parameter, EditorRequired] public DefinitionFormModel Form { get; set; } = null!;
    [Parameter, EditorRequired] public DefinitionFormSection Section { get; set; } = null!;
    [Parameter, EditorRequired] public DefinitionFormProgress SectionProgress { get; set; } = null!;
    [Parameter, EditorRequired] public CompactSectionLayout Layout { get; set; } = null!;
    [Parameter, EditorRequired] public FieldState[] ActiveFields { get; set; } = [];
    [Parameter, EditorRequired] public IReadOnlyList<ConfigurationMountNode> PlanRoots { get; set; } = [];
    [Parameter] public long PlanRevision { get; set; }
    [Parameter] public DefinitionValidationResult? Validation { get; set; }
    [Parameter] public bool SectionHidden { get; set; }
    [Parameter] public string? FocusField { get; set; }
    [Parameter] public long FocusGeneration { get; set; }
    [Parameter] public IReadOnlyList<string> FocusPath { get; set; } = [];
    [Parameter] public bool ShowMatchesOnly { get; set; }
    [Parameter] public HashSet<string>? MatchedFieldIds { get; set; }
    [Parameter] public string? HeadingIdOverride { get; set; }

    private readonly record struct CompactGroupStamp(
        string Id,
        string Title,
        string? Description,
        bool IsExpanded,
        bool Hidden);

    private readonly record struct CompactSlotStamp(
        string Id,
        bool Admitted,
        bool Hidden,
        bool IsRequired,
        bool IsSecret,
        bool Focused,
        long FocusGeneration,
        string? FocusPathKey,
        long RootScalarEpoch,
        DefinitionScalarFieldStamp ScalarStamp);

    private bool _hasRendered;
    private DefinitionFormModel? _renderedForm;
    private DefinitionFormSection? _renderedSectionRef;
    private IReadOnlyList<ConfigurationMountNode>? _renderedPlanRoots;
    private DefinitionValidationResult? _renderedValidation;
    private HashSet<string>? _renderedMatchedIdsRef;
    private int[]? _renderedLayoutAll;
    private long _renderedPlanRevision = -1;
    private long _renderedInputRevision = -1;
    private long _renderedScalarStampEpoch = -1;
    private long _renderedSectionScalarEpoch = -1;
    private string? _renderedLocale;
    private WorkspaceDensity _renderedDensity;
    private WorkspaceNamingMode _renderedNamingMode;
    private bool _renderedShowMatchesOnly;
    private bool _renderedSectionHidden;
    private string? _renderedSectionId;
    private string? _renderedSectionTitle;
    private string? _renderedSectionDescription;
    private DefinitionFormProgress? _renderedProgress;
    private string? _renderedFocusField;
    private long _renderedFocusGeneration;
    private string? _renderedFocusPathKey;
    private CompactGroupStamp[] _renderedGroups = [];
    private CompactSlotStamp[] _renderedSlots = [];
    private readonly Dictionary<string, string> _cachedFieldTitles = new(StringComparer.Ordinal);
    private DefinitionFormModel? _cachedTitleForm;
    private string? _cachedTitleLocale;
    private WorkspaceNamingMode _cachedTitleNamingMode;

    private string HeadingId => HeadingIdOverride ?? SectionHeadingId(Section.Id);

    internal static string SectionHeadingId(string sectionId) =>
        "section-" + Convert.ToHexString(Encoding.UTF8.GetBytes(sectionId));

    private string Ui(string key) => BrowserStrings.Get(key, Form.Locale);

    private string UiFormat(string key, params object[] values) =>
        string.Format(CultureInfo.GetCultureInfo(Form.Locale), Ui(key), values);

    private string SectionCountText()
    {
        int attention = SectionProgress.Missing + SectionProgress.Invalid;
        if (attention > 0)
            return UiFormat("CompactSectionAttentionCount", attention);
        if (SectionProgress.Required > 0)
            return UiFormat("CompactSectionReadyCount", SectionProgress.Ready, SectionProgress.Required);
        return Ui("CompactSectionOptionalStatus");
    }

    private string SectionCountLabel() => $"{Section.Title}: {SectionCountText()}";

    private bool IsFieldHiddenByFilter(string fieldId) =>
        ShowMatchesOnly && (MatchedFieldIds is null || !MatchedFieldIds.Contains(fieldId));

    private bool IsGroupHiddenByFilter(int[] groupIndices)
    {
        if (!ShowMatchesOnly) return false;
        foreach (int index in groupIndices)
            if (!IsFieldHiddenByFilter(ActiveFields[index].Id))
                return false;
        return true;
    }

    private string ResolveFieldTitle(FieldState state)
    {
        if (!ReferenceEquals(_cachedTitleForm, Form) ||
            !string.Equals(_cachedTitleLocale, Form.Locale, StringComparison.Ordinal) ||
            _cachedTitleNamingMode != Form.WorkspaceNamingMode)
        {
            _cachedTitleForm = Form;
            _cachedTitleLocale = Form.Locale;
            _cachedTitleNamingMode = Form.WorkspaceNamingMode;
            _cachedFieldTitles.Clear();
        }
        if (_cachedFieldTitles.TryGetValue(state.Id, out string? cached)) return cached;
        string resolved = Form.GetEditorPresentation(Form.GetRootFieldPath(state.Id))?.Title ??
            DefinitionDisplayText.Resolve(state.Definition.Title, state.Id);
        _cachedFieldTitles[state.Id] = resolved;
        return resolved;
    }

    protected override bool ShouldRender()
    {
        if (!_hasRendered) return true;
        string? focusPathKey = FocusField is not null && FocusPath.Count > 0 ? string.Join('\0', FocusPath) : null;
        long focusGeneration = FocusField is not null ? FocusGeneration : 0L;
        if (!ReferenceEquals(Form, _renderedForm) ||
            PlanRevision != _renderedPlanRevision ||
            !string.Equals(Form.Locale, _renderedLocale, StringComparison.Ordinal) ||
            Form.WorkspaceDensity != _renderedDensity ||
            Form.WorkspaceNamingMode != _renderedNamingMode ||
            ShowMatchesOnly != _renderedShowMatchesOnly ||
            SectionHidden != _renderedSectionHidden ||
            !string.Equals(Section.Id, _renderedSectionId, StringComparison.Ordinal) ||
            !string.Equals(Section.Title, _renderedSectionTitle, StringComparison.Ordinal) ||
            !string.Equals(Section.Description, _renderedSectionDescription, StringComparison.Ordinal) ||
            !Equals(SectionProgress, _renderedProgress) ||
            !string.Equals(FocusField, _renderedFocusField, StringComparison.Ordinal) ||
            focusGeneration != _renderedFocusGeneration ||
            !string.Equals(focusPathKey, _renderedFocusPathKey, StringComparison.Ordinal) ||
            !ReferenceEquals(Layout.All, _renderedLayoutAll) ||
            _renderedSlots.Length != Layout.All.Length)
            return true;

        if (!ReferenceEquals(Section, _renderedSectionRef) ||
            (ShowMatchesOnly && !ReferenceEquals(MatchedFieldIds, _renderedMatchedIdsRef)))
        {
            if (GroupsChanged()) return true;
        }

        if (Form.InputRevision == _renderedInputRevision &&
            Form.ScalarStampEpoch == _renderedScalarStampEpoch &&
            ReferenceEquals(PlanRoots, _renderedPlanRoots) &&
            ReferenceEquals(Validation, _renderedValidation) &&
            (!ShowMatchesOnly || ReferenceEquals(MatchedFieldIds, _renderedMatchedIdsRef)))
            return false;

        bool validationChanged = !ReferenceEquals(_renderedValidation, Validation);
        bool validationNullChanged = (_renderedValidation is null) != (Validation is null);
        long sectionScalarEpoch = Form.GetSectionScalarEpoch(Section.Id);
        if (sectionScalarEpoch == _renderedSectionScalarEpoch &&
            !validationChanged &&
            ReferenceEquals(PlanRoots, _renderedPlanRoots) &&
            (!ShowMatchesOnly || ReferenceEquals(MatchedFieldIds, _renderedMatchedIdsRef)))
        {
            _renderedSectionRef = Section;
            _renderedInputRevision = Form.InputRevision;
            _renderedScalarStampEpoch = Form.ScalarStampEpoch;
            return false;
        }

        bool inputChanged = Form.InputRevision != _renderedInputRevision;
        for (int i = 0; i < Layout.All.Length; i++)
        {
            int index = Layout.All[i];
            FieldState state = ActiveFields[index];
            ConfigurationMountNode node = PlanRoots[index];
            bool slotHidden = IsFieldHiddenByFilter(state.Id);
            bool focused = string.Equals(FocusField, state.Id, StringComparison.Ordinal);
            ref readonly CompactSlotStamp prev = ref _renderedSlots[i];
            if (!string.Equals(state.Id, prev.Id, StringComparison.Ordinal) ||
                node.Admitted != prev.Admitted ||
                slotHidden != prev.Hidden ||
                state.IsRequired != prev.IsRequired ||
                focused != prev.Focused)
                return true;

            if (focused && (focusGeneration != prev.FocusGeneration || !string.Equals(focusPathKey, prev.FocusPathKey, StringComparison.Ordinal)))
                return true;

            if (node.Admitted)
            {
                if (prev.IsSecret && validationNullChanged)
                    return true;
                if (validationChanged && !ConfigurationField.SameDiagnosticsForField(state.Id, _renderedValidation, Validation))
                    return true;
                if (inputChanged)
                {
                    long rootEpoch = Form.GetRootScalarEpoch(state.Id);
                    if (rootEpoch != prev.RootScalarEpoch)
                    {
                        DefinitionScalarFieldStamp scalarStamp = Form.GetScalarFieldRenderStamp(Form.GetRootFieldPath(state.Id), inheritedSecret: false);
                        if (!scalarStamp.SupportsSkip || !scalarStamp.Equals(prev.ScalarStamp))
                            return true;
                    }
                }
            }
        }

        _renderedSectionRef = Section;
        _renderedPlanRoots = PlanRoots;
        _renderedValidation = Validation;
        _renderedMatchedIdsRef = ShowMatchesOnly ? MatchedFieldIds : null;
        _renderedInputRevision = Form.InputRevision;
        _renderedScalarStampEpoch = Form.ScalarStampEpoch;
        _renderedSectionScalarEpoch = sectionScalarEpoch;
        return false;
    }

    private bool GroupsChanged()
    {
        int matchedIndex = 0;
        foreach (var group in Section.Groups)
        {
            if (!group.IsApplicable) continue;
            if (!Layout.Groups.TryGetValue(group.Id, out int[]? indices) || indices.Length == 0) continue;
            if (matchedIndex >= _renderedGroups.Length) return true;
            var candidate = new CompactGroupStamp(
                group.Id,
                group.Title,
                group.Description,
                group.IsExpanded,
                IsGroupHiddenByFilter(indices));
            if (!candidate.Equals(_renderedGroups[matchedIndex])) return true;
            matchedIndex++;
        }
        return matchedIndex != _renderedGroups.Length;
    }

    private void RecordRenderedState()
    {
        DefinitionFormModel? previousForm = _renderedForm;
        _hasRendered = true;
        _renderedForm = Form;
        _renderedSectionRef = Section;
        _renderedPlanRoots = PlanRoots;
        _renderedValidation = Validation;
        _renderedMatchedIdsRef = ShowMatchesOnly ? MatchedFieldIds : null;
        _renderedLayoutAll = Layout.All;
        _renderedPlanRevision = PlanRevision;
        _renderedInputRevision = Form.InputRevision;
        _renderedScalarStampEpoch = Form.ScalarStampEpoch;
        _renderedSectionScalarEpoch = Form.GetSectionScalarEpoch(Section.Id);
        _renderedLocale = Form.Locale;
        _renderedDensity = Form.WorkspaceDensity;
        _renderedNamingMode = Form.WorkspaceNamingMode;
        _renderedShowMatchesOnly = ShowMatchesOnly;
        _renderedSectionHidden = SectionHidden;
        _renderedSectionId = Section.Id;
        _renderedSectionTitle = Section.Title;
        _renderedSectionDescription = Section.Description;
        _renderedProgress = SectionProgress;
        _renderedFocusField = FocusField;
        _renderedFocusGeneration = FocusField is not null ? FocusGeneration : 0L;
        _renderedFocusPathKey = FocusField is not null && FocusPath.Count > 0 ? string.Join('\0', FocusPath) : null;

        var groups = new List<CompactGroupStamp>(Section.Groups.Count);
        foreach (var group in Section.Groups)
        {
            if (!group.IsApplicable) continue;
            if (!Layout.Groups.TryGetValue(group.Id, out int[]? indices) || indices.Length == 0) continue;
            groups.Add(new CompactGroupStamp(
                group.Id,
                group.Title,
                group.Description,
                group.IsExpanded,
                IsGroupHiddenByFilter(indices)));
        }
        _renderedGroups = groups.ToArray();

        int count = Layout.All.Length;
        bool sameForm = ReferenceEquals(Form, previousForm);
        CompactSlotStamp[] previousSlots = _renderedSlots;
        if (_renderedSlots.Length != count)
            _renderedSlots = new CompactSlotStamp[count];
        for (int i = 0; i < count; i++)
        {
            int index = Layout.All[i];
            FieldState state = ActiveFields[index];
            ConfigurationMountNode node = PlanRoots[index];
            bool focused = string.Equals(FocusField, state.Id, StringComparison.Ordinal);
            long rootEpoch = node.Admitted ? Form.GetRootScalarEpoch(state.Id) : 0L;
            bool canReusePrevious = sameForm &&
                i < previousSlots.Length &&
                string.Equals(previousSlots[i].Id, state.Id, StringComparison.Ordinal) &&
                previousSlots[i].Admitted &&
                node.Admitted &&
                previousSlots[i].RootScalarEpoch == rootEpoch &&
                previousSlots[i].ScalarStamp.SupportsSkip;
            bool isSecret;
            DefinitionScalarFieldStamp scalarStamp;
            if (canReusePrevious)
            {
                isSecret = previousSlots[i].IsSecret;
                scalarStamp = previousSlots[i].ScalarStamp;
            }
            else
            {
                isSecret = Form.GetFieldEditor(state.Id).Schema.Secret is not null;
                scalarStamp = node.Admitted
                    ? Form.GetScalarFieldRenderStamp(Form.GetRootFieldPath(state.Id), inheritedSecret: false)
                    : default;
            }
            _renderedSlots[i] = new CompactSlotStamp(
                state.Id,
                node.Admitted,
                IsFieldHiddenByFilter(state.Id),
                state.IsRequired,
                isSecret,
                focused,
                focused ? FocusGeneration : 0L,
                focused && FocusPath.Count > 0 ? string.Join('\0', FocusPath) : null,
                rootEpoch,
                scalarStamp);
        }
    }
}
