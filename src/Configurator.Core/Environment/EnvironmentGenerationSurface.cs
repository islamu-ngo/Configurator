namespace Configurator.Core.Environment;

[Flags]
public enum EnvironmentGenerationSurface
{
    None = 0,
    Dotenv = 1,
    Compose = 2,
    Startup = 4,
}
