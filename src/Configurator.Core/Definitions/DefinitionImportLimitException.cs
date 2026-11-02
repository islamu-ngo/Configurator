namespace Configurator.Core.Definitions;

using System.Text.Json;
using Configurator.Core.Composition;

internal sealed class DefinitionImportLimitException : Exception
{
    internal DefinitionImportLimitException() : base("assessment-limit") { }
}
