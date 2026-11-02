namespace Configurator.Desktop.Tests;

using Avalonia.Controls;
using Avalonia.LogicalTree;

internal static class NamedControls
{
    public static T GetNamedControl<T>(this Control root, string name) where T : Control =>
        root.GetLogicalDescendants().OfType<T>().Single(control => control.Name == name);

    public static T? FindNamedControl<T>(this Control root, string name) where T : Control =>
        root.GetLogicalDescendants().OfType<T>().SingleOrDefault(control => control.Name == name);
}
