namespace Configurator.Presentation;

using System.Text.Json;
using System.Text.Json.Nodes;
using Configurator.Core.Definitions;

public sealed partial class DefinitionFormModel
{
    private readonly object _transferReviewOwner = new();
    private long _transferReviewGeneration;
    private DefinitionTransitionTransferPlan? _transferReview;

    public DefinitionTransitionTransferPlan ReviewOutputTransition(string targetOption)
    {
        ArgumentNullException.ThrowIfNull(targetOption);
        using var mutation = BeginMutation();
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (Definition.Presentation?.OutputSelector is not { } selector
            || !selector.Options.Any(option => option.Value == targetOption))
            throw new ArgumentException("transfer-option-invalid", nameof(targetOption));
        return CaptureTransferReview(targetOption, null);
    }

    public DefinitionTransitionTransferPlan ReviewSuggestion(DefinitionPath target)
    {
        ArgumentNullException.ThrowIfNull(target);
        using var mutation = BeginMutation();
        ObjectDisposedException.ThrowIf(_disposed, this);
        return CaptureTransferReview(null, target);
    }

    private DefinitionTransitionTransferPlan CaptureTransferReview(string? targetOption, DefinitionPath? inlineTarget)
    {
        RetireTransferReviews();
        AnswerSnapshot original = Snapshot();
        DefinitionValidationResult live = Validate();
        var candidates = new List<DefinitionTransferCandidate>();
        var pending = new Stack<(DefinitionNode Node, DefinitionPath Path)>();
        pending.Push((Definition.Configuration, DefinitionPath.FromSegments([])));
        while (pending.TryPop(out var current))
        {
            if (current.Node.PresentationMetadata.SuggestedSource is { } suggestion
                && (inlineTarget is null || inlineTarget == current.Path)
                && TryTransferValue(current.Path, targetOption, original, live, out _, out bool masking))
            {
                DefinitionPath source = suggestion.Location;
                bool provided = DefinitionDocumentEditing.TryRead(original.Values, current.Path, out _)
                    || original.Omitted.Any(omission => DefinitionDocumentEditing.Prefix(omission, current.Path))
                    || HasTransferDraft(current.Path);
                candidates.Add(new(candidates.Count, source, current.Path,
                    TransferTitle(source), TransferTitle(current.Path), masking, provided, !provided));
            }
            foreach (string name in current.Node.OrderedPropertyNames.Reverse())
                pending.Push((current.Node.Properties[name], DefinitionPath.FromSegments([.. current.Path.Segments, name])));
        }
        return _transferReview = new(_transferReviewOwner, _transferReviewGeneration, Definition.Digest,
            InputRevision, targetOption, candidates);
    }

    /// <summary>Reads only an unmasked source for the exact current review, without retaining it in the descriptor.</summary>
    public JsonElement? ReadTransferPreview(DefinitionTransitionTransferPlan review, int candidateId)
    {
        ArgumentNullException.ThrowIfNull(review);
        lock (_gate)
        {
            if (!TransferReviewIsCurrent(review)) return null;
            DefinitionTransferCandidate? candidate = review.Candidates.FirstOrDefault(item => item.Id == candidateId);
            if (candidate is null || candidate.RequiresMasking) return null;
            return TryTransferValue(candidate.Target, review.TargetOption, Snapshot(), Validate(),
                out JsonElement value, out bool masking) && !masking ? value.Clone() : null;
        }
    }

