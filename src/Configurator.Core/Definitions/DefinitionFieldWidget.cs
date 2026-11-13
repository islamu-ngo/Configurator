namespace Configurator.Core.Definitions;

/// <summary>Authored editor hint. Clients may fall back to <see cref="Auto"/>; widgets never change validation.</summary>
public enum DefinitionFieldWidget
{
    Auto,
    SingleLine,
    MultiLine,
    Markdown,
    Code,
    Select,
    Radio,
    Switch,
    Slider
}
