namespace Configurator.Presentation;

public readonly record struct WorkspaceId
{
    private WorkspaceId(string value) => Value = value;

    public static int MaxLength => 128;

    public string Value { get; }

    public static bool TryCreate(string? value, out WorkspaceId identifier)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > MaxLength)
        {
            identifier = default;
            return false;
        }

        identifier = new WorkspaceId(value);
        return true;
    }

    public override string ToString() => Value ?? string.Empty;
}
