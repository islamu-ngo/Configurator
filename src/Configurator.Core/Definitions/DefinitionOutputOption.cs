namespace Configurator.Core.Definitions;

using System.Text.Json;

public sealed record DefinitionOutputOption
{
    internal DefinitionOutputOption(JsonElement data)
    {
        Value = data.GetProperty("value").GetString()!;
        Title = data.GetProperty("title").GetString()!;
        Description = DefinitionPresentation.Text(data, "description");
        ArtifactIds = DefinitionPresentation.Strings(data, "artifactIds");
        NextActions = Array.AsReadOnly(data.TryGetProperty("nextActions", out var actions)
            ? actions.EnumerateArray().Select(action => new DefinitionOutputNextAction(action)).ToArray() : []);
    }

    public string Value { get; }
    public string Title { get; }
    public string? Description { get; }
    public IReadOnlyList<string> ArtifactIds { get; }
    public IReadOnlyList<DefinitionOutputNextAction> NextActions { get; }
}
