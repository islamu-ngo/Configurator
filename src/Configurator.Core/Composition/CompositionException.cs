namespace Configurator.Core.Composition;

using System.Buffers;
using System.Text;
using System.Text.Json;
using Configurator.Core.Definitions;

internal sealed class CompositionException : Exception
{
    internal CompositionException(CompositionFailureCode code, CompositionSourceLocation? location = null)
        : base("Setup composition failed.")
    {
        Code = code;
        Location = location;
    }

    internal CompositionFailureCode Code { get; }
    internal CompositionSourceLocation? Location { get; }
}
