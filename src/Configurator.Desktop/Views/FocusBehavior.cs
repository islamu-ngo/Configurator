namespace Configurator.Desktop.Views;

using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;

/// <summary>Bridges declarative focus requests to native controls without exposing controls to viewmodels.</summary>
public sealed class FocusBehavior : AvaloniaObject
{
    public static readonly AttachedProperty<int> RequestProperty =
        AvaloniaProperty.RegisterAttached<FocusBehavior, Control, int>("Request");

    public static readonly AttachedProperty<bool> OnOpenedProperty =
        AvaloniaProperty.RegisterAttached<FocusBehavior, Control, bool>("OnOpened");

    public static readonly AttachedProperty<string?> TargetProperty =
        AvaloniaProperty.RegisterAttached<FocusBehavior, Control, string?>("Target");

    public static ICommand FocusCommand { get; } = new RelayCommand<Control>(control => control?.Focus());

    static FocusBehavior()
    {
        RequestProperty.Changed.AddClassHandler<Control>((control, args) =>
        {
            if (args.NewValue is int request && request > 0)
                Focus(control);
        });
        OnOpenedProperty.Changed.AddClassHandler<Control>((control, args) =>
        {
            if (args.NewValue is true)
                control.AttachedToVisualTree += OnAttached;
            else
                control.AttachedToVisualTree -= OnAttached;
        });
    }

    public static int GetRequest(Control control) => control.GetValue(RequestProperty);
    public static void SetRequest(Control control, int value) => control.SetValue(RequestProperty, value);
    public static bool GetOnOpened(Control control) => control.GetValue(OnOpenedProperty);
    public static void SetOnOpened(Control control, bool value) => control.SetValue(OnOpenedProperty, value);
    public static string? GetTarget(Control control) => control.GetValue(TargetProperty);
    public static void SetTarget(Control control, string? value) => control.SetValue(TargetProperty, value);

    private static void OnAttached(object? sender, VisualTreeAttachmentEventArgs args)
    {
        if (sender is Control control)
            Focus(control);
    }

    private static void Focus(Control control)
    {
        Control target = control is Window window && GetTarget(control) is { } name
            ? window.FindControl<Control>(name) ?? control : control;
        if (target.IsEffectivelyVisible && target.Focus()) return;
        Dispatcher.UIThread.Post(() =>
        {
            if (target.IsAttachedToVisualTree() && target.IsEffectivelyVisible)
                target.Focus();
        }, DispatcherPriority.Loaded);
    }
}
