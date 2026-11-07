namespace Configurator.Browser.Components.Configuration;

using Configurator.Core.Definitions;
using Configurator.Presentation;
using Microsoft.AspNetCore.Components;

/// <summary>Places the literal root in the existing recursive editor without an artificial answer field.</summary>
public partial class ConfigurationRoot
{
    [Parameter, EditorRequired] public DefinitionFormModel Form { get; set; } = null!;
    [Parameter] public DefinitionValidationResult? Validation { get; set; }
    [Parameter] public CancellationToken MountCancellation { get; set; }
    [Parameter] public bool FocusFirstControl { get; set; }
    [Parameter] public long FocusGeneration { get; set; }
    [Parameter] public IReadOnlyList<string> FocusPath { get; set; } = [];
    private readonly string _id = "configuration-root-" + Guid.NewGuid().ToString("N");
    private ElementReference _heading;
    public ElementReference Heading => _heading;

    private IReadOnlyList<string> CorrectionFocusPath
    {
        get
        {
            if (!FocusFirstControl || FocusPath.Count > 0) return FocusPath;
            IReadOnlyList<string> container = [];
            foreach (var node in Form.Validate().Nodes.Where(node => node.IsActive &&
                (node.HasError || node.IsRequired && node.IsMissing)))
            {
                if (!Form.TryResolveCorrectionPath(node.FieldId, node.Location, out var location) ||
                    !Form.GetFieldValidation(location).CanEdit) continue;
                if (Form.GetEditorSchema(location)?.Type is DefinitionNodeType.Mapping or DefinitionNodeType.Sequence)
                {
                    if (container.Count == 0) container = location.Segments;
                }
                else return location.Segments;
            }
            return container;
        }
    }
}
