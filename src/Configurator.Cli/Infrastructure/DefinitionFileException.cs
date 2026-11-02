namespace Configurator.Cli;


internal sealed class DefinitionFileException(string code) : Exception(code)
{
    internal string Code { get; } = code;
}

