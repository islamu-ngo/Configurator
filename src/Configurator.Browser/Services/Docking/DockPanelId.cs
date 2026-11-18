namespace Configurator.Browser.Services.Docking;

public sealed record DockPanelId
{
    public DockPanelId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value;
}
