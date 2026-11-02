namespace Configurator.Core.Definitions;

/// <summary>One sensitivity floor for every workspace, projection and client.</summary>
public static class DefinitionSensitivityPolicy
{
    public static SetupFieldSensitivity SensitivityOf(DefinitionNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (node.Secret is not null || node.Keywords.TryGetProperty("writeOnly", out var writeOnly) && writeOnly.GetBoolean())
            return SetupFieldSensitivity.Secret;
        return node.Keywords.TryGetProperty("x-configurator", out var metadata)
            && metadata.TryGetProperty("sensitivity", out var sensitivity) ? sensitivity.GetString() switch
            {
                "public" => SetupFieldSensitivity.Public,
                "secret" => SetupFieldSensitivity.Secret,
                _ => SetupFieldSensitivity.Sensitive
            } : SetupFieldSensitivity.Sensitive;
    }

    public static bool DeclaresSecret(DefinitionNode node) => SensitivityOf(node) == SetupFieldSensitivity.Secret;

    public static bool ContainsSecret(DefinitionNode node) => DeclaresSecret(node)
        || node.Properties.Values.Any(ContainsSecret) || node.Variants.Any(ContainsSecret)
        || node.Items is not null && ContainsSecret(node.Items)
        || node.AdditionalProperties is not null && ContainsSecret(node.AdditionalProperties);
}
