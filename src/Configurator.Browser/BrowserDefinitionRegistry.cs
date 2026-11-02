namespace Configurator.Browser;

using Configurator.Core.Definitions;

/// <summary>Admits bundled data without remote discovery or host authority.</summary>
public sealed class BrowserDefinitionRegistry
{
    private DefinitionRegistry _admitted;

    public BrowserDefinitionRegistry(IEnumerable<Definition> definitions)
    {
        Current = new DefinitionRegistry(definitions);
        _admitted = Current;
    }

    public DefinitionRegistry Current { get; private set; }

    public void Admit(Definition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var admitted = new DefinitionRegistry(_admitted.Definitions.Append(definition));
        var current = new DefinitionRegistry(Current.Definitions.Append(definition));
        _admitted = admitted;
        Current = current;
    }

    public void Replace(IEnumerable<Definition> definitions)
    {
        var replacement = new DefinitionRegistry(definitions);
        // A refreshed registry cannot relabel a revision that an active session may have pinned.
        var admitted = new DefinitionRegistry(_admitted.Definitions.Concat(replacement.Definitions));
        _admitted = admitted;
        Current = replacement;
    }

    public static BrowserDefinitionRegistry LoadBundled()
    {
        var assembly = typeof(BrowserDefinitionRegistry).Assembly;
        var definitions = new List<Definition>();
        foreach (string name in assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith("Configurator.Browser.Definitions.", StringComparison.Ordinal)
                && name.EndsWith(".json", StringComparison.Ordinal)).Order(StringComparer.Ordinal))
        {
            using Stream input = assembly.GetManifestResourceStream(name)!;
            using var bytes = new MemoryStream();
            input.CopyTo(bytes);
            definitions.Add(DefinitionCodec.ParseJson(bytes.ToArray()));
        }
        return new BrowserDefinitionRegistry(definitions);
    }
}
