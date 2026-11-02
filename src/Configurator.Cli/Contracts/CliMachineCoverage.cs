using System.Text.Json.Serialization;

namespace Configurator.Cli;

public sealed record CliMachineCoverage
{
    private readonly string[] _coveredKeys;
    private readonly string[] _missingKeys;
    public CliMachineCoverage(IEnumerable<string> coveredKeys, IEnumerable<string> missingKeys)
    {
        _coveredKeys = Snapshot(coveredKeys);
        _missingKeys = Snapshot(missingKeys);
    }
    [JsonIgnore] public IReadOnlyList<string> CoveredKeys => Array.AsReadOnly((string[])_coveredKeys.Clone());
    [JsonIgnore] public IReadOnlyList<string> MissingKeys => Array.AsReadOnly((string[])_missingKeys.Clone());
    public int CoveredCount => _coveredKeys.Length;
    public int MissingCount => _missingKeys.Length;
    public string DetailDigest { get; init; } = "";
    private static string[] Snapshot(IEnumerable<string> values) =>
        values.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
}
