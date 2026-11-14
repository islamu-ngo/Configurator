namespace Configurator.Presentation;

using Configurator.Core.Profiles;

/// <summary>Declared field facts only; this is neither a secret value nor authority to read one.</summary>
public sealed record BlindSecretSlot(string Path, ProfileCarryEligibility Eligibility, bool IsRetained);