    /// <summary>Stages all selected leaves and the selector, then adopts once with one revision and undo entry.</summary>
    public DefinitionTransferResult ApplyTransfer(DefinitionTransitionTransferPlan review, IEnumerable<int> candidateIds)
    {
        ArgumentNullException.ThrowIfNull(review);
        ArgumentNullException.ThrowIfNull(candidateIds);
        bool committed = false;
        long acceptedRevision = review.InputRevision;
        DefinitionAcceptedEditingState? replacement = null;
        try
        {
            using var mutation = BeginMutation(PresentationStrings.Get("UndoActionTransfer", Locale));
            if (!TransferReviewIsCurrent(review)) return new(false, "transfer-stale", InputRevision);
            int[] selected = candidateIds.Take(review.Candidates.Count + 1).ToArray();
            if (selected.Length != selected.Distinct().Count()
                || selected.Any(id => !review.Candidates.Any(candidate => candidate.Id == id)))
            {
                RetireTransferReviews();
                return new(false, "transfer-invalid-selection", InputRevision);
            }
            if (selected.Length == 0 && review.TargetOption is null)
                return new(false, "transfer-no-selection", InputRevision);

            AnswerSnapshot original = Snapshot();
            DefinitionValidationResult live = Validate();
            JsonObject staged = JsonSerializer.SerializeToNode(original.Values)!.AsObject();
            var omitted = original.Omitted.ToHashSet();
            StageTransferSelector(staged, omitted, review.TargetOption);
            var changed = new List<DefinitionPath>();
            if (review.TargetOption is not null) changed.Add(Definition.Presentation!.OutputSelector!.Location);
            var transfers = new List<(DefinitionTransferCandidate Candidate, JsonElement Value)>();
            foreach (int id in selected)
            {
                DefinitionTransferCandidate candidate = review.Candidates.Single(item => item.Id == id);
                if (!TryTransferValue(candidate.Target, review.TargetOption, original, live, out JsonElement value, out _))
                {
                    RetireTransferReviews();
                    return new(false, "transfer-unavailable", InputRevision);
                }
                // A leaf overwrite may replace that leaf's draft, but cannot discard an ancestor's unrelated edits.
                for (int length = 0; length < candidate.Target.Segments.Count; length++)
                    if (HasInputError(DefinitionPath.FromSegments(candidate.Target.Segments.Take(length))))
                    {
                        RetireTransferReviews();
                        return new(false, "transfer-draft-conflict", InputRevision);
                    }
                DefinitionDocumentEditing.Set(staged, candidate.Target, value);
                omitted.RemoveWhere(omission => DefinitionDocumentEditing.Overlaps(omission, candidate.Target));
                transfers.Add((candidate, value));
                changed.Add(candidate.Target);
            }
            var accepted = new AnswerSnapshot(InputRevision, DefinitionDocumentEditing.Values(staged), omitted);
            DefinitionValidationResult validation = DefinitionValidator.Validate(Definition, accepted);
            foreach (var transfer in transfers)
                if (validation.ConfigurationContext is { } context && !context.RelevantLocations.Contains(transfer.Candidate.Target)
                    || !validation.TryGetEffectiveValue(transfer.Candidate.Target, out JsonElement effective)
                    || !JsonElement.DeepEquals(effective, transfer.Value))
                {
                    RetireTransferReviews();
                    return new(false, "transfer-inactive", InputRevision);
                }

            replacement = _editing.Capture();
            replacement.Root.Clear();
            foreach ((string name, JsonElement value) in accepted.Values) replacement.Root.Add(name, value);
            replacement.Omitted.Clear();
            replacement.Omitted.UnionWith(accepted.Omitted);
            var changedFields = new HashSet<int>();
            foreach (DefinitionPath path in changed)
            {
                int position = Fields.ToList().FindIndex(field => field.Definition.Name == path.Segments[0]);
                if (position < 0) throw new InvalidOperationException("transfer-field-invalid");
                changedFields.Add(position);
                DefinitionFieldState input = replacement.Inputs[position];
                string[] ordinary = [Fields[position].Definition.Id, .. path.Segments.Skip(1)];
                input.DraftTexts.RemoveAll(draft => HasPrefix(ordinary, draft.Path));
                input.InvalidPaths.RemoveAll(invalid => HasPrefix(ordinary, invalid));
                input.Origins.RemoveAll(origin => HasPrefix(ordinary, origin.Path));
                input.Origins.Add((ordinary, DefinitionAnswerOrigin.Explicit));
                if (ordinary.Length == 1)
                {
                    input.InputDiagnosticCode = null;
                    input.Origin = DefinitionAnswerOrigin.Explicit;
                }
                input.Text = input.Value is { } supplied ? DefinitionDocumentEditing.Display(supplied) : string.Empty;
                if (input.Text.Length > 65_536)
                {
                    RetireTransferReviews();
                    return new(false, "answer-budget-exceeded", InputRevision);
                }
                replacement.RootInput.DraftTexts.RemoveAll(draft => HasPrefix(path.Segments, draft.Path));
                replacement.RootInput.InvalidPaths.RemoveAll(invalid => HasPrefix(path.Segments, invalid));
                replacement.RootInput.Origins.RemoveAll(origin => HasPrefix(path.Segments, origin.Path));
                replacement.RootInput.Origins.Add((path.Segments.ToArray(), DefinitionAnswerOrigin.Explicit));
            }
            DefinitionFieldState[] retired = changedFields.Select(_editing.Input).ToArray();
            acceptedRevision = checked(InputRevision + 1);
            RetireTransferReviews();
            _editing.Adopt(replacement);
            replacement = null;
            committed = true;
            foreach (DefinitionFieldState previous in retired) previous.Retire();
            OnInputChanged();
            foreach (int position in changedFields) Fields[position].NotifyDocumentChanged();
            return new(true, "transfer-applied", acceptedRevision);
        }
        catch (Exception) when (committed)
        {
            // Notification failures and reentrant edits cannot roll back the already accepted state.
            return new(true, "transfer-applied", acceptedRevision) { ObserverFailed = true };
        }
        catch (DefinitionException)
        {
            using var mutation = BeginMutation();
            if (ReferenceEquals(_transferReview, review)) RetireTransferReviews();
            return new(false, "transfer-invalid-value", InputRevision);
        }
        catch (InvalidOperationException exception) when (exception.Message == "document-container-conflict")
        {
            using var mutation = BeginMutation();
            if (ReferenceEquals(_transferReview, review)) RetireTransferReviews();
            return new(false, "transfer-container-conflict", InputRevision);
        }
        finally { replacement?.Retire(); }

        static bool HasPrefix(IReadOnlyList<string> prefix, IReadOnlyList<string> path) =>
            path.Take(prefix.Count).SequenceEqual(prefix, StringComparer.Ordinal);
    }

