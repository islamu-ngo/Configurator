namespace Configurator.Core.Profiles;

/// <summary>Value-free facts for a declared field, never a dynamic answer key or reusable row ordinal.</summary>
public sealed record ProfileDeltaSummary(string FieldPath, ProfileCarryEligibility Eligibility,
    ProfileComparisonResult Comparison = ProfileComparisonResult.Unavailable);

public enum ProfileCarryEligibility
{
    Eligible,
    Missing,
    Removed,
    Invalid,
    Inactive,
    ReplacementRequired,
    RevalidationRequired
}
