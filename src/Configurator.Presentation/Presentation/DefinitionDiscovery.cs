namespace Configurator.Presentation;

using System.Collections.ObjectModel;
using Configurator.Core.Definitions;

public enum DefinitionDiscoveryFacet { Section, Requirement, Applicability, State, Sensitivity, Artifact }

/// <summary>Public metadata and projected facts only; never supply answers, drafts or artifact contents.</summary>
public sealed class DefinitionDiscoveryEntry
{
    public DefinitionDiscoveryEntry(string fieldId, IEnumerable<string> relativePath, string title,
        string sectionId, string sectionTitle, string type, SetupFieldSensitivity sensitivity,
        bool isActive, bool isRequired, bool isProvided, bool isReady, bool needsAttention,
        bool isSecretDeferred, DefinitionAnswerOrigin origin, IEnumerable<string>? searchTerms = null,
        string? help = null, string? instructions = null, IEnumerable<string>? outputArtifactIds = null,
        IEnumerable<string>? bindingKeys = null, int sectionOrder = 0,
        IEnumerable<string>? activeControllerFields = null)
    {
        FieldId = fieldId;
        RelativePath = Array.AsReadOnly(relativePath.ToArray());
        Title = title;
        SectionId = sectionId;
        SectionTitle = sectionTitle;
        Type = type;
        Sensitivity = sensitivity;
        IsActive = isActive;
        IsRequired = isRequired;
        IsProvided = isProvided;
        IsReady = isReady;
        NeedsAttention = needsAttention;
        IsDeferred = isSecretDeferred;
        Origin = origin;
        SearchTerms = Array.AsReadOnly(searchTerms?.ToArray() ?? []);
        Help = help;
        Instructions = instructions;
        OutputArtifactIds = Array.AsReadOnly(outputArtifactIds?.ToArray() ?? []);
        BindingKeys = Array.AsReadOnly(bindingKeys?.ToArray() ?? []);
        SectionOrder = sectionOrder;
        ActiveControllerFields = Array.AsReadOnly(activeControllerFields?.ToArray() ?? []);
    }

    public string FieldId { get; }
    public IReadOnlyList<string> RelativePath { get; }
    public string Title { get; }
    public string SectionId { get; }
    public string SectionTitle { get; }
    public string Type { get; }
    public SetupFieldSensitivity Sensitivity { get; }
    public bool IsActive { get; }
    public bool IsRequired { get; }
    public bool IsProvided { get; }
    public bool IsReady { get; }
    public bool NeedsAttention { get; }
    public bool IsDeferred { get; }
    public DefinitionAnswerOrigin Origin { get; }
    public IReadOnlyList<string> SearchTerms { get; }
    public string? Help { get; }
    public string? Instructions { get; }
    public IReadOnlyList<string> OutputArtifactIds { get; }
    public IReadOnlyList<string> BindingKeys { get; }
    public int SectionOrder { get; }
    public IReadOnlyList<string> ActiveControllerFields { get; }

    internal DefinitionDiscoveryEntry(DefinitionFormIndex.Node metadata, DefinitionFormSection section,
        DefinitionNodeState state, bool inputError, bool deferred, DefinitionAnswerOrigin origin,
        IReadOnlyList<string>? artifacts, IReadOnlyList<string>? bindingKeys, IReadOnlyList<string> controllers)
    {
        FieldId = state.FieldId;
        RelativePath = state.RelativePath;
        Title = metadata.Title ?? section.Title + " item";
        SectionId = section.Id;
        SectionTitle = section.Title;
        Type = metadata.Type;
        Sensitivity = state.Sensitivity;
        IsActive = state.IsActive;
        IsRequired = state.IsRequired;
        IsProvided = state.IsProvided;
        IsReady = state.IsActive && state.IsReady && !inputError;
        NeedsAttention = state.IsActive && (state.HasError || inputError);
        IsDeferred = state.IsActive && !inputError && deferred;
        Origin = origin;
        SearchTerms = metadata.SearchTerms;
        Help = metadata.Help;
        Instructions = metadata.Instructions;
        OutputArtifactIds = artifacts ?? [];
        BindingKeys = bindingKeys ?? [];
        ActiveControllerFields = controllers;
    }

    private DefinitionDiscoveryEntry(DefinitionDiscoveryEntry current, int sectionOrder)
    {
        FieldId = current.FieldId;
        RelativePath = current.RelativePath;
        Title = current.Title;
        SectionId = current.SectionId;
        SectionTitle = current.SectionTitle;
        Type = current.Type;
        Sensitivity = current.Sensitivity;
        IsActive = current.IsActive;
        IsRequired = current.IsRequired;
        IsProvided = current.IsProvided;
        IsReady = current.IsReady;
        NeedsAttention = current.NeedsAttention;
        IsDeferred = current.IsDeferred;
        Origin = current.Origin;
        SearchTerms = current.SearchTerms;
        Help = current.Help;
        Instructions = current.Instructions;
        OutputArtifactIds = current.OutputArtifactIds;
        BindingKeys = current.BindingKeys;
        SectionOrder = sectionOrder;
        ActiveControllerFields = current.ActiveControllerFields;
    }

