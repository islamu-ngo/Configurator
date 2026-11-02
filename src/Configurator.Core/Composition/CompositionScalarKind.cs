namespace Configurator.Core.Composition;

using System.Buffers;
using System.Text;
using System.Text.Json;
using Configurator.Core.Definitions;

internal enum CompositionScalarKind
{
    String,
    Integer,
    Boolean,
    Null,
    JsonNumber
}
