namespace Configurator.Core.Environment;

public sealed record EnvironmentActivationContext
{
    private readonly string[] _capabilities;
    private readonly string[] _providers;

    public EnvironmentActivationContext(
        string topology,
        IEnumerable<string> capabilities,
        IEnumerable<string> providers)
    {
        ArgumentNullException.ThrowIfNull(topology);
        ArgumentNullException.ThrowIfNull(capabilities);
        ArgumentNullException.ThrowIfNull(providers);
        Topology = topology;
        _capabilities = capabilities.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        _providers = providers.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
    }

    public string Topology { get; }
    public IReadOnlyList<string> Capabilities => Array.AsReadOnly((string[])_capabilities.Clone());
    public IReadOnlyList<string> Providers => Array.AsReadOnly((string[])_providers.Clone());

    internal bool HasCapability(string identifier) => Array.BinarySearch(_capabilities, identifier, StringComparer.Ordinal) >= 0;
    internal bool HasProvider(string identifier) => Array.BinarySearch(_providers, identifier, StringComparer.Ordinal) >= 0;
}
