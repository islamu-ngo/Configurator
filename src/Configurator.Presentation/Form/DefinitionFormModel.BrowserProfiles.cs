namespace Configurator.Presentation;

using System.Text.Json;
using System.Text.Json.Nodes;
using Configurator.Core.Definitions;
using Configurator.Core.Profiles;

/// <summary>Safe worker metadata only; an opaque reference grants no independent authority.</summary>
public sealed record BrowserProfileProjection
{
    public BrowserProfileProjection(Guid reference, ProfileProtectionMode mode, IEnumerable<DefinitionPath> retainedPaths)
    {
        Reference = reference;
        Mode = mode;
        RetainedPaths = Array.AsReadOnly(retainedPaths.Distinct().ToArray());
    }

    public Guid Reference { get; }
    public ProfileProtectionMode Mode { get; }
    public IReadOnlyList<DefinitionPath> RetainedPaths { get; }
}

public sealed partial class DefinitionFormModel
{
    private BrowserProfileProjection? _browserProfile;
    private Action? _retireBrowserProfile;
    private Action? _discardBrowserProfileOutput;
    private bool _requiresBrowserProfileComposition;

    public BrowserProfileProjection? BrowserProfile
    {
        get { lock (_gate) return _disposed ? null : _browserProfile; }
    }

    /// <summary>A stripped opening is never evidence that ordinary output is complete, even after worker retirement.</summary>
    public bool RequiresBrowserProfileComposition
    {
        get { lock (_gate) return !_disposed && _requiresBrowserProfileComposition; }
    }

    public bool AdoptBrowserProfile(long expectedRevision, Guid reference, ProfileProtectionMode mode,
        JsonElement publicValues, IReadOnlyList<DefinitionPath> retainedPaths,
        Action retire, Action discardOutput)
    {
        ArgumentNullException.ThrowIfNull(retire);
        ArgumentNullException.ThrowIfNull(discardOutput);
        using var mutation = BeginMutation();
        if (_disposed || InputRevision != expectedRevision ||
            SessionPrivacy != DefinitionSessionPrivacy.BrowserSecretsEnabled) return false;
        if (reference == Guid.Empty || mode is not
            (ProfileProtectionMode.ImportUpgradeOnly or ProfileProtectionMode.InspectableEncrypted) ||
            publicValues.ValueKind != JsonValueKind.Object || retainedPaths.Count > 4096 ||
            retainedPaths.Any(path => path.Segments.Count > 1))
            throw new ProfileException(ProfileFailureCode.InvalidPayload);

        // A public source field may have become protected in the admitted target.
        JsonNode? projection = JsonNode.Parse(publicValues.GetRawText());
        if (DefinitionSecretPolicy.RemoveProtected(Definition.Configuration, projection)) projection = null;
        var values = projection is JsonObject mapping
            ? mapping.Where(pair => Definition.Configuration.Properties.ContainsKey(pair.Key))
                .ToDictionary(pair => pair.Key, pair => JsonSerializer.SerializeToElement(pair.Value), StringComparer.Ordinal)
            : new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        var answers = new AnswerSnapshot(InputRevision, values);
        RetireProfileSession();
        _browserProfile = new(reference, mode, retainedPaths);
        _retireBrowserProfile = retire;
        _discardBrowserProfileOutput = discardOutput;
        _requiresBrowserProfileComposition = true;
        AdoptBrowserProfileAnswers(answers);
        return true;
    }

    /// <summary>Enables ordinary replacement only after the worker consumed its predecessor and authority still matches.</summary>
    public async Task<bool> ReplaceBrowserRetainedValueAsync(DefinitionPath path, JsonElement? replacement,
        bool confirmed, Func<DefinitionPath, CancellationToken, Task<IReadOnlyList<DefinitionPath>>> retire,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(retire);
        BrowserProfileProjection profile;
        long revision;
        long generation;
        using (BeginMutation())
        {
            if (_disposed || !confirmed || cancellationToken.IsCancellationRequested ||
                _browserProfile is not { } current || !BrowserProfileBlocks(path)) return false;
            profile = current;
            revision = InputRevision;
            generation = _importGeneration;
            _discardBrowserProfileOutput?.Invoke();
        }
        IReadOnlyList<DefinitionPath> retained = await retire(path, cancellationToken).ConfigureAwait(false);
        using (BeginMutation(PresentationStrings.Get("ProfileReplace", Locale)))
        {
            if (!ReferenceEquals(profile, _browserProfile) ||
                !ImportIsCurrent(generation, revision, cancellationToken)) return false;
            if (retained.Any(candidate => !profile.RetainedPaths.Contains(candidate)) ||
                retained.Any(candidate => Overlaps(candidate, path)))
                throw new ProfileException(ProfileFailureCode.InvalidPayload);
            _browserProfile = new(profile.Reference, profile.Mode, retained);
            // Permanent retirement is not recorded in undo, including when replacement validation fails.
            OnInputChanged();
            OnPropertyChanged(nameof(BrowserProfile));
            return SetValue(path, replacement);
        }
    }

    /// <summary>Only explicit inspectable-mode disclosure may turn worker answers into ordinary consented edits.</summary>
    public bool AdoptBrowserProfileDisclosure(Guid reference, long expectedRevision, AnswerSnapshot disclosed)
    {
        ArgumentNullException.ThrowIfNull(disclosed);
        using var mutation = BeginMutation();
        if (_disposed || InputRevision != expectedRevision || disclosed.InputRevision != expectedRevision ||
            SessionPrivacy != DefinitionSessionPrivacy.BrowserSecretsEnabled ||
            _browserProfile is not { Mode: ProfileProtectionMode.InspectableEncrypted } profile ||
            profile.Reference != reference) return false;
        AdoptBrowserProfileAnswers(disclosed);
        RetireBrowserProfileSession();
        _requiresBrowserProfileComposition = false;
        OnPropertyChanged(nameof(RequiresBrowserProfileComposition));
        return true;
    }

    public void CloseBrowserProfile()
    {
        using var mutation = BeginMutation();
        if (_disposed || _browserProfile is null) return;
        RetireBrowserProfileSession();
        OnInputChanged();
    }

    private void AdoptBrowserProfileAnswers(AnswerSnapshot answers)
    {
        _undoHistory.Clear();
        NotifyUndoPropertiesChanged();
        _editing.Clear();
        Omitted.Clear();
        Omitted.UnionWith(answers.Omitted);
        foreach ((string name, JsonElement value) in answers.Values)
            if (Fields.FirstOrDefault(field => field.Definition.Name == name) is { } field)
                field.SetImported(value);
            else _editing.Root[name] = value.Clone();
        foreach (DefinitionFieldInput field in Fields) ClearRowIdentities([field.Definition.Id]);
        OnInputChanged();
        OnPropertyChanged(nameof(BrowserProfile));
        OnPropertyChanged(nameof(RequiresBrowserProfileComposition));
    }

    private bool BrowserProfileBlocks(DefinitionPath path) =>
        _browserProfile?.RetainedPaths.Any(retained => Overlaps(retained, path)) == true;

    private static bool Overlaps(DefinitionPath left, DefinitionPath right) =>
        left.Segments.Count == 0 || right.Segments.Count == 0 || left.Segments[0] == right.Segments[0];

    private void RetireBrowserProfileSession()
    {
        Action? retire = _retireBrowserProfile;
        _browserProfile = null;
        _retireBrowserProfile = null;
        _discardBrowserProfileOutput = null;
        retire?.Invoke();
        OnPropertyChanged(nameof(BrowserProfile));
    }
}
