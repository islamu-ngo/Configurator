namespace Configurator.Terminal;

internal sealed record TerminalImportResult(bool IsCurrent, bool Succeeded, string Code)
{
    internal bool IsComparison { get; init; }
}
