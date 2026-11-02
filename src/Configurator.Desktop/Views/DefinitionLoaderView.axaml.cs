namespace Configurator.Desktop.Views;

using Avalonia.Controls;
using Avalonia.Markup.Xaml;

public sealed partial class DefinitionLoaderView : UserControl
{
    public DefinitionLoaderView() => AvaloniaXamlLoader.Load(this);
}
