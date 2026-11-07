namespace Configurator.Desktop.ViewModels.Fields;

using Configurator.Core.Definitions;

/// <summary>Places the existing native editor at the literal workspace root without introducing an answer property.</summary>
public sealed class RootFieldViewModel : FieldViewModel
{
    internal RootFieldViewModel(DefinitionNode schema, string title)
        : base(schema, title, true, null, false)
    {
        Editor = FormFieldFactory.Project(schema, string.Empty, true, this, false);
    }

    public FieldViewModel Editor { get; }
    internal override IEnumerable<FieldViewModel> Descendants => [Editor];
}