    private void RetireTransferReviews()
    {
        _transferReview = null;
        _transferReviewGeneration = checked(_transferReviewGeneration + 1);
    }

    private bool TransferReviewIsCurrent(DefinitionTransitionTransferPlan review) =>
        !_disposed && ReferenceEquals(review, _transferReview)
        && ReferenceEquals(review.Owner, _transferReviewOwner)
        && review.Generation == _transferReviewGeneration && review.InputRevision == InputRevision
        && review.DefinitionDigest == Definition.Digest;

    private bool TryTransferValue(DefinitionPath target, string? targetOption, AnswerSnapshot original,
        DefinitionValidationResult live, out JsonElement value, out bool masking)
    {
        value = default;
        masking = true;
        if (!TryTransferMetadata(target, out DefinitionNode destination, out FieldSensitivity destinationSensitivity, out bool readOnly)
            || destination.PresentationMetadata.SuggestedSource is not { } suggestion || readOnly
            || !TryTransferMetadata(suggestion.Location, out DefinitionNode source, out FieldSensitivity sourceSensitivity, out _)
            || SessionPrivacy == DefinitionSessionPrivacy.BrowserNoSecrets
                && (sourceSensitivity == FieldSensitivity.Secret || destinationSensitivity == FieldSensitivity.Secret))
            return false;
        // Read custody is checked above, before touching the retained source answer.
        if (suggestion.EvaluateEligibility(live, original) != true || HasTransferDraft(suggestion.Location)
            || original.Omitted.Any(omission => DefinitionDocumentEditing.Prefix(omission, suggestion.Location))
            || !DefinitionDocumentEditing.TryRead(original.Values, suggestion.Location, out JsonElement supplied)
            || supplied.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(supplied.GetString())
            || !DefinitionValidator.ValueValid(source, supplied) || !DefinitionValidator.ValueValid(destination, supplied))
            return false;
        masking = sourceSensitivity != FieldSensitivity.Public || destinationSensitivity != FieldSensitivity.Public;
        try
        {
            JsonObject probe = JsonSerializer.SerializeToNode(original.Values)!.AsObject();
            var omitted = original.Omitted.ToHashSet();
            StageTransferSelector(probe, omitted, targetOption);
            DefinitionDocumentEditing.Set(probe, target, supplied);
            omitted.RemoveWhere(omission => DefinitionDocumentEditing.Overlaps(omission, target));
            var snapshot = new AnswerSnapshot(InputRevision, DefinitionDocumentEditing.Values(probe), omitted);
            DefinitionValidationResult validation = DefinitionValidator.Validate(Definition, snapshot);
            if (validation.ConfigurationContext is { } context && !context.RelevantLocations.Contains(target)
                || !validation.TryGetEffectiveValue(target, out JsonElement effective) || !JsonElement.DeepEquals(effective, supplied))
                return false;
        }
        catch (DefinitionException) { return false; }
        catch (InvalidOperationException exception) when (exception.Message == "document-container-conflict") { return false; }
        value = supplied;
        return true;
    }

