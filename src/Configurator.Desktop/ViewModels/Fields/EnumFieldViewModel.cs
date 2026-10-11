namespace Configurator.Desktop.ViewModels.Fields;

using System.Text.Json;

public sealed class EnumFieldViewModel : FieldViewModel
{
    private static readonly string[] UnsetChoice = [""];
    internal EnumFieldViewModel(JsonElement schema, FieldViewModel? parent, bool listItem) : base(schema, parent, listItem)
    {
        Choices = RequiresMasking ? [] : Array.AsReadOnly(UnsetChoice
            .Concat(schema.GetProperty("choices").EnumerateArray().Select(Display)).ToArray());
    }
    public IReadOnlyList<string> Choices { get; }
    public string SelectedChoice
    {
        get => RequiresMasking ? "" : Text;
        set { if (!RequiresMasking) Text = value; }
    }
    protected override void NotifyValue() => OnPropertyChanged(nameof(SelectedChoice));
}
