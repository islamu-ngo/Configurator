namespace Configurator.Core.Definitions;

using System.Text.Json;
using System.Text.Json.Nodes;

/// <summary>An inert link to a retained scalar answer, subject to explicit operator adoption.</summary>
public sealed record DefinitionSuggestedSource
{
    internal DefinitionSuggestedSource(JsonElement data)
    {
        Location = DefinitionPath.Parse(data.GetProperty("pointer").GetString()!);
        When = data.TryGetProperty("when", out JsonElement condition) ? condition.Clone() : null;
    }

    public DefinitionPath Location { get; }
    public JsonElement? When { get; }

    /// <summary>Evaluates eligibility over retained answers, including inactive branches, while preserving undecided-value guards.</summary>
    public bool? EvaluateEligibility(DefinitionValidationResult validation, AnswerSnapshot retainedAnswers)
    {
        ArgumentNullException.ThrowIfNull(validation);
        ArgumentNullException.ThrowIfNull(retainedAnswers);
        if (When is not { } condition) return true;
        // Keep supplied inactive branches while filling absent members from current effective initialization.
        JsonObject values = JsonSerializer.SerializeToNode(retainedAnswers.Values)!.AsObject();
        var pending = new Stack<(JsonObject Target, JsonElement Effective)>();
        foreach ((string name, JsonElement effective) in validation.Values)
        {
            if (!values.TryGetPropertyValue(name, out JsonNode? supplied))
                values[name] = JsonSerializer.SerializeToNode(effective);
            else if (supplied is JsonObject mapping && effective.ValueKind == JsonValueKind.Object)
                pending.Push((mapping, effective));
        }
        while (pending.TryPop(out var current))
            foreach (JsonProperty property in current.Effective.EnumerateObject())
            {
                if (!current.Target.TryGetPropertyValue(property.Name, out JsonNode? supplied))
                    current.Target[property.Name] = JsonSerializer.SerializeToNode(property.Value);
                else if (supplied is JsonObject mapping && property.Value.ValueKind == JsonValueKind.Object)
                    pending.Push((mapping, property.Value));
            }
        var conditionValues = JsonSerializer.SerializeToElement(values).EnumerateObject()
            .ToDictionary(property => property.Name, property => property.Value.Clone(), StringComparer.Ordinal);
        return DefinitionValidator.EvaluateCondition(
            condition, conditionValues, validation.UndecidedRoots,
            deferredLocations: validation.DeferredLocations,
            unresolvedLocations: validation.UndecidedLocations.Concat(retainedAnswers.Omitted).ToHashSet());
    }
}
