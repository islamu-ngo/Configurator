namespace Configurator.Desktop.Views;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Configurator.Desktop.ViewModels.Fields;

/// <summary>Observes native blur while a field view is attached; semantic validity remains in Presentation.</summary>
public sealed class FieldFeedbackBehavior : AvaloniaObject
{
    public static readonly AttachedProperty<bool> EnabledProperty =
        AvaloniaProperty.RegisterAttached<FieldFeedbackBehavior, Control, bool>("Enabled");

    static FieldFeedbackBehavior() =>
        EnabledProperty.Changed.AddClassHandler<Control>((control, args) =>
        {
            if (args.NewValue is true)
            {
                control.AttachedToVisualTree += Attached;
                control.DetachedFromVisualTree += Detached;
                if (control.IsLoaded) control.LostFocus += LostFocus;
            }
            else
            {
                control.AttachedToVisualTree -= Attached;
                control.DetachedFromVisualTree -= Detached;
                control.LostFocus -= LostFocus;
            }
        });

    public static bool GetEnabled(Control control) => control.GetValue(EnabledProperty);
    public static void SetEnabled(Control control, bool value) => control.SetValue(EnabledProperty, value);
    private static void Attached(object? sender, VisualTreeAttachmentEventArgs args)
    {
        if (sender is Control control) control.LostFocus += LostFocus;
    }
    private static void Detached(object? sender, VisualTreeAttachmentEventArgs args)
    {
        if (sender is Control control) control.LostFocus -= LostFocus;
    }
    private static void LostFocus(object? sender, RoutedEventArgs args)
    {
        if (sender is Control { DataContext: FieldViewModel field }) field.BlurCommand.Execute(null);
    }
}
