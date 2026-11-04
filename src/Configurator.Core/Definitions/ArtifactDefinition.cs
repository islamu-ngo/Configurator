namespace Configurator.Core.Definitions;

using System.Text.Json;
using Configurator.Core.Dotenv;

public sealed class ArtifactDefinition
{
    internal ArtifactDefinition(JsonElement data) => Data = data.Clone();
    internal JsonElement Data { get; }
    public string Id => Data.GetProperty("id").GetString()!;
    public DefinitionArtifactFormat Format => DefinitionArtifactVocabulary.ReadFormat(Data.GetProperty("format").GetString()!);
    public string FileName => Data.GetProperty("fileName").GetString()!;
    public string Title => Data.TryGetProperty("title", out var title) ? title.GetString()! : Id;
    public string? Description => Data.TryGetProperty("description", out var description) ? description.GetString() : null;
    public IReadOnlyList<string> ConsumerIds => Data.TryGetProperty("consumerIds", out var consumers)
        ? Array.AsReadOnly(consumers.EnumerateArray().Select(item => item.GetString()!).ToArray()) : Array.Empty<string>();
    public string? Surface => Data.TryGetProperty("surface", out var surface) ? surface.GetString() : null;
    public EnvironmentDestinationDialect? EnvironmentDialect => Format != DefinitionArtifactFormat.Dotenv ? null
        : Data.TryGetProperty("environmentDialect", out var dialect) && dialect.GetString() == "compose"
            ? EnvironmentDestinationDialect.Compose : EnvironmentDestinationDialect.DockerRun;
    public DefinitionTargetUpdateMode ApplySemantics => DefinitionArtifactVocabulary.ReadTargetUpdateMode(Data.GetProperty("targetUpdateMode").GetString()!);
    public DefinitionRestartRequirement Restart => DefinitionArtifactVocabulary.ReadRestartRequirement(Data.GetProperty("restartRequirement").GetString()!);
    public JsonElement Projection => Data.GetProperty("projection").Clone();
    public override string ToString() => $"{nameof(ArtifactDefinition)}:{Id}:{Format}";
}
