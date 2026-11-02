namespace Configurator.Core.Dotenv;

using Configurator.Core.Environment;

public sealed record DotenvEntry
{
    public DotenvEntry(
        string key,
        string? value,
        DotenvEntryKind kind,
        bool isSecret,
        DotenvProvenance provenance)
    {
        ArgumentNullException.ThrowIfNull(key);
        Key = key;
        Value = value;
        Kind = kind;
        IsSecret = isSecret;
        Provenance = provenance;
    }

    public string Key { get; }
    public string? Value { get; }
    public DotenvEntryKind Kind { get; }
    public bool IsSecret { get; }
    public DotenvProvenance Provenance { get; }
    public override string ToString() => $"{nameof(DotenvEntry)}:{Kind}:{Provenance}:Protected={IsSecret}";
}
