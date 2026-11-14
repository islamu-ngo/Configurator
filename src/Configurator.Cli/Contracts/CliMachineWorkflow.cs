using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace Configurator.Cli;

/// <summary>A complete public structural inventory, exposed in bounded definition-bound pages.</summary>
public sealed record CliMachineWorkflow(int Offset, int Limit, int Total, string Digest,
    int OptionCount, int NextActionCount, int SuggestionCount, IReadOnlyList<CliMachineWorkflowItem> Items)
{
    private ReadOnlyCollection<CliMachineWorkflowItem> _items = Array.AsReadOnly(Items.ToArray());
    public IReadOnlyList<CliMachineWorkflowItem> Items
    {
        get => _items;
        init => _items = Array.AsReadOnly(value.ToArray());
    }
    public int RetainedCount => Total;
    public int ReturnedCount => Items.Count;
    public bool Terminal => checked(Offset + ReturnedCount) == Total;
    public int? NextOffset => Terminal ? null : checked(Offset + ReturnedCount);
}

/// <summary>Static identifiers and schema locations only; authored prose and predicate values are excluded.</summary>
public sealed record CliMachineWorkflowItem(string Kind, string Id, string SchemaPointer, bool Conditional)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OptionId { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TargetOption { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SourcePointer { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TargetPointer { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Priority { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ArtifactCount { get; init; }
}
