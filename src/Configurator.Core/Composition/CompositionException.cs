namespace Configurator.Core.Composition;

using System.Buffers;
using System.Text;
using System.Text.Json;
using Configurator.Core.Definitions;

internal sealed class CompositionException : Exception
{
    internal CompositionException(CompositionFailureCode code) : base("Setup composition failed.") => Code = code;
    internal CompositionFailureCode Code { get; }
}
