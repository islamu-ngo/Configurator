namespace Configurator.Core.Profiles;

using System.Collections.ObjectModel;
using System.Text.Json;

/// <summary>Public values and stripped structure; commitments reveal equality and are not anonymous data.</summary>
public sealed class CompareBaselineSnapshot
{
    internal CompareBaselineSnapshot(JsonElement document)
    {
        Document = document.Clone();
        ProfileId = document.GetProperty("profileId").GetGuid();
        SnapshotId = document.GetProperty("snapshotId").GetGuid();
        FamilyId = document.GetProperty("familyId").GetGuid();
        DefinitionRevision = document.GetProperty("definitionRevision").GetInt64();
        DefinitionDigest = ArtifactDigest.Parse(document.GetProperty("definitionDigest").GetString()!);
        PublicValues = document.GetProperty("values").Clone();
        Structure = document.GetProperty("structure").Clone();
        KeyId = document.GetProperty("keyId").ValueKind == JsonValueKind.Null
            ? null : document.GetProperty("keyId").GetGuid();
        ContextTag = document.GetProperty("contextTag").GetString();
        Commitments = new ReadOnlyDictionary<string, string>(document.GetProperty("commitments").EnumerateObject()
            .ToDictionary(property => property.Name, property => property.Value.GetString()!, StringComparer.Ordinal));
    }

    internal JsonElement Document { get; }
    public Guid ProfileId { get; }
    public Guid SnapshotId { get; }
    public Guid FamilyId { get; }
    public long DefinitionRevision { get; }
    public ArtifactDigest DefinitionDigest { get; }
    public JsonElement PublicValues { get; }
    public JsonElement Structure { get; }
    public Guid? KeyId { get; }
    public string? ContextTag { get; }
    public IReadOnlyDictionary<string, string> Commitments { get; }
    public override string ToString() => nameof(CompareBaselineSnapshot);
}

public enum ProfileComparisonResult
{
    Unavailable,
    Unchanged,
    Changed
}
