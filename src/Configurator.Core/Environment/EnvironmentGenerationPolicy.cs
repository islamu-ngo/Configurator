namespace Configurator.Core.Environment;

public sealed record EnvironmentGenerationPolicy
{
    public EnvironmentGenerationPolicy(
        EnvironmentGenerationSurface surfaces,
        int? dotenvOrder,
        int? composeOrder,
        bool composeRequired)
    {
        Surfaces = surfaces;
        DotenvOrder = dotenvOrder;
        ComposeOrder = composeOrder;
        ComposeRequired = composeRequired;
    }

    public EnvironmentGenerationSurface Surfaces { get; }
    public int? DotenvOrder { get; }
    public int? ComposeOrder { get; }
    public bool ComposeRequired { get; }
}
