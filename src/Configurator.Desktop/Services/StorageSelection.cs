namespace Configurator.Desktop.Services;

/// <summary>A picker outcome; only a selected result transfers ownership of its value.</summary>
public sealed record StorageSelection<T>(StorageSelectionStatus Status, T? Value = null) where T : class
{
    public override string ToString() => Status.ToString();
}
