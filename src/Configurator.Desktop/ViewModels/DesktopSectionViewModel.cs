namespace Configurator.Desktop.ViewModels;

using Configurator.Desktop.ViewModels.Fields;
using Configurator.Presentation;
using CommunityToolkit.Mvvm.ComponentModel;

/// <summary>Public section metadata and references to the workspace's existing editors.</summary>
public sealed class DesktopSectionViewModel(string id, string title, string? description,
    IReadOnlyList<FieldViewModel> fields, DefinitionFormProgress progress) : ObservableObject
{
    private string _title = title;
    private string? _description = description;
    private IReadOnlyList<FieldViewModel> _fields = fields;
    private DefinitionFormProgress _progress = progress;
    public string Id { get; } = id;
    public string Title => _title;
    public string? Description => _description;
    public IReadOnlyList<FieldViewModel> Fields => _fields;
    public DefinitionFormProgress Progress => _progress;

    internal void Refresh(string title, string? description, IReadOnlyList<FieldViewModel> fields,
        DefinitionFormProgress progress)
    {
        SetProperty(ref _title, title, nameof(Title));
        SetProperty(ref _description, description, nameof(Description));
        if (!_fields.SequenceEqual(fields)) SetProperty(ref _fields, fields, nameof(Fields));
        SetProperty(ref _progress, progress, nameof(Progress));
    }
}
