namespace Configurator.Core.Definitions;

using System.Text.Json;
using Configurator.Core.Composition;

/// <summary>Immutable value-free facts with no reference or lazy access to private evidence.</summary>
public sealed record DefinitionImportReport
{
    internal DefinitionImportReport(string definitionDigest, string artifactId, string code,
        DefinitionImportCompatibility compatibility, bool completed, bool coverage, bool inventory,
        bool autofill, IEnumerable<DefinitionImportFinding> findings, int workUnits = 0,
        int inverseNodes = 0, int validationNodes = 0)
    {
        DefinitionDigest = definitionDigest;
        ArtifactId = artifactId;
        Code = code;
        Compatibility = compatibility;
        ExecutionCompleted = completed;
        CoverageComplete = coverage;
        InventoryComplete = inventory;
        AutofillEligible = autofill;
        Findings = Array.AsReadOnly(findings.ToArray());
        WorkUnits = workUnits;
        InverseNodes = inverseNodes;
        ValidationNodes = validationNodes;
    }
    public string DefinitionDigest { get; }
    public string ArtifactId { get; }
    public string Code { get; }
    public bool ExecutionCompleted { get; }
    public DefinitionImportCompatibility Compatibility { get; }
    public bool CoverageComplete { get; }
    public bool InventoryComplete { get; }
    public bool AutofillEligible { get; }
    public IReadOnlyList<DefinitionImportFinding> Findings { get; }
    public int RetainedFindingCount => Findings.Count;
    public int? TotalFindingCount => InventoryComplete ? Findings.Count : null;
    public int WorkUnits { get; }
    public int InverseNodes { get; }
    public int ValidationNodes { get; }
}
