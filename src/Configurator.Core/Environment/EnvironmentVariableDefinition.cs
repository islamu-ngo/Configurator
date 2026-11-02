namespace Configurator.Core.Environment;

public sealed record EnvironmentVariableDefinition
{
    public EnvironmentVariableDefinition(
        string key,
        EnvironmentVariableCategory category,
        EnvironmentVariableSensitivity sensitivity,
        EnvironmentVariableRequirement requirement,
        string? safeDefault,
        int order,
        EnvironmentActivationExpression activation,
        string validatorId,
        EnvironmentGenerationPolicy generation,
        EnvironmentRestartBehavior restartBehavior,
        EnvironmentDocumentationMetadata documentation)
    {
        Key = key;
        Category = category;
        Sensitivity = sensitivity;
        Requirement = requirement;
        SafeDefault = safeDefault;
        Order = order;
        Activation = activation;
        ValidatorId = validatorId;
        Generation = generation;
        RestartBehavior = restartBehavior;
        Documentation = documentation;
    }

    public string Key { get; }
    public EnvironmentVariableCategory Category { get; }
    public EnvironmentVariableSensitivity Sensitivity { get; }
    public EnvironmentVariableRequirement Requirement { get; }
    public string? SafeDefault { get; }
    public int Order { get; }
    public EnvironmentActivationExpression Activation { get; }
    public string ValidatorId { get; }
    public EnvironmentGenerationPolicy Generation { get; }
    public EnvironmentRestartBehavior RestartBehavior { get; }
    public EnvironmentDocumentationMetadata Documentation { get; }
    public string LocalizationKey => Documentation.LocalizationKey;
    public string HelpKey => Documentation.HelpKey;
}
