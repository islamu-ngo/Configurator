namespace Configurator.Core.Composition;

using System.Text.Json;

public readonly record struct CompositionFailure
{
    public CompositionFailure(CompositionFailureCode code)
    {
        if (code == CompositionFailureCode.None)
            throw new ArgumentOutOfRangeException(nameof(code));
        Code = code;
    }

    public CompositionFailureCode Code { get; }

    public override string ToString() => Code.ToString();
}
