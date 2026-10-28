namespace Configurator.Desktop.ViewModels.Fields;

using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using Configurator.Core.Definitions;

public sealed class VariantFieldViewModel : FieldViewModel
{
    private readonly ObservableCollection<FieldViewModel> _children = [];
    private DefinitionNode? _branch;
    private VariantChoice? _selectedChoice;
    private bool _loading;

    internal VariantFieldViewModel(DefinitionNode schema, string name, bool required,
        FieldViewModel? parent, bool listItem, bool inheritedMasking = false)
        : base(schema, name, required, parent, listItem, inheritedMasking)
    {
        Children = new(_children);
        Choices = Array.AsReadOnly(schema.Variants.Select((_, index) => new VariantChoice(index)).ToArray());
    }

    public ReadOnlyObservableCollection<FieldViewModel> Children { get; }
    internal override IEnumerable<FieldViewModel> Descendants => Children;
    public IReadOnlyList<VariantChoice> Choices { get; }
    public VariantChoice? SelectedChoice
    {
        get => _selectedChoice;
        set
        {
            if (IsDisposed || _loading) return;
            if (value is null) Form.SetValue(FullPath(), null);
            else Form.SelectVariant(FullPath(), value.Index);
        }
    }

    internal override void Load(JsonElement? value)
    {
        _loading = true;
        try
        {
            DefinitionNode? branch = value is { } supplied ? DefinitionValidator.SelectVariant(Schema, supplied) : null;
            if (!ReferenceEquals(branch, _branch))
            {
                foreach (FieldViewModel child in _children) child.Dispose();
                _children.Clear();
                _branch = branch;
                if (branch is not null)
                    foreach ((string name, DefinitionNode childSchema) in branch.Properties)
                        _children.Add(CreateChild(name, childSchema, branch.Required.Contains(name, StringComparer.Ordinal)));
            }
            for (int index = 0; index < Choices.Count; index++)
                Choices[index].UpdateTitle(Form.GetVariantTitle(FullPath(), index)!);
            SetProperty(ref _selectedChoice, branch is null ? null :
                Choices.Single(choice => ReferenceEquals(Schema.Variants[choice.Index], branch)), nameof(SelectedChoice));
            base.Load(value);
        }
        finally { _loading = false; }
    }

    public override void RequestFocus() => SignalFocus();
    private FieldViewModel CreateChild(string name, DefinitionNode schema, bool required)
    {
        var child = FormFieldFactory.Project(schema, name, required, this, false);
        child.AdoptRoot(Root);
        return child;
    }
    public override void Dispose()
    {
        if (IsDisposed) return;
        base.Dispose();
        _children.Clear();
        _branch = null;
        SetProperty(ref _selectedChoice, null, nameof(SelectedChoice));
    }

    public sealed class VariantChoice : ObservableObject
    {
        private string _title = "";
        internal VariantChoice(int index) => Index = index;
        public int Index { get; }
        public string Title => _title;
        internal void UpdateTitle(string title) => SetProperty(ref _title, title, nameof(Title));
    }
}
