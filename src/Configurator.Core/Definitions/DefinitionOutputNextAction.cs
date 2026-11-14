namespace Configurator.Core.Definitions;

using System.Text.Json;

/// <summary>Inert navigation metadata. Conditions are admitted runtime predicates, never executable actions.</summary>
public sealed record DefinitionOutputNextAction
{
    internal DefinitionOutputNextAction(JsonElement data)
    {
        TargetOption = data.GetProperty("targetOption").GetString()!;
        Title = data.GetProperty("title").GetString()!;
        Description = DefinitionPresentation.Text(data, "description");
        Priority = DefinitionPresentation.Text(data, "priority") ?? "available";
        ProposedWhen = data.TryGetProperty("proposedWhen", out var condition) ? condition.Clone() : null;
    }

    public string TargetOption { get; }
    public string Title { get; }
    public string? Description { get; }
    public string Priority { get; }
    public JsonElement? ProposedWhen { get; }

    /// <summary>Evaluates eligibility without changing answers; unresolved dependencies remain undecided.</summary>
    public bool? EvaluateProposal(DefinitionValidationResult validation)
    {
        ArgumentNullException.ThrowIfNull(validation);
        return ProposedWhen is not { } condition ? true : DefinitionValidator.EvaluateCondition(
            condition, validation.Values, validation.UndecidedRoots,
            deferredLocations: validation.DeferredLocations,
            unresolvedLocations: validation.UndecidedLocations);
    }
}
