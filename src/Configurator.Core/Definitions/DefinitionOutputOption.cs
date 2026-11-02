namespace Configurator.Core.Definitions;

using System.Text.Json;

public sealed record DefinitionOutputOption
{
    internal DefinitionOutputOption(JsonElement data)
    {
        Value = data.GetProperty("value").GetString()!;
        Title = data.GetProperty("title").GetString()!;
        ArtifactIds = DefinitionPresentation.Strings(data, "artifactIds");
    }

    public string Value { get; }
    public string Title { get; }
    public IReadOnlyList<string> ArtifactIds { get; }
}
