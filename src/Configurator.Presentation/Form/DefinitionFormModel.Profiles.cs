namespace Configurator.Presentation;

using System.Text.Json;
using Configurator.Core.Profiles;
using Configurator.Core.Definitions;

public sealed partial class DefinitionFormModel
{
    private ProfileWorkspaceSession? _profileSession;
    private ProfileCompositionInput? _profileComposition;
    private ProfileArtifact? _profileArtifact;

    public ProfileComparisonProjection? Profile
    {
        get { lock (_gate) return _disposed ? null : _profileSession?.Projection; }
    }

    /// <summary>
    /// Transfers a native unlocked owner only when the captured form and input revision still apply.
    /// Browser retained custody belongs in its dedicated worker, even when ordinary secret entry is enabled.
    /// </summary>
    public async Task<bool> OpenProfileAsync(Func<CancellationToken, Task<UnlockedProfile>> unlock,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(unlock);
        long generation;
        long revision;
        using (BeginMutation())
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (SessionPrivacy != DefinitionSessionPrivacy.NativeComplete || cancellationToken.IsCancellationRequested) return false;
            RetireImportComparison();
            generation = _importGeneration;
            revision = InputRevision;
        }
        UnlockedProfile? profile = null;
        ProfileWorkspaceSession? candidate = null;
        try
        {
            profile = await unlock(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            using (BeginMutation())
            {
                if (!ImportIsCurrent(generation, revision, cancellationToken)) return false;
                candidate = new(profile, Definition);
                IReadOnlyDictionary<string, JsonElement> answers = candidate.OpeningAnswers();
                RetireProfileSession();
                _undoHistory.Clear();
                NotifyUndoPropertiesChanged();
                foreach (DefinitionFieldInput field in Fields) field.Clear();
                _editing.Root.Clear();
                Omitted.Clear();
                foreach ((string name, JsonElement value) in answers)
                    if (Fields.FirstOrDefault(field => field.Definition.Name == name) is { } field)
                        field.SetImported(value);
                    else _editing.Root[name] = value.Clone();
                foreach (DefinitionFieldInput field in Fields) ClearRowIdentities([field.Definition.Id]);
                _profileSession = candidate;
                candidate = null;
                profile = null;
                OnInputChanged();
                OnPropertyChanged(nameof(Profile));
                return true;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return false; }
        finally
        {
            candidate?.Dispose();
            profile?.Dispose();
        }
    }

    /// <summary>Retires an entire retained field before replacement; collection ordinals never identify retained secrets.</summary>
    public bool ReplaceRetainedProfileValue(DefinitionPath path, JsonElement? value, bool confirmed)
    {
        ArgumentNullException.ThrowIfNull(path);
        using var mutation = BeginMutation(PresentationStrings.Get("ProfileReplace", Locale));
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!confirmed || _profileSession is null || !_profileSession.Blocks(path)) return false;
        RetireProfilePreparation();
        if (!_profileSession.Retire(path)) return false;
        // Retirement survives a rejected replacement and every undo/redo operation.
        OnInputChanged();
        OnPropertyChanged(nameof(Profile));
        return SetValue(path, value);
    }

