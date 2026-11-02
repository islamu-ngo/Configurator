namespace Configurator.Core.Dotenv;

using Configurator.Core.Environment;

public enum DotenvEntryKind
{
    EmptyPlaceholder,
    SafeDefault,
    LocalHumanValue,
    GeneratedValueReference,
}
