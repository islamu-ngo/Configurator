namespace Configurator.Presentation;

using Configurator.Core.Definitions;

/// <summary>Transient disclosure and discovery selection; the form alone settles and notifies transitions.</summary>
internal sealed class DefinitionViewState(string selectedExperienceId)
{
    internal string SelectedExperienceId { get; set; } = selectedExperienceId;
    internal string Locale { get; set; } = "";
    internal bool OptionalEditorsExpanded { get; set; }
    internal SetupFieldDisclosure? DisclosureOverride { get; set; }
    internal DefinitionDiscoverySelection DiscoverySelection { get; set; } = new();
    internal HashSet<string> PromotedPaths { get; } = new(StringComparer.Ordinal);
    internal Dictionary<string, bool> SectionExpansion { get; } = new(StringComparer.Ordinal);
    internal Dictionary<string, bool> EditorExpansion { get; } = new(StringComparer.Ordinal);

    internal void ResetExpansion(bool clearPromotions)
    {
        SectionExpansion.Clear();
        EditorExpansion.Clear();
        if (clearPromotions) PromotedPaths.Clear();
    }

    internal void ClearEditorExpansion(string identity, bool includeSelf)
    {
        foreach (string key in EditorExpansion.Keys.Where(key => includeSelf && key == identity ||
            key.StartsWith(identity + "-", StringComparison.Ordinal)).ToArray())
            EditorExpansion.Remove(key);
    }

    internal void ClearPromotions(string identity) =>
        PromotedPaths.RemoveWhere(key => key == identity || key.StartsWith(identity + "-", StringComparison.Ordinal));

    internal void Clear()
    {
        ResetExpansion(clearPromotions: true);
        DiscoverySelection = new();
    }
}
