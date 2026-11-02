namespace Configurator.Presentation;

using System.Globalization;

public readonly record struct OperationGeneration(long Value)
{
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}
