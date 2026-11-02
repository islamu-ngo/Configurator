namespace Configurator.Core.Definitions;

using System.Text.Json;

public sealed record DefinitionFieldGuidance
{
    internal DefinitionFieldGuidance(JsonElement data)
    {
        Instructions = DefinitionPresentation.Text(data, "instructions");
        Links = Array.AsReadOnly(data.TryGetProperty("links", out var links)
            ? links.EnumerateArray().Select(link => new DefinitionGuidanceLink(
                link.GetProperty("title").GetString()!, link.GetProperty("url").GetString()!)).ToArray() : []);
        SearchTerms = DefinitionPresentation.Strings(data, "searchTerms");
    }

    public string? Instructions { get; }
    public IReadOnlyList<DefinitionGuidanceLink> Links { get; }
    public IReadOnlyList<string> SearchTerms { get; }
}
