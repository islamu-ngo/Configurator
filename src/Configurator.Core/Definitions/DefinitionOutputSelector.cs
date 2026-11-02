namespace Configurator.Core.Definitions;

using System.Text.Json;

/// <summary>Binds human output choices to one admitted public root enum; selection remains an ordinary answer edit.</summary>
public sealed record DefinitionOutputSelector
{
    internal DefinitionOutputSelector(JsonElement data)
    {
        Location = DefinitionPath.Parse(data.GetProperty("pointer").GetString()!);
        Options = Array.AsReadOnly(data.GetProperty("options").EnumerateArray()
            .Select(option => new DefinitionOutputOption(option)).ToArray());
    }

    public DefinitionPath Location { get; }
    public IReadOnlyList<DefinitionOutputOption> Options { get; }
}
