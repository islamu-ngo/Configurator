namespace Configurator.Core.Dotenv;

using Configurator.Core.Environment;

public enum DotenvReadinessState
{
    Ready,
    Incomplete,
    Blocked,
}
