namespace Configurator.Browser;

using Configurator.Core.Definitions;

/// <summary>Admits bundled data without remote discovery or host authority.</summary>
public sealed class BrowserDefinitionRegistry
{
    private SetupDefinitionRegistry _admitted;

    public BrowserDefinitionRegistry(IEnumerable<SetupDefinition> definitions)
    {
        Current = new SetupDefinitionRegistry(definitions);
        _admitted = Current;
    }

    public SetupDefinitionRegistry Current { get; private set; }

    public void Admit(SetupDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var admitted = new SetupDefinitionRegistry(_admitted.Definitions.Append(definition));
        var current = new SetupDefinitionRegistry(Current.Definitions.Append(definition));
        _admitted = admitted;
        Current = current;
    }

    public void Replace(IEnumerable<SetupDefinition> definitions)
    {
        var replacement = new SetupDefinitionRegistry(definitions);
        // A refreshed registry cannot relabel a revision that an active session may have pinned.
        var admitted = new SetupDefinitionRegistry(_admitted.Definitions.Concat(replacement.Definitions));
        _admitted = admitted;
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
