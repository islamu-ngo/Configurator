namespace Configurator.Core.Composition;

using System.Buffers;
using System.Text;
using System.Text.Json;
using Configurator.Core.Definitions;

internal abstract record CompositionNode
{
    internal DefinitionSourceSpan? SourceSpan { get; init; }
}