    /// <summary>Prepares private owned output, never the ordinary preview or copy channel.</summary>
    public async Task<IReadOnlyList<string>> PrepareProfileOutputAsync(string artifactId,
        CancellationToken cancellationToken = default)
    {
        ProfileCompositionInput input;
        long generation;
        long revision;
        using (BeginMutation())
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            RetireProfilePreparation();
            if (_profileSession is null) return ["profile-unavailable"];
            _profileComposition = input = _profileSession.Capture(Snapshot());
            generation = _importGeneration;
            revision = InputRevision;
        }
        ProfileArtifactResult result;
        try
        {
            result = await Task.Run(() => ProfileArtifactComposer.Compose(input, artifactId, cancellationToken),
                cancellationToken).ConfigureAwait(false);
        }
        catch (ObjectDisposedException) when (!ProfileInputIsCurrent(input, generation, revision, cancellationToken))
        {
            return ["profile-stale"];
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            using var mutation = BeginMutation();
            if (ReferenceEquals(input, _profileComposition)) RetireProfilePreparation();
            return ["profile-cancelled"];
        }
        using (BeginMutation())
        {
            if (!ProfileInputIsCurrent(input, generation, revision, cancellationToken))
            {
                result.Artifact?.Dispose();
                return ["profile-stale"];
            }
            _profileArtifact = result.Artifact;
            return Array.AsReadOnly(result.Diagnostics.Select(diagnostic => diagnostic.Code).ToArray());
        }
    }

    /// <summary>Seals the scoped current answers; stale encrypted results never become current output.</summary>
    public async ValueTask<ProfileSealedSnapshot?> SealProfileAsync(IProfileContainerCodec codec, Guid newSnapshotId,
        IReadOnlyList<ReadOnlyMemory<byte>> portableRecipientMaterials, Guid? deviceBoundAcknowledgement = null,
        CancellationToken cancellationToken = default)
    {
        ProfileCompositionInput input;
        long generation;
        long revision;
        using (BeginMutation())
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            RetireProfilePreparation();
            if (_profileSession is null) return null;
            _profileComposition = input = _profileSession.Capture(Snapshot());
            generation = _importGeneration;
            revision = InputRevision;
        }
        ProfileSealedSnapshot result;
        try
        {
            result = await ProfileArtifactComposer.SealAsync(input, codec, newSnapshotId,
                portableRecipientMaterials, deviceBoundAcknowledgement, cancellationToken).ConfigureAwait(false);
        }
        catch (ObjectDisposedException) when (!ProfileInputIsCurrent(input, generation, revision, cancellationToken))
        {
            return null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            using var mutation = BeginMutation();
            if (ReferenceEquals(input, _profileComposition)) RetireProfilePreparation();
            return null;
        }
        using (BeginMutation())
        {
            if (ProfileInputIsCurrent(input, generation, revision, cancellationToken)) return result;
            result.Dispose();
            return null;
        }
    }

    /// <summary>
    /// Enters a deliberate restricted native sink. The sink reports physical installation through its own receipt;
    /// this result states whether the form authority still applies after the callback.
    /// </summary>
    public async ValueTask<bool> WriteProfileOutputToNativeSaveAsync(
        Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask> save, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(save);
        ProfileArtifact? artifact;
        ProfileCompositionInput? input;
        long generation;
        long revision;
        using (BeginMutation())
        {
            if (_disposed || _profileArtifact is null || cancellationToken.IsCancellationRequested) return false;
            artifact = _profileArtifact;
            _profileArtifact = null;
            input = _profileComposition;
            generation = _importGeneration;
            revision = InputRevision;
        }
        try
        {
            await artifact.WriteToNativeSaveAsync(save, cancellationToken).ConfigureAwait(false);
            return input is not null && ProfileInputIsCurrent(input, generation, revision, cancellationToken);
        }
        catch (ObjectDisposedException) when (input is null ||
            !ProfileInputIsCurrent(input, generation, revision, cancellationToken))
        {
            return false;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return false; }
        finally { artifact.Dispose(); }
    }

    private bool ProfileBlocks(DefinitionPath path) =>
        _profileSession?.Blocks(path) == true || BrowserProfileBlocks(path);

    private bool ProfileInputIsCurrent(ProfileCompositionInput input, long generation, long revision,
        CancellationToken cancellationToken)
    {
        lock (_gate) return ReferenceEquals(input, _profileComposition) &&
            ImportIsCurrent(generation, revision, cancellationToken);
    }

    private void RetireProfilePreparation()
    {
        _profileComposition?.Dispose();
        _profileComposition = null;
        _profileArtifact?.Dispose();
        _profileArtifact = null;
    }

    private void RetireProfileSession()
    {
        RetireBrowserProfileSession();
        RetireProfilePreparation();
        _profileSession?.Dispose();
        _profileSession = null;
    }
}
