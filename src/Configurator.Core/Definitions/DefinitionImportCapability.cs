namespace Configurator.Core.Definitions;

using System.Text.Json;
using Configurator.Core.Composition;

public sealed record DefinitionImportCapability
{
    internal DefinitionImportCapability(bool supported, string code)
    {
        InspectionSupported = supported;
        StrictAutofillSupported = supported;
        Code = code;
    }
    public bool InspectionSupported { get; }
    public bool StrictAutofillSupported { get; }
    public string Code { get; }
    public string SupportedDomain => "bounded-projection";
    public IReadOnlyList<string> CoverageLimitations { get; } = Array.AsReadOnly(new[] { "represented-content-only" });
}