    private void StageTransferSelector(JsonObject values, HashSet<DefinitionPath> omitted, string? targetOption)
    {
        if (targetOption is null) return;
        DefinitionPath selector = Definition.Presentation!.OutputSelector!.Location;
        DefinitionDocumentEditing.Set(values, selector, JsonSerializer.SerializeToElement(targetOption));
        omitted.RemoveWhere(omission => DefinitionDocumentEditing.Overlaps(omission, selector));
    }

    private bool HasTransferDraft(DefinitionPath path)
    {
        for (int length = 0; length <= path.Segments.Count; length++)
            if (HasInputError(DefinitionPath.FromSegments(path.Segments.Take(length)))) return true;
        return false;
    }

    private bool TryTransferMetadata(DefinitionPath path, out DefinitionNode node,
        out FieldSensitivity sensitivity, out bool readOnly)
    {
        node = Definition.Configuration;
        sensitivity = DefinitionSensitivityPolicy.SensitivityOf(node);
        readOnly = node.Keywords.TryGetProperty("readOnly", out JsonElement rootReadOnly) && rootReadOnly.GetBoolean();
        foreach (string segment in path.Segments)
        {
            if (node.Type != DefinitionNodeType.Mapping || node.Variants.Count != 0
                || !node.Properties.TryGetValue(segment, out DefinitionNode? child)) return false;
            node = child;
            sensitivity = (FieldSensitivity)Math.Max((int)sensitivity, (int)DefinitionSensitivityPolicy.SensitivityOf(node));
            readOnly |= node.Keywords.TryGetProperty("readOnly", out JsonElement flag) && flag.GetBoolean();
        }
        return node.Type is not (DefinitionNodeType.Mapping or DefinitionNodeType.Sequence);
    }

    private string TransferTitle(DefinitionPath path)
    {
        TryTransferMetadata(path, out DefinitionNode node, out _, out _);
        string? title = node.Keywords.TryGetProperty("title", out JsonElement authored) ? authored.GetString() : null;
        string schemaPath = "/configuration" + string.Concat(path.Segments.Select(segment => "/properties/"
            + segment.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal)));
        return _localization.Text(Locale, schemaPath + "/title",
            DefinitionDisplayText.Resolve(title, path.Segments[^1]))!;
    }
}
