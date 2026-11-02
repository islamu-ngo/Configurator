namespace Configurator.Core.Definitions;

public enum DefinitionTargetUpdateMode
{
    Unspecified = 1,
    Create,
    Merge,
    Replace,
    Import,
    Reconcile,
    GenerationOnly
}
