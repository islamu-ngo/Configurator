namespace Configurator.Core.Definitions;

using System.Text.Json;

public sealed class ArtifactDefinition
{
    internal ArtifactDefinition(JsonElement data) => Data = data.Clone();
    internal JsonElement Data { get; }
    public string Id => Data.GetProperty("id").GetString()!;
    public DefinitionArtifactFormat Format => DefinitionArtifactVocabulary.ReadFormat(Data.GetProperty("format").GetString()!);
    public string FileName => Data.GetProperty("fileName").GetString()!;
    public DefinitionTargetUpdateMode ApplySemantics => DefinitionArtifactVocabulary.ReadTargetUpdateMode(Data.GetProperty("targetUpdateMode").GetString()!);
    public DefinitionRestartRequirement Restart => DefinitionArtifactVocabulary.ReadRestartRequirement(Data.GetProperty("restartRequirement").GetString()!);
    public JsonElement Projection => Data.GetProperty("projection").Clone();
    public override string ToString() => $"{nameof(ArtifactDefinition)}:{Id}:{Format}";
}
