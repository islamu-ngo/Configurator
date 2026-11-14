namespace Configurator.Core.Profiles;

/// <summary>Advisory metadata only. Actual composition must validate again under its current custody and authority.</summary>
public sealed class ProfileUpgradePlan
{
    internal ProfileUpgradePlan(bool sourceSchemaAvailable, IEnumerable<DefinitionSchemaDelta> schemaDeltas,
        IEnumerable<ProfileDeltaSummary> fields)
    {
        SourceSchemaAvailable = sourceSchemaAvailable;
        SchemaDeltas = Array.AsReadOnly(schemaDeltas.ToArray());
        Fields = Array.AsReadOnly(fields.ToArray());
    }

    public bool SourceSchemaAvailable { get; }
    public IReadOnlyList<DefinitionSchemaDelta> SchemaDeltas { get; }
    public IReadOnlyList<ProfileDeltaSummary> Fields { get; }
    public bool RequiresRevalidation => true;
}
