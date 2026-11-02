namespace Configurator.Core.Definitions;

/// <summary>Value-free validation facts for one literal editor path, including required-work obligations.</summary>
public sealed class DefinitionNodeState
{
    internal DefinitionNodeState(string fieldId, IReadOnlyList<string> relativePath,
        bool active, bool required, bool provided, bool valid, bool obligation, bool obligationReady, bool missing,
        FieldSensitivity sensitivity, DefinitionCorrectionLocation location)
    {
        FieldId = fieldId;
        RelativePath = Array.AsReadOnly(relativePath.ToArray());
        IsActive = active;
        IsRequired = active && required;
        IsProvided = provided;
        IsReady = valid;
        HasError = active && !valid;
        IsObligation = active && obligation;
        IsObligationReady = active && obligationReady;
        IsMissing = missing;
        Sensitivity = sensitivity;
        Location = location;
    }

    public string FieldId { get; }
    public IReadOnlyList<string> RelativePath { get; }
    public bool IsActive { get; }
    public bool IsRequired { get; }
    public bool IsProvided { get; }
    public bool IsReady { get; }
    public bool HasError { get; }
    public bool IsObligation { get; }
    public bool IsObligationReady { get; }
    public bool IsMissing { get; }
    public FieldSensitivity Sensitivity { get; }
    public DefinitionCorrectionLocation Location { get; }
}
