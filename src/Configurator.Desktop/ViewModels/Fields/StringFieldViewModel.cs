namespace Configurator.Desktop.ViewModels.Fields;

using System.Text.Json;

public sealed class StringFieldViewModel : FieldViewModel
{
    internal StringFieldViewModel(JsonElement schema, FieldViewModel? parent, bool listItem) : base(schema, parent, listItem) { }
    public int MaxLength => 65_536;
}
