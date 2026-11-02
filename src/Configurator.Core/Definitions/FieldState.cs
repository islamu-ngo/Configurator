namespace Configurator.Core.Definitions;

public sealed class FieldState
{
    internal FieldState(FieldDefinition field, bool active, bool required, bool provided, bool error)
    {
        Definition = field;
        IsActive = active;
        IsRequired = required;
        IsProvided = provided;
        HasError = error;
    }

    public FieldDefinition Definition { get; }
    public string Id => Definition.Id;
    public bool IsActive { get; }
    public bool IsRequired { get; }
    public bool IsProvided { get; }
    public bool HasError { get; }
}

