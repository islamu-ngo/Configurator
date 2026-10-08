namespace Configurator.Browser;

using Configurator.Core.Definitions;

/// <summary>Admits bundled data without remote discovery or host authority.</summary>
public sealed class BrowserDefinitionRegistry
{
    public BrowserDefinitionRegistry(IEnumerable<SetupDefinition> definitions) =>
        Current = new SetupDefinitionRegistry(definitions);

    public SetupDefinitionRegistry Current { get; private set; }

    public void Replace(IEnumerable<SetupDefinition> definitions)
    {
        var replacement = new SetupDefinitionRegistry(definitions);
        // A refreshed registry cannot relabel a revision that an active session may have pinned.
        _ = new SetupDefinitionRegistry(Current.Definitions.Concat(replacement.Definitions));
        Current = replacement;
    }

    public static BrowserDefinitionRegistry LoadBundled()
    {
        var assembly = typeof(BrowserDefinitionRegistry).Assembly;
        var definitions = new List<SetupDefinition>();
        foreach (string name in assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith("Configurator.Browser.Definitions.", StringComparison.Ordinal)
                && name.EndsWith(".json", StringComparison.Ordinal)).Order(StringComparer.Ordinal))
        {
            using Stream input = assembly.GetManifestResourceStream(name)!;
            using var bytes = new MemoryStream();
            input.CopyTo(bytes);
            definitions.Add(SetupDefinitionCodec.ParseJson(bytes.ToArray()));
        }
        return new BrowserDefinitionRegistry(definitions);
    }
}
