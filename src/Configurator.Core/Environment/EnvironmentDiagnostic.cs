namespace Configurator.Core.Environment;

public sealed record EnvironmentDiagnostic(string Code, string Path, string? Key, string Category)
{
    public override string ToString() => $"{Code}:{Path}:{Category}";
}
