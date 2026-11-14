namespace Configurator.Presentation;

using Configurator.Core;
using Configurator.Core.Profiles;

/// <summary>Value-free profile identity and advisory upgrade facts for ordinary presentation.</summary>
public sealed record ProfileComparisonProjection(Guid ProfileId, Guid SnapshotId, ProfileProtectionMode Mode,
    ArtifactDigest DefinitionDigest, ProfileUpgradePlan Upgrade, IReadOnlyList<BlindSecretSlot> Slots);
