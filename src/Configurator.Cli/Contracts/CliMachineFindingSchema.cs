namespace Configurator.Cli;

public sealed record CliMachineFindingSchema
{
    public CliMachineFindingSchema(string fieldId, IReadOnlyList<string> relativePath)
    {
        FieldId = fieldId;
        RelativePath = Array.AsReadOnly(relativePath.ToArray());
    }
    public string FieldId { get; }
    public IReadOnlyList<string> RelativePath { get; }
}
