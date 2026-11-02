namespace Configurator.Core.Definitions;

using System.Text.Json;
using Configurator.Core.Composition;

/// <summary>Private immutable observations reference one owned recovered snapshot, not a second answer store.</summary>
internal sealed class DefinitionImportEvidence
{
    private readonly Dictionary<DefinitionPath, DefinitionImportObservation> _observations;
    internal AnswerSnapshot Answers { get; }
    internal IReadOnlyList<DefinitionPath> Assignments { get; }

    /// <summary>Takes ownership of recovery's completed private observation dictionary.</summary>
    internal DefinitionImportEvidence(AnswerSnapshot answers, IEnumerable<DefinitionPath> assignments,
        Dictionary<DefinitionPath, DefinitionImportObservation> ownedObservations)
    {
        Answers = answers;
        Assignments = Array.AsReadOnly(assignments.ToArray());
        _observations = ownedObservations;
    }

    internal DefinitionImportObservationState State(DefinitionPath path)
    {
        if (_observations.TryGetValue(path, out DefinitionImportObservation? exact)) return exact.State;
        for (int count = path.Segments.Count - 1; count >= 0; count--)
        {
            DefinitionPath ancestor = DefinitionPath.FromSegments(path.Segments.Take(count));
            if (!_observations.TryGetValue(ancestor, out DefinitionImportObservation? observed) || !observed.CompleteSubtree) continue;
            if (observed.State == DefinitionImportObservationState.ObservedAbsence)
                return DefinitionImportObservationState.ObservedAbsence;
            if (!TryValue(ancestor, out JsonElement value)) return DefinitionImportObservationState.Unobserved;
            for (int index = count; index < path.Segments.Count; index++)
            {
                if (value.ValueKind == JsonValueKind.Object)
                {
                    if (!value.TryGetProperty(path.Segments[index], out value))
                        return DefinitionImportObservationState.ObservedAbsence;
                }
                else if (value.ValueKind == JsonValueKind.Array && path.TryGetArrayIndex(index, out int row))
                {
                    if (row >= value.GetArrayLength()) return DefinitionImportObservationState.ObservedAbsence;
                    value = value[row];
                }
                else return DefinitionImportObservationState.Unobserved;
            }
            return observed.State;
        }
        return DefinitionImportObservationState.Unobserved;
    }

    internal bool Complete(DefinitionPath path)
    {
        if (_observations.TryGetValue(path, out DefinitionImportObservation? exact)) return exact.CompleteContainer;
        for (int count = path.Segments.Count - 1; count >= 0; count--)
            if (_observations.TryGetValue(DefinitionPath.FromSegments(path.Segments.Take(count)), out var ancestor)
                && ancestor.CompleteSubtree) return State(path) != DefinitionImportObservationState.Unobserved;
        return false;
    }

    internal bool TryValue(DefinitionPath path, out JsonElement value)
    {
        if (path.Segments.Count == 0)
        {
            value = JsonSerializer.SerializeToElement(Answers.Values);
            return true;
        }
        if (!Answers.Values.TryGetValue(path.Segments[0], out value)) return false;
        for (int index = 1; index < path.Segments.Count; index++)
        {
            if (value.ValueKind == JsonValueKind.Object)
            {
                if (!value.TryGetProperty(path.Segments[index], out value)) return false;
            }
            else if (value.ValueKind == JsonValueKind.Array && path.TryGetArrayIndex(index, out int row) &&
                row < value.GetArrayLength()) value = value[row];
            else return false;
        }
        return true;
    }

    internal DefinitionImportSourceLocation? Source(DefinitionPath path)
    {
        for (int count = path.Segments.Count; count >= 0; count--)
        {
            DefinitionPath ancestor = DefinitionPath.FromSegments(path.Segments.Take(count));
            if (!_observations.TryGetValue(ancestor, out var observed)) continue;
            if (observed.Source is null || count == path.Segments.Count || !TryValue(ancestor, out JsonElement value))
                return observed.Source;
            var ordinals = observed.Source.Ordinals.ToList();
            for (int index = count; index < path.Segments.Count; index++)
            {
                if (value.ValueKind == JsonValueKind.Object)
                {
                    int ordinal = 0;
                    bool found = false;
                    foreach (JsonProperty member in value.EnumerateObject())
                    {
                        if (member.Name == path.Segments[index])
                        {
                            value = member.Value;
                            ordinals.Add(ordinal);
                            found = true;
                            break;
                        }
                        ordinal++;
                    }
                    if (!found) break;
                }
                else if (value.ValueKind == JsonValueKind.Array && path.TryGetArrayIndex(index, out int row) &&
                    row < value.GetArrayLength())
                {
                    ordinals.Add(row);
                    value = value[row];
                }
                else break;
            }
            return new(ordinals);
        }
        return null;
    }

    internal async ValueTask<DefinitionImportSourceLocation?> SourceAsync(DefinitionPath root,
        IReadOnlyList<DefinitionLocationSegment> segments, DefinitionImportWork work)
    {
        var concrete = new List<string>(root.Segments);
        foreach (DefinitionLocationSegment segment in segments)
        {
            await work.VisitAsync();
            if (segment.Kind == DefinitionLocationSegmentKind.Property)
                concrete.Add(segment.PropertyName!);
            else if (segment.Kind == DefinitionLocationSegmentKind.ArrayRow)
                concrete.Add(segment.Index!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            else
            {
                if (!_observations.TryGetValue(DefinitionPath.FromSegments(concrete), out DefinitionImportObservation? observation) ||
                    observation.MapEntries is not { } entries || segment.Index!.Value >= entries.Count) return null;
                concrete.Add(entries[segment.Index.Value]);
            }
        }
        return Source(DefinitionPath.FromSegments(concrete));
    }
}
