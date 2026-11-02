namespace Configurator.Core.Definitions;

using System.Text.Json;
using Configurator.Core.Composition;

/// <summary>Input ordinals identify assessed content, never workspace rows or private member names.</summary>
public sealed record DefinitionImportSourceLocation
{
    internal DefinitionImportSourceLocation(IEnumerable<int> ordinals) => Ordinals = Array.AsReadOnly(ordinals.ToArray());
    public IReadOnlyList<int> Ordinals { get; }
}
