namespace Configurator.Desktop.ViewModels.Fields;

using System.Text.Json;

public sealed class EnumFieldViewModel : FieldViewModel
{
    private static readonly string[] UnsetChoice = ["Not supplied"];
    internal EnumFieldViewModel(JsonElement schema, FieldViewModel? parent, bool listItem) : base(schema, parent, listItem)
    {
        Choices = RequiresMasking ? [] : Array.AsReadOnly(UnsetChoice
            .Concat(schema.GetProperty("choices").EnumerateArray()
                .Select(choice => Display(choice) is { Length: > 0 } text ? text : "Empty string")).ToArray());
    }
    public IReadOnlyList<string> Choices { get; }
    public int SelectedIndex
    {
        get
        {
            if (RequiresMasking) return -1;
            if (!IsProvided) return 0;
            int index = 1;
            foreach (JsonElement choice in Schema.GetProperty("choices").EnumerateArray())
            {
                if (Display(choice) == Text) return index;
                index++;
            }
            return -1;
        }
        set
        {
            if (RequiresMasking || IsDisposed || value == SelectedIndex || value < 0 || value >= Choices.Count) return;
            Load(value == 0 ? null : Schema.GetProperty("choices")[value - 1]);
            Parent?.MarkProvided();
            Commit();
        }
    }
    protected override void NotifyValue() => OnPropertyChanged(nameof(SelectedIndex));
}
