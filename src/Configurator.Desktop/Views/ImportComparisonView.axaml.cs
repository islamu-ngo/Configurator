namespace Configurator.Desktop.Views;

using Avalonia.Controls;
using Avalonia.Markup.Xaml;

/// <summary>Renders detached import facts and deliberate commands without a workspace editor.</summary>
public sealed partial class ImportComparisonView : UserControl
{
    public ImportComparisonView() => AvaloniaXamlLoader.Load(this);
}
