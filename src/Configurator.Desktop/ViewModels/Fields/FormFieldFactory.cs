namespace Configurator.Desktop.ViewModels.Fields;

using System.Text.Json;
using Configurator.Presentation;

/// <summary>Owns one workspace's binding projections and its single shared-form refresh subscription.</summary>
public sealed class FormFieldFactory : IDisposable
{
    private DefinitionFormModel? _form;
    private IReadOnlyList<FieldViewModel> _fields = [];

    public IReadOnlyList<FieldViewModel> Create(DefinitionFormModel form)
    {
        ArgumentNullException.ThrowIfNull(form);
        Dispose();
        _form = form;
        _fields = Array.AsReadOnly(form.Fields.Select(input =>
        {
            var field = Project(input.Definition.Constraints, null, false);
            field.Attach(form, input);
            return field;
        }).ToArray());
        form.InputChanged += FormChanged;
        form.ViewChanged += FormChanged;
        Refresh();
        return _fields;
    }

    public void Refresh()
    {
        if (_form is null) return;
        var validation = _form.Validate();
        var roots = validation.Fields.ToDictionary(state => state.Id, StringComparer.Ordinal);
        var diagnostics = validation.Diagnostics.Concat(_form.InputDiagnostics)
            .ToLookup(diagnostic => diagnostic.FieldId, StringComparer.Ordinal);
        foreach (var field in _fields) field.Refresh(roots[field.Id], diagnostics[field.Id].ToArray());
    }

    private void FormChanged(object? sender, EventArgs args) => Refresh();

    public void Dispose()
    {
        if (_form is not null)
        {
            _form.InputChanged -= FormChanged;
            _form.ViewChanged -= FormChanged;
        }
        foreach (var field in _fields) field.Dispose();
        _fields = [];
        _form = null;
        GC.SuppressFinalize(this);
    }

    internal static FieldViewModel Project(JsonElement schema, FieldViewModel? parent, bool listItem) =>
        schema.GetProperty("type").GetString() switch
        {
            "string" => new StringFieldViewModel(schema, parent, listItem),
            "enum" => new EnumFieldViewModel(schema, parent, listItem),
            "integer" or "number" => new NumericFieldViewModel(schema, parent, listItem),
            "boolean" => new BooleanFieldViewModel(schema, parent, listItem),
            "object" => new ObjectFieldViewModel(schema, parent, listItem),
            "list" => new ListFieldViewModel(schema, parent, listItem),
            _ => throw new InvalidOperationException("Unknown admitted field type.")
        };
}
