namespace Configurator.Core.Composition;

using System.Buffers;
using System.Text;
using System.Text.Json;
using Configurator.Core.Definitions;

internal sealed record CompositionMap(IReadOnlyDictionary<string, CompositionNode> Entries) : CompositionNode;
