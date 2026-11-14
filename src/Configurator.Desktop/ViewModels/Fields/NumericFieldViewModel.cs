namespace Configurator.Desktop.ViewModels.Fields;

using System.Globalization;
using Configurator.Core.Definitions;

public sealed class NumericFieldViewModel : FieldViewModel
{
    internal NumericFieldViewModel(DefinitionNode schema, string name, bool required,
        FieldViewModel? parent, bool listItem, bool inheritedMasking = false)
        : base(schema, name, required, parent, listItem, inheritedMasking) { }

    public bool IsInteger => Schema.Type == DefinitionNodeType.WholeNumber;
    public decimal? Minimum => Schema.Keywords.TryGetProperty("minimum", out var value) ? value.GetDecimal() : null;
    public decimal? Maximum => Schema.Keywords.TryGetProperty("maximum", out var value) ? value.GetDecimal() : null;

    public bool IsSliderWidget => IsPublic && !RequiresMasking &&
        Presentation?.Widget == DefinitionFieldWidget.Slider &&
        Minimum is { } min && Maximum is { } max && max > min;

    public double SliderMinimum => (double)(Minimum ?? 0m);
    public double SliderMaximum => (double)(Maximum ?? 100m);
    public double SliderFrequency => IsInteger ? 1d : 0.1d;

    public double SliderValue
    {
        get
        {
            if (decimal.TryParse(Text, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal current))
            {
                double clamped = Math.Clamp((double)current, SliderMinimum, SliderMaximum);
                return clamped;
            }
            return SliderMinimum;
        }
        set
        {
            if (IsDisposed || !CanEdit || !IsSliderWidget) return;
            string formatted = IsInteger
                ? ((long)Math.Round(value)).ToString(CultureInfo.InvariantCulture)
                : Math.Round(value, 2).ToString(CultureInfo.InvariantCulture);
            if (!string.Equals(Text, formatted, StringComparison.Ordinal))
                Text = formatted;
        }
    }

    protected override void NotifyValue()
    {
        OnPropertyChanged(nameof(IsSliderWidget));
        OnPropertyChanged(nameof(SliderMinimum));
        OnPropertyChanged(nameof(SliderMaximum));
        OnPropertyChanged(nameof(SliderValue));
    }
}
