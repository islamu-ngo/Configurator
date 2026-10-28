namespace Configurator.Desktop.ViewModels.Fields;

using Configurator.Core.Definitions;

public sealed class NumericFieldViewModel : FieldViewModel
{
    internal NumericFieldViewModel(DefinitionNode schema, string name, bool required,
        FieldViewModel? parent, bool listItem, bool inheritedMasking = false)
        : base(schema, name, required, parent, listItem, inheritedMasking) { }
    public bool IsInteger => Schema.Type == DefinitionNodeType.WholeNumber;
    public decimal? Minimum => Schema.Keywords.TryGetProperty("minimum", out var value) ? value.GetDecimal() : null;
    public decimal? Maximum => Schema.Keywords.TryGetProperty("maximum", out var value) ? value.GetDecimal() : null;
}