    internal DefinitionDiscoveryEntry WithSectionOrder(int order) => order == SectionOrder ? this : new(this, order);
}

/// <summary>A transient query and copied facet selections. Oversized queries reject rather than truncate.</summary>
public sealed class DefinitionDiscoverySelection
{
    public DefinitionDiscoverySelection(string query = "", bool includeInactive = false,
        IReadOnlyDictionary<DefinitionDiscoveryFacet, IReadOnlyList<string>>? facets = null)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.Length > 256)
            throw new ArgumentException("discovery-query-limit", nameof(query));
        string[] tokens = query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length > 16)
            throw new ArgumentException("discovery-query-limit", nameof(query));
        Query = query.Trim();
        QueryTerms = Array.AsReadOnly(tokens);
        IncludeInactive = includeInactive;
        var snapshot = new Dictionary<DefinitionDiscoveryFacet, IReadOnlyList<string>>();
        if (facets is not null)
            foreach ((DefinitionDiscoveryFacet facet, IReadOnlyList<string> values) in facets)
                snapshot.Add(facet, Array.AsReadOnly(values.Distinct(StringComparer.OrdinalIgnoreCase).ToArray()));
        Facets = new ReadOnlyDictionary<DefinitionDiscoveryFacet, IReadOnlyList<string>>(snapshot);
    }

    public string Query { get; }
    public IReadOnlyList<string> QueryTerms { get; }
    public bool IncludeInactive { get; }
    public IReadOnlyDictionary<DefinitionDiscoveryFacet, IReadOnlyList<string>> Facets { get; }
}

public sealed record DefinitionDiscoveryMatch(DefinitionDiscoveryEntry Entry, string MatchReason, int Rank);
public sealed record DefinitionDiscoveryFacetOption(string Value, int Count);

public sealed class DefinitionDiscoverySnapshot
{
    internal DefinitionDiscoverySnapshot(IEnumerable<DefinitionDiscoveryMatch> matches,
        IDictionary<DefinitionDiscoveryFacet, IReadOnlyList<DefinitionDiscoveryFacetOption>> facets)
    {
        Matches = Array.AsReadOnly(matches.ToArray());
        Facets = new ReadOnlyDictionary<DefinitionDiscoveryFacet, IReadOnlyList<DefinitionDiscoveryFacetOption>>(
            new Dictionary<DefinitionDiscoveryFacet, IReadOnlyList<DefinitionDiscoveryFacetOption>>(facets));
    }

    public IReadOnlyList<DefinitionDiscoveryMatch> Matches { get; }
    public IReadOnlyDictionary<DefinitionDiscoveryFacet, IReadOnlyList<DefinitionDiscoveryFacetOption>> Facets { get; }
    public int TotalCount => Matches.Count;
}

