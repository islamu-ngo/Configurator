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

    private static readonly AttachedProperty<Action?> CancelPendingProperty =
        AvaloniaProperty.RegisterAttached<FocusBehavior, Control, Action?>("CancelPending");

    public static ICommand FocusCommand { get; } = new RelayCommand<Control>(control => control?.Focus());

    static FocusBehavior()
    {
        TargetProperty.Changed.AddClassHandler<Control>((control, _) =>
        {
            if (GetRequest(control) > 0) Focus(control);
        });
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
        control.GetValue(CancelPendingProperty)?.Invoke();
        static Control Resolve(Control owner) => owner is Window window && GetTarget(owner) is { } name
            ? window.FindControl<Control>(name) ?? owner : owner;
        Control target = Resolve(control);
        if (target.IsEffectivelyVisible && target.IsEffectivelyEnabled && target.Focus()) return;

        void Cancel()
        {
            target.PropertyChanged -= Ready;
            target.AttachedToVisualTree -= Attached;
            control.DetachedFromVisualTree -= Detached;
            control.ClearValue(CancelPendingProperty);
        }
        void TryFocus()
        {
            if (target.IsAttachedToVisualTree() && target.IsEffectivelyVisible &&
                target.IsEffectivelyEnabled && target.Focus()) Cancel();
        }
        void Ready(object? sender, AvaloniaPropertyChangedEventArgs args) => TryFocus();
        void Attached(object? sender, VisualTreeAttachmentEventArgs args) => TryFocus();
        void Detached(object? sender, VisualTreeAttachmentEventArgs args) => Cancel();
        target.PropertyChanged += Ready;
        target.AttachedToVisualTree += Attached;
        control.DetachedFromVisualTree += Detached;
        Action cancel = Cancel;
        control.SetValue(CancelPendingProperty, cancel);
        Dispatcher.UIThread.Post(() =>
        {
            if (ReferenceEquals(control.GetValue(CancelPendingProperty), cancel)) TryFocus();
        }, DispatcherPriority.Loaded);
    }
}
