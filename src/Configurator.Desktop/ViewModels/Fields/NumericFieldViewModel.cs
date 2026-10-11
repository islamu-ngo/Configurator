namespace Configurator.Desktop.ViewModels.Fields;

using System.Text.Json;

public sealed class NumericFieldViewModel : FieldViewModel
{
    internal NumericFieldViewModel(JsonElement schema, FieldViewModel? parent, bool listItem) : base(schema, parent, listItem) { }
    public bool IsInteger => Schema.GetProperty("type").GetString() == "integer";
    public decimal? Minimum => Schema.TryGetProperty("minimum", out var value) ? value.GetDecimal() : null;
    public decimal? Maximum => Schema.TryGetProperty("maximum", out var value) ? value.GetDecimal() : null;
}
