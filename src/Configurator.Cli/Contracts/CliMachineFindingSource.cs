namespace Configurator.Cli;

public sealed record CliMachineFindingSource
{
    public CliMachineFindingSource(IReadOnlyList<int> ordinals) => Ordinals = Array.AsReadOnly(ordinals.ToArray());
    public IReadOnlyList<int> Ordinals { get; }
}
