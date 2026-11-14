namespace Configurator.Desktop.ViewModels;

using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Configurator.Core.Definitions;
using Configurator.Desktop.Resources;

/// <summary>Read-only preview of an admitted definition without creating an answer workspace.</summary>
public sealed partial class DefinitionExplorerViewModel : ObservableObject
{
    public sealed record FieldItem(
        string Id,
        string Title,
        string WireKey,
        string TypeLabel,
        bool IsRequired,
        string SensitivityLabel,
        string? Units,
        string? IfOmittedDisplay,
        string? DeprecationGuidance);

    public sealed record SectionItem(
        string Id,
        string Title,
        string? Description,
        IReadOnlyList<FieldItem> Fields);

    public sealed record OutputItem(
        string Id,
        string FileName,
        string Format);

    private readonly Action<Definition> _configureCallback;
    private Definition? _definition;

    [ObservableProperty]
    private bool _isOpen;
    [ObservableProperty]
    private string _title = string.Empty;
    [ObservableProperty]
    private string _applicationId = string.Empty;
    [ObservableProperty]
    private long _revision;
    [ObservableProperty]
    private string _digest = string.Empty;
    [ObservableProperty]
    private IReadOnlyList<SectionItem> _sections = [];
    [ObservableProperty]
    private IReadOnlyList<OutputItem> _outputs = [];

    public DefinitionExplorerViewModel(Action<Definition> configureCallback)
    {
        _configureCallback = configureCallback;
        CloseCommand = new RelayCommand(Close);
        ConfigureCommand = new RelayCommand(() =>
        {
            if (_definition is { } definition)
            {
                Close();
                _configureCallback(definition);
            }
        }, () => _definition is not null);
    }

    public IRelayCommand CloseCommand { get; }
    public IRelayCommand ConfigureCommand { get; }

    public void Show(Definition definition, DesktopStrings strings)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(strings);
        _definition = definition;
        Title = definition.Title;
        ApplicationId = definition.ApplicationId;
        Revision = definition.Revision;
        Digest = definition.Digest.ToString();

        var fieldMap = definition.Fields.ToDictionary(field => field.Name, StringComparer.Ordinal);
        var sectionList = new List<SectionItem>();
        if (definition.Presentation is { Sections.Count: > 0 } presentation)
        {
            foreach (DefinitionSection section in presentation.Sections)
            {
                var items = section.Fields
                    .Distinct(StringComparer.Ordinal)
                    .Select(id => definition.Fields.FirstOrDefault(field => string.Equals(field.Id, id, StringComparison.Ordinal)))
                    .Where(field => field is not null)
                    .Select(field => CreateFieldItem(field!, strings))
                    .ToArray();
                sectionList.Add(new SectionItem(section.Id, section.Title, section.Description, items));
            }
        }
        else
        {
            var items = definition.Fields.Select(field => CreateFieldItem(field, strings)).ToArray();
            sectionList.Add(new SectionItem("configuration", strings["Configure"], null, items));
        }

        Sections = Array.AsReadOnly(sectionList.ToArray());
        Outputs = Array.AsReadOnly(definition.Artifacts
            .Select(artifact => new OutputItem(artifact.Id, artifact.FileName, artifact.Format.ToString()))
            .ToArray());
        IsOpen = true;
        ConfigureCommand.NotifyCanExecuteChanged();
    }

    public void Close()
    {
        IsOpen = false;
        _definition = null;
        Sections = [];
        Outputs = [];
        Title = string.Empty;
        ApplicationId = string.Empty;
        Digest = string.Empty;
        Revision = 0;
        ConfigureCommand.NotifyCanExecuteChanged();
    }

    private static FieldItem CreateFieldItem(FieldDefinition field, DesktopStrings strings)
    {
        bool isPrivate = DefinitionSensitivityPolicy.SensitivityOf(field.Node) != FieldSensitivity.Public;
        DefinitionFieldPresentationMetadata meta = field.Node.PresentationMetadata;
        string title = field.Node.Keywords.TryGetProperty("title", out JsonElement t) && t.ValueKind == JsonValueKind.String
            ? t.GetString()! : field.Title;
        string? defaultText = meta.DefaultText;
        return new FieldItem(
            field.Id,
            title,
            field.Name,
            field.Node.Type.ToString(),
            field.Required,
            strings[isPrivate ? "ExplorerSensitivityPrivate" : "ExplorerSensitivityPublic"],
            meta.Units,
            string.IsNullOrEmpty(defaultText) ? null : defaultText,
            meta.DeprecationGuidance);
    }
}
