namespace Configurator.Desktop.ViewModels.Fields;

using Configurator.Core.Definitions;
using System.Collections.ObjectModel;

public sealed class ObjectFieldViewModel : FieldViewModel
{
    private readonly ObservableCollection<FieldViewModel> _visibleChildren = [];
    private bool _isExpanded;
    internal ObjectFieldViewModel(DefinitionNode schema, string name, bool required,
        FieldViewModel? parent, bool listItem, bool inheritedMasking = false)
        : base(schema, name, required, parent, listItem, inheritedMasking)
    {
        _isExpanded = parent is null or RootFieldViewModel;
        VisibleChildren = new(_visibleChildren);
        Children = Array.AsReadOnly(schema.Properties
            .Select(property => FormFieldFactory.Project(property.Value, property.Key,
                schema.Required.Contains(property.Key, StringComparer.Ordinal), this, false)).ToArray());
    }
    public IReadOnlyList<FieldViewModel> Children { get; }
    public ReadOnlyObservableCollection<FieldViewModel> VisibleChildren { get; }
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (!IsDisposed && SetProperty(ref _isExpanded, value)) RefreshChildren();
        }
    }
    internal override IEnumerable<FieldViewModel> Descendants => Children;
    protected override void OnRefreshed() => RefreshChildren();
    private void RefreshChildren() =>
        FieldProjectionCollection.Synchronize(_visibleChildren, IsExpanded ? Children.ToArray() : []);
    public override void Dispose()
    {
        if (IsDisposed) return;
        _visibleChildren.Clear();
        base.Dispose();
    }
}
