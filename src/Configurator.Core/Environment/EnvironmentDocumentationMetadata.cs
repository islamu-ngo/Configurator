namespace Configurator.Core.Environment;

public sealed record EnvironmentDocumentationMetadata
{
    public EnvironmentDocumentationMetadata(string localizationKey, string helpKey, string anchor)
    {
        LocalizationKey = localizationKey;
        HelpKey = helpKey;
        Anchor = anchor;
    }

    public string LocalizationKey { get; }
    public string HelpKey { get; }
    public string Anchor { get; }
}