/// <summary>Deterministic discovery over admitted metadata; it neither reads nor changes workspace answers.</summary>
public static class DefinitionDiscovery
{
    public static DefinitionDiscoverySnapshot Search(IEnumerable<DefinitionDiscoveryEntry> entries,
        DefinitionDiscoverySelection selection)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(selection);
        DefinitionDiscoveryEntry[] snapshot = entries.ToArray();
        DefinitionDiscoveryEntry[] queried = snapshot.Where(entry => selection.QueryTerms.All(token =>
            Metadata(entry).Any(text => text.Contains(token, StringComparison.OrdinalIgnoreCase)))).ToArray();
        var matches = queried.Where(entry => MatchesFacets(entry, selection, null))
            .Select(entry => Match(entry, selection)).ToArray();
        Array.Sort(matches, CompareMatches);
        var facets = new Dictionary<DefinitionDiscoveryFacet, IReadOnlyList<DefinitionDiscoveryFacetOption>>();
        foreach (DefinitionDiscoveryFacet facet in Enum.GetValues<DefinitionDiscoveryFacet>())
        {
            Dictionary<string, int> counts = queried.Where(entry => MatchesFacets(entry, selection, facet))
                .SelectMany(entry => Values(entry, facet).Distinct(StringComparer.OrdinalIgnoreCase))
                .GroupBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
            // Keep unavailable options visible, including selected values with no remaining matches.
            IEnumerable<string> options = snapshot.SelectMany(entry => Values(entry, facet))
                .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)
                .Concat(selection.Facets.TryGetValue(facet, out var selected) ? selected.Order(StringComparer.Ordinal) : [])
                .Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.Ordinal);
            facets.Add(facet, Array.AsReadOnly(options.Select(value =>
                new DefinitionDiscoveryFacetOption(value, counts.GetValueOrDefault(value))).ToArray()));
        }
        return new DefinitionDiscoverySnapshot(matches, facets);
    }

    private static IEnumerable<string> Metadata(DefinitionDiscoveryEntry entry) =>
        new[] { entry.FieldId, entry.Title, entry.SectionId, entry.SectionTitle,
            entry.Help ?? "", entry.Instructions ?? "" }
        .Concat(entry.RelativePath).Concat(entry.SearchTerms)
        .Concat(entry.OutputArtifactIds).Concat(entry.BindingKeys);

    private static bool MatchesFacets(DefinitionDiscoveryEntry entry, DefinitionDiscoverySelection selection,
        DefinitionDiscoveryFacet? ignored)
    {
        // Applicability counts show both options. Selecting inactive is itself deliberate inclusion.
        if (!entry.IsActive && ignored != DefinitionDiscoveryFacet.Applicability && !selection.IncludeInactive &&
            !(selection.Facets.TryGetValue(DefinitionDiscoveryFacet.Applicability, out var applicability) &&
              applicability.Contains("inactive", StringComparer.OrdinalIgnoreCase)))
            return false;
        foreach ((DefinitionDiscoveryFacet facet, IReadOnlyList<string> selected) in selection.Facets)
            if (facet != ignored && selected.Count != 0 &&
                !Values(entry, facet).Any(value => selected.Contains(value, StringComparer.OrdinalIgnoreCase)))
                return false;
        return true;
    }

    private static IEnumerable<string> Values(DefinitionDiscoveryEntry entry, DefinitionDiscoveryFacet facet)
    {
        switch (facet)
        {
            case DefinitionDiscoveryFacet.Section:
                yield return entry.SectionId;
                break;
            case DefinitionDiscoveryFacet.Requirement:
                yield return entry.IsRequired ? "required" : "optional";
                break;
            case DefinitionDiscoveryFacet.Applicability:
                yield return entry.IsActive ? "active" : "inactive";
                break;
            case DefinitionDiscoveryFacet.State:
                if (entry.NeedsAttention) yield return "needs-attention";
                if (entry.IsReady) yield return "ready";
                if (entry.IsProvided) yield return "supplied";
                if (entry.Origin == DefinitionAnswerOrigin.Defaulted) yield return "defaulted";
                if (entry.Origin == DefinitionAnswerOrigin.Omitted) yield return "omitted";
                if (entry.IsDeferred) yield return "secret-deferred";
                break;
            case DefinitionDiscoveryFacet.Sensitivity:
                yield return entry.Sensitivity switch
                {
                    SetupFieldSensitivity.Public => "public",
                    SetupFieldSensitivity.Sensitive => "sensitive",
                    SetupFieldSensitivity.Secret => "secret",
                    _ => throw new ArgumentOutOfRangeException(nameof(facet))
                };
                break;
            case DefinitionDiscoveryFacet.Artifact:
                foreach (string artifact in entry.OutputArtifactIds) yield return artifact;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(facet));
        }
    }

    private static DefinitionDiscoveryMatch Match(DefinitionDiscoveryEntry entry, DefinitionDiscoverySelection selection)
    {
        string query = selection.Query;
        if (query.Length == 0) return new(entry, "all", 0);
        if (entry.BindingKeys.Contains(query, StringComparer.OrdinalIgnoreCase)) return new(entry, "binding-key", 0);
        if (string.Equals(entry.FieldId, query, StringComparison.OrdinalIgnoreCase)) return new(entry, "field-id", 0);
        if (string.Equals(entry.Title, query, StringComparison.OrdinalIgnoreCase)) return new(entry, "title", 1);
        if (entry.Title.StartsWith(query, StringComparison.OrdinalIgnoreCase)) return new(entry, "title", 2);
        if (selection.QueryTerms.All(token => entry.Title.Contains(token, StringComparison.OrdinalIgnoreCase)))
            return new(entry, "title", 3);
        if (selection.QueryTerms.All(token => entry.SearchTerms.Any(term =>
            term.Contains(token, StringComparison.OrdinalIgnoreCase))))
            return new(entry, "search-term", 4);
        return new(entry, selection.QueryTerms.Any(token =>
            (entry.Help?.Contains(token, StringComparison.OrdinalIgnoreCase) ?? false) ||
            (entry.Instructions?.Contains(token, StringComparison.OrdinalIgnoreCase) ?? false)) ? "help" : "metadata", 5);
    }

    private static int CompareMatches(DefinitionDiscoveryMatch left, DefinitionDiscoveryMatch right)
    {
        int comparison = left.Rank.CompareTo(right.Rank);
        if (comparison != 0) return comparison;
        comparison = left.Entry.SectionOrder.CompareTo(right.Entry.SectionOrder);
        if (comparison != 0) return comparison;
        comparison = StringComparer.Ordinal.Compare(left.Entry.FieldId, right.Entry.FieldId);
        if (comparison != 0) return comparison;
        // Compare literal segments, never a dotted rendering that conflates ["a.b"] and ["a", "b"].
        for (int index = 0; index < Math.Min(left.Entry.RelativePath.Count, right.Entry.RelativePath.Count); index++)
        {
            comparison = StringComparer.Ordinal.Compare(left.Entry.RelativePath[index], right.Entry.RelativePath[index]);
            if (comparison != 0) return comparison;
        }
        return left.Entry.RelativePath.Count.CompareTo(right.Entry.RelativePath.Count);
    }
}
