using System.Text.Json.Serialization;

namespace Configurator.Cli;

public sealed record CliMachineReadiness
{
    private readonly string[] _missingKeys;
    private readonly string[] _blockedKeys;
    public CliMachineReadiness(string state, IEnumerable<string> missingKeys, IEnumerable<string> blockedKeys)
    {
        State = state;
        _missingKeys = Snapshot(missingKeys);
        _blockedKeys = Snapshot(blockedKeys);
    }
    public string State { get; }
    [JsonIgnore] public IReadOnlyList<string> MissingKeys => Array.AsReadOnly((string[])_missingKeys.Clone());
    [JsonIgnore] public IReadOnlyList<string> BlockedKeys => Array.AsReadOnly((string[])_blockedKeys.Clone());
    public int MissingCount => _missingKeys.Length;
    public int BlockedCount => _blockedKeys.Length;
    private static string[] Snapshot(IEnumerable<string> values) =>
        values.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
}
